using Microsoft.Extensions.Logging;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Application.Services;

/// <summary>Orchestriert Git-Operationen für Aufgaben.</summary>
public sealed class GitOrchestrationService
{
    private readonly WorkItemService _workItemService;
    private readonly ProjectService _projektService;
    private readonly LogService _logService;
    private readonly IGitPlugin _gitPlugin;
    private readonly PluginSelectionService _pluginSelectionService;
    private readonly ILogger<GitOrchestrationService> _logger;
    private readonly IGitWorkspaceBrowserService? _gitWorkspaceBrowserService;
    private readonly PullRequestReferenceService? _pullRequestReferenceService;

    /// <inheritdoc cref="GitOrchestrationService"/>
    public GitOrchestrationService(
        WorkItemService workItemService,
        ProjectService projektService,
        LogService logService,
        IGitPlugin gitPlugin,
        PluginSelectionService pluginSelectionService,
        ILogger<GitOrchestrationService> logger,
        IGitWorkspaceBrowserService? gitWorkspaceBrowserService = null,
        PullRequestReferenceService? pullRequestReferenceService = null)
    {
        _workItemService = workItemService;
        _projektService = projektService;
        _logService = logService;
        _gitPlugin = gitPlugin;
        _pluginSelectionService = pluginSelectionService;
        _logger = logger;
        _gitWorkspaceBrowserService = gitWorkspaceBrowserService;
        _pullRequestReferenceService = pullRequestReferenceService;
    }

    /// <summary>Ruft Issues aus einem Repository ab.</summary>
    public async Task<IEnumerable<Issue>> FetchIssuesAsync(string repositoryId, CancellationToken ct = default)
    {
        _logger.LogInformation("Issues für Repository '{RepositoryId}' abrufen.", repositoryId);
        return await _gitPlugin.GetIssuesAsync(repositoryId, ct);
    }

    /// <summary>Führt einen Commit durch und protokolliert die Aktion.</summary>
    public async Task CommitAsync(Guid workItemId, string message, CancellationToken ct = default)
    {
        _logger.LogInformation("Commit für Aufgabe {AufgabeId} durchführen.", workItemId);

        var workItem = await _workItemService.GetDetailAsync(workItemId, ct)
            ?? throw new InvalidOperationException($"Aufgabe {workItemId} nicht gefunden.");

        if (string.IsNullOrEmpty(workItem.LocalClonePath))
            throw new InvalidOperationException($"Aufgabe {workItemId} hat keinen lokalen Klonpfad.");

        var gitPlugin = await ResolveGitPluginAsync(workItem, ct);
        await gitPlugin.CommitAsync(workItem.LocalClonePath, message, ct);

        await _logService.AddEntryAsync(
            workItemId,
            LogEntryType.GitAction,
            $"Commit: {message}",
            ct: ct);

        _logger.LogInformation("Commit für Aufgabe {AufgabeId} durchgeführt.", workItemId);
    }

    /// <summary>Setzt Commits zurück und protokolliert die Aktion.</summary>
    public async Task ResetAsync(Guid workItemId, string resetType, string? targetRef, CancellationToken ct = default)
    {
        _logger.LogInformation("Reset ({ResetType}) für Aufgabe {AufgabeId} durchführen.", resetType, workItemId);

        var workItem = await _workItemService.GetDetailAsync(workItemId, ct)
            ?? throw new InvalidOperationException($"Aufgabe {workItemId} nicht gefunden.");

        if (string.IsNullOrEmpty(workItem.LocalClonePath))
            throw new InvalidOperationException($"Aufgabe {workItemId} hat keinen lokalen Klonpfad.");

        var gitPlugin = await ResolveGitPluginAsync(workItem, ct);
        await gitPlugin.ResetAsync(workItem.LocalClonePath, resetType, targetRef, ct);

        var target = targetRef ?? "HEAD";
        await _logService.AddEntryAsync(
            workItemId,
            LogEntryType.GitAction,
            $"Reset ({resetType}) auf {target}",
            ct: ct);

        _logger.LogInformation("Reset ({ResetType}) für Aufgabe {AufgabeId} durchgeführt.", resetType, workItemId);
    }

