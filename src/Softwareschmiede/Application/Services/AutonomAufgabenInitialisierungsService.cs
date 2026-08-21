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
    private readonly AutonomAufgabenOptions _options;
    private readonly ILogger<AutonomAufgabenInitialisierungsService> _logger;

    /// <inheritdoc cref="AutonomAufgabenInitialisierungsService"/>
    public AutonomAufgabenInitialisierungsService(
        SoftwareschmiededDbContext db,
        ICliRunner cliRunner,
        IOptions<AutonomAufgabenOptions> options,
        ILogger<AutonomAufgabenInitialisierungsService> logger)
    {
        _db = db;
        _cliRunner = cliRunner;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>Erstellt das Arbeitsverzeichnis, erzeugt den Repository-Klon und initialisiert state.json und permissions.json für eine Autonome Aufgabe.</summary>
    public async Task<AutonomAufgabeKonfiguration> InitialisiereAsync(Aufgabe aufgabe, AutonomAufgabeInitialisierungsAnfrage anfrage, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(aufgabe);
        ArgumentNullException.ThrowIfNull(anfrage);
        ValidiereAnfrage(anfrage);

        await ErstelleArbeitsverzeichnisStrukturAsync(anfrage.ArbeitsverzeichnisPfad, ct);

        var repoMainPfad = Path.Combine(anfrage.ArbeitsverzeichnisPfad, "clones", "repo_main");
        await KloneHauptRepositoryAsync(aufgabe, repoMainPfad, ct);

        var permissionsPfad = Path.Combine(anfrage.ArbeitsverzeichnisPfad, "permissions.json");
        var stateJsonPfad = Path.Combine(anfrage.ArbeitsverzeichnisPfad, "state.json");
        await DirectoryAccessGuard.AusfuehrenAsync(anfrage.ArbeitsverzeichnisPfad, async () =>
        {
            if (anfrage.PermissionsQuelle == PermissionsJsonOption.Generate || !File.Exists(permissionsPfad))
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
            TokenBudget = anfrage.TokenBudget,
            TokenBudgetErweitert = anfrage.TokenBudgetErweitert,
            LaufzeitLimitMinuten = anfrage.LaufzeitLimitMinuten,
            PersistenzModus = anfrage.PersistenzModus,
            SkillAutogeneration = anfrage.SkillAutogeneration,
            ArbeitsverzeichnisPfad = anfrage.ArbeitsverzeichnisPfad
        };

        _db.AutonomAufgabeKonfigurationen.Add(konfiguration);
        SetzeAusfuehrungsStatusAutonomAufgabe(aufgabe);
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

    private void SetzeAusfuehrungsStatusAutonomAufgabe(Aufgabe aufgabe)
    {
        var verfolgteAufgabe = _db.ChangeTracker.Entries<Aufgabe>().Select(e => e.Entity).FirstOrDefault(a => a.Id == aufgabe.Id);
        if (verfolgteAufgabe is null)
        {
            _db.Attach(aufgabe);
            verfolgteAufgabe = aufgabe;
        }

        verfolgteAufgabe.AusfuehrungsStatus = AufgabeAusfuehrungsStatus.AutonomAufgabe;
    }

    private Task KloneHauptRepositoryAsync(Aufgabe aufgabe, string zielPfad, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(aufgabe.LokalerKlonPfad))
        {
            throw new InvalidOperationException(
                $"Aufgabe {aufgabe.Id} besitzt keinen lokalen Klon-Pfad; Repository-Klon für die Autonome Aufgabe kann nicht erstellt werden.");
        }

        return GitKlonHelper.KloneFallsNichtVorhandenAsync(
            _cliRunner,
            aufgabe.LokalerKlonPfad,
            zielPfad,
            branch: null,
            _logger,
            $"Repository-Klon nach '{zielPfad}' fehlgeschlagen",
            ct);
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
                token_budget = anfrage.TokenBudget,
                net_runtime_minutes = anfrage.LaufzeitLimitMinuten
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
                net_minutes_limit = anfrage.LaufzeitLimitMinuten,
                paused_utc = (DateTimeOffset?)null
            },
            governance = new
            {
                max_subagents = _options.MaxConcurrentUnteragenten,
                max_clones = _options.MaxClones,
                max_feature_branches = _options.MaxFeatureBranches,
                token_budget = anfrage.TokenBudget
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
                allow_token_extension = anfrage.TokenBudgetErweitert.HasValue,
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
        if (string.IsNullOrWhiteSpace(anfrage.ProjektBranchName) || !IstGueltigerBranchName(anfrage.ProjektBranchName))
        {
            throw new ArgumentException("ProjektBranchName darf nicht leer sein und muss ein gültiger Git-Branch-Name sein.", nameof(anfrage));
        }

        if (string.IsNullOrWhiteSpace(anfrage.InitialPrompt) || anfrage.InitialPrompt.Trim().Length < 10)
        {
            throw new ArgumentException("InitialPrompt darf nicht leer sein und muss mindestens 10 Zeichen enthalten.", nameof(anfrage));
        }

        if (anfrage.TokenBudget <= 0 || anfrage.TokenBudget > 5_000_000)
        {
            throw new ArgumentException("TokenBudget muss größer als 0 und maximal 5.000.000 sein.", nameof(anfrage));
        }

        if (anfrage.LaufzeitLimitMinuten < 60 || anfrage.LaufzeitLimitMinuten > 1440)
        {
            throw new ArgumentException("LaufzeitLimitMinuten muss zwischen 60 und 1440 (24h) liegen.", nameof(anfrage));
        }

        if (string.IsNullOrWhiteSpace(anfrage.ArbeitsverzeichnisPfad) || !Path.IsPathRooted(anfrage.ArbeitsverzeichnisPfad))
        {
            throw new ArgumentException("ArbeitsverzeichnisPfad muss ein absoluter Pfad sein.", nameof(anfrage));
        }
    }

    private static bool IstGueltigerBranchName(string branchName)
    {
        if (branchName.StartsWith('/') || branchName.EndsWith('/') || branchName.EndsWith('.'))
        {
            return false;
        }

        return !branchName.Contains("..", StringComparison.Ordinal)
            && !branchName.Any(c => c is ' ' or '~' or '^' or ':' or '?' or '*' or '[' or '\\');
    }
}
