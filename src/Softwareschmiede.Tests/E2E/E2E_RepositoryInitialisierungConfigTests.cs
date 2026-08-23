using FlaUI.Core.AutomationElements;

namespace Softwareschmiede.Tests.E2E;

/// <summary>
/// E2E-Tests für die Konfiguration eines Repository-Initialisierungsskripts in der Projektdetailansicht
/// (Issue #228): Label, Eingabefeld (editierbare ComboBox mit Vorschlägen) sowie Speichern-/Abbrechen-Buttons.
///
/// Der Testmodus lädt als SCM-Plugin ausschließlich <c>LocalDirectoryPlugin</c>, dessen
/// <c>GetRepositoryStructureLoadResultAsync</c> ausschließlich Verzeichnis-Einträge liefert (siehe
/// <c>LocalDirectoryPlugin.CollectDirectoryEntries</c>), sodass <c>InitialisierungsskriptSuggestionen</c>
/// hier stets leer bleibt und die editierbare ComboBox über den manuellen Freitext-Eingabepfad geprüft wird.
///
/// Konsolidierung: Beide Szenarien (Konfigurieren und Speichern, nachträgliches Bearbeiten mit Abbruch)
/// laufen als aufeinanderfolgende Phasen in einem gemeinsamen App-Lifecycle, um die Laufzeit der
/// FlaUI-E2E-Suite gering zu halten.
///
/// CI-Regular-Lauf: dotnet test --filter "Category!=OsInterface"
/// </summary>
[Trait("Category", "E2E")]
[OsInterface]
[Collection("E2E")]
public sealed class E2E_RepositoryInitialisierungConfigTests : WpfTestBase
{
    private const string RepoFolderName = "Init-Config-Repo";
    private const string ProjektName = "Init-Config-Projekt";

    /// <summary>
    /// Führt beide Initialisierungsskript-Konfigurationsszenarien nacheinander im selben App-Lifecycle aus:
    /// Konfigurieren und Speichern eines Skripts, danach die nachträgliche Bearbeitung mit Abbruch.
    /// </summary>
    [Fact]
    public async Task InitialisierungsskriptKonfiguration()
    {
        var mainWindow = LaunchAppAndGetMainWindow();

        await InitialisierungsskriptKonfigurieren_SpeichertUndZeigtSkript_E2E(mainWindow);
        await InitialisierungsskriptBearbeiten_Abbrechen_VerwirftAenderung_E2E(mainWindow);
    }

    /// <summary>
    /// Szenario: Ein Repository wird zugewiesen und ein Initialisierungsskript konfiguriert.
    /// Erwartung: Der manuell eingegebene Skriptpfad wird persistiert und in der Anzeige übernommen.
    /// </summary>
    /// <param name="mainWindow">Das bereits laufende Hauptfenster, in dem diese Phase ausgeführt wird.</param>
    private async Task InitialisierungsskriptKonfigurieren_SpeichertUndZeigtSkript_E2E(Window mainWindow)
    {
        const string skriptPfad = "scripts/init.ps1";

        var sourceDirectory = CreateLocalSourceDirectory(RepoFolderName);

        ConfigureLocalDirectoryPlugin(mainWindow, sourceDirectory);
        NavigateToProjects(mainWindow);
        CreateAndOpenProject(mainWindow, ProjektName);

        AssignLocalDirectoryRepository(mainWindow);

        var ladenButton = WaitForElement(mainWindow, cf => cf.ByName("InitialisierungsskriptLaden"), Short);
        ladenButton.AsButton().Click();

        var auswahlBox = WaitForElement(mainWindow, cf => cf.ByName("InitialisierungsskriptAuswahlComboBox"), Short);
        auswahlBox.AsComboBox().EditableText = skriptPfad;

        var speichernButton = WaitForElement(mainWindow, cf => cf.ByName("InitialisierungsskriptSpeichern"), Short);
        speichernButton.AsButton().Click();

        var saved = await WaitForSavedInitialisierungsskriptAsync(RepoFolderName, skriptPfad);
        Assert.Equal(skriptPfad, saved);

        var anzeige = WaitForElement(mainWindow, cf => cf.ByAutomationId("InitialisierungsskriptAnzeige"), Short);
        Assert.Equal(skriptPfad, anzeige.Name);
    }

    /// <summary>
    /// Szenario: Bearbeitung eines bereits konfigurierten Initialisierungsskripts wird abgebrochen.
    /// Erwartung: Die Änderung wird verworfen, der ursprüngliche Wert bleibt gespeichert und angezeigt.
    /// </summary>
    /// <param name="mainWindow">Das bereits laufende Hauptfenster, in dem diese Phase ausgeführt wird.</param>
    private async Task InitialisierungsskriptBearbeiten_Abbrechen_VerwirftAenderung_E2E(Window mainWindow)
    {
        const string ursprungsSkript = "scripts/init.ps1";
        const string geaenderterSkript = "scripts/changed.ps1";

        var ladenButton = WaitForElement(mainWindow, cf => cf.ByName("InitialisierungsskriptLaden"), Short);
        ladenButton.AsButton().Click();

        var auswahlBox = WaitForElement(mainWindow, cf => cf.ByName("InitialisierungsskriptAuswahlComboBox"), Short);
        auswahlBox.AsComboBox().EditableText = geaenderterSkript;

        var abbrechenButton = WaitForElement(mainWindow, cf => cf.ByName("InitialisierungsskriptAbbrechen"), Short);
        abbrechenButton.AsButton().Click();

        var anzeige = WaitForElement(mainWindow, cf => cf.ByAutomationId("InitialisierungsskriptAnzeige"), Short);
        Assert.Equal(ursprungsSkript, anzeige.Name);

        var saved = await WaitForSavedInitialisierungsskriptAsync(RepoFolderName, ursprungsSkript);
        Assert.Equal(ursprungsSkript, saved);

        DeleteCurrentProject(mainWindow);
        NavigateBackToDashboard(mainWindow);
    }

    private async Task<string?> WaitForSavedInitialisierungsskriptAsync(string repositoryName, string expected)
    {
        var deadline = DateTime.UtcNow + Medium;
        string? saved = null;
        while (DateTime.UtcNow < deadline)
        {
            await using var db = OpenTestDbContext();
            var repo = db.GitRepositories.FirstOrDefault(r => r.RepositoryName == repositoryName);
            saved = repo is null
                ? null
                : db.RepositoryInitialisierungKonfigurationen.Where(c => c.GitRepositoryId == repo.Id).SingleOrDefault()?.InitialisierungsskriptRelativePath;
            if (string.Equals(saved, expected, StringComparison.Ordinal))
                return saved;

            await Task.Delay(200);
        }

        return saved;
    }
}
