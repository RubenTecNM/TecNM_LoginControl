using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;

namespace TecNM.Api.Modules.Session;

[ApiController]
[Route("api/session")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class SessionController(SessionService sessions) : ControllerBase
{
    [HttpGet("login/{provider}")]
    public IActionResult Login(string provider)
    {
        var scheme = provider switch { "google" => "Google", "microsoft" => "Microsoft", _ => null };
        if (scheme is null) return NotFound();
        if (!sessions.IsConfigured(scheme)) return StatusCode(503, new { message = "El inicio de sesión no está disponible." });
        return Challenge(new AuthenticationProperties(), scheme);
    }

    [HttpGet]
    public async Task<IActionResult> GetSession(CancellationToken cancellationToken)
    {
        var result = await sessions.GetSessionAsync(Request.Cookies[SessionService.CookieName], cancellationToken);
        return result is null ? Unauthorized() : Ok(result);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        if (!sessions.IsTrustedOrigin(Request.Headers.Origin.ToString())) return StatusCode(403);
        var result = await sessions.GetSessionAsync(Request.Cookies[SessionService.CookieName], cancellationToken);
        if (result is null) return Unauthorized();
        Response.Cookies.Delete(SessionService.CookieName, SessionAuthentication.CookieOptions());
        return NoContent();
    }
}
