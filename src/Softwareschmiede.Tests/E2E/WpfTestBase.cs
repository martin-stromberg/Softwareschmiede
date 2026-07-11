using System.Diagnostics;
using System.IO;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.Core.Input;
using FlaUI.UIA3;
using Microsoft.EntityFrameworkCore;
using Softwareschmiede.Infrastructure.Data;
using Softwareschmiede.Infrastructure.Services;

namespace Softwareschmiede.Tests.E2E;

/// <summary>
/// Basisklasse für WPF-E2E-Tests. Startet die Anwendung als separaten Prozess,
/// wartet auf das Hauptfenster und beendet den Prozess nach dem Test.
/// </summary>
public abstract class WpfTestBase : IDisposable
{
    private const string BuildConfigDebug = "Debug";
    private const string BuildConfigRelease = "Release";
    private const string TargetFramework = "net10.0-windows10.0.17763.0";

    /// <summary>Kurzes Timeout (10s) für schnell erscheinende UI-Elemente.</summary>
    protected static readonly TimeSpan Short = TimeSpan.FromSeconds(10);

    /// <summary>Mittleres Timeout (15s) für UI-Elemente nach asynchronen Operationen.</summary>
    protected static readonly TimeSpan Medium = TimeSpan.FromSeconds(15);

    /// <summary>Langes Timeout (30s), z. B. für das initiale Erscheinen des Hauptfensters.</summary>
    protected static readonly TimeSpan Long = TimeSpan.FromSeconds(30);

    private FlaUI.Core.Application? _application;
    private UIA3Automation? _automation;
    private readonly string _testDbPath;
    private string? _appLogDirectory;
    private LogSnapshot _appLogSnapshot;

    /// <summary>
    /// Pfad zur SQLite-Testdatenbank des laufenden App-Prozesses. Ermöglicht Tests, Vorbedingungen
    /// direkt in der Datenbank zu hinterlegen, die über die UI (noch) nicht abbildbar sind
    /// (z. B. ein vorkonfiguriertes Arbeitsverzeichnis, solange kein Plugin die Verzeichnisstruktur-
    /// Vorschau unterstützt).
    /// </summary>
    protected string TestDbPath => _testDbPath;

    /// <summary>Gibt den FlaUI-Automatisierungskontext zurück.</summary>
    /// <exception cref="InvalidOperationException">Wird geworfen, wenn <see cref="LaunchApp"/> noch nicht aufgerufen wurde.</exception>
    protected UIA3Automation Automation => _automation ?? throw new InvalidOperationException("LaunchApp muss vor dem Zugriff auf Automation aufgerufen werden.");

    /// <summary>Gibt den gestarteten FlaUI-Application-Handle zurück.</summary>
    /// <exception cref="InvalidOperationException">Wird geworfen, wenn <see cref="LaunchApp"/> noch nicht aufgerufen wurde.</exception>
    protected FlaUI.Core.Application FlaUiApp => _application ?? throw new InvalidOperationException("LaunchApp muss vor dem Zugriff auf FlaUiApp aufgerufen werden.");

    /// <summary>Initialisiert eine neue Instanz und erzeugt einen temporären DB-Pfad.</summary>
    protected WpfTestBase()
    {
        _testDbPath = Path.Combine(
            Path.GetTempPath(),
            $"softwareschmiede_e2e_{Guid.NewGuid():N}.db");
    }

    /// <summary>
    /// Löscht die temporäre Testdatenbank, falls sie existiert. Sollte nach jedem Test aufgerufen werden, um sicherzustellen, dass keine Testdaten zurückbleiben.
    /// </summary>
    protected void DeleteTestDatabase()
    {
        if (File.Exists(_testDbPath))
            File.Delete(_testDbPath);
    }

