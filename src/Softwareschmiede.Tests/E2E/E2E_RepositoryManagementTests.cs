using FlaUI.Core.AutomationElements;
using Softwareschmiede.Infrastructure.Services;

namespace Softwareschmiede.Tests.E2E;

/// <summary>
/// E2E-Tests für die Basis-Branch-Konfiguration eines Repositories (Issue #188): Auswahl beim
/// Zuweisen eines Repositories zu einem Projekt sowie nachträgliche Bearbeitung in der
/// Projektdetailansicht.
///
/// Der Testmodus lädt als SCM-Plugin ausschließlich <c>LocalDirectoryPlugin</c>, das
/// <c>GetRemoteBranchesAsync</c>/<c>GetDefaultBranchAsync</c> nicht unterstützt (siehe
/// <c>LocalDirectoryPlugin.GetRemoteBranchesAsync</c>). Die Basis-Branch-Eingabefelder in
/// <see cref="Softwareschmiede.App.ViewModels.RepositoryAssignViewModel"/>/<see cref="Softwareschmiede.App.ViewModels.ProjectDetailViewModel"/> degradieren in
/// diesem Fall bewusst auf freie Texteingabe ohne Vorschlagsliste/Validierung (siehe
/// <c>RepositoryAssignViewModel.LoadSourceBranchesAsync</c>), sodass beide Szenarien hier den
/// manuellen Eingabepfad prüfen.
///
/// Konsolidierung: Beide Szenarien (Zuweisung mit Basis-Branch-Auswahl, nachträgliche Bearbeitung)
/// laufen als aufeinanderfolgende Phasen in einem gemeinsamen App-Lifecycle, um die Laufzeit der
/// FlaUI-E2E-Suite gering zu halten.
///
/// CI-Regular-Lauf: dotnet test --filter "Category!=OsInterface"
/// </summary>
[Trait("Category", "E2E")]
[OsInterface]
[Collection("E2E")]
public sealed class E2E_RepositoryManagementTests : WpfTestBase
{
    /// <summary>
    /// Führt beide Basis-Branch-Szenarien nacheinander im selben App-Lifecycle aus: Zuweisung eines
    /// Repositories mit manuell eingegebenem Basis-Branch, danach die nachträgliche Bearbeitung dieses
    /// Basis-Branches in der Projektdetailansicht.
    /// </summary>
    [Fact]
    public async Task BasisBranchVerwaltung()
    {
        var mainWindow = LaunchAppAndGetMainWindow();

        await RepositoryZuweisen_MitBasisBranchAuswahl_SpeichertUndZeigtBasisBranch_E2E(mainWindow);
        await BasisBranchBearbeiten_InProjektdetailansicht_SpeichertNeuenBasisBranch_E2E(mainWindow);
    }

    /// <summary>
    /// Szenario: Repository-Zuordnung mit Basis-Branch-Auswahl.
    /// Erwartung: Der manuell eingegebene Basis-Branch wird persistiert und in der
    /// Projektdetailansicht angezeigt.
    /// </summary>
    /// <param name="mainWindow">Das bereits laufende Hauptfenster, in dem diese Phase ausgeführt wird.</param>
    private async Task RepositoryZuweisen_MitBasisBranchAuswahl_SpeichertUndZeigtBasisBranch_E2E(Window mainWindow)
    {
        const string repoFolderName = "BasisBranch-Assign-Repo";
        const string projektName = "BasisBranch-Assign-Projekt";
        const string basisBranch = "staging";

        var sourceDirectory = CreateLocalSourceDirectory(repoFolderName);

        ConfigureLocalDirectoryPlugin(mainWindow, sourceDirectory);
        NavigateToProjects(mainWindow);
        CreateAndOpenProject(mainWindow, projektName);

        var dialog = OpenRepositoryAssignDialog(mainWindow);
        var repositoryItem = WaitForFirstRepositoryItem(dialog);
        repositoryItem.Click();

        var basisBranchEingabe = WaitForEnabledElement(dialog, "BasisBranchEingabe", Short);
        basisBranchEingabe.AsTextBox().Text = basisBranch;

        ConfirmDialog(dialog, "Zuweisen");

        var saved = await WaitForSavedSourceBranchAsync(repoFolderName, basisBranch);
        Assert.Equal(basisBranch, saved);

        var anzeige = WaitForElement(mainWindow, cf => cf.ByAutomationId("BasisBranchAnzeige"), Short);
        Assert.Equal(basisBranch, anzeige.Name);

        NavigateBackToDashboard(mainWindow);
    }

