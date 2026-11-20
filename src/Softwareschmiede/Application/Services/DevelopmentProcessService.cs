using Microsoft.Extensions.Logging;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Exceptions;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Infrastructure.Data;

namespace Softwareschmiede.Application.Services;

/// <summary>Optionale Abhängigkeiten für den EntwicklungsprozessService.</summary>
/// <param name="ProjectService">Optionaler Dienst zum Auflösen von Projekt-Repositories.</param>
/// <param name="RepositoryStartScriptService">Optionaler Dienst zum Ausführen von Repository-Startskripten.</param>
/// <param name="RepositoryInitializationService">Optionaler Dienst zum Ausführen von Repository-Initialisierungsskripten.</param>
/// <param name="AiExecutionService">Optionaler Dienst zum Starten der KI-CLI.</param>
/// <param name="GitOrchestrationService">
/// Optionaler Dienst zur Validierung des konfigurierten Arbeitsverzeichnisses direkt nach dem Git-Klon.
/// </param>
/// <param name="PromptScheduleDispatchService">Optionaler Dienst zum Zurückziehen geplanter zeitgesteuerter Prompts beim Aufgaben-Abschluss.</param>
/// <returns>Eine neue Instanz mit den angegebenen optionalen Abhängigkeiten.</returns>
public sealed record DevelopmentProcessServiceOptions(
    ProjectService? ProjectService = null,
    RepositoryStartScriptService? RepositoryStartScriptService = null,
    RepositoryInitializationService? RepositoryInitializationService = null,
    AiExecutionService? AiExecutionService = null,
    GitOrchestrationService? GitOrchestrationService = null,
    PromptScheduleDispatchService? PromptScheduleDispatchService = null);

/// <summary>
/// Koordiniert Git-Repository-Setup für Aufgaben und Rate-Limit-Marker-Erkennung.
/// </summary>
public sealed class DevelopmentProcessService
{
    private readonly WorkItemService _workItemService;
    private readonly LogService _logService;
    private readonly IGitPlugin _gitPlugin;
    private readonly PluginSelectionService _pluginSelectionService;
    private readonly IWorkingDirectoryResolver _workingDirectoryResolver;
    private readonly DevelopmentProcessServiceOptions _options;
    private readonly ILogger<DevelopmentProcessService> _logger;

    internal const string CloneBaseDirectory = "softwareschmiede";

    /// <inheritdoc cref="DevelopmentProcessService"/>
    public DevelopmentProcessService(
        WorkItemService workItemService,
        LogService logService,
        IGitPlugin gitPlugin,
        PluginSelectionService pluginSelectionService,
        IWorkingDirectoryResolver workingDirectoryResolver,
        ILogger<DevelopmentProcessService> logger)
        : this(
            workItemService,
            logService,
            gitPlugin,
            pluginSelectionService,
            workingDirectoryResolver,
            new DevelopmentProcessServiceOptions(),
            logger)
    {
    }

    /// <inheritdoc cref="DevelopmentProcessService"/>
    public DevelopmentProcessService(
        WorkItemService workItemService,
        LogService logService,
        IGitPlugin gitPlugin,
        PluginSelectionService pluginSelectionService,
        IWorkingDirectoryResolver workingDirectoryResolver,
        DevelopmentProcessServiceOptions options,
        ILogger<DevelopmentProcessService> logger)
    {
        _workItemService = workItemService;
        _logService = logService;
        _gitPlugin = gitPlugin;
        _pluginSelectionService = pluginSelectionService;
        _workingDirectoryResolver = workingDirectoryResolver;
        _options = options;
        _logger = logger;
    }

    /// <summary>
    /// Richtet das Git-Repository für eine Aufgabe ein: Klon, Branch, optionales Startskript.
    /// Setzt den Status auf <see cref="WorkItemStatus.Started"/>.
    /// </summary>
    /// <param name="workItemId">ID der zu startenden Aufgabe.</param>
    /// <param name="repositoryUrl">URL des zu klonenden Repositories.</param>
    /// <param name="baseBranchName">Optionaler Basis-Branch; wird ein neuer Task-Branch angelegt, wenn er dem Default-Branch entspricht.</param>
    /// <param name="selectedScmPluginPrefix">Optionaler Prefix des zu verwendenden SCM-Plugins.</param>
    /// <param name="ct">Abbruch-Token.</param>
    internal Task StartProcessAsync(
        Guid workItemId,
        string repositoryUrl,
        string? baseBranchName = null,
        string? selectedScmPluginPrefix = null,
        CancellationToken ct = default)
        => ScmTaskCoordinator.Shared.RunAsync(workItemId,
            () => StartProcessCoordinatedAsync(workItemId, repositoryUrl, baseBranchName, selectedScmPluginPrefix, ct), ct);

