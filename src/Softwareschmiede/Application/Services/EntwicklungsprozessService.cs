using Microsoft.Extensions.Logging;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Exceptions;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Infrastructure.Data;

namespace Softwareschmiede.Application.Services;

/// <summary>Optionale Abhängigkeiten für den EntwicklungsprozessService.</summary>
/// <param name="ProjektService">Optionaler Dienst zum Auflösen von Projekt-Repositories.</param>
/// <param name="RepositoryStartskriptService">Optionaler Dienst zum Ausführen von Repository-Startskripten.</param>
/// <param name="RepositoryInitialisierungService">Optionaler Dienst zum Ausführen von Repository-Initialisierungsskripten.</param>
/// <param name="KiAusfuehrungsService">Optionaler Dienst zum Starten der KI-CLI.</param>
/// <param name="GitOrchestrationService">
/// Optionaler Dienst zur Validierung des konfigurierten Arbeitsverzeichnisses direkt nach dem Git-Klon.
/// </param>
/// <returns>Eine neue Instanz mit den angegebenen optionalen Abhängigkeiten.</returns>
public sealed record EntwicklungsprozessServiceOptions(
    ProjektService? ProjektService = null,
    RepositoryStartskriptService? RepositoryStartskriptService = null,
    RepositoryInitialisierungService? RepositoryInitialisierungService = null,
    KiAusfuehrungsService? KiAusfuehrungsService = null,
    GitOrchestrationService? GitOrchestrationService = null);

/// <summary>
/// Koordiniert Git-Repository-Setup für Aufgaben und Rate-Limit-Marker-Erkennung.
/// </summary>
public sealed class EntwicklungsprozessService
{
    private readonly AufgabeService _aufgabeService;
    private readonly ProtokollService _protokollService;
    private readonly IGitPlugin _gitPlugin;
    private readonly PluginSelectionService _pluginSelectionService;
    private readonly IArbeitsverzeichnisResolver _arbeitsverzeichnisResolver;
    private readonly EntwicklungsprozessServiceOptions _options;
    private readonly ILogger<EntwicklungsprozessService> _logger;

    internal const string KlonBasisVerzeichnis = "softwareschmiede";

    /// <inheritdoc cref="EntwicklungsprozessService"/>
    public EntwicklungsprozessService(
        AufgabeService aufgabeService,
        ProtokollService protokollService,
        IGitPlugin gitPlugin,
        PluginSelectionService pluginSelectionService,
        IArbeitsverzeichnisResolver arbeitsverzeichnisResolver,
        ILogger<EntwicklungsprozessService> logger)
        : this(
            aufgabeService,
            protokollService,
            gitPlugin,
            pluginSelectionService,
            arbeitsverzeichnisResolver,
            new EntwicklungsprozessServiceOptions(),
            logger)
    {
    }

    /// <inheritdoc cref="EntwicklungsprozessService"/>
    public EntwicklungsprozessService(
        AufgabeService aufgabeService,
        ProtokollService protokollService,
        IGitPlugin gitPlugin,
        PluginSelectionService pluginSelectionService,
        IArbeitsverzeichnisResolver arbeitsverzeichnisResolver,
        EntwicklungsprozessServiceOptions options,
        ILogger<EntwicklungsprozessService> logger)
    {
        _aufgabeService = aufgabeService;
        _protokollService = protokollService;
        _gitPlugin = gitPlugin;
        _pluginSelectionService = pluginSelectionService;
        _arbeitsverzeichnisResolver = arbeitsverzeichnisResolver;
        _options = options;
        _logger = logger;
    }

    /// <summary>
    /// Richtet das Git-Repository für eine Aufgabe ein: Klon, Branch, optionales Startskript.
    /// Setzt den Status auf <see cref="AufgabeStatus.Gestartet"/>.
    /// </summary>
    /// <param name="aufgabeId">ID der zu startenden Aufgabe.</param>
    /// <param name="repositoryUrl">URL des zu klonenden Repositories.</param>
    /// <param name="basisBranchName">Optionaler Basis-Branch; wird ein neuer Task-Branch angelegt, wenn er dem Default-Branch entspricht.</param>
    /// <param name="selectedScmPluginPrefix">Optionaler Prefix des zu verwendenden SCM-Plugins.</param>
    /// <param name="ct">Abbruch-Token.</param>
    public async Task ProzessStartenAsync(
        Guid aufgabeId,
        string repositoryUrl,
        string? basisBranchName = null,
        string? selectedScmPluginPrefix = null,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Repository-Setup für Aufgabe {AufgabeId} starten.", aufgabeId);

        var aufgabe = await _aufgabeService.GetDetailAsync(aufgabeId, ct)
            ?? throw new InvalidOperationException($"Aufgabe {aufgabeId} nicht gefunden.");
        await ProzessStartenCoreAsync(aufgabeId, aufgabe, repositoryUrl, basisBranchName, selectedScmPluginPrefix, ct);

        _logger.LogInformation("Repository-Setup für Aufgabe {AufgabeId} abgeschlossen.", aufgabeId);
    }

