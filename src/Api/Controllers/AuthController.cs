using MarksBaseballCards.Api.Auth;
using MarksBaseballCards.Shared.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace MarksBaseballCards.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AuthService _auth;

    public AuthController(AuthService auth)
    {
        _auth = auth;
    }

    /// <summary>Authenticates a user and returns a signed JWT. Rate-limited per client IP.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
    {
        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var (ok, response, error) = await _auth.LoginAsync(request, clientIp);
        if (!ok)
        {
            return Unauthorized(new { error });
        }
        return Ok(response);
    }

    /// <summary>Returns the identity of the currently authenticated user.</summary>
    [HttpGet("me")]
    [Authorize]
    public ActionResult Me()
    {
        var role = User.Claims.FirstOrDefault(c => c.Type == TokenService.RoleClaim)?.Value;
        return Ok(new { username = User.Identity?.Name, role });
    }
}
