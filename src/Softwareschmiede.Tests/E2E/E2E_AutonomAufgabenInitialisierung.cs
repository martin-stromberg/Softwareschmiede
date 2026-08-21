using FlaUI.Core.AutomationElements;
using Microsoft.EntityFrameworkCore;

namespace Softwareschmiede.Tests.E2E;

/// <summary>
/// E2E-Tests für die Initialisierung einer Autonomen Aufgabe (Issue 205).
///
/// Konsolidiert die drei im Umsetzungsplan beschriebenen Szenarien (Dialoganzeige,
/// Arbeitsverzeichnis-/Repository-Klon-Erstellung, Detail-View-Anzeige) in einer einzigen
/// Testmethode mit einem gemeinsamen App-Lifecycle (siehe CLAUDE.md, Abschnitt FlaUI-Konsolidierung).
/// </summary>
public partial class End2EndTest
{
    /// <summary>
    /// Szenario: Über die Aufgabendetailansicht wird der Initialisierungsdialog für eine Autonome
    /// Aufgabe geöffnet, das Formular ausgefüllt und bestätigt. Erwartung: Dialog zeigt alle
    /// Formularfelder, nach Bestätigung existiert das Arbeitsverzeichnis mit vollständiger Struktur
    /// inkl. Repository-Klon in clones/repo_main/, und die Detail-Ansicht der Autonomen Aufgabe
    /// öffnet sich automatisch und zeigt die Konfiguration an.
    /// </summary>
    protected async Task AutonomAufgabeInitialisierung_DialogErstelltArbeitsverzeichnisUndZeigtDetailAnsicht_E2E(Window mainWindow)
    {
        var repositoryFolderName = "autonom-init-repo";
        var projektName = "AutonomAufgabe-Init-Projekt";
        var aufgabeTitel = $"Autonome Testaufgabe {Guid.NewGuid():N}"[..40];

        // Vorherige Phase (CommandLineParameters) endet auf der Einstellungsseite, nicht dem Dashboard;
        // ohne explizite Rücknavigation sieht CreateProject deren "Speichern"-Button als Altlast an.
        NavigateBackToDashboard(mainWindow);

        SetupProjectMitNeuerAufgabeForStartedApp(mainWindow, repositoryFolderName, projektName);
        AufgabeTitelSetzen(mainWindow, aufgabeTitel);
        AufgabeDetailSpeichern(mainWindow, false);

        // LokalerKlonPfad wird sonst nur beim (ConPTY-abhängigen) CLI-Start gesetzt; hier direkt in der
        // Test-Datenbank hinterlegt, da für dieses Szenario kein CLI-Prozess benötigt wird.
        var quellVerzeichnis = CreateLocalSourceDirectory("autonom-init-quelle");
        var quellRepoPfad = Path.Combine(quellVerzeichnis, "autonom-init-quelle");
        await using (var seedDb = OpenTestDbContext())
        {
            var aufgabe = await seedDb.Aufgaben.FirstAsync(a => a.Titel == aufgabeTitel);
            aufgabe.LokalerKlonPfad = quellRepoPfad;
            await seedDb.SaveChangesAsync();
        }

        var initialisierenButton = WaitForElement(mainWindow, cf => cf.ByName("AutonomAufgabeInitialisieren"), Short);
        initialisierenButton.AsButton().Click();

        var dialog = WaitForWindow("Autonome Aufgabe initialisieren", Medium);
        WaitForElement(dialog, cf => cf.ByName("AutonomAufgabeProjektbranch"), Short);
        WaitForElement(dialog, cf => cf.ByName("AutonomAufgabePermissionsAuswahl"), Short);
        WaitForElement(dialog, cf => cf.ByName("AutonomAufgabeTokenBudget"), Short);
        WaitForElement(dialog, cf => cf.ByName("AutonomAufgabeLaufzeitLimit"), Short);

        var promptBox = WaitForElement(dialog, cf => cf.ByName("AutonomAufgabeInitialPrompt"), Short);
        promptBox.AsTextBox().Text = "Implementiere die Autonome Aufgabe vollständig gemäß Anforderung.";

        ConfirmDialog(dialog, "AutonomAufgabeBestaetigen");

        var detailFenster = WaitForWindow("Autonome Aufgabe", Long);
        WaitForElement(detailFenster, cf => cf.ByName("AutonomAufgabeStart"), Long);

        string arbeitsverzeichnisPfad;
        await using (var db = OpenTestDbContext())
        {
            var konfiguration = await db.AutonomAufgabeKonfigurationen
                .Include(k => k.Aufgabe)
                .FirstAsync(k => k.Aufgabe.Titel == aufgabeTitel);
            arbeitsverzeichnisPfad = konfiguration.ArbeitsverzeichnisPfad;
        }

        Assert.True(Directory.Exists(arbeitsverzeichnisPfad), "Arbeitsverzeichnis wurde nicht erstellt.");
        Assert.True(File.Exists(Path.Combine(arbeitsverzeichnisPfad, "plan.md")));
        Assert.True(File.Exists(Path.Combine(arbeitsverzeichnisPfad, "progress.md")));
        Assert.True(File.Exists(Path.Combine(arbeitsverzeichnisPfad, "governance.md")));
        Assert.True(File.Exists(Path.Combine(arbeitsverzeichnisPfad, "permissions.json")));
        Assert.True(File.Exists(Path.Combine(arbeitsverzeichnisPfad, "state.json")));
        Assert.True(Directory.Exists(Path.Combine(arbeitsverzeichnisPfad, "clones", "repo_main")), "Repository-Klon wurde nicht erstellt.");

        detailFenster.AsWindow().Close();

        NavigateBackFromTaskToProject(mainWindow);
        DeleteCurrentProject(mainWindow);
        NavigateBackToDashboard(mainWindow);
    }
}
