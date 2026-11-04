using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using TecNM.Api.Data;

namespace TecNM.Api.Modules.ModuleAccess;

public sealed record ModuleInput(string Name, string EntryUrl, [property: JsonRequired] bool IsActive);
public sealed record ModuleView(Guid Id, string Name, string EntryUrl, bool IsActive);
public sealed record ModuleCreated(ModuleView Module, string ApiKey);
public sealed record AssignmentInput([property: JsonRequired] bool IsActive);
public sealed record AssignmentBatch(string Mode, Guid[]? UserIds, [property: JsonRequired] bool IsActive);
public sealed record BatchResult(int Created, int Changed, int Unchanged);
public sealed class ModuleAccessException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}

public sealed class ModuleAdminService(TecNMDbContext db)
{
    public static string NewSecret() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
    public static byte[] Hash(string value) => SHA256.HashData(Encoding.UTF8.GetBytes(value));
    public static ModuleView View(ExternalModule x) => new(x.Id, x.Name, x.EntryUrl, x.IsActive);
    public Task<List<ModuleView>> ListAsync(CancellationToken ct) => db.Set<ExternalModule>().AsNoTracking()
        .OrderBy(x => x.Name).Select(x => new ModuleView(x.Id, x.Name, x.EntryUrl, x.IsActive)).ToListAsync(ct);
    private static void Validate(ModuleInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Name) || input.Name.Trim().Length > 150
            || !Uri.TryCreate(input.EntryUrl, UriKind.Absolute, out var uri) || uri.Scheme != "https"
            || !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Fragment)
            || uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries)
                .Any(x => new[] { "code", "state", "ticket" }.Contains(Uri.UnescapeDataString(x.Split('=')[0]), StringComparer.OrdinalIgnoreCase)))
            throw new ModuleAccessException(400, "Nombre o URL de módulo inválidos.");
    }
    public async Task<ModuleCreated> CreateAsync(ModuleInput input, CancellationToken ct)
    {
        Validate(input); var key = NewSecret();
        var module = new ExternalModule { Id = Guid.NewGuid(), Name = input.Name.Trim(), EntryUrl = input.EntryUrl, IsActive = input.IsActive, ApiKeyHash = Hash(key) };
        db.Add(module); await db.SaveChangesAsync(ct); return new(View(module), key);
    }
    public async Task<ModuleView> UpdateAsync(Guid id, ModuleInput input, CancellationToken ct)
    {
        Validate(input); await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var module = await RequireModule(id, ct);
        if (module.EntryUrl != input.EntryUrl) await InvalidateAsync(id, ct);
        module.Name = input.Name.Trim(); module.EntryUrl = input.EntryUrl; module.IsActive = input.IsActive;
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return View(module);
    }
    public async Task<string> RotateAsync(Guid id, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var module = await RequireModule(id, ct); var key = NewSecret(); module.ApiKeyHash = Hash(key);
        await InvalidateAsync(id, ct);
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return key;
    }
    private Task<int> InvalidateAsync(Guid moduleId, CancellationToken ct) => db.Set<ModuleAuthToken>()
        .Where(x => x.ModuleId == moduleId && x.UsedAt == null).ExecuteUpdateAsync(update => update.SetProperty(x => x.UsedAt, DateTimeOffset.UtcNow), ct);
    private async Task<ExternalModule> RequireModule(Guid id, CancellationToken ct) =>
        await db.Set<ExternalModule>().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new ModuleAccessException(404, "Módulo no encontrado.");
    public async Task<object> UsersAsync(string? search, CancellationToken ct)
    {
        var query = db.Set<User>().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search)) { var term = search.Trim().ToLowerInvariant(); query = query.Where(x => x.FirstNames.ToLower().Contains(term) || x.PaternalLastName.ToLower().Contains(term) || (x.GoogleEmail != null && x.GoogleEmail.ToLower().Contains(term)) || (x.MicrosoftEmail != null && x.MicrosoftEmail.ToLower().Contains(term))); }
        return await query.OrderBy(x => x.FirstNames).ThenBy(x => x.Id).Select(x => new { x.Id, x.FirstNames, x.PaternalLastName, x.MaternalLastName, x.GoogleEmail, x.MicrosoftEmail }).ToListAsync(ct);
    }
    public async Task<object> UserModulesAsync(Guid userId, CancellationToken ct)
    {
        if (!await db.Set<User>().AnyAsync(x => x.Id == userId, ct)) throw new ModuleAccessException(404, "Usuario no encontrado.");
        return await db.Set<ExternalModule>().AsNoTracking().OrderBy(x => x.Name).Select(x => new { x.Id, x.Name, x.EntryUrl, ModuleActive = x.IsActive, IsActive = db.Set<UserModule>().Any(a => a.ModuleId == x.Id && a.UserId == userId && a.IsActive) }).ToListAsync(ct);
    }
    public async Task<object> ModuleUsersAsync(Guid moduleId, CancellationToken ct)
    {
        await RequireModule(moduleId, ct);
        return await db.Set<User>().AsNoTracking().OrderBy(x => x.FirstNames).ThenBy(x => x.Id).Select(x => new { x.Id, x.FirstNames, x.PaternalLastName, x.MaternalLastName, x.GoogleEmail, x.MicrosoftEmail, IsActive = db.Set<UserModule>().Any(a => a.ModuleId == moduleId && a.UserId == x.Id && a.IsActive) }).ToListAsync(ct);
    }
    public async Task<BatchResult> SetAssignmentAsync(Guid userId, Guid moduleId, bool active, CancellationToken ct)
    {
        if (!await db.Set<User>().AnyAsync(x => x.Id == userId, ct)) throw new ModuleAccessException(404, "Usuario no encontrado.");
        return await BulkAsync(moduleId, new("selected", [userId], active), ct);
    }
    public async Task<BatchResult> BulkAsync(Guid moduleId, AssignmentBatch input, CancellationToken ct)
    {
        if (input.Mode is not ("all" or "selected") || (input.Mode == "selected" && (input.UserIds is null || input.UserIds.Length == 0)) || (input.Mode == "all" && input.UserIds is { Length: > 0 }))
            throw new ModuleAccessException(400, "Selecciona usuarios o el alcance completo de forma explícita.");
        for (var attempt = 0; attempt < 3; attempt++)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            try
            {
                await RequireModule(moduleId, ct);
                var ids = input.Mode == "all" ? await db.Set<User>().Select(x => x.Id).ToArrayAsync(ct) : input.UserIds!.Distinct().ToArray();
                if (input.Mode == "selected" && await db.Set<User>().CountAsync(x => ids.Contains(x.Id), ct) != ids.Length) throw new ModuleAccessException(400, "La selección contiene usuarios inexistentes.");
                var existing = await db.Set<UserModule>().Where(x => x.ModuleId == moduleId && ids.Contains(x.UserId)).ToDictionaryAsync(x => x.UserId, ct);
                var created = 0; var changed = 0; var unchanged = 0;
                foreach (var id in ids)
                {
                    if (existing.TryGetValue(id, out var row)) { if (row.IsActive == input.IsActive) unchanged++; else { row.IsActive = input.IsActive; changed++; } }
                    else if (input.IsActive) { db.Add(new UserModule { UserId = id, ModuleId = moduleId, IsActive = true }); created++; }
                    else unchanged++;
                }
                await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return new(created, changed, unchanged);
            }
            catch (Exception e) when (e is PostgresException { SqlState: "40001" or "40P01" or "23505" } || e is DbUpdateException { InnerException: PostgresException { SqlState: "40001" or "40P01" or "23505" } })
            {
                await transaction.RollbackAsync(ct); db.ChangeTracker.Clear();
                if (attempt == 2) throw new ModuleAccessException(409, "La asignación cambió simultáneamente. Inténtalo nuevamente.");
            }
        }
        throw new ModuleAccessException(409, "No fue posible actualizar las asignaciones.");
    }
}
