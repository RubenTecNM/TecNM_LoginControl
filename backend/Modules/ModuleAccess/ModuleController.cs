using Microsoft.AspNetCore.Mvc;
using TecNM.Api.Modules.Session;

namespace TecNM.Api.Modules.ModuleAccess;

[ApiController]
[Route("api/modules")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ModuleController(ModuleExchangeService modules, SessionService sessions) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var session = await sessions.GetSessionAsync(Request.Cookies[SessionService.CookieName], ct);
        return session is null ? Unauthorized() : Ok(await modules.ListAsync(session.User.Id, ct));
    }
    [HttpPost("{id:guid}/launch")]
    public async Task<IActionResult> Launch(Guid id, CancellationToken ct)
    {
        var session = await sessions.GetSessionAsync(Request.Cookies[SessionService.CookieName], ct);
        if (session is null) return Unauthorized();
        if (!sessions.IsTrustedOrigin(Request.Headers.Origin)) return StatusCode(403);
        try { return Ok(new { url = await modules.LaunchAsync(session.User.Id, id, ct) }); } catch (ModuleAccessException e) { return StatusCode(e.StatusCode, new { message = e.Message }); }
    }
    [HttpGet("authorize")]
    public async Task<IActionResult> Authorize([FromQuery] string? ticket, [FromQuery] string? state, CancellationToken ct)
    {
        Response.Headers["Referrer-Policy"] = "no-referrer";
        var session = await sessions.GetSessionAsync(Request.Cookies[SessionService.CookieName], ct);
        if (session is null) return Unauthorized();
        if (ticket is null || state is null) return BadRequest();
        try { return Redirect(await modules.AuthorizeAsync(session.User.Id, ticket, state, ct)); }
        catch (ModuleAccessException e) { return StatusCode(e.StatusCode, new { message = e.Message }); }
    }
    [HttpPost("exchange")]
    public async Task<IActionResult> Exchange(ExchangeInput input, CancellationToken ct)
    {
        var header = Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(header[7..]) || header[7..].Length != 64) return Unauthorized();
        if (input.ModuleId == Guid.Empty || string.IsNullOrWhiteSpace(input.Code) || input.Code.Length != 64) return BadRequest();
        try { return Ok(await modules.ExchangeAsync(input.ModuleId, header[7..], input.Code, ct)); } catch (ModuleAccessException e) { return StatusCode(e.StatusCode, new { message = e.Message }); }
    }
}
