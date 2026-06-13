using System.IO;
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
    protected UIA3Automation Automation => _automation!;

    /// <summary>Gibt den gestarteten FlaUI-Application-Handle zurück.</summary>
    protected FlaUI.Core.Application FlaUiApp => _application!;

    /// <summary>Initialisiert eine neue Instanz und erzeugt einen temporären DB-Pfad.</summary>
    protected WpfTestBase()
    {
        _testDbPath = Path.Combine(
            Path.GetTempPath(),
            $"softwareschmiede_e2e_{Guid.NewGuid():N}.db");
    }

    /// <summary>
    /// Startet die Anwendung als Prozess mit einem temporären Datenbankpfad.
    /// Wartet, bis das Hauptfenster sichtbar ist.
    /// </summary>
    protected FlaUI.Core.Application LaunchApp()
    {
        var appPath = ResolveAppExePath();

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
        catch
        {
            // Prozess könnte bereits beendet sein.
        }

        // Warten, bis der Prozess vollständig beendet ist, damit der nächste Test
        // nicht mit einem noch laufenden Prozess konkurriert.
        try
        {
            _application?.WaitWhileMainHandleIsMissing(TimeSpan.FromMilliseconds(1));
        }
        catch
        {
            // Ignorieren – Prozess ist bereits beendet.
        }

        Thread.Sleep(1000);

        _automation?.Dispose();

        try
        {
            if (File.Exists(_testDbPath))
                File.Delete(_testDbPath);
        }
        catch
        {
            // Temporäre DB-Datei bereinigen – Fehler ignorieren.
        }
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
