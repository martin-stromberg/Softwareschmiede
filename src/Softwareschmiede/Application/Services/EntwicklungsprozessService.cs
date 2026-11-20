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
/// <param name="PromptZeitVersandService">Optionaler Dienst zum Zurückziehen geplanter zeitgesteuerter Prompts beim Aufgaben-Abschluss.</param>
/// <returns>Eine neue Instanz mit den angegebenen optionalen Abhängigkeiten.</returns>
public sealed record EntwicklungsprozessServiceOptions(
    ProjektService? ProjektService = null,
    RepositoryStartskriptService? RepositoryStartskriptService = null,
    RepositoryInitialisierungService? RepositoryInitialisierungService = null,
    KiAusfuehrungsService? KiAusfuehrungsService = null,
    GitOrchestrationService? GitOrchestrationService = null,
    PromptZeitVersandService? PromptZeitVersandService = null);

/// <summary>
/// Koordiniert Git-Repository-Setup für Aufgaben und Rate-Limit-Marker-Erkennung.
/// </summary>
public sealed class EntwicklungsprozessService
{
    private readonly AufgabeService _aufgabeService;
    private readonly ProtokollService _protokollService;
    private readonly IGitPlugin _gitPlugin;
    private readonly PluginSelectionService _pluginSelectionService;
    private readonly IWorkingDirectoryResolver _arbeitsverzeichnisResolver;
    private readonly EntwicklungsprozessServiceOptions _options;
    private readonly ILogger<EntwicklungsprozessService> _logger;

    internal const string KlonBasisVerzeichnis = "softwareschmiede";

