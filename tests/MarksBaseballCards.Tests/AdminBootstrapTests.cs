using System.Buffers.Binary;
using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using MarksBaseballCards.AdminBootstrap;
using MarksBaseballCards.Api.Data;
using MarksBaseballCards.Api.Entities;
using MarksBaseballCards.Shared.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MarksBaseballCards.Tests;

public sealed class AdminBootstrapTests
{
    private const string Password = "Fixture-only-long-passphrase!2026";
    private static string Template => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "bootstrap-admin.sql"));
    private static string Hash(string sql) => Regex.Match(sql, "DECLARE @AdminPasswordHash nvarchar\\(256\\) = N'([^']+)';").Groups[1].Value;

    [Fact]
    public void GeneratesSaltedCompatibleHashAndEscapesSqlEmail()
    {
        var first = AdminSqlGenerator.Generate(Template, "o'brien@example.test", Password);
        var hash = Hash(first);
        Assert.DoesNotContain(Password, first);
        Assert.Contains("N'o''brien@example.test'", first);
        Assert.NotEqual(hash, Hash(AdminSqlGenerator.Generate(Template, "o'brien@example.test", Password)));
        var payload = Convert.FromBase64String(hash);
        Assert.Equal(1, payload[0]);
        Assert.Equal(2u, BinaryPrimitives.ReadUInt32BigEndian(payload.AsSpan(1, 4)));
        Assert.Equal(210_000u, BinaryPrimitives.ReadUInt32BigEndian(payload.AsSpan(5, 4)));
        Assert.Equal(PasswordVerificationResult.Success, new PasswordHasher<AppUser>().VerifyHashedPassword(new(), hash, Password));
        Assert.Equal(PasswordVerificationResult.Failed, new PasswordHasher<AppUser>().VerifyHashedPassword(new(), hash, "incorrect"));
    }

    [Theory]
    [InlineData("not-an-email", Password)]
    [InlineData("$(SQLCMD)@example.test", Password)]
    [InlineData("admin@example.test\nGO", Password)]
    [InlineData("admin@example.test", "short")]
    [InlineData("admin@example.test", "CHANGE_ME_not_a_real_password")]
    public void RefusesUnsafeOrPlaceholderCredentials(string email, string password) =>
        Assert.Throws<ArgumentException>(() => AdminSqlGenerator.Generate(Template, email, password));

    [Fact]
    public async Task GeneratedAdminCanSignInAndLockedAccountsDoNotRevealTheirExistence()
    {
        using var host = new TestHost(); await host.InitializeAsync();
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = new AppUser { Username = "admin@example.test", PasswordHash = Hash(AdminSqlGenerator.Generate(Template, "admin@example.test", Password)), Role = "Admin", CreatedAtUtc = DateTime.UtcNow };
        db.Users.Add(user); await db.SaveChangesAsync();
        using var visitor = host.Client();
        var response = await visitor.PostAsJsonAsync("api/auth/login", new LoginRequest { Username = user.Username, Password = Password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        var login = (await response.Content.ReadFromJsonAsync<LoginResponse>())!;
        visitor.DefaultRequestHeaders.Authorization = new("Bearer", login.Token);
        Assert.Equal(HttpStatusCode.OK, (await visitor.GetAsync("api/cards/page")).StatusCode);
        visitor.DefaultRequestHeaders.Authorization = null;
        user.LockoutEndUtc = DateTime.UtcNow.AddMinutes(15); await db.SaveChangesAsync();
        var locked = await visitor.PostAsJsonAsync("api/auth/login", new LoginRequest { Username = user.Username, Password = Password });
        var unknown = await visitor.PostAsJsonAsync("api/auth/login", new LoginRequest { Username = "unknown@example.test", Password = Password });
        Assert.Equal(HttpStatusCode.Unauthorized, locked.StatusCode);
        Assert.Equal(await unknown.Content.ReadAsStringAsync(), await locked.Content.ReadAsStringAsync());
    }

    [SqlServerFact]
    public async Task SqlScriptCreatesAdminAndRerunDoesNotResetPassword()
    {
        using var host = new TestHost(); await host.InitializeAsync(); using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        // Only adapt the expected database guard inside this isolated test; the shipped script stays production-scoped.
        var template = Template.Replace("N'Website_Application_MarksBaseballCardsDb'", $"N'{db.Database.GetDbConnection().Database}'", StringComparison.Ordinal);
        var sql = AdminSqlGenerator.Generate(template, "o'brien@example.test", Password);
        await db.Database.ExecuteSqlRawAsync(sql);
        var hash = (await db.Users.AsNoTracking().SingleAsync()).PasswordHash;
        await db.Database.ExecuteSqlRawAsync(AdminSqlGenerator.Generate(template, "o'brien@example.test", "Different-fixture-password!2026"));
        Assert.Equal(hash, (await db.Users.AsNoTracking().SingleAsync()).PasswordHash);
        Assert.Equal("Admin", (await db.Users.AsNoTracking().SingleAsync()).Role);
    }

    [Theory]
    [InlineData("Encrypt=False;TrustServerCertificate=False;User ID=marks_app")]
    [InlineData("Encrypt=True;TrustServerCertificate=True;User ID=marks_app")]
    [InlineData("Encrypt=True;TrustServerCertificate=False;User ID=sa")]
    [InlineData("Encrypt=True;TrustServerCertificate=False;Persist Security Info=True;User ID=marks_app")]
    public void RejectsUnsafeProductionSqlConfiguration(string options) =>
        Assert.Throws<InvalidOperationException>(() => MarksBaseballCards.Api.Auth.ProductionSecurity.ValidateSqlConnection("Server=localhost;Database=example;" + options));

    [Fact]
    public void AcceptsCertificateValidatedEncryptedSql() =>
        MarksBaseballCards.Api.Auth.ProductionSecurity.ValidateSqlConnection("Server=localhost;Database=example;Encrypt=True;TrustServerCertificate=False;User ID=marks_app");
}