    /// <summary>Pusht den Branch auf den Remote und protokolliert die Aktion.</summary>
    public async Task PushAsync(Guid workItemId, CancellationToken ct = default)
    {
        _logger.LogInformation("Push für Aufgabe {AufgabeId} durchführen.", workItemId);

        var workItem = await _workItemService.GetDetailAsync(workItemId, ct)
            ?? throw new InvalidOperationException($"Aufgabe {workItemId} nicht gefunden.");

        if (string.IsNullOrEmpty(workItem.LocalClonePath))
            throw new InvalidOperationException($"Aufgabe {workItemId} hat keinen lokalen Klonpfad.");

        if (string.IsNullOrEmpty(workItem.BranchName))
            throw new InvalidOperationException($"Aufgabe {workItemId} hat keinen Branch-Namen.");

        var gitPlugin = await ResolveGitPluginAsync(workItem, ct);
        await gitPlugin.PushBranchAsync(workItem.LocalClonePath, workItem.BranchName, ct);

        await _logService.AddEntryAsync(
            workItemId,
            LogEntryType.GitAction,
            $"Push: Branch '{workItem.BranchName}' gepusht.",
            ct: ct);

        _logger.LogInformation("Push für Aufgabe {AufgabeId} durchgeführt.", workItemId);
    }

    /// <summary>Holt Änderungen vom Remote und protokolliert die Aktion.</summary>
    public async Task PullAsync(Guid workItemId, CancellationToken ct = default)
    {
        _logger.LogInformation("Pull für Aufgabe {AufgabeId} durchführen.", workItemId);

        var workItem = await _workItemService.GetDetailAsync(workItemId, ct)
            ?? throw new InvalidOperationException($"Aufgabe {workItemId} nicht gefunden.");

        if (string.IsNullOrEmpty(workItem.LocalClonePath))
            throw new InvalidOperationException($"Aufgabe {workItemId} hat keinen lokalen Klonpfad.");

        var gitPlugin = await ResolveGitPluginAsync(workItem, ct);
        await gitPlugin.PullAsync(workItem.LocalClonePath, ct);

        var pullLogText = string.Equals(gitPlugin.PluginPrefix, "LocalDirectoryPlugin", StringComparison.Ordinal)
            ? "Pull: Kein Merge durchgeführt. Arbeitsverzeichnis wurde per Dateisynchronisation aktualisiert."
            : "Pull: Änderungen vom Remote geholt.";

        await _logService.AddEntryAsync(
            workItemId,
            LogEntryType.GitAction,
            pullLogText,
            ct: ct);

        _logger.LogInformation("Pull für Aufgabe {AufgabeId} durchgeführt.", workItemId);
    }

    /// <summary>Übernimmt Änderungen vom Arbeitsverzeichnis ins Quellverzeichnis.</summary>
    public async Task MergeToSourceAsync(Guid workItemId, CancellationToken ct = default)
    {
        _logger.LogInformation("Merge (Workspace -> Source) für Aufgabe {AufgabeId} durchführen.", workItemId);

        var workItem = await _workItemService.GetDetailAsync(workItemId, ct)
            ?? throw new InvalidOperationException($"Aufgabe {workItemId} nicht gefunden.");

        if (string.IsNullOrEmpty(workItem.LocalClonePath))
            throw new InvalidOperationException($"Aufgabe {workItemId} hat keinen lokalen Klonpfad.");

        var gitPlugin = await ResolveGitPluginAsync(workItem, ct);
        await gitPlugin.MergeToSourceAsync(workItem.LocalClonePath, ct);

        await _logService.AddEntryAsync(
            workItemId,
            LogEntryType.GitAction,
            "Merge: Änderungen aus dem Arbeitsverzeichnis ins Quellverzeichnis übernommen.",
            ct: ct);

        _logger.LogInformation("Merge (Workspace -> Source) für Aufgabe {AufgabeId} durchgeführt.", workItemId);
    }

