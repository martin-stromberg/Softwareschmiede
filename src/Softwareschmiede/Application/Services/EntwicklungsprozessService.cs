using System.Globalization;
using Microsoft.Extensions.Logging;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Infrastructure.Data;

namespace Softwareschmiede.Application.Services;

/// <summary>
/// Koordiniert Git-Repository-Setup für Aufgaben und Rate-Limit-Marker-Erkennung.
/// </summary>
public sealed class EntwicklungsprozessService
{
    private readonly AufgabeService _aufgabeService;
    private readonly ProtokollService _protokollService;
    private readonly ProjektService? _projektService;
    private readonly IGitPlugin _gitPlugin;
    private readonly PluginSelectionService _pluginSelectionService;
    private readonly IArbeitsverzeichnisResolver _arbeitsverzeichnisResolver;
    private readonly RepositoryStartskriptService? _repositoryStartskriptService;
    private readonly ILogger<EntwicklungsprozessService> _logger;

    private const string RateLimitMarkerPrefix = "[[SOFTWARESCHMIEDE_RATE_LIMIT";

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
            null,
            gitPlugin,
            pluginSelectionService,
            arbeitsverzeichnisResolver,
            null,
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
        IArbeitsverzeichnisResolver arbeitsverzeichnisResolver,
        RepositoryStartskriptService? repositoryStartskriptService,
        ILogger<EntwicklungsprozessService> logger)
    {
        _aufgabeService = aufgabeService;
        _protokollService = protokollService;
        _projektService = projektService;
        _gitPlugin = gitPlugin;
        _pluginSelectionService = pluginSelectionService;
        _arbeitsverzeichnisResolver = arbeitsverzeichnisResolver;
        _repositoryStartskriptService = repositoryStartskriptService;
        _logger = logger;
    }

    /// <summary>
    /// Richtet das Git-Repository für eine Aufgabe ein: Klon, Branch, optionales Startskript.
    /// Setzt den Status auf <see cref="AufgabeStatus.ArbeitsverzeichnisEingerichtet"/>.
    /// </summary>
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
        var resolvedPluginPrefix = !string.IsNullOrWhiteSpace(repository.PluginTyp)
            ? repository.PluginTyp
            : selectedScmPluginPrefix;
        var gitPlugin = await _pluginSelectionService.ResolveSourceCodeManagementPluginAsync(resolvedPluginPrefix, ct);

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

        _logger.LogInformation("Repository '{RepositoryUrl}' nach '{KlonPfad}' klonen.", repository.RepositoryUrl, lokalerKlonPfad);
        await gitPlugin.CloneRepositoryAsync(repository.RepositoryUrl, lokalerKlonPfad, ct);

        string branchName;
        var nutzeExistierendenBranch = false;
        if (!string.IsNullOrEmpty(basisBranchName))
        {
            var defaultBranch = await gitPlugin.GetDefaultBranchAsync(repository.RepositoryUrl, ct);
            nutzeExistierendenBranch = !string.Equals(basisBranchName, defaultBranch, StringComparison.OrdinalIgnoreCase);
        }

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
                    $"Hinweis: Das Repository-Startskript konnte nicht ausgeführt werden ({ex.Message}).";
                _logger.LogWarning(ex, "Repository-Startskript für Aufgabe {AufgabeId} ist fehlgeschlagen.", aufgabeId);
            }
        }

        await _aufgabeService.StartenAsync(aufgabeId, branchName, lokalerKlonPfad, ct);

        var protokollNachricht = nutzeExistierendenBranch
            ? $"Klon angelegt, vorhandener Branch ausgecheckt: {branchName} in {lokalerKlonPfad}"
            : $"Klon und Branch angelegt: {branchName} in {lokalerKlonPfad}";
        if (!string.IsNullOrWhiteSpace(startskriptHinweis))
        {
            protokollNachricht = $"{protokollNachricht}\n{startskriptHinweis}";
        }

        await _protokollService.AddEintragAsync(aufgabeId, ProtokollTyp.GitAktion, protokollNachricht, ct: ct);

        _logger.LogInformation("Repository-Setup für Aufgabe {AufgabeId} abgeschlossen.", aufgabeId);
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

    /// <summary>Setzt Commits zurück.</summary>
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
    public async Task<IEnumerable<string>> GetRemoteBranchesAsync(string repositoryUrl, string? selectedScmPluginPrefix = null, CancellationToken ct = default)
    {
        var gitPlugin = await _pluginSelectionService.ResolveSourceCodeManagementPluginAsync(selectedScmPluginPrefix, ct);
        return await gitPlugin.GetRemoteBranchesAsync(repositoryUrl, ct);
    }

    /// <summary>
    /// Parst einen Rate-Limit-Marker aus einer CLI-Ausgabezeile.
    /// Format: <c>[[SOFTWARESCHMIEDE_RATE_LIMIT:ISO8601_DATETIME]]</c>
    /// </summary>
    public static bool TryParseRateLimitSuggestion(string outputLine, out SuggestionInfo? suggestion)
    {
        suggestion = null;

        if (string.IsNullOrWhiteSpace(outputLine))
        {
            return false;
        }

        var startIndex = outputLine.IndexOf(RateLimitMarkerPrefix, StringComparison.Ordinal);
        if (startIndex < 0)
        {
            return false;
        }

        var markerStart = startIndex + RateLimitMarkerPrefix.Length;
        var endIndex = outputLine.IndexOf("]]", markerStart, StringComparison.Ordinal);
        if (endIndex < 0)
        {
            return false;
        }

        var payload = outputLine[markerStart..endIndex];

        DateTimeOffset? resetUtc = null;
        if (payload.StartsWith(":", StringComparison.Ordinal))
        {
            var timestampRaw = payload[1..].Trim();
            if (DateTimeOffset.TryParse(
                    timestampRaw,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var parsed))
            {
                resetUtc = parsed;
            }
        }

        suggestion = new SuggestionInfo(resetUtc);
        return true;
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

    private static void DeleteDirectoryForce(string path)
    {
        foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(path, recursive: true);
    }
}

/// <summary>Information zu einem erkannten Rate-Limit-Marker.</summary>
public sealed record SuggestionInfo(DateTimeOffset? AusfuehrenAbUtc);