    /// <summary>
    /// Startet die Anwendung als Prozess mit einem temporären Datenbankpfad.
    /// Wartet, bis das Hauptfenster sichtbar ist.
    /// </summary>
    /// <param name="ensureDatabaseDeleted">Gibt an, ob die Testdatenbank vor dem Start gelöscht werden soll. Sollte normalerweise auf true gesetzt werden, um sicherzustellen, dass jeder Test mit einer frischen Datenbank beginnt.</param>
    protected FlaUI.Core.Application LaunchApp(bool ensureDatabaseDeleted = true)
    {
        if (ensureDatabaseDeleted)
        {
            DeleteTestDatabase();
        }

        var appPath = ResolveAppExePath();

        // WICHTIG: Die Umgebungsvariable gilt prozessweit für alle parallel laufenden Tests.
        // Deshalb ist [Collection("E2E")] zwingend erforderlich, um parallele Ausführung zu verhindern
        // und sicherzustellen, dass jeder Test seine eigene Datenbankinstanz isoliert benutzt.
        Environment.SetEnvironmentVariable("SOFTWARESCHMIEDE_TEST_DB_PATH", _testDbPath);

        _appLogDirectory = ResolveAppLogDirectory(appPath);
        _appLogSnapshot = AppStartupLogInspector.Snapshot(_appLogDirectory);

        _automation = new UIA3Automation();
        _application = FlaUI.Core.Application.Launch(appPath);
        _application.WaitWhileMainHandleIsMissing(Long);

        if (_application.HasExited || _application.MainWindowHandle == IntPtr.Zero)
        {
            var startupException = CheckAppStartupException();
            if (startupException is not null)
            {
                throw new InvalidOperationException(
                    $"Die Anwendung wurde gestartet, aber das Hauptfenster ist nicht verfügbar. Log-Auszug:{Environment.NewLine}{startupException}");
            }

            throw new InvalidOperationException(
                "Die Anwendung wurde gestartet, aber das Hauptfenster ist nicht verfügbar (Prozess bereits beendet oder hängt). " +
                "Im App-Log wurde keine [ERR]/[FTL]-Zeile gefunden.");
        }

        // Kurz warten, bis WPF-Rendering und EF-Migrationen abgeschlossen sind.
        Thread.Sleep(2000);

        return _application;
    }

    /// <summary>
    /// Öffnet einen <see cref="SoftwareschmiededDbContext"/> gegen die SQLite-Testdatenbank des laufenden
    /// App-Prozesses (<see cref="TestDbPath"/>). Für Testvorbedingungen, die über die UI nicht abbildbar sind.
    /// </summary>
    protected SoftwareschmiededDbContext OpenTestDbContext()
    {
        var options = new DbContextOptionsBuilder<SoftwareschmiededDbContext>()
            .UseSqlite($"Data Source={_testDbPath}")
            .Options;
        return new SoftwareschmiededDbContext(options);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        var processId = _application?.ProcessId;

        try { _application?.Close(); }
        catch (Exception ex) { Debug.WriteLine($"WpfTestBase.Dispose: Fehler beim Schließen der Anwendung: {ex}"); }

        try { _application?.WaitWhileMainHandleIsMissing(TimeSpan.FromSeconds(5)); }
        catch (Exception ex) { Debug.WriteLine($"WpfTestBase.Dispose: Fehler beim Warten auf das Schließen des Hauptfensters: {ex}"); }

        WaitForProcessExit(processId);

        _automation?.Dispose();

        try { DeleteTestDatabase(); }
        catch (Exception ex) { Debug.WriteLine($"WpfTestBase.Dispose: Fehler beim Löschen der Testdatenbank: {ex}"); }

        // Umgebungsvariable zurücksetzen, damit sie nicht in andere Tests im selben Prozess
        // hineinleckt (z. B. PluginManagerTests, die ohne Test-Modus-Einschränkung laufen sollen).
        Environment.SetEnvironmentVariable("SOFTWARESCHMIEDE_TEST_DB_PATH", null);

        // LocalDirectoryPlugin-Credentials aus dem OS-weiten Windows-Credential-Store löschen,
        // damit der WorkspaceMode nicht zwischen E2E-Tests leakt.
        try { DeleteLocalDirectoryPluginCredentials(); }
        catch (Exception ex) { Debug.WriteLine($"WpfTestBase.Dispose: Fehler beim Löschen der Plugin-Credentials: {ex}"); }
    }

    private static void DeleteLocalDirectoryPluginCredentials()
    {
        var store = new WindowsCredentialStore();
        store.DeleteCredential("LocalDirectoryPlugin.WorkspaceMode");
        store.DeleteCredential("LocalDirectoryPlugin.SourceDirectory");
        store.DeleteCredential("LocalDirectoryPlugin.ConfirmGitInitInSourceDirectory");
        store.DeleteCredential("Softwareschmiede.Codex.ExecutablePath");
    }

