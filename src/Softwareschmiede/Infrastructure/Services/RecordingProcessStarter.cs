using Microsoft.Extensions.Logging;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Infrastructure.Services;

/// <summary>
/// Testmodus-Implementierung von <see cref="IProcessStarter"/>: startet keinen echten Prozess, sondern
/// hängt jede <see cref="ProcessStartRequest"/> als Zeile an eine vom Aufrufer übergebene Logdatei an,
/// damit E2E-Tests den Prozessstart ohne echten OS-Aufruf beobachten können.
/// </summary>
/// <param name="logger">Logger für Diagnosemeldungen.</param>
/// <param name="logFilePath">
/// Der Pfad der Logdatei, an die jede aufgezeichnete <see cref="ProcessStartRequest"/> als Zeile
/// angehängt wird. Wird vom Aufrufer aufgelöst (z. B. via <see cref="ResolveLogFilePath"/>), statt
/// hier versteckt aus einer Umgebungsvariable gelesen zu werden.
/// </param>
public sealed class RecordingProcessStarter(ILogger<RecordingProcessStarter> logger, string logFilePath) : IProcessStarter
{
    /// <summary>
    /// Dateiname der Logdatei, in die jede aufgezeichnete <see cref="ProcessStartRequest"/> als Zeile
    /// angehängt wird. Einzige Quelle für Produktions- und Testcode (z. B. <c>WpfTestBase</c>), damit
    /// Dateiname und Pfadauflösung nicht mehrfach dupliziert werden.
    /// </summary>
    public const string LogFileName = "prozess-starts.log";

    /// <inheritdoc/>
    public void Start(ProcessStartRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var line = $"{request.FileName}|{request.Arguments}|{request.UseShellExecute}";

        logger.LogInformation("Prozessstart im Testmodus aufgezeichnet: {Zeile}", line);

        File.AppendAllText(logFilePath, line + Environment.NewLine);
    }

    /// <summary>
    /// Löst den Pfad der Prozessstart-Logdatei anhand des Verzeichnisses der Test-Datenbank auf. Legt die
    /// Logdatei neben <paramref name="testDbPath"/> ab, oder im temporären Verzeichnis, falls
    /// <paramref name="testDbPath"/> leer ist oder kein Verzeichnisanteil ermittelt werden kann.
    /// </summary>
    /// <param name="testDbPath">Der Pfad der Test-Datenbank, oder <c>null</c>/leer.</param>
    /// <returns>Der vollständige Pfad der Prozessstart-Logdatei.</returns>
    public static string ResolveLogFilePath(string? testDbPath)
    {
        var directory = string.IsNullOrEmpty(testDbPath)
            ? Path.GetTempPath()
            : Path.GetDirectoryName(testDbPath) ?? Path.GetTempPath();

        return Path.Combine(directory, LogFileName);
    }
}