    /// <inheritdoc cref="EntwicklungsprozessService"/>
    public EntwicklungsprozessService(
        AufgabeService aufgabeService,
        ProtokollService protokollService,
        IGitPlugin gitPlugin,
        PluginSelectionService pluginSelectionService,
        IWorkingDirectoryResolver arbeitsverzeichnisResolver,
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
        IWorkingDirectoryResolver arbeitsverzeichnisResolver,
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
    /// Setzt den Status auf <see cref="WorkItemStatus.Started"/>.
    /// </summary>
    /// <param name="workItemId">ID der zu startenden Aufgabe.</param>
    /// <param name="repositoryUrl">URL des zu klonenden Repositories.</param>
    /// <param name="basisBranchName">Optionaler Basis-Branch; wird ein neuer Task-Branch angelegt, wenn er dem Default-Branch entspricht.</param>
    /// <param name="selectedScmPluginPrefix">Optionaler Prefix des zu verwendenden SCM-Plugins.</param>
    /// <param name="ct">Abbruch-Token.</param>
    internal Task ProzessStartenAsync(
        Guid workItemId,
        string repositoryUrl,
        string? basisBranchName = null,
        string? selectedScmPluginPrefix = null,
        CancellationToken ct = default)
        => ScmTaskCoordinator.Shared.RunAsync(workItemId,
            () => ProzessStartenCoordinatedAsync(workItemId, repositoryUrl, basisBranchName, selectedScmPluginPrefix, ct), ct);

    private async Task ProzessStartenCoordinatedAsync(Guid workItemId, string repositoryUrl,
        string? basisBranchName, string? selectedScmPluginPrefix, CancellationToken ct)
    {
        _logger.LogInformation("Repository-Setup für Aufgabe {AufgabeId} starten.", workItemId);

        var workItem = await _aufgabeService.GetDetailAsync(workItemId, ct)
            ?? throw new InvalidOperationException($"Aufgabe {workItemId} nicht gefunden.");
        WirfWennPausiert(workItem);
        if (!string.IsNullOrWhiteSpace(workItem.LocalClonePath)) return;
        await ProzessStartenCoreAsync(workItemId, workItem, repositoryUrl, basisBranchName, selectedScmPluginPrefix, ct);

        _logger.LogInformation("Repository-Setup für Aufgabe {AufgabeId} abgeschlossen.", workItemId);
    }

    /// <summary>
    /// Kombiniert Repository-Setup (Klon, Branch) und CLI-Start in einem Schritt.
    /// Setzt den Status direkt auf <see cref="WorkItemStatus.Started"/> und startet anschließend die CLI mit dem gewählten Plugin.
    /// Im Fehlerfall wird der Status zurückgesetzt und das Klon-Verzeichnis gelöscht.
    /// </summary>
    /// <param name="workItemId">ID der zu startenden Aufgabe.</param>
    /// <param name="repositoryUrl">URL des zu klonenden Repositories.</param>
    /// <param name="basisBranchName">Optionaler Basis-Branch.</param>
    /// <param name="kiPluginPrefix">Optionaler Prefix des zu verwendenden KI-Plugins.</param>
    /// <param name="ct">Abbruch-Token.</param>
    /// <param name="strictPluginSelection">Verhindert Ersatzplugins bei automatischer SCM-Zustellung.</param>
    /// <param name="optionalParameters">Explizite Startparameter des gewählten Plugins.</param>
    public Task ProzessStartenUndCliStartenAsync(
        Guid workItemId,
        string repositoryUrl,
        string? basisBranchName,
        string? kiPluginPrefix,
        CancellationToken ct = default,
        bool strictPluginSelection = false,
        string? optionalParameters = null)
        => ScmTaskCoordinator.Shared.RunAsync(workItemId,
            () => ProzessUndCliCoordinatedAsync(workItemId, repositoryUrl, basisBranchName, kiPluginPrefix, ct, strictPluginSelection, optionalParameters), ct);

    private async Task ProzessUndCliCoordinatedAsync(Guid workItemId, string repositoryUrl,
        string? basisBranchName, string? kiPluginPrefix, CancellationToken ct, bool strictPluginSelection, string? optionalParameters)
    {
        if (_options.KiAusfuehrungsService is null)
        {
            throw new InvalidOperationException("KiAusfuehrungsService ist nicht konfiguriert.");
        }

        // Der Pausen-Guard steht bewusst VOR dem try-Block: Ein InvalidOperationException-Wurf
        // innerhalb von try würde den Rollback-Pfad auslösen (Klon löschen + Status-Reset), obwohl
        // die Aufgabe bei aktiver Pause gar nicht angefasst werden soll.
        var vorabAufgabe = await _aufgabeService.GetByIdAsync(workItemId, ct)
            ?? throw new InvalidOperationException($"Aufgabe {workItemId} nicht gefunden.");
        WirfWennPausiert(vorabAufgabe);
        if (vorabAufgabe.Status is WorkItemStatus.Completed or WorkItemStatus.Archived)
            throw new InvalidOperationException("Beendete oder archivierte Aufgaben können nicht gestartet werden.");
        // A competing automatic/manual start may have completed while we waited for the task gate.
        // Reuse that workspace before entering setup or its rollback scope.
        if (!string.IsNullOrWhiteSpace(vorabAufgabe.LocalClonePath))
        {
            await CliNeustartenAsync(workItemId, kiPluginPrefix, optionalParameters, ct, strictPluginSelection);
            return;
        }
        var previousClone = vorabAufgabe.LocalClonePath;
        var previousStatus = vorabAufgabe.Status;
        var strictPlugin = strictPluginSelection
            ? await _pluginSelectionService.ResolveDevelopmentAutomationPluginStrictAsync(kiPluginPrefix!, ct)
            : null;

        try
        {
            var workItem = await _aufgabeService.GetDetailAsync(workItemId, ct)
                ?? throw new InvalidOperationException($"Aufgabe {workItemId} nicht gefunden.");
            var (repository, gitPlugin) = await ProzessStartenCoreAsync(workItemId, workItem, repositoryUrl, basisBranchName, null, ct, strictPluginSelection);

            workItem = await _aufgabeService.GetByIdAsync(workItemId, ct)
                ?? throw new InvalidOperationException($"Aufgabe {workItemId} nicht gefunden.");

            if (string.IsNullOrEmpty(workItem.LocalClonePath))
            {
                throw new InvalidOperationException($"Aufgabe {workItemId} hat keinen lokalen Klonpfad.");
            }

            var kiPlugin = strictPlugin is not null
                ? await _pluginSelectionService.ResolveDevelopmentAutomationPluginStrictAsync(strictPlugin.PluginPrefix, ct)
                : await _pluginSelectionService.ResolveDevelopmentAutomationPluginAsync(kiPluginPrefix, ct);
            await _aufgabeService.UpdateAsync(
                workItemId,
                workItem.Title,
                workItem.RequirementDescription,
                kiPlugin.PluginPrefix,
                ct);

            await _options.KiAusfuehrungsService.StartTerminalSessionAsync(
                workItemId, kiPlugin, workItem.LocalClonePath, optionalParameters, ct, repository.StartConfiguration, gitPlugin,
                repositoryUrl: repository.RepositoryUrl);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("CLI-Start für Aufgabe {AufgabeId} abgebrochen, Rollback wird durchgeführt.", workItemId);
            await RollbackStartAsync(workItemId, CancellationToken.None, previousClone, strictPluginSelection ? previousStatus : null);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CLI-Start für Aufgabe {AufgabeId} fehlgeschlagen, Rollback wird durchgeführt.", workItemId);
            await RollbackStartAsync(workItemId, CancellationToken.None, previousClone, strictPluginSelection ? previousStatus : null);
            throw;
        }
    }

    private async Task<(GitRepository Repository, IGitPlugin GitPlugin)> ProzessStartenCoreAsync(
        Guid workItemId,
        WorkItem workItem,
        string repositoryUrl,
        string? basisBranchName,
        string? selectedScmPluginPrefix,
        CancellationToken ct,
        bool preserveExistingDirectory = false)
    {
        var repository = await ResolveRepositoryAsync(workItem, repositoryUrl, ct);
        var gitPlugin = await ResolvePluginAsync(repository, selectedScmPluginPrefix, workItemId, ct);

        await ValidateBaseBranchExistsAsync(repository, gitPlugin, ct);

        var lokalerKlonPfad = await PrepareCloneDirectoryAsync(gitPlugin, repository.RepositoryUrl, workItemId, ct, preserveExistingDirectory);

        if (_options.GitOrchestrationService is not null)
        {
            await _options.GitOrchestrationService.ValidateWorkingDirectoryAfterCloneAsync(lokalerKlonPfad, repository.StartConfiguration, gitPlugin);
        }

        var (branchName, nutzeExistierendenBranch, basisBranch) = await SetupBranchAsync(gitPlugin, repository.RepositoryUrl, lokalerKlonPfad, basisBranchName, repository.DefaultSourceBranchName, workItem, ct);
        await FinalizeStartAsync(workItemId, workItem, repository, lokalerKlonPfad, branchName, nutzeExistierendenBranch, basisBranch, ct);

        return (repository, gitPlugin);
    }

    /// <summary>Startet die KI-CLI erneut im bereits vorbereiteten Klon einer Aufgabe.</summary>
    public Task CliNeustartenAsync(
        Guid workItemId,
        string? kiPluginPrefix,
        string? optionalParameters,
        CancellationToken ct = default,
        bool strictPluginSelection = false)
        => ScmTaskCoordinator.Shared.RunAsync(workItemId,
            () => CliNeustartenCoordinatedAsync(workItemId, kiPluginPrefix, optionalParameters, ct, strictPluginSelection), ct);

    private async Task CliNeustartenCoordinatedAsync(Guid workItemId, string? kiPluginPrefix,
        string? optionalParameters, CancellationToken ct, bool strictPluginSelection)
    {
        if (_options.KiAusfuehrungsService is null)
        {
            throw new InvalidOperationException("KiAusfuehrungsService ist nicht konfiguriert.");
        }

        var workItem = await _aufgabeService.GetDetailAsync(workItemId, ct)
            ?? throw new InvalidOperationException($"Aufgabe {workItemId} nicht gefunden.");

        if (workItem.Status is WorkItemStatus.Completed or WorkItemStatus.Archived)
        {
            throw new InvalidOperationException("Beendete oder archivierte Aufgaben können nicht gestartet werden.");
        }

        // Deckt auch den Plugin-Wechsel-Pfad (PluginWechselAsync → CliNeustartenAsync) ab.
        WirfWennPausiert(workItem);

        var current = _options.KiAusfuehrungsService.GetTerminalSession(workItemId);
        if (current is not null)
        {
            try
            {
                if (current.ExitCode is null && current.Failure is null && !current.Process.HasExited)
                {
                    // Lebende Session wird wiederverwendet: kein Gestartet-Ereignis — stattdessen das
                    // dedizierte CliLaufAktiv-Signal, damit die Laufdaten-Persistenz nachgezogen wird.
                    _options.KiAusfuehrungsService.NotifyLaufAktiv(workItemId);
                    return;
                }
            }
            catch (InvalidOperationException) { }
        }

        if (string.IsNullOrWhiteSpace(workItem.LocalClonePath))
        {
            throw new InvalidOperationException($"Aufgabe {workItemId} hat keinen lokalen Klonpfad.");
        }

        var kiPlugin = strictPluginSelection
            ? await _pluginSelectionService.ResolveDevelopmentAutomationPluginStrictAsync(kiPluginPrefix!, ct)
            : await _pluginSelectionService.ResolveDevelopmentAutomationPluginAsync(kiPluginPrefix, ct);
        await _aufgabeService.UpdateAsync(
            workItemId,
            workItem.Title,
            workItem.RequirementDescription,
            kiPlugin.PluginPrefix,
            ct);

        var repository = await ResolveRepositoryAsync(workItem, workItem.GitRepository?.RepositoryUrl ?? string.Empty, ct);
        var gitPlugin = await ResolvePluginAsync(repository, null, workItemId, ct);

        await _options.KiAusfuehrungsService.StartTerminalSessionAsync(
            workItemId,
            kiPlugin,
            workItem.LocalClonePath,
            optionalParameters,
            ct,
            repository.StartConfiguration,
            gitPlugin,
            repositoryUrl: repository.RepositoryUrl);
    }

    /// <summary>Führt einen manuellen Commit durch.</summary>
    /// <param name="workItemId">ID der Aufgabe.</param>
    /// <param name="message">Commit-Nachricht.</param>
    /// <param name="ct">Abbruch-Token.</param>
    public async Task CommitDurchfuehrenAsync(Guid workItemId, string message, CancellationToken ct = default)
    {
        _logger.LogInformation("Commit für Aufgabe {AufgabeId} durchführen.", workItemId);

        var workItem = await GetAufgabeMitKlonPfadAsync(workItemId, ct);

        await _gitPlugin.CommitAsync(workItem.LocalClonePath!, message, ct);

        await _protokollService.AddEintragAsync(
            workItemId,
            LogEntryType.GitAction,
            $"Commit: {message}",
            ct: ct);

        _logger.LogInformation("Commit für Aufgabe {AufgabeId} durchgeführt.", workItemId);
    }

    /// <summary>Setzt Commits zurück.</summary>
    /// <param name="workItemId">ID der Aufgabe.</param>
    /// <param name="resetType">Reset-Typ (z. B. soft, mixed, hard).</param>
    /// <param name="targetRef">Optionaler Ziel-Ref; Standard ist HEAD.</param>
    /// <param name="ct">Abbruch-Token.</param>
    public async Task ResetDurchfuehrenAsync(Guid workItemId, string resetType, string? targetRef, CancellationToken ct = default)
    {
        _logger.LogInformation("Reset ({ResetType}) für Aufgabe {AufgabeId} durchführen.", resetType, workItemId);

        var workItem = await GetAufgabeMitKlonPfadAsync(workItemId, ct);

        await _gitPlugin.ResetAsync(workItem.LocalClonePath!, resetType, targetRef, ct);

        var ziel = targetRef ?? "HEAD";
        await _protokollService.AddEintragAsync(
            workItemId,
            LogEntryType.GitAction,
            $"Reset ({resetType}) auf {ziel}",
            ct: ct);
    }

    /// <summary>Pusht den Branch auf den Remote.</summary>
    /// <param name="workItemId">ID der Aufgabe.</param>
    /// <param name="ct">Abbruch-Token.</param>
    public async Task PushDurchfuehrenAsync(Guid workItemId, CancellationToken ct = default)
    {
        _logger.LogInformation("Push für Aufgabe {AufgabeId} durchführen.", workItemId);

        var workItem = await GetAufgabeMitKlonPfadAsync(workItemId, ct);

        if (string.IsNullOrEmpty(workItem.BranchName))
            throw new InvalidOperationException($"Aufgabe {workItemId} hat keinen Branch-Namen.");

        await _gitPlugin.PushBranchAsync(workItem.LocalClonePath!, workItem.BranchName, ct);

        await _protokollService.AddEintragAsync(
            workItemId,
            LogEntryType.GitAction,
            $"Push: Branch '{workItem.BranchName}' gepusht.",
            ct: ct);
    }

    /// <summary>Holt Änderungen vom Remote.</summary>
    /// <param name="workItemId">ID der Aufgabe.</param>
    /// <param name="ct">Abbruch-Token.</param>
    public async Task PullDurchfuehrenAsync(Guid workItemId, CancellationToken ct = default)
    {
        _logger.LogInformation("Pull für Aufgabe {AufgabeId} durchführen.", workItemId);

        var workItem = await GetAufgabeMitKlonPfadAsync(workItemId, ct);

        await _gitPlugin.PullAsync(workItem.LocalClonePath!, ct);

        await _protokollService.AddEintragAsync(
            workItemId,
            LogEntryType.GitAction,
            "Pull: Änderungen vom Remote geholt.",
            ct: ct);
    }

    /// <summary>Erstellt einen Pull Request für die Aufgabe.</summary>
    /// <param name="workItemId">ID der Aufgabe.</param>
    /// <param name="repositoryId">ID des Repositories.</param>
    /// <param name="title">Titel des Pull Requests.</param>
    /// <param name="body">Beschreibung des Pull Requests.</param>
    /// <param name="ct">Abbruch-Token.</param>
    /// <returns>Der erstellte Pull Request.</returns>
    public async Task<PullRequest> PullRequestErstellenAsync(
        Guid workItemId,
        string repositoryId,
        string title,
        string body,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Pull Request für Aufgabe {AufgabeId} erstellen.", workItemId);

        var workItem = await _aufgabeService.GetDetailAsync(workItemId, ct)
            ?? throw new InvalidOperationException($"Aufgabe {workItemId} nicht gefunden.");

        if (string.IsNullOrEmpty(workItem.BranchName))
            throw new InvalidOperationException($"Aufgabe {workItemId} hat keinen Branch-Namen.");

        var prBody = PullRequestBodyBuilder.Build(workItem, body);
        var issueNummer = workItem.IssueReference?.IssueNumber;
        var pullRequest = await _gitPlugin.CreatePullRequestAsync(repositoryId, workItem.BranchName, null, title, prBody, ct);

        var issueLogSuffix = issueNummer is > 0
            ? $" (Issue #{issueNummer.Value}, Auto-Close aktiv)"
            : string.Empty;

        await _protokollService.AddEintragAsync(
            workItemId,
            LogEntryType.GitAction,
            $"Pull Request erstellt: #{pullRequest.Nummer} – {pullRequest.Title} ({pullRequest.Url}){issueLogSuffix}",
            ct: ct);

        return pullRequest;
    }

    /// <summary>
    /// Schließt die Aufgabe ab: CLI stoppen, geplanten Prompt zurückziehen, Klon löschen,
    /// Status auf Beendet setzen. Einziger produktiver Einstieg des Geschäftsvorgangs —
    /// läuft wie die Start-Einstiege unter der <see cref="ScmTaskCoordinator"/>-Aufgabensperre.
    /// </summary>
    /// <param name="workItemId">ID der Aufgabe.</param>
    /// <param name="ct">Abbruch-Token.</param>
    public Task AbschliessenAsync(Guid workItemId, CancellationToken ct = default)
        => ScmTaskCoordinator.Shared.RunAsync(workItemId,
            () => AbschliessenCoordinatedAsync(workItemId, ct), ct);

    private async Task AbschliessenCoordinatedAsync(Guid workItemId, CancellationToken ct)
    {
        _logger.LogInformation("Aufgabe {AufgabeId} abschließen.", workItemId);

        if (_options.KiAusfuehrungsService is not null)
        {
            await _options.KiAusfuehrungsService.StopCliAsync(workItemId, ct);
        }

        if (_options.PromptZeitVersandService is not null)
        {
            await _options.PromptZeitVersandService.CancelScheduledPromptAsync(workItemId);
        }

        var workItem = await _aufgabeService.GetDetailAsync(workItemId, ct)
            ?? throw new InvalidOperationException($"Aufgabe {workItemId} nicht gefunden.");

        if (!await _aufgabeService.CanCompleteTaskAsync(workItemId, ct))
        {
            var offeneTodoCount = workItem.Todos.Count(t => t.IstOffen);
            throw new InvalidOperationException(
                string.Format(AufgabeService.OffeneTodosFehlermeldungFormat, offeneTodoCount));
        }

        var vonStatus = workItem.Status;

        if (!string.IsNullOrEmpty(workItem.LocalClonePath) && Directory.Exists(workItem.LocalClonePath))
        {
            _logger.LogInformation("Klon-Verzeichnis '{KlonPfad}' löschen.", workItem.LocalClonePath);
            DeleteDirectoryForce(workItem.LocalClonePath);
        }

        await _aufgabeService.AbschliessenAsync(workItemId, ct);
        await _protokollService.AddStatusUebergangAsync(workItemId, vonStatus, WorkItemStatus.Completed, ct);

        _logger.LogInformation("Aufgabe {AufgabeId} erfolgreich abgeschlossen.", workItemId);
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
    /// <param name="workItemId">ID der Aufgabe.</param>
    /// <param name="ct">Abbruch-Token.</param>
    /// <returns>Ergebnis der Startskript-Ausführung.</returns>
    public async Task<string> RepositoryStartskriptAusfuehrenAsync(Guid workItemId, CancellationToken ct = default)
    {
        if (_options.RepositoryStartskriptService is null)
        {
            return "Startskript-Dienst ist nicht konfiguriert.";
        }

        var workItem = await _aufgabeService.GetDetailAsync(workItemId, ct)
            ?? throw new InvalidOperationException($"Aufgabe {workItemId} nicht gefunden.");

        if (string.IsNullOrEmpty(workItem.LocalClonePath))
        {
            throw new InvalidOperationException($"Aufgabe {workItemId} hat keinen lokalen Klonpfad.");
        }

        var repository = await ResolveRepositoryAsync(workItem, workItem.GitRepository?.RepositoryUrl ?? string.Empty, ct);

        if (repository.StartConfiguration is null || !repository.StartConfiguration.Active)
        {
            return "Kein aktives Startskript konfiguriert.";
        }

        await _options.RepositoryStartskriptService.RunAsync(workItem.LocalClonePath, repository.StartConfiguration, ct);
        return "Startskript erfolgreich ausgeführt.";
    }

    private async Task RollbackStartAsync(Guid workItemId, CancellationToken ct, string? previousClone = null, WorkItemStatus? previousStatus = null)
    {
        var workItem = await _aufgabeService.GetByIdAsync(workItemId, ct);
        if (workItem is null)
        {
            return;
        }

        if (string.IsNullOrEmpty(previousClone) && !string.IsNullOrEmpty(workItem.LocalClonePath) && Directory.Exists(workItem.LocalClonePath))
        {
            DeleteDirectoryForce(workItem.LocalClonePath);
        }

        await _aufgabeService.StartZuruecksetzenAsync(workItemId, ct);
        if (previousStatus is not null && previousStatus != WorkItemStatus.New)
            await _aufgabeService.SetStatusAsync(workItemId, previousStatus.Value, ct);
    }

    private async Task<IGitPlugin> ResolvePluginAsync(
        GitRepository repository,
        string? selectedScmPluginPrefix,
        Guid workItemId,
        CancellationToken ct)
    {
        var resolvedPluginPrefix = !string.IsNullOrWhiteSpace(repository.PluginType)
            ? repository.PluginType
            : selectedScmPluginPrefix;
        if (string.IsNullOrWhiteSpace(resolvedPluginPrefix))
        {
            _logger.LogWarning(
                "Aufgabe {AufgabeId}: Kein SCM-Plugin-Typ am Repository konfiguriert und kein SCM-Plugin-Prefix übergeben — erster verfügbarer SCM-Plugin wird verwendet.",
                workItemId);
        }
        return await _pluginSelectionService.ResolveSourceCodeManagementPluginAsync(resolvedPluginPrefix, ct);
    }

    private async Task<string> PrepareCloneDirectoryAsync(
        IGitPlugin gitPlugin,
        string repositoryUrl,
        Guid workItemId,
        CancellationToken ct,
        bool preserveExistingDirectory = false)
    {
        var workdirResult = await _arbeitsverzeichnisResolver.ResolveAsync(ct);
        var lokalerKlonPfad = Path.Combine(workdirResult.ResolvedPath, KlonBasisVerzeichnis, workItemId.ToString());

        if (workdirResult.UsedFallback)
        {
            await _protokollService.AddEintragAsync(
                workItemId,
                LogEntryType.GitAction,
                $"Arbeitsverzeichnis-Fallback aktiv ({workdirResult.ReasonCode}). Verwende {workdirResult.ResolvedPath}.",
                ct: ct);
        }

        if (Directory.Exists(lokalerKlonPfad))
        {
            if (preserveExistingDirectory)
                throw new InvalidOperationException("Der Zielarbeitsbereich ist bereits belegt. Bitte den vorhandenen Klon regulär zuordnen.");
            _logger.LogInformation("Zielverzeichnis '{KlonPfad}' existiert bereits, wird gelöscht.", lokalerKlonPfad);
            DeleteDirectoryForce(lokalerKlonPfad);
        }

        _logger.LogInformation("Repository '{RepositoryUrl}' nach '{KlonPfad}' klonen.", repositoryUrl, lokalerKlonPfad);
        await gitPlugin.CloneRepositoryAsync(repositoryUrl, lokalerKlonPfad, ct);
        return lokalerKlonPfad;
    }

    private async Task<(string BranchName, bool NutzeExistierendenBranch, string? BaseBranchName)> SetupBranchAsync(
        IGitPlugin gitPlugin,
        string repositoryUrl,
        string lokalerKlonPfad,
        string? basisBranchName,
        string? defaultSourceBranchName,
        WorkItem workItem,
        CancellationToken ct)
    {
        var reviewSources = workItem.PullRequests
            .Where(p => p.Role == PullRequestReferenzRolle.ReviewSource)
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

        return await CreateNewTaskBranchAsync(gitPlugin, repositoryUrl, lokalerKlonPfad, defaultSourceBranchName, workItem, defaultBranch, ct);
    }

    private async Task<(string BranchName, bool NutzeExistierendenBranch, string? BasisBranch)> CheckoutReviewSourceAsync(
        IGitPlugin gitPlugin,
        string repositoryUrl,
        string lokalerKlonPfad,
        PullRequestReference reviewSource,
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
        WorkItem workItem,
        string? cachedDefaultBranch,
        CancellationToken ct)
    {
        var branchName = ErstelleTaskBranchName(workItem);
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
        Guid workItemId,
        WorkItem workItem,
        GitRepository repository,
        string lokalerKlonPfad,
        string branchName,
        bool nutzeExistierendenBranch,
        string? basisBranch,
        CancellationToken ct)
    {
        var initialisierungsskriptHinweis = await RunInitialisierungsskriptAsync(workItemId, repository, lokalerKlonPfad, ct);
        var startskriptHinweis = await RunStartskriptAsync(workItemId, repository, lokalerKlonPfad, ct);

        await CreateIssueFileAsync(lokalerKlonPfad, workItem, branchName, repository.StartConfiguration, ct);
        await UpdateGitignoreAsync(lokalerKlonPfad, repository.StartConfiguration, ct);

        await _aufgabeService.StartenAsync(workItemId, branchName, lokalerKlonPfad, basisBranch, ct);

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

        await _protokollService.AddEintragAsync(workItemId, LogEntryType.GitAction, protokollNachricht, ct: ct);
    }

    private async Task<string?> RunInitialisierungsskriptAsync(Guid workItemId, GitRepository repository, string lokalerKlonPfad, CancellationToken ct)
    {
        if (repository.InitializationConfiguration is null || _options.RepositoryInitialisierungService is null)
        {
            return null;
        }

        return await RunOptionalRepositoryScriptAsync(
            workItemId,
            "Initialisierungsskript",
            () => _options.RepositoryInitialisierungService.RunAsync(lokalerKlonPfad, repository.InitializationConfiguration, ct),
            ct);
    }

    private async Task<string?> RunStartskriptAsync(Guid workItemId, GitRepository repository, string lokalerKlonPfad, CancellationToken ct)
    {
        if (repository.StartConfiguration is null || _options.RepositoryStartskriptService is null)
        {
            return null;
        }

        return await RunOptionalRepositoryScriptAsync(
            workItemId,
            "Startskript",
            () => _options.RepositoryStartskriptService.RunAsync(lokalerKlonPfad, repository.StartConfiguration, ct),
            ct);
    }

    private async Task<string?> RunOptionalRepositoryScriptAsync(Guid workItemId, string scriptLabel, Func<Task> runAsync, CancellationToken ct)
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
            _logger.LogWarning(ex, "Repository-{ScriptLabel} für Aufgabe {AufgabeId} ist fehlgeschlagen.", scriptLabel, workItemId);
            return $"Hinweis: Das Repository-{scriptLabel} konnte nicht ausgeführt werden ({ex.Message}).";
        }
    }