    /// <summary>
    /// Szenario: Basis-Branch-Bearbeitung in der Projektdetailansicht.
    /// Erwartung: Der Benutzer öffnet den Edit-Modus, ändert den Basis-Branch und die neue Auswahl
    /// wird persistiert und angezeigt.
    /// </summary>
    /// <param name="mainWindow">Das bereits laufende Hauptfenster, in dem diese Phase ausgeführt wird.</param>
    private async Task BasisBranchBearbeiten_InProjektdetailansicht_SpeichertNeuenBasisBranch_E2E(Window mainWindow)
    {
        const string repoFolderName = "BasisBranch-Assign-Repo";
        const string neuerBasisBranch = "release";

        NavigateToProjects(mainWindow);
        OpenProject(mainWindow, "BasisBranch-Assign-Projekt");

        var bearbeitenButton = WaitForElement(mainWindow, cf => cf.ByName("BasisBranchBearbeiten"), Short);
        bearbeitenButton.AsButton().Click();

        var editEingabe = WaitForElement(mainWindow, cf => cf.ByName("BasisBranchBearbeitenEingabe"), Short);
        editEingabe.AsTextBox().Text = neuerBasisBranch;

        var speichernButton = WaitForElement(mainWindow, cf => cf.ByName("BasisBranchSpeichern"), Short);
        speichernButton.AsButton().Click();

        var saved = await WaitForSavedSourceBranchAsync(repoFolderName, neuerBasisBranch);
        Assert.Equal(neuerBasisBranch, saved);

        var anzeige = WaitForElement(mainWindow, cf => cf.ByAutomationId("BasisBranchAnzeige"), Short);
        Assert.Equal(neuerBasisBranch, anzeige.Name);

        DeleteCurrentProject(mainWindow);
        NavigateBackToDashboard(mainWindow);
    }

    /// <summary>
    /// Wartet, bis ein benanntes Element gefunden wird UND aktiviert ist (nicht mehr durch
    /// IsLoadingSourceBranches deaktiviert).
    /// </summary>
    private static AutomationElement WaitForEnabledElement(AutomationElement parent, string automationName, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var element = parent.FindFirstDescendant(cf => cf.ByName(automationName));
            if (element is not null && element.IsEnabled)
                return element;

            Thread.Sleep(200);
        }

        throw new TimeoutException($"Element '{automationName}' wurde nicht innerhalb von {timeout.TotalSeconds}s aktiviert gefunden.");
    }

    private static void ConfirmDialog(AutomationElement dialog, string buttonName)
    {
        var button = WaitForElement(dialog, cf => cf.ByName(buttonName), Short);
        button.AsButton().Click();
    }

    private async Task<string?> WaitForSavedSourceBranchAsync(string repositoryName, string expected)
    {
        var deadline = DateTime.UtcNow + Medium;
        string? saved = null;
        while (DateTime.UtcNow < deadline)
        {
            await using var db = OpenTestDbContext();
            var repo = db.GitRepositories.FirstOrDefault(r => r.RepositoryName == repositoryName);
            saved = repo?.DefaultSourceBranchName;
            if (string.Equals(saved, expected, StringComparison.Ordinal))
                return saved;

            await Task.Delay(200);
        }

        return saved;
    }
}
