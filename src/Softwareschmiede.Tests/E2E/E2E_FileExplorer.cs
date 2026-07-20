using FlaUI.Core.AutomationElements;
using FluentAssertions;

namespace Softwareschmiede.Tests.E2E;

/// <summary>
/// E2E-Tests für das neue Dateiexplorer-Register in der Aufgabendetailansicht.
///
/// Voraussetzungen:
/// - Windows-Desktop-Session (kein Headless-CI)
/// - Softwareschmiede.App muss im Debug-Modus gebaut sein
/// - Im Test-Modus (SOFTWARESCHMIEDE_TEST_DB_PATH gesetzt) steht ausschließlich das LocalDirectoryPlugin
///   als SCM-Plugin zur Verfügung (kein GitHub-Plugin), siehe PluginManager.IsAllowedInTestMode.
///
/// Konsolidierung (Issue #153): Beide ursprünglichen Szenarien teilen exakt dasselbe Setup (Repository
/// klonen, Aufgabe starten, Dateiexplorer öffnen) und laufen deshalb als Phasen in einem App-Lifecycle.
///
/// CI-Regular-Lauf: dotnet test --filter "Category!=OsInterface"
/// </summary>
[Trait("Category", "E2E")]
[OsInterface]
[Collection("E2E")]
public sealed class E2E_FileExplorer : WpfTestBase
{
    /// <summary>
    /// Szenario: Repository klonen (Aufgabe starten), dann auf das "Dateien"-Register wechseln.
    /// Prüft: Split-View mit Baum und Standard/Vergleich/Aktualisieren-Umschaltung ist erreichbar.
    /// Anschließend: Wechsel zum Info-Register, dann zum CLI-Register.
    /// Prüft: Der Dateiexplorer wird ausgeblendet und das Info- sowie das CLI-Register bleiben
    /// erreichbar. Regressionstest für ein defektes Visibility-Binding (RelativeSource
    /// AncestorType=UserControl auf eine Eigenschaft, die nur im DataContext existiert), das das
    /// FileExplorerView zuvor dauerhaft sichtbar hielt und damit die anderen Register überdeckte.
    ///
    /// Prüft zusätzlich: Die Ribbon-Gruppen "CLI" und "Dateien" folgen der tatsächlich ausgewählten
    /// Ansicht (IsCliViewSelected/IsFileExplorerViewSelected), nicht nur der grundsätzlichen
    /// Verfügbarkeit des Panels (ShowCliPanel/ShowFileExplorerPanel). Während der ganzen Testdauer
    /// bleiben sowohl ShowCliPanel (Status=Gestartet) als auch ShowFileExplorerPanel (Arbeitsverzeichnis
    /// existiert) durchgehend true - ein an diese Properties gebundenes Ribbon würde die jeweilige
    /// Gruppe daher fälschlich dauerhaft sichtbar lassen, unabhängig von der ausgewählten Ansicht.
    /// Regressionstest für Issue #157 Nacharbeit: die Ribbon-Gruppen waren an ShowCliPanel/
    /// ShowFileExplorerPanel statt an IsCliViewSelected/IsFileExplorerViewSelected gebunden.
    /// </summary>
    [SkippableFact]
    public void DateiExplorer_ZeigtBaumUndModeButtons_UndWechseltZuInfoUndZurueck_E2E()
    {
        var mainWindow = SetupProjectMitNeuerAufgabe("FileExplorer-Repo", "FileExplorer-Projekt");

        // git init im Quellverzeichnis vorab bestätigen, damit "Starten" im ersten Versuch gelingt.
        ConfirmLocalDirectoryGitInitInSourceDirectory();
        StartenUndPluginWaehlen(mainWindow, "Softwareschmiede.KiSimulator");

        // Nach erfolgreichem Start ist das Repository geklont (LokalerKlonPfad gesetzt) und
        // das CLI-Panel sichtbar - das bestätigt, dass der kombinierte Klon-/Start-Ablauf durchlief.
        // Ab hier bleiben ShowCliPanel (Status=Gestartet) und ShowFileExplorerPanel (Klonpfad
        // existiert) für den Rest des Tests durchgehend true.
        WaitForElement(mainWindow, cf => cf.ByName("CliStoppen"), Medium);

        // In der initial ausgewählten CLI-Ansicht ist die Ribbon-Gruppe "CLI" sichtbar, die Gruppe
        // "Dateien" dagegen nicht - obwohl das Arbeitsverzeichnis (ShowFileExplorerPanel) bereits existiert.
        WaitForElement(mainWindow, cf => cf.ByName("PluginAendern"), Short);
        WaitUntilGone(mainWindow, cf => cf.ByName("DateiStandard"), Short);

        var dateiViewButton = WaitForElement(mainWindow, cf => cf.ByName("DateiViewButton"), Short);
        dateiViewButton.AsButton().Click();

        WaitForElement(mainWindow, cf => cf.ByName("FileExplorerBaum"), Short);
        WaitForElement(mainWindow, cf => cf.ByName("DateiStandard"), Short);
        WaitForElement(mainWindow, cf => cf.ByName("DateiVergleich"), Short);
        WaitForElement(mainWindow, cf => cf.ByName("DateiAktualisieren"), Short);
        WaitForElement(mainWindow, cf => cf.ByName("DateiOeffnen"), Short);

        // Sobald zur Dateien-Ansicht gewechselt wurde, muss die Ribbon-Gruppe "CLI" verschwinden -
        // obwohl ShowCliPanel (Status=Gestartet) weiterhin true ist.
        WaitUntilGone(mainWindow, cf => cf.ByName("PluginAendern"), Short);

        var infoButton = WaitForElement(mainWindow, cf => cf.ByName("InfoCliToggle"), Short);
        infoButton.AsButton().Click();

        // Dateiexplorer-Baum muss verschwinden - vorher blieb er wegen des defekten Bindings dauerhaft
        // sichtbar und überdeckte das Info-Register.
        WaitUntilGone(mainWindow, cf => cf.ByName("FileExplorerBaum"), Short);

        // In der Info-Ansicht ist weder die CLI- noch die Dateien-Ansicht ausgewählt - beide
        // Ribbon-Gruppen müssen daher verschwinden, obwohl ShowCliPanel und ShowFileExplorerPanel
        // beide weiterhin true sind.
        WaitUntilGone(mainWindow, cf => cf.ByName("PluginAendern"), Short);
        WaitUntilGone(mainWindow, cf => cf.ByName("DateiStandard"), Short);

        var cliViewButton = WaitForElement(mainWindow, cf => cf.ByName("CliViewButton"), Short);
        cliViewButton.AsButton().Click();

        WaitForElement(mainWindow, cf => cf.ByName("TerminalConsole"), Short);

        // Zurück in der CLI-Ansicht: Ribbon-Gruppe "CLI" erscheint wieder, "Dateien" bleibt verborgen.
        WaitForElement(mainWindow, cf => cf.ByName("PluginAendern"), Short);
        WaitUntilGone(mainWindow, cf => cf.ByName("DateiStandard"), Short);
    }

