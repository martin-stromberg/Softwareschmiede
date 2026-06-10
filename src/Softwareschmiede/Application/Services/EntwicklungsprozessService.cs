using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Softwareschmiede.Domain.Abstractions;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Application.Services;

/// <summary>
/// Orchestriert den KI-gestützten Entwicklungsprozess:
/// Klon anlegen → Branch anlegen → Agentenpaket deployen → KI starten → Protokoll führen.
/// </summary>
public sealed class EntwicklungsprozessService
{
    private readonly AufgabeService _aufgabeService;
    private readonly ProtokollService _protokollService;
    private readonly ProjektService? _projektService;
    private readonly IGitPlugin _gitPlugin;
    private readonly PluginSelectionService _pluginSelectionService;
    private readonly IAgentPackageService _agentPackageService;
    private readonly IArbeitsverzeichnisResolver _arbeitsverzeichnisResolver;
    private readonly RepositoryStartskriptService? _repositoryStartskriptService;
    private readonly KiAufgabenBenachrichtigungsHub? _benachrichtigungsHub;
    private readonly IConfiguration _configuration;
    private readonly ILogger<EntwicklungsprozessService> _logger;
    private const int DefaultContextCompressionSoftLimit = 12_000;
    private const int DefaultContextCompressionHardLimit = 20_000;
    private const string RateLimitSuggestionMarker = "[[SOFTWARESCHMIEDE_RATE_LIMIT]]";

    /// <inheritdoc cref="EntwicklungsprozessService"/>
    public EntwicklungsprozessService(
        AufgabeService aufgabeService,
        ProtokollService protokollService,
        IGitPlugin gitPlugin,
        PluginSelectionService pluginSelectionService,
        IAgentPackageService agentPackageService,
        IArbeitsverzeichnisResolver arbeitsverzeichnisResolver,
        IConfiguration configuration,
        ILogger<EntwicklungsprozessService> logger)
        : this(
            aufgabeService,
            protokollService,
            null,
            gitPlugin,
            pluginSelectionService,
            agentPackageService,
            arbeitsverzeichnisResolver,
            null,
            null,
            configuration,
            logger)
    {
    }

    /// <inheritdoc cref="EntwicklungsprozessService"/>
    public EntwicklungsprozessService(
        AufgabeService aufgabeService,
        ProtokollService protokollService,
        IGitPlugin gitPlugin,
        PluginSelectionService pluginSelectionService,
        IAgentPackageService agentPackageService,
        IArbeitsverzeichnisResolver arbeitsverzeichnisResolver,
        KiAufgabenBenachrichtigungsHub benachrichtigungsHub,
        IConfiguration configuration,
        ILogger<EntwicklungsprozessService> logger)
        : this(
            aufgabeService,
            protokollService,
            null,
            gitPlugin,
            pluginSelectionService,
            agentPackageService,
            arbeitsverzeichnisResolver,
            benachrichtigungsHub,
            null,
            configuration,
            logger)
    {
    }

    /// <inheritdoc cref="EntwicklungsprozessService"/>
    public EntwicklungsprozessService(
        AufgabeService aufgabeService,
        ProtokollService protokollService,
        ProjektService? projektService,
        IGitPlugin gitPlugin,
        PluginSelectionService pluginSelectionService,
        IAgentPackageService agentPackageService,
        IArbeitsverzeichnisResolver arbeitsverzeichnisResolver,
        RepositoryStartskriptService? repositoryStartskriptService,
        IConfiguration configuration,
        ILogger<EntwicklungsprozessService> logger)
        : this(
            aufgabeService,
            protokollService,
            projektService,
            gitPlugin,
            pluginSelectionService,
            agentPackageService,
            arbeitsverzeichnisResolver,
            null,
            repositoryStartskriptService,
            configuration,
            logger)
    {
    }

