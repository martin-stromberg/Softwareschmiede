using FlaUI.Core.AutomationElements;

namespace Softwareschmiede.Tests.E2E;

/// <summary>
/// E2E-Tests für die Konfiguration eines Repository-Initialisierungsskripts in der Projektdetailansicht
/// (Issue #228): Label, Eingabefeld (editierbare ComboBox mit Vorschlägen) sowie Speichern-/Cancel-Buttons.
///
/// Das lokale Testrepository enthält echte Skriptdateien ("scripts/init.ps1", "scripts/deploy.sh"), damit
/// <c>InitialisierungsskriptSuggestionen</c> tatsächlich befüllt wird — <c>LocalDirectoryPlugin.CollectDirectoryEntries</c>
/// liefert seit der Issue-228-Nacharbeit sowohl Verzeichnis- als auch Datei-Einträge (zuvor wurden Dateien
/// vollständig verworfen, wodurch die Vorschlagsliste immer leer blieb). Dieselbe Testinstanz deckt damit
/// sowohl die Auswahl aus der Vorschlagsliste als auch die Live-Filterung per Freitext sowie die manuelle
/// Eingabe eines nicht in der Liste enthaltenen Pfads ab.
///
/// Konsolidierung: Alle Aspekte (Auswahl aus Vorschlagsliste, Live-Filter, Freitext-Eingabe eines unbekannten
/// Skripts, nachträgliches Bearbeiten mit Abbruch) laufen als aufeinanderfolgende Phasen in einem gemeinsamen
/// App-Lifecycle, um die Laufzeit der FlaUI-E2E-Suite gering zu halten.
///
/// CI-Regular-Lauf: dotnet test --filter "Category!=OsInterface"
/// </summary>
[Trait("Category", "E2E")]
[OsInterface]
[Collection("E2E")]
public sealed class E2E_RepositoryInitializationConfigTests : WpfTestBase
{
    private const string RepoFolderName = "Init-Config-Repo";
    private const string ProjectName = "Init-Config-Projekt";
    private const string SuggestedScript = "scripts/init.ps1";
    private const string OtherSuggestedFile = "scripts/deploy.sh";
    private const string UnknownScript = "scripts/does-not-exist.ps1";

    /// <summary>
    /// Führt alle Initialisierungsskript-Konfigurationsszenarien nacheinander im selben App-Lifecycle aus:
    /// Auswahl aus der Vorschlagsliste, Live-Filterung, Speichern eines nicht vorgeschlagenen Skripts, danach
    /// die nachträgliche Bearbeitung mit Abbruch.
    /// </summary>
    [Fact]
    public async Task InitializationscriptConfiguration()
    {
        var mainWindow = LaunchAppAndGetMainWindow();

        await ConfigureInitializationscript_SelectionFilterAndFreetext_E2E(mainWindow);
        await InitializationscriptEdit_Cancel_DiscardsChange_E2E(mainWindow);
    }

