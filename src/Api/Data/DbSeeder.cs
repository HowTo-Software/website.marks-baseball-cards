using MarksBaseballCards.Api.Entities;
using MarksBaseballCards.Api.Auth;
using MarksBaseballCards.Shared.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MarksBaseballCards.Api.Data;

/// <summary>Initializes explicitly configured staff only. Live card data is entered by the owner.</summary>
public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db, IConfiguration config, ILogger logger)
    {
        await SeedUsersAsync(db, config, logger);
        await SeedConfiguredStaffAsync(db, config, logger);
    }

    private static async Task SeedUsersAsync(AppDbContext db, IConfiguration config, ILogger logger)
    {
        if (await db.Users.AnyAsync())
        {
            return;
        }

        var adminPassword = config["Seed:AdminPassword"];
        var systemPassword = config["Seed:SystemAdminPassword"];
        if (string.IsNullOrWhiteSpace(adminPassword) || adminPassword.Length < 16 || adminPassword.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(systemPassword) || systemPassword.Length < 16 || systemPassword.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Initial staff passwords must be explicitly configured (16+ characters); default credentials are forbidden.");

        var hasher = PasswordHashing.Create<AppUser>();
        var now = DateTime.UtcNow;

        var admin = new AppUser
        {
            Username = config["Seed:AdminUsername"] ?? "admin",
            Role = Roles.Admin,
            CreatedAtUtc = now
        };
        admin.PasswordHash = hasher.HashPassword(admin, adminPassword);

        var systemAdmin = new AppUser
        {
            Username = config["Seed:SystemAdminUsername"] ?? "sysadmin",
            Role = Roles.SystemAdmin,
            CreatedAtUtc = now
        };
        systemAdmin.PasswordHash = hasher.HashPassword(systemAdmin, systemPassword);

        db.Users.AddRange(admin, systemAdmin);
        await db.SaveChangesAsync();
        logger.LogInformation("Seeded admin accounts: {Admin}, {SystemAdmin}.", admin.Username, systemAdmin.Username);
    }

    /// <summary>
    /// Reconciles managed staff accounts defined in configuration (Seed:StaffUsers).
    /// Runs only during explicit initialization and is idempotent: creates the account if missing, otherwise
    /// updates its password and role to match config when they differ. Entries without a
    /// password are skipped (so a placeholder won't create or clobber an account).
    /// </summary>
    private static async Task SeedConfiguredStaffAsync(AppDbContext db, IConfiguration config, ILogger logger)
    {
        var staff = config.GetSection("Seed:StaffUsers").Get<List<StaffSeed>>() ?? new();
        if (staff.Count == 0)
        {
            return;
        }

        var hasher = PasswordHashing.Create<AppUser>();
        var changes = 0;

        foreach (var entry in staff)
        {
            var username = entry.Username?.Trim();
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(entry.Password))
            {
                logger.LogWarning("Skipping staff entry with a missing username or password ({Username}).",
                    string.IsNullOrWhiteSpace(username) ? "(none)" : username);
                continue;
            }

            if (entry.Password.Length < 16 || entry.Password.Length > 128 || entry.Password.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Managed staff passwords require a unique 16-128 character value.");
            var role = NormalizeRole(entry.Role);
            var existing = await db.Users.FirstOrDefaultAsync(u => u.Username == username);

            if (existing is null)
            {
                var user = new AppUser { Username = username, Role = role, CreatedAtUtc = DateTime.UtcNow };
                user.PasswordHash = hasher.HashPassword(user, entry.Password);
                db.Users.Add(user);
                changes++;
                logger.LogInformation("Seeded staff account {Username} with role {Role}.", username, role);
                continue;
            }

            // Reconcile an existing managed account to match configuration.
            var updated = false;
            if (hasher.VerifyHashedPassword(existing, existing.PasswordHash, entry.Password)
                == PasswordVerificationResult.Failed)
            {
                existing.PasswordHash = hasher.HashPassword(existing, entry.Password);
                existing.FailedLoginCount = 0;
                existing.LockoutEndUtc = null;
                updated = true;
                logger.LogInformation("Updated password for managed account {Username}.", username);
            }
            if (!string.Equals(existing.Role, role, StringComparison.Ordinal))
            {
                existing.Role = role;
                updated = true;
                logger.LogInformation("Updated role for managed account {Username} to {Role}.", username, role);
            }
            if (updated)
            {
                changes++;
            }
        }

        if (changes > 0)
        {
            await db.SaveChangesAsync();
        }
    }

    private static string NormalizeRole(string? role)
        => string.Equals(role, Roles.SystemAdmin, StringComparison.OrdinalIgnoreCase)
            ? Roles.SystemAdmin
            : Roles.Admin;

    private sealed class StaffSeed
    {
        public string? Username { get; set; }
        public string? Role { get; set; }
        public string? Password { get; set; }
    }
}
