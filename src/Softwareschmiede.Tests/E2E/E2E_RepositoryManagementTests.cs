using FlaUI.Core.AutomationElements;
using Softwareschmiede.Tests.E2E.Views;
using Softwareschmiede.Tests.E2E.Views.Dialogs;

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

        var settings = new SettingsView(mainWindow).ForceShow();
        var dashboard = settings.ConfigureLocalDirectoryPlugin(sourceDirectory);
        var projectList = dashboard.Menu.NavigateToProjects();
        projectList.CreateProject(projektName);
        var projectDetail = projectList.OpenProject(projektName);

        var dialog = new RepositoryAssignDialogView(mainWindow).ForceShow();
        dialog.SelectFirstRepository();
        dialog.SetBaseBranch(basisBranch);
        var projectDetailAfterAssign = dialog.Confirm();

        var saved = await WaitForSavedSourceBranchAsync(repoFolderName, basisBranch);
        Assert.Equal(basisBranch, saved);
        Assert.Equal(basisBranch, projectDetailAfterAssign.GetBaseBranch());

        projectDetailAfterAssign.Menu.NavigateToDashboard();
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

        var projectList = new ProjectListView(mainWindow).ForceShow();
        var projectDetail = projectList.OpenProject("BasisBranch-Assign-Projekt");

        projectDetail.EditBaseBranch();
        projectDetail.SetBaseBranch(neuerBasisBranch);
        projectDetail.SaveBaseBranch();

        var saved = await WaitForSavedSourceBranchAsync(repoFolderName, neuerBasisBranch);
        Assert.Equal(neuerBasisBranch, saved);
        Assert.Equal(neuerBasisBranch, projectDetail.GetBaseBranch());

        projectDetail.DeleteProject();
        projectDetail.Menu.NavigateToDashboard();
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