    /// <summary>
    /// Szenario (Issue #156, Lazy-Loading): Im bereits geklonten Arbeitsverzeichnis wird eine dreistufige
    /// Verzeichnisstruktur ("Ebene1/Ebene2/Deep.cs") angelegt und die Aufgabendetailansicht neu geladen
    /// (FileExplorerViewModel.InitialisierenAsync lädt dabei den Arbeitsbaum frisch von der Festplatte).
    /// Mit maxInitialDepth = 2 ist die tiefste Ebene ("Deep.cs") initial nicht geladen. Erst das Aufklappen
    /// des Verzeichnisknotens auf der Grenztiefe ("Ebene2") löst LadeKinderAsync/LoadSubtreeAsync aus und
    /// lässt "Deep.cs" im Baum erscheinen.
    /// </summary>
    [SkippableFact]
    public async Task DateiExplorer_KlapptVerzeichnisAufUndLaedtKinderNach_E2E()
    {
        var mainWindow = SetupProjectMitNeuerAufgabe("FileExplorer-LazyLoad-Repo", "FileExplorer-LazyLoad-Projekt");

        ConfirmLocalDirectoryGitInitInSourceDirectory();
        StartenUndPluginWaehlen(mainWindow, "Softwareschmiede.KiSimulator");

        WaitForElement(mainWindow, cf => cf.ByName("CliStoppen"), Medium);

        var lokalerKlonPfad = await GetLokalerKlonPfadAsync();
        Directory.CreateDirectory(Path.Combine(lokalerKlonPfad, "Ebene1", "Ebene2"));
        File.WriteAllText(Path.Combine(lokalerKlonPfad, "Ebene1", "Ebene2", "Deep.cs"), "tief verschachtelter Inhalt");

        // Der Arbeitsbaum wird beim ersten Anzeigen der Aufgabendetailansicht einmalig geladen - ein
        // erneutes Öffnen erzwingt einen frischen InitialisierenAsync-Aufruf, der die soeben angelegte
        // Struktur von der Festplatte erfasst.
        ReloadTaskDetail(mainWindow);

        var dateiViewButton = WaitForElement(mainWindow, cf => cf.ByName("DateiViewButton"), Short);
        dateiViewButton.AsButton().Click();

        WaitForElement(mainWindow, cf => cf.ByName("FileExplorerBaum"), Short);

        // "Ebene1" (Depth 0) ist oberhalb der Grenztiefe bereits vollständig geladen (ChildrenLoaded = true) -
        // Aufklappen macht nur "Ebene2" sichtbar, ohne Lazy-Load auszulösen.
        var ebene1 = WaitForElement(mainWindow, cf => cf.ByName("Ebene1"), Short);
        ebene1.Patterns.ExpandCollapse.Pattern.Expand();

        // "Ebene2" (Depth 1) liegt auf der Grenztiefe (ChildrenLoaded = false, Platzhalter-Kind). Erst das
        // Aufklappen löst OnBaumKnotenExpanded -> LadeKinderAsync -> LoadSubtreeAsync aus.
        var ebene2 = WaitForElement(mainWindow, cf => cf.ByName("Ebene2"), Short);
        WaitUntilGone(mainWindow, cf => cf.ByName("Deep.cs"), Short);
        ebene2.Patterns.ExpandCollapse.Pattern.Expand();

        WaitForElement(mainWindow, cf => cf.ByName("Deep.cs"), Short);
    }

