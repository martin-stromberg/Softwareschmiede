using Microsoft.Extensions.Logging;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Application.Services;

/// <summary>Orchestriert Git-Operationen für Aufgaben.</summary>
public sealed class GitOrchestrationService
{
    private readonly AufgabeService _aufgabeService;
    private readonly ProjektService _projektService;
    private readonly ProtokollService _protokollService;
    private readonly IGitPlugin _gitPlugin;
    private readonly PluginSelectionService _pluginSelectionService;
    private readonly ILogger<GitOrchestrationService> _logger;

    /// <inheritdoc cref="GitOrchestrationService"/>
    public GitOrchestrationService(
        AufgabeService aufgabeService,
        ProjektService projektService,
        ProtokollService protokollService,
        IGitPlugin gitPlugin,
        PluginSelectionService pluginSelectionService,
        ILogger<GitOrchestrationService> logger)
    {
        _aufgabeService = aufgabeService;
        _projektService = projektService;
        _protokollService = protokollService;
        _gitPlugin = gitPlugin;
        _pluginSelectionService = pluginSelectionService;
        _logger = logger;
    }

    /// <summary>Ruft Issues aus einem Repository ab.</summary>
    public async Task<IEnumerable<Issue>> IssuesAbrufenAsync(string repositoryId, CancellationToken ct = default)
    {
        _logger.LogInformation("Issues für Repository '{RepositoryId}' abrufen.", repositoryId);
        return await _gitPlugin.GetIssuesAsync(repositoryId, ct);
    }

    /// <summary>Führt einen Commit durch und protokolliert die Aktion.</summary>
    public async Task CommitAsync(Guid aufgabeId, string message, CancellationToken ct = default)
    {
        _logger.LogInformation("Commit für Aufgabe {AufgabeId} durchführen.", aufgabeId);

        var aufgabe = await _aufgabeService.GetDetailAsync(aufgabeId, ct)
            ?? throw new InvalidOperationException($"Aufgabe {aufgabeId} nicht gefunden.");

        if (string.IsNullOrEmpty(aufgabe.LokalerKlonPfad))
            throw new InvalidOperationException($"Aufgabe {aufgabeId} hat keinen lokalen Klonpfad.");

        var gitPlugin = await ResolveGitPluginAsync(aufgabe, ct);
        await gitPlugin.CommitAsync(aufgabe.LokalerKlonPfad, message, ct);

        await _protokollService.AddEintragAsync(
            aufgabeId,
            ProtokollTyp.GitAktion,
            $"Commit: {message}",
            ct: ct);

        _logger.LogInformation("Commit für Aufgabe {AufgabeId} durchgeführt.", aufgabeId);
    }

    /// <summary>Setzt Commits zurück und protokolliert die Aktion.</summary>
    public async Task ResetAsync(Guid aufgabeId, string resetType, string? targetRef, CancellationToken ct = default)
    {
        _logger.LogInformation("Reset ({ResetType}) für Aufgabe {AufgabeId} durchführen.", resetType, aufgabeId);

        var aufgabe = await _aufgabeService.GetDetailAsync(aufgabeId, ct)
            ?? throw new InvalidOperationException($"Aufgabe {aufgabeId} nicht gefunden.");

        if (string.IsNullOrEmpty(aufgabe.LokalerKlonPfad))
            throw new InvalidOperationException($"Aufgabe {aufgabeId} hat keinen lokalen Klonpfad.");

        var gitPlugin = await ResolveGitPluginAsync(aufgabe, ct);
        await gitPlugin.ResetAsync(aufgabe.LokalerKlonPfad, resetType, targetRef, ct);

        var ziel = targetRef ?? "HEAD";
        await _protokollService.AddEintragAsync(
            aufgabeId,
            ProtokollTyp.GitAktion,
            $"Reset ({resetType}) auf {ziel}",
            ct: ct);

        _logger.LogInformation("Reset ({ResetType}) für Aufgabe {AufgabeId} durchgeführt.", resetType, aufgabeId);
    }

    /// <summary>Pusht den Branch auf den Remote und protokolliert die Aktion.</summary>
    public async Task PushAsync(Guid aufgabeId, CancellationToken ct = default)
    {
        _logger.LogInformation("Push für Aufgabe {AufgabeId} durchführen.", aufgabeId);

        var aufgabe = await _aufgabeService.GetDetailAsync(aufgabeId, ct)
            ?? throw new InvalidOperationException($"Aufgabe {aufgabeId} nicht gefunden.");

        if (string.IsNullOrEmpty(aufgabe.LokalerKlonPfad))
            throw new InvalidOperationException($"Aufgabe {aufgabeId} hat keinen lokalen Klonpfad.");

        if (string.IsNullOrEmpty(aufgabe.BranchName))
            throw new InvalidOperationException($"Aufgabe {aufgabeId} hat keinen Branch-Namen.");

        var gitPlugin = await ResolveGitPluginAsync(aufgabe, ct);
        await gitPlugin.PushBranchAsync(aufgabe.LokalerKlonPfad, aufgabe.BranchName, ct);

        await _protokollService.AddEintragAsync(
            aufgabeId,
            ProtokollTyp.GitAktion,
            $"Push: Branch '{aufgabe.BranchName}' gepusht.",
            ct: ct);

        _logger.LogInformation("Push für Aufgabe {AufgabeId} durchgeführt.", aufgabeId);
    }

