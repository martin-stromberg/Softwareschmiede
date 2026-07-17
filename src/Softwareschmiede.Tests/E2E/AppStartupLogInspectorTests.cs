using FluentAssertions;

namespace Softwareschmiede.Tests.E2E;

/// <summary>Tests für <see cref="AppStartupLogInspector"/> gegen synthetische Log-Dateien.</summary>
[OsInterface]
public sealed class AppStartupLogInspectorTests : IDisposable
{
    private readonly string _logDirectory;

    /// <summary>Erstellt ein temporäres Log-Verzeichnis für den Test.</summary>
    public AppStartupLogInspectorTests()
    {
        _logDirectory = Path.Combine(Path.GetTempPath(), "app-startup-log-inspector-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_logDirectory);
    }

    /// <summary>Löscht das temporäre Log-Verzeichnis nach dem Test.</summary>
    public void Dispose()
    {
        if (Directory.Exists(_logDirectory))
            Directory.Delete(_logDirectory, recursive: true);
    }

    /// <summary>CheckAppStartupException liefert den Fehlerauszug, wenn eine MainWindow-Fehlersignatur enthalten ist.</summary>
    [Fact]
    public void CheckAppStartupException_ErkenntMainWindowFehler()
    {
        // Serilog-Standardformat (Serilog.Sinks.File-Default-Template): "yyyy-MM-dd HH:mm:ss.fff zzz [LVL] Message".
        var logContent = "2026-07-10 10:30:45.100 +02:00 [INF] Application started.\n" +
                          "2026-07-10 10:30:46.200 +02:00 [ERR] MainWindow konnte nicht angezeigt werden.\n";

        var result = AppStartupLogInspector.CheckAppStartupException(logContent);

        result.Should().Contain("MainWindow konnte nicht angezeigt werden.");
    }

    /// <summary>CheckAppStartupException liefert null, wenn kein [ERR]/[FTL]-Eintrag im Log enthalten ist.</summary>
    [Fact]
    public void CheckAppStartupException_OhneFehler_LiefertNull()
    {
        var logContent = "2026-07-10 10:30:45.100 +02:00 [INF] Application started.\n" +
                          "2026-07-10 10:30:46.200 +02:00 [INF] MainWindow angezeigt.\n";

        var result = AppStartupLogInspector.CheckAppStartupException(logContent);

        result.Should().BeNull();
    }

    /// <summary>GetNewEntries wertet nur den nach dem Snapshot-Offset angehängten Inhalt aus, alte Zeilen werden ignoriert.</summary>
    [Fact]
    public void GetNewEntries_LiestNurInhaltNachOffset()
    {
        var logFile = Path.Combine(_logDirectory, "softwareschmiede-20260710.log");
        File.WriteAllText(logFile, "2026-07-10 10:00:00.000 +02:00 [INF] Alte Zeile.\n");
        var snapshot = AppStartupLogInspector.Snapshot(_logDirectory);

        File.AppendAllText(logFile, "2026-07-10 10:00:01.000 +02:00 [ERR] Neue Fehlerzeile.\n");

        var result = AppStartupLogInspector.GetNewEntries(_logDirectory, snapshot);

        result.Should().NotContain("Alte Zeile.");
        result.Should().Contain("Neue Fehlerzeile.");
    }

    /// <summary>GetNewEntries liefert einen leeren String, wenn das Log-Verzeichnis nicht existiert.</summary>
    [Fact]
    public void GetNewEntries_KeinVerzeichnis_LiefertLeer()
    {
        var fehlendesVerzeichnis = Path.Combine(_logDirectory, "existiert-nicht");

        var result = AppStartupLogInspector.GetNewEntries(fehlendesVerzeichnis, new LogSnapshot(null, 0));

        result.Should().BeEmpty();
    }

    /// <summary>GetNewEntries wählt bei mehreren Log-Dateien die zuletzt geänderte aus.</summary>
    [Fact]
    public void GetNewEntries_WaehltNeuesteDatei()
    {
        var aelteresLog = Path.Combine(_logDirectory, "softwareschmiede-20260708.log");
        var neueresLog = Path.Combine(_logDirectory, "softwareschmiede-20260710.log");
        File.WriteAllText(aelteresLog, "Aeltere Datei.");
        File.WriteAllText(neueresLog, "Neuere Datei.");
        File.SetLastWriteTimeUtc(aelteresLog, DateTime.UtcNow.AddMinutes(-10));
        File.SetLastWriteTimeUtc(neueresLog, DateTime.UtcNow);

        var result = AppStartupLogInspector.GetNewEntries(_logDirectory, new LogSnapshot(null, 0));

        result.Should().Be("Neuere Datei.");
    }

    /// <summary>
    /// Bei einem Log-Rollover zwischen Snapshot und Auswertung (neue, "jüngere" Datei entsteht, z. B. durch
    /// Tageswechsel) darf der Byte-Offset der alten Datei nicht auf die neue Datei angewandt werden — sonst
    /// würde deren Anfang (inkl. eines dortigen Startup-Fehlers) übersprungen. GetNewEntries muss bei
    /// abweichendem Dateipfad ab Anfang der neuen Datei lesen.
    /// </summary>
    [Fact]
    public void GetNewEntries_BeiLogRollover_LiestNeueDateiAbAnfang()
    {
        var altesLog = Path.Combine(_logDirectory, "softwareschmiede-20260710.log");
        File.WriteAllText(altesLog, new string('A', 200) + "\n");
        var snapshot = AppStartupLogInspector.Snapshot(_logDirectory);

        // Rollover: eine neue, jüngere Datei entsteht. Deren Anfang (die ERR-Zeile) liegt innerhalb
        // der ersten `snapshot.Offset` Bytes — würde der alte Offset fälschlich angewandt, ginge sie verloren.
        var neuesLog = Path.Combine(_logDirectory, "softwareschmiede-20260711.log");
        File.WriteAllText(neuesLog, "[ERR] MainWindow konnte nicht angezeigt werden.\n" + new string('B', 300));
        File.SetLastWriteTimeUtc(altesLog, DateTime.UtcNow.AddMinutes(-1));
        File.SetLastWriteTimeUtc(neuesLog, DateTime.UtcNow);

        var result = AppStartupLogInspector.GetNewEntries(_logDirectory, snapshot);

        result.Should().Contain("[ERR] MainWindow konnte nicht angezeigt werden.");
    }

    /// <summary>
    /// Wird zwischen Snapshot und Auswertung keine neue Datei angelegt (dieselbe Datei bleibt die neueste),
    /// muss der Offset weiterhin angewandt werden, damit alte Zeilen nicht erneut als "neu" gemeldet werden.
    /// </summary>
    [Fact]
    public void GetNewEntries_OhneRollover_WendetOffsetWeiterhinAn()
    {
        var logFile = Path.Combine(_logDirectory, "softwareschmiede-20260710.log");
        File.WriteAllText(logFile, "Alte Zeile.\n");
        var snapshot = AppStartupLogInspector.Snapshot(_logDirectory);

        File.AppendAllText(logFile, "Neue Zeile.\n");

        var result = AppStartupLogInspector.GetNewEntries(_logDirectory, snapshot);

        result.Should().NotContain("Alte Zeile.");
        result.Should().Contain("Neue Zeile.");
    }
}
