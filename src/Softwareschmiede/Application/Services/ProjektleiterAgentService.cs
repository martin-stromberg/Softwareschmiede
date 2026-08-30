using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Infrastructure.Data;
using Softwareschmiede.Infrastructure.Terminal;

namespace Softwareschmiede.Application.Services;

/// <summary>Verwaltet den Projektleiter-Agent-Lifecycle, die Unteragenten-Erzeugung und die Integration von Unteragenten-Ergebnissen.</summary>
public sealed class ProjektleiterAgentService
{
    /// <summary>Feste Verzögerung, bevor der Initial-/Weitermachen-Prompt nach dem CLI-Start an die PseudoConsoleSession
    /// gesendet wird. Deutlich länger als die 300ms-Verzögerung in <see cref="KiAusfuehrungsService.SendCommandDelayedAsync"/>,
    /// da hier zusätzlich auf den Eigenstart der KI-CLI selbst gewartet werden muss (kein Ready-Signal vorhanden).</summary>
    private const int PromptSendeVerzoegerungMs = 3000;

    private readonly SoftwareschmiededDbContext _db;
    private readonly UnteragentGovernanceService _governanceService;
    private readonly UnteragentGitProvisioningService _gitProvisioningService;
    private readonly KiAusfuehrungsService _kiAusfuehrungsService;
    private readonly PluginSelectionService _pluginSelectionService;
    private readonly AppEinstellungService _appEinstellungService;
    private readonly IOptions<AutonomAufgabenOptions> _autonomAufgabenOptions;
    private readonly ILogger<ProjektleiterAgentService> _logger;

    /// <inheritdoc cref="ProjektleiterAgentService"/>
    public ProjektleiterAgentService(
        SoftwareschmiededDbContext db,
        UnteragentGovernanceService governanceService,
        UnteragentGitProvisioningService gitProvisioningService,
        KiAusfuehrungsService kiAusfuehrungsService,
        PluginSelectionService pluginSelectionService,
        AppEinstellungService appEinstellungService,
        IOptions<AutonomAufgabenOptions> autonomAufgabenOptions,
        ILogger<ProjektleiterAgentService> logger)
    {
        _db = db;
        _governanceService = governanceService;
        _gitProvisioningService = gitProvisioningService;
        _kiAusfuehrungsService = kiAusfuehrungsService;
        _pluginSelectionService = pluginSelectionService;
        _appEinstellungService = appEinstellungService;
        _autonomAufgabenOptions = autonomAufgabenOptions;
        _logger = logger;
    }

