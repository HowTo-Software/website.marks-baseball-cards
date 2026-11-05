using System.Buffers.Binary;
using System.Net;
using System.Net.Http.Json;
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

    [Fact]
    public void GeneratesSaltedCompatibleHashAndBindsEmailAsSqlParameter()
    {
        var credentials = AdminCredentials.Create("o'brien@example.test", Password);
        var hash = credentials.PasswordHash;
        using var connection = new Microsoft.Data.SqlClient.SqlConnection();
        using var command = AdminBootstrapCommand.Create(connection, Template, credentials);
        Assert.Equal(Template, command.CommandText);
        Assert.DoesNotContain(credentials.Email, command.CommandText);
        Assert.DoesNotContain(hash, command.CommandText);
        Assert.DoesNotContain(Password, command.CommandText);
        Assert.Equal(credentials.Email, command.Parameters["@AdminEmail"].Value);
        Assert.Equal(hash, command.Parameters["@AdminPasswordHash"].Value);
        Assert.NotEqual(hash, AdminCredentials.Create(credentials.Email, Password).PasswordHash);
        Assert.DoesNotContain(credentials.Email, credentials.ToString());
        Assert.DoesNotContain(hash, credentials.ToString());
        var payload = Convert.FromBase64String(hash);
        Assert.Equal(1, payload[0]);
        Assert.Equal(2u, BinaryPrimitives.ReadUInt32BigEndian(payload.AsSpan(1, 4)));
        Assert.Equal(210_000u, BinaryPrimitives.ReadUInt32BigEndian(payload.AsSpan(5, 4)));
        Assert.Equal(PasswordVerificationResult.Success, new PasswordHasher<AppUser>().VerifyHashedPassword(new(), hash, Password));
        Assert.Equal(PasswordVerificationResult.Failed, new PasswordHasher<AppUser>().VerifyHashedPassword(new(), hash, "incorrect"));
    }

    [Theory]
    [InlineData("not-an-email", Password)]
    [InlineData("Owner <admin@example.test>", Password)]
    [InlineData("admin@example.test\nGO", Password)]
    [InlineData("admin@example.test", "short")]
    [InlineData("admin@example.test", "CHANGE_ME_not_a_real_password")]
    public void RefusesUnsafeOrPlaceholderCredentials(string email, string password) =>
        Assert.Throws<ArgumentException>(() => AdminCredentials.Create(email, password));

    [Fact]
    public async Task GeneratedAdminCanSignInAndLockedAccountsDoNotRevealTheirExistence()
    {
        using var host = new TestHost(); await host.InitializeAsync();
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = new AppUser { Username = "admin@example.test", PasswordHash = AdminCredentials.Create("admin@example.test", Password).PasswordHash, Role = "Admin", CreatedAtUtc = DateTime.UtcNow };
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
        await db.Database.OpenConnectionAsync();
        var connection = (Microsoft.Data.SqlClient.SqlConnection)db.Database.GetDbConnection();
        using (var command = AdminBootstrapCommand.Create(connection, template, AdminCredentials.Create("o'brien@example.test", Password)))
            await command.ExecuteNonQueryAsync();
        var hash = (await db.Users.AsNoTracking().SingleAsync()).PasswordHash;
        using (var command = AdminBootstrapCommand.Create(connection, template, AdminCredentials.Create("o'brien@example.test", "Different-fixture-password!2026")))
            await command.ExecuteNonQueryAsync();
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