    /// <summary>
    /// Szenario: Ein Repository mit echten Skriptdateien wird zugewiesen. Geprüft werden drei Aspekte der
    /// editierbaren ComboBox in einem Durchlauf: (1) Auswahl eines vorgeschlagenen Skripts per Klick aus der
    /// (jetzt korrekt gefüllten) Vorschlagsliste, (2) Live-Filterung der Vorschlagsliste per Freitexteingabe,
    /// (3) Speichern eines frei eingegebenen, nicht in der Vorschlagsliste enthaltenen Skriptpfads. Das Öffnen
    /// des Dropdowns erfolgt über <c>ExpandCollapsePattern</c> statt über einen Koordinaten-Klick, da der
    /// ToggleButton im ComboBox-Template bei editierbaren ComboBoxen nur noch die Pfeil-Spalte abdeckt (die
    /// Text-Spalte wird jetzt von der neu ergänzten <c>PART_EditableTextBox</c> für Fokus/Texteingabe benötigt).
    /// </summary>
    /// <param name="mainWindow">Das bereits laufende Hauptfenster, in dem diese Phase ausgeführt wird.</param>
    private async Task ConfigureInitializationscript_SelectionFilterAndFreetext_E2E(Window mainWindow)
    {
        var sourceDirectory = CreateLocalSourceDirectory(RepoFolderName);
        var scriptsDirectory = Path.Combine(sourceDirectory, RepoFolderName, "scripts");
        Directory.CreateDirectory(scriptsDirectory);
        File.WriteAllText(Path.Combine(scriptsDirectory, "init.ps1"), "# init");
        File.WriteAllText(Path.Combine(scriptsDirectory, "deploy.sh"), "# deploy");

        ConfigureLocalDirectoryPlugin(mainWindow, sourceDirectory);
        NavigateToProjects(mainWindow);
        CreateAndOpenProject(mainWindow, ProjectName);

        AssignLocalDirectoryRepository(mainWindow);

        var loadButton = WaitForElement(mainWindow, cf => cf.ByName("LoadInitializationScript"), Short);
        loadButton.AsButton().ClickInForeground();

        var selectionBox = WaitForElement(mainWindow, cf => cf.ByName("InitializationScriptSelectionComboBox"), Short);

        // (1) Auswahl aus der Vorschlagsliste: Beide erzeugten Skriptdateien müssen als Vorschlag erscheinen
        // und ein Klick auf einen Vorschlag übernimmt ihn als ausgewähltes Skript.
        selectionBox.Patterns.ExpandCollapse.Pattern.Expand();
        var suggestion = WaitForElement(selectionBox, cf => cf.ByName(SuggestedScript), Short);
        suggestion.ClickInForeground();
        Assert.Equal(SuggestedScript, selectionBox.AsComboBox().EditableText);

        // (2) Live-Filter: Freitexteingabe engt die (erneut geöffnete) Vorschlagsliste auf passende Einträge ein.
        selectionBox.Patterns.ExpandCollapse.Pattern.Expand();
        selectionBox.AsComboBox().EditableText = "deploy";
        var filteredElement = WaitForElement(selectionBox, cf => cf.ByName(OtherSuggestedFile), Short);
        Assert.NotNull(filteredElement);
        Assert.Null(selectionBox.FindFirstDescendant(cf => cf.ByName(SuggestedScript)));

        // (3) Freitext-Eingabe eines nicht vorgeschlagenen Pfads wird trotzdem als Initialisierungsskript akzeptiert.
        // Das Dropdown aus der Live-Filterung (2) steht noch offen (ExpandCollapseState.Expanded); ein Klick
        // auf "Speichern" würde vom offenen Popup abgefangen und nur dessen Schließen auslösen, statt den
        // Button zu erreichen (WPF schließt ein offenes Popup beim ersten Klick außerhalb, ohne den Klick an
        // darunterliegende Controls weiterzureichen). Daher muss das Dropdown vor dem Klick explizit geschlossen werden.
        selectionBox.AsComboBox().EditableText = UnknownScript;
        selectionBox.Patterns.ExpandCollapse.Pattern.Collapse();

        var saveButton = WaitForElement(mainWindow, cf => cf.ByName("SaveInitializationScript"), Short);
        saveButton.AsButton().ClickInForeground();

        var saved = await WaitForSavedInitializationscriptAsync(RepoFolderName, UnknownScript);
        Assert.Equal(UnknownScript, saved);

        var display = WaitForElement(mainWindow, cf => cf.ByAutomationId("InitializationScriptDisplay"), Short);
        Assert.Equal(UnknownScript, display.Name);
    }

    /// <summary>
    /// Szenario: Bearbeitung eines bereits konfigurierten Initialisierungsskripts wird abgebrochen.
    /// Erwartung: Die Änderung wird verworfen, der ursprüngliche Wert bleibt gespeichert und angezeigt.
    /// </summary>
    /// <param name="mainWindow">Das bereits laufende Hauptfenster, in dem diese Phase ausgeführt wird.</param>
    private async Task InitializationscriptEdit_Cancel_DiscardsChange_E2E(Window mainWindow)
    {
        const string changedScript = "scripts/changed.ps1";

        var loadButton = WaitForElement(mainWindow, cf => cf.ByName("LoadInitializationScript"), Short);
        loadButton.AsButton().ClickInForeground();

        var selectionBox = WaitForElement(mainWindow, cf => cf.ByName("InitializationScriptSelectionComboBox"), Short);
        selectionBox.AsComboBox().EditableText = changedScript;

        var cancelButton = WaitForElement(mainWindow, cf => cf.ByName("CancelInitializationScript"), Short);
        cancelButton.AsButton().ClickInForeground();

        var display = WaitForElement(mainWindow, cf => cf.ByAutomationId("InitializationScriptDisplay"), Short);
        Assert.Equal(UnknownScript, display.Name);

        var saved = await WaitForSavedInitializationscriptAsync(RepoFolderName, UnknownScript);
        Assert.Equal(UnknownScript, saved);

        DeleteCurrentProject(mainWindow);
        NavigateBackToDashboard(mainWindow);
    }

    private async Task<string?> WaitForSavedInitializationscriptAsync(string repositoryName, string expected)
    {
        var deadline = DateTime.UtcNow + Medium;
        string? saved = null;
        while (DateTime.UtcNow < deadline)
        {
            await using var db = OpenTestDbContext();
            var repo = db.GitRepositories.FirstOrDefault(r => r.RepositoryName == repositoryName);
            saved = repo is null
                ? null
                : db.RepositoryInitializationConfigurations.Where(c => c.GitRepositoryId == repo.Id).SingleOrDefault()?.InitializationScriptRelativePath;
            if (string.Equals(saved, expected, StringComparison.Ordinal))
                return saved;

            await Task.Delay(200);
        }

        return saved;
    }
}
