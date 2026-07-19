using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.Core.Input;
using FlaUI.UIA3;
using Microsoft.EntityFrameworkCore;
using Softwareschmiede.App.Views;
using Softwareschmiede.Infrastructure.Data;
using Softwareschmiede.Infrastructure.Services;
using Softwareschmiede.Tests.Infrastructure.Services;
using System.Diagnostics;
using System.IO;

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

    /// <summary>
    /// Kurzes Timeout (20s) für schnell erscheinende UI-Elemente. War ursprünglich 10s; auf
    /// windows-latest-CI-Runnern (siehe .github/workflows/test.yml) zeigte sich ein einmaliger
    /// JIT-/Rendering-Warmup-Effekt bei den ersten Popup-/Dialog-Interaktionen eines Testlaufs
    /// (ComboBox-Dropdown, MessageBox) - belegt dadurch, dass spätere Tests mit identischen
    /// UI-Mustern im selben Lauf durchgehend in 6-10s durchliefen, während die ersten 2-3 solcher
    /// Interaktionen knapp über 10s lagen. 20s deckt diesen einmaligen Warmup-Puffer ab, ohne echte
    /// künftige Regressionen (die deutlich länger bräuchten) zu maskieren.
    /// </summary>
    protected static readonly TimeSpan Short = TimeSpan.FromSeconds(20);

    /// <summary>Mittleres Timeout (15s) für UI-Elemente nach asynchronen Operationen.</summary>
    protected static readonly TimeSpan Medium = TimeSpan.FromSeconds(15);

    /// <summary>Langes Timeout (30s), z. B. für das initiale Erscheinen des Hauptfensters.</summary>
    protected static readonly TimeSpan Long = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Credential-Schlüssel, die von E2E-Tests direkt oder indirekt (über die UI) im OS-weiten
    /// Windows Credential Store gesetzt werden. Wird ein neuer Schlüssel von einem E2E-Test
    /// verwendet, muss er hier ergänzt werden, damit er vom Backup/Restore erfasst wird.
    /// </summary>
    private static readonly string[] ManagedCredentialKeys =
    [
        "Softwareschmiede.Codex.CommandLineParameters",
        "LocalDirectoryPlugin.ConfirmGitInitInSourceDirectory",
        "LocalDirectoryPlugin.WorkspaceMode",
        "LocalDirectoryPlugin.SourceDirectory",
        "Softwareschmiede.Codex.ExecutablePath",
    ];

    private FlaUI.Core.Application? _application;
    private UIA3Automation? _automation;
    private readonly string _testDbPath;
    private readonly CredentialStoreSnapshot _credentialSnapshot;
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
        _credentialSnapshot = new CredentialStoreSnapshot(new WindowsCredentialStore(), ManagedCredentialKeys);
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
    /// Startet die Anwendung und gibt das Hauptfenster zurück. Wartet, bis das Hauptfenster sichtbar ist.
    /// </summary>
    /// <returns>Das Hauptfenster der gestarteten Anwendung.</returns>
    protected Window LaunchAppAndGetMainWindow()
    {
        var app = LaunchApp();
        return app.GetMainWindow(Automation, Long)!;
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

        // Credential-Store-Zustand aus dem OS-weiten Windows-Credential-Store wiederherstellen,
        // damit produktive Werte durch den Testlauf nicht verloren gehen oder zwischen E2E-Tests leaken.
        try { _credentialSnapshot.Restore(); }
        catch (Exception ex) { Debug.WriteLine($"WpfTestBase.Dispose: Fehler beim Wiederherstellen des Credential-Store-Zustands: {ex}"); }
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
    protected void NavigateToProjects(AutomationElement mainWindow)
    {
        var button = WaitForElement(mainWindow, cf => cf.ByName(" Projekte"), Short);
        button.AsButton().Click();
    }
    /// <summary>
    /// Navigiert von der Projekt-Kachel zurück zur Projektliste. Wird benötigt, wenn ein Test nach dem Öffnen eines Projekts wieder zur Projektliste zurückkehren muss.
    /// </summary>
    /// <param name="mainWindow">Das Hauptfenster der Anwendung.</param>
    protected void NavigateBackFromProjectCardToProjectsList(AutomationElement mainWindow)
    {
        var button = WaitForElement(mainWindow, cf => cf.ByName("Zurück"), Short);
        button.AsButton().Click();
    }

    /// <summary>
    /// Navigiert vom Dashboard zurück zur Projektliste. Wird benötigt, wenn ein Test nach dem Öffnen des Dashboards wieder zur Projektliste zurückkehren muss. 
    /// </summary>
    /// <param name="mainWindow">Das Hauptfenster der Anwendung.</param>
    protected void NavigateBackToDashboard(AutomationElement mainWindow)
    {
        var dashboardButton = WaitForElement(mainWindow, cf => cf.ByName("Dashboard"), Short);
        dashboardButton.AsButton().Click();
    }

    /// <summary>Navigiert zur Einstellungsseite und wartet, bis die Settings-Tabs geladen sind.</summary>
    protected void NavigateToSettings(AutomationElement mainWindow)
    {
        var deadline = DateTime.UtcNow + Long;
        while (DateTime.UtcNow < deadline)
        {
            mainWindow.Focus();

            var button = mainWindow.FindFirstDescendant(cf => cf.ByName(" Einstellungen"));
            if (button is not null)
                button.AsButton().Click();

            var settingsTab = mainWindow.FindFirstDescendant(cf => cf.ByName("Quellcodeverwaltung"));
            if (settingsTab is not null)
                return;

            Thread.Sleep(300);
        }

        WaitForElement(mainWindow, cf => cf.ByName("Quellcodeverwaltung"), Short);
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
        NavigateToProjects(mainWindow);

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
    protected string CreateLocalSourceDirectory(string repositoryFolderName, bool initializeGitRepository = true)
    {
        var sourceDirectory = Path.Combine(
            Path.GetTempPath(),
            $"softwareschmiede_e2e_source_{Guid.NewGuid():N}");
        var repositoryPath = Path.Combine(sourceDirectory, repositoryFolderName);
        Directory.CreateDirectory(repositoryPath);
        File.WriteAllText(Path.Combine(repositoryPath, "readme.txt"), "E2E-Testdatei");

        if (initializeGitRepository)
        {
            InitializeGitRepository(repositoryPath);
        }

        return sourceDirectory;
    }

    private static void InitializeGitRepository(string repositoryPath)
    {
        RunGitCommand(repositoryPath, "init");
        RunGitCommand(repositoryPath, "config", "user.name", "Softwareschmiede E2E");
        RunGitCommand(repositoryPath, "config", "user.email", "e2e@softwareschmiede.local");
        RunGitCommand(repositoryPath, "add", ".");
        RunGitCommand(repositoryPath, "commit", "-m", "Initial E2E repository snapshot");
    }

    private static void RunGitCommand(string workingDirectory, params string[] arguments)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        process.Start();
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Git-Testrepository konnte nicht vorbereitet werden: git {string.Join(' ', arguments)} in '{workingDirectory}' " +
                $"endete mit Exit-Code {process.ExitCode}.{Environment.NewLine}{stdout}{stderr}");
        }
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
        NavigateToSettings(mainWindow);

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
        bool useInSourceDirectoryMode = true,
        bool initializeSourceGitRepository = true)
    {
        var app = LaunchApp();
        var mainWindow = app.GetMainWindow(Automation, Long)!;
        return SetupProjectMitNeuerAufgabeForStartedApp(mainWindow, repositoryFolderName, projektName, useInSourceDirectoryMode, initializeSourceGitRepository);
    }
    /// <summary>
    /// Konfiguriert das LocalDirectoryPlugin mit einem neu erstellten lokalen Quellverzeichnis, legt ein Projekt an, öffnet es, weist das Repository zu und erstellt eine neue Aufgabe. Gibt das Hauptfenster zurück, in dem die Aufgabe im Edit-Panel (Status "Neu") bereit zum Starten ist.
    /// Im Gegensatz zu <see cref="SetupProjectMitNeuerAufgabe"/> startet diese Überladung keine neue Anwendung, sondern nutzt ein bereits laufendes Hauptfenster - für Testphasen, die als weiterer Schritt in einem gemeinsamen App-Lifecycle laufen.
    /// </summary>
    /// <param name="mainWindow">Das bereits laufende Hauptfenster, in dem Projekt und Aufgabe angelegt werden.</param>
    /// <param name="repositoryFolderName">Name des lokalen Quellverzeichnisses/Repositorys, das angelegt wird.</param>
    /// <param name="projektName">Name des anzulegenden Projekts.</param>
    /// <param name="useInSourceDirectoryMode">Ob das LocalDirectoryPlugin im In-Source-Directory-Modus konfiguriert wird.</param>
    /// <param name="initializeSourceGitRepository">Ob im lokalen Quellverzeichnis ein Git-Repository initialisiert wird.</param>
    /// <returns>Das Hauptfenster mit der neu angelegten Aufgabe im Edit-Panel.</returns>
    protected AutomationElement SetupProjectMitNeuerAufgabeForStartedApp(
        Window mainWindow,
        string repositoryFolderName,
        string projektName,
        bool useInSourceDirectoryMode = true,
        bool initializeSourceGitRepository = true)
    {
        var sourceDirectory = CreateLocalSourceDirectory(repositoryFolderName, initializeSourceGitRepository);

        ConfigureLocalDirectoryPlugin(mainWindow, sourceDirectory, useInSourceDirectoryMode);

        NavigateToProjects(mainWindow);
        CreateAndOpenProject(mainWindow, projektName);

        AssignLocalDirectoryRepository(mainWindow);

        NeueAufgabeAnlegen(mainWindow);

        return mainWindow;
    }


    /// <summary>
    /// Überspringt den aufrufenden Test (via <c>Skip.If</c>), wenn die Umgebungsvariable
    /// <c>SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1</c> gesetzt ist (siehe
    /// <see cref="ConPtyEnvironmentProbe"/> und CLAUDE.md, Abschnitt Testing). Betrifft alle Tests,
    /// die über einen CLI-Prozess (ConPTY, z. B. via <see cref="StartenUndPluginWaehlen"/>)
    /// sichtbare Terminal-Ausgabe erwarten - ohne funktionierende Konsolen-Isolation scheitern sie
    /// sonst mit einem nichtssagenden <see cref="TimeoutException"/> statt mit einer erklärenden
    /// Meldung. Muss als erste Zeile des jeweiligen Testkörpers aufgerufen werden, vor jedem
    /// App-Start. Erfordert, dass die aufrufende Testmethode mit <c>[SkippableFact]</c> (statt
    /// <c>[Fact]</c>) annotiert ist (Paket <c>Xunit.SkippableFact</c>) - nur damit erzeugt
    /// <c>Skip.If</c> ein echtes "Skipped"-Ergebnis statt eines fehlschlagenden Tests.
    /// </summary>
    protected static void SkipWennConPtyNichtVerfuegbar()
    {
        Skip.If(!ConPtyEnvironmentProbe.IsAvailable, ConPtyEnvironmentProbe.UnavailableReason);
    }

    /// <summary>
    /// Klickt den "Starten"-Button und bedient den anschließend erscheinenden Plugin-Auswahl-Dialog:
    /// wählt das angegebene KI-Plugin aus, setzt optional die "FuerProjektVerwenden"-Checkbox
    /// (Projekt-Standard speichern) und bestätigt mit "OK".
    /// </summary>
    /// <param name="mainWindow">Das Hauptfenster der Anwendung.</param>
    /// <param name="pluginName">Der Name des im Dialog auszuwählenden KI-Plugins.</param>
    /// <param name="fuerProjektVerwenden">Wenn <c>true</c>, wird vor dem Bestätigen die
    /// "FuerProjektVerwenden"-Checkbox gesetzt, damit das gewählte Plugin als Projekt-Standard
    /// gespeichert wird.</param>
    protected void StartenUndPluginWaehlen(AutomationElement mainWindow, string pluginName, bool fuerProjektVerwenden = false)
    {
        var startenButton = WaitForElement(mainWindow, cf => cf.ByName("Starten"), Short);
        startenButton.AsButton().Click();

        var dialog = WaitForWindow("KI-Plugin auswählen", Medium);
        var pluginAuswahlBox = WaitForElement(dialog, cf => cf.ByName("PluginAuswahl"), Short);
        SelectComboBoxItemByClick(pluginAuswahlBox, pluginName, Short);

        if (fuerProjektVerwenden)
        {
            var checkbox = WaitForElement(dialog, cf => cf.ByName("FuerProjektVerwenden"), Short);
            checkbox.AsCheckBox().IsChecked = true;
        }

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

    /// <summary>
    /// Klickt den "AufgabeNeu"-Button; die App legt die Aufgabe sofort mit Status "Neu" an und
    /// navigiert in die separate <c>TaskDetailView</c>. Wartet auf das "EditTitel"-Feld und gibt es zurück.
    /// </summary>
    protected AutomationElement NeueAufgabeAnlegen(AutomationElement mainWindow)
    {
        var aufgabeNeuButton = WaitForElement(mainWindow, cf => cf.ByName("AufgabeNeu"), Short);
        aufgabeNeuButton.AsButton().Click();

        return WaitForElement(mainWindow, cf => cf.ByName("EditTitel"), Short);
    }

    /// <summary>
    /// Fokussiert das "EditTitel"-Feld, markiert dessen Inhalt (Strg+A) und tippt <paramref name="titel"/>.
    /// Voraussetzung: <c>TaskDetailView</c> im Edit-Modus sichtbar (z. B. nach <see cref="NeueAufgabeAnlegen"/>).
    /// </summary>
    protected void AufgabeTitelSetzen(AutomationElement mainWindow, string titel)
    {
        var titelBox = WaitForElement(mainWindow, cf => cf.ByName("EditTitel"), Short);
        FeldInhaltErsetzen(titelBox, titel);
    }

    /// <summary>
    /// Klickt den "Speichern"-Button in der <c>TaskDetailView</c> und wartet auf das Wiedererscheinen
    /// von "ProjektName" (Rückkehr zur <c>ProjectDetailView</c>).
    /// </summary>
    protected void AufgabeDetailSpeichern(AutomationElement mainWindow)
    {
        var speichernButton = WaitForElement(mainWindow, cf => cf.ByName("Speichern"), Short);
        speichernButton.AsButton().Click();

        WaitForElement(mainWindow, cf => cf.ByName("ProjektName"), Medium);
    }

    /// <summary>
    /// Verwirft die Bearbeitung über den "Zurück"-Button in der <c>TaskDetailView</c> und wartet auf
    /// das Wiedererscheinen von "ProjektName" (Rückkehr zur <c>ProjectDetailView</c>).
    /// </summary>
    protected void AufgabeDetailZurueck(AutomationElement mainWindow)
    {
        var zurueckButton = WaitForElement(mainWindow, cf => cf.ByName("Zurück"), Short);
        zurueckButton.AsButton().Click();

        WaitForElement(mainWindow, cf => cf.ByName("ProjektName"), Medium);
    }

    /// <summary>
    /// Löscht das aktuell in der <c>ProjectDetailView</c> geöffnete Projekt über den "Löschen"-Button
    /// und bestätigt den nativen Win32-Bestätigungsdialog. Für mehrphasige Tests, die nach einer Phase
    /// ihr Projekt aufräumen müssen, bevor die nächste Phase im selben App-Lifecycle beginnt.
    /// </summary>
    /// <param name="mainWindow">Das Hauptfenster mit geöffneter <c>ProjectDetailView</c> (Voraussetzung: "AufgabeNeu" sichtbar).</param>
    protected void DeleteCurrentProject(AutomationElement mainWindow)
    {
        WaitForElement(mainWindow, cf => cf.ByName("AufgabeNeu"), Short);

        var loeschenButton = WaitForElement(mainWindow, cf => cf.ByName("Löschen"), Short);
        loeschenButton.AsButton().Click();

        // MessageBox "Löschen bestätigen" erscheint als separates Fenster. Der Titel stammt aus der
        // Anwendung (App-Ressource, daher sprachunabhängig sprachlich "Löschen bestätigen"), die
        // Button-Beschriftung "Ja"/"Nein" dagegen wird vom nativen Win32-MessageBox-Control anhand
        // der Systemsprache des ausführenden Betriebssystems gerendert (System.Windows.MessageBox
        // erlaubt keine eigenen Button-Texte) - auf einem englischsprachigen CI-Runner (z. B.
        // windows-latest bei GitHub Actions) heißt der Button "Yes" statt "Ja", wodurch die Suche
        // nach dem Namen dort unabhängig vom Timeout nie etwas findet. Die Automation-ID des
        // Ja/Yes-Buttons entspricht dagegen der stabilen, sprachunabhängigen Win32-Dialog-Control-ID
        // IDYES (6) und funktioniert auf jeder Systemsprache identisch.
        var msgBox = WaitForWindow("Löschen bestätigen", Short);
        var jaButton = WaitForElement(msgBox, cf => cf.ByAutomationId("6"), Short);
        jaButton.AsButton().Click();

        // Overlay geschlossen — "Speichern" nicht mehr sichtbar
        WaitUntilGone(mainWindow, cf => cf.ByName("Speichern"), Short);
    }

    /// <summary>
    /// Löscht die aktuell im Edit-Panel geöffnete Aufgabe über den "Löschen"-Button und bestätigt den
    /// nativen Win32-Bestätigungsdialog. Für mehrphasige Tests, die nach einer Phase ihre Aufgabe
    /// aufräumen müssen, bevor die nächste Phase im selben App-Lifecycle beginnt.
    /// </summary>
    /// <param name="mainWindow">Das Hauptfenster mit geöffneter Aufgabe (Voraussetzung: "Starten" sichtbar, d. h. kein laufender CLI-Prozess).</param>
    protected void DeleteCurrentTask(AutomationElement mainWindow)
    {
        WaitForElement(mainWindow, cf => cf.ByName("Starten"), Short);

        var loeschenButton = WaitForElement(mainWindow, cf => cf.ByName("Löschen"), Short);
        loeschenButton.AsButton().Click();

        var msgBox = WaitForWindow("Löschen bestätigen", Short);
        var jaButton = WaitForElement(msgBox, cf => cf.ByAutomationId("6"), Short);
        jaButton.AsButton().Click();

        // Overlay geschlossen — "Starten" nicht mehr sichtbar
        WaitUntilGone(mainWindow, cf => cf.ByName("Starten"), Short);
    }

    /// <summary>Wartet auf die "OffeneAufgabenListe" und gibt deren ListItem-Kinder zurück.</summary>
    protected AutomationElement[] OffeneAufgabenItems(AutomationElement mainWindow)
    {
        var listBox = WaitForElement(mainWindow, cf => cf.ByName("OffeneAufgabenListe"), Medium);
        return listBox.FindAllChildren(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.ListItem));
    }

    /// <summary>
    /// Öffnet das erste Item aus einer bereits ermittelten Aufgabenliste per Doppelklick, wodurch die
    /// <c>TaskDetailView</c> fensterumfassend erscheint. Für Aufrufer, die <see cref="OffeneAufgabenItems"/>
    /// bereits selbst (z. B. für ein eigenes Assert) abgefragt haben, um eine erneute UI-Automation-Abfrage
    /// derselben Liste zu vermeiden. Voraussetzung: <paramref name="items"/> enthält mindestens ein Element.
    /// </summary>
    /// <param name="items">Die bereits ermittelten Items der "OffeneAufgabenListe".</param>
    /// <exception cref="InvalidOperationException">Wird geworfen, wenn <paramref name="items"/> leer ist.</exception>
    protected static void ErsteOffeneAufgabeOeffnen(AutomationElement[] items)
    {
        if (items.Length == 0)
            throw new InvalidOperationException(
                "ErsteOffeneAufgabeOeffnen wurde mit einer leeren Aufgabenliste aufgerufen. " +
                "Aufrufer müssen zuvor sicherstellen, dass OffeneAufgabenItems mindestens ein Element liefert.");

        items[0].DoubleClick();
    }

    /// <summary>
    /// Sucht in der Aufgabenliste das ListItem mit dem angegebenen <paramref name="titel"/>, öffnet es
    /// per Doppelklick und wartet auf den "Zurück"-Button (Bestätigung, dass die <c>TaskDetailView</c> geladen ist).
    /// </summary>
    protected void AufgabeAusListeOeffnen(AutomationElement mainWindow, string titel)
    {
        var listenEintrag = WaitForElement(
            mainWindow,
            cf => cf.ByName(titel).And(cf.ByControlType(FlaUI.Core.Definitions.ControlType.ListItem)),
            Medium);
        listenEintrag.DoubleClick();
        WaitForElement(mainWindow, cf => cf.ByName("Zurück"), Short);
    }

    /// <summary>
    /// Fokussiert das "ProjektName"-Feld, markiert dessen Inhalt (Strg+A), tippt <paramref name="neuerName"/>
    /// und klickt "Speichern" (UpdateAsync-Pfad, bleibt in der Detailansicht). Der "Speichern"-Button
    /// verschwindet in diesem Pfad nicht (im Gegensatz zur Neuanlage) - als echtes Synchronisationssignal
    /// für den Abschluss von Speichern + anschließendem Reload wird stattdessen gewartet, bis das
    /// "ProjektName"-Feld den nach dem Reload aus der Datenbank zurückgebundenen (getrimmten) Namen
    /// tatsächlich anzeigt.
    /// Voraussetzung: <c>ProjectDetailView</c> im Edit-Modus sichtbar.
    /// </summary>
    /// <param name="mainWindow">Das Hauptfenster der Anwendung.</param>
    /// <param name="neuerName">Der neue Projektname.</param>
    protected void ProjektNamenAendernUndSpeichern(AutomationElement mainWindow, string neuerName)
    {
        var nameBox = WaitForElement(mainWindow, cf => cf.ByName("ProjektName"), Short);
        FeldInhaltErsetzen(nameBox, neuerName);

        var speichernButton = WaitForElement(mainWindow, cf => cf.ByName("Speichern"), Short);
        speichernButton.AsButton().Click();

        WaitForTextBoxText(mainWindow, "ProjektName", neuerName.Trim(), Medium);
    }

    /// <summary>
    /// Wartet, bis die per <paramref name="automationName"/> gefundene TextBox im Teilbaum von
    /// <paramref name="parent"/> exakt <paramref name="expectedText"/> anzeigt. Dient als echtes
    /// Synchronisationssignal für Abläufe, bei denen ein Button nach dem Speichern nicht verschwindet,
    /// der zugrunde liegende Wert aber nach einem Reload sichtbar aktualisiert wird.
    /// </summary>
    /// <param name="parent">Das Element, dessen Teilbaum durchsucht wird.</param>
    /// <param name="automationName">Der Automation-Name der gesuchten TextBox.</param>
    /// <param name="expectedText">Der erwartete Textinhalt.</param>
    /// <param name="timeout">Maximale Wartezeit.</param>
    /// <exception cref="TimeoutException">Wird geworfen, wenn die TextBox nicht innerhalb von <paramref name="timeout"/> den erwarteten Text anzeigt.</exception>
    private static void WaitForTextBoxText(AutomationElement parent, string automationName, string expectedText, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        string? aktuellerText = null;
        while (DateTime.UtcNow < deadline)
        {
            var box = parent.FindFirstDescendant(cf => cf.ByName(automationName));
            aktuellerText = box?.AsTextBox().Text;
            if (string.Equals(aktuellerText, expectedText, StringComparison.Ordinal))
                return;

            Thread.Sleep(200);
        }

        throw new TimeoutException(
            $"TextBox '{automationName}' zeigte nicht innerhalb von {timeout.TotalSeconds}s den erwarteten Text '{expectedText}'. Aktuell: '{aktuellerText}'.");
    }

    /// <summary>
    /// Fokussiert <paramref name="box"/> per Klick, markiert dessen Inhalt (Strg+A) und ersetzt ihn
    /// durch <paramref name="text"/>.
    /// </summary>
    private static void FeldInhaltErsetzen(AutomationElement box, string text)
    {
        box.Click();
        Keyboard.TypeSimultaneously(FlaUI.Core.WindowsAPI.VirtualKeyShort.CONTROL, FlaUI.Core.WindowsAPI.VirtualKeyShort.KEY_A);
        Keyboard.Type(text);
    }
}