    private async Task<GitRepository> ResolveRepositoryAsync(WorkItem workItem, string repositoryUrl, CancellationToken ct)
    {
        if (workItem.GitRepository is not null)
        {
            return workItem.GitRepository;
        }

        if (_options.ProjektService is null)
        {
            return new GitRepository
            {
                Id = Guid.Empty,
                ProjectId = workItem.ProjectId,
                PluginType = string.Empty,
                RepositoryUrl = repositoryUrl,
                RepositoryName = repositoryUrl,
                Active = true
            };
        }

        var project = await _options.ProjektService.GetDetailAsync(workItem.ProjectId, ct)
            ?? throw new InvalidOperationException($"Projekt {workItem.ProjectId} nicht gefunden.");

        var repositories = project.Repositories
            .Where(r => r.Active)
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
            $"Aufgabe {workItem.Id}: kein eindeutiges Repository für den Startkontext ermittelbar.");
    }

    private async Task<WorkItem> GetAufgabeMitKlonPfadAsync(Guid workItemId, CancellationToken ct)
    {
        var workItem = await _aufgabeService.GetByIdAsync(workItemId, ct)
            ?? throw new InvalidOperationException($"Aufgabe {workItemId} nicht gefunden.");

        if (string.IsNullOrWhiteSpace(workItem.LocalClonePath))
            throw new InvalidOperationException($"Aufgabe {workItemId} hat keinen lokalen Klonpfad.");

        return workItem;
    }

    private static string ErstelleTaskBranchName(WorkItem workItem)
    {
        var titelSlug = ErstelleTitelSlug(workItem.Title);
        var issueNummer = workItem.IssueReference?.IssueNumber;

        return issueNummer is > 0
            ? $"task/issue-{issueNummer.Value}-{workItem.Id:N}-{titelSlug}"
            : $"task/{workItem.Id:N}-{titelSlug}";
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

    private async Task CreateIssueFileAsync(string lokalerKlonPfad, WorkItem workItem, string branchName, RepositoryStartConfiguration? startKonfiguration, CancellationToken ct)
    {
        try
        {
            var beschreibung = string.IsNullOrWhiteSpace(workItem.RequirementDescription)
                ? "[Keine Anforderungsbeschreibung verfügbar]"
                : workItem.RequirementDescription;

            var metaDaten = $"""
                # Aufgabe: {workItem.Title}

                **Aufgaben-ID:** {workItem.Id}
                **Branch:** {branchName}
                **Erstellt:** {workItem.CreatedAt:yyyy-MM-dd}
                """;

            var issueAbschnitt = workItem.IssueReference is { IssueNumber: > 0 } referenz
                ? $"""


                    ## Verknüpftes Issue

                    **Kennung:** #{referenz.IssueNumber}
                    **Titel:** {referenz.Title}
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
            _logger.LogInformation("issue.md für Aufgabe {AufgabeId} erfolgreich erstellt.", workItem.Id);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Fehler beim Erstellen von issue.md für Aufgabe {AufgabeId}.", workItem.Id);
        }
    }

    private async Task UpdateGitignoreAsync(string lokalerKlonPfad, RepositoryStartConfiguration? startKonfiguration, CancellationToken ct)
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

    private static string EnsureEffectiveWorkingDirectory(string lokalerKlonPfad, RepositoryStartConfiguration? startKonfiguration)
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

    private static void WirfWennPausiert(WorkItem workItem)
    {
        if (workItem.PausedUntilUtc is { } pausiertBis && workItem.PausedUntilUtc.IstAktuellPausiert(DateTimeOffset.UtcNow))
        {
            throw new InvalidOperationException(
                $"Die Aufgabe ist bis {pausiertBis.ToLocalTime():dd.MM.yyyy HH:mm:ss} pausiert.");
        }
    }
}
