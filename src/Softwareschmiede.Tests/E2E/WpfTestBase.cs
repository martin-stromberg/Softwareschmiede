using System.IO;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.Core.Input;
using FlaUI.UIA3;

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

    private FlaUI.Core.Application? _application;
    private UIA3Automation? _automation;
    private readonly string _testDbPath;

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

        _automation = new UIA3Automation();
        _application = FlaUI.Core.Application.Launch(appPath);
        _application.WaitWhileMainHandleIsMissing(TimeSpan.FromSeconds(30));

        // Kurz warten, bis WPF-Rendering und EF-Migrationen abgeschlossen sind.
        Thread.Sleep(2000);

        return _application;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        try { _application?.Close(); } catch { }

        try { _application?.WaitWhileMainHandleIsMissing(TimeSpan.FromSeconds(5)); } catch { }

        _automation?.Dispose();

        try { DeleteTestDatabase(); } catch { }
    }

    /// <summary>
    /// Wartet, bis ein Element im Teilbaum von <paramref name="parent"/> gefunden wird.
    /// Wirft <see cref="TimeoutException"/>, wenn das Element nicht innerhalb von <paramref name="timeout"/> erscheint.
    /// </summary>
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
            Thread.Sleep(200);
        }
        throw new TimeoutException(
            $"Element wurde nicht innerhalb von {timeout.TotalSeconds}s gefunden.");
    }

    /// <summary>
    /// Wartet, bis ein Top-Level-Fenster mit dem angegebenen Titel auf dem Desktop erscheint.
    /// </summary>
    protected AutomationElement WaitForWindow(string title, TimeSpan timeout)
        => WaitForElement(Automation.GetDesktop(), cf => cf.ByName(title), timeout);

    /// <summary>Navigiert zur Projektliste.</summary>
    protected void NavigateToProjecten(AutomationElement mainWindow)
    {
        var button = WaitForElement(mainWindow, cf => cf.ByName(" Projekte"), TimeSpan.FromSeconds(10));
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
        var neuButton = WaitForElement(mainWindow, cf => cf.ByName("Neu"), TimeSpan.FromSeconds(5));
        neuButton.AsButton().Click();

        var nameBox = WaitForElement(mainWindow, cf => cf.ByName("ProjektName"), TimeSpan.FromSeconds(5));
        nameBox.Click();
        Keyboard.Type(name);

        var speichernButton = WaitForElement(mainWindow, cf => cf.ByName("Speichern"), TimeSpan.FromSeconds(5));
        speichernButton.AsButton().Click();

        // Warten bis Overlay geschlossen (Speichern-Button verschwunden)
        WaitUntilGone(mainWindow, cf => cf.ByName("Speichern"), TimeSpan.FromSeconds(10));

        // CreateAsync + Callback-Ausführung abwarten
        WaitForElement(mainWindow, cf => cf.ByName(name), TimeSpan.FromSeconds(10));
    }

    /// <summary>Öffnet ein Projekt aus der Liste anhand seines Namens.</summary>
    protected void OpenProject(AutomationElement mainWindow, string name)
    {
        var projektKachel = WaitForElement(mainWindow, cf => cf.ByName(name), TimeSpan.FromSeconds(5));
        projektKachel.Click();
        WaitForElement(mainWindow, cf => cf.ByName("Speichern"), TimeSpan.FromSeconds(5));
    }

    /// <summary>Legt ein neues Projekt an, speichert es und öffnet es wieder.</summary>
    protected void CreateAndOpenProject(AutomationElement mainWindow, string name)
    {
        CreateProject(mainWindow, name);
        OpenProject(mainWindow, name);
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
}
