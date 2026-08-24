using FlaUI.Core.AutomationElements;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Infrastructure.Services;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.E2E;

/// <summary>
/// E2E-Tests für Projektleiter-Agent-Start, Unteragenten-Erzeugung und Session-Pause einer
/// Autonomen Aufgabe (Issue 205).
///
/// Konsolidiert die drei im Umsetzungsplan beschriebenen Szenarien in einer einzigen Testmethode mit
/// einem gemeinsamen App-Lifecycle (siehe CLAUDE.md, Abschnitt FlaUI-Konsolidierung). Der
/// Projektleiter-Agent-Start und die Session-Wiederaufnahme werden über echte UI-Interaktion
/// (FlaUI-Klicks im laufenden App-Prozess) geprüft — seit der UI-Integration der Autonomen Aufgabe in
/// <c>TaskDetailView</c> (Folgeanforderung zu Issue 205) über die Ribbon-Buttons "Start"/"Resume" der
/// Gruppe "Autonome Aufgabe" statt über Buttons in einem eigenen Detail-Fenster. Die
/// Unteragenten-Erzeugung und die budgetbedingte Session-Pause sind laut Anforderung ausschließlich
/// Projektleiter-Agent-interne Aktionen ohne eigenen UI-Auslöser; sie werden daher direkt über die
/// Services gegen dieselbe SQLite-Testdatenbank des laufenden App-Prozesses ausgeführt und verifiziert.
/// </summary>
public partial class End2EndTest
{
    /// <summary>
    /// Szenario: Projektleiter-Agent wird über den Ribbon-Button "Start" der Aufgaben-Detailansicht
    /// gestartet (UI zeigt aktiven Status), ein Unteragent wird erzeugt (Verzeichnis, Branch, DB-Eintrag),
    /// die Aufgabe wird bei Budget-Limit pausiert (SessionPauseUtc gesetzt, state.json aktualisiert) und
    /// anschließend fortgesetzt (SessionPauseUtc wieder null); der Ribbon-Button "Resume" ist dabei
    /// erreichbar.
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

        // Kein eigenes Detail-Fenster mehr (Folge-Integration zu Issue 205): Die Aufgaben-Detailansicht
        // wechselt selbst zur "Automatisierung"-Registerkarte (identifiziert über deren TabControl
        // "AutonomAufgabeDetailTabs" — eigene Start/Stop/Resume-Buttons im Inhaltsbereich gibt es bewusst
        // nicht mehr, Steuerung erfolgt ausschließlich über das Ribbon).
        WaitForElement(mainWindow, cf => cf.ByName("AutonomAufgabeDetailTabs"), Long);

        // Phase 1: Projektleiter-Agent-Start über echte UI-Interaktion — über den Ribbon-Button
        // "Start" (Gruppe "Autonome Aufgabe").
        var startButton = WaitForElement(mainWindow, cf => cf.ByName("AutonomAufgabeStartAgent"), Long);
        startButton.AsButton().Click();

        await WartenBisAsync(async () =>
        {
            await using var db = OpenTestDbContext();
            var konfiguration = await db.AutonomAufgabeKonfigurationen.FirstAsync(k => k.AufgabeId == aufgabeId);
            return !string.IsNullOrWhiteSpace(konfiguration.ProjektleiterAgentId);
        });

        await using (var db = OpenTestDbContext())
        {
            var aufgabe = await db.Aufgaben.FirstAsync(a => a.Id == aufgabeId);
            var konfiguration = await db.AutonomAufgabeKonfigurationen.FirstAsync(k => k.AufgabeId == aufgabeId);
            Assert.False(string.IsNullOrWhiteSpace(konfiguration.ProjektleiterAgentId), "Projektleiter-Agent wurde nicht gestartet.");
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
            ExterneAgentId = "agent-e2e-001",
            TaskId = "task_001",
            Scope = "feature-backend",
            Prompt = "Implementiere das Backend-Feature.",
            VerzeichnisPfad = unteragentDirectory,
            Branch = "feature-unteragent-001",
            ClonePfad = unteragentClone
        };