    /// <summary>
    /// Kombiniert Repository-Setup (Klon, Branch) und CLI-Start in einem Schritt.
    /// Setzt den Status direkt auf <see cref="AufgabeStatus.Gestartet"/> und startet anschließend die CLI mit dem gewählten Plugin.
    /// Im Fehlerfall wird der Status zurückgesetzt und das Klon-Verzeichnis gelöscht.
    /// </summary>
    /// <param name="aufgabeId">ID der zu startenden Aufgabe.</param>
    /// <param name="repositoryUrl">URL des zu klonenden Repositories.</param>
    /// <param name="basisBranchName">Optionaler Basis-Branch.</param>
    /// <param name="kiPluginPrefix">Optionaler Prefix des zu verwendenden KI-Plugins.</param>
    /// <param name="ct">Abbruch-Token.</param>
    public async Task ProzessStartenUndCliStartenAsync(
        Guid aufgabeId,
        string repositoryUrl,
        string? basisBranchName,
        string? kiPluginPrefix,
        CancellationToken ct = default)
    {
        if (_options.KiAusfuehrungsService is null)
        {
            throw new InvalidOperationException("KiAusfuehrungsService ist nicht konfiguriert.");
        }

        try
        {
            var aufgabe = await _aufgabeService.GetDetailAsync(aufgabeId, ct)
                ?? throw new InvalidOperationException($"Aufgabe {aufgabeId} nicht gefunden.");
            var (repository, gitPlugin) = await ProzessStartenCoreAsync(aufgabeId, aufgabe, repositoryUrl, basisBranchName, null, ct);

            aufgabe = await _aufgabeService.GetByIdAsync(aufgabeId, ct)
                ?? throw new InvalidOperationException($"Aufgabe {aufgabeId} nicht gefunden.");

            if (string.IsNullOrEmpty(aufgabe.LokalerKlonPfad))
            {
                throw new InvalidOperationException($"Aufgabe {aufgabeId} hat keinen lokalen Klonpfad.");
            }

            var kiPlugin = await _pluginSelectionService.ResolveDevelopmentAutomationPluginAsync(kiPluginPrefix, ct);
            await _aufgabeService.UpdateAsync(
                aufgabeId,
                aufgabe.Titel,
                aufgabe.AnforderungsBeschreibung,
                kiPlugin.PluginPrefix,
                ct);

            await _options.KiAusfuehrungsService.StartWithPseudoConsoleAsync(
                aufgabeId, kiPlugin, aufgabe.LokalerKlonPfad, null, ct, repository.StartKonfiguration, gitPlugin);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("CLI-Start für Aufgabe {AufgabeId} abgebrochen, Rollback wird durchgeführt.", aufgabeId);
            await RollbackStartAsync(aufgabeId, CancellationToken.None);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CLI-Start für Aufgabe {AufgabeId} fehlgeschlagen, Rollback wird durchgeführt.", aufgabeId);
            await RollbackStartAsync(aufgabeId, CancellationToken.None);
            throw;
        }
    }

    private async Task<(GitRepository Repository, IGitPlugin GitPlugin)> ProzessStartenCoreAsync(
        Guid aufgabeId,
        Aufgabe aufgabe,
        string repositoryUrl,
        string? basisBranchName,
        string? selectedScmPluginPrefix,
        CancellationToken ct)
    {
        var repository = await ResolveRepositoryAsync(aufgabe, repositoryUrl, ct);
        var gitPlugin = await ResolvePluginAsync(repository, selectedScmPluginPrefix, aufgabeId, ct);

        await ValidateBaseBranchExistsAsync(repository, gitPlugin, ct);

        var lokalerKlonPfad = await PrepareCloneDirectoryAsync(gitPlugin, repository.RepositoryUrl, aufgabeId, ct);

        if (_options.GitOrchestrationService is not null)
        {
            await _options.GitOrchestrationService.ValidateWorkingDirectoryAfterCloneAsync(lokalerKlonPfad, repository.StartKonfiguration, gitPlugin);
        }

        var (branchName, nutzeExistierendenBranch, basisBranch) = await SetupBranchAsync(gitPlugin, repository.RepositoryUrl, lokalerKlonPfad, basisBranchName, repository.DefaultSourceBranchName, aufgabe, ct);
        await FinalizeStartAsync(aufgabeId, aufgabe, repository, lokalerKlonPfad, branchName, nutzeExistierendenBranch, basisBranch, ct);

        return (repository, gitPlugin);
    }

