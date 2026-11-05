using System.Net.Mail;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace MarksBaseballCards.AdminBootstrap;

public sealed record AdminCredentials(string Email, string PasswordHash)
{
    public override string ToString() => "Protected administrator credentials";
    public static AdminCredentials Create(string email, string password)
    {
        email = email.Trim();
        ValidateEmail(email);
        if (password.Length < 16 || password.Length > 128 || password.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Use a unique password or passphrase of 16 to 128 characters.");
        var hasher = new PasswordHasher<object>(Options.Create(new PasswordHasherOptions
        {
            CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV3,
            IterationCount = 210_000
        }));
        return new(email, hasher.HashPassword(new object(), password));
    }

    internal void Validate()
    {
        ValidateEmail(Email);
        var decoded = Convert.FromBase64String(PasswordHash);
        if (decoded.Length != 61 || decoded[0] != 1
            || System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(decoded.AsSpan(1, 4)) != 2
            || System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(decoded.AsSpan(5, 4)) != 210_000
            || System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(decoded.AsSpan(9, 4)) != 16)
            throw new ArgumentException("The package must contain the expected IdentityV3 password hash.");
    }

    private static void ValidateEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email) || email.Length > 64 || !MailAddress.TryCreate(email, out var address) || address.Address != email
            || email.Contains('\r') || email.Contains('\n'))
            throw new ArgumentException("Enter a valid email of up to 64 characters.");
    }
}
