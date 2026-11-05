using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using MarksBaseballCards.Api.Auth;
using MarksBaseballCards.Api.Data;
using MarksBaseballCards.Api.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace MarksBaseballCards.Tests;

internal static class TestBootstrap
{
    public const string Key = "Isolated-tests-only-not-a-deployment-signing-key-2026";
    [ModuleInitializer]
    public static void Initialize()
    {
        // This assembly exists only in the isolated test process. Never discover a live .env.
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");
        Environment.SetEnvironmentVariable("ConnectionStrings__Default", "Server=localhost;Database=MarksCardsTests_unused;Integrated Security=True");
        Environment.SetEnvironmentVariable("Jwt__Key", Key);
        Environment.SetEnvironmentVariable("Jwt__Issuer", "MarksBaseballCards");
        Environment.SetEnvironmentVariable("Jwt__Audience", "MarksBaseballCardsUsers");
        Environment.SetEnvironmentVariable("Database__InitializeOnStartup", "false");
    }
}

public sealed class TestHost : WebApplicationFactory<Program>
{
    private bool _disposed;
    private bool _initialized;
    public string DirectoryPath { get; } = Path.Combine(Path.GetTempPath(), "MarksCardsTests_" + Guid.NewGuid().ToString("N"));
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Directory.CreateDirectory(DirectoryPath);
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> { ["Photos:RootPath"] = Path.Combine(DirectoryPath, "photos") }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<AppDbContext>();
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
            var sql = Environment.GetEnvironmentVariable("MARKS_TEST_SQLSERVER");
            if (sql is not null)
            {
                var parsed = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(sql);
                var server = parsed.DataSource.Split(',')[0];
                var isolatedService = server == "sqlserver" && Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true";
                if (!(server.Equals("localhost", StringComparison.OrdinalIgnoreCase) || server == "127.0.0.1" || isolatedService)
                    || !parsed.InitialCatalog.StartsWith("MarksCardsTests_", StringComparison.Ordinal))
                    throw new InvalidOperationException("SQL tests require a dedicated MarksCardsTests_ database on localhost.");
                // Every factory gets a unique database, including under parallel execution.
                parsed.InitialCatalog = "MarksCardsTests_" + Guid.NewGuid().ToString("N");
                services.AddDbContext<AppDbContext>(options => options.UseSqlServer(parsed.ConnectionString));
            }
            else services.AddDbContext<AppDbContext>(options => options.UseSqlite("Data Source=" + Path.Combine(DirectoryPath, "test.db") + ";Pooling=False"));
        });
    }
    public async Task InitializeAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (Environment.GetEnvironmentVariable("MARKS_TEST_SQLSERVER") is not null)
            await db.Database.MigrateAsync(); // Exercise the real SQL Server migration chain in isolated CI.
        else await db.Database.EnsureCreatedAsync(); // SQL Server migrations cannot certify SQLite.
        _initialized = true;
    }
    public HttpClient Client(string? role = null)
    {
        var client = CreateClient();
        if (role is not null)
        {
            var service = new TokenService(Options.Create(new JwtOptions { Key = TestBootstrap.Key, Issuer = "MarksBaseballCards", Audience = "MarksBaseballCardsUsers", ExpiryMinutes = 10 }));
            var (token, _) = service.CreateToken(new AppUser { Id = 1, Username = "test-owner", Role = role });
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        return client;
    }
    public override async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        if (_initialized && Environment.GetEnvironmentVariable("MARKS_TEST_SQLSERVER") is not null)
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var name = db.Database.GetDbConnection().Database;
            if (!name.StartsWith("MarksCardsTests_", StringComparison.Ordinal)) throw new InvalidOperationException("Refusing database cleanup.");
            db.Database.EnsureDeleted(); // Only the unique, loopback-only test database created above.
        }
        await base.DisposeAsync();
        var directory = new DirectoryInfo(DirectoryPath);
        var temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!directory.Name.StartsWith("MarksCardsTests_", StringComparison.Ordinal) || !string.Equals(directory.Parent?.FullName, temp, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new InvalidOperationException("Refusing unexpected test-directory cleanup.");
        if (directory.Exists) directory.Delete(true);
    }
}
