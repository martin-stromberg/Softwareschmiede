using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Infrastructure.Data;

namespace Softwareschmiede.Application.Services;

/// <summary>Service für Aufgabenverwaltung (CRUD + Lebenszyklus).</summary>
public sealed class WorkItemService : IActiveWorkItemsService
{
    // AktivOderWartendStatus ist die einzige Quelle der Wahrheit für die Regel "Aufgabe ist aktiv oder
    // wartend" (siehe AufgabeStatusExtensions.IstAktivOderWartend). Für EF-Core-Queries wird die Array-
    // Variante referenziert (Contains wird nach SQL IN übersetzt); außerhalb von Queries wird direkt
    // AufgabeStatus.IstAktivOderWartend() verwendet (z. B. in DeleteAsync).
    private static readonly Expression<Func<WorkItem, bool>> IsActiveOrWaitingPredicate =
        a => WorkItemStatusExtensions.ActiveOrWaitingStatuses.Contains(a.Status);

    // Serialisiert prozessweit alle Laufdaten-Mutationen einer Aufgabe (AktivenLaufSetzen/
    // Sicherstellen/Beenden sowie die laufdatenrücksetzenden Lebenszyklus-Pfade Starten/
    // Abschliessen/StartZuruecksetzen). AufgabeService-Instanzen sind scoped, die Schreibpfade
    // konvergieren aber aus mehreren Quellen (CliProcessManager-Selbstheilung,
    // KiAusfuehrungsService-Exit-Pfad, ViewModel-Aufrufe) in unterschiedlichen DbContexts auf
    // derselben Row: ohne gemeinsame Sperre kann ein Sicherstellen-Save nach dem Beenden-Save
    // landen und einen Phantom-Lauf wiederbeleben (Issue 342). Einträge werden bewusst nie
    // entfernt — das Entfernen während wartender Aufrufer wäre selbst eine Race-Bedingung, und
    // der Speicherbedarf bleibt durch die Anzahl der Aufgaben begrenzt.
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> RunGates = new();

    private readonly SoftwareschmiedeDbContext _db;
    private readonly ILogger<WorkItemService> _logger;
    private readonly TodoService _todoService;
    private readonly ScmStartCheckTrigger? _startCheckTrigger;

    /// <inheritdoc cref="WorkItemService"/>
    /// <param name="db">Datenbankkontext.</param>
    /// <param name="logger">Logger.</param>
    /// <param name="todoService">Dienst für Aufgaben-To-dos.</param>
    /// <param name="startCheckTrigger">Optionaler Auslöser der SCM-Startprüfung;
    /// wird bei Pausenänderungen benachrichtigt. <see langword="null"/> in manuell
    /// aufgebauten Diensten (z. B. Tests) — dann entfällt die Benachrichtigung.</param>
    public WorkItemService(
        SoftwareschmiedeDbContext db,
        ILogger<WorkItemService> logger,
        TodoService todoService,
        ScmStartCheckTrigger? startCheckTrigger = null)
    {
        _db = db;
        _logger = logger;
        _todoService = todoService;
        _startCheckTrigger = startCheckTrigger;
    }

    /// <summary>Gibt alle aktiven (nicht archivierten) Aufgaben eines Projekts zurück.</summary>
    public async Task<IReadOnlyList<WorkItem>> GetByProjektAsync(Guid projectId, CancellationToken ct = default)
    {
        _logger.LogInformation("Aufgaben für Projekt {ProjektId} abrufen.", projectId);
        return await _db.WorkItems
            .AsNoTracking()
            .Include(a => a.IssueReference)
            .Include(a => a.AlertReference)
            .Include(a => a.PullRequests)
            .Where(a => a.ProjectId == projectId && a.Status != WorkItemStatus.Archived)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(ct);
    }

    /// <summary>Gibt alle archivierten Aufgaben eines Projekts zurück.</summary>
    public async Task<IReadOnlyList<WorkItem>> GetArchivedByProjectAsync(Guid projectId, CancellationToken ct = default)
    {
        _logger.LogInformation("Archivierte Aufgaben für Projekt {ProjektId} abrufen.", projectId);
        return await _db.WorkItems
            .AsNoTracking()
            .Where(a => a.ProjectId == projectId && a.Status == WorkItemStatus.Archived)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(ct);
    }