    private async Task StartProcessCoordinatedAsync(Guid workItemId, string repositoryUrl,
        string? baseBranchName, string? selectedScmPluginPrefix, CancellationToken ct)
    {
        _logger.LogInformation("Repository-Setup für Aufgabe {AufgabeId} starten.", workItemId);

        var workItem = await _workItemService.GetDetailAsync(workItemId, ct)
            ?? throw new InvalidOperationException($"Aufgabe {workItemId} nicht gefunden.");
        ThrowIfPaused(workItem);
        if (!string.IsNullOrWhiteSpace(workItem.LocalClonePath)) return;
        await StartProcessCoreAsync(workItemId, workItem, repositoryUrl, baseBranchName, selectedScmPluginPrefix, ct);

        _logger.LogInformation("Repository-Setup für Aufgabe {AufgabeId} abgeschlossen.", workItemId);
    }

    /// <summary>
    /// Kombiniert Repository-Setup (Klon, Branch) und CLI-Start in einem Schritt.
    /// Setzt den Status direkt auf <see cref="WorkItemStatus.Started"/> und startet anschließend die CLI mit dem gewählten Plugin.
    /// Im Fehlerfall wird der Status zurückgesetzt und das Klon-Verzeichnis gelöscht.
    /// </summary>
    /// <param name="workItemId">ID der zu startenden Aufgabe.</param>
    /// <param name="repositoryUrl">URL des zu klonenden Repositories.</param>
    /// <param name="baseBranchName">Optionaler Basis-Branch.</param>
    /// <param name="aiPluginPrefix">Optionaler Prefix des zu verwendenden KI-Plugins.</param>
    /// <param name="ct">Abbruch-Token.</param>
    /// <param name="strictPluginSelection">Verhindert Ersatzplugins bei automatischer SCM-Zustellung.</param>
    /// <param name="optionalParameters">Explizite Startparameter des gewählten Plugins.</param>
    public Task StartProcessAndCliAsync(
        Guid workItemId,
        string repositoryUrl,
        string? baseBranchName,
        string? aiPluginPrefix,
        CancellationToken ct = default,
        bool strictPluginSelection = false,
        string? optionalParameters = null)
        => ScmTaskCoordinator.Shared.RunAsync(workItemId,
            () => ProcessAndCliCoordinatedAsync(workItemId, repositoryUrl, baseBranchName, aiPluginPrefix, ct, strictPluginSelection, optionalParameters), ct);

