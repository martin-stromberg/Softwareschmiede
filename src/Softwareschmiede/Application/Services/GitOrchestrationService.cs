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
    private readonly ILogger<GitOrchestrationService> _logger;

    /// <inheritdoc cref="GitOrchestrationService"/>
    public GitOrchestrationService(
        AufgabeService aufgabeService,
        ProjektService projektService,
        ProtokollService protokollService,
        IGitPlugin gitPlugin,
        ILogger<GitOrchestrationService> logger)
    {
        _aufgabeService = aufgabeService;
        _projektService = projektService;
        _protokollService = protokollService;
        _gitPlugin = gitPlugin;
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

        var aufgabe = await _aufgabeService.GetByIdAsync(aufgabeId, ct)
            ?? throw new InvalidOperationException($"Aufgabe {aufgabeId} nicht gefunden.");

        if (string.IsNullOrEmpty(aufgabe.LokalerKlonPfad))
            throw new InvalidOperationException($"Aufgabe {aufgabeId} hat keinen lokalen Klonpfad.");

        await _gitPlugin.CommitAsync(aufgabe.LokalerKlonPfad, message, ct);

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

        var aufgabe = await _aufgabeService.GetByIdAsync(aufgabeId, ct)
            ?? throw new InvalidOperationException($"Aufgabe {aufgabeId} nicht gefunden.");

        if (string.IsNullOrEmpty(aufgabe.LokalerKlonPfad))
            throw new InvalidOperationException($"Aufgabe {aufgabeId} hat keinen lokalen Klonpfad.");

        await _gitPlugin.ResetAsync(aufgabe.LokalerKlonPfad, resetType, targetRef, ct);

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

        var aufgabe = await _aufgabeService.GetByIdAsync(aufgabeId, ct)
            ?? throw new InvalidOperationException($"Aufgabe {aufgabeId} nicht gefunden.");

        if (string.IsNullOrEmpty(aufgabe.LokalerKlonPfad))
            throw new InvalidOperationException($"Aufgabe {aufgabeId} hat keinen lokalen Klonpfad.");

        if (string.IsNullOrEmpty(aufgabe.BranchName))
            throw new InvalidOperationException($"Aufgabe {aufgabeId} hat keinen Branch-Namen.");

        await _gitPlugin.PushBranchAsync(aufgabe.LokalerKlonPfad, aufgabe.BranchName, ct);

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

        var aufgabe = await _aufgabeService.GetByIdAsync(aufgabeId, ct)
            ?? throw new InvalidOperationException($"Aufgabe {aufgabeId} nicht gefunden.");

        if (string.IsNullOrEmpty(aufgabe.LokalerKlonPfad))
            throw new InvalidOperationException($"Aufgabe {aufgabeId} hat keinen lokalen Klonpfad.");

        await _gitPlugin.PullAsync(aufgabe.LokalerKlonPfad, ct);

        await _protokollService.AddEintragAsync(
            aufgabeId,
            ProtokollTyp.GitAktion,
            "Pull: Änderungen vom Remote geholt.",
            ct: ct);

        _logger.LogInformation("Pull für Aufgabe {AufgabeId} durchgeführt.", aufgabeId);
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

        var repositoryId = await ResolveRepositoryIdAsync(aufgabe, ct);

        var prTitle = title ?? aufgabe.Titel;
        var prBody = body ?? $"Automatisch erstellt für Aufgabe: {aufgabe.Titel}";

        var pullRequest = await _gitPlugin.CreatePullRequestAsync(repositoryId, aufgabe.BranchName, prTitle, prBody, ct);

        await _protokollService.AddEintragAsync(
            aufgabeId,
            ProtokollTyp.GitAktion,
            $"Pull Request erstellt: #{pullRequest.Nummer} – {pullRequest.Titel} ({pullRequest.Url})",
            ct: ct);

        _logger.LogInformation("Pull Request für Aufgabe {AufgabeId} erstellt.", aufgabeId);

        return pullRequest;
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

    /// <summary>Extrahiert die Repository-ID (owner/repo) aus einer Repository-URL.</summary>
    private static string ExtractRepositoryIdFromUrl(string repositoryUrl)
    {
        // .git-Suffix entfernen, falls vorhanden
        var url = repositoryUrl.EndsWith(".git", StringComparison.OrdinalIgnoreCase)
            ? repositoryUrl[..^4]
            : repositoryUrl;

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
