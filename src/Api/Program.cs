using System.Text;
using System.Threading.RateLimiting;
using MarksBaseballCards.Api.Auth;
using MarksBaseballCards.Api.Data;
using MarksBaseballCards.Api.Payments;
using MarksBaseballCards.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.HttpOverrides;
using System.Net;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);
if (!builder.Environment.IsDevelopment() && !builder.Environment.IsEnvironment("Testing") && Environment.Version < new Version(10, 0, 14))
    throw new InvalidOperationException("Production requires the patched .NET 10.0.14 runtime or later.");

// ---- Configuration ----
var connectionString = builder.Configuration.GetConnectionString("Default");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "ConnectionStrings:Default is not configured. Set it via the .env file or environment variables.");
}

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
if (builder.Environment.IsProduction()) ProductionSecurity.ValidateSqlConnection(connectionString);
builder.Services.AddHttpsRedirection(options => options.HttpsPort = 443);
builder.Services.Configure<StripeOptions>(builder.Configuration.GetSection(StripeOptions.SectionName));

var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
if (string.IsNullOrWhiteSpace(jwt.Key) || jwt.Key.Length < 32 || jwt.Key.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase))
{
    throw new InvalidOperationException("Jwt:Key must be configured and at least 32 characters long.");
}

// ---- Persistence (SQL Server configured by the operator) ----
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString, sql => { sql.CommandTimeout(10); sql.EnableRetryOnFailure(1, TimeSpan.FromSeconds(1), null); }));

// ---- Application services ----
builder.Services.AddSingleton<TokenService>();
builder.Services.AddScoped<HistoryWriter>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddSingleton<PhotoStorage>();
builder.Services.AddRequestTimeouts(options => options.AddPolicy("reads", TimeSpan.FromSeconds(12)));
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 1;
    foreach (var address in builder.Configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? [])
    {
        if (!IPAddress.TryParse(address, out var ip)) throw new InvalidOperationException("Configure valid trusted reverse-proxy IP addresses.");
        options.KnownProxies.Add(ip);
    }
});

// ---- Authentication / Authorization ----
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            RoleClaimType = TokenService.RoleClaim,
            NameClaimType = TokenService.NameClaim
        };
    });
builder.Services.AddAuthorization();

// ---- Brute-force protection: rate limit the login endpoint per client IP ----
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("photos", context => RateLimitPartition.GetFixedWindowLimiter(
        context.User.Identity?.Name ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.AddPolicy("login", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

var app = builder.Build();

// ---- Apply migrations and seed (retry while the database becomes reachable) ----
// Production migrations are reviewed SQL scripts, not a side effect of starting the web host.
var initializeOnly = args.Contains("--initialize-database", StringComparer.Ordinal);
if (initializeOnly || builder.Configuration.GetValue("Database:InitializeOnStartup", false)) await InitializeDatabaseAsync(app);
if (initializeOnly) return;

// ---- HTTP pipeline ----
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
else
{
    // Return clean ProblemDetails JSON for unhandled exceptions instead of a stack trace.
    app.UseExceptionHandler();
}

app.UseForwardedHeaders();
app.Use(async (context, next) =>
{
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    await next();
});
if (!app.Environment.IsDevelopment()) app.UseHsts();
if (app.Environment.IsProduction()) app.UseWhen(context => context.Request.Path != "/health/live", pipeline => pipeline.UseHttpsRedirection());
app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

app.UseRouting();
app.UseRequestTimeouts();
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/health/live", () => Results.Ok()).AllowAnonymous().ExcludeFromDescription();
app.MapFallbackToFile("index.html");

app.Run();

static async Task InitializeDatabaseAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    var db = services.GetRequiredService<AppDbContext>();
    var config = services.GetRequiredService<IConfiguration>();

    const int maxAttempts = 12;
    for (var attempt = 1; ; attempt++)
    {
        try
        {
            await db.Database.MigrateAsync();
            await DbSeeder.SeedAsync(db, config, logger);
            logger.LogInformation("Database initialized successfully.");
            return;
        }
        catch (Exception ex) when (attempt < maxAttempts)
        {
            logger.LogWarning(ex,
                "Database not ready (attempt {Attempt}/{Max}). Retrying in 5s...", attempt, maxAttempts);
            await Task.Delay(TimeSpan.FromSeconds(5));
        }
    }
}

public partial class Program { }
