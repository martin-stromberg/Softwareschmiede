namespace Softwareschmiede.Tests.E2E;

/// <summary>
/// Reine Hilfsklasse zum Auslesen und Filtern der Anwendungs-Log-Dateien während E2E-Tests.
/// Trennt die parse-Logik von der FlaUI-/Prozess-Steuerung in <see cref="WpfTestBase"/>, damit sie
/// ohne laufende App unit-testbar bleibt.
/// </summary>
internal static class AppStartupLogInspector
{
    private const string LogFileSearchPattern = "softwareschmiede-*.log";

    /// <summary>
    /// Ermittelt den aktuellen Byte-Offset (Länge) der neuesten Log-Datei in <paramref name="logDirectory"/>.
    /// Dient als Snapshot vor dem App-Start, damit spätere Auswertungen nur neu angehängte Zeilen betrachten.
    /// Merkt sich zusätzlich den Pfad der zum Snapshot-Zeitpunkt neuesten Datei, damit <see cref="GetNewEntries"/>
    /// bei einem zwischenzeitlichen Log-Rollover (z. B. Tageswechsel) den Offset nicht fälschlich auf eine
    /// andere, neu angelegte Datei anwendet.
    /// </summary>
    /// <param name="logDirectory">Verzeichnis mit den App-Log-Dateien.</param>
    /// <returns>Snapshot aus Dateipfad und Länge der neuesten Log-Datei, oder ein leerer Snapshot, falls keine Log-Datei existiert.</returns>
    internal static LogSnapshot Snapshot(string logDirectory)
    {
        var latestLogFile = FindLatestLogFile(logDirectory);
        return new LogSnapshot(latestLogFile?.FullName, latestLogFile?.Length ?? 0);
    }

    /// <summary>
    /// Liest den seit <paramref name="snapshot"/> angehängten Inhalt der neuesten Log-Datei in
    /// <paramref name="logDirectory"/>. Die Datei wird mit <see cref="FileShare.ReadWrite"/> geöffnet,
    /// da Serilog sie während des App-Laufs offen hält. Ist die zum Auswertungszeitpunkt neueste Datei
    /// eine andere als beim Snapshot (Log-Rollover), wird der Offset ignoriert und ab Dateianfang gelesen,
    /// da der Offset der alten Datei für die neue Datei keine Aussagekraft hat.
    /// </summary>
    /// <param name="logDirectory">Verzeichnis mit den App-Log-Dateien.</param>
    /// <param name="snapshot">Der zuvor per <see cref="Snapshot"/> erstellte Snapshot.</param>
    /// <returns>Seit dem Snapshot angehängter Inhalt, oder ein leerer String, falls keine Log-Datei existiert oder kein neuer Inhalt vorliegt.</returns>
    internal static string GetNewEntries(string logDirectory, LogSnapshot snapshot)
    {
        var latestLogFile = FindLatestLogFile(logDirectory);
        if (latestLogFile is null)
            return string.Empty;

        var offset = string.Equals(latestLogFile.FullName, snapshot.FilePath, StringComparison.OrdinalIgnoreCase)
            ? snapshot.Offset
            : 0;

        using var stream = new FileStream(latestLogFile.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var seekOffset = offset >= 0 && offset <= stream.Length ? offset : 0;
        stream.Seek(seekOffset, SeekOrigin.Begin);

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// Filtert aus <paramref name="logContent"/> die Startup-Fehlerzeilen (<c>[ERR]</c>/<c>[FTL]</c>).
    /// </summary>
    /// <param name="logContent">Log-Inhalt, z. B. das Ergebnis von <see cref="GetNewEntries"/>.</param>
    /// <returns>Zusammengefasster Diagnosetext der Fehlerzeilen, oder <c>null</c>, falls keine gefunden wurden.</returns>
    internal static string? CheckAppStartupException(string logContent)
    {
        if (string.IsNullOrWhiteSpace(logContent))
            return null;

        var errorLines = logContent
            .Split('\n')
            .Select(line => line.TrimEnd('\r'))
            .Where(line => line.Contains("[ERR]", StringComparison.Ordinal) || line.Contains("[FTL]", StringComparison.Ordinal))
            .ToArray();

        return errorLines.Length == 0 ? null : string.Join(Environment.NewLine, errorLines);
    }

    private static FileInfo? FindLatestLogFile(string logDirectory)
    {
        if (!Directory.Exists(logDirectory))
            return null;

        return new DirectoryInfo(logDirectory)
            .GetFiles(LogFileSearchPattern)
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .FirstOrDefault();
    }
}

/// <summary>
/// Snapshot des Zustands der neuesten App-Log-Datei zu einem Zeitpunkt (Dateipfad und Byte-Offset).
/// Wird von <see cref="AppStartupLogInspector.Snapshot"/> erzeugt und an <see cref="AppStartupLogInspector.GetNewEntries"/>
/// übergeben, damit ein zwischenzeitlicher Log-Rollover erkannt werden kann.
/// </summary>
internal readonly record struct LogSnapshot
{
    /// <summary>Voller Pfad der zum Snapshot-Zeitpunkt neuesten Log-Datei, oder <c>null</c> wenn keine existierte.</summary>
    public string? FilePath { get; init; }

    /// <summary>Länge der Datei zum Snapshot-Zeitpunkt in Bytes.</summary>
    public long Offset { get; init; }

    /// <summary>Erstellt einen neuen Snapshot.</summary>
    /// <param name="filePath">Voller Pfad der zum Snapshot-Zeitpunkt neuesten Log-Datei, oder <c>null</c> wenn keine existierte.</param>
    /// <param name="offset">Länge der Datei zum Snapshot-Zeitpunkt in Bytes.</param>
    public LogSnapshot(string? filePath, long offset)
    {
        FilePath = filePath;
        Offset = offset;
    }
}