    /// <summary>Liefert die vom Plugin bereitgestellten Git-Aktions-Capabilities für die Aufgabe.</summary>
    public async Task<GitActionCapabilities> GetGitActionCapabilitiesAsync(Guid workItemId, CancellationToken ct = default)
    {
        var workItem = await _workItemService.GetDetailAsync(workItemId, ct)
            ?? throw new InvalidOperationException($"Aufgabe {workItemId} nicht gefunden.");

        var gitPlugin = await ResolveGitPluginAsync(workItem, ct);
        return await gitPlugin.GetGitActionCapabilitiesAsync(workItem.LocalClonePath, ct);
    }

    /// <summary>Erstellt einen Pull Request und protokolliert die Aktion.</summary>
    public async Task<PullRequest> CreatePullRequestAsync(
        Guid workItemId,
        string? title = null,
        string? body = null,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Pull Request für Aufgabe {AufgabeId} erstellen.", workItemId);

        var workItem = await _workItemService.GetDetailAsync(workItemId, ct)
            ?? throw new InvalidOperationException($"Aufgabe {workItemId} nicht gefunden.");

        if (string.IsNullOrEmpty(workItem.BranchName))
            throw new InvalidOperationException($"Aufgabe {workItemId} hat keinen Branch-Namen.");

        var gitPlugin = await ResolveGitPluginAsync(workItem, ct);
        var repositoryId = await ResolveRepositoryIdAsync(workItem, ct);

        var prTitle = title ?? workItem.Title;
        var issueNumber = workItem.IssueReference?.IssueNumber;

        if (string.IsNullOrEmpty(workItem.LocalClonePath))
            throw new InvalidOperationException($"Aufgabe {workItemId} hat keinen lokalen Klonpfad.");

        var baseBranch = await ResolveDefaultSourceBranchNameAsync(workItem, ct);
        var prBody = await BuildPullRequestBodyAsync(workItem, body, baseBranch, ct);

        await gitPlugin.PushBranchAsync(workItem.LocalClonePath, workItem.BranchName, ct);
        var pullRequest = await gitPlugin.CreatePullRequestAsync(repositoryId, workItem.BranchName, baseBranch, prTitle, prBody, ct);

        if (_pullRequestReferenceService is not null)
        {
            await _pullRequestReferenceService.SaveCreatedAsync(workItemId, repositoryId, pullRequest, ct);
        }

        var issueLogSuffix = issueNumber is > 0
            ? $" (Issue #{issueNumber.Value}, Auto-Close aktiv)"
            : string.Empty;

        await _logService.AddEntryAsync(
            workItemId,
            LogEntryType.GitAction,
            $"Push: Branch '{workItem.BranchName}' vor Pull-Request-Erstellung gepusht.",
            ct: ct);

        await _logService.AddEntryAsync(
            workItemId,
            LogEntryType.GitAction,
            $"Pull Request erstellt: #{pullRequest.Number} – {pullRequest.Title} ({pullRequest.Url}){issueLogSuffix}",
            ct: ct);

        _logger.LogInformation("Pull Request für Aufgabe {AufgabeId} erstellt.", workItemId);

        return pullRequest;
    }

    private async Task<string> BuildPullRequestBodyAsync(WorkItem workItem, string? fallbackBody, string? baseBranch, CancellationToken ct)
    {
        if (_gitWorkspaceBrowserService is null || string.IsNullOrWhiteSpace(workItem.LocalClonePath))
        {
            return PullRequestBodyBuilder.Build(workItem, fallbackBody);
        }

        var sourceBranch = !string.IsNullOrWhiteSpace(workItem.BaseBranchName)
            ? workItem.BaseBranchName
            : baseBranch;

        var snapshot = await _gitWorkspaceBrowserService.LoadSnapshotAsync(workItem.LocalClonePath, sourceBranch, ct);
        if (snapshot.HasError)
        {
            _logger.LogWarning(
                "Commit-Liste für Pull Request von Aufgabe {AufgabeId} konnte nicht geladen werden: {Error}",
                workItem.Id,
                snapshot.ErrorMessage);
            return PullRequestBodyBuilder.Build(workItem, fallbackBody);
        }

        return PullRequestBodyBuilder.BuildFromCommits(workItem, snapshot.BranchCommits);
    }

