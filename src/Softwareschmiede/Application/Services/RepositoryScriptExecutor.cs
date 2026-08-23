using Microsoft.Extensions.Logging;
using Softwareschmiede.Domain.Interfaces;

namespace Softwareschmiede.Application.Services;

/// <summary>
/// Gemeinsame Ausführungslogik für konfigurierbare Repository-Skripte (z. B. Start- oder
/// Initialisierungsskripte), genutzt von <see cref="RepositoryStartskriptService"/> und
/// <see cref="RepositoryInitialisierungService"/>.
/// </summary>
internal static class RepositoryScriptExecutor
{
    private const string PowershellExecutable = "powershell.exe";

    /// <summary>Führt ein konfiguriertes Repository-Skript per PowerShell aus.</summary>
    /// <param name="repositoryRootPath">Wurzelverzeichnis des Repositories.</param>
    /// <param name="aktiv">Gibt an, ob die Konfiguration aktiv ist; bei <c>false</c> wird nichts ausgeführt.</param>
    /// <param name="relativePath">Relativer Pfad zum Skript innerhalb des Repositories.</param>
    /// <param name="scriptLabel">Sprechender Name des Skripts für Log- und Fehlermeldungen (z. B. "Startskript").</param>
    /// <param name="cliRunner">CLI-Runner zur Ausführung des Skripts.</param>
    /// <param name="logger">Logger für Diagnosemeldungen.</param>
    /// <param name="ct">Abbruch-Token.</param>
    public static async Task RunAsync(
        string repositoryRootPath,
        bool aktiv,
        string relativePath,
        string scriptLabel,
        ICliRunner cliRunner,
        ILogger logger,
        CancellationToken ct)
    {
        if (!aktiv)
        {
            logger.LogInformation("Repository-{ScriptLabel} ist deaktiviert.", scriptLabel);
            return;
        }

        var scriptPath = ResolveScriptPath(repositoryRootPath, relativePath, scriptLabel);
        if (!File.Exists(scriptPath))
        {
            throw new InvalidOperationException($"Das {scriptLabel} wurde nicht gefunden: {relativePath}");
        }

        var args = BuildArguments(scriptPath);
        logger.LogInformation(
            "Starte Repository-{ScriptLabel} '{Script}' für Repository '{RepositoryRootPath}'.",
            scriptLabel, relativePath, repositoryRootPath);

        var result = await cliRunner.RunAsync(
            PowershellExecutable,
            args,
            repositoryRootPath,
            null,
            ct).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            throw new InvalidOperationException(
                $"Das Repository-{scriptLabel} '{relativePath}' ist fehlgeschlagen: {result.StdErr ?? result.StdOut}");
        }
    }

    private static string ResolveScriptPath(string repositoryRootPath, string relativePath, string scriptLabel)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            throw new InvalidOperationException($"Für die Repository-{scriptLabel}-Konfiguration ist ein Skriptpfad erforderlich.");
        }

        var combined = Path.GetFullPath(Path.Combine(repositoryRootPath, relativePath));
        var normalizedRoot = Path.GetFullPath(repositoryRootPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        if (!combined.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Das {scriptLabel} muss innerhalb des Repositorys liegen.");
        }

        return combined;
    }

    private static IReadOnlyList<string> BuildArguments(string scriptPath)
    {
        return
        [
            "-NoProfile",
            "-NonInteractive",
            "-ExecutionPolicy",
            "Bypass",
            "-File",
            scriptPath
        ];
    }
}