    /// <summary>Startet die KI-CLI erneut im bereits vorbereiteten Klon einer Aufgabe.</summary>
    public async Task CliNeustartenAsync(
        Guid aufgabeId,
        string? kiPluginPrefix,
        string? optionalParameters,
        CancellationToken ct = default)
    {
        if (_options.KiAusfuehrungsService is null)
        {
            throw new InvalidOperationException("KiAusfuehrungsService ist nicht konfiguriert.");
        }

        var aufgabe = await _aufgabeService.GetDetailAsync(aufgabeId, ct)
            ?? throw new InvalidOperationException($"Aufgabe {aufgabeId} nicht gefunden.");

        if (aufgabe.Status is AufgabeStatus.Beendet or AufgabeStatus.Archiviert)
        {
            throw new InvalidOperationException("Beendete oder archivierte Aufgaben können nicht gestartet werden.");
        }

        if (string.IsNullOrWhiteSpace(aufgabe.LokalerKlonPfad))
        {
            throw new InvalidOperationException($"Aufgabe {aufgabeId} hat keinen lokalen Klonpfad.");
        }

        var kiPlugin = await _pluginSelectionService.ResolveDevelopmentAutomationPluginAsync(kiPluginPrefix, ct);
        await _aufgabeService.UpdateAsync(
            aufgabeId,
            aufgabe.Titel,
            aufgabe.AnforderungsBeschreibung,
            kiPlugin.PluginPrefix,
            ct);

        var repository = await ResolveRepositoryAsync(aufgabe, aufgabe.GitRepository?.RepositoryUrl ?? string.Empty, ct);
        var gitPlugin = await ResolvePluginAsync(repository, null, aufgabeId, ct);

        await _options.KiAusfuehrungsService.StartWithPseudoConsoleAsync(
            aufgabeId,
            kiPlugin,
            aufgabe.LokalerKlonPfad,
            optionalParameters,
            ct,
            repository.StartKonfiguration,
            gitPlugin);
    }

    /// <summary>Führt einen manuellen Commit durch.</summary>
    /// <param name="aufgabeId">ID der Aufgabe.</param>
    /// <param name="message">Commit-Nachricht.</param>
    /// <param name="ct">Abbruch-Token.</param>
    public async Task CommitDurchfuehrenAsync(Guid aufgabeId, string message, CancellationToken ct = default)
    {
        _logger.LogInformation("Commit für Aufgabe {AufgabeId} durchführen.", aufgabeId);

        var aufgabe = await GetAufgabeMitKlonPfadAsync(aufgabeId, ct);

        await _gitPlugin.CommitAsync(aufgabe.LokalerKlonPfad!, message, ct);

        await _protokollService.AddEintragAsync(
            aufgabeId,
            ProtokollTyp.GitAktion,
            $"Commit: {message}",
            ct: ct);

        _logger.LogInformation("Commit für Aufgabe {AufgabeId} durchgeführt.", aufgabeId);
    }

    /// <summary>Setzt Commits zurück.</summary>
    /// <param name="aufgabeId">ID der Aufgabe.</param>
    /// <param name="resetType">Reset-Typ (z. B. soft, mixed, hard).</param>
    /// <param name="targetRef">Optionaler Ziel-Ref; Standard ist HEAD.</param>
    /// <param name="ct">Abbruch-Token.</param>
    public async Task ResetDurchfuehrenAsync(Guid aufgabeId, string resetType, string? targetRef, CancellationToken ct = default)
    {
        _logger.LogInformation("Reset ({ResetType}) für Aufgabe {AufgabeId} durchführen.", resetType, aufgabeId);

        var aufgabe = await GetAufgabeMitKlonPfadAsync(aufgabeId, ct);

        await _gitPlugin.ResetAsync(aufgabe.LokalerKlonPfad!, resetType, targetRef, ct);

        var ziel = targetRef ?? "HEAD";
        await _protokollService.AddEintragAsync(
            aufgabeId,
            ProtokollTyp.GitAktion,
            $"Reset ({resetType}) auf {ziel}",
            ct: ct);
    }

    /// <summary>Pusht den Branch auf den Remote.</summary>
    /// <param name="aufgabeId">ID der Aufgabe.</param>
    /// <param name="ct">Abbruch-Token.</param>
    public async Task PushDurchfuehrenAsync(Guid aufgabeId, CancellationToken ct = default)
    {
        _logger.LogInformation("Push für Aufgabe {AufgabeId} durchführen.", aufgabeId);

        var aufgabe = await GetAufgabeMitKlonPfadAsync(aufgabeId, ct);

        if (string.IsNullOrEmpty(aufgabe.BranchName))
            throw new InvalidOperationException($"Aufgabe {aufgabeId} hat keinen Branch-Namen.");

        await _gitPlugin.PushBranchAsync(aufgabe.LokalerKlonPfad!, aufgabe.BranchName, ct);

        await _protokollService.AddEintragAsync(
            aufgabeId,
            ProtokollTyp.GitAktion,
            $"Push: Branch '{aufgabe.BranchName}' gepusht.",
            ct: ct);
    }