    /// <summary>
    /// Startet den Projektleiter-Agenten: erzeugt den Projektleiter-Skill, startet den echten CLI-Prozess über
    /// <see cref="KiAusfuehrungsService.StartWithPseudoConsoleAsync"/> und sendet anschließend (verzögert, Fire-and-Forget)
    /// den Initial- bzw. Weitermachen-Prompt über die <see cref="PseudoConsoleSession"/> der Aufgabe.
    /// </summary>
    /// <param name="konfiguration">Die Konfiguration der Autonomen Aufgabe.</param>
    /// <param name="optionalResumePrompt">Bei App-Neustart-Recovery der Weitermachen-Prompt; bei Erststart <c>null</c>.</param>
    /// <param name="ct">Abbruch-Token.</param>
    /// <returns>Die neu erzeugte Projektleiter-Agent-ID.</returns>
    public async Task<string> StarteAgentAsync(AutonomAufgabeKonfiguration konfiguration, string? optionalResumePrompt = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(konfiguration);

        // DB-persistierter Laufzeit-Schalter (AppEinstellungService.AutonomAufgabenEnabledKey, GUI-Einstellung)
        // hat Vorrang vor dem appsettings.json-/Umgebungsvariable-Deployment-Default, sofern der Anwender ihn
        // bereits explizit in den Einstellungen gesetzt hat (Issue 205, Dual-Layer-Feature-Flag).
        if (!await _appEinstellungService.GetAutonomAufgabenEnabledAsync(_autonomAufgabenOptions.Value.Enabled, ct))
        {
            throw new InvalidOperationException(AutonomAufgabenOptions.DisabledErrorMessage);
        }

        var skillPfad = Path.Combine(konfiguration.ArbeitsverzeichnisPfad, "skills", "skill_projektleiter_v1.md");
        if (!File.Exists(skillPfad))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(skillPfad)!);
            await File.WriteAllTextAsync(skillPfad, BuildDefaultProjektleiterSkill(konfiguration), ct);
        }

        var aufgabe = await _db.Aufgaben
            .Include(a => a.AutonomKonfiguration)
            .FirstOrDefaultAsync(a => a.Id == konfiguration.AufgabeId, ct)
            ?? throw new InvalidOperationException($"Aufgabe {konfiguration.AufgabeId} nicht gefunden.");

        var autonomKonfiguration = aufgabe.AutonomKonfiguration
            ?? throw new InvalidOperationException($"AutonomAufgabeKonfiguration für Aufgabe {konfiguration.AufgabeId} nicht gefunden.");

        var kiPlugin = await _pluginSelectionService.ResolveDevelopmentAutomationPluginAsync(aufgabe.KiPluginPrefix, ct);
        var optionalParameters = optionalResumePrompt is not null && kiPlugin.SupportsSessionContinuation()
            ? "--continue"
            : null;

        try
        {
            await _kiAusfuehrungsService.StartWithPseudoConsoleAsync(
                konfiguration.AufgabeId,
                kiPlugin,
                konfiguration.ArbeitsverzeichnisPfad,
                optionalParameters,
                ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CLI-Start für Projektleiter-Agent der Aufgabe {AufgabeId} fehlgeschlagen.", konfiguration.AufgabeId);
            aufgabe.AusfuehrungsStatus = AufgabeAusfuehrungsStatus.Beendet;
            await _db.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }

        var agentId = $"projektleiter-{Guid.NewGuid():N}";
        var promptText = optionalResumePrompt ?? konfiguration.InitialPrompt;
        SendeInitialPromptVerzoegertAsync(konfiguration.AufgabeId, promptText, ct).SafeFireAndForget(_logger, "ProjektleiterAgentService.SendeInitialPromptVerzoegertAsync");

        aufgabe.AusfuehrungsStatus = AufgabeAusfuehrungsStatus.Aktiv;
        autonomKonfiguration.ProjektleiterAgentId = agentId;
        autonomKonfiguration.ExplizitGestoppt = false;
        aufgabe.AktiveRunId = agentId;
        aufgabe.LastHeartbeatUtc = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Projektleiter-Agent {AgentId} für Autonome Aufgabe {AufgabeId} gestartet.", agentId, konfiguration.AufgabeId);
        return agentId;
    }

    /// <summary>
    /// Startet den Projektleiter-Agenten nach einem App-Neustart automatisch neu, sofern die Aufgabe nicht explizit
    /// gestoppt wurde und noch als aktiv gilt. Wird von der App-Startup-Recovery aufgerufen.
    /// </summary>
    /// <param name="aufgabeId">ID der Aufgabe.</param>
    /// <param name="resumePrompt">Der zu sendende Weitermachen-Prompt.</param>
    /// <param name="ct">Abbruch-Token.</param>
    public async Task StarteAgenNachAppNeustartAsync(Guid aufgabeId, string resumePrompt, CancellationToken ct = default)
    {
        var aufgabe = await _db.Aufgaben
            .Include(a => a.AutonomKonfiguration)
            .FirstOrDefaultAsync(a => a.Id == aufgabeId, ct)
            ?? throw new InvalidOperationException($"Aufgabe {aufgabeId} nicht gefunden.");

        var konfiguration = aufgabe.AutonomKonfiguration
            ?? throw new InvalidOperationException($"AutonomAufgabeKonfiguration für Aufgabe {aufgabeId} nicht gefunden.");

        if (konfiguration.ExplizitGestoppt || aufgabe.AusfuehrungsStatus != AufgabeAusfuehrungsStatus.Aktiv)
        {
            return;
        }

        await StarteAgentAsync(konfiguration, optionalResumePrompt: resumePrompt, ct: ct);
    }

    /// <summary>Stoppt den Projektleiter-Agenten explizit auf Benutzerwunsch: setzt <see cref="AutonomAufgabeKonfiguration.ExplizitGestoppt"/>
    /// und beendet den laufenden CLI-Prozess. Verhindert einen automatischen Wiederstart nach dem nächsten App-Neustart.</summary>
    /// <param name="aufgabeId">ID der Aufgabe.</param>
    /// <param name="ct">Abbruch-Token.</param>
    public async Task StoppeAgenExplizitAsync(Guid aufgabeId, CancellationToken ct = default)
    {
        var konfiguration = await _db.AutonomAufgabeKonfigurationen
            .FirstOrDefaultAsync(k => k.AufgabeId == aufgabeId, ct)
            ?? throw new InvalidOperationException($"AutonomAufgabeKonfiguration für Aufgabe {aufgabeId} nicht gefunden.");

        konfiguration.ExplizitGestoppt = true;

        // ExplizitGestoppt zuerst persistieren, bevor der (bis zu 5s dauernde, siehe StopCliAsync) CLI-Stopp versucht
        // wird: Verhindert, dass ein einziges "Beenden" scheinbar wirkungslos bleibt, weil ExplizitGestoppt nie
        // gespeichert wurde (z. B. bei einer während des Wartens auftretenden DbContext-Kollision durch anderweitige
        // gleichzeitige Nutzung desselben Scoped-DbContext) — das Flag ist die primäre, sicherheitsrelevante Aussage
        // (verhindert App-Neustart-Recovery), der eigentliche Prozess-Stopp ist bereits Best-Effort.
        await _db.SaveChangesAsync(ct);

        try
        {
            await _kiAusfuehrungsService.StopCliAsync(aufgabeId, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "CLI-Prozess für Aufgabe {AufgabeId} konnte beim expliziten Stoppen nicht sauber beendet werden.", aufgabeId);
        }

        _logger.LogInformation("Projektleiter-Agent für Aufgabe {AufgabeId} explizit gestoppt.", aufgabeId);
    }

    /// <summary>Erzeugt und konfiguriert einen Unteragenten: erstellt sein Arbeitsverzeichnis, den Feature-Branch und den Klon, und persistiert die Spezifikation.</summary>
    public async Task SteuereUnteragentAsync(UnteragentSpezifikation unteragent, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(unteragent);
        ValidiereUnteragent(unteragent);

        var konfiguration = await LadeKonfigurationAsync(unteragent.AutonomAufgabeId, ct);
        PruefeGovernance(unteragent, konfiguration);

        var repoMainPfad = Path.Combine(konfiguration.ArbeitsverzeichnisPfad, "clones", "repo_main");
        await _gitProvisioningService.ProvisioniereAsync(unteragent, repoMainPfad, ct);

        await PersistiereUnteragentAsync(unteragent, ct);

        _logger.LogInformation(
            "Unteragent {AgentId} für Autonome Aufgabe {AutonomAufgabeId} erzeugt (Branch: {Branch}).",
            unteragent.ExterneAgentId,
            unteragent.AutonomAufgabeId,
            unteragent.GitArbeitsbereich.BranchName);
    }

    /// <summary>Lädt die AutonomAufgabeKonfiguration für die gegebene Id.</summary>
    private async Task<AutonomAufgabeKonfiguration> LadeKonfigurationAsync(Guid autonomAufgabeId, CancellationToken ct)
        => await _db.AutonomAufgabeKonfigurationen
            .AsNoTracking()
            .FirstOrDefaultAsync(k => k.Id == autonomAufgabeId, ct)
            ?? throw new InvalidOperationException($"AutonomAufgabeKonfiguration {autonomAufgabeId} nicht gefunden.");

    /// <summary>Prüft, dass das Arbeitsverzeichnis des Unteragenten innerhalb des erlaubten Bereichs der Autonomen Aufgabe liegt.</summary>
    private void PruefeGovernance(UnteragentSpezifikation unteragent, AutonomAufgabeKonfiguration konfiguration)
    {
        if (!_governanceService.VerifiziereBerechtigung(
                konfiguration.ArbeitsverzeichnisPfad,
                UnteragentAktion.ArbeitsverzeichnisErstellen,
                unteragent.VerzeichnisPfad,
                unteragent.ExterneAgentId))
        {
            throw new InvalidOperationException(
                $"Unteragent {unteragent.ExterneAgentId}: Arbeitsverzeichnis '{unteragent.VerzeichnisPfad}' liegt außerhalb des erlaubten Bereichs '{konfiguration.ArbeitsverzeichnisPfad}'.");
        }
    }

    /// <summary>Markiert den Unteragenten als erzeugt und persistiert die Spezifikation.</summary>
    private async Task PersistiereUnteragentAsync(UnteragentSpezifikation unteragent, CancellationToken ct)
    {
        unteragent.Status = UnteragentStatus.Erzeugt;
        unteragent.ErzeugungsDatum = DateTimeOffset.UtcNow;

        _db.UnteragentSpezifikationen.Add(unteragent);
        await _db.SaveChangesAsync(ct);
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

    /// <summary>
    /// Wartet <see cref="PromptSendeVerzoegerungMs"/> ab und sendet anschließend <paramref name="promptText"/> über die
    /// aktive <see cref="PseudoConsoleSession"/> der Aufgabe. Best-Effort: Ist keine Session (mehr) vorhanden oder
    /// tritt ein Fehler auf, wird lediglich geloggt, nicht geworfen (analog zu <see cref="KiAusfuehrungsService.SendCommandDelayedAsync"/>).
    /// </summary>
    /// <param name="aufgabeId">ID der Aufgabe, deren CLI-Session den Prompt erhalten soll.</param>
    /// <param name="promptText">Der zu sendende Prompttext.</param>
    /// <param name="ct">Abbruch-Token.</param>
    private async Task SendeInitialPromptVerzoegertAsync(Guid aufgabeId, string promptText, CancellationToken ct)
    {
        try
        {
            await Task.Delay(PromptSendeVerzoegerungMs, ct).ConfigureAwait(false);

            var session = _kiAusfuehrungsService.GetPseudoConsoleSession(aufgabeId);
            if (session is null)
            {
                _logger.LogWarning(
                    "Initialprompt für Aufgabe {AufgabeId} konnte nicht gesendet werden, da keine aktive CLI-Session vorhanden ist.",
                    aufgabeId);
                return;
            }

            await session.WritePromptAsync(promptText, ct).ConfigureAwait(false);
            _logger.LogInformation("Initialprompt für Aufgabe {AufgabeId} an CLI-Session gesendet.", aufgabeId);
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException ex)
        {
            _logger.LogDebug(
                ex,
                "Initialprompt für Aufgabe {AufgabeId} konnte nicht gesendet werden, da die Session zwischenzeitlich disposed wurde.",
                aufgabeId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Initialprompt für Aufgabe {AufgabeId} konnte nicht gesendet werden.", aufgabeId);
        }
    }

    private static void ValidiereUnteragent(UnteragentSpezifikation unteragent)
    {
        if (string.IsNullOrWhiteSpace(unteragent.Scope))
        {
            throw new ArgumentException("Scope darf nicht leer sein.", nameof(unteragent));
        }

        if (string.IsNullOrWhiteSpace(unteragent.GitArbeitsbereich.BranchName))
        {
            throw new ArgumentException("Branch darf nicht leer sein.", nameof(unteragent));
        }

        if (string.IsNullOrWhiteSpace(unteragent.VerzeichnisPfad) || !Path.IsPathRooted(unteragent.VerzeichnisPfad))
        {
            throw new ArgumentException("VerzeichnisPfad muss ein absoluter Pfad sein.", nameof(unteragent));
        }

        if (string.IsNullOrWhiteSpace(unteragent.GitArbeitsbereich.ClonePfad) || !Path.IsPathRooted(unteragent.GitArbeitsbereich.ClonePfad))
        {
            throw new ArgumentException("ClonePfad muss ein absoluter Pfad sein.", nameof(unteragent));
        }
    }
}
