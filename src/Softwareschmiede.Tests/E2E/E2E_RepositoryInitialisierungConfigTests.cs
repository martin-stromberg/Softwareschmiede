using FlaUI.Core.AutomationElements;

namespace Softwareschmiede.Tests.E2E;

/// <summary>
/// E2E-Tests für die Konfiguration eines Repository-Initialisierungsskripts in der Projektdetailansicht
/// (Issue #228): Label, Eingabefeld (editierbare ComboBox mit Vorschlägen) sowie Speichern-/Abbrechen-Buttons.
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
public sealed class E2E_RepositoryInitialisierungConfigTests : WpfTestBase
{
    private const string RepoFolderName = "Init-Config-Repo";
    private const string ProjektName = "Init-Config-Projekt";
    private const string VorgeschlagenesSkript = "scripts/init.ps1";
    private const string AndereVorgeschlageneDatei = "scripts/deploy.sh";
    private const string UnbekanntesSkript = "scripts/does-not-exist.ps1";

    /// <summary>
    /// Führt alle Initialisierungsskript-Konfigurationsszenarien nacheinander im selben App-Lifecycle aus:
    /// Auswahl aus der Vorschlagsliste, Live-Filterung, Speichern eines nicht vorgeschlagenen Skripts, danach
    /// die nachträgliche Bearbeitung mit Abbruch.
    /// </summary>
    [Fact]
    public async Task InitialisierungsskriptKonfiguration()
    {
        var mainWindow = LaunchAppAndGetMainWindow();

        await InitialisierungsskriptKonfigurieren_AuswahlFilterUndFreitext_E2E(mainWindow);
        await InitialisierungsskriptBearbeiten_Abbrechen_VerwirftAenderung_E2E(mainWindow);
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
    private async Task InitialisierungsskriptKonfigurieren_AuswahlFilterUndFreitext_E2E(Window mainWindow)
    {
        var sourceDirectory = CreateLocalSourceDirectory(RepoFolderName);
        var scriptsDirectory = Path.Combine(sourceDirectory, RepoFolderName, "scripts");
        Directory.CreateDirectory(scriptsDirectory);
        File.WriteAllText(Path.Combine(scriptsDirectory, "init.ps1"), "# init");
        File.WriteAllText(Path.Combine(scriptsDirectory, "deploy.sh"), "# deploy");

        ConfigureLocalDirectoryPlugin(mainWindow, sourceDirectory);
        NavigateToProjects(mainWindow);
        CreateAndOpenProject(mainWindow, ProjektName);

        AssignLocalDirectoryRepository(mainWindow);

        var ladenButton = WaitForElement(mainWindow, cf => cf.ByName("InitialisierungsskriptLaden"), Short);
        ladenButton.AsButton().Click();

        var auswahlBox = WaitForElement(mainWindow, cf => cf.ByName("InitialisierungsskriptAuswahlComboBox"), Short);

        // (1) Auswahl aus der Vorschlagsliste: Beide erzeugten Skriptdateien müssen als Vorschlag erscheinen
        // und ein Klick auf einen Vorschlag übernimmt ihn als ausgewähltes Skript.
        auswahlBox.Patterns.ExpandCollapse.Pattern.Expand();
        var vorschlag = WaitForElement(auswahlBox, cf => cf.ByName(VorgeschlagenesSkript), Short);
        vorschlag.Click();
        Assert.Equal(VorgeschlagenesSkript, auswahlBox.AsComboBox().EditableText);

        // (2) Live-Filter: Freitexteingabe engt die (erneut geöffnete) Vorschlagsliste auf passende Einträge ein.
        auswahlBox.Patterns.ExpandCollapse.Pattern.Expand();
        auswahlBox.AsComboBox().EditableText = "deploy";
        var gefiltertesElement = WaitForElement(auswahlBox, cf => cf.ByName(AndereVorgeschlageneDatei), Short);
        Assert.NotNull(gefiltertesElement);
        Assert.Null(auswahlBox.FindFirstDescendant(cf => cf.ByName(VorgeschlagenesSkript)));

        // (3) Freitext-Eingabe eines nicht vorgeschlagenen Pfads wird trotzdem als Initialisierungsskript akzeptiert.
        // Das Dropdown aus der Live-Filterung (2) steht noch offen (ExpandCollapseState.Expanded); ein Klick
        // auf "Speichern" würde vom offenen Popup abgefangen und nur dessen Schließen auslösen, statt den
        // Button zu erreichen (WPF schließt ein offenes Popup beim ersten Klick außerhalb, ohne den Klick an
        // darunterliegende Controls weiterzureichen). Daher muss das Dropdown vor dem Klick explizit geschlossen werden.
        auswahlBox.AsComboBox().EditableText = UnbekanntesSkript;
        auswahlBox.Patterns.ExpandCollapse.Pattern.Collapse();

        var speichernButton = WaitForElement(mainWindow, cf => cf.ByName("InitialisierungsskriptSpeichern"), Short);
        speichernButton.AsButton().Click();

        var saved = await WaitForSavedInitialisierungsskriptAsync(RepoFolderName, UnbekanntesSkript);
        Assert.Equal(UnbekanntesSkript, saved);

        var anzeige = WaitForElement(mainWindow, cf => cf.ByAutomationId("InitialisierungsskriptAnzeige"), Short);
        Assert.Equal(UnbekanntesSkript, anzeige.Name);
    }

    /// <summary>
    /// Szenario: Bearbeitung eines bereits konfigurierten Initialisierungsskripts wird abgebrochen.
    /// Erwartung: Die Änderung wird verworfen, der ursprüngliche Wert bleibt gespeichert und angezeigt.
    /// </summary>
    /// <param name="mainWindow">Das bereits laufende Hauptfenster, in dem diese Phase ausgeführt wird.</param>
    private async Task InitialisierungsskriptBearbeiten_Abbrechen_VerwirftAenderung_E2E(Window mainWindow)
    {
        const string geaenderterSkript = "scripts/changed.ps1";

        var ladenButton = WaitForElement(mainWindow, cf => cf.ByName("InitialisierungsskriptLaden"), Short);
        ladenButton.AsButton().Click();

        var auswahlBox = WaitForElement(mainWindow, cf => cf.ByName("InitialisierungsskriptAuswahlComboBox"), Short);
        auswahlBox.AsComboBox().EditableText = geaenderterSkript;

        var abbrechenButton = WaitForElement(mainWindow, cf => cf.ByName("InitialisierungsskriptAbbrechen"), Short);
        abbrechenButton.AsButton().Click();

        var anzeige = WaitForElement(mainWindow, cf => cf.ByAutomationId("InitialisierungsskriptAnzeige"), Short);
        Assert.Equal(UnbekanntesSkript, anzeige.Name);

        var saved = await WaitForSavedInitialisierungsskriptAsync(RepoFolderName, UnbekanntesSkript);
        Assert.Equal(UnbekanntesSkript, saved);

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