    /// <summary>Holt Änderungen vom Remote.</summary>
    /// <param name="aufgabeId">ID der Aufgabe.</param>
    /// <param name="ct">Abbruch-Token.</param>
    public async Task PullDurchfuehrenAsync(Guid aufgabeId, CancellationToken ct = default)
    {
        _logger.LogInformation("Pull für Aufgabe {AufgabeId} durchführen.", aufgabeId);

        var aufgabe = await GetAufgabeMitKlonPfadAsync(aufgabeId, ct);

        await _gitPlugin.PullAsync(aufgabe.LokalerKlonPfad!, ct);

        await _protokollService.AddEintragAsync(
            aufgabeId,
            ProtokollTyp.GitAktion,
            "Pull: Änderungen vom Remote geholt.",
            ct: ct);
    }

    /// <summary>Erstellt einen Pull Request für die Aufgabe.</summary>
    /// <param name="aufgabeId">ID der Aufgabe.</param>
    /// <param name="repositoryId">ID des Repositories.</param>
    /// <param name="title">Titel des Pull Requests.</param>
    /// <param name="body">Beschreibung des Pull Requests.</param>
    /// <param name="ct">Abbruch-Token.</param>
    /// <returns>Der erstellte Pull Request.</returns>
    public async Task<PullRequest> PullRequestErstellenAsync(
        Guid aufgabeId,
        string repositoryId,
        string title,
        string body,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Pull Request für Aufgabe {AufgabeId} erstellen.", aufgabeId);

        var aufgabe = await _aufgabeService.GetDetailAsync(aufgabeId, ct)
            ?? throw new InvalidOperationException($"Aufgabe {aufgabeId} nicht gefunden.");

        if (string.IsNullOrEmpty(aufgabe.BranchName))
            throw new InvalidOperationException($"Aufgabe {aufgabeId} hat keinen Branch-Namen.");

        var prBody = PullRequestBodyBuilder.Build(aufgabe, body);
        var issueNummer = aufgabe.IssueReferenz?.IssueNummer;
        var pullRequest = await _gitPlugin.CreatePullRequestAsync(repositoryId, aufgabe.BranchName, null, title, prBody, ct);

        var issueLogSuffix = issueNummer is > 0
            ? $" (Issue #{issueNummer.Value}, Auto-Close aktiv)"
            : string.Empty;

        await _protokollService.AddEintragAsync(
            aufgabeId,
            ProtokollTyp.GitAktion,
            $"Pull Request erstellt: #{pullRequest.Nummer} – {pullRequest.Titel} ({pullRequest.Url}){issueLogSuffix}",
            ct: ct);

        return pullRequest;
    }

    /// <summary>Schließt die Aufgabe ab: Klon löschen, Status auf Beendet setzen.</summary>
    /// <param name="aufgabeId">ID der Aufgabe.</param>
    /// <param name="ct">Abbruch-Token.</param>
    public async Task AbschliessenAsync(Guid aufgabeId, CancellationToken ct = default)
    {
        _logger.LogInformation("Aufgabe {AufgabeId} abschließen.", aufgabeId);

        var aufgabe = await _aufgabeService.GetDetailAsync(aufgabeId, ct)
            ?? throw new InvalidOperationException($"Aufgabe {aufgabeId} nicht gefunden.");

        if (!await _aufgabeService.CanCompleteTaskAsync(aufgabeId, ct))
        {
            var offeneTodoCount = aufgabe.Todos.Count(t => t.IstOffen);
            throw new InvalidOperationException(
                string.Format(AufgabeService.OffeneTodosFehlermeldungFormat, offeneTodoCount));
        }

        var vonStatus = aufgabe.Status;

        if (!string.IsNullOrEmpty(aufgabe.LokalerKlonPfad) && Directory.Exists(aufgabe.LokalerKlonPfad))
        {
            _logger.LogInformation("Klon-Verzeichnis '{KlonPfad}' löschen.", aufgabe.LokalerKlonPfad);
            DeleteDirectoryForce(aufgabe.LokalerKlonPfad);
        }

        await _aufgabeService.AbschliessenAsync(aufgabeId, ct);
        await _protokollService.AddStatusUebergangAsync(aufgabeId, vonStatus, AufgabeStatus.Beendet, ct);

        _logger.LogInformation("Aufgabe {AufgabeId} erfolgreich abgeschlossen.", aufgabeId);
    }

    /// <summary>Gibt die Remote-Branches eines Repositories zurück.</summary>
    /// <param name="repositoryUrl">URL des Repositories.</param>
    /// <param name="selectedScmPluginPrefix">Optionaler Prefix des zu verwendenden SCM-Plugins.</param>
    /// <param name="ct">Abbruch-Token.</param>
    /// <returns>Liste der Remote-Branch-Namen.</returns>
    public async Task<IEnumerable<string>> GetRemoteBranchesAsync(string repositoryUrl, string? selectedScmPluginPrefix = null, CancellationToken ct = default)
    {
        var gitPlugin = await _pluginSelectionService.ResolveSourceCodeManagementPluginAsync(selectedScmPluginPrefix, ct);
        return await gitPlugin.GetRemoteBranchesAsync(repositoryUrl, ct);
    }