    /// <summary>
    /// Szenario (Issue #156, Lazy-Loading): Nach dem Aufklappen bis in die Tiefe wird der äußere
    /// Verzeichnisknoten ("Ebene1") wieder zugeklappt. BeraeumeKnoten muss dabei dessen direktes Kind
    /// ("Ebene2", ein geladenes Verzeichnis) auf ChildrenLoaded = false zurücksetzen und den Platzhalter
    /// wiederherstellen, wodurch der Groß-Enkel-Knoten ("Deep.cs") entfernt wird. Ein erneutes Aufklappen von
    /// "Ebene1" und danach "Ebene2" muss dadurch tatsächlich einen frischen LoadSubtreeAsync-Aufruf auslösen,
    /// statt den zwischengespeicherten alten Knoten wiederzuverwenden.
    /// Nachweis: Während zugeklappt ist, wird "Deep.cs" durch "Deep2.cs" ersetzt. Erscheint nach dem erneuten
    /// Aufklappen "Deep2.cs" (statt weiterhin nur der veraltete Stand "Deep.cs"), bestätigt das einen echten
    /// Neu-Ladevorgang und damit, dass die Bereinigung ChildrenLoaded zurückgesetzt hat.
    /// </summary>
    [SkippableFact]
    public async Task DateiExplorer_KlapptVerzeichnisZuUndErneutAuf_LaedtKinderNach_E2E()
    {
        var mainWindow = SetupProjectMitNeuerAufgabe("FileExplorer-LazyLoad-Collapse-Repo", "FileExplorer-LazyLoad-Collapse-Projekt");

        ConfirmLocalDirectoryGitInitInSourceDirectory();
        StartenUndPluginWaehlen(mainWindow, "Softwareschmiede.KiSimulator");

        WaitForElement(mainWindow, cf => cf.ByName("CliStoppen"), Medium);

        var lokalerKlonPfad = await GetLokalerKlonPfadAsync();
        var ebene2Verzeichnis = Path.Combine(lokalerKlonPfad, "Ebene1", "Ebene2");
        Directory.CreateDirectory(ebene2Verzeichnis);
        File.WriteAllText(Path.Combine(ebene2Verzeichnis, "Deep.cs"), "tief verschachtelter Inhalt");

        ReloadTaskDetail(mainWindow);

        var dateiViewButton = WaitForElement(mainWindow, cf => cf.ByName("DateiViewButton"), Short);
        dateiViewButton.AsButton().Click();

        WaitForElement(mainWindow, cf => cf.ByName("FileExplorerBaum"), Short);

        var ebene1 = WaitForElement(mainWindow, cf => cf.ByName("Ebene1"), Short);
        ebene1.Patterns.ExpandCollapse.Pattern.Expand();

        var ebene2 = WaitForElement(mainWindow, cf => cf.ByName("Ebene2"), Short);
        ebene2.Patterns.ExpandCollapse.Pattern.Expand();
        WaitForElement(mainWindow, cf => cf.ByName("Deep.cs"), Short);

        // "Ebene2" selbst zuklappen (IsExpanded true -> false): notwendig, damit ein späteres erneutes
        // Aufklappen von "Ebene2" überhaupt einen TreeViewItem.Expanded-Übergang (und damit
        // LadeKinderAsync) auslöst - WPF feuert Expanded nur bei einem echten Zustandswechsel, nicht wenn
        // ein bereits aufgeklappter Knoten durch Zuklappen eines Vorfahren nur visuell verborgen wird.
        ebene2.Patterns.ExpandCollapse.Pattern.Collapse();

        // "Ebene1" zuklappen: BeraeumeKnoten wird für den zugeklappten Knoten aufgerufen und setzt dessen
        // DIREKTE Kinder zurück, die selbst geladene Verzeichnisse sind - hier "Ebene2". Dadurch wird
        // "Deep.cs" (Groß-Enkel von "Ebene1") entfernt und der Platzhalter unter "Ebene2" wiederhergestellt.
        ebene1.Patterns.ExpandCollapse.Pattern.Collapse();

        // Während zugeklappt ist, wird der Verzeichnisinhalt auf der Festplatte geändert - ein erneutes
        // Aufklappen kann den neuen Stand nur zeigen, wenn "Ebene2" tatsächlich neu geladen wird.
        File.Delete(Path.Combine(ebene2Verzeichnis, "Deep.cs"));
        File.WriteAllText(Path.Combine(ebene2Verzeichnis, "Deep2.cs"), "neuer Inhalt nach Bereinigung");

        ebene1.Patterns.ExpandCollapse.Pattern.Expand();
        var ebene2ErneutAufgeklappt = WaitForElement(mainWindow, cf => cf.ByName("Ebene2"), Short);
        ebene2ErneutAufgeklappt.Patterns.ExpandCollapse.Pattern.Expand();

        WaitForElement(mainWindow, cf => cf.ByName("Deep2.cs"), Short);
        var veralteterKnoten = mainWindow.FindFirstDescendant(cf => cf.ByName("Deep.cs"));
        veralteterKnoten.Should().BeNull("die Zuklapp-Bereinigung muss den zwischengespeicherten Stand verwerfen, statt ihn beim erneuten Aufklappen weiterzuverwenden");
    }

