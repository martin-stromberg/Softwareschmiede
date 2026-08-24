using FlaUI.Core.AutomationElements;
using Microsoft.EntityFrameworkCore;

namespace Softwareschmiede.Tests.E2E;

/// <summary>
/// E2E-Tests für die Initialisierung einer Autonomen Aufgabe (Issue 205).
///
/// Konsolidiert die im Umsetzungsplan beschriebenen Szenarien (Dialoganzeige, Branch-Namenseingabe ohne
/// verfrühte Git-Operation, Arbeitsverzeichnis-/Repository-Klon-/Projektbranch-Erstellung beim Submit,
/// Detail-View-Anzeige) in einer einzigen Testmethode mit einem gemeinsamen App-Lifecycle (siehe CLAUDE.md,
/// Abschnitt FlaUI-Konsolidierung).
///
/// Die im Umsetzungsplan zusätzlich vorgesehenen Szenarien "Bestehender Remote-Branch (Checkout)" und
/// "Branch-Anlage-Fehler bei erfolgreichem Klon" sind über FlaUI nicht sinnvoll abbildbar: Das in dieser
/// Testumgebung einzige verfügbare SCM-Plugin (LocalDirectoryPlugin) unterstützt weder
/// GetRemoteBranchesAsync noch CheckoutRemoteBranchAsync (wirft NotSupportedException), und ein
/// Service-Mock lässt sich nicht in die laufende, über echte Dependency Injection konfigurierte App
/// einschleusen. Beide Szenarien sind stattdessen durch
/// AutonomAufgabenInitialisierungsServiceTests.ErstelleProjektbranchAsync_CheckoutRemoteBranch_WennExistent()
/// und ErstelleProjektbranchAsync_WirftException_BeiGitFehler() abgedeckt.
/// </summary>
public partial class End2EndTest
{
    /// <summary>
    /// Szenario: Über die Aufgabendetailansicht wird der Initialisierungsdialog für eine Autonome
    /// Aufgabe geöffnet und ein neuer Projektbranch-Name eingegeben — dies löst (anders als vor Issue 205)
    /// keine Git-Operation aus. Nach Bestätigen des gesamten Formulars erstellt der Service
    /// Arbeitsverzeichnis, Repository-Klon (direkt von der Repository-URL der Aufgabe, nicht mehr von einem
    /// vorher gestarteten LokalerKlonPfad) und legt den gewählten Projektbranch im Klon tatsächlich an. Die
    /// Detail-Ansicht der Autonomen Aufgabe öffnet sich automatisch und zeigt die Konfiguration an.
    /// </summary>
    /// <param name="mainWindow">Das Hauptfenster der Anwendung.</param>
    protected async Task AutonomAufgabeInitialisierung_DialogErstelltArbeitsverzeichnisUndZeigtDetailAnsicht_E2E(Window mainWindow)
    {
        var repositoryFolderName = "autonom-init-repo";
        var projektName = "AutonomAufgabe-Init-Projekt";
        var aufgabeTitel = $"Autonome Testaufgabe {Guid.NewGuid():N}"[..40];
        var neuerProjektBranch = $"feature-autonom-{Guid.NewGuid():N}"[..40];

        // Vorherige Phase (CommandLineParameters) endet auf der Einstellungsseite, nicht dem Dashboard;
        // ohne explizite Rücknavigation sieht CreateProject deren "Speichern"-Button als Altlast an.
        NavigateBackToDashboard(mainWindow);

        SetupProjectMitNeuerAufgabeForStartedApp(mainWindow, repositoryFolderName, projektName);
        AufgabeTitelSetzen(mainWindow, aufgabeTitel);
        AufgabeDetailSpeichern(mainWindow, false);

        var initialisierenButton = WaitForElement(mainWindow, cf => cf.ByName("AutonomAufgabeInitialisieren"), Short);
        initialisierenButton.AsButton().Click();

        var dialog = WaitForWindow("Autonome Aufgabe initialisieren", Medium);
        // Das zugewiesene LocalDirectoryPlugin-Repository kennt keine Remote-Branches
        // (GetRemoteBranchesAsync wirft NotSupportedException), daher fällt der Branch-Bereich auf die
        // manuelle Texteingabe zurück. Der neue Branch-Name wird direkt eingegeben (NeuenBranchAnlegenAsync
        // über den "+"-Button validiert nur zusätzlich den Namen ohne Git-Aufruf und ist bereits durch
        // AutonomAufgabeInitialisierungsDialogViewModelTests_BranchUndVorlagen abgedeckt).
        var projektbranchEingabe = WaitForElement(dialog, cf => cf.ByName("AutonomAufgabeProjektbranchEingabe"), Short);
        projektbranchEingabe.AsTextBox().Text = neuerProjektBranch;
        WaitForElement(dialog, cf => cf.ByName("AutonomAufgabePermissionsAuswahl"), Short);
        WaitForElement(dialog, cf => cf.ByName("AutonomAufgabeTokenBudget"), Short);
        WaitForElement(dialog, cf => cf.ByName("AutonomAufgabeLaufzeitLimit"), Short);

        var promptBox = WaitForElement(dialog, cf => cf.ByName("AutonomAufgabeInitialPrompt"), Short);
        promptBox.AsTextBox().Text = "Implementiere die Autonome Aufgabe vollständig gemäß Anforderung.";

        ConfirmDialog(dialog, "AutonomAufgabeBestaetigen");

        var detailFenster = WaitForWindow("Autonome Aufgabe", Long);
        WaitForElement(detailFenster, cf => cf.ByName("AutonomAufgabeStart"), Long);

        string arbeitsverzeichnisPfad;
        string projektBranchName;
        await using (var db = OpenTestDbContext())
        {
            var konfiguration = await db.AutonomAufgabeKonfigurationen
                .Include(k => k.Aufgabe)
                .FirstAsync(k => k.Aufgabe.Titel == aufgabeTitel);
            arbeitsverzeichnisPfad = konfiguration.ArbeitsverzeichnisPfad;
            projektBranchName = konfiguration.ProjektBranchName;
        }

        Assert.Equal(neuerProjektBranch, projektBranchName);
        Assert.True(Directory.Exists(arbeitsverzeichnisPfad), "Arbeitsverzeichnis wurde nicht erstellt.");
        Assert.True(File.Exists(Path.Combine(arbeitsverzeichnisPfad, "plan.md")));
        Assert.True(File.Exists(Path.Combine(arbeitsverzeichnisPfad, "progress.md")));
        Assert.True(File.Exists(Path.Combine(arbeitsverzeichnisPfad, "governance.md")));
        Assert.True(File.Exists(Path.Combine(arbeitsverzeichnisPfad, "permissions.json")));
        Assert.True(File.Exists(Path.Combine(arbeitsverzeichnisPfad, "state.json")));
        var repoMainPfad = Path.Combine(arbeitsverzeichnisPfad, "clones", "repo_main");
        Assert.True(Directory.Exists(repoMainPfad), "Repository-Klon wurde nicht erstellt.");

        // LocalDirectoryPlugin legt im (hier standardmäßig verwendeten) InSourceDirectory-Modus unter
        // clones/repo_main nur eine Pointer-Datei ab, die auf das tatsächliche Quellverzeichnis verweist;
        // der tatsächliche Git-Klon (in dem der Branch angelegt wurde) liegt dort.
        var tatsaechlicherRepoPfad = ResolveLocalWorkspacePointerPath(repoMainPfad);
        Assert.True(GitBranchExistsLocally(tatsaechlicherRepoPfad, neuerProjektBranch), "Projektbranch wurde im Repository-Klon nicht angelegt.");
        // Der eigentliche Zweck von ErstelleProjektbranchAsync ist es, repo_main tatsächlich auf den neuen
        // Projektbranch umzuschalten (nicht nur den Branch-Ref anzulegen), damit nachfolgend erzeugte
        // Unteragenten-Branches von ihm abzweigen. Prüft daher zusätzlich zur reinen Existenz, dass HEAD
        // tatsächlich auf den neuen Branch zeigt.
        Assert.Equal(neuerProjektBranch, GitCurrentBranch(tatsaechlicherRepoPfad));

        detailFenster.AsWindow().Close();

        NavigateBackFromTaskToProject(mainWindow);
        DeleteCurrentProject(mainWindow);
        NavigateBackToDashboard(mainWindow);
    }
}
