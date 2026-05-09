using Microsoft.EntityFrameworkCore;
using Softwareschmiede.Domain.Entities;

namespace Softwareschmiede.Infrastructure.Data;

/// <summary>Entity Framework Core DbContext für die Softwareschmiede-Anwendung.</summary>
public sealed class SoftwareschmiededDbContext : DbContext
{
    /// <inheritdoc/>
    public SoftwareschmiededDbContext(DbContextOptions<SoftwareschmiededDbContext> options) : base(options) { }

    /// <summary>Projekte.</summary>
    public DbSet<Projekt> Projekte => Set<Projekt>();

    /// <summary>Git-Repositories.</summary>
    public DbSet<GitRepository> GitRepositories => Set<GitRepository>();

    /// <summary>Aufgaben.</summary>
    public DbSet<Aufgabe> Aufgaben => Set<Aufgabe>();

    /// <summary>Issue-Referenzen.</summary>
    public DbSet<IssueReferenz> IssueReferenzen => Set<IssueReferenz>();

    /// <summary>Protokolleinträge.</summary>
    public DbSet<Protokolleintrag> Protokolleintraege => Set<Protokolleintrag>();

    /// <summary>Testergebnisse.</summary>
    public DbSet<TestErgebnis> TestErgebnisse => Set<TestErgebnis>();

    /// <summary>Plugin-Konfigurationen.</summary>
    public DbSet<PluginKonfiguration> PluginKonfigurationen => Set<PluginKonfiguration>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Projekt
        modelBuilder.Entity<Projekt>(e =>
        {
            e.HasKey(p => p.Id);
            e.Property(p => p.Status).HasConversion<string>();
            // DateTimeOffset als Unix-Millisekunden (long) speichern, damit SQLite ORDER BY funktioniert.
            e.Property(p => p.ErstellungsDatum).HasConversion(
                v => v.ToUnixTimeMilliseconds(),
                v => DateTimeOffset.FromUnixTimeMilliseconds(v));
            e.HasMany(p => p.Repositories)
                .WithOne(r => r.Projekt)
                .HasForeignKey(r => r.ProjektId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasMany(p => p.Aufgaben)
                .WithOne(a => a.Projekt)
                .HasForeignKey(a => a.ProjektId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // GitRepository
        modelBuilder.Entity<GitRepository>(e =>
        {
            e.HasKey(r => r.Id);
        });

        // Aufgabe
        modelBuilder.Entity<Aufgabe>(e =>
        {
            e.HasKey(a => a.Id);
            e.Property(a => a.Status).HasConversion<string>();
            // DateTimeOffset als Unix-Millisekunden (long) speichern, damit SQLite ORDER BY funktioniert.
            e.Property(a => a.ErstellungsDatum).HasConversion(
                v => v.ToUnixTimeMilliseconds(),
                v => DateTimeOffset.FromUnixTimeMilliseconds(v));
            e.Property(a => a.AbschlussDatum).HasConversion(
                v => v.HasValue ? v.Value.ToUnixTimeMilliseconds() : (long?)null,
                v => v.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(v.Value) : (DateTimeOffset?)null);
            e.HasOne(a => a.GitRepository)
                .WithMany()
                .HasForeignKey(a => a.GitRepositoryId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);
            e.HasOne(a => a.IssueReferenz)
                .WithOne(i => i.Aufgabe)
                .HasForeignKey<IssueReferenz>(i => i.AufgabeId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasMany(a => a.Protokolleintraege)
                .WithOne(p => p.Aufgabe)
                .HasForeignKey(p => p.AufgabeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // IssueReferenz
        modelBuilder.Entity<IssueReferenz>(e =>
        {
            e.HasKey(i => i.Id);
        });

        // Protokolleintrag
        modelBuilder.Entity<Protokolleintrag>(e =>
        {
            e.HasKey(p => p.Id);
            e.Property(p => p.Typ).HasConversion<string>();
            // DateTimeOffset als Unix-Millisekunden (long) speichern, damit SQLite ORDER BY funktioniert.
            e.Property(p => p.Zeitstempel).HasConversion(
                v => v.ToUnixTimeMilliseconds(),
                v => DateTimeOffset.FromUnixTimeMilliseconds(v));
            e.HasMany(p => p.TestErgebnisse)
                .WithOne(t => t.Protokolleintrag)
                .HasForeignKey(t => t.ProtokollEintragId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // TestErgebnis
        modelBuilder.Entity<TestErgebnis>(e =>
        {
            e.HasKey(t => t.Id);
            e.Property(t => t.Status).HasConversion<string>();
            e.Property(t => t.Dauer)
                .HasConversion(
                    ts => ts.Ticks,
                    ticks => TimeSpan.FromTicks(ticks));
        });

        // PluginKonfiguration
        modelBuilder.Entity<PluginKonfiguration>(e =>
        {
            e.HasKey(p => p.Id);
            e.Property(p => p.PluginKategorie).HasConversion<string>();
        });
    }
}
