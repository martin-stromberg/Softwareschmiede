using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;

namespace Softwareschmiede.Infrastructure.Data;

/// <summary>Entity Framework Core DbContext für die Softwareschmiede-Anwendung.</summary>
public sealed class SoftwareschmiededDbContext : DbContext
{
    // Gemeinsame Konverter für DateTimeOffset <-> Unix-Millisekunden (long), damit SQLite ORDER BY
    // funktioniert. Wird von praktisch allen Entities mit Zeitstempel-Properties referenziert.
    private static readonly ValueConverter<DateTimeOffset, long> UnixMillisConverter = new(
        v => v.ToUnixTimeMilliseconds(),
        v => DateTimeOffset.FromUnixTimeMilliseconds(v));

    private static readonly ValueConverter<DateTimeOffset?, long?> NullableUnixMillisConverter = new(
        v => v.HasValue ? v.Value.ToUnixTimeMilliseconds() : (long?)null,
        v => v.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(v.Value) : (DateTimeOffset?)null);

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

    /// <summary>Alert-Referenzen.</summary>
    public DbSet<AlertReferenz> AlertReferenzen => Set<AlertReferenz>();

    /// <summary>Pull-Request-Referenzen.</summary>
    public DbSet<PullRequestReferenz> PullRequestReferenzen => Set<PullRequestReferenz>();

    /// <summary>Pull-Request-Workflow-Runs.</summary>
    public DbSet<PullRequestWorkflowRun> PullRequestWorkflowRuns => Set<PullRequestWorkflowRun>();

    /// <summary>Protokolleinträge.</summary>
    public DbSet<Protokolleintrag> Protokolleintraege => Set<Protokolleintrag>();

    /// <summary>Testergebnisse.</summary>
    public DbSet<TestErgebnis> TestErgebnisse => Set<TestErgebnis>();

    /// <summary>Plugin-Konfigurationen.</summary>
    public DbSet<PluginKonfiguration> PluginKonfigurationen => Set<PluginKonfiguration>();

    /// <summary>Globale App-Einstellungen.</summary>
    public DbSet<AppEinstellung> AppEinstellungen => Set<AppEinstellung>();

    /// <summary>Promptvorlagen fuer wiederkehrende CLI-Eingaben.</summary>
    public DbSet<PromptVorlage> PromptVorlagen => Set<PromptVorlage>();

    /// <summary>Benutzerbezogene Benachrichtigungseinstellungen.</summary>
    public DbSet<BenachrichtigungsEinstellung> BenachrichtigungsEinstellungen => Set<BenachrichtigungsEinstellung>();

    /// <summary>Benutzerdefinierte Benachrichtigungstöne.</summary>
    public DbSet<BenachrichtigungsAudioDatei> BenachrichtigungsAudioDateien => Set<BenachrichtigungsAudioDatei>();

    /// <summary>Auditlog für Benachrichtigungsentscheidungen.</summary>
    public DbSet<BenachrichtigungsDispatchLog> BenachrichtigungsDispatchLogs => Set<BenachrichtigungsDispatchLog>();

    /// <summary>Diff-Ergebnisse (Vergleiche zwischen Dateiversionen).</summary>
    public DbSet<DiffResult> DiffResults => Set<DiffResult>();

    /// <summary>Diff-Blöcke (gruppierte Änderungen).</summary>
    public DbSet<DiffBlock> DiffBlocks => Set<DiffBlock>();

    /// <summary>Diff-Zeilen (einzelne Zeilen mit Änderungsstatus).</summary>
    public DbSet<DiffLine> DiffLines => Set<DiffLine>();

    /// <summary>Diff-Cache-Einträge (für TTL-basierte Invalidierung).</summary>
    public DbSet<DiffCache> DiffCaches => Set<DiffCache>();

    /// <summary>To-Do-Elemente von Aufgaben.</summary>
    public DbSet<Todo> Todos => Set<Todo>();

    /// <summary>Konfigurationen von Autonomen Aufgaben.</summary>
    public DbSet<AutonomAufgabeKonfiguration> AutonomAufgabeKonfigurationen => Set<AutonomAufgabeKonfiguration>();

    /// <summary>Unteragenten-Spezifikationen von Autonomen Aufgaben.</summary>
    public DbSet<UnteragentSpezifikation> UnteragentSpezifikationen => Set<UnteragentSpezifikation>();

