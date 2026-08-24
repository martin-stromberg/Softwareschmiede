using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Infrastructure.Data;

namespace Softwareschmiede.Application.Services;

/// <summary>Orchestriert die Erstellung des Arbeitsverzeichnisses, des Repository-Klons und der Initialisierung von state.json für eine Autonome Aufgabe.</summary>
public sealed class AutonomAufgabenInitialisierungsService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly SoftwareschmiededDbContext _db;
    private readonly ICliRunner _cliRunner;
    private readonly PluginSelectionService _pluginSelectionService;
    private readonly AutonomAufgabenOptions _options;
    private readonly ILogger<AutonomAufgabenInitialisierungsService> _logger;

    /// <inheritdoc cref="AutonomAufgabenInitialisierungsService"/>
    public AutonomAufgabenInitialisierungsService(
        SoftwareschmiededDbContext db,
        ICliRunner cliRunner,
        PluginSelectionService pluginSelectionService,
        IOptions<AutonomAufgabenOptions> options,
        ILogger<AutonomAufgabenInitialisierungsService> logger)
    {
        _db = db;
        _cliRunner = cliRunner;
        _pluginSelectionService = pluginSelectionService;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>Erstellt das Arbeitsverzeichnis, erzeugt den Repository-Klon und initialisiert state.json und permissions.json für eine Autonome Aufgabe.</summary>
    public async Task<AutonomAufgabeKonfiguration> InitialisiereAsync(Aufgabe aufgabe, AutonomAufgabeInitialisierungsAnfrage anfrage, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(aufgabe);
        ArgumentNullException.ThrowIfNull(anfrage);
        ValidiereAnfrage(anfrage);

        var gitPlugin = await _pluginSelectionService.ResolveSourceCodeManagementPluginAsync(aufgabe.GitRepository?.PluginTyp, ct);

        await ErstelleArbeitsverzeichnisStrukturAsync(anfrage.ArbeitsverzeichnisPfad, ct);

        var repoMainPfad = Path.Combine(anfrage.ArbeitsverzeichnisPfad, "clones", "repo_main");
        await KloneHauptRepositoryAsync(gitPlugin, aufgabe, repoMainPfad, ct);
        await ErstelleProjektbranchAsync(gitPlugin, aufgabe, repoMainPfad, anfrage.ProjektBranchName, ct);

        var permissionsPfad = Path.Combine(anfrage.ArbeitsverzeichnisPfad, "permissions.json");
        var stateJsonPfad = Path.Combine(anfrage.ArbeitsverzeichnisPfad, "state.json");
        await DirectoryAccessGuard.AusfuehrenAsync(anfrage.ArbeitsverzeichnisPfad, async () =>
        {
            if (anfrage.PermissionsQuelle == PermissionsJsonOption.Generieren || !File.Exists(permissionsPfad))
            {
                await File.WriteAllTextAsync(permissionsPfad, BuildPermissionsJson(anfrage), ct);
            }

            await File.WriteAllTextAsync(stateJsonPfad, BuildStateJson(aufgabe, anfrage), ct);
        });

        var konfiguration = new AutonomAufgabeKonfiguration
        {
            Id = Guid.NewGuid(),
            AufgabeId = aufgabe.Id,
            ProjektBranchName = anfrage.ProjektBranchName,
            InitialPrompt = anfrage.InitialPrompt,
            PermissionsJsonPfad = permissionsPfad,
            RessourcenLimits = anfrage.RessourcenLimits,
            PersistenzModus = anfrage.PersistenzModus,
            SkillAutogeneration = anfrage.SkillAutogeneration,
            ArbeitsverzeichnisPfad = anfrage.ArbeitsverzeichnisPfad
        };

        _db.AutonomAufgabeKonfigurationen.Add(konfiguration);
        SicherstelleAufgabeGetrackt(aufgabe);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Autonome Aufgabe {AufgabeId} initialisiert (Arbeitsverzeichnis: {Pfad}).",
            aufgabe.Id,
            anfrage.ArbeitsverzeichnisPfad);

        return konfiguration;
    }

    /// <summary>Erstellt die Verzeichnisstruktur mit plan.md, progress.md, governance.md und den Subdirectories skills/, clones/, tasks/, logs/. state.json und permissions.json werden ausschließlich von <see cref="InitialisiereAsync(Aufgabe,AutonomAufgabeInitialisierungsAnfrage,CancellationToken)"/> geschrieben, da sie anfragespezifische Werte enthalten.</summary>
    public async Task ErstelleArbeitsverzeichnisStrukturAsync(string arbeitsverzeichnisPfad, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(arbeitsverzeichnisPfad);
        if (!Path.IsPathRooted(arbeitsverzeichnisPfad))
        {
            throw new ArgumentException("ArbeitsverzeichnisPfad muss ein absoluter Pfad sein.", nameof(arbeitsverzeichnisPfad));
        }

        await DirectoryAccessGuard.AusfuehrenAsync(arbeitsverzeichnisPfad, async () =>
        {
            Directory.CreateDirectory(arbeitsverzeichnisPfad);
            Directory.CreateDirectory(Path.Combine(arbeitsverzeichnisPfad, "skills"));
            Directory.CreateDirectory(Path.Combine(arbeitsverzeichnisPfad, "skills", "archive"));
            Directory.CreateDirectory(Path.Combine(arbeitsverzeichnisPfad, "clones"));
            Directory.CreateDirectory(Path.Combine(arbeitsverzeichnisPfad, "tasks"));
            Directory.CreateDirectory(Path.Combine(arbeitsverzeichnisPfad, "logs"));

            var planPfad = Path.Combine(arbeitsverzeichnisPfad, "plan.md");
            if (!File.Exists(planPfad))
            {
                await File.WriteAllTextAsync(planPfad, "# Plan\n\nNoch keine Teilaufgaben geplant.\n", ct);
            }

            var progressPfad = Path.Combine(arbeitsverzeichnisPfad, "progress.md");
            if (!File.Exists(progressPfad))
            {
                await File.WriteAllTextAsync(progressPfad, "# Fortschritt\n\nNoch kein Fortschritt protokolliert.\n", ct);
            }

            var governancePfad = Path.Combine(arbeitsverzeichnisPfad, "governance.md");
            if (!File.Exists(governancePfad))
            {
                await File.WriteAllTextAsync(governancePfad, BuildGovernanceMarkdown(), ct);
            }
        });
    }

    /// <summary>
    /// Stellt sicher, dass <paramref name="aufgabe"/> im <see cref="_db"/>-ChangeTracker getrackt ist, damit das
    /// EF-Relationship-Fixup zwischen <see cref="AutonomAufgabeKonfiguration.AufgabeId"/> und
    /// <see cref="Aufgabe.AutonomKonfiguration"/> greift — auch dann, wenn die aufrufende Seite eine
    /// nicht-getrackte <see cref="Aufgabe"/>-Instanz übergeben hat.
    /// </summary>
    /// <param name="aufgabe">Die sicherzustellende Aufgabe.</param>
    private void SicherstelleAufgabeGetrackt(Aufgabe aufgabe)
    {
        var verfolgteAufgabe = _db.ChangeTracker.Entries<Aufgabe>().Select(e => e.Entity).FirstOrDefault(a => a.Id == aufgabe.Id);
        if (verfolgteAufgabe is null)
        {
            _db.Attach(aufgabe);
        }
    }

    private async Task KloneHauptRepositoryAsync(IGitPlugin gitPlugin, Aufgabe aufgabe, string zielPfad, CancellationToken ct)
    {
        var repositoryUrl = aufgabe.GitRepository?.RepositoryUrl;
        if (string.IsNullOrWhiteSpace(repositoryUrl))
        {
            throw new InvalidOperationException(
                $"Aufgabe {aufgabe.Id} besitzt kein verknüpftes Git-Repository mit RepositoryUrl; Repository-Klon für die Autonome Aufgabe kann nicht erstellt werden.");
        }

        // Macht InitialisiereAsync retry-sicher: Schlägt der nachfolgende ErstelleProjektbranchAsync-Schritt fehl
        // (z. B. weil "git checkout -b" wegen eines darunterliegenden Git-Fehlers fehlschlägt), bleibt zielPfad
        // bereits geklont zurück. Ein erneuter Klick auf "Initialisieren" (derselbe, deterministische
        // ArbeitsverzeichnisPfad) darf dann nicht erneut klonen, da IGitPlugin.CloneRepositoryAsync gegen ein
        // bereits nicht-leeres Zielverzeichnis fehlschlägt.
        if (Directory.Exists(zielPfad) && Directory.EnumerateFileSystemEntries(zielPfad).Any())
        {
            _logger.LogInformation("Repository-Klon existiert bereits unter {ZielPfad}, überspringe Klonvorgang.", zielPfad);
            return;
        }

        try
        {
            await gitPlugin.CloneRepositoryAsync(repositoryUrl, zielPfad, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Repository-Klon nach '{zielPfad}' fehlgeschlagen: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Legt <paramref name="projektBranchName"/> im bereits geklonten Hauptrepository (<paramref name="repoMainPfad"/>)
    /// an und checkt ihn dort auch aus: existiert der Branch bereits remote, wird er per
    /// <see cref="IGitPlugin.CheckoutRemoteBranchAsync"/> ausgecheckt; andernfalls wird er per
    /// <see cref="IGitPlugin.CreateBranchAsync"/> lokal neu erstellt (macht per "git checkout -b" zugleich den
    /// Checkout). Beide Zweige stellen damit sicher, dass <paramref name="repoMainPfad"/> danach tatsächlich auf
    /// <paramref name="projektBranchName"/> steht — das ist der eigentliche Zweck dieses Schritts, da nachfolgend
    /// angelegte Unteragenten-Branches (siehe <c>UnteragentGitProvisioningService</c>) implizit von der aktuellen
    /// HEAD von <paramref name="repoMainPfad"/> abzweigen. Idempotent (retry-sicher): existiert der lokale Branch
    /// bereits (z. B. weil ein vorheriger Initialisierungsversuch nach der Branch-Anlage, aber vor Abschluss
    /// fehlgeschlagen ist), wird die Neuanlage übersprungen.
    /// </summary>
    private async Task ErstelleProjektbranchAsync(IGitPlugin gitPlugin, Aufgabe aufgabe, string repoMainPfad, string projektBranchName, CancellationToken ct)
    {
        try
        {
            var remoteBranches = await LadeRemoteBranchesAsync(gitPlugin, aufgabe.GitRepository?.RepositoryUrl, ct);
            if (remoteBranches.Contains(projektBranchName, StringComparer.OrdinalIgnoreCase))
            {
                await gitPlugin.CheckoutRemoteBranchAsync(repoMainPfad, projektBranchName, ct);
                return;
            }

            if (await LokalerBranchExistiertBereitsAsync(gitPlugin, repoMainPfad, projektBranchName, ct))
            {
                _logger.LogInformation(
                    "Lokaler Branch {BranchName} existiert in {RepoPfad} bereits, überspringe erneute Anlage (Retry-Fall).",
                    projektBranchName,
                    repoMainPfad);
                return;
            }

            // repoMainPfad wird unverändert (nicht manuell aufgelöst) übergeben: CreateBranchAsync löst den
            // tatsächlichen Repository-Pfad intern selbst auf (siehe LocalDirectoryPlugin.CreateBranchAsync,
            // dasselbe Muster wie CheckoutRemoteBranchAsync oben) und checkt den neuen Branch per
            // "git checkout -b" zugleich aus.
            await gitPlugin.CreateBranchAsync(repoMainPfad, projektBranchName, sourceBranchName: null, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Branch '{projektBranchName}' konnte nicht angelegt werden: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Prüft, ob <paramref name="branchName"/> im tatsächlich aufgelösten Repository-Pfad unter
    /// <paramref name="repoPfad"/> bereits als lokaler Branch existiert (via <c>git branch --list</c>). Macht
    /// die Branch-Neuanlage in <see cref="ErstelleProjektbranchAsync"/> retry-sicher, analog zum
    /// Klon-Idempotenz-Guard in <see cref="KloneHauptRepositoryAsync"/>.
    /// </summary>
    private async Task<bool> LokalerBranchExistiertBereitsAsync(IGitPlugin gitPlugin, string repoPfad, string branchName, CancellationToken ct)
    {
        var effektiverRepoPfad = await gitPlugin.ResolveEffectiveRepositoryPathAsync(repoPfad, ct);
        var ergebnis = await _cliRunner.RunAsync("git", ["branch", "--list", branchName], effektiverRepoPfad, null, ct);
        return ergebnis.IsSuccess && !string.IsNullOrWhiteSpace(ergebnis.StdOut);
    }

    /// <summary>
    /// Lädt die Remote-Branches des Repositories der Aufgabe. Unterstützt das Plugin keine Remote-Branches
    /// (z. B. <c>LocalDirectoryPlugin</c>, <see cref="NotSupportedException"/>), wird eine leere Liste
    /// zurückgegeben, sodass <see cref="ErstelleProjektbranchAsync"/> stets den lokalen Neuanlage-Pfad wählt.
    /// </summary>
    private async Task<IEnumerable<string>> LadeRemoteBranchesAsync(IGitPlugin gitPlugin, string? repositoryUrl, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(repositoryUrl))
        {
            return [];
        }

        try
        {
            return await gitPlugin.GetRemoteBranchesAsync(repositoryUrl, ct);
        }
        catch (NotSupportedException)
        {
            return [];
        }
    }

    private string BuildPermissionsJson(AutonomAufgabeInitialisierungsAnfrage anfrage)
    {
        var permissions = new
        {
            allowed_actions = new[]
            {
                "read_files", "write_files_in_task_dir", "git_commit_in_feature_branch",
                "run_tests", "create_skill", "spawn_subagent", "manage_skills"
            },
            limits = new
            {
                max_subagents = _options.MaxConcurrentUnteragenten,
                max_clones = _options.MaxClones,
                max_feature_branches = _options.MaxFeatureBranches,
                token_budget = anfrage.RessourcenLimits.TokenBudget,
                net_runtime_minutes = anfrage.RessourcenLimits.LaufzeitLimitMinuten
            },
            persistence = new
            {
                mode = anfrage.PersistenzModus.ToString(),
                auto_resume = true
            }
        };

        return JsonSerializer.Serialize(permissions, JsonOptions);
    }

    private string BuildStateJson(Aufgabe aufgabe, AutonomAufgabeInitialisierungsAnfrage anfrage)
    {
        var state = new
        {
            task_id = aufgabe.Id,
            project_branch = anfrage.ProjektBranchName,
            initial_prompt = anfrage.InitialPrompt,
            permissions = "permissions.json",
            runtime = new
            {
                started_utc = DateTimeOffset.UtcNow,
                net_minutes_used = 0,
                net_minutes_limit = anfrage.RessourcenLimits.LaufzeitLimitMinuten,
                paused_utc = (DateTimeOffset?)null
            },
            governance = new
            {
                max_subagents = _options.MaxConcurrentUnteragenten,
                max_clones = _options.MaxClones,
                max_feature_branches = _options.MaxFeatureBranches,
                token_budget = anfrage.RessourcenLimits.TokenBudget
            },
            clones = new[]
            {
                new { name = "repo_main", path = "clones/repo_main", branch = anfrage.ProjektBranchName }
            },
            subagents = Array.Empty<object>(),
            skills = Array.Empty<object>(),
            progress = new
            {
                phase = "Initialisiert",
                completion_percentage = 0,
                last_updated_utc = DateTimeOffset.UtcNow
            },
            pull_request = new
            {
                status = "Geplant",
                url = (string?)null
            },
            flags = new
            {
                allow_token_extension = anfrage.RessourcenLimits.TokenBudgetErweitert.HasValue,
                skip_conpty_tests = false
            }
        };

        return JsonSerializer.Serialize(state, JsonOptions);
    }

    private static string BuildGovernanceMarkdown() => """
        # Governance

        Der Projektleiter-Agent darf innerhalb der in `permissions.json` definierten Grenzen:
        - plan.md und progress.md aktualisieren
        - Unteragenten erzeugen (max. gemäß `limits.max_subagents`)
        - Skills definieren und versionieren
        - Pull Requests vorbereiten (kein automatischer Merge)

        Unteragenten dürfen ausschließlich innerhalb ihres zugewiesenen `tasks/task_XXX/`-Verzeichnisses schreiben.
        """;

    private static void ValidiereAnfrage(AutonomAufgabeInitialisierungsAnfrage anfrage)
    {
        if (string.IsNullOrWhiteSpace(anfrage.ProjektBranchName) || !GitBranchNameValidator.IstGueltig(anfrage.ProjektBranchName))
        {
            throw new ArgumentException("ProjektBranchName darf nicht leer sein und muss ein gültiger Git-Branch-Name sein.", nameof(anfrage));
        }

        if (string.IsNullOrWhiteSpace(anfrage.InitialPrompt) || anfrage.InitialPrompt.Trim().Length < 10)
        {
            throw new ArgumentException("InitialPrompt darf nicht leer sein und muss mindestens 10 Zeichen enthalten.", nameof(anfrage));
        }

        if (anfrage.RessourcenLimits.TokenBudget <= 0 || anfrage.RessourcenLimits.TokenBudget > 5_000_000)
        {
            throw new ArgumentException("TokenBudget muss größer als 0 und maximal 5.000.000 sein.", nameof(anfrage));
        }

        if (anfrage.RessourcenLimits.LaufzeitLimitMinuten < 60 || anfrage.RessourcenLimits.LaufzeitLimitMinuten > 1440)
        {
            throw new ArgumentException("LaufzeitLimitMinuten muss zwischen 60 und 1440 (24h) liegen.", nameof(anfrage));
        }

        if (string.IsNullOrWhiteSpace(anfrage.ArbeitsverzeichnisPfad) || !Path.IsPathRooted(anfrage.ArbeitsverzeichnisPfad))
        {
            throw new ArgumentException("ArbeitsverzeichnisPfad muss ein absoluter Pfad sein.", nameof(anfrage));
        }
    }
}