    /// <summary>Führt das Repository-Startskript für eine Aufgabe manuell aus.</summary>
    /// <param name="aufgabeId">ID der Aufgabe.</param>
    /// <param name="ct">Abbruch-Token.</param>
    /// <returns>Ergebnis der Startskript-Ausführung.</returns>
    public async Task<string> RepositoryStartskriptAusfuehrenAsync(Guid aufgabeId, CancellationToken ct = default)
    {
        if (_options.RepositoryStartskriptService is null)
        {
            return "Startskript-Dienst ist nicht konfiguriert.";
        }

        var aufgabe = await _aufgabeService.GetDetailAsync(aufgabeId, ct)
            ?? throw new InvalidOperationException($"Aufgabe {aufgabeId} nicht gefunden.");

        if (string.IsNullOrEmpty(aufgabe.LokalerKlonPfad))
        {
            throw new InvalidOperationException($"Aufgabe {aufgabeId} hat keinen lokalen Klonpfad.");
        }

        var repository = await ResolveRepositoryAsync(aufgabe, aufgabe.GitRepository?.RepositoryUrl ?? string.Empty, ct);

        if (repository.StartKonfiguration is null || !repository.StartKonfiguration.Aktiv)
        {
            return "Kein aktives Startskript konfiguriert.";
        }

        await _options.RepositoryStartskriptService.RunAsync(aufgabe.LokalerKlonPfad, repository.StartKonfiguration, ct);
        return "Startskript erfolgreich ausgeführt.";
    }

    private async Task RollbackStartAsync(Guid aufgabeId, CancellationToken ct)
    {
        var aufgabe = await _aufgabeService.GetByIdAsync(aufgabeId, ct);
        if (aufgabe is null)
        {
            return;
        }

        if (!string.IsNullOrEmpty(aufgabe.LokalerKlonPfad) && Directory.Exists(aufgabe.LokalerKlonPfad))
        {
            DeleteDirectoryForce(aufgabe.LokalerKlonPfad);
        }

        await _aufgabeService.StartZuruecksetzenAsync(aufgabeId, ct);
    }

    private async Task<IGitPlugin> ResolvePluginAsync(
        GitRepository repository,
        string? selectedScmPluginPrefix,
        Guid aufgabeId,
        CancellationToken ct)
    {
        var resolvedPluginPrefix = !string.IsNullOrWhiteSpace(repository.PluginTyp)
            ? repository.PluginTyp
            : selectedScmPluginPrefix;
        if (string.IsNullOrWhiteSpace(resolvedPluginPrefix))
        {
            _logger.LogWarning(
                "Aufgabe {AufgabeId}: Kein SCM-Plugin-Typ am Repository konfiguriert und kein SCM-Plugin-Prefix übergeben — erster verfügbarer SCM-Plugin wird verwendet.",
                aufgabeId);
        }
        return await _pluginSelectionService.ResolveSourceCodeManagementPluginAsync(resolvedPluginPrefix, ct);
    }

    private async Task<string> PrepareCloneDirectoryAsync(
        IGitPlugin gitPlugin,
        string repositoryUrl,
        Guid aufgabeId,
        CancellationToken ct)
    {
        var workdirResult = await _arbeitsverzeichnisResolver.ResolveAsync(ct);
        var lokalerKlonPfad = Path.Combine(workdirResult.ResolvedPath, KlonBasisVerzeichnis, aufgabeId.ToString());

        if (workdirResult.UsedFallback)
        {
            await _protokollService.AddEintragAsync(
                aufgabeId,
                ProtokollTyp.GitAktion,
                $"Arbeitsverzeichnis-Fallback aktiv ({workdirResult.ReasonCode}). Verwende {workdirResult.ResolvedPath}.",
                ct: ct);
        }

        if (Directory.Exists(lokalerKlonPfad))
        {
            _logger.LogInformation("Zielverzeichnis '{KlonPfad}' existiert bereits, wird gelöscht.", lokalerKlonPfad);
            DeleteDirectoryForce(lokalerKlonPfad);
        }

        _logger.LogInformation("Repository '{RepositoryUrl}' nach '{KlonPfad}' klonen.", repositoryUrl, lokalerKlonPfad);
        await gitPlugin.CloneRepositoryAsync(repositoryUrl, lokalerKlonPfad, ct);
        return lokalerKlonPfad;
    }

    private async Task<(string BranchName, bool NutzeExistierendenBranch, string? BasisBranchName)> SetupBranchAsync(
        IGitPlugin gitPlugin,
        string repositoryUrl,
        string lokalerKlonPfad,
        string? basisBranchName,
        string? defaultSourceBranchName,
        Aufgabe aufgabe,
        CancellationToken ct)
    {
        var reviewSources = aufgabe.PullRequests
            .Where(p => p.Rolle == PullRequestReferenzRolle.ReviewSource)
            .ToList();
        if (reviewSources.Count > 1)
        {
            throw new InvalidOperationException("Die Aufgabe besitzt mehrere Pull-Request-Review-Quellen.");
        }

        if (reviewSources.Count == 1)
        {
            return await CheckoutReviewSourceAsync(gitPlugin, repositoryUrl, lokalerKlonPfad, reviewSources[0], ct);
        }

        string? defaultBranch = null;
        var nutzeExistierendenBranch = false;
        if (!string.IsNullOrEmpty(basisBranchName))
        {
            defaultBranch = await gitPlugin.GetDefaultBranchAsync(repositoryUrl, ct);
            nutzeExistierendenBranch = !string.Equals(basisBranchName, defaultBranch, StringComparison.OrdinalIgnoreCase);
        }

        if (nutzeExistierendenBranch)
        {
            return await CheckoutExistingBranchAsync(gitPlugin, lokalerKlonPfad, basisBranchName!, ct);
        }

        return await CreateNewTaskBranchAsync(gitPlugin, repositoryUrl, lokalerKlonPfad, defaultSourceBranchName, aufgabe, defaultBranch, ct);
    }