    /// <inheritdoc cref="EntwicklungsprozessService"/>
    public EntwicklungsprozessService(
        AufgabeService aufgabeService,
        ProtokollService protokollService,
        ProjektService? projektService,
        IGitPlugin gitPlugin,
        PluginSelectionService pluginSelectionService,
        IAgentPackageService agentPackageService,
        IArbeitsverzeichnisResolver arbeitsverzeichnisResolver,
        KiAufgabenBenachrichtigungsHub? benachrichtigungsHub,
        RepositoryStartskriptService? repositoryStartskriptService,
        IConfiguration configuration,
        ILogger<EntwicklungsprozessService> logger)
    {
        _aufgabeService = aufgabeService;
        _protokollService = protokollService;
        _projektService = projektService;
        _gitPlugin = gitPlugin;
        _pluginSelectionService = pluginSelectionService;
        _agentPackageService = agentPackageService;
        _arbeitsverzeichnisResolver = arbeitsverzeichnisResolver;
        _benachrichtigungsHub = benachrichtigungsHub;
        _repositoryStartskriptService = repositoryStartskriptService;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>Startet den Entwicklungsprozess: Klon, Branch, Agentenpaket-Deploy.</summary>
    /// <param name="aufgabeId">ID der Aufgabe.</param>
    /// <param name="repositoryUrl">URL des zu klonenden Repositories.</param>
    /// <param name="basisBranchName">
    /// Optionaler vorhandener Remote-Branch. Ist er angegeben und kein Haupt-Branch,
    /// wird nach dem Klon zu diesem Branch gewechselt statt einen neuen anzulegen.
    /// Bei Haupt-Branch oder <c>null</c> wird wie gewohnt ein neuer <c>task/</c>-Branch erstellt.
    /// </param>
    /// <param name="selectedScmPluginPrefix">
    /// Optionaler Plugin-Prefix des SCM-Plugins (entspricht <see cref="Domain.Abstractions.IPlugin.PluginPrefix"/>).
    /// Wird gesetzt, wenn das Repository einem bestimmten Plugin zugeordnet ist (z.B. <c>LocalDirectoryPlugin</c>).
    /// Bei <c>null</c> wird das gespeicherte Standard-SCM-Plugin verwendet.
    /// </param>
    /// <param name="ct">Cancellation Token.</param>
    public async Task ProzessStartenAsync(
        Guid aufgabeId,
        string repositoryUrl,
        string? basisBranchName = null,
        string? selectedScmPluginPrefix = null,
        string? selectedKiPluginPrefix = null,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Entwicklungsprozess für Aufgabe {AufgabeId} starten.", aufgabeId);

        var aufgabe = await _aufgabeService.GetDetailAsync(aufgabeId, ct)
            ?? throw new InvalidOperationException($"Aufgabe {aufgabeId} nicht gefunden.");
        var repository = await ResolveRepositoryAsync(aufgabe, repositoryUrl, ct);
        var resolvedPluginPrefix = !string.IsNullOrWhiteSpace(repository.PluginTyp)
            ? repository.PluginTyp
            : selectedScmPluginPrefix;
        var gitPlugin = await _pluginSelectionService.ResolveSourceCodeManagementPluginAsync(resolvedPluginPrefix, ct);
        var kiPlugin = await _pluginSelectionService.ResolveDevelopmentAutomationPluginAsync(
            string.IsNullOrWhiteSpace(selectedKiPluginPrefix) ? aufgabe.KiPluginPrefix : selectedKiPluginPrefix,
            ct);

        // Kompatibilität des Agentenpakets VOR dem Klonen prüfen
        if (!string.IsNullOrEmpty(aufgabe.AgentenpaketName))
        {
            var paketVorCheck = await _agentPackageService.GetPackageAsync(aufgabe.AgentenpaketName, ct);
            if (paketVorCheck is not null)
            {
                var istKompatibel = await kiPlugin.IsAgentPackageCompatibleAsync(paketVorCheck.Pfad, ct);
                if (!istKompatibel)
                {
                    throw new InvalidOperationException(
                        $"Agentenpaket '{aufgabe.AgentenpaketName}' ist nicht kompatibel mit Plugin '{kiPlugin.PluginName}'. " +
                        $"Für GitHub Copilot muss das Paket einen '.github'-Ordner enthalten.");
                }
            }
        }

        // Lokalen Klon-Pfad bestimmen
        var workdirResult = await _arbeitsverzeichnisResolver.ResolveAsync(ct);
        var lokalerKlonPfad = Path.Combine(workdirResult.ResolvedPath, "softwareschmiede", aufgabeId.ToString());

        if (workdirResult.UsedFallback)
        {
            await _protokollService.AddEintragAsync(
                aufgabeId,
                ProtokollTyp.GitAktion,
                $"Arbeitsverzeichnis-Fallback aktiv ({workdirResult.ReasonCode}). Verwende {workdirResult.ResolvedPath}. Bitte Einstellung prüfen.",
                ct: ct);
        }

        // Zielverzeichnis löschen, falls es bereits existiert, um einen sauberen Klon sicherzustellen
        if (Directory.Exists(lokalerKlonPfad))
        {
            _logger.LogInformation("Zielverzeichnis '{KlonPfad}' existiert bereits, wird gelöscht.", lokalerKlonPfad);
            DeleteDirectoryForce(lokalerKlonPfad);
        }

        // Repository klonen
        _logger.LogInformation("Repository '{RepositoryUrl}' nach '{KlonPfad}' klonen (Plugin: {PluginPrefix}).", repository.RepositoryUrl, lokalerKlonPfad, gitPlugin.PluginPrefix);
        await gitPlugin.CloneRepositoryAsync(repository.RepositoryUrl, lokalerKlonPfad, ct);

        if (kiPlugin is CliKiPluginBase cliKiPlugin)
        {
            cliKiPlugin.ClearContextFiles(lokalerKlonPfad);
        }

        string branchName;

        // Prüfen ob ein vorhandener Remote-Branch genutzt werden soll
        var nutzeExistierendenBranch = false;
        if (!string.IsNullOrEmpty(basisBranchName))
        {
            var defaultBranch = await gitPlugin.GetDefaultBranchAsync(repository.RepositoryUrl, ct);
            nutzeExistierendenBranch = !string.Equals(basisBranchName, defaultBranch, StringComparison.OrdinalIgnoreCase);
        }

        if (nutzeExistierendenBranch)
        {
            // Vorhandenen Remote-Branch auschecken
            _logger.LogInformation("Wechsle zu vorhandenem Branch '{BasisBranch}'.", basisBranchName);
            await gitPlugin.CheckoutRemoteBranchAsync(lokalerKlonPfad, basisBranchName!, ct);
            branchName = basisBranchName!;
        }
        else
        {
            // Neuen task-Branch anlegen
            branchName = ErstelleTaskBranchName(aufgabe);

            _logger.LogInformation("Branch '{BranchName}' anlegen.", branchName);
            await gitPlugin.CreateBranchAsync(lokalerKlonPfad, branchName, ct);
        }

        string? startskriptHinweis = null;
        if (repository.StartKonfiguration is not null && _repositoryStartskriptService is not null)
        {
            try
            {
                await _repositoryStartskriptService.RunAsync(lokalerKlonPfad, repository.StartKonfiguration, ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                startskriptHinweis =
                    $"Hinweis: Das Repository-Startskript konnte nicht ausgeführt werden ({ex.Message}). Die Aufgabe wurde dennoch gestartet.";
                _logger.LogWarning(ex, "Repository-Startskript für Aufgabe {AufgabeId} ist fehlgeschlagen.", aufgabeId);
            }
        }

        // Agentenpaket deployen, wenn vorhanden (Kompatibilität wurde bereits vor dem Klonen geprüft)
        if (!string.IsNullOrEmpty(aufgabe.AgentenpaketName))
        {
            var paket = await _agentPackageService.GetPackageAsync(aufgabe.AgentenpaketName, ct);
            if (paket is not null)
            {
                _logger.LogInformation("Agentenpaket '{PaketName}' deployen.", aufgabe.AgentenpaketName);
                await kiPlugin.DeployAgentPackageAsync(paket.Pfad, lokalerKlonPfad, ct);
            }
            else
            {
                _logger.LogWarning("Agentenpaket '{PaketName}' nicht gefunden.", aufgabe.AgentenpaketName);
            }
        }

        // Aufgabe als gestartet markieren
        await _aufgabeService.StartenAsync(aufgabeId, branchName, lokalerKlonPfad, ct);

        // Protokolleintrag
        var protokollNachricht = nutzeExistierendenBranch
            ? $"Klon angelegt, vorhandener Branch ausgecheckt: {branchName} in {lokalerKlonPfad}"
            : $"Klon und Branch angelegt: {branchName} in {lokalerKlonPfad}";
        if (!string.IsNullOrWhiteSpace(startskriptHinweis))
        {
            protokollNachricht = $"{protokollNachricht}{Environment.NewLine}{startskriptHinweis}";
        }

        await _protokollService.AddEintragAsync(aufgabeId, ProtokollTyp.GitAktion, protokollNachricht, ct: ct);

        _logger.LogInformation("Entwicklungsprozess für Aufgabe {AufgabeId} erfolgreich gestartet.", aufgabeId);
    }

    /// <summary>Führt das konfigurierte Repository-Startskript einer laufenden Aufgabe manuell aus.</summary>
    public async Task<(bool Success, string Message)> RepositoryStartskriptAusfuehrenAsync(Guid aufgabeId, CancellationToken ct = default)
    {
        _logger.LogInformation("Repository-Startskript für Aufgabe {AufgabeId} manuell ausführen.", aufgabeId);

        if (_repositoryStartskriptService is null)
        {
            throw new InvalidOperationException("Repository-Startskript-Service ist nicht verfügbar.");
        }

        var aufgabe = await _aufgabeService.GetDetailAsync(aufgabeId, ct)
            ?? throw new InvalidOperationException($"Aufgabe {aufgabeId} nicht gefunden.");
        if (aufgabe.Status is not (AufgabeStatus.InBearbeitung or AufgabeStatus.KiAktiv))
        {
            throw new InvalidOperationException("Das Repository-Startskript kann nur für laufende Aufgaben ausgeführt werden.");
        }

        if (string.IsNullOrWhiteSpace(aufgabe.LokalerKlonPfad))
        {
            throw new InvalidOperationException("Die Aufgabe hat keinen lokalen Klonpfad.");
        }

        if (!Directory.Exists(aufgabe.LokalerKlonPfad))
        {
            throw new InvalidOperationException("Das lokale Arbeitsverzeichnis der Aufgabe ist nicht mehr vorhanden.");
        }

        var repositoryUrl = aufgabe.GitRepository?.RepositoryUrl ?? string.Empty;
        var repository = await ResolveRepositoryAsync(aufgabe, repositoryUrl, ct);
        var startKonfiguration = repository.StartKonfiguration
            ?? throw new InvalidOperationException("Für das Repository ist kein Startskript konfiguriert.");

        try
        {
            await _repositoryStartskriptService.RunAsync(aufgabe.LokalerKlonPfad, startKonfiguration, ct);
            const string successMessage = "Repository-Startskript wurde ausgeführt.";
            await _protokollService.AddEintragAsync(aufgabeId, ProtokollTyp.GitAktion, successMessage, ct: ct);
            return (true, successMessage);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            var hintMessage = $"Hinweis: Das Repository-Startskript konnte nicht ausgeführt werden ({ex.Message}).";
            _logger.LogWarning(ex, "Manuelle Startskript-Ausführung für Aufgabe {AufgabeId} ist fehlgeschlagen.", aufgabeId);
            await _protokollService.AddEintragAsync(aufgabeId, ProtokollTyp.GitAktion, hintMessage, ct: ct);
            return (false, hintMessage);
        }
    }

    /// <summary>Startet einen KI-Lauf und streamt die Ausgabe.</summary>
    /// <remarks>RACE CONDITION SCHUTZ: Wirft Exception wenn Status bereits KiAktiv.</remarks>
    public async IAsyncEnumerable<string> KiStartenAsync(
        Guid aufgabeId,
        string prompt,
        AgentInfo agent,
        string? selectedKiPluginPrefix = null,
        string? model = null,
        FolgeanweisungsKontextmodus? kontextmodus = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var aufgabe = await _aufgabeService.GetByIdAsync(aufgabeId, ct)
            ?? throw new InvalidOperationException($"Aufgabe {aufgabeId} nicht gefunden.");

        if (aufgabe.Status == AufgabeStatus.KiAktiv)
            throw new InvalidOperationException("KI ist bereits aktiv für diese Aufgabe.");

        if (string.IsNullOrEmpty(aufgabe.LokalerKlonPfad))
            throw new InvalidOperationException($"Aufgabe {aufgabeId} hat keinen lokalen Klonpfad. Bitte zuerst ProzessStartenAsync aufrufen.");

        var runId = Guid.NewGuid();
        var kiPlugin = await _pluginSelectionService.ResolveDevelopmentAutomationPluginAsync(
            string.IsNullOrWhiteSpace(selectedKiPluginPrefix) ? aufgabe.KiPluginPrefix : selectedKiPluginPrefix,
            ct);
        var finalPrompt = prompt;
        var contextFilePath = ResolveContextFilePath(kiPlugin, aufgabe.LokalerKlonPfad);

        if (kontextmodus is not null)
        {
            try
            {
                finalPrompt = await BuildFollowPromptWithContextAsync(
                    aufgabeId,
                    prompt,
                    agent,
                    aufgabe.LokalerKlonPfad,
                    runId,
                    kiPlugin,
                    kontextmodus.Value,
                    model,
                    ct);
            }
            catch (Exception ex)
            {
                var preflightContextEventId = Guid.NewGuid();
                var preflightEntry = BuildContextEntry(
                    runId,
                    preflightContextEventId,
                    kontextmodus.Value,
                    prompt,
                    $"Vor dem KI-Start abgebrochen: {ex.Message}",
                    isError: true);
                await AppendContextEntryAsync(contextFilePath, preflightEntry, CancellationToken.None);
                await _protokollService.AddEintragAsync(
                    aufgabeId,
                    ProtokollTyp.KiAntwort,
                    $"[RunId:{runId}][ContextEventId:{preflightContextEventId}] Fehler vor KI-Start: {ex.Message}",
                    agent.Name,
                    CancellationToken.None);
                throw;
            }
        }

        await _protokollService.AddEintragAsync(
            aufgabeId,
            ProtokollTyp.Prompt,
            $"[RunId:{runId}] {(kontextmodus is null ? "Initialprompt" : $"Kontextmodus={kontextmodus}")}\n- KI-Plugin: {kiPlugin.PluginName} ({kiPlugin.PluginPrefix})\n{finalPrompt}",
            agent.Name,
            ct);
        await _aufgabeService.ClearPromptVorschlagAsync(aufgabeId, ct);
        await _aufgabeService.KiAktiviertAsync(aufgabeId, ct);

        var vollstaendigeAntwort = new StringBuilder();
        Exception? fehler = null;

        // yield return inside a try-catch is not allowed in C#.
        // Workaround: manually advance the enumerator and catch MoveNextAsync separately.
        var enumerator = kiPlugin.StartDevelopmentAsync(finalPrompt, agent, aufgabe.LokalerKlonPfad, model, ct)
            .GetAsyncEnumerator(ct);

        try
        {
            while (true)
            {
                bool hasNext;
                try
                {
                    hasNext = await enumerator.MoveNextAsync();
                }
                catch (Exception ex)
                {
                    fehler = ex;
                    _logger.LogError(ex, "Fehler beim KI-Lauf für Aufgabe {AufgabeId}.", aufgabeId);
                    break;
                }

                if (!hasNext)
                {
                    break;
                }

                if (TryParseRateLimitSuggestion(enumerator.Current, out var rateLimitSuggestion))
                {
                    await _aufgabeService.SavePromptVorschlagAsync(
                        aufgabeId,
                        rateLimitSuggestion.Prompt,
                        rateLimitSuggestion.AusfuehrenAbUtc,
                        CancellationToken.None);

                    var localTime = rateLimitSuggestion.AusfuehrenAbUtc.ToLocalTime();
                    var waitMessage = string.Format(
                        CultureInfo.CurrentUICulture,
                        "Rate-Limit erreicht. Vorschlag gespeichert: \"{0}\" ab {1:dd.MM.yyyy HH:mm:ss}.",
                        rateLimitSuggestion.Prompt,
                        localTime);

                    vollstaendigeAntwort.AppendLine(waitMessage);
                    yield return waitMessage;
                    continue;
                }

                vollstaendigeAntwort.AppendLine(enumerator.Current);
                yield return enumerator.Current;
            }
        }
        finally
        {
            await enumerator.DisposeAsync();

            if (fehler is not null)
            {
                var markdownFehler = BuildKiArbeitsprotokollMarkdown(
                    runId,
                    DateTimeOffset.UtcNow,
                    $"Fehler: {fehler.Message}");

                await _aufgabeService.FehlgeschlagenAsync(aufgabeId, CancellationToken.None);
                await _protokollService.AddEintragAsync(
                    aufgabeId,
                    ProtokollTyp.KiAntwort,
                    markdownFehler,
                    agent.Name,
                    CancellationToken.None);
            }
            else
            {
                var markdownAntwort = BuildKiArbeitsprotokollMarkdown(
                    runId,
                    DateTimeOffset.UtcNow,
                    vollstaendigeAntwort.ToString());

                await _protokollService.AddEintragAsync(
                    aufgabeId,
                    ProtokollTyp.KiAntwort,
                    markdownAntwort,
                    agent.Name,
                    CancellationToken.None);
                await _aufgabeService.KiAbgeschlossenAsync(aufgabeId, CancellationToken.None);
            }

            var abschlusstatus = fehler is null ? AufgabeStatus.InBearbeitung : AufgabeStatus.Fehlgeschlagen;
            if (_benachrichtigungsHub is not null)
            {
                await _benachrichtigungsHub.PublishAsync(
                    new KiAufgabenAbschlussEreignis(
                        Guid.NewGuid(),
                        aufgabeId,
                        aufgabe.Titel,
                        abschlusstatus,
                        DateTimeOffset.UtcNow));
            }

            var contextEventId = Guid.NewGuid();
            var responseContent = fehler is null
                ? vollstaendigeAntwort.ToString().Trim()
                : $"Fehler: {fehler.Message}";
            var contextEntry = BuildContextEntry(runId, contextEventId, kontextmodus, prompt, responseContent, fehler is not null);
            await AppendContextEntryAsync(contextFilePath, contextEntry, CancellationToken.None);
        }
    }

    /// <summary>Führt einen manuellen Commit durch.</summary>
    public async Task CommitDurchfuehrenAsync(Guid aufgabeId, string message, CancellationToken ct = default)
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

    /// <summary>Setzt Commits zurück (soft/mixed/hard).</summary>
    public async Task ResetDurchfuehrenAsync(Guid aufgabeId, string resetType, string? targetRef, CancellationToken ct = default)
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

    /// <summary>Pusht den Branch auf den Remote.</summary>
    public async Task PushDurchfuehrenAsync(Guid aufgabeId, CancellationToken ct = default)
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

    /// <summary>Holt Änderungen vom Remote.</summary>
    public async Task PullDurchfuehrenAsync(Guid aufgabeId, CancellationToken ct = default)
    {
        _logger.LogInformation("Pull für Aufgabe {AufgabeId} durchführen.", aufgabeId);

        var aufgabe = await _aufgabeService.GetByIdAsync(aufgabeId, ct)
            ?? throw new InvalidOperationException($"Aufgabe {aufgabeId} nicht gefunden.");

        if (string.IsNullOrEmpty(aufgabe.LokalerKlonPfad))
            throw new InvalidOperationException($"Aufgabe {aufgabeId} hat keinen lokalen Klonpfad.");

        await _gitPlugin.PullAsync(aufgabe.LokalerKlonPfad, ct);

        var pullLogText = string.Equals(_gitPlugin.PluginPrefix, "LocalDirectoryPlugin", StringComparison.Ordinal)
            ? "Pull: Kein Merge durchgeführt. Arbeitsverzeichnis wurde per Dateisynchronisation aktualisiert."
            : "Pull: Änderungen vom Remote geholt.";

        await _protokollService.AddEintragAsync(
            aufgabeId,
            ProtokollTyp.GitAktion,
            pullLogText,
            ct: ct);

        _logger.LogInformation("Pull für Aufgabe {AufgabeId} durchgeführt.", aufgabeId);
    }

    /// <summary>Erstellt einen Pull Request für die Aufgabe.</summary>
    public async Task<PullRequest> PullRequestErstellenAsync(
        Guid aufgabeId,
        string repositoryId,
        string title,
        string body,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Pull Request für Aufgabe {AufgabeId} erstellen.", aufgabeId);

        var aufgabe = await _aufgabeService.GetByIdAsync(aufgabeId, ct)
            ?? throw new InvalidOperationException($"Aufgabe {aufgabeId} nicht gefunden.");

        if (string.IsNullOrEmpty(aufgabe.BranchName))
            throw new InvalidOperationException($"Aufgabe {aufgabeId} hat keinen Branch-Namen.");

        var pullRequest = await _gitPlugin.CreatePullRequestAsync(repositoryId, aufgabe.BranchName, title, body, ct);

        await _protokollService.AddEintragAsync(
            aufgabeId,
            ProtokollTyp.GitAktion,
            $"Pull Request erstellt: #{pullRequest.Nummer} – {pullRequest.Titel} ({pullRequest.Url})",
            ct: ct);

        _logger.LogInformation("Pull Request #{PrNummer} für Aufgabe {AufgabeId} erstellt.", pullRequest.Nummer, aufgabeId);
        return pullRequest;
    }

    /// <summary>Führt Tests aus und protokolliert das Ergebnis.</summary>
    public async Task<TestResult> TestsAusfuehrenAsync(Guid aufgabeId, CancellationToken ct = default)
    {
        _logger.LogInformation("Tests für Aufgabe {AufgabeId} ausführen.", aufgabeId);

        var aufgabe = await _aufgabeService.GetByIdAsync(aufgabeId, ct)
            ?? throw new InvalidOperationException($"Aufgabe {aufgabeId} nicht gefunden.");

        if (string.IsNullOrEmpty(aufgabe.LokalerKlonPfad))
            throw new InvalidOperationException($"Aufgabe {aufgabeId} hat keinen lokalen Klonpfad.");

        var kiPlugin = await _pluginSelectionService.ResolveDevelopmentAutomationPluginAsync(null, ct);
        var testResult = await kiPlugin.RunTestsAsync(aufgabe.LokalerKlonPfad, ct);
        await _protokollService.AddTestErgebnisseAsync(aufgabeId, testResult, ct);

        _logger.LogInformation("Tests für Aufgabe {AufgabeId} abgeschlossen. Bestanden: {Bestanden}.", aufgabeId, testResult.Bestanden);
        return testResult;
    }

    /// <summary>Schließt die Aufgabe ab: Klon löschen, Status auf Abgeschlossen setzen.</summary>
    public async Task AbschliessenAsync(Guid aufgabeId, CancellationToken ct = default)
    {
        _logger.LogInformation("Aufgabe {AufgabeId} abschließen.", aufgabeId);

        var aufgabe = await _aufgabeService.GetByIdAsync(aufgabeId, ct)
            ?? throw new InvalidOperationException($"Aufgabe {aufgabeId} nicht gefunden.");

        var vonStatus = aufgabe.Status;

        // Klon-Verzeichnis löschen
        if (!string.IsNullOrEmpty(aufgabe.LokalerKlonPfad) && Directory.Exists(aufgabe.LokalerKlonPfad))
        {
            _logger.LogInformation("Klon-Verzeichnis '{KlonPfad}' löschen.", aufgabe.LokalerKlonPfad);
            DeleteDirectoryForce(aufgabe.LokalerKlonPfad);
        }

        await _aufgabeService.AbschliessenAsync(aufgabeId, ct);

        await _protokollService.AddStatusUebergangAsync(aufgabeId, vonStatus, AufgabeStatus.Abgeschlossen, ct);

        _logger.LogInformation("Aufgabe {AufgabeId} erfolgreich abgeschlossen.", aufgabeId);
    }

    /// <summary>Bricht die Aufgabe ab: Klon löschen ohne Änderungen zu pushen.</summary>
    public async Task AbbrechenAsync(Guid aufgabeId, CancellationToken ct = default)
    {
        _logger.LogInformation("Aufgabe {AufgabeId} abbrechen.", aufgabeId);

        var aufgabe = await _aufgabeService.GetByIdAsync(aufgabeId, ct)
            ?? throw new InvalidOperationException($"Aufgabe {aufgabeId} nicht gefunden.");

        var vonStatus = aufgabe.Status;

        // Klon-Verzeichnis löschen (keine Änderungen pushen)
        if (!string.IsNullOrEmpty(aufgabe.LokalerKlonPfad) && Directory.Exists(aufgabe.LokalerKlonPfad))
        {
            _logger.LogInformation("Klon-Verzeichnis '{KlonPfad}' ohne Push löschen.", aufgabe.LokalerKlonPfad);
            DeleteDirectoryForce(aufgabe.LokalerKlonPfad);
        }

        await _aufgabeService.AbbrechenAsync(aufgabeId, ct);

        await _protokollService.AddStatusUebergangAsync(aufgabeId, vonStatus, AufgabeStatus.Offen, ct);

        _logger.LogInformation("Aufgabe {AufgabeId} abgebrochen.", aufgabeId);
    }

    /// <summary>Gibt die Remote-Branches eines Repositories zurück (ohne Klon).</summary>
    /// <param name="repositoryUrl">URL des Repositories.</param>
    /// <param name="selectedScmPluginPrefix">
    /// Optionaler Plugin-Prefix des SCM-Plugins. Entspricht dem <see cref="GitRepository.PluginTyp"/> des Repositories.
    /// Bei <c>null</c> wird das gespeicherte Standard-SCM-Plugin verwendet.
    /// </param>
    /// <param name="ct">Cancellation Token.</param>
    public async Task<IEnumerable<string>> GetRemoteBranchesAsync(string repositoryUrl, string? selectedScmPluginPrefix = null, CancellationToken ct = default)
    {
        _logger.LogInformation("Remote-Branches für {RepositoryUrl} abrufen.", repositoryUrl);
        var gitPlugin = await _pluginSelectionService.ResolveSourceCodeManagementPluginAsync(selectedScmPluginPrefix, ct);
        return await gitPlugin.GetRemoteBranchesAsync(repositoryUrl, ct);
    }

    private async Task<GitRepository> ResolveRepositoryAsync(Aufgabe aufgabe, string repositoryUrl, CancellationToken ct)
    {
        if (aufgabe.GitRepository is not null)
        {
            return aufgabe.GitRepository;
        }

        if (_projektService is null)
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

        var projekt = await _projektService.GetDetailAsync(aufgabe.ProjektId, ct)
            ?? throw new InvalidOperationException($"Projekt {aufgabe.ProjektId} nicht gefunden.");

        var repositories = projekt.Repositories
            .Where(repository => repository.Aktiv)
            .OrderBy(repository => repository.RepositoryName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(repository => repository.Id)
            .ToList();

        var matchingRepository = repositories.FirstOrDefault(repository =>
            string.Equals(repository.RepositoryUrl, repositoryUrl, StringComparison.OrdinalIgnoreCase));

        if (matchingRepository is not null)
        {
            return matchingRepository;
        }

        if (repositories.Count == 1)
        {
            return repositories[0];
        }

        throw new InvalidOperationException(
            $"Aufgabe {aufgabe.Id} kann nicht gestartet werden, weil kein eindeutiges Repository für den Startkontext ermittelt werden konnte.");
    }

    private async Task<string> BuildFollowPromptWithContextAsync(
        Guid aufgabeId,
        string userPrompt,
        AgentInfo agent,
        string localRepoPath,
        Guid runId,
        IKiPlugin kiPlugin,
        FolgeanweisungsKontextmodus kontextmodus,
        string? model,
        CancellationToken ct)
    {
        var contextEventId = Guid.NewGuid();

        if (kontextmodus == FolgeanweisungsKontextmodus.KontextNeuBeginnen)
        {
            if (kiPlugin is CliKiPluginBase contextPlugin)
            {
                contextPlugin.ClearContextFiles(localRepoPath);
            }

            await _protokollService.AddEintragAsync(
                aufgabeId,
                ProtokollTyp.StatusUebergang,
                $"[RunId:{runId}][ContextEventId:{contextEventId}] Kontext wurde zurückgesetzt (Modus: Kontext neu beginnen).",
                agent.Name,
                ct);
            return userPrompt;
        }

        if (kontextmodus == FolgeanweisungsKontextmodus.KontextIgnorieren)
        {
            await _protokollService.AddEintragAsync(
                aufgabeId,
                ProtokollTyp.StatusUebergang,
                $"[RunId:{runId}][ContextEventId:{contextEventId}] Folgeanweisung ohne Kontextpräfix (Modus: Kontext ignorieren).",
                agent.Name,
                ct);
            return userPrompt;
        }

        var existingContextFilePath = ResolveExistingContextFilePath(kiPlugin, localRepoPath, ResolveContextFilePath(kiPlugin, localRepoPath));
        if (string.IsNullOrWhiteSpace(existingContextFilePath))
        {
            await _protokollService.AddEintragAsync(
                aufgabeId,
                ProtokollTyp.StatusUebergang,
                $"[RunId:{runId}][ContextEventId:{contextEventId}] Keine vorhandene Kontextdatei gefunden, Folgeanweisung ohne Kontextpräfix gesendet.",
                agent.Name,
                ct);
            return userPrompt;
        }

        await EnsureContextWithinLimitsAsync(
            aufgabeId,
            existingContextFilePath,
            agent,
            localRepoPath,
            runId,
            kiPlugin,
            model,
            ct);

        await _protokollService.AddEintragAsync(
            aufgabeId,
            ProtokollTyp.StatusUebergang,
            $"[RunId:{runId}][ContextEventId:{contextEventId}] Folgeanweisung mit Kontextdatei-Referenz gesendet.",
            agent.Name,
            ct);

        return CliKiPluginBase.MarkPromptToIncludeContextFile(userPrompt);
    }

    private static string ResolveContextFilePath(IKiPlugin kiPlugin, string localRepoPath)
    {
        if (kiPlugin is CliKiPluginBase cliKiPlugin)
        {
            return cliKiPlugin.BuildContextFilePath(localRepoPath);
        }

        return Path.Combine(localRepoPath, "copilot.context.md");
    }

    private static string? ResolveExistingContextFilePath(IKiPlugin kiPlugin, string localRepoPath, string fallbackContextFilePath)
    {
        if (kiPlugin is CliKiPluginBase cliKiPlugin)
        {
            return cliKiPlugin.GetLatestContextFilePath(localRepoPath);
        }

        return File.Exists(fallbackContextFilePath)
            ? fallbackContextFilePath
            : null;
    }

    private async Task EnsureContextWithinLimitsAsync(
        Guid aufgabeId,
        string contextFilePath,
        AgentInfo agent,
        string localRepoPath,
        Guid runId,
        IKiPlugin kiPlugin,
        string? model,
        CancellationToken ct)
    {
        var context = await ReadFileTextSafeAsync(contextFilePath, ct);
        if (string.IsNullOrWhiteSpace(context))
        {
            return;
        }

        var softLimit = _configuration.GetValue<int?>("KiKontext:SoftLimitChars") ?? DefaultContextCompressionSoftLimit;
        var hardLimit = _configuration.GetValue<int?>("KiKontext:HardLimitChars") ?? DefaultContextCompressionHardLimit;

        if (context.Length > softLimit)
        {
            var compressionEventId = Guid.NewGuid();
            await _protokollService.AddEintragAsync(
                aufgabeId,
                ProtokollTyp.StatusUebergang,
                $"[RunId:{runId}][ContextEventId:{compressionEventId}] Soft-Limit ({softLimit}) überschritten, starte KI-Komprimierung der Kontextdatei.",
                agent.Name,
                ct);

            context = await CompressContextAsync(contextFilePath, agent, localRepoPath, kiPlugin, model, ct);
            await WriteTextAtomicallyAsync(contextFilePath, context, ct);

            await _protokollService.AddEintragAsync(
                aufgabeId,
                ProtokollTyp.StatusUebergang,
                $"[RunId:{runId}][ContextEventId:{compressionEventId}] Kontextdatei komprimiert auf {context.Length} Zeichen.",
                agent.Name,
                ct);
        }

        if (context.Length > hardLimit)
        {
            await _protokollService.AddEintragAsync(
                aufgabeId,
                ProtokollTyp.StatusUebergang,
                $"[RunId:{runId}] Kontextdatei konnte nicht zufriedenstellend komprimiert werden. Aktuelle Anzahl Zeichen: {context.Length}",
                agent.Name,
                ct);
        }
    }

    private async Task<string> CompressContextAsync(
        string contextFilePath,
        AgentInfo agent,
        string localRepoPath,
        IKiPlugin kiPlugin,
        string? model,
        CancellationToken ct)
    {
        var contextFileName = Path.GetFileName(contextFilePath);
        var compressionPrompt = $"""
            Komprimiere den Inhalt der Datei {contextFileName} in eine knappe, strukturierte Markdown-Zusammenfassung.
            Schreibe die komprimierte Fassung direkt in dieselbe Datei zurück.
            Gib anschließend ausschließlich den finalen Dateiinhalt als Markdown zurück, ohne Einleitung und ohne Codeblock.
            Pflichtabschnitte:
            - Ziel
            - Offene Punkte
            - Letzte Entscheidungen
            - Relevante Randbedingungen
            """;

        var builder = new StringBuilder();
        await foreach (var line in kiPlugin.StartDevelopmentAsync(compressionPrompt, new AgentInfo("", null, ""), localRepoPath, model, ct))
        {
            builder.AppendLine(line);
        }

        var compressed = builder.ToString().Trim();
        if (string.IsNullOrWhiteSpace(compressed))
        {
            throw new InvalidOperationException("KI-Komprimierung hat keinen verwertbaren Kontext zurückgegeben.");
        }
        if (!ContainsMandatoryCompressionSections(compressed))
        {
            throw new InvalidOperationException(
                "KI-Komprimierung enthält nicht alle Pflichtabschnitte (Ziel, Offene Punkte, Letzte Entscheidungen).");
        }

        return compressed;
    }

    private static bool ContainsMandatoryCompressionSections(string markdown)
        => markdown.Contains("Ziel", StringComparison.OrdinalIgnoreCase)
            && markdown.Contains("Offene Punkte", StringComparison.OrdinalIgnoreCase)
            && markdown.Contains("Letzte Entscheidungen", StringComparison.OrdinalIgnoreCase);

    private static bool TryParseRateLimitSuggestion(string line, out RateLimitSuggestion suggestion)
    {
        suggestion = default;
        if (string.IsNullOrWhiteSpace(line) || !line.StartsWith(RateLimitSuggestionMarker, StringComparison.Ordinal))
        {
            return false;
        }

        var payload = line[RateLimitSuggestionMarker.Length..].TrimStart(';');
        var parts = payload.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        string? resetUtcRaw = null;
        string? prompt = null;
        foreach (var part in parts)
        {
            var separatorIndex = part.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = part[..separatorIndex].Trim();
            var value = part[(separatorIndex + 1)..].Trim();
            if (key.Equals("resetUtc", StringComparison.OrdinalIgnoreCase))
            {
                resetUtcRaw = value;
            }
            else if (key.Equals("prompt", StringComparison.OrdinalIgnoreCase))
            {
                prompt = value;
            }
        }

        if (!DateTimeOffset.TryParse(
                resetUtcRaw,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var resetUtc))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(prompt))
        {
            prompt = "Mach nun bitte weiter.";
        }

        suggestion = new RateLimitSuggestion(prompt.Trim(), resetUtc);
        return true;
    }

    private static string BuildKiArbeitsprotokollMarkdown(Guid runId, DateTimeOffset zeitpunktUtc, string antwortRohtext)
    {
        var zeilen = antwortRohtext
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.None)
            .Where(zeile => !string.IsNullOrWhiteSpace(zeile))
            .Select(zeile => zeile.TrimEnd())
            .ToArray();

        var builder = new StringBuilder();
        builder.AppendLine($"# {zeitpunktUtc:yyyy-MM-dd}");
        builder.AppendLine();
        builder.AppendLine($"- RunId: `{runId}`");
        builder.AppendLine();

        if (zeilen.Length == 0)
        {
            builder.AppendLine("## Schritt 1");
            builder.AppendLine("Keine Ausgabe vorhanden.");
            return builder.ToString().TrimEnd();
        }

        for (var i = 0; i < zeilen.Length; i++)
        {
            builder.AppendLine($"## Schritt {i + 1}");
            builder.AppendLine(zeilen[i]);
            builder.AppendLine();
        }

        return builder.ToString().TrimEnd();
    }

    private static string BuildContextEntry(
        Guid runId,
        Guid contextEventId,
        FolgeanweisungsKontextmodus? kontextmodus,
        string userPrompt,
        string response,
        bool isError)
        => $"""
            ## Verlaufseintrag
            - RunId: {runId}
            - ContextEventId: {contextEventId}
            - Modus: {(kontextmodus?.ToString() ?? "Initialprompt")}
            - Zeit: {DateTimeOffset.UtcNow:O}
            - Status: {(isError ? "Fehler" : "Erfolgreich")}

            ### Nutzeranweisung
            {userPrompt}

            ### KI-Rückmeldung
            {response}

            """;

    private readonly record struct RateLimitSuggestion(string Prompt, DateTimeOffset AusfuehrenAbUtc);

    private async Task AppendContextEntryAsync(string contextFilePath, string entry, CancellationToken ct)
    {
        var current = await ReadFileTextSafeAsync(contextFilePath, ct);
        var next = string.IsNullOrWhiteSpace(current)
            ? entry
            : $"{current.TrimEnd()}\n\n{entry}";

        await WriteTextAtomicallyAsync(contextFilePath, next, ct);
    }

    private static async Task<string> ReadFileTextSafeAsync(string path, CancellationToken ct)
    {
        if (!File.Exists(path))
        {
            return string.Empty;
        }

        return await File.ReadAllTextAsync(path, ct);
    }

    private static async Task WriteTextAtomicallyAsync(string targetPath, string content, CancellationToken ct)
    {
        var directory = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = $"{targetPath}.tmp";

        try
        {
            await using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            await using (var writer = new StreamWriter(stream, Encoding.UTF8))
            {
                await writer.WriteAsync(content.AsMemory(), ct);
                await writer.FlushAsync(ct);
                await stream.FlushAsync(ct);
            }

            if (File.Exists(targetPath))
            {
                File.Delete(targetPath);
            }

            File.Move(tempPath, targetPath);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    /// <summary>Erstellt einen URL-freundlichen Slug aus dem Titel (max. 30 Zeichen).</summary>
    private static string ErstelleTaskBranchName(Aufgabe aufgabe)
    {
        var titelSlug = ErstelleTitelSlug(aufgabe.Titel);
        var issueNummer = aufgabe.IssueReferenz?.IssueNummer;

        return issueNummer is > 0
            ? $"task/issue-{issueNummer.Value}-{aufgabe.Id:N}-{titelSlug}"
            : $"task/{aufgabe.Id:N}-{titelSlug}";
    }

    /// <summary>Erstellt einen URL-freundlichen Slug aus dem Titel (max. 30 Zeichen).</summary>
    private static string ErstelleTitelSlug(string titel)
    {
        var slug = titel.ToLowerInvariant()
            .Replace(' ', '-')
            .Replace("ä", "ae")
            .Replace("ö", "oe")
            .Replace("ü", "ue")
            .Replace("ß", "ss");

        // Nur alphanumerische Zeichen und Bindestriche behalten
        slug = new string(slug.Where(c => char.IsLetterOrDigit(c) || c == '-').ToArray());

        // Mehrfache Bindestriche zusammenfassen
        while (slug.Contains("--"))
            slug = slug.Replace("--", "-");

        slug = slug.Trim('-');

        // Auf max. 30 Zeichen kürzen
        if (slug.Length > 30)
            slug = slug[..30].TrimEnd('-');

        return string.IsNullOrEmpty(slug) ? "aufgabe" : slug;
    }

    /// <summary>
    /// Löscht ein Verzeichnis rekursiv und setzt dabei read-only-Attribute zurück.
    /// Notwendig für Git-Repositories unter Windows, die schreibgeschützte Pack-Dateien enthalten.
    /// </summary>
    private static void DeleteDirectoryForce(string path)
    {
        foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(path, recursive: true);
    }
}
