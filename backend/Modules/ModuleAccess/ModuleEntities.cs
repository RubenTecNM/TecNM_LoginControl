using Microsoft.EntityFrameworkCore;
using TecNM.Api.Data;

namespace TecNM.Api.Modules.ModuleAccess;

public sealed class ExternalModule
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string EntryUrl { get; set; } = "";
    public byte[] ApiKeyHash { get; set; } = [];
    public bool IsActive { get; set; }
}
public sealed class UserModule
{
    public Guid UserId { get; set; }
    public Guid ModuleId { get; set; }
    public bool IsActive { get; set; }
}
public sealed class ModuleAuthToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid ModuleId { get; set; }
    public byte[] TokenHash { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? UsedAt { get; set; }
    public bool BrowserConfirmed { get; set; }
}
public static class ModuleMappings
{
    public static void ConfigureModules(this ModelBuilder model)
    {
        var m = model.Entity<ExternalModule>();
        m.ToTable("modules"); m.HasKey(x => x.Id);
        m.Property(x => x.Id).HasColumnName("id"); m.Property(x => x.Name).HasColumnName("name").HasMaxLength(150);
        m.Property(x => x.EntryUrl).HasColumnName("entry_url"); m.Property(x => x.ApiKeyHash).HasColumnName("api_key_hash"); m.Property(x => x.IsActive).HasColumnName("is_active");
        var u = model.Entity<UserModule>();
        u.ToTable("user_modules"); u.HasKey(x => new { x.UserId, x.ModuleId });
        u.Property(x => x.UserId).HasColumnName("user_id"); u.Property(x => x.ModuleId).HasColumnName("module_id"); u.Property(x => x.IsActive).HasColumnName("is_active");
        u.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction); u.HasOne<ExternalModule>().WithMany().HasForeignKey(x => x.ModuleId).OnDelete(DeleteBehavior.NoAction);
        var t = model.Entity<ModuleAuthToken>();
        t.ToTable("auth_tokens"); t.HasKey(x => x.Id);
        t.Property(x => x.Id).HasColumnName("id"); t.Property(x => x.UserId).HasColumnName("user_id"); t.Property(x => x.ModuleId).HasColumnName("module_id"); t.Property(x => x.TokenHash).HasColumnName("token_hash"); t.HasIndex(x => x.TokenHash).IsUnique();
        t.Property(x => x.BrowserConfirmed).HasColumnName("browser_confirmed").HasDefaultValue(false); t.Property(x => x.CreatedAt).HasColumnName("created_at"); t.Property(x => x.ExpiresAt).HasColumnName("expires_at"); t.Property(x => x.UsedAt).HasColumnName("used_at");
        t.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction); t.HasOne<ExternalModule>().WithMany().HasForeignKey(x => x.ModuleId).OnDelete(DeleteBehavior.NoAction);
    }
}
