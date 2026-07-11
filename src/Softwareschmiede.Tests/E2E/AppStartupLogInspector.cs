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
    /// </summary>
    /// <param name="logDirectory">Verzeichnis mit den App-Log-Dateien.</param>
    /// <returns>Länge der neuesten Log-Datei in Bytes, oder 0, falls keine Log-Datei existiert.</returns>
    internal static long Snapshot(string logDirectory)
        => FindLatestLogFile(logDirectory)?.Length ?? 0;

    /// <summary>
    /// Liest den seit <paramref name="offset"/> angehängten Inhalt der neuesten Log-Datei in
    /// <paramref name="logDirectory"/>. Die Datei wird mit <see cref="FileShare.ReadWrite"/> geöffnet,
    /// da Serilog sie während des App-Laufs offen hält.
    /// </summary>
    /// <param name="logDirectory">Verzeichnis mit den App-Log-Dateien.</param>
    /// <param name="offset">Byte-Offset, ab dem gelesen werden soll (siehe <see cref="Snapshot"/>).</param>
    /// <returns>Seit dem Offset angehängter Inhalt, oder ein leerer String, falls keine Log-Datei existiert oder kein neuer Inhalt vorliegt.</returns>
    internal static string GetNewEntries(string logDirectory, long offset)
    {
        var latestLogFile = FindLatestLogFile(logDirectory);
        if (latestLogFile is null)
            return string.Empty;

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