    /// <summary>Holt Änderungen vom Remote und protokolliert die Aktion.</summary>
    public async Task PullAsync(Guid aufgabeId, CancellationToken ct = default)
    {
        _logger.LogInformation("Pull für Aufgabe {AufgabeId} durchführen.", aufgabeId);

        var aufgabe = await _aufgabeService.GetDetailAsync(aufgabeId, ct)
            ?? throw new InvalidOperationException($"Aufgabe {aufgabeId} nicht gefunden.");

        if (string.IsNullOrEmpty(aufgabe.LokalerKlonPfad))
            throw new InvalidOperationException($"Aufgabe {aufgabeId} hat keinen lokalen Klonpfad.");

        var gitPlugin = await ResolveGitPluginAsync(aufgabe, ct);
        await gitPlugin.PullAsync(aufgabe.LokalerKlonPfad, ct);

        var pullLogText = string.Equals(gitPlugin.PluginPrefix, "LocalDirectoryPlugin", StringComparison.Ordinal)
            ? "Pull: Kein Merge durchgeführt. Arbeitsverzeichnis wurde per Dateisynchronisation aktualisiert."
            : "Pull: Änderungen vom Remote geholt.";

        await _protokollService.AddEintragAsync(
            aufgabeId,
            ProtokollTyp.GitAktion,
            pullLogText,
            ct: ct);

        _logger.LogInformation("Pull für Aufgabe {AufgabeId} durchgeführt.", aufgabeId);
    }

    /// <summary>Übernimmt Änderungen vom Arbeitsverzeichnis ins Quellverzeichnis.</summary>
    public async Task MergeToSourceAsync(Guid aufgabeId, CancellationToken ct = default)
    {
        _logger.LogInformation("Merge (Workspace -> Source) für Aufgabe {AufgabeId} durchführen.", aufgabeId);

        var aufgabe = await _aufgabeService.GetDetailAsync(aufgabeId, ct)
            ?? throw new InvalidOperationException($"Aufgabe {aufgabeId} nicht gefunden.");

        if (string.IsNullOrEmpty(aufgabe.LokalerKlonPfad))
            throw new InvalidOperationException($"Aufgabe {aufgabeId} hat keinen lokalen Klonpfad.");

        var gitPlugin = await ResolveGitPluginAsync(aufgabe, ct);
        await gitPlugin.MergeToSourceAsync(aufgabe.LokalerKlonPfad, ct);

        await _protokollService.AddEintragAsync(
            aufgabeId,
            ProtokollTyp.GitAktion,
            "Merge: Änderungen aus dem Arbeitsverzeichnis ins Quellverzeichnis übernommen.",
            ct: ct);

        _logger.LogInformation("Merge (Workspace -> Source) für Aufgabe {AufgabeId} durchgeführt.", aufgabeId);
    }

    /// <summary>Liefert die vom Plugin bereitgestellten Git-Aktions-Capabilities für die Aufgabe.</summary>
    public async Task<GitActionCapabilities> GetGitActionCapabilitiesAsync(Guid aufgabeId, CancellationToken ct = default)
    {
        var aufgabe = await _aufgabeService.GetDetailAsync(aufgabeId, ct)
            ?? throw new InvalidOperationException($"Aufgabe {aufgabeId} nicht gefunden.");

        var gitPlugin = await ResolveGitPluginAsync(aufgabe, ct);
        return await gitPlugin.GetGitActionCapabilitiesAsync(aufgabe.LokalerKlonPfad, ct);
    }