    private async Task<string> GetLokalerKlonPfadAsync()
    {
        await using var db = OpenTestDbContext();
        var aufgabe = db.Aufgaben.Single();
        return aufgabe.LokalerKlonPfad
            ?? throw new InvalidOperationException("LokalerKlonPfad wurde nach dem Starten der Aufgabe nicht gesetzt.");
    }

    /// <summary>
    /// Verlässt die Aufgabendetailansicht und öffnet dieselbe (einzige) Aufgabe erneut, um einen frischen
    /// FileExplorerViewModel.InitialisierenAsync-Aufruf zu erzwingen, der extern (im Testprozess) auf der
    /// Festplatte vorgenommene Änderungen erfasst. Analog zum etablierten Muster in
    /// E2E_VerzeichnisAktionen.ReloadTaskDetail.
    /// </summary>
    /// <param name="mainWindow">Das Hauptfenster mit geöffneter Aufgabendetailansicht.</param>
    private void ReloadTaskDetail(AutomationElement mainWindow)
    {
        AufgabeDetailZurueck(mainWindow);
        var items = OffeneAufgabenItems(mainWindow);
        ErsteOffeneAufgabeOeffnen(items);
        WaitForElement(mainWindow, cf => cf.ByName("Zurück"), Short);
    }
}