    private async Task<(string BranchName, bool NutzeExistierendenBranch, string? BasisBranch)> CheckoutReviewSourceAsync(
        IGitPlugin gitPlugin,
        string repositoryUrl,
        string lokalerKlonPfad,
        PullRequestReferenz reviewSource,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(reviewSource.SourceBranch))
            throw new InvalidOperationException("Die Pull-Request-Review-Quelle besitzt keinen Quell-Branch.");

        var checkoutSpec = new PullRequestCheckoutSpec(
            reviewSource.RepositoryId,
            repositoryUrl,
            string.IsNullOrWhiteSpace(reviewSource.SourceRepositoryId) ? reviewSource.RepositoryId : reviewSource.SourceRepositoryId,
            reviewSource.SourceRepositoryUrl,
            reviewSource.SourceBranch,
            reviewSource.SourceRef,
            reviewSource.HeadSha);

        _logger.LogInformation(
            "Checke Pull-Request-Review-Quelle {RepositoryId}#{PullRequestNumber} als Branch '{BranchName}' aus.",
            reviewSource.RepositoryId,
            reviewSource.PullRequestNumber,
            reviewSource.SourceBranch);
        await gitPlugin.CheckoutPullRequestSourceAsync(lokalerKlonPfad, checkoutSpec, ct);
        return (reviewSource.SourceBranch, true, null);
    }

    private async Task<(string BranchName, bool NutzeExistierendenBranch, string? BasisBranch)> CheckoutExistingBranchAsync(
        IGitPlugin gitPlugin,
        string lokalerKlonPfad,
        string basisBranchName,
        CancellationToken ct)
    {
        _logger.LogInformation("Wechsle zu vorhandenem Branch '{BasisBranch}'.", basisBranchName);
        await gitPlugin.CheckoutRemoteBranchAsync(lokalerKlonPfad, basisBranchName, ct);
        return (basisBranchName, true, null);
    }

    private async Task<(string BranchName, bool NutzeExistierendenBranch, string? BasisBranch)> CreateNewTaskBranchAsync(
        IGitPlugin gitPlugin,
        string repositoryUrl,
        string lokalerKlonPfad,
        string? defaultSourceBranchName,
        Aufgabe aufgabe,
        string? cachedDefaultBranch,
        CancellationToken ct)
    {
        var branchName = ErstelleTaskBranchName(aufgabe);
        string? basisBranch = null;

        if (!string.IsNullOrEmpty(defaultSourceBranchName))
        {
            var defaultBranch = cachedDefaultBranch ?? await gitPlugin.GetDefaultBranchAsync(repositoryUrl, ct);
            if (!string.Equals(defaultSourceBranchName, defaultBranch, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("Basis-Branch '{BasisBranch}' lokal nachziehen.", defaultSourceBranchName);
                await gitPlugin.CheckoutRemoteBranchAsync(lokalerKlonPfad, defaultSourceBranchName, ct);
            }

            _logger.LogInformation("Branch '{BranchName}' vom Basis-Branch '{BasisBranch}' anlegen.", branchName, defaultSourceBranchName);
            await gitPlugin.CreateBranchAsync(lokalerKlonPfad, branchName, defaultSourceBranchName, ct);
            basisBranch = defaultSourceBranchName;
        }
        else
        {
            _logger.LogInformation("Branch '{BranchName}' anlegen.", branchName);
            await gitPlugin.CreateBranchAsync(lokalerKlonPfad, branchName, null, ct);
            basisBranch = null;
        }

        return (branchName, false, basisBranch);
    }

    private async Task FinalizeStartAsync(
        Guid aufgabeId,
        Aufgabe aufgabe,
        GitRepository repository,
        string lokalerKlonPfad,
        string branchName,
        bool nutzeExistierendenBranch,
        string? basisBranch,
        CancellationToken ct)
    {
        var initialisierungsskriptHinweis = await RunInitialisierungsskriptAsync(aufgabeId, repository, lokalerKlonPfad, ct);
        var startskriptHinweis = await RunStartskriptAsync(aufgabeId, repository, lokalerKlonPfad, ct);

        await CreateIssueFileAsync(lokalerKlonPfad, aufgabe, branchName, repository.StartKonfiguration, ct);
        await UpdateGitignoreAsync(lokalerKlonPfad, repository.StartKonfiguration, ct);

        await _aufgabeService.StartenAsync(aufgabeId, branchName, lokalerKlonPfad, basisBranch, ct);

        var protokollNachricht = nutzeExistierendenBranch
            ? $"Klon angelegt, vorhandener Branch ausgecheckt: {branchName} in {lokalerKlonPfad}"
            : $"Klon und Branch angelegt: {branchName} in {lokalerKlonPfad}";
        if (!string.IsNullOrWhiteSpace(initialisierungsskriptHinweis))
        {
            protokollNachricht = $"{protokollNachricht}\n{initialisierungsskriptHinweis}";
        }
        if (!string.IsNullOrWhiteSpace(startskriptHinweis))
        {
            protokollNachricht = $"{protokollNachricht}\n{startskriptHinweis}";
        }

        await _protokollService.AddEintragAsync(aufgabeId, ProtokollTyp.GitAktion, protokollNachricht, ct: ct);
    }

    private async Task<string?> RunInitialisierungsskriptAsync(Guid aufgabeId, GitRepository repository, string lokalerKlonPfad, CancellationToken ct)
    {
        if (repository.InitialisierungKonfiguration is null || _options.RepositoryInitialisierungService is null)
        {
            return null;
        }

        return await RunOptionalRepositoryScriptAsync(
            aufgabeId,
            "Initialisierungsskript",
            () => _options.RepositoryInitialisierungService.RunAsync(lokalerKlonPfad, repository.InitialisierungKonfiguration, ct),
            ct);
    }

    private async Task<string?> RunStartskriptAsync(Guid aufgabeId, GitRepository repository, string lokalerKlonPfad, CancellationToken ct)
    {
        if (repository.StartKonfiguration is null || _options.RepositoryStartskriptService is null)
        {
            return null;
        }

        return await RunOptionalRepositoryScriptAsync(
            aufgabeId,
            "Startskript",
            () => _options.RepositoryStartskriptService.RunAsync(lokalerKlonPfad, repository.StartKonfiguration, ct),
            ct);
    }

    private async Task<string?> RunOptionalRepositoryScriptAsync(Guid aufgabeId, string scriptLabel, Func<Task> runAsync, CancellationToken ct)
    {
        try
        {
            await runAsync();
            return null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Repository-{ScriptLabel} für Aufgabe {AufgabeId} ist fehlgeschlagen.", scriptLabel, aufgabeId);
            return $"Hinweis: Das Repository-{scriptLabel} konnte nicht ausgeführt werden ({ex.Message}).";
        }
    }

    private async Task<GitRepository> ResolveRepositoryAsync(Aufgabe aufgabe, string repositoryUrl, CancellationToken ct)
    {
        if (aufgabe.GitRepository is not null)
        {
            return aufgabe.GitRepository;
        }

        if (_options.ProjektService is null)
        {
            return new GitRepository
            {
                Id = Guid.Empty,
                ProjektId = aufgabe.ProjektId,
                PluginTyp = string.Empty,
                RepositoryUrl = repositoryUrl,
                RepositoryName = repositoryUrl,
                Aktiv = true
            };
        }

        var projekt = await _options.ProjektService.GetDetailAsync(aufgabe.ProjektId, ct)
            ?? throw new InvalidOperationException($"Projekt {aufgabe.ProjektId} nicht gefunden.");

        var repositories = projekt.Repositories
            .Where(r => r.Aktiv)
            .OrderBy(r => r.RepositoryName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.Id)
            .ToList();

        var matching = repositories.FirstOrDefault(r =>
            string.Equals(r.RepositoryUrl, repositoryUrl, StringComparison.OrdinalIgnoreCase));

        if (matching is not null)
        {
            return matching;
        }

        if (repositories.Count == 1)
        {
            return repositories[0];
        }

        throw new InvalidOperationException(
            $"Aufgabe {aufgabe.Id}: kein eindeutiges Repository für den Startkontext ermittelbar.");
    }

    private async Task<Aufgabe> GetAufgabeMitKlonPfadAsync(Guid aufgabeId, CancellationToken ct)
    {
        var aufgabe = await _aufgabeService.GetByIdAsync(aufgabeId, ct)
            ?? throw new InvalidOperationException($"Aufgabe {aufgabeId} nicht gefunden.");

        if (string.IsNullOrWhiteSpace(aufgabe.LokalerKlonPfad))
            throw new InvalidOperationException($"Aufgabe {aufgabeId} hat keinen lokalen Klonpfad.");

        return aufgabe;
    }

    private static string ErstelleTaskBranchName(Aufgabe aufgabe)
    {
        var titelSlug = ErstelleTitelSlug(aufgabe.Titel);
        var issueNummer = aufgabe.IssueReferenz?.IssueNummer;

        return issueNummer is > 0
            ? $"task/issue-{issueNummer.Value}-{aufgabe.Id:N}-{titelSlug}"
            : $"task/{aufgabe.Id:N}-{titelSlug}";
    }

    private static string ErstelleTitelSlug(string titel)
    {
        var slug = titel.ToLowerInvariant()
            .Replace(' ', '-')
            .Replace("ä", "ae")
            .Replace("ö", "oe")
            .Replace("ü", "ue")
            .Replace("ß", "ss");

        slug = new string(slug.Where(c => char.IsLetterOrDigit(c) || c == '-').ToArray());

        while (slug.Contains("--"))
            slug = slug.Replace("--", "-");

        slug = slug.Trim('-');

        if (slug.Length > 30)
            slug = slug[..30].TrimEnd('-');

        return string.IsNullOrEmpty(slug) ? "aufgabe" : slug;
    }

    private async Task CreateIssueFileAsync(string lokalerKlonPfad, Aufgabe aufgabe, string branchName, RepositoryStartKonfiguration? startKonfiguration, CancellationToken ct)
    {
        try
        {
            var beschreibung = string.IsNullOrWhiteSpace(aufgabe.AnforderungsBeschreibung)
                ? "[Keine Anforderungsbeschreibung verfügbar]"
                : aufgabe.AnforderungsBeschreibung;

            var metaDaten = $"""
                # Aufgabe: {aufgabe.Titel}

                **Aufgaben-ID:** {aufgabe.Id}
                **Branch:** {branchName}
                **Erstellt:** {aufgabe.ErstellungsDatum:yyyy-MM-dd}
                """;

            var issueAbschnitt = aufgabe.IssueReferenz is { IssueNummer: > 0 } referenz
                ? $"""


                    ## Verknüpftes Issue

                    **Kennung:** #{referenz.IssueNummer}
                    **Titel:** {referenz.Titel}
                    """
                : string.Empty;

            var anforderungsAbschnitt = $"""


                ## Anforderung

                {beschreibung}
                """;

            var inhalt = metaDaten + issueAbschnitt + anforderungsAbschnitt;

            var effektivesVerzeichnis = EnsureEffectiveWorkingDirectory(lokalerKlonPfad, startKonfiguration);

            var issueFilePath = Path.Combine(effektivesVerzeichnis, "issue.md");
            await File.WriteAllTextAsync(issueFilePath, inhalt, ct);
            _logger.LogInformation("issue.md für Aufgabe {AufgabeId} erfolgreich erstellt.", aufgabe.Id);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Fehler beim Erstellen von issue.md für Aufgabe {AufgabeId}.", aufgabe.Id);
        }
    }

    private async Task UpdateGitignoreAsync(string lokalerKlonPfad, RepositoryStartKonfiguration? startKonfiguration, CancellationToken ct)
    {
        var effektivesVerzeichnis = lokalerKlonPfad;
        try
        {
            effektivesVerzeichnis = EnsureEffectiveWorkingDirectory(lokalerKlonPfad, startKonfiguration);

            var gitignorePath = Path.Combine(effektivesVerzeichnis, ".gitignore");
            var existingContent = File.Exists(gitignorePath)
                ? await File.ReadAllTextAsync(gitignorePath, ct)
                : string.Empty;

            var lines = existingContent.Split('\n').Select(l => l.TrimEnd('\r'));
            if (lines.Contains("issue.md", StringComparer.OrdinalIgnoreCase))
            {
                return;
            }

            var newContent = existingContent.Length > 0 && !existingContent.EndsWith('\n')
                ? existingContent + "\nissue.md\n"
                : existingContent + "issue.md\n";

            await File.WriteAllTextAsync(gitignorePath, newContent, new System.Text.UTF8Encoding(false), ct);
            _logger.LogInformation(".gitignore für '{KlonPfad}' aktualisiert: 'issue.md' eingetragen.", effektivesVerzeichnis);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Fehler beim Aktualisieren von .gitignore in '{KlonPfad}'.", effektivesVerzeichnis);
        }
    }

    private static string EnsureEffectiveWorkingDirectory(string lokalerKlonPfad, RepositoryStartKonfiguration? startKonfiguration)
    {
        var effektivesVerzeichnis = WorkingDirectoryResolver.ResolveEffectiveWorkingDirectory(
            lokalerKlonPfad, startKonfiguration?.WorkingDirectoryRelativePath);
        Directory.CreateDirectory(effektivesVerzeichnis);
        return effektivesVerzeichnis;
    }

    private static void DeleteDirectoryForce(string path)
    {
        foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(path, recursive: true);
    }

    private static async Task ValidateBaseBranchExistsAsync(GitRepository gitRepository, IGitPlugin gitPlugin, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(gitRepository.DefaultSourceBranchName))
        {
            return;
        }

        var remoteBranches = await gitPlugin.GetRemoteBranchesAsync(gitRepository.RepositoryUrl, ct);
        if (!remoteBranches.Contains(gitRepository.DefaultSourceBranchName, StringComparer.OrdinalIgnoreCase))
        {
            throw new GitBranchNotFoundException(gitRepository.DefaultSourceBranchName);
        }
    }
}