    /// <summary>
    /// Validiert nach einem erfolgreichen Git-Klon, dass das in <paramref name="startConfig"/> konfigurierte
    /// Arbeitsverzeichnis innerhalb des geklonten Repositories existiert.
    /// </summary>
    /// <param name="clonePath">Pfad zum geklonten Repository (Repository-Root).</param>
    /// <param name="startConfig">Optionale Startkonfiguration des Repositories (z. B. Arbeitsverzeichnis).</param>
    /// <param name="gitPlugin">
    /// Optionales Git-Plugin, das zum Klonen des Repositories verwendet wurde (für die Auflösung des
    /// tatsächlichen Repository-Pfads, z. B. bei <c>LocalDirectoryPlugin</c> im <c>InSourceDirectory</c>-Modus,
    /// wo <paramref name="clonePath"/> nur eine Pointer-Datei enthält). Bleibt <paramref name="gitPlugin"/>
    /// <c>null</c>, verhält sich die Methode wie zuvor und verwendet <paramref name="clonePath"/> unverändert.
    /// </param>
    public async Task ValidateWorkingDirectoryAfterCloneAsync(string clonePath, RepositoryStartConfiguration? startConfig, IGitPlugin? gitPlugin = null)
    {
        if (startConfig?.WorkingDirectoryRelativePath is null)
        {
            return;
        }

        try
        {
            await WorkingDirectoryResolver.DetermineEffectiveWorkingDirectoryAsync(clonePath, startConfig, gitPlugin, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is InvalidOperationException or DirectoryNotFoundException)
        {
            _logger.LogError(
                ex,
                "Arbeitsverzeichnis '{WorkingDirectoryRelativePath}' konnte nach dem Klon von '{ClonePath}' nicht validiert werden.",
                startConfig.WorkingDirectoryRelativePath,
                clonePath);
            throw;
        }
    }

    /// <summary>Ermittelt die Repository-ID aus der Aufgabe oder dem zugehörigen Projekt.</summary>
    private async Task<string> ResolveRepositoryIdAsync(WorkItem workItem, CancellationToken ct)
    {
        if (workItem.GitRepository is not null)
        {
            return ExtractRepositoryIdFromUrl(workItem.GitRepository.RepositoryUrl);
        }

        var project = await _projektService.GetDetailAsync(workItem.ProjectId, ct)
            ?? throw new InvalidOperationException($"Projekt {workItem.ProjectId} nicht gefunden.");

        var aktiveRepositories = project.Repositories
            .Where(repository => repository.Active)
            .OrderBy(repository => repository.RepositoryName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(repository => repository.Id)
            .ToList();

        if (aktiveRepositories.Count == 1)
        {
            return ExtractRepositoryIdFromUrl(aktiveRepositories[0].RepositoryUrl);
        }

        if (aktiveRepositories.Count == 0)
        {
            throw new InvalidOperationException(
                $"Aufgabe {workItem.Id} kann keinen Pull Request erstellen, weil im Projekt '{project.Name}' kein aktives Repository vorhanden ist.");
        }

        throw new InvalidOperationException(
            $"Aufgabe {workItem.Id} kann keinen Pull Request erstellen, weil im Projekt '{project.Name}' mehrere aktive Repositories vorhanden sind. Verknüpfen Sie die Aufgabe mit genau einem Repository.");
    }

    private async Task<IGitPlugin> ResolveGitPluginAsync(WorkItem workItem, CancellationToken ct)
    {
        var selectedPluginPrefix = await ResolveSelectedPluginPrefixAsync(workItem, ct);
        var gitPlugin = await _pluginSelectionService.ResolveSourceCodeManagementPluginAsync(selectedPluginPrefix, ct);

        _logger.LogDebug(
            "Git-Plugin für Aufgabe {AufgabeId} aufgelöst: Selected='{SelectedPluginPrefix}', Effective='{EffectivePluginPrefix}'.",
            workItem.Id,
            selectedPluginPrefix ?? "<default>",
            gitPlugin.PluginPrefix);

        return gitPlugin;
    }

    private async Task<string?> ResolveSelectedPluginPrefixAsync(WorkItem workItem, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(workItem.GitRepository?.PluginType))
        {
            return workItem.GitRepository.PluginType.Trim();
        }

        var project = await _projektService.GetDetailAsync(workItem.ProjectId, ct)
            ?? throw new InvalidOperationException($"Projekt {workItem.ProjectId} nicht gefunden.");

        var aktiveRepositories = project.Repositories
            .Where(repository => repository.Active)
            .OrderBy(repository => repository.RepositoryName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(repository => repository.Id)
            .ToList();

        if (aktiveRepositories.Count == 1 && !string.IsNullOrWhiteSpace(aktiveRepositories[0].PluginType))
        {
            return aktiveRepositories[0].PluginType.Trim();
        }

        if (aktiveRepositories.Count > 1)
        {
            _logger.LogWarning(
                "Aufgabe {AufgabeId}: Mehrdeutige Plugin-Auflösung ({RepositoryCount} aktive Repositories). Es wird der konfigurierte Standard genutzt.",
                workItem.Id,
                aktiveRepositories.Count);
        }

        return null;
    }

    /// <summary>Ermittelt den konfigurierten Basis-Branch (<see cref="GitRepository.DefaultSourceBranchName"/>) aus der Aufgabe oder dem zugehörigen Projekt.</summary>
    private async Task<string?> ResolveDefaultSourceBranchNameAsync(WorkItem workItem, CancellationToken ct)
    {
        if (workItem.GitRepository is not null)
        {
            return workItem.GitRepository.DefaultSourceBranchName;
        }

        var project = await _projektService.GetDetailAsync(workItem.ProjectId, ct)
            ?? throw new InvalidOperationException($"Projekt {workItem.ProjectId} nicht gefunden.");

        var aktiveRepositories = project.Repositories
            .Where(repository => repository.Active)
            .OrderBy(repository => repository.RepositoryName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(repository => repository.Id)
            .ToList();

        if (aktiveRepositories.Count > 1)
        {
            _logger.LogWarning(
                "Aufgabe {AufgabeId}: Mehrdeutige Basis-Branch-Auflösung ({RepositoryCount} aktive Repositories). Es wird kein Basis-Branch aufgelöst.",
                workItem.Id,
                aktiveRepositories.Count);
        }

        return aktiveRepositories.Count == 1 ? aktiveRepositories[0].DefaultSourceBranchName : null;
    }

    /// <summary>Extrahiert die Repository-ID (owner/repo) aus einer Repository-URL.</summary>
    private static string ExtractRepositoryIdFromUrl(string repositoryUrl)
    {
        // .git-Suffix entfernen, falls vorhanden
        var url = repositoryUrl.EndsWith(".git", StringComparison.OrdinalIgnoreCase)
            ? repositoryUrl[..^4]
            : repositoryUrl;

        // SCP-/SSH-Format behandeln:
        // git@github.com:owner/repo
        if (!url.Contains("://", StringComparison.Ordinal))
        {
            var colonIndex = url.IndexOf(':');
            if (colonIndex >= 0 && colonIndex < url.Length - 1)
            {
                var repositoryPath = url[(colonIndex + 1)..];
                var slashIndex = repositoryPath.IndexOf('/');
                if (slashIndex > 0 && slashIndex < repositoryPath.Length - 1)
                {
                    var sshOwner = repositoryPath[..slashIndex];
                    var sshRepo = repositoryPath[(slashIndex + 1)..];
                    return $"{sshOwner}/{sshRepo}";
                }
            }
        }

        // Owner/Repo aus verschiedenen URL-Formaten extrahieren:
        // https://github.com/owner/repo
        // https://github.com/owner/repo.git
        // git@github.com:owner/repo.git
        // git@github.com:owner/repo

        var lastSlash = url.LastIndexOf('/');
        if (lastSlash == -1)
            throw new InvalidOperationException($"Ungültige Repository-URL: {repositoryUrl}");

        var repo = url[(lastSlash + 1)..];

        var secondLastSlash = url.LastIndexOf('/', lastSlash - 1);
        if (secondLastSlash == -1)
            throw new InvalidOperationException($"Ungültige Repository-URL: {repositoryUrl}");

        var owner = url[(secondLastSlash + 1)..lastSlash];

        return $"{owner}/{repo}";
    }
}