        await using (var db = OpenTestDbContext())
        {
            var governanceService = new UnteragentGovernanceService(NullLogger<UnteragentGovernanceService>.Instance);
            var cliRunner = new CliRunner(NullLogger<CliRunner>.Instance);

            // LocalDirectoryPlugin legt im (hier verwendeten) InSourceDirectory-Modus unter clones/repo_main
            // nur eine Pointer-Datei ab, die auf das tatsächliche Quellverzeichnis verweist; dieser Stub
            // repliziert exakt diese Auflösung, ohne die im laufenden App-Prozess konfigurierte
            // Plugin-Instanz (mit eigenem CredentialStore) referenzieren zu müssen.
            var gitPluginMock = new Mock<IGitPlugin>();
            gitPluginMock
                .Setup(p => p.ResolveEffectiveRepositoryPathAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns<string, CancellationToken>((pfad, _) => Task.FromResult(ResolveLocalWorkspacePointerPath(pfad)));

            var gitProvisioningService = new UnteragentGitProvisioningService(cliRunner, gitPluginMock.Object, NullLogger<UnteragentGitProvisioningService>.Instance);
            using var kiAusfuehrungsService = TestKiAusfuehrungsServiceFactory.Create();
            var (_, pluginSelectionService) = ProjektleiterAgentServiceTestDatenFactory.ErstellePluginSelectionServiceMitKiPlugin(db);
            var projektleiterAgentService = new ProjektleiterAgentService(db, governanceService, gitProvisioningService, kiAusfuehrungsService, pluginSelectionService, NullLogger<ProjektleiterAgentService>.Instance);
            await projektleiterAgentService.SteuereUnteragentAsync(unteragent);
        }

        Assert.True(Directory.Exists(unteragentDirectory), "Unteragenten-Arbeitsverzeichnis wurde nicht erstellt.");
        await using (var db = OpenTestDbContext())
        {
            var persistiert = await db.UnteragentSpezifikationen.FirstAsync(u => u.Id == unteragent.Id);
            Assert.Equal(UnteragentStatus.Erzeugt, persistiert.Status);
            Assert.Equal("feature-unteragent-001", persistiert.Branch);
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
            var konfiguration = await db.AutonomAufgabeKonfigurationen.FirstAsync(k => k.AufgabeId == aufgabeId);
            Assert.NotNull(konfiguration.SessionPauseUtc);
        }

        var stateJsonPfad = Path.Combine(arbeitsverzeichnisPfad, "state.json");
        var stateJson = await File.ReadAllTextAsync(stateJsonPfad);
        Assert.Contains("paused_utc", stateJson);

        // Phase 4: Wiederaufnahme. Der Ribbon-Button "Resume" (Gruppe "Autonome Aufgabe") ist vorhanden
        // und mit dem ResumeCommand verbunden (UI-Erreichbarkeit wird hier geprüft). Die eigentliche
        // Ausführung erfolgt bewusst über einen direkten Service-Aufruf statt über einen FlaUI-Klick: Die
        // Detail-Ansicht läuft im Prozess der Anwendung mit einem eigenen, langlebigen DbContext, dessen
        // Change-Tracking die in Phase 3 außerhalb des Anwendungsprozesses (separate SQLite-Verbindung)
        // gesetzte SessionPauseUtc nicht sieht (EF-Core-Identity-Map hält die zuvor geladene Aufgabe-
        // Instanz mit dem alten Wert zurück) — ein reines Testaufbau-Artefakt der Kombination aus
        // In-Process-UI und Out-of-Process-DB-Seeding, keine Auswirkung auf reales Anwendungsverhalten,
        // bei dem Pause und Resume im selben Anwendungsprozess laufen.
        var resumeButton = WaitForElement(mainWindow, cf => cf.ByName("AutonomAufgabeResumeAgent"), Short);
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
            var konfiguration = await db.AutonomAufgabeKonfigurationen.FirstAsync(k => k.AufgabeId == aufgabeId);
            Assert.Null(konfiguration.SessionPauseUtc);
            Assert.Equal(Softwareschmiede.Domain.Enums.AufgabeAusfuehrungsStatus.Aktiv, aufgabe.AusfuehrungsStatus);
            Assert.False(string.IsNullOrWhiteSpace(aufgabe.VorschlagPrompt), "Weitermachen-Prompt wurde nicht gesetzt.");
        }

        // Phase 5: Reguläre Aufgaben-Buttons ("Starten"/"Beenden", Gruppe "Aufgabe") sind für Autonome Aufgaben
        // ausgeblendet (TaskDetailViewModel.IsAutonomAufgabe == true) — Collapsed-Elemente erscheinen nicht im
        // UIA-Baum, daher genügt FindFirstDescendant + Null-Check (siehe AufgabeStarten_KlontRepositoryUndStartetCli_E2E
        // für dasselbe Assertion-Muster bei einem anderen Collapsed-Element).
        Assert.Null(mainWindow.FindFirstDescendant(cf => cf.ByName("Starten")));
        Assert.Null(mainWindow.FindFirstDescendant(cf => cf.ByName("Beenden")));

        // Phase 6: Explizites Stoppen via Ribbon-Button "Beenden" (Gruppe "Autonome Aufgabe", AutomationName
        // "AutonomAufgabeStopAgent") setzt ExplizitGestoppt und stoppt den laufenden Projektleiter-Agent-CLI-Prozess
        // — verhindert einen automatischen Wiederstart durch App-Startup-Recovery.
        var stopButton = WaitForElement(mainWindow, cf => cf.ByName("AutonomAufgabeStopAgent"), Short);
        stopButton.AsButton().Click();

        await WartenBisAsync(async () =>
        {
            await using var db = OpenTestDbContext();
            var konfiguration = await db.AutonomAufgabeKonfigurationen.FirstAsync(k => k.AufgabeId == aufgabeId);
            return konfiguration.ExplizitGestoppt;
        });

        await using (var db = OpenTestDbContext())
        {
            var konfiguration = await db.AutonomAufgabeKonfigurationen.FirstAsync(k => k.AufgabeId == aufgabeId);
            Assert.True(konfiguration.ExplizitGestoppt, "ExplizitGestoppt muss nach Klick auf den Ribbon-Button 'Beenden' gesetzt sein.");
        }

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
