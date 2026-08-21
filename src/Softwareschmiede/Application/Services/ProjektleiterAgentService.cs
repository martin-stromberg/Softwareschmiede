using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Infrastructure.Data;

namespace Softwareschmiede.Application.Services;

/// <summary>Verwaltet den Projektleiter-Agent-Lifecycle, die Unteragenten-Erzeugung und die Integration von Unteragenten-Ergebnissen.</summary>
public sealed class ProjektleiterAgentService
{
    private readonly SoftwareschmiededDbContext _db;
    private readonly ICliRunner _cliRunner;
    private readonly UnteragentGovernanceService _governanceService;
    private readonly ILogger<ProjektleiterAgentService> _logger;

    /// <inheritdoc cref="ProjektleiterAgentService"/>
    public ProjektleiterAgentService(
        SoftwareschmiededDbContext db,
        ICliRunner cliRunner,
        UnteragentGovernanceService governanceService,
        ILogger<ProjektleiterAgentService> logger)
    {
        _db = db;
        _cliRunner = cliRunner;
        _governanceService = governanceService;
        _logger = logger;
    }

    /// <summary>Startet den Projektleiter-Agenten mit dem Initialprompt aus der Konfiguration und dem Projektleiter-Skill.</summary>
    public async Task<string> StarteAgentAsync(AutonomAufgabeKonfiguration konfiguration, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(konfiguration);

        var skillPfad = Path.Combine(konfiguration.ArbeitsverzeichnisPfad, "skills", "skill_projektleiter_v1.md");
        if (!File.Exists(skillPfad))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(skillPfad)!);
            await File.WriteAllTextAsync(skillPfad, BuildDefaultProjektleiterSkill(konfiguration), ct);
        }

        var aufgabe = await _db.Aufgaben.FirstOrDefaultAsync(a => a.Id == konfiguration.AufgabeId, ct)
            ?? throw new InvalidOperationException($"Aufgabe {konfiguration.AufgabeId} nicht gefunden.");

        var agentId = $"projektleiter-{Guid.NewGuid():N}";

        aufgabe.AusfuehrungsStatus = AufgabeAusfuehrungsStatus.Aktiv;
        aufgabe.ProjektleiterAgentId = agentId;
        aufgabe.AktiveRunId = agentId;
        aufgabe.LastHeartbeatUtc = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Projektleiter-Agent {AgentId} für Autonome Aufgabe {AufgabeId} gestartet.", agentId, konfiguration.AufgabeId);
        return agentId;
    }

    /// <summary>Erzeugt und konfiguriert einen Unteragenten: erstellt sein Arbeitsverzeichnis, den Feature-Branch und den Klon, und persistiert die Spezifikation.</summary>
    public async Task SteuereUnteragentAsync(UnteragentSpezifikation unteragent, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(unteragent);
        ValidiereUnteragent(unteragent);

        var konfiguration = await _db.AutonomAufgabeKonfigurationen
            .AsNoTracking()
            .FirstOrDefaultAsync(k => k.Id == unteragent.AutonomAufgabeId, ct)
            ?? throw new InvalidOperationException($"AutonomAufgabeKonfiguration {unteragent.AutonomAufgabeId} nicht gefunden.");

        if (!_governanceService.VerifiziereBerechtigung(
                konfiguration.ArbeitsverzeichnisPfad,
                UnteragentAktion.ArbeitsverzeichnisErstellen,
                unteragent.VerzeichnisPfad,
                unteragent.ExterneAgentId))
        {
            throw new InvalidOperationException(
                $"Unteragent {unteragent.ExterneAgentId}: Arbeitsverzeichnis '{unteragent.VerzeichnisPfad}' liegt außerhalb des erlaubten Bereichs '{konfiguration.ArbeitsverzeichnisPfad}'.");
        }

        await DirectoryAccessGuard.AusfuehrenAsync(unteragent.VerzeichnisPfad, () =>
        {
            Directory.CreateDirectory(unteragent.VerzeichnisPfad);
            return Task.CompletedTask;
        });

        var repoMainPfad = Path.Combine(konfiguration.ArbeitsverzeichnisPfad, "clones", "repo_main");
        var branchErgebnis = await _cliRunner.RunAsync("git", ["branch", unteragent.Branch], repoMainPfad, null, ct);
        if (!branchErgebnis.IsSuccess)
        {
            throw new InvalidOperationException($"Branch '{unteragent.Branch}' für Unteragent '{unteragent.ExterneAgentId}' konnte nicht angelegt werden: {branchErgebnis.StdErr}");
        }

        await GitKlonHelper.KloneFallsNichtVorhandenAsync(
            _cliRunner,
            repoMainPfad,
            unteragent.ClonePfad,
            unteragent.Branch,
            _logger,
            $"Klon für Unteragent '{unteragent.ExterneAgentId}' fehlgeschlagen",
            ct);

        unteragent.Status = UnteragentStatus.Erzeugt;
        unteragent.ErzeugungsDatum = DateTimeOffset.UtcNow;

        _db.UnteragentSpezifikationen.Add(unteragent);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Unteragent {AgentId} für Autonome Aufgabe {AutonomAufgabeId} erzeugt (Branch: {Branch}).",
            unteragent.ExterneAgentId,
            unteragent.AutonomAufgabeId,
            unteragent.Branch);
    }

    /// <summary>Integriert die Ergebnisse eines abgeschlossenen Unteragenten in plan.md, progress.md und state.json.</summary>
    public async Task IntegriereErgebnisseAsync(AutonomAufgabeKonfiguration konfiguration, UnteragentSpezifikation unteragent, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(konfiguration);
        ArgumentNullException.ThrowIfNull(unteragent);

        var reportPfad = Path.Combine(unteragent.VerzeichnisPfad, "task_report.md");
        var report = File.Exists(reportPfad)
            ? await File.ReadAllTextAsync(reportPfad, ct)
            : "(kein task_report.md gefunden)";

        var planPfad = Path.Combine(konfiguration.ArbeitsverzeichnisPfad, "plan.md");
        await File.AppendAllTextAsync(planPfad, $"\n## Teilaufgabe {unteragent.TaskId} ({unteragent.Scope})\nStatus: Abgeschlossen\n", ct);

        var progressPfad = Path.Combine(konfiguration.ArbeitsverzeichnisPfad, "progress.md");
        await File.AppendAllTextAsync(progressPfad, $"\n## {DateTimeOffset.UtcNow:O} — Unteragent {unteragent.ExterneAgentId} abgeschlossen\n{report}\n", ct);

        await AktualisiereSubagentsInStateJsonAsync(konfiguration.ArbeitsverzeichnisPfad, unteragent, ct);

        var entity = await _db.UnteragentSpezifikationen.FirstOrDefaultAsync(u => u.Id == unteragent.Id, ct)
            ?? throw new InvalidOperationException($"UnteragentSpezifikation {unteragent.Id} nicht gefunden.");

        entity.Status = UnteragentStatus.Abgeschlossen;
        entity.AbschlussDatum = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Ergebnisse von Unteragent {AgentId} integriert (plan.md, progress.md, state.json aktualisiert).",
            unteragent.ExterneAgentId);
    }

    private async Task AktualisiereSubagentsInStateJsonAsync(string arbeitsverzeichnisPfad, UnteragentSpezifikation unteragent, CancellationToken ct)
    {
        var stateJsonPfad = Path.Combine(arbeitsverzeichnisPfad, "state.json");
        var node = await StateJsonHelper.LeseAsync(stateJsonPfad, _logger, ct);
        if (node is null)
        {
            return;
        }

        if (node["subagents"] is not JsonArray subagents)
        {
            subagents = new JsonArray();
            node["subagents"] = subagents;
        }

        subagents.Add(new JsonObject
        {
            ["agent_id"] = unteragent.ExterneAgentId,
            ["task_id"] = unteragent.TaskId,
            ["scope"] = unteragent.Scope,
            ["status"] = "Abgeschlossen",
            ["completed_utc"] = DateTimeOffset.UtcNow.ToString("O")
        });

        await StateJsonHelper.SchreibeAsync(stateJsonPfad, node, ct);
    }

    private static string BuildDefaultProjektleiterSkill(AutonomAufgabeKonfiguration konfiguration) => $"""
        # Skill: Projektleiter v1

        ## Auftrag
        {konfiguration.InitialPrompt}

        ## Verantwortlichkeiten
        - Zerlege die Gesamtaufgabe in Teilaufgaben und pflege plan.md
        - Erzeuge und steuere Unteragenten im Rahmen der Governance-Grenzen
        - Aktualisiere progress.md nach jedem abgeschlossenen Schritt
        - Bereite Pull Requests vor (kein automatischer Merge)
        """;

    private static void ValidiereUnteragent(UnteragentSpezifikation unteragent)
    {
        if (string.IsNullOrWhiteSpace(unteragent.Scope))
        {
            throw new ArgumentException("Scope darf nicht leer sein.", nameof(unteragent));
        }

        if (string.IsNullOrWhiteSpace(unteragent.Branch))
        {
            throw new ArgumentException("Branch darf nicht leer sein.", nameof(unteragent));
        }

        if (string.IsNullOrWhiteSpace(unteragent.VerzeichnisPfad) || !Path.IsPathRooted(unteragent.VerzeichnisPfad))
        {
            throw new ArgumentException("VerzeichnisPfad muss ein absoluter Pfad sein.", nameof(unteragent));
        }

        if (string.IsNullOrWhiteSpace(unteragent.ClonePfad) || !Path.IsPathRooted(unteragent.ClonePfad))
        {
            throw new ArgumentException("ClonePfad muss ein absoluter Pfad sein.", nameof(unteragent));
        }
    }
}