    /// <summary>
    /// Bestätigt für E2E-Erfolgsszenarien explizit, dass das LocalDirectoryPlugin im Quellverzeichnis git init ausführen darf.
    /// </summary>
    protected static void ConfirmLocalDirectoryGitInitInSourceDirectory()
        => new WindowsCredentialStore().SetCredential("LocalDirectoryPlugin.ConfirmGitInitInSourceDirectory", "true");

    /// <summary>Setzt den Workspace-Modus des LocalDirectoryPlugins für E2E-Tests.</summary>
    protected static void SetLocalDirectoryWorkspaceMode(string workspaceMode)
        => new WindowsCredentialStore().SetCredential("LocalDirectoryPlugin.WorkspaceMode", workspaceMode);

    /// <summary>
    /// Wartet, bis ein Element im Teilbaum von <paramref name="parent"/> gefunden wird.
    /// Wirft <see cref="TimeoutException"/>, wenn das Element nicht innerhalb von <paramref name="timeout"/> erscheint.
    /// </summary>
    /// <remarks>
    /// Als Fail-Fast-Diagnose wird bei jeder Polling-Iteration zusätzlich nach einem Fehlerbanner
    /// ("FehlerMeldung") gesucht: Tests, die auf ein *anderes* Element warten, sollen sofort mit einer
    /// aussagekräftigen Meldung abbrechen, statt erst nach Ablauf des vollen Timeouts. Da die Suche nach
    /// <paramref name="conditionFunc"/> und die Suche nach "FehlerMeldung" zwei separate, nicht-atomare
    /// UI-Automation-Aufrufe sind, kann es vorkommen, dass der erste Aufruf ein gerade erst erscheinendes
    /// Element knapp verpasst, während der zweite (Millisekunden später) es bereits findet. Für Aufrufer,
    /// deren <paramref name="conditionFunc"/> selbst auf "FehlerMeldung" zielt (z. B. Fehlerfall-Tests, die
    /// genau dieses Element als Erfolgskriterium erwarten), würde das fälschlich als Abbruchgrund statt als
    /// gefundenes Zielelement gewertet. Deshalb wird die Zielsuche unmittelbar vor dem Werfen der Exception
    /// erneut versucht: Ist "FehlerMeldung" tatsächlich das gesuchte Zielelement, ist es zu diesem Zeitpunkt
    /// im Automation-Baum bereits vorhanden (der Fehlerbanner-Check hat es ja soeben gefunden) und die
    /// erneute Zielsuche liefert es als regulären Treffer zurück. Zielt <paramref name="conditionFunc"/> auf
    /// ein anderes Element, bleibt die erneute Suche erfolglos und die Fail-Fast-Diagnose greift wie bisher.
    /// </remarks>
    protected static AutomationElement WaitForElement(
        AutomationElement parent,
        Func<ConditionFactory, ConditionBase> conditionFunc,
        TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var element = parent.FindFirstDescendant(conditionFunc);
            if (element is not null)
                return element;

            var fehlerMeldung = parent.FindFirstDescendant(cf => cf.ByName("FehlerMeldung"));
            if (fehlerMeldung is not null)
            {
                // Letzter Versuch: Falls conditionFunc selbst auf "FehlerMeldung" zielt, ist das
                // Element inzwischen sicher auffindbar (siehe Erklärung oben) und der Aufruf soll
                // regulär mit diesem Treffer zurückkehren statt in den Fehlerpfad zu laufen.
                element = parent.FindFirstDescendant(conditionFunc);
                if (element is not null)
                    return element;

                throw new InvalidOperationException(
                    $"In der Anwendung wird eine Fehlermeldung angezeigt: {GetFehlerText(fehlerMeldung)}");
            }

            Thread.Sleep(200);
        }
        throw new TimeoutException(
            $"Element wurde nicht innerhalb von {timeout.TotalSeconds}s gefunden.");
    }

    private static string GetFehlerText(AutomationElement fehlerMeldung)
    {
        string? helpText = null;
        try
        {
            helpText = fehlerMeldung.HelpText;
        }
        catch (FlaUI.Core.Exceptions.PropertyNotSupportedException)
        {
        }

        if (!string.IsNullOrWhiteSpace(helpText))
            return helpText;

        return fehlerMeldung.Name;
    }

    /// <summary>
    /// Wartet, bis ein Top-Level-Fenster mit dem angegebenen Titel auf dem Desktop erscheint.
    /// </summary>
    protected AutomationElement WaitForWindow(string title, TimeSpan timeout)
        => WaitForElement(Automation.GetDesktop(), cf => cf.ByName(title), timeout);

    /// <summary>Navigiert zur Projektliste.</summary>
    protected void NavigateToProjecten(AutomationElement mainWindow)
    {
        var button = WaitForElement(mainWindow, cf => cf.ByName(" Projekte"), Short);
        button.AsButton().Click();
    }

    /// <summary>
    /// Wartet, bis ein Element im Teilbaum von <paramref name="parent"/> verschwunden ist.
    /// Assertiert anschließend, dass das Element nicht mehr vorhanden ist.
    /// </summary>
    protected static void WaitUntilGone(
        AutomationElement parent,
        Func<ConditionFactory, ConditionBase> conditionFunc,
        TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        AutomationElement? element = null;
        while (DateTime.UtcNow < deadline)
        {
            element = parent.FindFirstDescendant(conditionFunc);
            if (element is null)
                break;
            Thread.Sleep(200);
        }
        Assert.Null(element);
    }

    /// <summary>Legt ein neues Projekt an und speichert es. Nach dem Speichern navigiert das ViewModel automatisch zurück.</summary>
    protected void CreateProject(AutomationElement mainWindow, string name)
    {
        var neuButton = WaitForElement(mainWindow, cf => cf.ByName("Neu"), Short);
        neuButton.AsButton().Click();

        var nameBox = WaitForElement(mainWindow, cf => cf.ByName("ProjektName"), Short);
        nameBox.Click();
        Keyboard.Type(name);

        var speichernButton = WaitForElement(mainWindow, cf => cf.ByName("Speichern"), Short);
        speichernButton.AsButton().Click();

        // Warten bis Overlay geschlossen (Speichern-Button verschwunden)
        WaitUntilGone(mainWindow, cf => cf.ByName("Speichern"), Medium);

        // CreateAsync + Callback-Ausführung abwarten
        WaitForElement(mainWindow, cf => cf.ByName(name), Medium);
    }

    /// <summary>Öffnet ein Projekt aus der Liste anhand seines Namens.</summary>
    protected void OpenProject(AutomationElement mainWindow, string name)
    {
        var projektKachel = WaitForElement(mainWindow, cf => cf.ByName(name), Short);
        projektKachel.Click();
        WaitForElement(mainWindow, cf => cf.ByName("Speichern"), Medium);
    }

    /// <summary>Legt ein neues Projekt an, speichert es und öffnet es wieder.</summary>
    protected void CreateAndOpenProject(AutomationElement mainWindow, string name)
    {
        CreateProject(mainWindow, name);
        OpenProject(mainWindow, name);
    }

    /// <summary>
    /// Startet die Anwendung, wartet auf das Hauptfenster und navigiert zur Projektliste.
    /// Ist <paramref name="projektName"/> angegeben, wird zusätzlich ein Projekt mit diesem Namen
    /// angelegt und geöffnet.
    /// </summary>
    protected AutomationElement StartAndNavigateToProjects(string? projektName = null)
    {
        var app = LaunchApp();
        var mainWindow = app.GetMainWindow(Automation, Long)!;
        NavigateToProjecten(mainWindow);

        if (projektName is not null)
            CreateAndOpenProject(mainWindow, projektName);

        return mainWindow;
    }

    /// <summary>
    /// Wählt einen Eintrag in einer ComboBox per Klick auf das ComboBoxItem aus (robuster als
    /// FlaUI's <c>Select(string)</c>, das bei manchen TwoWay-Bindings das Binding nicht zuverlässig aktualisiert).
    /// </summary>
    protected static void SelectComboBoxItemByClick(AutomationElement comboBoxElement, string itemText, TimeSpan timeout)
    {
        var comboBox = comboBoxElement.AsComboBox();
        comboBox.Click();
        Thread.Sleep(300);

        var item = WaitForElement(comboBoxElement, cf => cf.ByName(itemText), timeout);
        item.Click();
        Thread.Sleep(200);
    }

    /// <summary>Wartet, bis eine ComboBox den erwarteten selektierten Eintrag anzeigt.</summary>
    protected static void WaitForSelectedComboBoxItem(AutomationElement comboBoxElement, string expectedItemText, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        string? selectedItemName = null;
        while (DateTime.UtcNow < deadline)
        {
            selectedItemName = comboBoxElement.AsComboBox().SelectedItem?.Name;
            if (string.Equals(selectedItemName, expectedItemText, StringComparison.Ordinal))
                return;

            Thread.Sleep(200);
        }

        throw new TimeoutException(
            $"ComboBox zeigte nicht innerhalb von {timeout.TotalSeconds}s den erwarteten Eintrag '{expectedItemText}'. Aktuell: '{selectedItemName}'.");
    }

    /// <summary>
    /// Erstellt ein temporäres lokales Quellverzeichnis mit einem Unterordner (simuliertes Repository)
    /// für Tests des LocalDirectoryPlugin. Gibt den Pfad des Quellverzeichnisses zurück.
    /// </summary>
    protected string CreateLocalSourceDirectory(string repositoryFolderName)
    {
        var sourceDirectory = Path.Combine(
            Path.GetTempPath(),
            $"softwareschmiede_e2e_source_{Guid.NewGuid():N}");
        var repositoryPath = Path.Combine(sourceDirectory, repositoryFolderName);
        Directory.CreateDirectory(repositoryPath);
        File.WriteAllText(Path.Combine(repositoryPath, "readme.txt"), "E2E-Testdatei");
        return sourceDirectory;
    }

    /// <summary>
    /// Öffnet die Einstellungsseite, wählt das LocalDirectoryPlugin als SCM-Plugin, setzt WorkspaceMode
    /// auf InSourceDirectory (Standard) oder SeparateWorkingDirectory, trägt das Quellverzeichnis ein
    /// und speichert. Navigiert anschließend zurück zum Dashboard.
    /// SeparateWorkingDirectory wird benötigt, wenn mehrere Aufgaben dasselbe Quellverzeichnis nutzen,
    /// da InSourceDirectory nach dem ersten Lauf uncommitted changes im Quellverzeichnis hinterlassen kann.
    /// </summary>
    protected void ConfigureLocalDirectoryPlugin(AutomationElement mainWindow, string sourceDirectory, bool useInSourceDirectoryMode = true)
    {
        var einstellungenButton = WaitForElement(mainWindow, cf => cf.ByName(" Einstellungen"), Short);
        einstellungenButton.AsButton().Click();

        WaitForElement(mainWindow, cf => cf.ByName("Speichern"), Short);

        var quellcodeTab = WaitForElement(mainWindow, cf => cf.ByName("Quellcodeverwaltung"), Short);
        quellcodeTab.Click();

        var workspaceModeBox = WaitForElement(mainWindow, cf => cf.ByName("WorkspaceMode"), Short);
        var workspaceMode = useInSourceDirectoryMode ? "InSourceDirectory" : "SeparateWorkingDirectory";
        SelectComboBoxItemByClick(workspaceModeBox, workspaceMode, Short);

        var sourceDirectoryBox = WaitForElement(mainWindow, cf => cf.ByName("SourceDirectory"), Short);
        sourceDirectoryBox.AsTextBox().Text = sourceDirectory;

        var speichernButton = WaitForElement(mainWindow, cf => cf.ByName("Speichern"), Short);
        speichernButton.AsButton().Click();

        WaitForElement(mainWindow, cf => cf.ByName("Einstellungen gespeichert."), Short);

        var dashboardButton = WaitForElement(mainWindow, cf => cf.ByName("Dashboard"), Short);
        dashboardButton.AsButton().Click();
    }

    /// <summary>
    /// Öffnet den Repository-Zuweisungs-Dialog, wählt das erste Repository aus der Liste
    /// (LocalDirectoryPlugin ist im Test-Modus das einzige verfügbare SCM-Plugin) und bestätigt die Zuweisung.
    /// </summary>
    protected void AssignLocalDirectoryRepository(AutomationElement mainWindow)
    {
        var zuweisenButton = WaitForElement(mainWindow, cf => cf.ByName("Zuweisen"), Short);
        zuweisenButton.AsButton().Click();

        var dialog = WaitForWindow("Repository zuweisen", Short);

        AutomationElement[] items = [];
        var deadline = DateTime.UtcNow + Short;
        while (DateTime.UtcNow < deadline)
        {
            var listBox = dialog.FindFirstDescendant(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.List));
            if (listBox is not null)
            {
                items = listBox.FindAllChildren(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.ListItem));
                if (items.Length > 0)
                    break;
            }
            Thread.Sleep(200);
        }

        if (items.Length == 0)
            throw new TimeoutException("Repository-Liste im Zuweisungsdialog enthielt kein Element innerhalb des Timeouts.");

        items[0].Click();

        var zuweisenBestaetigenButton = WaitForElement(dialog, cf => cf.ByName("Zuweisen"), Short);
        zuweisenBestaetigenButton.AsButton().Click();
    }

    /// <summary>
    /// Startet die Anwendung, konfiguriert das LocalDirectoryPlugin mit einem neu erstellten lokalen
    /// Quellverzeichnis, legt ein Projekt an, öffnet es, weist das Repository zu und erstellt eine
    /// neue Aufgabe. Gibt das Hauptfenster zurück, in dem die Aufgabe im Edit-Panel (Status "Neu")
    /// bereit zum Starten ist.
    /// </summary>
    protected AutomationElement SetupProjectMitNeuerAufgabe(
        string repositoryFolderName,
        string projektName,
        bool useInSourceDirectoryMode = true)
    {
        var sourceDirectory = CreateLocalSourceDirectory(repositoryFolderName);

        var app = LaunchApp();
        var mainWindow = app.GetMainWindow(Automation, Long)!;

        ConfigureLocalDirectoryPlugin(mainWindow, sourceDirectory, useInSourceDirectoryMode);

        NavigateToProjecten(mainWindow);
        CreateAndOpenProject(mainWindow, projektName);

        AssignLocalDirectoryRepository(mainWindow);

        var aufgabeNeuButton = WaitForElement(mainWindow, cf => cf.ByName("AufgabeNeu"), Short);
        aufgabeNeuButton.AsButton().Click();

        WaitForElement(mainWindow, cf => cf.ByName("EditTitel"), Short);

        return mainWindow;
    }

    /// <summary>
    /// Überspringt den aufrufenden Test (via <c>Assert.Skip</c>), wenn ConPTY-Kindprozesse in dieser
    /// Ausführungsumgebung nicht isoliert an die Pseudo-Konsole angebunden werden (siehe
    /// <see cref="ConPtyEnvironmentProbe"/>). Betrifft alle Tests, die über einen CLI-Prozess
    /// (ConPTY, z. B. via <see cref="StartenUndPluginWaehlen"/>) sichtbare Terminal-Ausgabe erwarten -
    /// ohne funktionierende Konsolen-Isolation scheitern sie sonst mit einem nichtssagenden
    /// <see cref="TimeoutException"/> statt mit einer erklärenden Meldung. Muss als erste Zeile des
    /// jeweiligen Testkörpers aufgerufen werden, vor jedem App-Start.
    /// Erfordert, dass die aufrufende Testmethode mit <c>[SkippableFact]</c> (statt <c>[Fact]</c>)
    /// annotiert ist (Paket <c>Xunit.SkippableFact</c>) - nur damit erzeugt <c>Skip.If</c> ein
    /// echtes "Skipped"-Ergebnis statt eines fehlschlagenden Tests.
    /// </summary>
    protected static void SkipWennConPtyNichtVerfuegbar()
    {
        Skip.If(
            !ConPtyEnvironmentProbe.IsAvailable,
            "ConPTY-Konsolen-Isolation konnte in dieser Ausführungsumgebung nicht bestätigt werden " +
            $"(Probe-Ergebnis: {ConPtyEnvironmentProbe.UnavailableReason}). Falls dieser Test auch " +
            "in einer Umgebung übersprungen wird, in der ConPTY nachweislich funktioniert (z. B. " +
            "Visual Studio), ist das ein Bug in ConPtyEnvironmentProbe, keine echte " +
            "Umgebungslimitation - bitte den Probe-Grund oben melden.");
    }

    /// <summary>
    /// Klickt den "Starten"-Button und bedient den anschließend erscheinenden Plugin-Auswahl-Dialog:
    /// wählt das angegebene KI-Plugin aus und bestätigt mit "OK".
    /// </summary>
    protected void StartenUndPluginWaehlen(AutomationElement mainWindow, string pluginName)
    {
        var startenButton = WaitForElement(mainWindow, cf => cf.ByName("Starten"), Short);
        startenButton.AsButton().Click();

        var dialog = WaitForWindow("KI-Plugin auswählen", Medium);
        var pluginAuswahlBox = WaitForElement(dialog, cf => cf.ByName("PluginAuswahl"), Short);
        SelectComboBoxItemByClick(pluginAuswahlBox, pluginName, Short);

        var okButton = WaitForElement(dialog, cf => cf.ByName("OK"), Short);
        okButton.AsButton().Click();
    }

    private static string ResolveAppExePath()
    {
        var baseDir = AppContext.BaseDirectory;

        // Bei Tests läuft das Test-Binary in src\Softwareschmiede.Tests\bin\Debug\net10.0-windows10.0.17763.0\;
        // das App-Binary liegt im selben src-Verzeichnis unter Softwareschmiede.App\bin\<Config>\net10.0-windows10.0.17763.0\
        var candidates = new[]
        {
            Path.GetFullPath(Path.Combine(
                baseDir,
                "..", "..", "..", "..",
                "Softwareschmiede.App", "bin", BuildConfigDebug,
                TargetFramework,
                "Softwareschmiede.App.exe")),
            Path.GetFullPath(Path.Combine(
                baseDir,
                "..", "..", "..", "..",
                "Softwareschmiede.App", "bin", BuildConfigRelease,
                TargetFramework,
                "Softwareschmiede.App.exe")),
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
                return candidate;
        }

        throw new FileNotFoundException(
            $"Softwareschmiede.App.exe wurde nicht gefunden. Bitte zuerst das App-Projekt bauen. " +
            $"Gesuchte Pfade:{Environment.NewLine}{string.Join(Environment.NewLine, candidates)}");
    }

    private static string ResolveAppLogDirectory(string appExePath)
        => Path.Combine(Path.GetDirectoryName(appExePath) ?? string.Empty, "logs");

    /// <summary>
    /// Gibt den seit dem in <see cref="LaunchApp"/> gemerkten Offset angehängten Inhalt der neuesten
    /// App-Log-Datei zurück.
    /// </summary>
    /// <returns>Neu angehängter Log-Inhalt, oder ein leerer String, falls kein Log-Verzeichnis bekannt ist oder kein neuer Inhalt vorliegt.</returns>
    protected string GetLatestAppLogContent()
        => _appLogDirectory is null ? string.Empty : AppStartupLogInspector.GetNewEntries(_appLogDirectory, _appLogSnapshot);

    /// <summary>Prüft die seit dem Start neu angehängten Log-Zeilen auf eine Startup-Fehlersignatur.</summary>
    /// <returns>Diagnosetext der Fehlerzeilen, oder <c>null</c>, falls keine Startup-Exception erkannt wurde.</returns>
    protected string? CheckAppStartupException()
        => AppStartupLogInspector.CheckAppStartupException(GetLatestAppLogContent());

    private static void WaitForProcessExit(int? processId)
    {
        if (processId is null)
            return;

        try
        {
            var process = Process.GetProcessById(processId.Value);
            if (!process.WaitForExit((int)TimeSpan.FromSeconds(10).TotalMilliseconds))
            {
                Debug.WriteLine($"WpfTestBase.Dispose: Prozess {processId} ist nach Ablauf des Timeouts weiterhin aktiv.");
            }
        }
        catch (ArgumentException)
        {
            // Prozess ist bereits beendet.
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"WpfTestBase.Dispose: Fehler beim Warten auf den vollständigen Prozess-Exit: {ex}");
        }
    }
}
