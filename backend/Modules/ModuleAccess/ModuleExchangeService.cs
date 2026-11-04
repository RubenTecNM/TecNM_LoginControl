using System.Data;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using TecNM.Api.Data;

namespace TecNM.Api.Modules.ModuleAccess;

public sealed record ExchangeInput(Guid ModuleId, string Code);
public sealed class ModuleExchangeService(TecNMDbContext db)
{
    public Task<List<ModuleView>> ListAsync(Guid userId, CancellationToken ct) => db.Set<ExternalModule>().AsNoTracking().Where(m => m.IsActive && db.Set<UserModule>().Any(a => a.UserId == userId && a.ModuleId == m.Id && a.IsActive)).OrderBy(m => m.Name).Select(m => new ModuleView(m.Id, m.Name, m.EntryUrl, m.IsActive)).ToListAsync(ct);
    public async Task<string> LaunchAsync(Guid userId, Guid moduleId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var module = await db.Set<ExternalModule>().SingleOrDefaultAsync(m => m.Id == moduleId && m.IsActive && db.Set<UserModule>().Any(a => a.UserId == userId && a.ModuleId == m.Id && a.IsActive), ct);
            if (module is null) throw new ModuleAccessException(403, "No fue posible autorizar el acceso.");
            var code = ModuleAdminService.NewSecret(); var now = DateTimeOffset.UtcNow;
            db.Add(new ModuleAuthToken { Id = Guid.NewGuid(), UserId = userId, ModuleId = moduleId, TokenHash = ModuleAdminService.Hash(code), CreatedAt = now, ExpiresAt = now.AddSeconds(60) });
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString(module.EntryUrl, "ticket", code);
        }
        catch (Exception e) when (e is PostgresException { SqlState: "40001" or "40P01" } || e is DbUpdateException { InnerException: PostgresException { SqlState: "40001" or "40P01" } })
        {
            throw new ModuleAccessException(409, "La operación cambió simultáneamente. Inténtalo nuevamente.");
        }
    }
    public async Task<string> AuthorizeAsync(Guid userId, string ticket, string state, CancellationToken ct)
    {
        if (ticket.Length != 64 || state.Length is < 32 or > 128 || state.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-' && c != '_'))
            throw new ModuleAccessException(400, "No fue posible autorizar el acceso.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var hash = ModuleAdminService.Hash(ticket); var now = DateTimeOffset.UtcNow;
            var token = await db.Set<ModuleAuthToken>().SingleOrDefaultAsync(t => t.TokenHash == hash && t.UserId == userId && !t.BrowserConfirmed && t.UsedAt == null && t.ExpiresAt > now, ct);
            if (token is null) throw new ModuleAccessException(400, "No fue posible autorizar el acceso.");
            var module = await db.Set<ExternalModule>().SingleOrDefaultAsync(m => m.Id == token.ModuleId && m.IsActive && db.Set<UserModule>().Any(a => a.UserId == userId && a.ModuleId == m.Id && a.IsActive), ct);
            if (module is null) throw new ModuleAccessException(403, "No fue posible autorizar el acceso.");
            var code = ModuleAdminService.NewSecret(); token.UsedAt = now;
            db.Add(new ModuleAuthToken { Id = Guid.NewGuid(), UserId = userId, ModuleId = module.Id, TokenHash = ModuleAdminService.Hash(code), CreatedAt = now, ExpiresAt = now.AddSeconds(60), BrowserConfirmed = true });
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            return Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString(module.EntryUrl, new Dictionary<string, string?> { ["code"] = code, ["state"] = state });
        }
        catch (Exception e) when (e is PostgresException { SqlState: "40001" or "40P01" } || e is DbUpdateException { InnerException: PostgresException { SqlState: "40001" or "40P01" } })
        { throw new ModuleAccessException(409, "La operación cambió simultáneamente. Inténtalo nuevamente."); }
    }
    public async Task<object> ExchangeAsync(Guid moduleId, string apiKey, string code, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var module = await db.Set<ExternalModule>().SingleOrDefaultAsync(m => m.Id == moduleId, ct);
            if (module is null || !CryptographicOperations.FixedTimeEquals(module.ApiKeyHash, ModuleAdminService.Hash(apiKey))) throw new ModuleAccessException(401, "No fue posible autorizar el acceso.");
            if (!module.IsActive) throw new ModuleAccessException(403, "No fue posible autorizar el acceso.");
            var hash = ModuleAdminService.Hash(code); var now = DateTimeOffset.UtcNow;
            var token = await db.Set<ModuleAuthToken>().SingleOrDefaultAsync(t => t.TokenHash == hash && t.ModuleId == moduleId && t.BrowserConfirmed && t.UsedAt == null && t.ExpiresAt > now, ct);
            if (token is null) throw new ModuleAccessException(400, "No fue posible autorizar el acceso.");
            var user = await db.Set<User>().AsNoTracking().SingleOrDefaultAsync(u => u.Id == token.UserId && db.Set<UserModule>().Any(a => a.UserId == u.Id && a.ModuleId == moduleId && a.IsActive), ct);
            if (user is null) throw new ModuleAccessException(403, "No fue posible autorizar el acceso.");
            token.UsedAt = now; await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            return new { user = new { user.Id, user.FirstNames, user.PaternalLastName, user.MaternalLastName, user.GoogleEmail, user.MicrosoftEmail } };
        }
        catch (Exception e) when (e is PostgresException { SqlState: "40001" or "40P01" } || e is DbUpdateException { InnerException: PostgresException { SqlState: "40001" or "40P01" } }) { throw new ModuleAccessException(409, "La operación cambió simultáneamente. Inténtalo nuevamente."); }
    }
}
