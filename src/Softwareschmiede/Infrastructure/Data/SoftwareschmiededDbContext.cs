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

    /// <summary>Repository-Startkonfigurationen.</summary>
    public DbSet<RepositoryStartKonfiguration> RepositoryStartKonfigurationen => Set<RepositoryStartKonfiguration>();

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

    /// <summary>Globale App-Einstellungen.</summary>
    public DbSet<AppEinstellung> AppEinstellungen => Set<AppEinstellung>();

    /// <summary>Diff-Ergebnisse (Vergleiche zwischen Dateiversionen).</summary>
    public DbSet<DiffResult> DiffResults => Set<DiffResult>();

    /// <summary>Diff-Blöcke (gruppierte Änderungen).</summary>
    public DbSet<DiffBlock> DiffBlocks => Set<DiffBlock>();

    /// <summary>Diff-Zeilen (einzelne Zeilen mit Änderungsstatus).</summary>
    public DbSet<DiffLine> DiffLines => Set<DiffLine>();

    /// <summary>Diff-Cache-Einträge (für TTL-basierte Invalidierung).</summary>
    public DbSet<DiffCache> DiffCaches => Set<DiffCache>();

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
            e.HasOne(r => r.StartKonfiguration)
                .WithOne(c => c.GitRepository)
                .HasForeignKey<RepositoryStartKonfiguration>(c => c.GitRepositoryId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // RepositoryStartKonfiguration
        modelBuilder.Entity<RepositoryStartKonfiguration>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.StartScriptRelativePath)
                .IsRequired()
                .HasMaxLength(512);
            e.HasIndex(c => c.GitRepositoryId).IsUnique();
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
            e.HasMany(a => a.DiffResults)
                .WithOne(dr => dr.Aufgabe)
                .HasForeignKey(dr => dr.AufgabeId)
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

        // AppEinstellung
        modelBuilder.Entity<AppEinstellung>(e =>
        {
            e.HasKey(a => a.Id);
            e.Property(a => a.Schluessel)
                .IsRequired()
                .HasMaxLength(200);
            e.HasIndex(a => a.Schluessel).IsUnique();
            e.Property(a => a.Wert)
                .HasMaxLength(2000);
            e.Property(a => a.AktualisiertAm).HasConversion(
                v => v.ToUnixTimeMilliseconds(),
                v => DateTimeOffset.FromUnixTimeMilliseconds(v));
        });

        // DiffResult
        modelBuilder.Entity<DiffResult>(e =>
        {
            e.HasKey(dr => dr.Id);
            e.Property(dr => dr.FilePath)
                .IsRequired()
                .HasMaxLength(500);
            e.Property(dr => dr.SourceVersion)
                .IsRequired()
                .HasMaxLength(100);
            e.Property(dr => dr.TargetVersion)
                .IsRequired()
                .HasMaxLength(100);
            e.Property(dr => dr.DiffType).HasConversion<string>();
            e.Property(dr => dr.Status).HasConversion<string>();
            e.Property(dr => dr.GeneratedBy)
                .IsRequired()
                .HasMaxLength(200);
            e.Property(dr => dr.GeneratedAt).HasConversion(
                v => v.ToUnixTimeMilliseconds(),
                v => DateTimeOffset.FromUnixTimeMilliseconds(v));
            e.Property(dr => dr.ExpiresAt).HasConversion(
                v => v.HasValue ? v.Value.ToUnixTimeMilliseconds() : (long?)null,
                v => v.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(v.Value) : (DateTimeOffset?)null);

            // Foreign keys
            e.HasOne(dr => dr.Aufgabe)
                .WithMany(a => a.DiffResults)
                .HasForeignKey(dr => dr.AufgabeId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(dr => dr.GitRepository)
                .WithMany(gr => gr.DiffResults)
                .HasForeignKey(dr => dr.GitRepositoryId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);
            e.HasOne(dr => dr.ProtokollEintrag)
                .WithOne(pe => pe.DiffResult)
                .HasForeignKey<DiffResult>(dr => dr.ProtokollEintragId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);
            e.HasMany(dr => dr.DiffBlocks)
                .WithOne(db => db.DiffResult)
                .HasForeignKey(db => db.DiffResultId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(dr => dr.DiffCache)
                .WithOne(dc => dc.DiffResult)
                .HasForeignKey<DiffCache>(dc => dc.DiffResultId)
                .OnDelete(DeleteBehavior.Cascade);

            // Indizes
            e.HasIndex(dr => dr.AufgabeId);
            e.HasIndex(dr => dr.GitRepositoryId);
            e.HasIndex(dr => new { dr.AufgabeId, dr.FilePath });
            e.HasIndex(dr => dr.Status);
            e.HasIndex(dr => dr.ExpiresAt);
        });

        // DiffBlock
        modelBuilder.Entity<DiffBlock>(e =>
        {
            e.HasKey(db => db.Id);
            e.Property(db => db.BlockType).HasConversion<string>();
            e.HasMany(db => db.DiffLines)
                .WithOne(dl => dl.DiffBlock)
                .HasForeignKey(dl => dl.DiffBlockId)
                .OnDelete(DeleteBehavior.Cascade);

            // Indizes
            e.HasIndex(db => db.DiffResultId);
            e.HasIndex(db => new { db.DiffResultId, db.BlockSequence });
        });

        // DiffLine
        modelBuilder.Entity<DiffLine>(e =>
        {
            e.HasKey(dl => dl.Id);
            e.Property(dl => dl.LineStatus).HasConversion<string>();
            e.Property(dl => dl.Content)
                .IsRequired();

            // Indizes
            e.HasIndex(dl => dl.DiffBlockId);
            e.HasIndex(dl => new { dl.DiffBlockId, dl.LineSequence });
        });

        // DiffCache
        modelBuilder.Entity<DiffCache>(e =>
        {
            e.HasKey(dc => dc.Id);
            e.Property(dc => dc.CacheKey)
                .IsRequired()
                .HasMaxLength(300);
            e.Property(dc => dc.CachedData)
                .IsRequired();
            e.Property(dc => dc.CachingStrategy).HasConversion<string>();
            e.Property(dc => dc.CachedAt).HasConversion(
                v => v.ToUnixTimeMilliseconds(),
                v => DateTimeOffset.FromUnixTimeMilliseconds(v));
            e.Property(dc => dc.ExpiresAt).HasConversion(
                v => v.ToUnixTimeMilliseconds(),
                v => DateTimeOffset.FromUnixTimeMilliseconds(v));

            // Indizes
            e.HasIndex(dc => dc.CacheKey).IsUnique();
            e.HasIndex(dc => dc.DiffResultId).IsUnique();
            e.HasIndex(dc => dc.ExpiresAt);
        });
    }
}
