using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using TecNM.Api.Modules.Session;

namespace TecNM.Api.Modules.ModuleAccess;

[ApiController]
[Route("api/admin")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ModuleAdminController(ModuleAdminService modules, SessionService sessions) : ControllerBase
{
    private async Task<IActionResult> Execute(bool mutation, Func<Task<object?>> action, CancellationToken ct)
    {
        var session = await sessions.GetSessionAsync(Request.Cookies[SessionService.CookieName], ct);
        if (session is null) return Unauthorized();
        if (session.User.UserType.Code != "superadmin" || (mutation && !sessions.IsTrustedOrigin(Request.Headers.Origin))) return StatusCode(403);
        try { return Ok(await action()); }
        catch (ModuleAccessException e) { return StatusCode(e.StatusCode, new { message = e.Message }); }
        catch (Exception e) when (e is PostgresException { SqlState: "40001" or "40P01" } || e is DbUpdateException { InnerException: PostgresException { SqlState: "40001" or "40P01" } })
        { return Conflict(new { message = "La operación cambió simultáneamente. Inténtalo nuevamente." }); }
    }
    [HttpGet("modules")] public Task<IActionResult> List(CancellationToken ct) => Execute(false, async () => await modules.ListAsync(ct), ct);
    [HttpPost("modules")] public Task<IActionResult> Create(ModuleInput input, CancellationToken ct) => Execute(true, async () => await modules.CreateAsync(input, ct), ct);
    [HttpPatch("modules/{id:guid}")] public Task<IActionResult> Update(Guid id, ModuleInput input, CancellationToken ct) => Execute(true, async () => await modules.UpdateAsync(id, input, ct), ct);
    [HttpPost("modules/{id:guid}/key")] public Task<IActionResult> Rotate(Guid id, CancellationToken ct) => Execute(true, async () => new { apiKey = await modules.RotateAsync(id, ct) }, ct);
    [HttpGet("users")] public Task<IActionResult> Users([FromQuery] string? search, CancellationToken ct) => Execute(false, () => modules.UsersAsync(search, ct)!, ct);
    [HttpGet("users/{userId:guid}/modules")] public Task<IActionResult> UserModules(Guid userId, CancellationToken ct) => Execute(false, () => modules.UserModulesAsync(userId, ct)!, ct);
    [HttpGet("modules/{moduleId:guid}/users")] public Task<IActionResult> ModuleUsers(Guid moduleId, CancellationToken ct) => Execute(false, () => modules.ModuleUsersAsync(moduleId, ct)!, ct);
    [HttpPut("users/{userId:guid}/modules/{moduleId:guid}")] public Task<IActionResult> Assign(Guid userId, Guid moduleId, AssignmentInput input, CancellationToken ct) => Execute(true, async () => await modules.SetAssignmentAsync(userId, moduleId, input.IsActive, ct), ct);
    [HttpPut("modules/{moduleId:guid}/users")] public Task<IActionResult> Bulk(Guid moduleId, AssignmentBatch input, CancellationToken ct) => Execute(true, async () => await modules.BulkAsync(moduleId, input, ct), ct);
}