    /// <summary>Erstellt einen Pull Request und protokolliert die Aktion.</summary>
    public async Task<PullRequest> PullRequestErstellenAsync(
        Guid aufgabeId,
        string? title = null,
        string? body = null,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Pull Request für Aufgabe {AufgabeId} erstellen.", aufgabeId);

        var aufgabe = await _aufgabeService.GetDetailAsync(aufgabeId, ct)
            ?? throw new InvalidOperationException($"Aufgabe {aufgabeId} nicht gefunden.");

        if (string.IsNullOrEmpty(aufgabe.BranchName))
            throw new InvalidOperationException($"Aufgabe {aufgabeId} hat keinen Branch-Namen.");

        var gitPlugin = await ResolveGitPluginAsync(aufgabe, ct);
        var repositoryId = await ResolveRepositoryIdAsync(aufgabe, ct);

        var prTitle = title ?? aufgabe.Titel;
        var prBody = PullRequestBodyBuilder.Build(aufgabe, body);
        var issueNummer = aufgabe.IssueReferenz?.IssueNummer;

        if (string.IsNullOrEmpty(aufgabe.LokalerKlonPfad))
            throw new InvalidOperationException($"Aufgabe {aufgabeId} hat keinen lokalen Klonpfad.");

        await gitPlugin.PushBranchAsync(aufgabe.LokalerKlonPfad, aufgabe.BranchName, ct);
        var pullRequest = await gitPlugin.CreatePullRequestAsync(repositoryId, aufgabe.BranchName, prTitle, prBody, ct);

        var issueLogSuffix = issueNummer is > 0
            ? $" (Issue #{issueNummer.Value}, Auto-Close aktiv)"
            : string.Empty;

        await _protokollService.AddEintragAsync(
            aufgabeId,
            ProtokollTyp.GitAktion,
            $"Push: Branch '{aufgabe.BranchName}' vor Pull-Request-Erstellung gepusht.",
            ct: ct);

        await _protokollService.AddEintragAsync(
            aufgabeId,
            ProtokollTyp.GitAktion,
            $"Pull Request erstellt: #{pullRequest.Nummer} – {pullRequest.Titel} ({pullRequest.Url}){issueLogSuffix}",
            ct: ct);

        _logger.LogInformation("Pull Request für Aufgabe {AufgabeId} erstellt.", aufgabeId);

        return pullRequest;
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
    public async Task ValidateWorkingDirectoryAfterCloneAsync(string clonePath, RepositoryStartKonfiguration? startConfig, IGitPlugin? gitPlugin = null)
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
    private async Task<string> ResolveRepositoryIdAsync(Aufgabe aufgabe, CancellationToken ct)
    {
        if (aufgabe.GitRepository is not null)
        {
            return ExtractRepositoryIdFromUrl(aufgabe.GitRepository.RepositoryUrl);
        }

        var projekt = await _projektService.GetDetailAsync(aufgabe.ProjektId, ct)
            ?? throw new InvalidOperationException($"Projekt {aufgabe.ProjektId} nicht gefunden.");

        var aktiveRepositories = projekt.Repositories
            .Where(repository => repository.Aktiv)
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
                $"Aufgabe {aufgabe.Id} kann keinen Pull Request erstellen, weil im Projekt '{projekt.Name}' kein aktives Repository vorhanden ist.");
        }

        throw new InvalidOperationException(
            $"Aufgabe {aufgabe.Id} kann keinen Pull Request erstellen, weil im Projekt '{projekt.Name}' mehrere aktive Repositories vorhanden sind. Verknüpfen Sie die Aufgabe mit genau einem Repository.");
    }

    private async Task<IGitPlugin> ResolveGitPluginAsync(Aufgabe aufgabe, CancellationToken ct)
    {
        var selectedPluginPrefix = await ResolveSelectedPluginPrefixAsync(aufgabe, ct);
        var gitPlugin = await _pluginSelectionService.ResolveSourceCodeManagementPluginAsync(selectedPluginPrefix, ct);

        _logger.LogDebug(
            "Git-Plugin für Aufgabe {AufgabeId} aufgelöst: Selected='{SelectedPluginPrefix}', Effective='{EffectivePluginPrefix}'.",
            aufgabe.Id,
            selectedPluginPrefix ?? "<default>",
            gitPlugin.PluginPrefix);

        return gitPlugin;
    }

    private async Task<string?> ResolveSelectedPluginPrefixAsync(Aufgabe aufgabe, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(aufgabe.GitRepository?.PluginTyp))
        {
            return aufgabe.GitRepository.PluginTyp.Trim();
        }

        var projekt = await _projektService.GetDetailAsync(aufgabe.ProjektId, ct)
            ?? throw new InvalidOperationException($"Projekt {aufgabe.ProjektId} nicht gefunden.");

        var aktiveRepositories = projekt.Repositories
            .Where(repository => repository.Aktiv)
            .OrderBy(repository => repository.RepositoryName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(repository => repository.Id)
            .ToList();

        if (aktiveRepositories.Count == 1 && !string.IsNullOrWhiteSpace(aktiveRepositories[0].PluginTyp))
        {
            return aktiveRepositories[0].PluginTyp.Trim();
        }

        if (aktiveRepositories.Count > 1)
        {
            _logger.LogWarning(
                "Aufgabe {AufgabeId}: Mehrdeutige Plugin-Auflösung ({RepositoryCount} aktive Repositories). Es wird der konfigurierte Standard genutzt.",
                aufgabe.Id,
                aktiveRepositories.Count);
        }

        return null;
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
