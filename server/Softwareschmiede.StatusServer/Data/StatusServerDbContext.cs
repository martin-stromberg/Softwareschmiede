using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Softwareschmiede.StatusServer.Data;

/// <summary>Entity-Framework-Core-DbContext des Status-Servers
/// (Identity-Benutzerverwaltung, Relay-Tokens, Server-Einstellungen).</summary>
public sealed class StatusServerDbContext : IdentityDbContext<ApplicationUser, IdentityRole, string>
{
    // Konvention aus SoftwareschmiedeDbContext: DateTimeOffset wird als Unix-ms (long)
    // persistiert, damit SQLite ORDER BY korrekt sortiert.
    private static readonly ValueConverter<DateTimeOffset, long> UnixMillisConverter = new(
        v => v.ToUnixTimeMilliseconds(),
        v => DateTimeOffset.FromUnixTimeMilliseconds(v));

    private static readonly ValueConverter<DateTimeOffset?, long?> NullableUnixMillisConverter = new(
        v => v.HasValue ? v.Value.ToUnixTimeMilliseconds() : (long?)null,
        v => v.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(v.Value) : (DateTimeOffset?)null);

    /// <inheritdoc/>
    public StatusServerDbContext(DbContextOptions<StatusServerDbContext> options) : base(options) { }

    /// <summary>Relay-Zugangstokens (nur Hash, niemals Klartext).</summary>
    public DbSet<RelayToken> RelayTokens => Set<RelayToken>();

    /// <summary>Admin-konfigurierbare Server-Einstellungen.</summary>
    public DbSet<ServerSetting> ServerSettings => Set<ServerSetting>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ApplicationUser>(e =>
        {
            e.Property(u => u.LockoutEnd).HasConversion(NullableUnixMillisConverter);
        });

        modelBuilder.Entity<RelayToken>(e =>
        {
            e.HasKey(t => t.Id);
            e.Property(t => t.Id).ValueGeneratedOnAdd();
            e.Property(t => t.Label).HasMaxLength(200);
            e.Property(t => t.TokenHash).HasMaxLength(100);
            e.HasIndex(t => t.TokenHash).IsUnique();
            e.Property(t => t.CreatedUtc).HasConversion(UnixMillisConverter);
            e.Property(t => t.RevokedUtc).HasConversion(NullableUnixMillisConverter);
        });

        modelBuilder.Entity<ServerSetting>(e =>
        {
            e.HasKey(s => s.Key);
            e.Property(s => s.Key).HasMaxLength(100);
            e.Property(s => s.Value).HasMaxLength(500);
        });
    }
}
