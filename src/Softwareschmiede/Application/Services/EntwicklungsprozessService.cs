using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
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
    private readonly IGitPlugin _gitPlugin;
    private readonly IKiPlugin _kiPlugin;
    private readonly IAgentPackageService _agentPackageService;
    private readonly IArbeitsverzeichnisResolver _arbeitsverzeichnisResolver;
    private readonly IConfiguration _configuration;
    private readonly ILogger<EntwicklungsprozessService> _logger;
    private const int DefaultContextCompressionSoftLimit = 12_000;
    private const int DefaultContextCompressionHardLimit = 20_000;

    /// <inheritdoc cref="EntwicklungsprozessService"/>
    public EntwicklungsprozessService(
        AufgabeService aufgabeService,
        ProtokollService protokollService,
        IGitPlugin gitPlugin,
        IKiPlugin kiPlugin,
        IAgentPackageService agentPackageService,
        IArbeitsverzeichnisResolver arbeitsverzeichnisResolver,
        IConfiguration configuration,
        ILogger<EntwicklungsprozessService> logger)
    {
        _aufgabeService = aufgabeService;
        _protokollService = protokollService;
        _gitPlugin = gitPlugin;
        _kiPlugin = kiPlugin;
        _agentPackageService = agentPackageService;
        _arbeitsverzeichnisResolver = arbeitsverzeichnisResolver;
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
    /// <param name="ct">Cancellation Token.</param>
    public async Task ProzessStartenAsync(Guid aufgabeId, string repositoryUrl, string? basisBranchName = null, CancellationToken ct = default)
    {
        _logger.LogInformation("Entwicklungsprozess für Aufgabe {AufgabeId} starten.", aufgabeId);

        var aufgabe = await _aufgabeService.GetByIdAsync(aufgabeId, ct)
            ?? throw new InvalidOperationException($"Aufgabe {aufgabeId} nicht gefunden.");

        // Kompatibilität des Agentenpakets VOR dem Klonen prüfen
        if (!string.IsNullOrEmpty(aufgabe.AgentenpaketName))
        {
            var paketVorCheck = await _agentPackageService.GetPackageAsync(aufgabe.AgentenpaketName, ct);
            if (paketVorCheck is not null)
            {
                var istKompatibel = await _kiPlugin.IsAgentPackageCompatibleAsync(paketVorCheck.Pfad, ct);
                if (!istKompatibel)
                {
                    throw new InvalidOperationException(
                        $"Agentenpaket '{aufgabe.AgentenpaketName}' ist nicht kompatibel mit Plugin '{_kiPlugin.PluginName}'. " +
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
        _logger.LogInformation("Repository '{RepositoryUrl}' nach '{KlonPfad}' klonen.", repositoryUrl, lokalerKlonPfad);
        await _gitPlugin.CloneRepositoryAsync(repositoryUrl, lokalerKlonPfad, ct);

        string branchName;

        // Prüfen ob ein vorhandener Remote-Branch genutzt werden soll
        var defaultBranch = await _gitPlugin.GetDefaultBranchAsync(repositoryUrl, ct);
        var nutzeExistierendenBranch = !string.IsNullOrEmpty(basisBranchName)
            && !string.Equals(basisBranchName, defaultBranch, StringComparison.OrdinalIgnoreCase);

        if (nutzeExistierendenBranch)
        {
            // Vorhandenen Remote-Branch auschecken
            _logger.LogInformation("Wechsle zu vorhandenem Branch '{BasisBranch}'.", basisBranchName);
            await _gitPlugin.CheckoutRemoteBranchAsync(lokalerKlonPfad, basisBranchName!, ct);
            branchName = basisBranchName!;
        }
        else
        {
            // Neuen task-Branch anlegen
            var titelSlug = ErstelleTitelSlug(aufgabe.Titel);
            branchName = $"task/{aufgabeId:N}-{titelSlug}";

            _logger.LogInformation("Branch '{BranchName}' anlegen.", branchName);
            await _gitPlugin.CreateBranchAsync(lokalerKlonPfad, branchName, ct);
        }

        // Agentenpaket deployen, wenn vorhanden (Kompatibilität wurde bereits vor dem Klonen geprüft)
        if (!string.IsNullOrEmpty(aufgabe.AgentenpaketName))
        {
            var paket = await _agentPackageService.GetPackageAsync(aufgabe.AgentenpaketName, ct);
            if (paket is not null)
            {
                _logger.LogInformation("Agentenpaket '{PaketName}' deployen.", aufgabe.AgentenpaketName);
                await _kiPlugin.DeployAgentPackageAsync(paket.Pfad, lokalerKlonPfad, ct);
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

        await _protokollService.AddEintragAsync(aufgabeId, ProtokollTyp.GitAktion, protokollNachricht, ct: ct);

        _logger.LogInformation("Entwicklungsprozess für Aufgabe {AufgabeId} erfolgreich gestartet.", aufgabeId);
    }

    /// <summary>Startet einen KI-Lauf und streamt die Ausgabe.</summary>
    /// <remarks>RACE CONDITION SCHUTZ: Wirft Exception wenn Status bereits KiAktiv.</remarks>
    public async IAsyncEnumerable<string> KiStartenAsync(
        Guid aufgabeId,
        string prompt,
        AgentInfo agent,
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
        var finalPrompt = prompt;
        var contextFilePath = Path.Combine(aufgabe.LokalerKlonPfad, $"{aufgabeId}.copilot.context.md");

        if (kontextmodus is not null)
        {
            try
            {
                finalPrompt = await BuildFollowPromptWithContextAsync(
                    aufgabeId,
                    prompt,
                    agent,
                    aufgabe.LokalerKlonPfad,
                    contextFilePath,
                    runId,
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
            $"[RunId:{runId}] {(kontextmodus is null ? "Initialprompt" : $"Kontextmodus={kontextmodus}")}\n{finalPrompt}",
            agent.Name,
            ct);
        await _aufgabeService.KiAktiviertAsync(aufgabeId, ct);

        var vollstaendigeAntwort = new StringBuilder();
        Exception? fehler = null;

        // yield return inside a try-catch is not allowed in C#.
        // Workaround: manually advance the enumerator and catch MoveNextAsync separately.
        var enumerator = _kiPlugin.StartDevelopmentAsync(finalPrompt, agent, aufgabe.LokalerKlonPfad, model, ct)
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
                    break;

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

            if (kontextmodus is not null)
            {
                var contextEventId = Guid.NewGuid();
                var responseContent = fehler is null
                    ? vollstaendigeAntwort.ToString().Trim()
                    : $"Fehler: {fehler.Message}";
                var contextEntry = BuildContextEntry(runId, contextEventId, kontextmodus.Value, prompt, responseContent, fehler is not null);
                await AppendContextEntryAsync(contextFilePath, contextEntry, CancellationToken.None);
            }
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

        await _protokollService.AddEintragAsync(
            aufgabeId,
            ProtokollTyp.GitAktion,
            "Pull: Änderungen vom Remote geholt.",
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

        var testResult = await _kiPlugin.RunTestsAsync(aufgabe.LokalerKlonPfad, ct);
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
    public async Task<IEnumerable<string>> GetRemoteBranchesAsync(string repositoryUrl, CancellationToken ct = default)
    {
        _logger.LogInformation("Remote-Branches für {RepositoryUrl} abrufen.", repositoryUrl);
        return await _gitPlugin.GetRemoteBranchesAsync(repositoryUrl, ct);
    }

    private async Task<string> BuildFollowPromptWithContextAsync(
        Guid aufgabeId,
        string userPrompt,
        AgentInfo agent,
        string localRepoPath,
        string contextFilePath,
        Guid runId,
        FolgeanweisungsKontextmodus kontextmodus,
        string? model,
        CancellationToken ct)
    {
        var contextEventId = Guid.NewGuid();

        if (kontextmodus == FolgeanweisungsKontextmodus.KontextNeuBeginnen)
        {
            var resetHeader = $"# Kontextverlauf Aufgabe {aufgabeId}\n\nReset durch Folgeanweisung.\nRunId: {runId}\nContextEventId: {contextEventId}\nZeit: {DateTimeOffset.UtcNow:O}\n";
            await WriteTextAtomicallyWithBackupAsync(contextFilePath, resetHeader, ct);
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

        var context = await ReadFileTextSafeAsync(contextFilePath, ct);
        context = await EnsureContextWithinLimitsAsync(
            aufgabeId,
            context,
            contextFilePath,
            agent,
            localRepoPath,
            runId,
            model,
            ct);

        await _protokollService.AddEintragAsync(
            aufgabeId,
            ProtokollTyp.StatusUebergang,
            $"[RunId:{runId}][ContextEventId:{contextEventId}] Folgeanweisung mit Kontextpräfix gesendet.",
            agent.Name,
            ct);

        if (string.IsNullOrWhiteSpace(context))
        {
            return userPrompt;
        }

        return $"{context.TrimEnd()}\n\n---\n\n{userPrompt}";
    }

    private async Task<string> EnsureContextWithinLimitsAsync(
        Guid aufgabeId,
        string context,
        string contextFilePath,
        AgentInfo agent,
        string localRepoPath,
        Guid runId,
        string? model,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(context))
        {
            return string.Empty;
        }

        var softLimit = _configuration.GetValue<int?>("KiKontext:SoftLimitChars") ?? DefaultContextCompressionSoftLimit;
        var hardLimit = _configuration.GetValue<int?>("KiKontext:HardLimitChars") ?? DefaultContextCompressionHardLimit;

        var current = context;
        if (current.Length > softLimit)
        {
            var compressionEventId = Guid.NewGuid();
            await _protokollService.AddEintragAsync(
                aufgabeId,
                ProtokollTyp.StatusUebergang,
                $"[RunId:{runId}][ContextEventId:{compressionEventId}] Soft-Limit ({softLimit}) überschritten, starte KI-Komprimierung.",
                agent.Name,
                ct);

            current = await CompressContextAsync(current, agent, localRepoPath, model, ct);
            await WriteTextAtomicallyWithBackupAsync(contextFilePath, current, ct);

            await _protokollService.AddEintragAsync(
                aufgabeId,
                ProtokollTyp.StatusUebergang,
                $"[RunId:{runId}][ContextEventId:{compressionEventId}] Kontext komprimiert auf {current.Length} Zeichen.",
                agent.Name,
                ct);
        }

        if (current.Length > hardLimit)
        {
            throw new InvalidOperationException(
                $"Kontextdatei überschreitet das Hard-Limit ({hardLimit} Zeichen) trotz Komprimierung. " +
                "Bitte mit 'Kontext ignorieren' fortsetzen oder 'Kontext neu beginnen'.");
        }

        return current;
    }

    private async Task<string> CompressContextAsync(
        string context,
        AgentInfo agent,
        string localRepoPath,
        string? model,
        CancellationToken ct)
    {
        var compressionPrompt = $"""
            Komprimiere den folgenden Projektkontext in eine knappe, strukturierte Markdown-Zusammenfassung.
            Gib ausschließlich Markdown zurück, ohne Einleitung und ohne Codeblock.
            Pflichtabschnitte:
            - Ziel
            - Offene Punkte
            - Letzte Entscheidungen
            - Relevante Randbedingungen

            Kontext:
            {context}
            """;

        var builder = new StringBuilder();
        await foreach (var line in _kiPlugin.StartDevelopmentAsync(compressionPrompt, agent, localRepoPath, model, ct))
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
        FolgeanweisungsKontextmodus kontextmodus,
        string userPrompt,
        string response,
        bool isError)
        => $"""
            ## Verlaufseintrag
            - RunId: {runId}
            - ContextEventId: {contextEventId}
            - Modus: {kontextmodus}
            - Zeit: {DateTimeOffset.UtcNow:O}
            - Status: {(isError ? "Fehler" : "Erfolgreich")}

            ### Nutzeranweisung
            {userPrompt}

            ### KI-Rückmeldung
            {response}

            """;

    private async Task AppendContextEntryAsync(string contextFilePath, string entry, CancellationToken ct)
    {
        var current = await ReadFileTextSafeAsync(contextFilePath, ct);
        var next = string.IsNullOrWhiteSpace(current)
            ? entry
            : $"{current.TrimEnd()}\n\n{entry}";

        await WriteTextAtomicallyWithBackupAsync(contextFilePath, next, ct);
    }

    private static async Task<string> ReadFileTextSafeAsync(string path, CancellationToken ct)
    {
        if (!File.Exists(path))
        {
            return string.Empty;
        }

        try
        {
            return await File.ReadAllTextAsync(path, ct);
        }
        catch
        {
            var backupPath = $"{path}.bak";
            if (File.Exists(backupPath))
            {
                return await File.ReadAllTextAsync(backupPath, ct);
            }

            throw;
        }
    }

    private static async Task WriteTextAtomicallyWithBackupAsync(string targetPath, string content, CancellationToken ct)
    {
        var directory = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = $"{targetPath}.tmp";
        var backupPath = $"{targetPath}.bak";

        if (File.Exists(targetPath))
        {
            File.Copy(targetPath, backupPath, overwrite: true);
        }

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
                File.Replace(tempPath, targetPath, backupPath, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(tempPath, targetPath, overwrite: true);
            }
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