    private async Task ProcessAndCliCoordinatedAsync(Guid workItemId, string repositoryUrl,
        string? baseBranchName, string? aiPluginPrefix, CancellationToken ct, bool strictPluginSelection, string? optionalParameters)
    {
        if (_options.AiExecutionService is null)
        {
            throw new InvalidOperationException("AiExecutionService ist nicht konfiguriert.");
        }

        // Der Pausen-Guard steht bewusst VOR dem try-Block: Ein InvalidOperationException-Wurf
        // innerhalb von try würde den Rollback-Pfad auslösen (Klon löschen + Status-Reset), obwohl
        // die Aufgabe bei aktiver Pause gar nicht angefasst werden soll.
        var prerequisiteWorkItem = await _workItemService.GetByIdAsync(workItemId, ct)
            ?? throw new InvalidOperationException($"Aufgabe {workItemId} nicht gefunden.");
        ThrowIfPaused(prerequisiteWorkItem);
        if (prerequisiteWorkItem.Status is WorkItemStatus.Completed or WorkItemStatus.Archived)
            throw new InvalidOperationException("Beendete oder archivierte Aufgaben können nicht gestartet werden.");
        // Ein konkurrierender automatischer/manueller Start kann abgeschlossen worden sein, während
        // wir auf das Task-Gate warteten. Diesen Arbeitsbereich wiederverwenden, bevor Setup oder
        // dessen Rollback-Bereich betreten wird.
        if (!string.IsNullOrWhiteSpace(prerequisiteWorkItem.LocalClonePath))
        {
            await RestartCliAsync(workItemId, aiPluginPrefix, optionalParameters, ct, strictPluginSelection);
            return;
        }
        var previousClone = prerequisiteWorkItem.LocalClonePath;
        var previousStatus = prerequisiteWorkItem.Status;
        var strictPlugin = strictPluginSelection
            ? await _pluginSelectionService.ResolveDevelopmentAutomationPluginStrictAsync(aiPluginPrefix!, ct)
            : null;

        try
        {
            var workItem = await _workItemService.GetDetailAsync(workItemId, ct)
                ?? throw new InvalidOperationException($"Aufgabe {workItemId} nicht gefunden.");
            var (repository, gitPlugin) = await StartProcessCoreAsync(workItemId, workItem, repositoryUrl, baseBranchName, null, ct, strictPluginSelection);

            workItem = await _workItemService.GetByIdAsync(workItemId, ct)
                ?? throw new InvalidOperationException($"Aufgabe {workItemId} nicht gefunden.");

            if (string.IsNullOrEmpty(workItem.LocalClonePath))
            {
                throw new InvalidOperationException($"Aufgabe {workItemId} hat keinen lokalen Klonpfad.");
            }

            var aiPlugin = strictPlugin is not null
                ? await _pluginSelectionService.ResolveDevelopmentAutomationPluginStrictAsync(strictPlugin.PluginPrefix, ct)
                : await _pluginSelectionService.ResolveDevelopmentAutomationPluginAsync(aiPluginPrefix, ct);
            await _workItemService.UpdateAsync(
                workItemId,
                workItem.Title,
                workItem.RequirementDescription,
                aiPlugin.PluginPrefix,
                ct);

            await _options.AiExecutionService.StartTerminalSessionAsync(
                workItemId, aiPlugin, workItem.LocalClonePath, optionalParameters, ct, repository.StartConfiguration, gitPlugin,
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

    private async Task<(GitRepository Repository, IGitPlugin GitPlugin)> StartProcessCoreAsync(
        Guid workItemId,
        WorkItem workItem,
        string repositoryUrl,
        string? baseBranchName,
        string? selectedScmPluginPrefix,
        CancellationToken ct,
        bool preserveExistingDirectory = false)
    {
        var repository = await ResolveRepositoryAsync(workItem, repositoryUrl, ct);
        var gitPlugin = await ResolvePluginAsync(repository, selectedScmPluginPrefix, workItemId, ct);

        await ValidateBaseBranchExistsAsync(repository, gitPlugin, ct);

        var localClonePath = await PrepareCloneDirectoryAsync(gitPlugin, repository.RepositoryUrl, workItemId, ct, preserveExistingDirectory);

        if (_options.GitOrchestrationService is not null)
        {
            await _options.GitOrchestrationService.ValidateWorkingDirectoryAfterCloneAsync(localClonePath, repository.StartConfiguration, gitPlugin);
        }

        var (branchName, useExistingBranch, baseBranch) = await SetupBranchAsync(gitPlugin, repository.RepositoryUrl, localClonePath, baseBranchName, repository.DefaultSourceBranchName, workItem, ct);
        await FinalizeStartAsync(workItemId, workItem, repository, localClonePath, branchName, useExistingBranch, baseBranch, ct);

        return (repository, gitPlugin);
    }

    /// <summary>Startet die KI-CLI erneut im bereits vorbereiteten Klon einer Aufgabe.</summary>
    public Task RestartCliAsync(
        Guid workItemId,
        string? aiPluginPrefix,
        string? optionalParameters,
        CancellationToken ct = default,
        bool strictPluginSelection = false)
        => ScmTaskCoordinator.Shared.RunAsync(workItemId,
            () => RestartCliCoordinatedAsync(workItemId, aiPluginPrefix, optionalParameters, ct, strictPluginSelection), ct);

    private async Task RestartCliCoordinatedAsync(Guid workItemId, string? aiPluginPrefix,
        string? optionalParameters, CancellationToken ct, bool strictPluginSelection)
    {
        if (_options.AiExecutionService is null)
        {
            throw new InvalidOperationException("AiExecutionService ist nicht konfiguriert.");
        }

        var workItem = await _workItemService.GetDetailAsync(workItemId, ct)
            ?? throw new InvalidOperationException($"Aufgabe {workItemId} nicht gefunden.");

        if (workItem.Status is WorkItemStatus.Completed or WorkItemStatus.Archived)
        {
            throw new InvalidOperationException("Beendete oder archivierte Aufgaben können nicht gestartet werden.");
        }

        // Deckt auch den Plugin-Wechsel-Pfad (PluginWechselAsync → CliNeustartenAsync) ab.
        ThrowIfPaused(workItem);

        var current = _options.AiExecutionService.GetTerminalSession(workItemId);
        if (current is not null)
        {
            try
            {
                if (current.ExitCode is null && current.Failure is null && !current.Process.HasExited)
                {
                    // Lebende Session wird wiederverwendet: kein Gestartet-Ereignis — stattdessen das
                    // dedizierte CliRunActive-Signal, damit die Laufdaten-Persistenz nachgezogen wird.
                    _options.AiExecutionService.NotifyRunActive(workItemId);
                    return;
                }
            }
            catch (InvalidOperationException) { }
        }

        if (string.IsNullOrWhiteSpace(workItem.LocalClonePath))
        {
            throw new InvalidOperationException($"Aufgabe {workItemId} hat keinen lokalen Klonpfad.");
        }

        var aiPlugin = strictPluginSelection
            ? await _pluginSelectionService.ResolveDevelopmentAutomationPluginStrictAsync(aiPluginPrefix!, ct)
            : await _pluginSelectionService.ResolveDevelopmentAutomationPluginAsync(aiPluginPrefix, ct);
        await _workItemService.UpdateAsync(
            workItemId,
            workItem.Title,
            workItem.RequirementDescription,
            aiPlugin.PluginPrefix,
            ct);

        var repository = await ResolveRepositoryAsync(workItem, workItem.GitRepository?.RepositoryUrl ?? string.Empty, ct);
        var gitPlugin = await ResolvePluginAsync(repository, null, workItemId, ct);

        await _options.AiExecutionService.StartTerminalSessionAsync(
            workItemId,
            aiPlugin,
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
    public async Task ExecuteCommitAsync(Guid workItemId, string message, CancellationToken ct = default)
    {
        _logger.LogInformation("Commit für Aufgabe {AufgabeId} durchführen.", workItemId);

        var workItem = await GetWorkItemWithClonePathAsync(workItemId, ct);

        await _gitPlugin.CommitAsync(workItem.LocalClonePath!, message, ct);

        await _logService.AddEntryAsync(
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
    public async Task ExecuteResetAsync(Guid workItemId, string resetType, string? targetRef, CancellationToken ct = default)
    {
        _logger.LogInformation("Reset ({ResetType}) für Aufgabe {AufgabeId} durchführen.", resetType, workItemId);

        var workItem = await GetWorkItemWithClonePathAsync(workItemId, ct);

        await _gitPlugin.ResetAsync(workItem.LocalClonePath!, resetType, targetRef, ct);

        var target = targetRef ?? "HEAD";
        await _logService.AddEntryAsync(
            workItemId,
            LogEntryType.GitAction,
            $"Reset ({resetType}) auf {target}",
            ct: ct);
    }

    /// <summary>Pusht den Branch auf den Remote.</summary>
    /// <param name="workItemId">ID der Aufgabe.</param>
    /// <param name="ct">Abbruch-Token.</param>
    public async Task ExecutePushAsync(Guid workItemId, CancellationToken ct = default)
    {
        _logger.LogInformation("Push für Aufgabe {AufgabeId} durchführen.", workItemId);

        var workItem = await GetWorkItemWithClonePathAsync(workItemId, ct);

        if (string.IsNullOrEmpty(workItem.BranchName))
            throw new InvalidOperationException($"Aufgabe {workItemId} hat keinen Branch-Namen.");

        await _gitPlugin.PushBranchAsync(workItem.LocalClonePath!, workItem.BranchName, ct);

        await _logService.AddEntryAsync(
            workItemId,
            LogEntryType.GitAction,
            $"Push: Branch '{workItem.BranchName}' gepusht.",
            ct: ct);
    }

    /// <summary>Holt Änderungen vom Remote.</summary>
    /// <param name="workItemId">ID der Aufgabe.</param>
    /// <param name="ct">Abbruch-Token.</param>
    public async Task ExecutePullAsync(Guid workItemId, CancellationToken ct = default)
    {
        _logger.LogInformation("Pull für Aufgabe {AufgabeId} durchführen.", workItemId);

        var workItem = await GetWorkItemWithClonePathAsync(workItemId, ct);

        await _gitPlugin.PullAsync(workItem.LocalClonePath!, ct);

        await _logService.AddEntryAsync(
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
    public async Task<PullRequest> CreatePullRequestAsync(
        Guid workItemId,
        string repositoryId,
        string title,
        string body,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Pull Request für Aufgabe {AufgabeId} erstellen.", workItemId);

        var workItem = await _workItemService.GetDetailAsync(workItemId, ct)
            ?? throw new InvalidOperationException($"Aufgabe {workItemId} nicht gefunden.");

        if (string.IsNullOrEmpty(workItem.BranchName))
            throw new InvalidOperationException($"Aufgabe {workItemId} hat keinen Branch-Namen.");

        var prBody = PullRequestBodyBuilder.Build(workItem, body);
        var issueNumber = workItem.IssueReference?.IssueNumber;
        var pullRequest = await _gitPlugin.CreatePullRequestAsync(repositoryId, workItem.BranchName, null, title, prBody, ct);

        var issueLogSuffix = issueNumber is > 0
            ? $" (Issue #{issueNumber.Value}, Auto-Close aktiv)"
            : string.Empty;

        await _logService.AddEntryAsync(
            workItemId,
            LogEntryType.GitAction,
            $"Pull Request erstellt: #{pullRequest.Number} – {pullRequest.Title} ({pullRequest.Url}){issueLogSuffix}",
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
    public Task CompleteAsync(Guid workItemId, CancellationToken ct = default)
        => ScmTaskCoordinator.Shared.RunAsync(workItemId,
            () => CompleteCoordinatedAsync(workItemId, ct), ct);

    private async Task CompleteCoordinatedAsync(Guid workItemId, CancellationToken ct)
    {
        _logger.LogInformation("Aufgabe {AufgabeId} abschließen.", workItemId);

        if (_options.AiExecutionService is not null)
        {
            await _options.AiExecutionService.StopCliAsync(workItemId, ct);
        }

        if (_options.PromptScheduleDispatchService is not null)
        {
            await _options.PromptScheduleDispatchService.CancelScheduledPromptAsync(workItemId);
        }

        var workItem = await _workItemService.GetDetailAsync(workItemId, ct)
            ?? throw new InvalidOperationException($"Aufgabe {workItemId} nicht gefunden.");

        if (!await _workItemService.CanCompleteTaskAsync(workItemId, ct))
        {
            var openTodoCount = workItem.Todos.Count(t => t.IsOpen);
            throw new InvalidOperationException(
                string.Format(WorkItemService.OpenTodosErrorMessageFormat, openTodoCount));
        }

        var fromStatus = workItem.Status;

        if (!string.IsNullOrEmpty(workItem.LocalClonePath) && Directory.Exists(workItem.LocalClonePath))
        {
            _logger.LogInformation("Klon-Verzeichnis '{KlonPfad}' löschen.", workItem.LocalClonePath);
            DeleteDirectoryForce(workItem.LocalClonePath);
        }

        await _workItemService.CompleteAsync(workItemId, ct);
        await _logService.AddStatusTransitionAsync(workItemId, fromStatus, WorkItemStatus.Completed, ct);

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
    public async Task<string> ExecuteRepositoryStartScriptAsync(Guid workItemId, CancellationToken ct = default)
    {
        if (_options.RepositoryStartScriptService is null)
        {
            return "Startskript-Dienst ist nicht konfiguriert.";
        }

        var workItem = await _workItemService.GetDetailAsync(workItemId, ct)
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

        await _options.RepositoryStartScriptService.RunAsync(workItem.LocalClonePath, repository.StartConfiguration, ct);
        return "Startskript erfolgreich ausgeführt.";
    }

    private async Task RollbackStartAsync(Guid workItemId, CancellationToken ct, string? previousClone = null, WorkItemStatus? previousStatus = null)
    {
        var workItem = await _workItemService.GetByIdAsync(workItemId, ct);
        if (workItem is null)
        {
            return;
        }

        if (string.IsNullOrEmpty(previousClone) && !string.IsNullOrEmpty(workItem.LocalClonePath) && Directory.Exists(workItem.LocalClonePath))
        {
            DeleteDirectoryForce(workItem.LocalClonePath);
        }

        await _workItemService.ResetStartAsync(workItemId, ct);
        if (previousStatus is not null && previousStatus != WorkItemStatus.New)
            await _workItemService.SetStatusUncheckedAsync(workItemId, previousStatus.Value, ct);
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
        var workdirResult = await _workingDirectoryResolver.ResolveAsync(ct);
        var localClonePath = Path.Combine(workdirResult.ResolvedPath, CloneBaseDirectory, workItemId.ToString());

        if (workdirResult.UsedFallback)
        {
            await _logService.AddEntryAsync(
                workItemId,
                LogEntryType.GitAction,
                $"Arbeitsverzeichnis-Fallback aktiv ({workdirResult.ReasonCode}). Verwende {workdirResult.ResolvedPath}.",
                ct: ct);
        }

        if (Directory.Exists(localClonePath))
        {
            if (preserveExistingDirectory)
                throw new InvalidOperationException("Der Zielarbeitsbereich ist bereits belegt. Bitte den vorhandenen Klon regulär zuordnen.");
            _logger.LogInformation("Zielverzeichnis '{KlonPfad}' existiert bereits, wird gelöscht.", localClonePath);
            DeleteDirectoryForce(localClonePath);
        }

        _logger.LogInformation("Repository '{RepositoryUrl}' nach '{KlonPfad}' klonen.", repositoryUrl, localClonePath);
        await gitPlugin.CloneRepositoryAsync(repositoryUrl, localClonePath, ct);
        return localClonePath;
    }

    private async Task<(string BranchName, bool UseExistingBranch, string? BaseBranchName)> SetupBranchAsync(
        IGitPlugin gitPlugin,
        string repositoryUrl,
        string localClonePath,
        string? baseBranchName,
        string? defaultSourceBranchName,
        WorkItem workItem,
        CancellationToken ct)
    {
        var reviewSources = workItem.PullRequests
            .Where(p => p.Role == PullRequestReferenceRole.ReviewSource)
            .ToList();
        if (reviewSources.Count > 1)
        {
            throw new InvalidOperationException("Die Aufgabe besitzt mehrere Pull-Request-Review-Quellen.");
        }

        if (reviewSources.Count == 1)
        {
            return await CheckoutReviewSourceAsync(gitPlugin, repositoryUrl, localClonePath, reviewSources[0], ct);
        }

        string? defaultBranch = null;
        var useExistingBranch = false;
        if (!string.IsNullOrEmpty(baseBranchName))
        {
            defaultBranch = await gitPlugin.GetDefaultBranchAsync(repositoryUrl, ct);
            useExistingBranch = !string.Equals(baseBranchName, defaultBranch, StringComparison.OrdinalIgnoreCase);
        }

        if (useExistingBranch)
        {
            return await CheckoutExistingBranchAsync(gitPlugin, localClonePath, baseBranchName!, ct);
        }

        return await CreateNewTaskBranchAsync(gitPlugin, repositoryUrl, localClonePath, defaultSourceBranchName, workItem, defaultBranch, ct);
    }

    private async Task<(string BranchName, bool UseExistingBranch, string? BaseBranch)> CheckoutReviewSourceAsync(
        IGitPlugin gitPlugin,
        string repositoryUrl,
        string localClonePath,
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
        await gitPlugin.CheckoutPullRequestSourceAsync(localClonePath, checkoutSpec, ct);
        return (reviewSource.SourceBranch, true, null);
    }

    private async Task<(string BranchName, bool UseExistingBranch, string? BaseBranch)> CheckoutExistingBranchAsync(
        IGitPlugin gitPlugin,
        string localClonePath,
        string baseBranchName,
        CancellationToken ct)
    {
        _logger.LogInformation("Wechsle zu vorhandenem Branch '{BaseBranch}'.", baseBranchName);
        await gitPlugin.CheckoutRemoteBranchAsync(localClonePath, baseBranchName, ct);
        return (baseBranchName, true, null);
    }

    private async Task<(string BranchName, bool UseExistingBranch, string? BaseBranch)> CreateNewTaskBranchAsync(
        IGitPlugin gitPlugin,
        string repositoryUrl,
        string localClonePath,
        string? defaultSourceBranchName,
        WorkItem workItem,
        string? cachedDefaultBranch,
        CancellationToken ct)
    {
        var branchName = CreateTaskBranchName(workItem);
        string? baseBranch = null;

        if (!string.IsNullOrEmpty(defaultSourceBranchName))
        {
            var defaultBranch = cachedDefaultBranch ?? await gitPlugin.GetDefaultBranchAsync(repositoryUrl, ct);
            if (!string.Equals(defaultSourceBranchName, defaultBranch, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("Basis-Branch '{BaseBranch}' lokal nachziehen.", defaultSourceBranchName);
                await gitPlugin.CheckoutRemoteBranchAsync(localClonePath, defaultSourceBranchName, ct);
            }

            _logger.LogInformation("Branch '{BranchName}' vom Basis-Branch '{BaseBranch}' anlegen.", branchName, defaultSourceBranchName);
            await gitPlugin.CreateBranchAsync(localClonePath, branchName, defaultSourceBranchName, ct);
            baseBranch = defaultSourceBranchName;
        }
        else
        {
            _logger.LogInformation("Branch '{BranchName}' anlegen.", branchName);
            await gitPlugin.CreateBranchAsync(localClonePath, branchName, null, ct);
            baseBranch = null;
        }

        return (branchName, false, baseBranch);
    }

    private async Task FinalizeStartAsync(
        Guid workItemId,
        WorkItem workItem,
        GitRepository repository,
        string localClonePath,
        string branchName,
        bool useExistingBranch,
        string? baseBranch,
        CancellationToken ct)
    {
        var initializationScriptHint = await RunInitializationScriptAsync(workItemId, repository, localClonePath, ct);
        var startScriptHint = await RunStartScriptAsync(workItemId, repository, localClonePath, ct);

        await CreateIssueFileAsync(localClonePath, workItem, branchName, repository.StartConfiguration, ct);
        await UpdateGitignoreAsync(localClonePath, repository.StartConfiguration, ct);

        await _workItemService.StartAsync(workItemId, branchName, localClonePath, baseBranch, ct);

        var logMessage = useExistingBranch
            ? $"Klon angelegt, vorhandener Branch ausgecheckt: {branchName} in {localClonePath}"
            : $"Klon und Branch angelegt: {branchName} in {localClonePath}";
        if (!string.IsNullOrWhiteSpace(initializationScriptHint))
        {
            logMessage = $"{logMessage}\n{initializationScriptHint}";
        }
        if (!string.IsNullOrWhiteSpace(startScriptHint))
        {
            logMessage = $"{logMessage}\n{startScriptHint}";
        }

        await _logService.AddEntryAsync(workItemId, LogEntryType.GitAction, logMessage, ct: ct);
    }

    private async Task<string?> RunInitializationScriptAsync(Guid workItemId, GitRepository repository, string localClonePath, CancellationToken ct)
    {
        if (repository.InitializationConfiguration is null || _options.RepositoryInitializationService is null)
        {
            return null;
        }

        return await RunOptionalRepositoryScriptAsync(
            workItemId,
            "Initialisierungsskript",
            () => _options.RepositoryInitializationService.RunAsync(localClonePath, repository.InitializationConfiguration, ct),
            ct);
    }

    private async Task<string?> RunStartScriptAsync(Guid workItemId, GitRepository repository, string localClonePath, CancellationToken ct)
    {
        if (repository.StartConfiguration is null || _options.RepositoryStartScriptService is null)
        {
            return null;
        }

        return await RunOptionalRepositoryScriptAsync(
            workItemId,
            "Startskript",
            () => _options.RepositoryStartScriptService.RunAsync(localClonePath, repository.StartConfiguration, ct),
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

        if (_options.ProjectService is null)
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

        var project = await _options.ProjectService.GetDetailAsync(workItem.ProjectId, ct)
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

    private async Task<WorkItem> GetWorkItemWithClonePathAsync(Guid workItemId, CancellationToken ct)
    {
        var workItem = await _workItemService.GetByIdAsync(workItemId, ct)
            ?? throw new InvalidOperationException($"Aufgabe {workItemId} nicht gefunden.");

        if (string.IsNullOrWhiteSpace(workItem.LocalClonePath))
            throw new InvalidOperationException($"Aufgabe {workItemId} hat keinen lokalen Klonpfad.");

        return workItem;
    }

    private static string CreateTaskBranchName(WorkItem workItem)
    {
        var titleSlug = CreateTitleSlug(workItem.Title);
        var issueNumber = workItem.IssueReference?.IssueNumber;

        return issueNumber is > 0
            ? $"task/issue-{issueNumber.Value}-{workItem.Id:N}-{titleSlug}"
            : $"task/{workItem.Id:N}-{titleSlug}";
    }

    private static string CreateTitleSlug(string title)
    {
        var slug = title.ToLowerInvariant()
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

    private async Task CreateIssueFileAsync(string localClonePath, WorkItem workItem, string branchName, RepositoryStartConfiguration? startConfiguration, CancellationToken ct)
    {
        try
        {
            var description = string.IsNullOrWhiteSpace(workItem.RequirementDescription)
                ? "[Keine Anforderungsbeschreibung verfügbar]"
                : workItem.RequirementDescription;

            var metaDaten = $"""
                # Aufgabe: {workItem.Title}

                **Aufgaben-ID:** {workItem.Id}
                **Branch:** {branchName}
                **Erstellt:** {workItem.CreatedAt:yyyy-MM-dd}
                """;

            var issueSection = workItem.IssueReference is { IssueNumber: > 0 } reference
                ? $"""


                    ## Verknüpftes Issue

                    **Kennung:** #{reference.IssueNumber}
                    **Titel:** {reference.Title}
                    """
                : string.Empty;

            var requirementSection = $"""


                ## Anforderung

                {description}
                """;

            var content = metaDaten + issueSection + requirementSection;

            var effectiveDirectory = EnsureEffectiveWorkingDirectory(localClonePath, startConfiguration);

            var issueFilePath = Path.Combine(effectiveDirectory, "issue.md");
            await File.WriteAllTextAsync(issueFilePath, content, ct);
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

    private async Task UpdateGitignoreAsync(string localClonePath, RepositoryStartConfiguration? startConfiguration, CancellationToken ct)
    {
        var effectiveDirectory = localClonePath;
        try
        {
            effectiveDirectory = EnsureEffectiveWorkingDirectory(localClonePath, startConfiguration);

            var gitignorePath = Path.Combine(effectiveDirectory, ".gitignore");
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
            _logger.LogInformation(".gitignore für '{KlonPfad}' aktualisiert: 'issue.md' eingetragen.", effectiveDirectory);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Fehler beim Aktualisieren von .gitignore in '{KlonPfad}'.", effectiveDirectory);
        }
    }

    private static string EnsureEffectiveWorkingDirectory(string localClonePath, RepositoryStartConfiguration? startConfiguration)
    {
        var effectiveDirectory = WorkingDirectoryResolver.ResolveEffectiveWorkingDirectory(
            localClonePath, startConfiguration?.WorkingDirectoryRelativePath);
        Directory.CreateDirectory(effectiveDirectory);
        return effectiveDirectory;
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

    private static void ThrowIfPaused(WorkItem workItem)
    {
        if (workItem.PausedUntilUtc is { } pausedUntil && workItem.PausedUntilUtc.IsCurrentlyPaused(DateTimeOffset.UtcNow))
        {
            throw new InvalidOperationException(
                $"Die Aufgabe ist bis {pausedUntil.ToLocalTime():dd.MM.yyyy HH:mm:ss} pausiert.");
        }
    }
}
