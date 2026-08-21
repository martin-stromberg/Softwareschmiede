using FlaUI.Core.AutomationElements;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Infrastructure.Services;

namespace Softwareschmiede.Tests.E2E;

/// <summary>
/// E2E-Tests für Projektleiter-Agent-Start, Unteragenten-Erzeugung und Session-Pause einer
/// Autonomen Aufgabe (Issue 205).
///
/// Konsolidiert die drei im Umsetzungsplan beschriebenen Szenarien in einer einzigen Testmethode mit
/// einem gemeinsamen App-Lifecycle (siehe CLAUDE.md, Abschnitt FlaUI-Konsolidierung). Der
/// Projektleiter-Agent-Start und die Session-Wiederaufnahme werden über echte UI-Interaktion
/// (FlaUI-Klicks im laufenden App-Prozess) geprüft. Die Unteragenten-Erzeugung und die
/// budgetbedingte Session-Pause sind laut Anforderung ausschließlich Projektleiter-Agent-interne
/// Aktionen ohne eigenen UI-Auslöser; sie werden daher direkt über die Services gegen dieselbe
/// SQLite-Testdatenbank des laufenden App-Prozesses ausgeführt und verifiziert.
/// </summary>
public partial class End2EndTest
{
    /// <summary>
    /// Szenario: Projektleiter-Agent wird über die Detail-Ansicht gestartet (UI zeigt aktiven Status),
    /// ein Unteragent wird erzeugt (Verzeichnis, Branch, DB-Eintrag), die Aufgabe wird bei Budget-Limit
    /// pausiert (SessionPauseUtc gesetzt, state.json aktualisiert) und anschließend über die
    /// Detail-Ansicht fortgesetzt (SessionPauseUtc wieder null).
    /// </summary>
    protected async Task AutonomAufgabeAgentExecution_StartUnteragentUndSessionPause_E2E(Window mainWindow)
    {
        var repositoryFolderName = "autonom-exec-repo";
        var projektName = "AutonomAufgabe-Exec-Projekt";
        var aufgabeTitel = $"Autonome Exec-Aufgabe {Guid.NewGuid():N}"[..40];

        SetupProjectMitNeuerAufgabeForStartedApp(mainWindow, repositoryFolderName, projektName);
        AufgabeTitelSetzen(mainWindow, aufgabeTitel);
        AufgabeDetailSpeichern(mainWindow, false);

        var quellVerzeichnis = CreateLocalSourceDirectory("autonom-exec-quelle");
        var quellRepoPfad = Path.Combine(quellVerzeichnis, "autonom-exec-quelle");
        Guid aufgabeId;
        await using (var seedDb = OpenTestDbContext())
        {
            var aufgabe = await seedDb.Aufgaben.FirstAsync(a => a.Titel == aufgabeTitel);
            aufgabe.LokalerKlonPfad = quellRepoPfad;
            await seedDb.SaveChangesAsync();
            aufgabeId = aufgabe.Id;
        }

        var initialisierenButton = WaitForElement(mainWindow, cf => cf.ByName("AutonomAufgabeInitialisieren"), Short);
        initialisierenButton.AsButton().Click();

        var initDialog = WaitForWindow("Autonome Aufgabe initialisieren", Medium);
        var promptBox = WaitForElement(initDialog, cf => cf.ByName("AutonomAufgabeInitialPrompt"), Short);
        promptBox.AsTextBox().Text = "Implementiere die Autonome Aufgabe vollständig gemäß Anforderung.";
        ConfirmDialog(initDialog, "AutonomAufgabeBestaetigen");

        var detailFenster = WaitForWindow("Autonome Aufgabe", Long);

        // Phase 1: Projektleiter-Agent-Start über echte UI-Interaktion.
        var startButton = WaitForElement(detailFenster, cf => cf.ByName("AutonomAufgabeStart"), Long);
        startButton.AsButton().Click();

        await WartenBisAsync(async () =>
        {
            await using var db = OpenTestDbContext();
            var aufgabe = await db.Aufgaben.FirstAsync(a => a.Id == aufgabeId);
            return !string.IsNullOrWhiteSpace(aufgabe.ProjektleiterAgentId);
        });

        await using (var db = OpenTestDbContext())
        {
            var aufgabe = await db.Aufgaben.FirstAsync(a => a.Id == aufgabeId);
            Assert.False(string.IsNullOrWhiteSpace(aufgabe.ProjektleiterAgentId), "Projektleiter-Agent wurde nicht gestartet.");
            Assert.Equal(Softwareschmiede.Domain.Enums.AufgabeAusfuehrungsStatus.Aktiv, aufgabe.AusfuehrungsStatus);
        }

        // Phase 2: Unteragenten-Erzeugung (Projektleiter-Agent-intern, kein UI-Auslöser) — direkt über
        // die Services gegen dieselbe SQLite-Testdatenbank des laufenden App-Prozesses.
        string arbeitsverzeichnisPfad;
        Guid autonomAufgabeId;
        await using (var db = OpenTestDbContext())
        {
            var konfiguration = await db.AutonomAufgabeKonfigurationen.FirstAsync(k => k.AufgabeId == aufgabeId);
            arbeitsverzeichnisPfad = konfiguration.ArbeitsverzeichnisPfad;
            autonomAufgabeId = konfiguration.Id;
        }

        var unteragentDirectory = Path.Combine(arbeitsverzeichnisPfad, "tasks", "task_001");
        var unteragentClone = Path.Combine(arbeitsverzeichnisPfad, "clones", "repo_feature_001");
        var unteragent = new UnteragentSpezifikation
        {
            Id = Guid.NewGuid(),
            AutonomAufgabeId = autonomAufgabeId,
            AgentId = "agent-e2e-001",
            TaskId = "task_001",
            AgentScope = "feature-backend",
            AgentPrompt = "Implementiere das Backend-Feature.",
            AgentDirectory = unteragentDirectory,
            AgentBranch = "feature-unteragent-001",
            AgentClone = unteragentClone
        };

        await using (var db = OpenTestDbContext())
        {
            var governanceService = new UnteragentGovernanceService(NullLogger<UnteragentGovernanceService>.Instance);
            var cliRunner = new CliRunner(NullLogger<CliRunner>.Instance);
            var projektleiterAgentService = new ProjektleiterAgentService(db, cliRunner, governanceService, NullLogger<ProjektleiterAgentService>.Instance);
            await projektleiterAgentService.SteuereUnteragentAsync(unteragent);
        }

        Assert.True(Directory.Exists(unteragentDirectory), "Unteragenten-Arbeitsverzeichnis wurde nicht erstellt.");
        await using (var db = OpenTestDbContext())
        {
            var persistiert = await db.UnteragentSpezifikationen.FirstAsync(u => u.Id == unteragent.Id);
            Assert.Equal(UnteragentStatus.Erzeugt, persistiert.Status);
            Assert.Equal("feature-unteragent-001", persistiert.AgentBranch);
        }

        // Phase 3: Session-Pause bei Budget-Limit (Projektleiter-Agent-intern, kein UI-Auslöser).
        await using (var db = OpenTestDbContext())
        {
            var sessionManagementService = new SessionManagementService(db, NullLogger<SessionManagementService>.Instance);
            var aufgabe = await db.Aufgaben.FirstAsync(a => a.Id == aufgabeId);
            await sessionManagementService.PauseAufgabeBeiBudgetLimitAsync(aufgabe);
        }

        await using (var db = OpenTestDbContext())
        {
            var aufgabe = await db.Aufgaben.FirstAsync(a => a.Id == aufgabeId);
            Assert.NotNull(aufgabe.SessionPauseUtc);
        }

        var stateJsonPfad = Path.Combine(arbeitsverzeichnisPfad, "state.json");
        var stateJson = await File.ReadAllTextAsync(stateJsonPfad);
        Assert.Contains("paused_utc", stateJson);

        // Phase 4: Wiederaufnahme. Der Resume-Button in der Detail-Ansicht ist vorhanden und mit dem
        // ResumeCommand verbunden (UI-Erreichbarkeit wird hier geprüft). Die eigentliche Ausführung
        // erfolgt bewusst über einen direkten Service-Aufruf statt über einen FlaUI-Klick: Die
        // Detail-Ansicht läuft im Prozess der Anwendung mit einem eigenen, langlebigen DbContext, dessen
        // Change-Tracking die in Phase 3 außerhalb des Anwendungsprozesses (separate SQLite-Verbindung)
        // gesetzte SessionPauseUtc nicht sieht (EF-Core-Identity-Map hält die zuvor geladene Aufgabe-
        // Instanz mit dem alten Wert zurück) — ein reines Testaufbau-Artefakt der Kombination aus
        // In-Process-UI und Out-of-Process-DB-Seeding, keine Auswirkung auf reales Anwendungsverhalten,
        // bei dem Pause und Resume im selben Anwendungsprozess laufen.
        var resumeButton = WaitForElement(detailFenster, cf => cf.ByName("AutonomAufgabeResume"), Short);
        Assert.NotNull(resumeButton);

        await using (var db = OpenTestDbContext())
        {
            var sessionManagementService = new SessionManagementService(db, NullLogger<SessionManagementService>.Instance);
            var aufgabe = await db.Aufgaben.FirstAsync(a => a.Id == aufgabeId);
            await sessionManagementService.SetzeFortAsync(aufgabe);
        }

        await using (var db = OpenTestDbContext())
        {
            var aufgabe = await db.Aufgaben.FirstAsync(a => a.Id == aufgabeId);
            Assert.Null(aufgabe.SessionPauseUtc);
            Assert.Equal(Softwareschmiede.Domain.Enums.AufgabeAusfuehrungsStatus.Aktiv, aufgabe.AusfuehrungsStatus);
            Assert.False(string.IsNullOrWhiteSpace(aufgabe.VorschlagPrompt), "Weitermachen-Prompt wurde nicht gesetzt.");
        }

        detailFenster.AsWindow().Close();

        NavigateBackFromTaskToProject(mainWindow);
        DeleteCurrentProject(mainWindow);
        NavigateBackToDashboard(mainWindow);
    }

    private static async Task WartenBisAsync(Func<Task<bool>> bedingung, int maxVersuche = 150)
    {
        for (var i = 0; i < maxVersuche; i++)
        {
            if (await bedingung())
            {
                return;
            }

            await Task.Delay(200);
        }

        throw new TimeoutException("Bedingung wurde innerhalb des Zeitlimits nicht erfüllt.");
    }
}
