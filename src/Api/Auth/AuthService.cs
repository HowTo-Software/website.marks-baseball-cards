using MarksBaseballCards.Api.Data;
using MarksBaseballCards.Api.Entities;
using MarksBaseballCards.Api.Services;
using MarksBaseballCards.Shared.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MarksBaseballCards.Api.Auth;

/// <summary>
/// Validates credentials with PBKDF2 hashing, enforces account lockout after repeated
/// failures, mitigates user-enumeration / timing attacks, and issues JWTs.
/// </summary>
public class AuthService
{
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    // Pre-computed hash so unknown-user logins still perform a verify (constant-ish timing).
    private static readonly string DummyHash =
        PasswordHashing.Create<AppUser>().HashPassword(new AppUser(), "::timing-mitigation::");

    private const string GenericError = "Invalid username or password.";

    private static string SanitizeForLog(string value) =>
        value.Replace("\r", string.Empty).Replace("\n", string.Empty);

    private readonly AppDbContext _db;
    private readonly TokenService _tokens;
    private readonly HistoryWriter _history;
    private readonly PasswordHasher<AppUser> _hasher = PasswordHashing.Create<AppUser>();
    private readonly ILogger<AuthService> _logger;

    public AuthService(AppDbContext db, TokenService tokens, HistoryWriter history, ILogger<AuthService> logger)
    {
        _db = db;
        _tokens = tokens;
        _history = history;
        _logger = logger;
    }

    public async Task<(bool Ok, LoginResponse? Response, string Error)> LoginAsync(
        LoginRequest request, string clientIp)
    {
        var lookupUsername = request.Username.Trim();
        var username = SanitizeForLog(lookupUsername);
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == lookupUsername);

        if (user is null)
        {
            // Verify against a dummy hash so response time does not reveal account existence.
            _hasher.VerifyHashedPassword(new AppUser(), DummyHash, request.Password);
            await _history.LogAsync("LoginFailed", username, $"Unknown user from {clientIp}.");
            return (false, null, GenericError);
        }

        if (user.LockoutEndUtc is { } lockedUntil && lockedUntil > DateTime.UtcNow)
        {
            _hasher.VerifyHashedPassword(new AppUser(), DummyHash, request.Password);
            await _history.LogAsync("LoginFailed", username, $"Locked account from {clientIp}.");
            return (false, null, GenericError);
        }

        var verification = _hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (verification == PasswordVerificationResult.Failed)
        {
            user.FailedLoginCount++;
            if (user.FailedLoginCount >= MaxFailedAttempts)
            {
                user.LockoutEndUtc = DateTime.UtcNow.Add(LockoutDuration);
                user.FailedLoginCount = 0;
                _logger.LogWarning("Account {User} locked after repeated failures from {Ip}.", username, clientIp);
            }
            await _db.SaveChangesAsync();
            await _history.LogAsync("LoginFailed", username, $"Incorrect password from {clientIp}.");
            return (false, null, GenericError);
        }

        // Success: clear counters, refresh hash if the algorithm parameters changed.
        user.FailedLoginCount = 0;
        user.LockoutEndUtc = null;
        user.LastLoginUtc = DateTime.UtcNow;
        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = _hasher.HashPassword(user, request.Password);
        }
        await _db.SaveChangesAsync();
        await _history.LogAsync("Login", username, $"Signed in from {clientIp}.");

        var (token, expires) = _tokens.CreateToken(user);
        return (true, new LoginResponse
        {
            Token = token,
            Username = user.Username,
            Role = user.Role,
            ExpiresUtc = expires
        }, string.Empty);
    }
}