    /// <summary>Skill-Definitionen von Autonomen Aufgaben.</summary>
    public DbSet<SkillDefinition> SkillDefinitionen => Set<SkillDefinition>();

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
            e.Property(p => p.ErstellungsDatum).HasConversion(UnixMillisConverter);
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
            e.Property(r => r.DefaultSourceBranchName)
                .HasMaxLength(255);
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
            e.Property(c => c.WorkingDirectoryRelativePath)
                .IsRequired(false)
                .HasMaxLength(512);
            e.HasIndex(c => c.GitRepositoryId).IsUnique();
        });

        // Aufgabe
        modelBuilder.Entity<Aufgabe>(e =>
        {
            e.HasKey(a => a.Id);
            e.Property(a => a.Status).HasConversion<string>();
            e.Property(a => a.AusfuehrungsStatus)
                .HasConversion<string>()
                .HasDefaultValue(AufgabeAusfuehrungsStatus.NichtGestartet);
            // DateTimeOffset als Unix-Millisekunden (long) speichern, damit SQLite ORDER BY funktioniert.
            e.Property(a => a.ErstellungsDatum).HasConversion(UnixMillisConverter);
            e.Property(a => a.AbschlussDatum).HasConversion(NullableUnixMillisConverter);
            e.Property(a => a.LastHeartbeatUtc).HasConversion(NullableUnixMillisConverter);
            e.Property(a => a.LetzterCliStartUtc).HasConversion(NullableUnixMillisConverter);
            e.Property(a => a.LaufStatus).HasConversion<string>();
            e.Property(a => a.VorschlagAusfuehrenAbUtc).HasConversion(NullableUnixMillisConverter);
            e.Property(a => a.RecoveryVersion)
                .HasDefaultValue(0)
                .IsConcurrencyToken();
            e.HasOne(a => a.GitRepository)
                .WithMany()
                .HasForeignKey(a => a.GitRepositoryId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);
            e.HasOne(a => a.IssueReferenz)
                .WithOne(i => i.Aufgabe)
                .HasForeignKey<IssueReferenz>(i => i.AufgabeId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(a => a.AlertReferenz)
                .WithOne(i => i.Aufgabe)
                .HasForeignKey<AlertReferenz>(i => i.AufgabeId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasMany(a => a.PullRequests)
                .WithOne(p => p.Aufgabe)
                .HasForeignKey(p => p.AufgabeId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasMany(a => a.Protokolleintraege)
                .WithOne(p => p.Aufgabe)
                .HasForeignKey(p => p.AufgabeId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasMany(a => a.DiffResults)
                .WithOne(dr => dr.Aufgabe)
                .HasForeignKey(dr => dr.AufgabeId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasMany(a => a.Todos)
                .WithOne(t => t.Aufgabe)
                .HasForeignKey(t => t.AufgabeId)
                .OnDelete(DeleteBehavior.Cascade);
            e.Property(a => a.SessionPauseUtc).HasConversion(NullableUnixMillisConverter);
            e.HasOne(a => a.AutonomKonfiguration)
                .WithOne(k => k.Aufgabe)
                .HasForeignKey<AutonomAufgabeKonfiguration>(k => k.AufgabeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // AutonomAufgabeKonfiguration
        modelBuilder.Entity<AutonomAufgabeKonfiguration>(e =>
        {
            e.HasKey(k => k.Id);
            e.HasIndex(k => k.AufgabeId).IsUnique();
            e.Property(k => k.ProjektBranchName).IsRequired().HasMaxLength(255);
            e.Property(k => k.InitialPrompt).IsRequired();
            e.Property(k => k.PermissionsJsonPfad).IsRequired().HasMaxLength(512);
            e.Property(k => k.PersistenzModus).IsRequired().HasConversion<string>();
            e.Property(k => k.ArbeitsverzeichnisPfad).IsRequired().HasMaxLength(512);
            e.HasMany(k => k.Unteragenten)
                .WithOne(u => u.AutonomAufgabe)
                .HasForeignKey(u => u.AutonomAufgabeId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasMany(k => k.Skills)
                .WithOne(s => s.AutonomAufgabe)
                .HasForeignKey(s => s.AutonomAufgabeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // UnteragentSpezifikation
        modelBuilder.Entity<UnteragentSpezifikation>(e =>
        {
            e.HasKey(u => u.Id);
            e.Property(u => u.AgentId).IsRequired();
            e.Property(u => u.TaskId).IsRequired();
            e.Property(u => u.AgentScope).IsRequired();
            e.Property(u => u.AgentPrompt).IsRequired();
            e.Property(u => u.AgentDirectory).IsRequired().HasMaxLength(512);
            e.Property(u => u.AgentBranch).IsRequired().HasMaxLength(255);
            e.Property(u => u.AgentClone).IsRequired().HasMaxLength(512);
            e.Property(u => u.Status).IsRequired().HasConversion<string>();
            e.Property(u => u.ErzeugungsDatum).HasConversion(UnixMillisConverter);
            e.Property(u => u.AbschlussDatum).HasConversion(NullableUnixMillisConverter);
            e.HasIndex(u => u.AutonomAufgabeId);
        });

        // SkillDefinition
        modelBuilder.Entity<SkillDefinition>(e =>
        {
            e.HasKey(s => s.Id);
            e.Property(s => s.SkillName).IsRequired();
            e.Property(s => s.SkillVersion).IsRequired();
            e.Property(s => s.SkillContent).IsRequired();
            e.Property(s => s.SkillStatus).IsRequired().HasConversion<string>();
            e.Property(s => s.ErstellungsDatum).HasConversion(UnixMillisConverter);
            e.Property(s => s.FreigabeDatum).HasConversion(NullableUnixMillisConverter);
            e.HasIndex(s => s.AutonomAufgabeId);
        });

        // Todo
        modelBuilder.Entity<Todo>(e =>
        {
            e.HasKey(t => t.Id);
            e.Property(t => t.Beschreibung).IsRequired();
            // DateTimeOffset als Unix-Millisekunden (long) speichern, damit SQLite ORDER BY funktioniert.
            e.Property(t => t.ErstellungsDatum).HasConversion(UnixMillisConverter);
            e.Property(t => t.ErledigtAm).HasConversion(NullableUnixMillisConverter);
        });

        // IssueReferenz
        modelBuilder.Entity<IssueReferenz>(e =>
        {
            e.HasKey(i => i.Id);
        });

        // AlertReferenz
        modelBuilder.Entity<AlertReferenz>(e =>
        {
            e.HasKey(i => i.Id);
            e.Property(i => i.Provider).IsRequired().HasMaxLength(200);
            e.Property(i => i.RepositoryId).IsRequired().HasMaxLength(500);
            e.Property(i => i.AlertType).IsRequired().HasMaxLength(100);
            e.Property(i => i.SourceKey).IsRequired().HasMaxLength(700);
            e.Property(i => i.Titel).IsRequired();
            e.HasIndex(i => i.AufgabeId).IsUnique();
            e.HasIndex(i => i.SourceKey).IsUnique();
        });

        // PullRequestReferenz
        modelBuilder.Entity<PullRequestReferenz>(e =>
        {
            e.HasKey(p => p.Id);
            e.Property(p => p.Provider).HasConversion<string>();
            e.Property(p => p.RepositoryId).IsRequired().HasMaxLength(500);
            e.Property(p => p.ProviderPullRequestId).HasMaxLength(200);
            e.Property(p => p.Url).IsRequired().HasMaxLength(1000);
            e.Property(p => p.Titel).IsRequired().HasMaxLength(500);
            e.Property(p => p.SourceBranch).IsRequired().HasMaxLength(300);
            e.Property(p => p.TargetBranch).IsRequired().HasMaxLength(300);
            e.Property(p => p.HeadSha).HasMaxLength(100);
            e.Property(p => p.MergeCommitSha).HasMaxLength(100);
            e.Property(p => p.Status).HasConversion<string>();
            e.Property(p => p.MergeStatus).HasConversion<string>();
            e.Property(p => p.MonitoringPhase).HasConversion<string>();
            e.Property(p => p.CreatedUtc).HasConversion(UnixMillisConverter);
            e.Property(p => p.LastCheckedUtc).HasConversion(NullableUnixMillisConverter);
            e.Property(p => p.NextCheckUtc).HasConversion(NullableUnixMillisConverter);
            e.HasIndex(p => p.AufgabeId);
            e.HasIndex(p => new { p.Provider, p.RepositoryId, p.PullRequestNumber }).IsUnique();
            e.HasIndex(p => new { p.MonitoringPhase, p.LastCheckedUtc });
            e.HasMany(p => p.WorkflowRuns)
                .WithOne(w => w.PullRequestReferenz)
                .HasForeignKey(w => w.PullRequestReferenzId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // PullRequestWorkflowRun
        modelBuilder.Entity<PullRequestWorkflowRun>(e =>
        {
            e.HasKey(w => w.Id);
            e.Property(w => w.ProviderRunId).IsRequired().HasMaxLength(200);
            e.Property(w => w.Name).IsRequired().HasMaxLength(300);
            e.Property(w => w.Url).HasMaxLength(1000);
            e.Property(w => w.HeadSha).HasMaxLength(100);
            e.Property(w => w.BranchName).HasMaxLength(300);
            e.Property(w => w.Status).HasConversion<string>();
            e.Property(w => w.Conclusion).HasConversion<string>();
            e.Property(w => w.StartedAtUtc).HasConversion(NullableUnixMillisConverter);
            e.Property(w => w.CompletedAtUtc).HasConversion(NullableUnixMillisConverter);
            e.Property(w => w.UpdatedUtc).HasConversion(UnixMillisConverter);
            e.HasIndex(w => w.PullRequestReferenzId);
            e.HasIndex(w => w.ProviderRunId);
            e.HasIndex(w => new { w.PullRequestReferenzId, w.ProviderRunId }).IsUnique();
        });

        // Protokolleintrag
        modelBuilder.Entity<Protokolleintrag>(e =>
        {
            e.HasKey(p => p.Id);
            e.Property(p => p.Typ).HasConversion<string>();
            // DateTimeOffset als Unix-Millisekunden (long) speichern, damit SQLite ORDER BY funktioniert.
            e.Property(p => p.Zeitstempel).HasConversion(UnixMillisConverter);
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
            e.Property(a => a.AktualisiertAm).HasConversion(UnixMillisConverter);
        });

        // PromptVorlage
        modelBuilder.Entity<PromptVorlage>(e =>
        {
            e.HasKey(p => p.Id);
            e.Property(p => p.Name)
                .IsRequired()
                .HasMaxLength(200);
            e.Property(p => p.Prompttext)
                .IsRequired();
            e.Property(p => p.ErstelltAm).HasConversion(UnixMillisConverter);
            e.Property(p => p.AktualisiertAm).HasConversion(UnixMillisConverter);
            e.HasIndex(p => p.Sortierung);
        });

        // BenachrichtigungsEinstellung
        modelBuilder.Entity<BenachrichtigungsEinstellung>(e =>
        {
            e.HasKey(b => b.Id);
            e.Property(b => b.BenutzerId)
                .IsRequired()
                .HasMaxLength(200);
            e.HasIndex(b => b.BenutzerId).IsUnique();
            e.Property(b => b.BannerModus).HasConversion<string>();
            e.Property(b => b.TonModus).HasConversion<string>();
            e.Property(b => b.AktualisiertAm).HasConversion(UnixMillisConverter);
        });

        // BenachrichtigungsAudioDatei
        modelBuilder.Entity<BenachrichtigungsAudioDatei>(e =>
        {
            e.HasKey(b => b.Id);
            e.Property(b => b.BenutzerId)
                .IsRequired()
                .HasMaxLength(200);
            e.HasIndex(b => b.BenutzerId).IsUnique();
            e.Property(b => b.OriginalDateiname)
                .IsRequired()
                .HasMaxLength(260);
            e.Property(b => b.MimeType)
                .IsRequired()
                .HasMaxLength(100);
            e.Property(b => b.Inhalt)
                .IsRequired();
            e.Property(b => b.HochgeladenAm).HasConversion(UnixMillisConverter);
        });

        // BenachrichtigungsDispatchLog
        modelBuilder.Entity<BenachrichtigungsDispatchLog>(e =>
        {
            e.HasKey(b => b.Id);
            e.Property(b => b.BenutzerId)
                .IsRequired()
                .HasMaxLength(200);
            e.Property(b => b.Kanal).HasConversion<string>();
            e.Property(b => b.Modus).HasConversion<string>();
            e.Property(b => b.Entscheidung).HasConversion<string>();
            e.Property(b => b.Grund)
                .IsRequired()
                .HasMaxLength(200);
            e.Property(b => b.ErstelltAm).HasConversion(UnixMillisConverter);
            e.HasIndex(b => new { b.EreignisId, b.BenutzerId, b.Kanal }).IsUnique();
            e.HasIndex(b => b.AufgabeId);
            e.HasIndex(b => b.ErstelltAm);
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
            e.Property(dr => dr.GeneratedAt).HasConversion(UnixMillisConverter);
            e.Property(dr => dr.ExpiresAt).HasConversion(NullableUnixMillisConverter);

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
            e.Property(dc => dc.CachedAt).HasConversion(UnixMillisConverter);
            e.Property(dc => dc.ExpiresAt).HasConversion(UnixMillisConverter);

            // Indizes
            e.HasIndex(dc => dc.CacheKey).IsUnique();
            e.HasIndex(dc => dc.DiffResultId).IsUnique();
            e.HasIndex(dc => dc.ExpiresAt);
        });
    }
}
