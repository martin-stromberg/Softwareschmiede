namespace Softwareschmiede.Infrastructure.Services;

/// <summary>
/// Gemeinsame Hilfsfunktionen zum Auflösen von ausführbaren Programmen über die
/// PATH-Umgebungsvariable. Wird von <see cref="Application.Services.RepositoryScriptExecutor"/>
/// für die Suche nach 'bash.exe' verwendet sowie von <see cref="VisualStudioCodeLocator"/> und
/// <see cref="Updates.UpdateScriptService"/> für das Aufteilen der PATH-Verzeichnisliste.
/// <see cref="VisualStudioCodeLocator"/> und <see cref="Updates.UpdateScriptService"/> prüfen je
/// PATH-Verzeichnis mehrere Kandidaten-Dateinamen (mehrere Namensvarianten bzw. PATHEXT-Erweiterungen)
/// statt eines einzelnen exakten Dateinamens; eine vollständige Vereinheitlichung dieser
/// Kandidatensuche würde ihr bestehendes Verhalten verändern und wird daher bewusst nicht
/// vorgenommen, die PATH-Zerlegung selbst wird jedoch geteilt.
/// </summary>
internal static class PathExecutableResolver
{
    /// <summary>Liefert die einzelnen Verzeichniseinträge der PATH-Umgebungsvariable.</summary>
    /// <param name="getEnvironmentVariable">Zugriff auf Umgebungsvariablen (Standard: <see cref="Environment.GetEnvironmentVariable(string)"/>).</param>
    /// <returns>Die getrimmten, nicht-leeren PATH-Verzeichniseinträge.</returns>
    public static IEnumerable<string> EnumeratePathDirectories(Func<string, string?> getEnvironmentVariable)
    {
        var path = getEnvironmentVariable("PATH") ?? string.Empty;
        return path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    /// <summary>
    /// Sucht eine ausführbare Datei mit exaktem Dateinamen in den PATH-Verzeichnissen und liefert
    /// den ersten gefundenen vollständigen Pfad, oder <c>null</c>, falls keiner existiert.
    /// </summary>
    /// <param name="fileName">Exakter Dateiname der gesuchten ausführbaren Datei (z. B. "bash.exe").</param>
    /// <param name="getEnvironmentVariable">Zugriff auf Umgebungsvariablen (Standard: <see cref="Environment.GetEnvironmentVariable(string)"/>).</param>
    /// <param name="fileExists">Existenzprüfung für Dateien (Standard: <see cref="File.Exists(string?)"/>).</param>
    /// <returns>Der vollständige Pfad der gefundenen Datei, oder <c>null</c>, falls keine gefunden wurde.</returns>
    public static string? TryResolveByFileName(
        string fileName,
        Func<string, string?> getEnvironmentVariable,
        Func<string, bool> fileExists)
    {
        foreach (var directory in EnumeratePathDirectories(getEnvironmentVariable))
        {
            var candidate = Path.Combine(directory, fileName);
            if (fileExists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}
