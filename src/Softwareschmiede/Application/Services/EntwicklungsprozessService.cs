using System.Runtime.CompilerServices;
using System.Text;
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
    private readonly ILogger<EntwicklungsprozessService> _logger;

    /// <inheritdoc cref="EntwicklungsprozessService"/>
    public EntwicklungsprozessService(
        AufgabeService aufgabeService,
        ProtokollService protokollService,
        IGitPlugin gitPlugin,
        IKiPlugin kiPlugin,
        IAgentPackageService agentPackageService,
        IArbeitsverzeichnisResolver arbeitsverzeichnisResolver,
        ILogger<EntwicklungsprozessService> logger)
    {
        _aufgabeService = aufgabeService;
        _protokollService = protokollService;
        _gitPlugin = gitPlugin;
        _kiPlugin = kiPlugin;
        _agentPackageService = agentPackageService;
        _arbeitsverzeichnisResolver = arbeitsverzeichnisResolver;
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
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var aufgabe = await _aufgabeService.GetByIdAsync(aufgabeId, ct)
            ?? throw new InvalidOperationException($"Aufgabe {aufgabeId} nicht gefunden.");

        if (aufgabe.Status == AufgabeStatus.KiAktiv)
            throw new InvalidOperationException("KI ist bereits aktiv für diese Aufgabe.");

        if (string.IsNullOrEmpty(aufgabe.LokalerKlonPfad))
            throw new InvalidOperationException($"Aufgabe {aufgabeId} hat keinen lokalen Klonpfad. Bitte zuerst ProzessStartenAsync aufrufen.");

        await _protokollService.AddEintragAsync(aufgabeId, ProtokollTyp.Prompt, prompt, agent.Name, ct);
        await _aufgabeService.KiAktiviertAsync(aufgabeId, ct);

        var vollstaendigeAntwort = new StringBuilder();
        Exception? fehler = null;

        // yield return inside a try-catch is not allowed in C#.
        // Workaround: manually advance the enumerator and catch MoveNextAsync separately.
        var enumerator = _kiPlugin.StartDevelopmentAsync(prompt, agent, aufgabe.LokalerKlonPfad, model, ct)
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
                await _aufgabeService.FehlgeschlagenAsync(aufgabeId, CancellationToken.None);
                await _protokollService.AddEintragAsync(
                    aufgabeId,
                    ProtokollTyp.KiAntwort,
                    $"Fehler: {fehler.Message}",
                    agent.Name,
                    CancellationToken.None);
            }
            else
            {
                await _protokollService.AddEintragAsync(
                    aufgabeId,
                    ProtokollTyp.KiAntwort,
                    vollstaendigeAntwort.ToString(),
                    agent.Name,
                    CancellationToken.None);
                await _aufgabeService.KiAbgeschlossenAsync(aufgabeId, CancellationToken.None);
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
