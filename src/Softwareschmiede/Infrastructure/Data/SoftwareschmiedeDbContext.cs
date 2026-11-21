using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;

namespace Softwareschmiede.Infrastructure.Data;

/// <summary>Entity Framework Core DbContext für die Softwareschmiede-Anwendung.</summary>
public sealed class SoftwareschmiedeDbContext : DbContext
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
    public SoftwareschmiedeDbContext(DbContextOptions<SoftwareschmiedeDbContext> options) : base(options) { }

    /// <summary>Projekte.</summary>
    public DbSet<Project> Projects => Set<Project>();

    /// <summary>Git-Repositories.</summary>
    public DbSet<GitRepository> GitRepositories => Set<GitRepository>();

    /// <summary>Repository-Startkonfigurationen.</summary>
    public DbSet<RepositoryStartConfiguration> RepositoryStartConfigurations => Set<RepositoryStartConfiguration>();

    /// <summary>Repository-Initialisierungskonfigurationen.</summary>
    public DbSet<RepositoryInitializationConfiguration> RepositoryInitializationConfigurations => Set<RepositoryInitializationConfiguration>();

    /// <summary>Aufgaben.</summary>
    public DbSet<WorkItem> WorkItems => Set<WorkItem>();

    /// <summary>Issue-Referenzen.</summary>
    public DbSet<IssueReference> IssueReferences => Set<IssueReference>();

    /// <summary>Alert-Referenzen.</summary>
    public DbSet<AlertReference> AlertReferences => Set<AlertReference>();

    /// <summary>Pull-Request-Referenzen.</summary>
    public DbSet<PullRequestReference> PullRequestReferences => Set<PullRequestReference>();

    /// <summary>Pull-Request-Workflow-Runs.</summary>
    public DbSet<PullRequestWorkflowRun> PullRequestWorkflowRuns => Set<PullRequestWorkflowRun>();

    /// <summary>Protokolleinträge.</summary>
    public DbSet<LogEntry> LogEntries => Set<LogEntry>();

    /// <summary>Testergebnisse.</summary>
    public DbSet<TestResult> TestResults => Set<TestResult>();

    /// <summary>Plugin-Konfigurationen.</summary>
    public DbSet<PluginConfiguration> PluginConfigurations => Set<PluginConfiguration>();

    /// <summary>Globale App-Einstellungen.</summary>
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();

    /// <summary>Promptvorlagen fuer wiederkehrende CLI-Eingaben.</summary>
    public DbSet<PromptTemplate> PromptTemplates => Set<PromptTemplate>();

    /// <summary>Benutzerbezogene Benachrichtigungseinstellungen.</summary>
    public DbSet<NotificationSetting> NotificationSettings => Set<NotificationSetting>();

    /// <summary>Benutzerdefinierte Benachrichtigungstöne.</summary>
    public DbSet<NotificationAudioFile> NotificationAudioFiles => Set<NotificationAudioFile>();

    /// <summary>Auditlog für Benachrichtigungsentscheidungen.</summary>
    public DbSet<NotificationDispatchLog> NotificationDispatchLogs => Set<NotificationDispatchLog>();

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

    /// <summary>Persistierte geplante zeitgesteuerte Prompts (max. ein Plan pro Aufgabe).</summary>
    public DbSet<ScheduledPrompt> ScheduledPrompts => Set<ScheduledPrompt>();

    /// <summary>Dauerhafte SCM-Eingangsqueue.</summary>
    public DbSet<ScmIncomingEvent> ScmIncomingEvents => Set<ScmIncomingEvent>();
    /// <summary>Beobachtete SCM-Textstände.</summary>
    public DbSet<ScmContentSnapshot> ScmContentSnapshots => Set<ScmContentSnapshot>();
    /// <summary>Quellenspezifische Abfragestände.</summary>
    public DbSet<ScmObservationCheckpoint> ScmObservationCheckpoints => Set<ScmObservationCheckpoint>();
    /// <summary>Dauerhafte ausgehende SCM-Aktionen.</summary>
    public DbSet<ScmOutgoingAction> ScmOutgoingActions => Set<ScmOutgoingAction>();
    /// <summary>Instanzübergreifende Aufgabenleases.</summary>
    public DbSet<ScmTaskLease> ScmTaskLeases => Set<ScmTaskLease>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ScmIncomingEvent>(e =>
        {
            e.HasKey(x => x.Sequence);
            e.Property(x => x.Sequence).ValueGeneratedOnAdd();
            e.HasIndex(x => x.Id).IsUnique();
            e.HasIndex(x => x.EventKey).IsUnique();
            e.Property(x => x.Purpose).HasMaxLength(100);
            e.HasIndex(x => new { x.WorkItemId, x.Sequence });
            e.HasIndex(x => new { x.Status, x.NextAttemptUtc });
        });
        modelBuilder.Entity<ScmContentSnapshot>().HasKey(x => x.ContentKey);
        modelBuilder.Entity<ScmObservationCheckpoint>().HasKey(x => x.SourceKey);
        modelBuilder.Entity<ScmTaskLease>().HasKey(x => x.WorkItemId);
        modelBuilder.Entity<ScmOutgoingAction>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.EventId, x.Status });
        });

        // Projekt
        modelBuilder.Entity<Project>(e =>
        {
            e.HasKey(p => p.Id);
            e.Property(p => p.Status).HasConversion<string>();
            // DateTimeOffset als Unix-Millisekunden (long) speichern, damit SQLite ORDER BY funktioniert.
            e.Property(p => p.CreatedAt).HasConversion(UnixMillisConverter);
            e.HasMany(p => p.Repositories)
                .WithOne(r => r.Project)
                .HasForeignKey(r => r.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasMany(p => p.WorkItems)
                .WithOne(a => a.Project)
                .HasForeignKey(a => a.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // GitRepository
        modelBuilder.Entity<GitRepository>(e =>
        {
            e.HasKey(r => r.Id);
            e.Property(r => r.DefaultSourceBranchName)
                .HasMaxLength(255);
            e.Property(r => r.IssuePreparationEnabled)
                .HasDefaultValue(false);
            e.HasOne(r => r.StartConfiguration)
                .WithOne(c => c.GitRepository)
                .HasForeignKey<RepositoryStartConfiguration>(c => c.GitRepositoryId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(r => r.InitializationConfiguration)
                .WithOne(c => c.GitRepository)
                .HasForeignKey<RepositoryInitializationConfiguration>(c => c.GitRepositoryId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // RepositoryStartKonfiguration
        modelBuilder.Entity<RepositoryStartConfiguration>(e =>
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

        // RepositoryInitialisierungKonfiguration
        modelBuilder.Entity<RepositoryInitializationConfiguration>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.InitializationScriptRelativePath)
                .IsRequired()
                .HasMaxLength(512);
            e.HasIndex(c => c.GitRepositoryId).IsUnique();
        });

        // Aufgabe
        modelBuilder.Entity<WorkItem>(e =>
        {
            e.HasKey(a => a.Id);
            e.Property(a => a.Status).HasConversion<string>();
            e.Property(a => a.ExecutionStatus)
                .HasConversion<string>()
                .HasDefaultValue(WorkItemExecutionStatus.NotStarted);
            // DateTimeOffset als Unix-Millisekunden (long) speichern, damit SQLite ORDER BY funktioniert.
            e.Property(a => a.CreatedAt).HasConversion(UnixMillisConverter);
            e.Property(a => a.CompletedAt).HasConversion(NullableUnixMillisConverter);
            e.Property(a => a.LastHeartbeatUtc).HasConversion(NullableUnixMillisConverter);
            e.Property(a => a.LastCliStartUtc).HasConversion(NullableUnixMillisConverter);
            e.Property(a => a.RunStatus).HasConversion<string>();
            e.Property(a => a.SuggestedExecuteAfterUtc).HasConversion(NullableUnixMillisConverter);
            e.Property(a => a.PausedUntilUtc).HasConversion(NullableUnixMillisConverter);
            e.Property(a => a.BaseBranchName)
                .HasMaxLength(255);
            e.Property(a => a.Purpose)
                .HasMaxLength(100);
            e.Property(a => a.RecoveryVersion)
                .HasDefaultValue(0)
                .IsConcurrencyToken();
            e.HasOne(a => a.GitRepository)
                .WithMany()
                .HasForeignKey(a => a.GitRepositoryId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);
            e.HasOne(a => a.IssueReference)
                .WithOne(i => i.WorkItem)
                .HasForeignKey<IssueReference>(i => i.WorkItemId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(a => a.AlertReference)
                .WithOne(i => i.WorkItem)
                .HasForeignKey<AlertReference>(i => i.WorkItemId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasMany(a => a.PullRequests)
                .WithOne(p => p.WorkItem)
                .HasForeignKey(p => p.WorkItemId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasMany(a => a.LogEntries)
                .WithOne(p => p.WorkItem)
                .HasForeignKey(p => p.WorkItemId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasMany(a => a.DiffResults)
                .WithOne(dr => dr.WorkItem)
                .HasForeignKey(dr => dr.WorkItemId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasMany(a => a.Todos)
                .WithOne(t => t.WorkItem)
                .HasForeignKey(t => t.WorkItemId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Todo
        modelBuilder.Entity<Todo>(e =>
        {
            e.HasKey(t => t.Id);
            e.Property(t => t.Description).IsRequired();
            // DateTimeOffset als Unix-Millisekunden (long) speichern, damit SQLite ORDER BY funktioniert.
            e.Property(t => t.CreatedAt).HasConversion(UnixMillisConverter);
            e.Property(t => t.CompletedAt).HasConversion(NullableUnixMillisConverter);
        });

        // ZeitgesteuerterPrompt
        modelBuilder.Entity<ScheduledPrompt>(e =>
        {
            e.HasKey(z => z.Id);
            e.Property(z => z.PromptText).IsRequired();
            // DateTimeOffset als Unix-Millisekunden (long) speichern, damit SQLite ORDER BY funktioniert.
            e.Property(z => z.TargetTimeUtc).HasConversion(UnixMillisConverter);
            e.Property(z => z.CreatedAtUtc).HasConversion(UnixMillisConverter);
            e.Property(z => z.UpdatedAtUtc).HasConversion(UnixMillisConverter);
            // Ersetzungsregel: max. ein geplanter Prompt pro Aufgabe.
            e.HasIndex(z => z.WorkItemId).IsUnique();
            e.HasOne<WorkItem>()
                .WithMany()
                .HasForeignKey(z => z.WorkItemId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // IssueReferenz
        modelBuilder.Entity<IssueReference>(e =>
        {
            e.HasKey(i => i.Id);
            e.Property(i => i.SourceKey).HasMaxLength(700);
            e.HasIndex(i => i.SourceKey).IsUnique();
        });

        // AlertReferenz
        modelBuilder.Entity<AlertReference>(e =>
        {
            e.HasKey(i => i.Id);
            e.Property(i => i.Provider).IsRequired().HasMaxLength(200);
            e.Property(i => i.RepositoryId).IsRequired().HasMaxLength(500);
            e.Property(i => i.AlertType).IsRequired().HasMaxLength(100);
            e.Property(i => i.SourceKey).IsRequired().HasMaxLength(700);
            e.Property(i => i.Title).IsRequired();
            e.HasIndex(i => i.WorkItemId).IsUnique();
            e.HasIndex(i => i.SourceKey).IsUnique();
        });

        // PullRequestReferenz
        modelBuilder.Entity<PullRequestReference>(e =>
        {
            e.HasKey(p => p.Id);
            e.Property(p => p.Provider).HasConversion<string>();
            e.Property(p => p.Role)
                .HasConversion<string>()
                .HasDefaultValue(PullRequestReferenceRole.CreatedByTask);
            e.Property(p => p.RepositoryId).IsRequired().HasMaxLength(500);
            e.Property(p => p.ProviderPullRequestId).HasMaxLength(200);
            e.Property(p => p.Url).IsRequired().HasMaxLength(1000);
            e.Property(p => p.Title).IsRequired().HasMaxLength(500);
            e.Property(p => p.SourceBranch).IsRequired().HasMaxLength(300);
            e.Property(p => p.SourceRepositoryId).IsRequired().HasMaxLength(500);
            e.Property(p => p.SourceRepositoryUrl).HasMaxLength(1000);
            e.Property(p => p.SourceRef).HasMaxLength(500);
            e.Property(p => p.TargetBranch).IsRequired().HasMaxLength(300);
            e.Property(p => p.HeadSha).HasMaxLength(100);
            e.Property(p => p.MergeCommitSha).HasMaxLength(100);
            e.Property(p => p.Status).HasConversion<string>();
            e.Property(p => p.MergeStatus).HasConversion<string>();
            e.Property(p => p.MonitoringPhase).HasConversion<string>();
            e.Property(p => p.CreatedUtc).HasConversion(UnixMillisConverter);
            e.Property(p => p.LastCheckedUtc).HasConversion(NullableUnixMillisConverter);
            e.Property(p => p.NextCheckUtc).HasConversion(NullableUnixMillisConverter);
            e.HasIndex(p => p.WorkItemId)
                .HasDatabaseName("IX_PullRequestReferenzen_AufgabeId");
            e.HasIndex(p => p.WorkItemId)
                .IsUnique()
                .HasFilter("[Rolle] = 'ReviewSource'")
                .HasDatabaseName("IX_PullRequestReferenzen_AufgabeId_ReviewSource");
            e.HasIndex(p => new { p.Provider, p.RepositoryId, p.PullRequestNumber }).IsUnique();
            e.HasIndex(p => new { p.MonitoringPhase, p.LastCheckedUtc });
            e.HasMany(p => p.WorkflowRuns)
                .WithOne(w => w.PullRequestReference)
                .HasForeignKey(w => w.PullRequestReferenceId)
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
            e.HasIndex(w => w.PullRequestReferenceId);
            e.HasIndex(w => w.ProviderRunId);
            e.HasIndex(w => new { w.PullRequestReferenceId, w.ProviderRunId }).IsUnique();
        });

        // Protokolleintrag
        modelBuilder.Entity<LogEntry>(e =>
        {
            e.HasKey(p => p.Id);
            e.Property(p => p.Type).HasConversion<string>();
            // DateTimeOffset als Unix-Millisekunden (long) speichern, damit SQLite ORDER BY funktioniert.
            e.Property(p => p.Timestamp).HasConversion(UnixMillisConverter);
            e.HasMany(p => p.TestResults)
                .WithOne(t => t.LogEntry)
                .HasForeignKey(t => t.LogEntryId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // TestErgebnis
        modelBuilder.Entity<TestResult>(e =>
        {
            e.HasKey(t => t.Id);
            e.Property(t => t.Status).HasConversion<string>();
            e.Property(t => t.Duration)
                .HasConversion(
                    ts => ts.Ticks,
                    ticks => TimeSpan.FromTicks(ticks));
        });

        // PluginKonfiguration
        modelBuilder.Entity<PluginConfiguration>(e =>
        {
            e.HasKey(p => p.Id);
            e.Property(p => p.PluginCategory).HasConversion<string>();
        });

        // AppEinstellung
        modelBuilder.Entity<AppSetting>(e =>
        {
            e.HasKey(a => a.Id);
            e.Property(a => a.Key)
                .IsRequired()
                .HasMaxLength(200);
            e.HasIndex(a => a.Key).IsUnique();
            e.Property(a => a.Value)
                .HasMaxLength(2000);
            e.Property(a => a.UpdatedAt).HasConversion(UnixMillisConverter);
        });

        // PromptVorlage
        modelBuilder.Entity<PromptTemplate>(e =>
        {
            e.HasKey(p => p.Id);
            e.Property(p => p.Name)
                .IsRequired()
                .HasMaxLength(200);
            e.Property(p => p.PromptText)
                .IsRequired();
            e.Property(p => p.CreatedAt).HasConversion(UnixMillisConverter);
            e.Property(p => p.UpdatedAt).HasConversion(UnixMillisConverter);
            e.HasIndex(p => p.SortOrder);
        });

        // BenachrichtigungsEinstellung
        modelBuilder.Entity<NotificationSetting>(e =>
        {
            e.HasKey(b => b.Id);
            e.Property(b => b.UserId)
                .IsRequired()
                .HasMaxLength(200);
            e.HasIndex(b => b.UserId).IsUnique();
            e.Property(b => b.BannerMode).HasConversion<string>();
            e.Property(b => b.SoundMode).HasConversion<string>();
            e.Property(b => b.UpdatedAt).HasConversion(UnixMillisConverter);
        });

        // BenachrichtigungsAudioDatei
        modelBuilder.Entity<NotificationAudioFile>(e =>
        {
            e.HasKey(b => b.Id);
            e.Property(b => b.UserId)
                .IsRequired()
                .HasMaxLength(200);
            e.HasIndex(b => b.UserId).IsUnique();
            e.Property(b => b.OriginalFileName)
                .IsRequired()
                .HasMaxLength(260);
            e.Property(b => b.MimeType)
                .IsRequired()
                .HasMaxLength(100);
            e.Property(b => b.Content)
                .IsRequired();
            e.Property(b => b.UploadedAt).HasConversion(UnixMillisConverter);
        });

        // BenachrichtigungsDispatchLog
        modelBuilder.Entity<NotificationDispatchLog>(e =>
        {
            e.HasKey(b => b.Id);
            e.Property(b => b.UserId)
                .IsRequired()
                .HasMaxLength(200);
            e.Property(b => b.Channel).HasConversion<string>();
            e.Property(b => b.Mode).HasConversion<string>();
            e.Property(b => b.Decision).HasConversion<string>();
            e.Property(b => b.Reason)
                .IsRequired()
                .HasMaxLength(200);
            e.Property(b => b.CreatedAt).HasConversion(UnixMillisConverter);
            e.HasIndex(b => new { b.EventId, b.UserId, b.Channel }).IsUnique();
            e.HasIndex(b => b.WorkItemId);
            e.HasIndex(b => b.CreatedAt);
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
            e.HasOne(dr => dr.WorkItem)
                .WithMany(a => a.DiffResults)
                .HasForeignKey(dr => dr.WorkItemId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(dr => dr.GitRepository)
                .WithMany(gr => gr.DiffResults)
                .HasForeignKey(dr => dr.GitRepositoryId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);
            e.HasOne(dr => dr.LogEntry)
                .WithOne(pe => pe.DiffResult)
                .HasForeignKey<DiffResult>(dr => dr.LogEntryId)
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
            e.HasIndex(dr => dr.WorkItemId);
            e.HasIndex(dr => dr.GitRepositoryId);
            e.HasIndex(dr => new { dr.WorkItemId, dr.FilePath });
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

        PinLegacySchemaNames(modelBuilder);
    }

    /// <summary>
    /// Bildet einen Enum-Konverter, der für umbenannte Member die bisher persistierten
    /// (deutschen) String-Werte beibehält. Nicht aufgeführte Member werden per Namen abgebildet.
    /// </summary>
    private static ValueConverter<TEnum, string> EnumLegacyConverter<TEnum>(params (TEnum Value, string Persisted)[] map)
        where TEnum : struct, Enum
    {
        var toPersisted = map.ToDictionary(x => x.Value, x => x.Persisted);
        var toEnum = map.ToDictionary(x => x.Persisted, x => x.Value);
        return new ValueConverter<TEnum, string>(
            v => MapEnumToPersisted(v, toPersisted),
            s => MapPersistedToEnum(s, toEnum));
    }

    private static string MapEnumToPersisted<TEnum>(TEnum value, Dictionary<TEnum, string> map)
        where TEnum : struct, Enum
        => map.TryGetValue(value, out var s) ? s : value.ToString();

    private static TEnum MapPersistedToEnum<TEnum>(string persisted, Dictionary<string, TEnum> map)
        where TEnum : struct, Enum
        => map.TryGetValue(persisted, out var v) ? v : Enum.Parse<TEnum>(persisted);

    /// <summary>
    /// Pint Tabellen-, Spalten- und persistierte Enum-Werte auf die bisherigen deutschen
    /// Bezeichner, damit die Umbenennung der CLR-Typen keine Schema- oder Datenänderung bewirkt.
    /// </summary>
    private static void PinLegacySchemaNames(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Project>(e =>
        {
            e.ToTable("Projekte");
            e.Property(p => p.Description).HasColumnName("Beschreibung");
            e.Property(p => p.CreatedAt).HasColumnName("ErstellungsDatum");
            e.Property(p => p.Status).HasConversion(EnumLegacyConverter(
                (ProjectStatus.Active, "Aktiv"), (ProjectStatus.Archived, "Archiviert")));
        });
        modelBuilder.Entity<WorkItem>(e =>
        {
            e.ToTable("Aufgaben");
            e.Property(a => a.CompletedAt).HasColumnName("AbschlussDatum");
            e.Property(a => a.AgentName).HasColumnName("AgentenName");
            e.Property(a => a.AgentPackageName).HasColumnName("AgentenpaketName");
            e.Property(a => a.ActiveRunId).HasColumnName("AktiveRunId");
            e.Property(a => a.RequirementDescription).HasColumnName("AnforderungsBeschreibung");
            e.Property(a => a.ExecutionStatus).HasColumnName("AusfuehrungsStatus");
            e.Property(a => a.BaseBranchName).HasColumnName("BasisBranchName");
            e.Property(a => a.CreatedAt).HasColumnName("ErstellungsDatum");
            e.Property(a => a.AiPluginPrefix).HasColumnName("KiPluginPrefix");
            e.Property(a => a.RunStatus).HasColumnName("LaufStatus");
            e.Property(a => a.LastCliStartUtc).HasColumnName("LetzterCliStartUtc");
            e.Property(a => a.LocalClonePath).HasColumnName("LokalerKlonPfad");
            e.Property(a => a.PausedUntilUtc).HasColumnName("PausiertBisUtc");
            e.Property(a => a.ProjectId).HasColumnName("ProjektId");
            e.Property(a => a.ScmCompletionDetectedUtc).HasColumnName("ScmAbschlussErkanntUtc");
            e.Property(a => a.ScmCompletionReference).HasColumnName("ScmAbschlussReferenz");
            e.Property(a => a.Title).HasColumnName("Titel");
            e.Property(a => a.SuggestedExecuteAfterUtc).HasColumnName("VorschlagAusfuehrenAbUtc");
            e.Property(a => a.SuggestedPrompt).HasColumnName("VorschlagPrompt");
            e.Property(a => a.Purpose).HasColumnName("Zweck");
            e.Property(a => a.Status).HasConversion(EnumLegacyConverter(
                (WorkItemStatus.New, "Neu"), (WorkItemStatus.Started, "Gestartet"),
                (WorkItemStatus.Waiting, "Wartend"), (WorkItemStatus.Completed, "Beendet"),
                (WorkItemStatus.Archived, "Archiviert")));
            e.Property(a => a.ExecutionStatus).HasConversion(EnumLegacyConverter(
                (WorkItemExecutionStatus.NotStarted, "NichtGestartet"),
                (WorkItemExecutionStatus.Active, "Aktiv"),
                (WorkItemExecutionStatus.Completed, "Beendet")));
            e.Property(a => a.RunStatus).HasConversion(EnumLegacyConverter(
                (WorkItemRunStatus.Running, "Laeuft"),
                (WorkItemRunStatus.WaitingForInput, "WartetAufEingabe")));
        });
        modelBuilder.Entity<IssueReference>(e =>
        {
            e.ToTable("IssueReferenzen");
            e.Property(i => i.WorkItemId).HasColumnName("AufgabeId");
            e.Property(i => i.IssueNumber).HasColumnName("IssueNummer");
            e.Property(i => i.Title).HasColumnName("Titel");
        });
        modelBuilder.Entity<AlertReference>(e =>
        {
            e.ToTable("AlertReferenzen");
            e.Property(i => i.WorkItemId).HasColumnName("AufgabeId");
            e.Property(i => i.Title).HasColumnName("Titel");
        });
        modelBuilder.Entity<PullRequestReference>(e =>
        {
            e.ToTable("PullRequestReferenzen");
            e.Property(p => p.WorkItemId).HasColumnName("AufgabeId");
            e.Property(p => p.Role).HasColumnName("Rolle");
            e.Property(p => p.Title).HasColumnName("Titel");
        });
        modelBuilder.Entity<PullRequestWorkflowRun>()
            .Property(w => w.PullRequestReferenceId).HasColumnName("PullRequestReferenzId");
        modelBuilder.Entity<LogEntry>(e =>
        {
            e.ToTable("Protokolleintraege");
            e.Property(p => p.WorkItemId).HasColumnName("AufgabeId");
            e.Property(p => p.Content).HasColumnName("Inhalt");
            e.Property(p => p.Type).HasColumnName("Typ");
            e.Property(p => p.Timestamp).HasColumnName("Zeitstempel");
            e.Property(p => p.Type).HasConversion(EnumLegacyConverter(
                (LogEntryType.AiResponse, "KiAntwort"),
                (LogEntryType.StatusTransition, "StatusUebergang"),
                (LogEntryType.TestResult, "TestErgebnis"),
                (LogEntryType.GitAction, "GitAktion"),
                (LogEntryType.SystemMessage, "SystemMeldung")));
        });
        modelBuilder.Entity<TestResult>(e =>
        {
            e.ToTable("TestErgebnisse");
            e.Property(t => t.Duration).HasColumnName("Dauer");
            e.Property(t => t.ErrorMessage).HasColumnName("Fehlermeldung");
            e.Property(t => t.LogEntryId).HasColumnName("ProtokollEintragId");
            e.Property(t => t.Status).HasConversion(EnumLegacyConverter(
                (TestStatus.Passed, "Bestanden"), (TestStatus.Failed, "Fehlgeschlagen"),
                (TestStatus.Skipped, "Uebersprungen")));
        });
        modelBuilder.Entity<PluginConfiguration>(e =>
        {
            e.ToTable("PluginKonfigurationen");
            e.Property(p => p.Enabled).HasColumnName("Aktiviert");
            e.Property(p => p.DisplayName).HasColumnName("AnzeigeName");
            e.Property(p => p.PluginCategory).HasColumnName("PluginKategorie");
            e.Property(p => p.PluginType).HasColumnName("PluginTyp");
            e.Property(p => p.PluginCategory).HasConversion(EnumLegacyConverter(
                (PluginCategory.Ai, "Ki")));
        });
        modelBuilder.Entity<AppSetting>(e =>
        {
            e.ToTable("AppEinstellungen");
            e.Property(a => a.Key).HasColumnName("Schluessel");
            e.Property(a => a.Value).HasColumnName("Wert");
            e.Property(a => a.UpdatedAt).HasColumnName("AktualisiertAm");
        });
        modelBuilder.Entity<PromptTemplate>(e =>
        {
            e.ToTable("PromptVorlagen");
            e.Property(p => p.PromptText).HasColumnName("Prompttext");
            e.Property(p => p.SortOrder).HasColumnName("Sortierung");
            e.Property(p => p.CreatedAt).HasColumnName("ErstelltAm");
            e.Property(p => p.UpdatedAt).HasColumnName("AktualisiertAm");
        });
        modelBuilder.Entity<NotificationSetting>(e =>
        {
            e.ToTable("BenachrichtigungsEinstellungen");
            e.Property(b => b.UserId).HasColumnName("BenutzerId");
            e.Property(b => b.BannerMode).HasColumnName("BannerModus");
            e.Property(b => b.SoundMode).HasColumnName("TonModus");
            e.Property(b => b.UpdatedAt).HasColumnName("AktualisiertAm");
            e.Property(b => b.BannerMode).HasConversion(NotificationModeConverter);
            e.Property(b => b.SoundMode).HasConversion(NotificationModeConverter);
        });
        modelBuilder.Entity<NotificationAudioFile>(e =>
        {
            e.ToTable("BenachrichtigungsAudioDateien");
            e.Property(b => b.UserId).HasColumnName("BenutzerId");
            e.Property(b => b.SizeBytes).HasColumnName("GroesseBytes");
            e.Property(b => b.UploadedAt).HasColumnName("HochgeladenAm");
            e.Property(b => b.Content).HasColumnName("Inhalt");
            e.Property(b => b.OriginalFileName).HasColumnName("OriginalDateiname");
        });
        modelBuilder.Entity<NotificationDispatchLog>(e =>
        {
            e.ToTable("BenachrichtigungsDispatchLogs");
            e.Property(b => b.WorkItemId).HasColumnName("AufgabeId");
            e.Property(b => b.UserId).HasColumnName("BenutzerId");
            e.Property(b => b.Decision).HasColumnName("Entscheidung");
            e.Property(b => b.EventId).HasColumnName("EreignisId");
            e.Property(b => b.CreatedAt).HasColumnName("ErstelltAm");
            e.Property(b => b.Reason).HasColumnName("Grund");
            e.Property(b => b.Channel).HasColumnName("Kanal");
            e.Property(b => b.Mode).HasColumnName("Modus");
            e.Property(b => b.Channel).HasConversion(EnumLegacyConverter(
                (NotificationChannel.Sound, "Ton")));
            e.Property(b => b.Mode).HasConversion(NotificationModeConverter);
            e.Property(b => b.Decision).HasConversion(EnumLegacyConverter(
                (NotificationDecision.Sent, "Gesendet"),
                (NotificationDecision.Suppressed, "Unterdrueckt"),
                (NotificationDecision.Deferred, "Zurueckgestellt"),
                (NotificationDecision.Failed, "Fehlgeschlagen")));
        });
        modelBuilder.Entity<Todo>(e =>
        {
            e.Property(t => t.WorkItemId).HasColumnName("AufgabeId");
            e.Property(t => t.Description).HasColumnName("Beschreibung");
            e.Property(t => t.CompletedAt).HasColumnName("ErledigtAm");
            e.Property(t => t.CreatedAt).HasColumnName("ErstellungsDatum");
        });
        modelBuilder.Entity<ScheduledPrompt>(e =>
        {
            e.ToTable("ZeitgesteuertePrompts");
            e.Property(z => z.WorkItemId).HasColumnName("AufgabeId");
            e.Property(z => z.TargetTimeUtc).HasColumnName("ZielZeitpunktUtc");
            e.Property(z => z.CreatedAtUtc).HasColumnName("ErstelltAmUtc");
            e.Property(z => z.UpdatedAtUtc).HasColumnName("AktualisiertAmUtc");
        });
        modelBuilder.Entity<ScmIncomingEvent>(e =>
        {
            e.Property(x => x.WorkItemId).HasColumnName("AufgabeId");
            e.Property(x => x.Purpose).HasColumnName("Zweck");
        });
        modelBuilder.Entity<ScmTaskLease>()
            .Property(x => x.WorkItemId).HasColumnName("AufgabeId");
        modelBuilder.Entity<DiffResult>(e =>
        {
            e.Property(dr => dr.WorkItemId).HasColumnName("AufgabeId");
            e.Property(dr => dr.LogEntryId).HasColumnName("ProtokollEintragId");
        });
        modelBuilder.Entity<GitRepository>(e =>
        {
            e.Property(r => r.Active).HasColumnName("Aktiv");
            e.Property(r => r.IssuePreparationEnabled).HasColumnName("IssueAufbereitungAktiv");
            e.Property(r => r.PluginType).HasColumnName("PluginTyp");
            e.Property(r => r.ProjectId).HasColumnName("ProjektId");
        });
        modelBuilder.Entity<RepositoryStartConfiguration>(e =>
        {
            e.ToTable("RepositoryStartKonfigurationen");
            e.Property(c => c.Active).HasColumnName("Aktiv");
        });
        modelBuilder.Entity<RepositoryInitializationConfiguration>(e =>
        {
            e.ToTable("RepositoryInitialisierungKonfigurationen");
            e.Property(c => c.Active).HasColumnName("Aktiv");
            e.Property(c => c.InitializationScriptRelativePath)
                .HasColumnName("InitialisierungsskriptRelativePath");
        });
    }

    private static readonly ValueConverter<NotificationMode, string> NotificationModeConverter =
        EnumLegacyConverter(
            (NotificationMode.Disabled, "Deaktiviert"),
            (NotificationMode.Sound, "Ton"));
}
