using Microsoft.Extensions.Logging;
using Softwareschmiede.Domain.Interfaces;

namespace Softwareschmiede.Infrastructure.Services;

/// <summary>
/// Testmodus-Implementierung von <see cref="ISystemShutdownService"/>: löst keinen echten
/// OS-Shutdown aus, sondern hängt jede Shutdown-Anforderung (inkl. Force-Flag) als Zeile an
/// eine vom Aufrufer übergebene Logdatei an, damit E2E-Tests die Anforderung ohne echten
/// OS-Aufruf beobachten können (Muster: <see cref="AufzeichnenderProzessStarter"/>).
/// </summary>
/// <param name="logger">Logger für Diagnosemeldungen.</param>
/// <param name="logDateiPfad">
/// Der Pfad der Logdatei, an die jede Shutdown-Anforderung als Zeile angehängt wird. Wird vom
/// Aufrufer aufgelöst (z. B. via <see cref="ResolveLogDateiPfad"/>), statt hier versteckt aus
/// einer Umgebungsvariable gelesen zu werden.
/// </param>
public sealed class AufzeichnenderSystemShutdownService(ILogger<AufzeichnenderSystemShutdownService> logger, string logDateiPfad) : ISystemShutdownService
{
    /// <summary>
    /// Dateiname der Logdatei, in die jede Shutdown-Anforderung als Zeile angehängt wird.
    /// Einzige Quelle für Produktions- und Testcode (z. B. <c>WpfTestBase</c>), damit Dateiname
    /// und Pfadauflösung nicht mehrfach dupliziert werden.
    /// </summary>
    public const string LogDateiName = "system-shutdowns.log";

    /// <inheritdoc/>
    public Task RequestShutdownAsync(bool erzwingen, CancellationToken cancellationToken = default)
    {
        var zeile = $"erzwingen={erzwingen}|{DateTimeOffset.UtcNow:O}|pid={Environment.ProcessId}";

        logger.LogWarning("OS-Shutdown im Testmodus aufgezeichnet: {Zeile}", zeile);

        var verzeichnis = Path.GetDirectoryName(logDateiPfad);
        if (!string.IsNullOrEmpty(verzeichnis))
        {
            Directory.CreateDirectory(verzeichnis);
        }

        File.AppendAllText(logDateiPfad, zeile + Environment.NewLine);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Löst den Pfad der Shutdown-Logdatei anhand des Verzeichnisses der Test-Datenbank auf. Legt
    /// die Logdatei neben <paramref name="testDbPath"/> ab, oder im temporären Verzeichnis, falls
    /// <paramref name="testDbPath"/> leer ist oder kein Verzeichnisanteil ermittelt werden kann.
    /// </summary>
    /// <param name="testDbPath">Der Pfad der Test-Datenbank, oder <c>null</c>/leer.</param>
    /// <returns>Der vollständige Pfad der Shutdown-Logdatei.</returns>
    public static string ResolveLogDateiPfad(string? testDbPath)
    {
        var verzeichnis = string.IsNullOrEmpty(testDbPath)
            ? Path.GetTempPath()
            : Path.GetDirectoryName(testDbPath) ?? Path.GetTempPath();

        return Path.Combine(verzeichnis, LogDateiName);
    }
}
