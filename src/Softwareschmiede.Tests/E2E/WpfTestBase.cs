using System.IO;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.UIA3;

namespace Softwareschmiede.Tests.E2E;

/// <summary>
/// Basisklasse für WPF-E2E-Tests. Startet die Anwendung als separaten Prozess,
/// wartet auf das Hauptfenster und beendet den Prozess nach dem Test.
/// </summary>
public abstract class WpfTestBase : IDisposable
{
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
        try
        {
            _application?.Close();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"WpfTestBase.Dispose: Fehler beim Schließen der Anwendung: {ex.Message}");
        }

        // Warten, bis der Prozess vollständig beendet ist, damit der nächste Test
        // nicht mit einem noch laufenden Prozess konkurriert.
        try
        {
            _application?.WaitWhileMainHandleIsMissing(TimeSpan.FromSeconds(5));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"WpfTestBase.Dispose: Warten auf Prozessende fehlgeschlagen: {ex.Message}");
        }

        _automation?.Dispose();

        try
        {
            DeleteTestDatabase();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"WpfTestBase.Dispose: Fehler beim Löschen der Testdatenbank: {ex.Message}");
        }
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
                "Softwareschmiede.App", "bin", "Debug",
                "net10.0-windows10.0.17763.0",
                "Softwareschmiede.App.exe")),
            Path.GetFullPath(Path.Combine(
                baseDir,
                "..", "..", "..", "..",
                "Softwareschmiede.App", "bin", "Release",
                "net10.0-windows10.0.17763.0",
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
