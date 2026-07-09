using Microsoft.Extensions.Logging;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Infrastructure.Data;

namespace Softwareschmiede.Application.Services;

/// <summary>Optionale Abhängigkeiten für den EntwicklungsprozessService.</summary>
/// <param name="ProjektService">Optionaler Dienst zum Auflösen von Projekt-Repositories.</param>
/// <param name="RepositoryStartskriptService">Optionaler Dienst zum Ausführen von Repository-Startskripten.</param>
/// <param name="KiAusfuehrungsService">Optionaler Dienst zum Starten der KI-CLI.</param>
/// <param name="GitOrchestrationService">
/// Optionaler Dienst zur Validierung des konfigurierten Arbeitsverzeichnisses direkt nach dem Git-Klon.
/// </param>
/// <returns>Eine neue Instanz mit den angegebenen optionalen Abhängigkeiten.</returns>
public sealed record EntwicklungsprozessServiceOptions(
    ProjektService? ProjektService = null,
    RepositoryStartskriptService? RepositoryStartskriptService = null,
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
        var repository = await ResolveRepositoryAsync(aufgabe, repositoryUrl, ct);
        var gitPlugin = await ResolvePluginAsync(repository, selectedScmPluginPrefix, aufgabeId, ct);
        var lokalerKlonPfad = await PrepareCloneDirectoryAsync(gitPlugin, repository.RepositoryUrl, aufgabeId, ct);

        if (_options.GitOrchestrationService is not null)
        {
            await _options.GitOrchestrationService.ValidateWorkingDirectoryAfterCloneAsync(lokalerKlonPfad, repository.StartKonfiguration);
        }

        var (branchName, nutzeExistierendenBranch) = await SetupBranchAsync(gitPlugin, repository.RepositoryUrl, lokalerKlonPfad, basisBranchName, aufgabe, ct);
        await FinalizeStartAsync(aufgabeId, aufgabe, repository, lokalerKlonPfad, branchName, nutzeExistierendenBranch, ct);

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
            await ProzessStartenAsync(aufgabeId, repositoryUrl, basisBranchName, null, ct);

            var aufgabe = await _aufgabeService.GetByIdAsync(aufgabeId, ct)
                ?? throw new InvalidOperationException($"Aufgabe {aufgabeId} nicht gefunden.");

            if (string.IsNullOrEmpty(aufgabe.LokalerKlonPfad))
            {
                throw new InvalidOperationException($"Aufgabe {aufgabeId} hat keinen lokalen Klonpfad.");
            }

            var kiPlugin = await _pluginSelectionService.ResolveDevelopmentAutomationPluginAsync(kiPluginPrefix, ct);

            RepositoryStartKonfiguration? startConfig = null;
            if (aufgabe.GitRepositoryId is { } repositoryId && _options.ProjektService is not null)
            {
                startConfig = await _options.ProjektService.GetRepositoryStartKonfigurationAsync(repositoryId, ct);
            }

            await _options.KiAusfuehrungsService.StartWithPseudoConsoleAsync(aufgabeId, kiPlugin, aufgabe.LokalerKlonPfad, null, ct, startConfig);
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

    /// <summary>Führt einen manuellen Commit durch.</summary>
    /// <param name="aufgabeId">ID der Aufgabe.</param>
    /// <param name="message">Commit-Nachricht.</param>
    /// <param name="ct">Abbruch-Token.</param>
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

    /// <summary>Setzt Commits zurück.</summary>
    /// <param name="aufgabeId">ID der Aufgabe.</param>
    /// <param name="resetType">Reset-Typ (z. B. soft, mixed, hard).</param>
    /// <param name="targetRef">Optionaler Ziel-Ref; Standard ist HEAD.</param>
    /// <param name="ct">Abbruch-Token.</param>
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
    }

    /// <summary>Pusht den Branch auf den Remote.</summary>
    /// <param name="aufgabeId">ID der Aufgabe.</param>
    /// <param name="ct">Abbruch-Token.</param>
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
    }

    /// <summary>Holt Änderungen vom Remote.</summary>
    /// <param name="aufgabeId">ID der Aufgabe.</param>
    /// <param name="ct">Abbruch-Token.</param>
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

        return pullRequest;
    }

    /// <summary>Schließt die Aufgabe ab: Klon löschen, Status auf Beendet setzen.</summary>
    /// <param name="aufgabeId">ID der Aufgabe.</param>
    /// <param name="ct">Abbruch-Token.</param>
    public async Task AbschliessenAsync(Guid aufgabeId, CancellationToken ct = default)
    {
        _logger.LogInformation("Aufgabe {AufgabeId} abschließen.", aufgabeId);

        var aufgabe = await _aufgabeService.GetByIdAsync(aufgabeId, ct)
            ?? throw new InvalidOperationException($"Aufgabe {aufgabeId} nicht gefunden.");

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
    public async Task<StartskriptErgebnis> RepositoryStartskriptAusfuehrenAsync(Guid aufgabeId, CancellationToken ct = default)
    {
        if (_options.RepositoryStartskriptService is null)
        {
            return new StartskriptErgebnis("Startskript-Dienst ist nicht konfiguriert.");
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
            return new StartskriptErgebnis("Kein aktives Startskript konfiguriert.");
        }

        await _options.RepositoryStartskriptService.RunAsync(aufgabe.LokalerKlonPfad, repository.StartKonfiguration, ct);
        return new StartskriptErgebnis("Startskript erfolgreich ausgeführt.");
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

        await _aufgabeService.StatusSetzenAsync(aufgabeId, AufgabeStatus.Neu, ct);
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
        var lokalerKlonPfad = Path.Combine(workdirResult.ResolvedPath, "softwareschmiede", aufgabeId.ToString());

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

    private async Task<(string BranchName, bool NutzeExistierendenBranch)> SetupBranchAsync(
        IGitPlugin gitPlugin,
        string repositoryUrl,
        string lokalerKlonPfad,
        string? basisBranchName,
        Aufgabe aufgabe,
        CancellationToken ct)
    {
        var nutzeExistierendenBranch = false;
        if (!string.IsNullOrEmpty(basisBranchName))
        {
            var defaultBranch = await gitPlugin.GetDefaultBranchAsync(repositoryUrl, ct);
            nutzeExistierendenBranch = !string.Equals(basisBranchName, defaultBranch, StringComparison.OrdinalIgnoreCase);
        }

        string branchName;
        if (nutzeExistierendenBranch)
        {
            _logger.LogInformation("Wechsle zu vorhandenem Branch '{BasisBranch}'.", basisBranchName);
            await gitPlugin.CheckoutRemoteBranchAsync(lokalerKlonPfad, basisBranchName!, ct);
            branchName = basisBranchName!;
        }
        else
        {
            branchName = ErstelleTaskBranchName(aufgabe);
            _logger.LogInformation("Branch '{BranchName}' anlegen.", branchName);
            await gitPlugin.CreateBranchAsync(lokalerKlonPfad, branchName, ct);
        }

        return (branchName, nutzeExistierendenBranch);
    }

    private async Task FinalizeStartAsync(
        Guid aufgabeId,
        Aufgabe aufgabe,
        GitRepository repository,
        string lokalerKlonPfad,
        string branchName,
        bool nutzeExistierendenBranch,
        CancellationToken ct)
    {
        string? startskriptHinweis = null;
        if (repository.StartKonfiguration is not null && _options.RepositoryStartskriptService is not null)
        {
            try
            {
                await _options.RepositoryStartskriptService.RunAsync(lokalerKlonPfad, repository.StartKonfiguration, ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                startskriptHinweis =
                    $"Hinweis: Das Repository-Startskript konnte nicht ausgeführt werden ({ex.Message}).";
                _logger.LogWarning(ex, "Repository-Startskript für Aufgabe {AufgabeId} ist fehlgeschlagen.", aufgabeId);
            }
        }

        await CreateIssueFileAsync(lokalerKlonPfad, aufgabe, branchName, ct);
        await UpdateGitignoreAsync(lokalerKlonPfad, ct);

        await _aufgabeService.StartenAsync(aufgabeId, branchName, lokalerKlonPfad, ct);

        var protokollNachricht = nutzeExistierendenBranch
            ? $"Klon angelegt, vorhandener Branch ausgecheckt: {branchName} in {lokalerKlonPfad}"
            : $"Klon und Branch angelegt: {branchName} in {lokalerKlonPfad}";
        if (!string.IsNullOrWhiteSpace(startskriptHinweis))
        {
            protokollNachricht = $"{protokollNachricht}\n{startskriptHinweis}";
        }

        await _protokollService.AddEintragAsync(aufgabeId, ProtokollTyp.GitAktion, protokollNachricht, ct: ct);
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

    private async Task CreateIssueFileAsync(string lokalerKlonPfad, Aufgabe aufgabe, string branchName, CancellationToken ct)
    {
        try
        {
            var beschreibung = string.IsNullOrWhiteSpace(aufgabe.AnforderungsBeschreibung)
                ? "[Keine Anforderungsbeschreibung verfügbar]"
                : aufgabe.AnforderungsBeschreibung;

            var inhalt = $"""
                # Aufgabe: {aufgabe.Titel}

                **Aufgaben-ID:** {aufgabe.Id}
                **Branch:** {branchName}
                **Erstellt:** {aufgabe.ErstellungsDatum:yyyy-MM-dd}

                ## Anforderung

                {beschreibung}
                """;

            var issueFilePath = Path.Combine(lokalerKlonPfad, "issue.md");
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

    private async Task UpdateGitignoreAsync(string lokalerKlonPfad, CancellationToken ct)
    {
        try
        {
            var gitignorePath = Path.Combine(lokalerKlonPfad, ".gitignore");
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
            _logger.LogInformation(".gitignore für '{KlonPfad}' aktualisiert: 'issue.md' eingetragen.", lokalerKlonPfad);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Fehler beim Aktualisieren von .gitignore in '{KlonPfad}'.", lokalerKlonPfad);
        }
    }

    private static void DeleteDirectoryForce(string path)
    {
        foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(path, recursive: true);
    }
}

/// <summary>Ergebnis der manuellen Ausführung eines Repository-Startskripts.</summary>
/// <param name="Message">Meldung über das Ergebnis der Ausführung.</param>
/// <returns>Eine neue Instanz mit der angegebenen Meldung.</returns>
public sealed record StartskriptErgebnis(string Message);