    /// <summary>Gibt die Anzahl aktiver (Gestartet) und wartender (Wartend) Aufgaben als Tupel zurück — eine einzige Abfrage.</summary>
    public async Task<(int Active, int Waiting)> GetActiveAndWaitingCountAsync(CancellationToken ct = default)
    {
        var counts = await _db.WorkItems
            .AsNoTracking()
            .Where(IsActiveOrWaitingPredicate)
            .GroupBy(a => a.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var aktiv = counts.FirstOrDefault(c => c.Status == WorkItemStatus.Started)?.Count ?? 0;
        var waiting = counts.FirstOrDefault(c => c.Status == WorkItemStatus.Waiting)?.Count ?? 0;
        return (aktiv, waiting);
    }

    /// <summary>Gibt eine Aufgabe anhand ihrer ID zurück.</summary>
    public async Task<WorkItem?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        _logger.LogInformation("Aufgabe {AufgabeId} abrufen.", id);
        return await _db.WorkItems
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, ct);
    }

    /// <summary>Gibt eine Aufgabe mit IssueReferenz zurück (ohne Protokolleinträge).</summary>
    public async Task<WorkItem?> GetDetailAsync(Guid id, CancellationToken ct = default)
    {
        _logger.LogInformation("Aufgabe {AufgabeId} mit Details abrufen.", id);
        return await _db.WorkItems
            .AsNoTracking()
            .Include(a => a.Project)
            .Include(a => a.IssueReference)
            .Include(a => a.AlertReference)
            .Include(a => a.PullRequests)
            .Include(a => a.GitRepository)
                .ThenInclude(r => r!.StartConfiguration)
            .Include(a => a.GitRepository)
                .ThenInclude(r => r!.InitializationConfiguration)
            .Include(a => a.Todos)
            .FirstOrDefaultAsync(a => a.Id == id, ct);
    }

    /// <summary>Gibt eine bereits aus einem Alert erzeugte Aufgabe anhand des Alert-SourceKeys zurück.</summary>
    public async Task<WorkItem?> GetByAlertSourceKeyAsync(string sourceKey, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceKey);

        return await _db.WorkItems
            .AsNoTracking()
            .Include(a => a.IssueReference)
            .Include(a => a.AlertReference)
            .FirstOrDefaultAsync(a => a.AlertReference != null && a.AlertReference.SourceKey == sourceKey, ct);
    }

    /// <summary>Prueft global, ob ein Pull Request bereits einer Aufgabe zugeordnet ist.</summary>
    public async Task<bool> IsPullRequestLinkedAsync(
        PullRequestProvider provider,
        string repositoryId,
        int pullRequestNumber,
        CancellationToken ct = default)
    {
        var normalizedRepositoryId = PullRequestRepositoryId.Normalize(provider, repositoryId);
        return await _db.PullRequestReferences
            .AsNoTracking()
            .AnyAsync(p => p.Provider == provider
                           && p.RepositoryId == normalizedRepositoryId
                           && p.PullRequestNumber == pullRequestNumber,
                ct);
    }

    /// <summary>Gibt die ID des zuletzt generierten Diff-Ergebnisses einer Aufgabe zurück.</summary>
    public async Task<Guid?> GetLatestDiffResultIdAsync(Guid workItemId, CancellationToken ct = default)
    {
        _logger.LogInformation("Letzte DiffResult-ID für Aufgabe {AufgabeId} abrufen.", workItemId);

        return await _db.DiffResults
            .AsNoTracking()
            .Where(dr => dr.WorkItemId == workItemId)
            .OrderByDescending(dr => dr.GeneratedAt)
            .Select(dr => (Guid?)dr.Id)
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>Gibt die ID des zuletzt generierten Diff-Ergebnisses einer Datei innerhalb einer Aufgabe zurück.</summary>
    public async Task<Guid?> GetLatestDiffResultIdForFileAsync(Guid workItemId, string relativePath, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        var normalizedLookupPath = NormalizeRelativePathForLookup(relativePath);

        _logger.LogInformation(
            "Dateispezifische DiffResult-ID für Aufgabe {AufgabeId} und Datei {RelativePath} abrufen.",
            workItemId,
            normalizedLookupPath);

        return await _db.DiffResults
            .AsNoTracking()
            .Where(dr => dr.WorkItemId == workItemId
                && dr.FilePath != null
                && dr.FilePath.Replace("\\", "/").ToLower() == normalizedLookupPath)
            .OrderByDescending(dr => dr.GeneratedAt)
            .Select(dr => (Guid?)dr.Id)
            .FirstOrDefaultAsync(ct);
    }

    private static string NormalizeRelativePathForLookup(string relativePath)
    {
        var normalized = relativePath.Trim().Replace('\\', '/');
        while (normalized.StartsWith("./", StringComparison.Ordinal))
        {
            normalized = normalized[2..];
        }

        return normalized.TrimStart('/').ToLowerInvariant();
    }

    /// <summary>Erstellt eine neue Aufgabe mit Status <see cref="WorkItemStatus.New"/>.</summary>
    public async Task<WorkItem> CreateAsync(
        Guid projectId,
        string titel,
        string? requirementDescription,
        Guid? gitRepositoryId = null,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Aufgabe '{Titel}' für Projekt {ProjektId} erstellen.", titel, projectId);

        var workItem = new WorkItem
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            GitRepositoryId = gitRepositoryId,
            Title = titel,
            RequirementDescription = requirementDescription,
            Status = WorkItemStatus.New,
            ExecutionStatus = WorkItemExecutionStatus.NotStarted,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.WorkItems.Add(workItem);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Aufgabe '{Titel}' mit ID {AufgabeId} erstellt.", titel, workItem.Id);
        return workItem;
    }

    /// <summary>Erstellt eine neue Aufgabe aus einem Issue.</summary>
    public async Task<WorkItem> CreateFromIssueAsync(
        Guid projectId,
        Issue issue,
        Guid? gitRepositoryId = null,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Aufgabe aus Issue #{IssueNummer} für Projekt {ProjektId} erstellen.", issue.Nummer, projectId);

        var workItem = new WorkItem
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            GitRepositoryId = gitRepositoryId,
            Title = issue.Title,
            RequirementDescription = issue.Body,
            Status = WorkItemStatus.New,
            ExecutionStatus = WorkItemExecutionStatus.NotStarted,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var issueReferenz = new IssueReference
        {
            Id = Guid.NewGuid(),
            WorkItemId = workItem.Id,
            IssueNumber = issue.Nummer,
            Title = issue.Title,
            Body = issue.Body,
            LabelsJson = System.Text.Json.JsonSerializer.Serialize(issue.Labels),
            Milestone = issue.Milestone,
            IssueUrl = issue.IssueUrl
        };

        workItem.IssueReference = issueReferenz;

        _db.WorkItems.Add(workItem);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Aufgabe aus Issue #{IssueNummer} mit ID {AufgabeId} erstellt.", issue.Nummer, workItem.Id);
        return workItem;
    }

    /// <summary>Erstellt eine neue Aufgabe aus einer Issue-Ereignisreferenz. Der kanonische
    /// <paramref name="sourceKey"/> verhindert Anlageduplikate; bei paralleler Anlage wird die
    /// bestehende Aufgabe zurückgegeben.</summary>
    /// <param name="projectId">ID des Projekts, dem die Aufgabe zugeordnet wird.</param>
    /// <param name="issue">Die Issue-Anlagedaten der Quelle.</param>
    /// <param name="gitRepositoryId">ID des lokalen Git-Repositorys.</param>
    /// <param name="sourceKey">Der deduplizierende Quellenschlüssel der Aufgabenreferenz.</param>
    /// <param name="ct">Abbruchtoken.</param>
    /// <param name="zweck">Optionale Zweck-Markierung der Aufgabe (siehe <see cref="WorkItem.Purpose"/>);
    /// <see langword="null"/> legt eine reguläre Arbeitsaufgabe an.</param>
    /// <param name="kiPluginPrefix">Optionaler Plugin-Prefix für die funktionale Plugin-Zuordnung
    /// (siehe <see cref="WorkItem.AiPluginPrefix"/>).</param>
    public async Task<WorkItem> CreateFromIssueEventAsync(
        Guid projectId,
        Issue issue,
        Guid gitRepositoryId,
        string sourceKey,
        CancellationToken ct = default,
        string? zweck = null,
        string? kiPluginPrefix = null)
    {
        ArgumentNullException.ThrowIfNull(issue);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceKey);

        _logger.LogInformation("Aufgabe aus Issue-Ereignis {SourceKey} für Projekt {ProjektId} erstellen.", sourceKey, projectId);

        var existing = await _db.IssueReferences
            .AsNoTracking()
            .Where(i => i.SourceKey == sourceKey)
            .Select(i => (Guid?)i.WorkItemId)
            .FirstOrDefaultAsync(ct);
        if (existing is { } existingId)
        {
            _logger.LogInformation("Issue-Ereignis {SourceKey} ist bereits als Aufgabe {AufgabeId} angelegt.", sourceKey, existingId);
            return await _db.WorkItems.AsNoTracking().SingleAsync(a => a.Id == existingId, ct);
        }

        var workItem = new WorkItem
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            GitRepositoryId = gitRepositoryId,
            Title = issue.Title,
            RequirementDescription = issue.Body,
            Status = WorkItemStatus.New,
            ExecutionStatus = WorkItemExecutionStatus.NotStarted,
            CreatedAt = DateTimeOffset.UtcNow,
            Purpose = zweck,
            AiPluginPrefix = kiPluginPrefix
        };

        var issueReferenz = CreateIssueReferenz(workItem.Id, issue);
        issueReferenz.SourceKey = sourceKey;
        workItem.IssueReference = issueReferenz;

        _db.WorkItems.Add(workItem);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            _db.ChangeTracker.Clear();
            var concurrentlyCreated = await _db.IssueReferences
                .AsNoTracking()
                .Where(i => i.SourceKey == sourceKey)
                .Select(i => (Guid?)i.WorkItemId)
                .FirstOrDefaultAsync(ct);
            if (concurrentlyCreated is { } workItemId)
            {
                _logger.LogWarning(ex, "Issue-Ereignis {SourceKey} wurde parallel angelegt.", sourceKey);
                return await _db.WorkItems.AsNoTracking().SingleAsync(a => a.Id == workItemId, ct);
            }

            throw;
        }

        _logger.LogInformation("Aufgabe aus Issue-Ereignis {SourceKey} mit ID {AufgabeId} erstellt.", sourceKey, workItem.Id);
        return workItem;
    }

    /// <summary>Erstellt eine neue Aufgabe aus einem Pull-Request-Vorschlag.</summary>
    public async Task<WorkItem> CreateFromPullRequestAsync(
        Guid projectId,
        PullRequest pullRequest,
        ScmRepositoryContext repositoryContext,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(pullRequest);
        ArgumentNullException.ThrowIfNull(repositoryContext);

        if (pullRequest.Nummer <= 0)
            throw new InvalidOperationException("Der Pull Request besitzt keine gueltige Nummer.");
        if (string.IsNullOrWhiteSpace(pullRequest.BranchName))
            throw new InvalidOperationException("Der Pull Request besitzt keinen Quell-Branch.");

        var provider = pullRequest.Provider;
        var repositoryId = PullRequestRepositoryId.Normalize(provider, pullRequest.RepositoryId ?? repositoryContext.RepositoryId);
        var sourceRepositoryId = PullRequestRepositoryId.Normalize(provider, pullRequest.SourceRepositoryId ?? repositoryId);
        if (!string.Equals(repositoryId, repositoryContext.RepositoryId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Der Pull-Request-Vorschlag passt nicht zum Repository-Kontext.");

        if (string.IsNullOrWhiteSpace(pullRequest.SourceRef) && string.IsNullOrWhiteSpace(pullRequest.SourceRepositoryUrl))
            throw new InvalidOperationException("Der Pull Request besitzt keine auscheckbare Quell-Referenz.");

        var repositoryExists = await _db.GitRepositories
            .AsNoTracking()
            .AnyAsync(r => r.Id == repositoryContext.GitRepositoryId
                           && r.ProjectId == projectId
                           && r.Active
                           && r.PluginType == repositoryContext.PluginPrefix,
                ct);
        if (!repositoryExists)
            throw new InvalidOperationException("Der Repository-Kontext des Pull-Request-Vorschlags ist nicht mehr gueltig.");

        var alreadyLinked = await _db.PullRequestReferences
            .AsNoTracking()
            .AnyAsync(p => p.Provider == provider
                           && p.RepositoryId == repositoryId
                           && p.PullRequestNumber == pullRequest.Nummer,
                ct);
        if (alreadyLinked)
            throw new InvalidOperationException("Dieser Pull Request ist bereits einer Aufgabe zugeordnet.");

        var now = DateTimeOffset.UtcNow;
        var requirementDescription = BuildPullRequestRequirementDescription(pullRequest);
        var workItem = new WorkItem
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            GitRepositoryId = repositoryContext.GitRepositoryId,
            Title = pullRequest.Title,
            RequirementDescription = requirementDescription,
            Status = WorkItemStatus.New,
            ExecutionStatus = WorkItemExecutionStatus.NotStarted,
            CreatedAt = now
        };

        workItem.PullRequests.Add(new PullRequestReference
        {
            Id = Guid.NewGuid(),
            WorkItemId = workItem.Id,
            Provider = provider,
            Role = PullRequestReferenzRolle.ReviewSource,
            RepositoryId = repositoryId,
            PullRequestNumber = pullRequest.Nummer,
            ProviderPullRequestId = pullRequest.ProviderPullRequestId,
            Url = pullRequest.Url,
            Title = pullRequest.Title,
            SourceBranch = pullRequest.BranchName,
            SourceRepositoryId = sourceRepositoryId,
            SourceRepositoryUrl = pullRequest.SourceRepositoryUrl,
            SourceRef = pullRequest.SourceRef,
            TargetBranch = pullRequest.TargetBranch ?? string.Empty,
            HeadSha = pullRequest.HeadSha,
            Status = PullRequestStatus.Open,
            MergeStatus = PullRequestMergeStatus.Unknown,
            MonitoringPhase = PullRequestMonitoringPolicy.GetInitialPhase(provider),
            NextCheckUtc = PullRequestMonitoringPolicy.GetInitialPhase(provider) == PullRequestMonitoringPhase.NotMonitored ? null : now,
            LastError = null,
            CreatedUtc = now
        });

        _db.WorkItems.Add(workItem);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            _db.ChangeTracker.Clear();
            var concurrentlyLinked = await _db.PullRequestReferences
                .AsNoTracking()
                .AnyAsync(p => p.Provider == provider
                               && p.RepositoryId == repositoryId
                               && p.PullRequestNumber == pullRequest.Nummer,
                    ct);
            if (concurrentlyLinked)
                throw new InvalidOperationException("Dieser Pull Request ist bereits einer Aufgabe zugeordnet.", ex);

            throw;
        }

        return workItem;
    }

    private static string BuildPullRequestRequirementDescription(PullRequest pullRequest)
    {
        var parts = new List<string> { pullRequest.Title };

        if (!string.IsNullOrWhiteSpace(pullRequest.Body))
        {
            var body = CleanPullRequestBody(pullRequest.Body);
            if (!string.IsNullOrWhiteSpace(body))
            {
                parts.Add(body);
            }
        }

        if (!string.IsNullOrWhiteSpace(pullRequest.Url))
        {
            parts.Add(pullRequest.Url);
        }

        return string.Join($"{Environment.NewLine}{Environment.NewLine}", parts);
    }

    private static string CleanPullRequestBody(string body)
    {
        var text = body.ReplaceLineEndings("\n");

        text = Regex.Replace(text, @"(?m)^\[//\]:\s*#\s*\([^\n]*\)\s*$", string.Empty);
        text = Regex.Replace(text, @"<!--.*?-->", string.Empty, RegexOptions.Singleline);
        text = Regex.Replace(text, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"<li[^>]*>", "- ", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"</(p|div|blockquote|details|summary|h[1-6]|ul|ol|li)>", "\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"<code[^>]*>(.*?)</code>", "`$1`", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        text = Regex.Replace(text, @"<a\s+[^>]*href\s*=\s*[""'](?<href>[^""']+)[""'][^>]*>(?<text>.*?)</a>", "${text}", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        text = Regex.Replace(text, @"<[^>]+>", string.Empty);
        text = WebUtility.HtmlDecode(text);
        text = Regex.Replace(text, @"[ \t]+\n", "\n");
        text = Regex.Replace(text, @"\n{3,}", "\n\n");

        return text.Trim();
    }

    /// <summary>Erstellt eine neue Aufgabe aus einem Alert und verknüpft sie mit dem bereits erstellten externen Issue.</summary>
    public async Task<WorkItem> CreateFromAlertAsync(
        Guid projectId,
        ScmAlert alert,
        Issue createdIssue,
        Guid? gitRepositoryId = null,
        string provider = "",
        string repositoryId = "",
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(alert);
        ArgumentNullException.ThrowIfNull(createdIssue);

        _logger.LogInformation("Aufgabe aus Alert {SourceKey} für Projekt {ProjektId} erstellen.", alert.SourceKey, projectId);

        var existing = await _db.WorkItems
            .AsNoTracking()
            .Include(a => a.IssueReference)
            .Include(a => a.AlertReference)
            .FirstOrDefaultAsync(a => a.AlertReference != null && a.AlertReference.SourceKey == alert.SourceKey, ct);
        if (existing != null)
        {
            _logger.LogInformation("Alert {SourceKey} ist bereits als Aufgabe {AufgabeId} konvertiert.", alert.SourceKey, existing.Id);
            return existing;
        }

        var workItem = new WorkItem
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            GitRepositoryId = gitRepositoryId,
            Title = alert.Title,
            RequirementDescription = string.IsNullOrWhiteSpace(createdIssue.Body)
                ? alert.Description
                : createdIssue.Body,
            Status = WorkItemStatus.New,
            ExecutionStatus = WorkItemExecutionStatus.NotStarted,
            CreatedAt = DateTimeOffset.UtcNow
        };

        workItem.IssueReference = CreateIssueReferenz(workItem.Id, createdIssue);
        workItem.AlertReference = new AlertReference
        {
            Id = Guid.NewGuid(),
            WorkItemId = workItem.Id,
            Provider = provider,
            RepositoryId = repositoryId,
            AlertType = alert.AlertType.ToString(),
            SourceKey = alert.SourceKey,
            AlertUrl = alert.AlertUrl,
            Title = alert.Title,
            Severity = alert.Severity,
            State = alert.State,
            RuleId = alert.RuleId,
            ToolName = alert.ToolName
        };

        _db.WorkItems.Add(workItem);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            _db.ChangeTracker.Clear();
            var concurrentlyCreated = await _db.WorkItems
                .AsNoTracking()
                .Include(a => a.IssueReference)
                .Include(a => a.AlertReference)
                .FirstOrDefaultAsync(a => a.AlertReference != null && a.AlertReference.SourceKey == alert.SourceKey, ct);
            if (concurrentlyCreated != null)
            {
                _logger.LogWarning(ex, "Alert {SourceKey} wurde parallel konvertiert.", alert.SourceKey);
                return concurrentlyCreated;
            }

            throw;
        }

        _logger.LogInformation("Aufgabe aus Alert {SourceKey} mit ID {AufgabeId} erstellt.", alert.SourceKey, workItem.Id);
        return workItem;
    }

    /// <summary>Aktualisiert Titel, Beschreibung und KI-Plugin-Prefix einer Aufgabe.</summary>
    public async Task UpdateAsync(
        Guid id,
        string titel,
        string? requirementDescription,
        string? kiPluginPrefix = null,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Aufgabe {AufgabeId} aktualisieren.", id);

        var workItem = await _db.WorkItems.FindAsync([id], ct)
            ?? throw new InvalidOperationException($"Aufgabe {id} nicht gefunden.");

        workItem.Title = titel;
        workItem.RequirementDescription = requirementDescription;
        workItem.AiPluginPrefix = kiPluginPrefix;

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Aufgabe {AufgabeId} aktualisiert.", id);
    }

    /// <summary>Setzt die IssueReferenz einer Aufgabe. Übergabe von null entfernt die bestehende Referenz.</summary>
    public async Task UpdateIssueReferenzAsync(Guid id, Issue? issue, CancellationToken ct = default)
    {
        _logger.LogInformation("IssueReferenz für Aufgabe {AufgabeId} aktualisieren.", id);

        var workItem = await _db.WorkItems
            .Include(a => a.IssueReference)
            .FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new InvalidOperationException($"Aufgabe {id} nicht gefunden.");

        if (issue == null)
        {
            if (workItem.IssueReference != null)
                _db.Remove(workItem.IssueReference);
            workItem.IssueReference = null;
        }
        else
        {
            if (workItem.IssueReference != null)
            {
                workItem.IssueReference.IssueNumber = issue.Nummer;
                workItem.IssueReference.Title = issue.Title;
                workItem.IssueReference.Body = issue.Body;
                workItem.IssueReference.LabelsJson = System.Text.Json.JsonSerializer.Serialize(issue.Labels);
                workItem.IssueReference.Milestone = issue.Milestone;
                workItem.IssueReference.IssueUrl = issue.IssueUrl;
            }
            else
            {
                var neueReferenz = new IssueReference
                {
                    Id = Guid.NewGuid(),
                    WorkItemId = workItem.Id,
                    IssueNumber = issue.Nummer,
                    Title = issue.Title,
                    Body = issue.Body,
                    LabelsJson = System.Text.Json.JsonSerializer.Serialize(issue.Labels),
                    Milestone = issue.Milestone,
                    IssueUrl = issue.IssueUrl
                };
                workItem.IssueReference = neueReferenz;
                _db.IssueReferences.Add(neueReferenz);
            }
        }

        await _db.SaveChangesAsync(ct);
        _db.ChangeTracker.Clear();
        _logger.LogInformation("IssueReferenz für Aufgabe {AufgabeId} aktualisiert.", id);
    }

    /// <summary>
    /// Weist einer Aufgabe eine IssueReferenz nur dann zu, wenn noch keine Referenz existiert.
    /// Gibt false zurück, wenn die Aufgabe bereits eine Referenz besitzt oder parallel eine Referenz gesetzt wurde.
    /// </summary>
    public async Task<bool> TryAssignIssueReferenzIfNoneAsync(Guid id, Issue issue, CancellationToken ct = default)
        => await TryAssignIssueReferenzAndUpdateDescriptionIfNoneAsync(id, issue, false, null, ct);

    /// <summary>
    /// Weist einer Aufgabe eine IssueReferenz nur dann zu, wenn noch keine Referenz existiert,
    /// und aktualisiert optional die Aufgabenbeschreibung im selben Speichervorgang.
    /// Gibt false zurück, wenn die Aufgabe bereits eine Referenz besitzt oder parallel eine Referenz gesetzt wurde.
    /// </summary>
    public async Task<bool> TryAssignIssueReferenzAndUpdateDescriptionIfNoneAsync(
        Guid id,
        Issue issue,
        bool updateRequirementDescription,
        string? requirementDescription,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(issue);

        _logger.LogInformation("IssueReferenz für Aufgabe {AufgabeId} zuweisen, falls noch keine Referenz existiert.", id);

        var aufgabeExists = await _db.WorkItems
            .AsNoTracking()
            .AnyAsync(a => a.Id == id, ct);
        if (!aufgabeExists)
        {
            throw new InvalidOperationException($"Aufgabe {id} nicht gefunden.");
        }

        if (await _db.IssueReferences.AsNoTracking().AnyAsync(i => i.WorkItemId == id, ct))
        {
            return false;
        }

        var workItem = await _db.WorkItems.FindAsync([id], ct)
            ?? throw new InvalidOperationException($"Aufgabe {id} nicht gefunden.");

        if (updateRequirementDescription)
        {
            workItem.RequirementDescription = requirementDescription;
        }

        _db.IssueReferences.Add(CreateIssueReferenz(id, issue));

        try
        {
            await _db.SaveChangesAsync(ct);
            _db.ChangeTracker.Clear();
            _logger.LogInformation("IssueReferenz für Aufgabe {AufgabeId} neu zugewiesen.", id);
            return true;
        }
        catch (DbUpdateException ex)
        {
            _db.ChangeTracker.Clear();

            if (await _db.IssueReferences.AsNoTracking().AnyAsync(i => i.WorkItemId == id, ct))
            {
                _logger.LogWarning(ex, "IssueReferenz für Aufgabe {AufgabeId} wurde parallel zugewiesen.", id);
                return false;
            }

            throw;
        }
    }

    private static IssueReference CreateIssueReferenz(Guid workItemId, Issue issue) => new()
    {
        Id = Guid.NewGuid(),
        WorkItemId = workItemId,
        IssueNumber = issue.Nummer,
        Title = issue.Title,
        Body = issue.Body,
        LabelsJson = System.Text.Json.JsonSerializer.Serialize(issue.Labels),
        Milestone = issue.Milestone,
        IssueUrl = issue.IssueUrl
    };

    /// <summary>Löscht eine Aufgabe.</summary>
    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        _logger.LogInformation("Aufgabe {AufgabeId} löschen.", id);

        var workItem = await _db.WorkItems.FindAsync([id], ct)
            ?? throw new InvalidOperationException($"Aufgabe {id} nicht gefunden.");

        if (workItem.Status.IsActiveOrWaiting())
        {
            throw new InvalidOperationException(
                $"Aufgabe {id} kann nicht gelöscht werden, da sie aktiv ist (Status: {workItem.Status}). Bitte zuerst abbrechen oder abschließen.");
        }

        _db.WorkItems.Remove(workItem);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Aufgabe {AufgabeId} gelöscht.", id);
    }

    /// <summary>Verwirft eine Aufgabe im Status Neu durch Archivieren oder Löschen.</summary>
    public async Task DiscardAsync(Guid id, DiscardAction action, CancellationToken ct = default)
    {
        _logger.LogInformation("Aufgabe {AufgabeId} verwerfen (Aktion: {Aktion}).", id, action);

        var workItem = await _db.WorkItems.FindAsync([id], ct)
            ?? throw new InvalidOperationException($"Aufgabe {id} nicht gefunden.");

        if (workItem.Status != WorkItemStatus.New)
        {
            throw new InvalidOperationException("Nur neue Aufgaben können verworfen werden.");
        }

        if (action == DiscardAction.Archive)
        {
            workItem.Status = WorkItemStatus.Archived;
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Aufgabe {AufgabeId} verworfen und archiviert.", id);
            return;
        }

        await DeleteAsync(id, ct);
        _logger.LogInformation("Aufgabe {AufgabeId} verworfen und dauerhaft gelöscht.", id);
    }

    /// <summary>Archiviert eine Aufgabe. Nur für Aufgaben im Status Beendet möglich.</summary>
    public async Task ArchiveAsync(Guid id, CancellationToken ct = default)
    {
        _logger.LogInformation("Aufgabe {AufgabeId} archivieren.", id);

        var workItem = await _db.WorkItems.FindAsync([id], ct)
            ?? throw new InvalidOperationException($"Aufgabe {id} nicht gefunden.");

        if (workItem.Status is not WorkItemStatus.Completed)
        {
            throw new InvalidOperationException("Nur beendete Aufgaben können archiviert werden.");
        }

        workItem.Status = WorkItemStatus.Archived;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Aufgabe {AufgabeId} archiviert.", id);
    }

    /// <summary>Startet eine Aufgabe: Status → Gestartet, Branch und Arbeitsverzeichnis setzen.</summary>
    public async Task StartenAsync(Guid id, string branchName, string localClonePath, string? basisBranchName = null, CancellationToken ct = default)
    {
        _logger.LogInformation("Aufgabe {AufgabeId} starten (Branch: {BranchName}).", id, branchName);

        var gate = RunGates.GetOrAdd(id, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            var workItem = await _db.WorkItems.FindAsync([id], ct)
                ?? throw new InvalidOperationException($"Aufgabe {id} nicht gefunden.");

            if (workItem.Status is WorkItemStatus.Completed or WorkItemStatus.Archived)
            {
                throw new InvalidOperationException("Beendete oder archivierte Aufgaben können nicht gestartet werden.");
            }

            workItem.Status = WorkItemStatus.Started;
            workItem.ExecutionStatus = WorkItemExecutionStatus.Active;
            workItem.GitWorkspace = new GitWorkspace(branchName, localClonePath, basisBranchName);
            workItem.ActiveRunId = null;
            workItem.LastHeartbeatUtc = null;
            workItem.LastCliStartUtc = null;
            workItem.RunStatus = null;

            await _db.SaveChangesAsync(ct);
        }
        finally
        {
            gate.Release();
        }

        _logger.LogInformation("Aufgabe {AufgabeId} gestartet (Status: Gestartet).", id);
    }

    /// <summary>Speichert einen Vorschlagsprompt und optionalen Ausführungszeitpunkt für die Aufgabe.</summary>
    public async Task SavePromptVorschlagAsync(Guid id, string? prompt, DateTimeOffset? executeAfterUtc, CancellationToken ct = default)
    {
        _logger.LogInformation("Aufgabe {AufgabeId}: Vorschlagsprompt speichern.", id);

        var workItem = await _db.WorkItems.FindAsync([id], ct)
            ?? throw new InvalidOperationException($"Aufgabe {id} nicht gefunden.");

        var normalizedPrompt = string.IsNullOrWhiteSpace(prompt)
            ? null
            : prompt.Trim();

        workItem.SuggestedPrompt = normalizedPrompt;
        workItem.SuggestedExecuteAfterUtc = normalizedPrompt is null
            ? null
            : executeAfterUtc;

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Entfernt den gespeicherten Vorschlagsprompt und den geplanten Ausführungszeitpunkt.</summary>
    public async Task ClearPromptVorschlagAsync(Guid id, CancellationToken ct = default)
    {
        _logger.LogInformation("Aufgabe {AufgabeId}: Vorschlagsprompt löschen.", id);

        var workItem = await _db.WorkItems.FindAsync([id], ct)
            ?? throw new InvalidOperationException($"Aufgabe {id} nicht gefunden.");

        workItem.SuggestedPrompt = null;
        workItem.SuggestedExecuteAfterUtc = null;

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Setzt oder leert den Pausen-Endzeitpunkt (<see cref="WorkItem.PausedUntilUtc"/>) einer Aufgabe.
    /// Erlaubte Stati: <see cref="WorkItemStatus.New"/>, <see cref="WorkItemStatus.Started"/> und
    /// <see cref="WorkItemStatus.Waiting"/>. Beim Setzen muss der Zeitpunkt in der Zukunft liegen;
    /// <c>null</c> hebt eine bestehende Pause auf. Schreibt einen
    /// <see cref="LogEntryType.SystemMessage"/>-Eintrag.
    /// </summary>
    /// <param name="workItemId">ID der Aufgabe.</param>
    /// <param name="pausedUntilUtc">UTC-Zeitpunkt, bis zu dem pausiert wird, oder <c>null</c> zum Aufheben.</param>
    /// <param name="ct">Token zum Abbrechen der Operation.</param>
    public async Task SetPauseAsync(Guid workItemId, DateTimeOffset? pausedUntilUtc, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Aufgabe {AufgabeId}: Pause {Aktion} (bis {PausiertBisUtc}).",
            workItemId,
            pausedUntilUtc.HasValue ? "setzen" : "aufheben",
            pausedUntilUtc?.ToString("O") ?? "-");

        var workItem = await _db.WorkItems
            .FirstOrDefaultAsync(a => a.Id == workItemId, ct)
            ?? throw new InvalidOperationException($"Aufgabe {workItemId} nicht gefunden.");

        if (workItem.Status is not (WorkItemStatus.New or WorkItemStatus.Started or WorkItemStatus.Waiting))
        {
            throw new InvalidOperationException(
                $"Aufgabe {workItemId} kann im Status {workItem.Status} nicht pausiert werden.");
        }

        var now = DateTimeOffset.UtcNow;
        if (pausedUntilUtc.HasValue && pausedUntilUtc.Value <= now)
        {
            throw new InvalidOperationException(
                "Der Pausen-Endzeitpunkt muss in der Zukunft liegen.");
        }

        workItem.PausedUntilUtc = pausedUntilUtc;

        _db.LogEntries.Add(new LogEntry
        {
            Id = Guid.NewGuid(),
            WorkItemId = workItemId,
            Type = LogEntryType.SystemMessage,
            Content = pausedUntilUtc.HasValue
                ? $"Aufgabe pausiert bis {pausedUntilUtc.Value:O}."
                : "Pause aufgehoben.",
            Timestamp = now
        });

        await _db.SaveChangesAsync(ct);

        // Pausen-Setzpfad der SCM-Startprüfung: Setzen wie Aufheben verändern die
        // Startbarkeit — die Prüfung bewertet Priorisierung/Freigabe sofort neu statt
        // erst beim nächsten Poll.
        _startCheckTrigger?.RequestStartCheck();
    }

    /// <summary>Schließt eine Aufgabe ab: Status → Beendet, AbschlussDatum setzen, Branch- und Klonpfad-Felder leeren.</summary>
    public async Task AbschliessenAsync(Guid id, CancellationToken ct = default)
    {
        _logger.LogInformation("Aufgabe {AufgabeId} abschließen.", id);

        var gate = RunGates.GetOrAdd(id, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            var workItem = await _db.WorkItems.FindAsync([id], ct)
                ?? throw new InvalidOperationException($"Aufgabe {id} nicht gefunden.");

            workItem.Status = WorkItemStatus.Completed;
            workItem.ExecutionStatus = WorkItemExecutionStatus.Completed;
            workItem.CompletedAt = DateTimeOffset.UtcNow;
            workItem.GitWorkspace = null;
            workItem.ActiveRunId = null;
            workItem.LastHeartbeatUtc = null;
            workItem.LastCliStartUtc = null;
            workItem.RunStatus = null;

            await _db.SaveChangesAsync(ct);
        }
        finally
        {
            gate.Release();
        }

        _logger.LogInformation("Aufgabe {AufgabeId} abgeschlossen (Status: Beendet).", id);
    }

    /// <summary>Setzt den Status einer Aufgabe mit Validierung der erlaubten Übergänge.</summary>
    public async Task SetStatusAsync(Guid id, WorkItemStatus newStatus, CancellationToken ct = default)
    {
        _logger.LogInformation("Aufgabe {AufgabeId}: Status auf {Status} setzen.", id, newStatus);

        var workItem = await _db.WorkItems.FindAsync([id], ct)
            ?? throw new InvalidOperationException($"Aufgabe {id} nicht gefunden.");

        ValidateStatusTransition(workItem.Status, newStatus);

        workItem.Status = newStatus;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Aufgabe {AufgabeId}: Status auf {Status} gesetzt.", id, newStatus);
    }

    /// <summary>Setzt den Status einer Aufgabe generisch (ohne Transitions-Validierung).</summary>
    public async Task StatusSetzenAsync(Guid id, WorkItemStatus status, CancellationToken ct = default)
    {
        _logger.LogInformation("Aufgabe {AufgabeId}: Status auf {Status} setzen.", id, status);

        var workItem = await _db.WorkItems.FindAsync([id], ct)
            ?? throw new InvalidOperationException($"Aufgabe {id} nicht gefunden.");

        workItem.Status = status;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Aufgabe {AufgabeId}: Status auf {Status} gesetzt.", id, status);
    }

    /// <summary>Setzt eine fehlgeschlagene Startvorbereitung vollständig auf den Neuzustand zurück.</summary>
    public async Task StartZuruecksetzenAsync(Guid id, CancellationToken ct = default)
    {
        _logger.LogInformation("Aufgabe {AufgabeId}: Start zurücksetzen.", id);

        var gate = RunGates.GetOrAdd(id, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            var workItem = await _db.WorkItems.FindAsync([id], ct)
                ?? throw new InvalidOperationException($"Aufgabe {id} nicht gefunden.");

            workItem.Status = WorkItemStatus.New;
            workItem.ExecutionStatus = WorkItemExecutionStatus.NotStarted;
            workItem.GitWorkspace = null;
            workItem.ActiveRunId = null;
            workItem.LastHeartbeatUtc = null;
            workItem.LastCliStartUtc = null;
            workItem.RunStatus = null;

            await _db.SaveChangesAsync(ct);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// Markiert eine Aufgabe als aktuell aktiv ausgeführt: setzt <see cref="WorkItem.ActiveRunId"/> auf die
    /// übergebene Lauf-ID, aktualisiert <see cref="WorkItem.LastHeartbeatUtc"/> für den Laufstatus und
    /// speichert <see cref="WorkItem.LastCliStartUtc"/> als Zeitstempel des echten CLI-Prozessstarts.
    /// Wird von <c>CliProcessManager</c> beim Start eines CLI-Prozesses aufgerufen.
    /// </summary>
    /// <param name="id">ID der Aufgabe.</param>
    /// <param name="laufId">Eindeutige ID des aktiven Laufs.</param>
    /// <param name="ct">Token zum Abbrechen der Operation.</param>
    /// <param name="laeuftGuard">
    /// Optionaler Guard, der innerhalb der serialisierten Schreibphase (siehe <c>LaufGates</c>)
    /// ausgewertet wird: liefert er <c>false</c> (z. B. weil der Prozess zwischen dem Ereignis und dem
    /// Eintritt in die Sperre bereits beendet wurde), wird nichts geschrieben — so kann kein
    /// Phantom-Lauf nach einem bereits erfolgten <see cref="EndActiveRunAsync"/> wiederbelebt
    /// werden (Issue 342, TOCTOU).
    /// </param>
    public async Task SetActiveRunAsync(Guid id, string laufId, CancellationToken ct = default, Func<bool>? laeuftGuard = null)
    {
        var gate = RunGates.GetOrAdd(id, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            if (laeuftGuard?.Invoke() == false)
            {
                _logger.LogDebug(
                    "Setzen des aktiven Laufs für Aufgabe {AufgabeId} übersprungen (Prozess nicht mehr aktiv).",
                    id);
                return;
            }

            var workItem = await _db.WorkItems.FindAsync([id], ct)
                ?? throw new InvalidOperationException($"Aufgabe {id} nicht gefunden.");

            var now = DateTimeOffset.UtcNow;
            if (workItem.Status is WorkItemStatus.Completed or WorkItemStatus.Archived)
            {
                throw new InvalidOperationException("Für beendete oder archivierte Aufgaben kann kein aktiver Lauf gesetzt werden.");
            }

            workItem.ExecutionStatus = WorkItemExecutionStatus.Active;
            workItem.ActiveRunId = laufId;
            workItem.LastHeartbeatUtc = now;
            workItem.LastCliStartUtc = now;
            // LaufStatus startet mit Laeuft (Standardwert von PseudoConsoleSession.RuntimeStatus beim Start),
            // damit die Kachel unmittelbar "▶ Läuft" statt eines veralteten Werts eines vorherigen Laufs zeigt.
            workItem.RunStatus = WorkItemRunStatus.Running;
            await _db.SaveChangesAsync(ct);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// Markiert eine Aufgabe als nicht mehr aktiv ausgeführt: entfernt <see cref="WorkItem.ActiveRunId"/>.
    /// Wird von <c>CliProcessManager</c> aufgerufen, sobald der zugehörige CLI-Prozess beendet wurde
    /// (regulär oder mit Fehler), damit der KI-Ausführungsstatus nicht länger "▶ Läuft" anzeigt.
    /// </summary>
    /// <param name="id">ID der Aufgabe.</param>
    /// <param name="ct">Token zum Abbrechen der Operation.</param>
    public async Task EndActiveRunAsync(Guid id, CancellationToken ct = default)
    {
        var gate = RunGates.GetOrAdd(id, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            var workItem = await _db.WorkItems.FindAsync([id], ct)
                ?? throw new InvalidOperationException($"Aufgabe {id} nicht gefunden.");

            workItem.ActiveRunId = null;
            workItem.LastHeartbeatUtc = null;
            workItem.LastCliStartUtc = null;
            // LaufStatus gehört nur zu einem aktiven Lauf (siehe AktivenLaufSetzenAsync) — beim Beenden
            // entfernen, damit kein veralteter Substatus eines beendeten Laufs zurückbleibt.
            workItem.RunStatus = null;
            workItem.ExecutionStatus = WorkItemExecutionStatus.Completed;
            await _db.SaveChangesAsync(ct);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// Stellt die Laufdaten eines aktiven CLI-Laufs idempotent sicher: Fehlt <see cref="WorkItem.ActiveRunId"/>
    /// (z. B. weil der Lauf über einen Wiederverwendungspfad ohne Gestartet-Ereignis zustande kam oder ein
    /// früherer Persistierungsversuch scheiterte), wird der vollständige Lauf-Zustand gesetzt
    /// (<see cref="WorkItem.ExecutionStatus"/> = Aktiv, neue <see cref="WorkItem.ActiveRunId"/>,
    /// <see cref="WorkItem.LastHeartbeatUtc"/>, <see cref="WorkItem.RunStatus"/>); bei bereits vorhandenem Lauf
    /// wird nur der Heartbeat aufgefrischt (und optional der Substatus). Ein bekannter echter
    /// <see cref="WorkItem.LastCliStartUtc"/>-Wert bleibt erhalten (Sortierstabilität der Aufgabenliste).
    /// Aufgaben in terminalem Status (<see cref="WorkItemStatus.Completed"/>/<see cref="WorkItemStatus.Archived"/>)
    /// werden ohne Änderung übersprungen, damit der periodische Abgleich keine Warnungsflut erzeugt.
    /// Wird von <c>CliProcessManager</c> aus den Selbstheilungspfaden (CliLaufAktiv, RuntimeStatusChanged,
    /// Heartbeat-Tick, periodischer Abgleich) aufgerufen.
    /// </summary>
    /// <param name="id">ID der Aufgabe.</param>
    /// <param name="laufStatusProvider">
    /// Optionaler Provider für den Laufzeit-Substatus (Live-Status der Session); wird — ebenso wie
    /// <paramref name="laeuftGuard"/> — erst innerhalb der serialisierten Schreibphase ausgewertet,
    /// damit beim Schreiben der frischeste Session-Stand gilt und nicht ein vor dem Sperreintritt
    /// gelesener, möglicherweise veralteter Wert als letzter Schreiber landet (Issue 342,
    /// Stale-Write). Liefert er <c>null</c> (oder fehlt er), wird bei neu angelegtem Lauf
    /// <see cref="WorkItemRunStatus.Running"/> gesetzt und bei bestehendem Lauf der Substatus nicht
    /// verändert.
    /// </param>
    /// <param name="ct">Token zum Abbrechen der Operation.</param>
    /// <param name="laeuftGuard">
    /// Optionaler Guard, der innerhalb der serialisierten Schreibphase (siehe <c>LaufGates</c>)
    /// ausgewertet wird: liefert er <c>false</c> (z. B. weil der Prozess zwischen dem Aufruf-Check
    /// und dem Eintritt in die Sperre bereits beendet wurde), wird nichts geschrieben — so kann kein
    /// Phantom-Lauf nach einem bereits erfolgten <see cref="EndActiveRunAsync"/> wiederbelebt
    /// werden (Issue 342, TOCTOU). Aufrufer aus den Selbstheilungspfaden übergeben
    /// <c>KiAusfuehrungsService.IsRunning</c>.
    /// </param>
    public async Task EnsureActiveRunAsync(Guid id, Func<WorkItemRunStatus?>? laufStatusProvider = null, CancellationToken ct = default, Func<bool>? laeuftGuard = null)
    {
        var gate = RunGates.GetOrAdd(id, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            if (laeuftGuard?.Invoke() == false)
            {
                _logger.LogDebug(
                    "Laufdaten-Sicherstellung für Aufgabe {AufgabeId} übersprungen (Prozess nicht mehr aktiv).",
                    id);
                return;
            }

            var runStatus = laufStatusProvider?.Invoke();

            var workItem = await _db.WorkItems.FindAsync([id], ct)
                ?? throw new InvalidOperationException($"Aufgabe {id} nicht gefunden.");

            if (workItem.Status is WorkItemStatus.Completed or WorkItemStatus.Archived)
            {
                _logger.LogDebug(
                    "Laufdaten-Sicherstellung für Aufgabe {AufgabeId} übersprungen (terminaler Status {Status}).",
                    id,
                    workItem.Status);
                return;
            }

            var now = DateTimeOffset.UtcNow;
            if (workItem.ActiveRunId is null)
            {
                workItem.ExecutionStatus = WorkItemExecutionStatus.Active;
                workItem.ActiveRunId = Guid.NewGuid().ToString("N");
                workItem.LastHeartbeatUtc = now;
                workItem.LastCliStartUtc ??= now;
                workItem.RunStatus = runStatus ?? WorkItemRunStatus.Running;
            }
            else
            {
                workItem.LastHeartbeatUtc = now;
                if (runStatus is not null)
                {
                    workItem.RunStatus = runStatus;
                }
            }

            await _db.SaveChangesAsync(ct);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>Gibt die Minuten seit dem letzten Heartbeat zurück (null wenn kein Heartbeat gesetzt).</summary>
    public async Task<int?> GetHeartbeatAgeMinutesAsync(Guid id, CancellationToken ct = default)
    {
        var workItem = await _db.WorkItems
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new InvalidOperationException($"Aufgabe {id} nicht gefunden.");

        if (workItem.LastHeartbeatUtc is null)
        {
            return null;
        }

        var ageMinutes = (int)(DateTimeOffset.UtcNow - workItem.LastHeartbeatUtc.Value).TotalMinutes;
        return ageMinutes;
    }

    /// <summary>Gibt alle aktiven Aufgaben (Status Gestartet oder Wartend) zurück, sortiert nach letztem CLI-Start.</summary>
    /// <param name="ct">Token zum Abbrechen der Operation.</param>
    /// <returns>Die aktiven Aufgaben, absteigend nach letztem CLI-Start sortiert (maximal 20).</returns>
    public async Task<List<WorkItem>> GetAktiveAufgabenAsync(CancellationToken ct = default)
    {
        return await _db.WorkItems
            .AsNoTracking()
            .Include(a => a.Project)
            .Include(a => a.GitRepository)
            .Where(IsActiveOrWaitingPredicate)
            .OrderByDescending(a => a.LastCliStartUtc ?? a.CreatedAt)
            .ThenBy(a => a.Title)
            .ThenBy(a => a.Id)
            .Take(20)
            .ToListAsync(ct);
    }

    /// <summary>Fehlermeldungsformat für einen blockierten Aufgabenabschluss wegen offener To-Dos (Platzhalter {0}: Anzahl offener To-Dos).</summary>
    public const string OpenTodosErrorMessageFormat = "Diese Aufgabe kann nicht beendet werden, solange noch {0} offene To-Do(s) vorhanden sind.";

    /// <summary>Validiert, ob eine Aufgabe beendet werden kann: gibt false zurück, wenn offene To-Dos existieren.</summary>
    public async Task<bool> CanCompleteTaskAsync(Guid workItemId, CancellationToken ct = default)
    {
        var offeneTodoCount = await _todoService.GetTodoCountAsync(workItemId, ct);
        return offeneTodoCount == 0;
    }

    private static void ValidateStatusTransition(WorkItemStatus current, WorkItemStatus next)
    {
        if (next == WorkItemStatus.Archived)
        {
            return;
        }

        var allowed = current switch
        {
            WorkItemStatus.New => new[] { WorkItemStatus.Started },
            WorkItemStatus.Started => new[] { WorkItemStatus.Waiting, WorkItemStatus.Completed },
            WorkItemStatus.Waiting => new[] { WorkItemStatus.Started, WorkItemStatus.Completed },
            WorkItemStatus.Completed => Array.Empty<WorkItemStatus>(),
            WorkItemStatus.Archived => Array.Empty<WorkItemStatus>(),
            _ => Array.Empty<WorkItemStatus>()
        };

        if (!allowed.Contains(next))
        {
            throw new InvalidStatusTransitionException(current, next);
        }
    }
}
