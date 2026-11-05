using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace MarksBaseballCards.Api.Auth;

internal static class PasswordHashing
{
    // IdentityV3: PBKDF2-HMAC-SHA512, independent random salt per password.
    internal static PasswordHasher<TUser> Create<TUser>() where TUser : class => new(Options.Create(new PasswordHasherOptions
    {
        CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV3,
        IterationCount = 210_000
    }));
}
