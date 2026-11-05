using System.Net.Mail;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace MarksBaseballCards.AdminBootstrap;

public static class AdminSqlGenerator
{
    public static string Generate(string template, string email, string password)
    {
        email = email.Trim();
        if (email.Length > 64 || !MailAddress.TryCreate(email, out var address) || address.Address != email
            || email.Contains('\r') || email.Contains('\n') || email.Contains('$'))
            throw new ArgumentException("Enter a valid email of up to 64 characters; SQLCMD substitution characters are not supported.");
        if (password.Length < 16 || password.Length > 128 || password.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Use a unique password or passphrase of 16 to 128 characters.");
        if (!template.Contains("N'REPLACE_WITH_ADMIN_EMAIL'", StringComparison.Ordinal)
            || !template.Contains("N'REPLACE_WITH_IDENTITY_HASH'", StringComparison.Ordinal))
            throw new ArgumentException("The reviewed bootstrap SQL template is required.");

        var user = new BootstrapIdentity();
        var hasher = new PasswordHasher<BootstrapIdentity>(Options.Create(new PasswordHasherOptions
        {
            CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV3,
            IterationCount = 210_000
        }));
        var hash = hasher.HashPassword(user, password);
        // Replace each declaration only, leaving the template's placeholder guards intact.
        return template.Replace("DECLARE @AdminEmail nvarchar(64) = N'REPLACE_WITH_ADMIN_EMAIL';",
                $"DECLARE @AdminEmail nvarchar(64) = N'{email.Replace("'", "''", StringComparison.Ordinal)}';", StringComparison.Ordinal)
            .Replace("DECLARE @AdminPasswordHash nvarchar(256) = N'REPLACE_WITH_IDENTITY_HASH';",
                $"DECLARE @AdminPasswordHash nvarchar(256) = N'{hash}';", StringComparison.Ordinal);
    }

    private sealed class BootstrapIdentity;
}
