using Microsoft.Extensions.Logging;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Infrastructure.Services;

namespace Softwareschmiede.Application.Services;

/// <summary>
/// Gemeinsame Ausführungslogik für konfigurierbare Repository-Skripte (z. B. Start- oder
/// Initialisierungsskripte), genutzt von <see cref="RepositoryStartskriptService"/> und
/// <see cref="RepositoryInitialisierungService"/>.
/// </summary>
internal static class RepositoryScriptExecutor
{
    private const string PowershellExecutable = "powershell.exe";
    private const string CmdExecutable = "cmd.exe";
    private const string BashExecutable = "bash.exe";

    private delegate (string Command, IReadOnlyList<string> Args) CommandBuilder(
        string scriptPath,
        string scriptLabel,
        Func<string, string?> getEnvironmentVariable,
        Func<string, bool> fileExists);

    /// <summary>
    /// Ordnet unterstützte Skript-Dateiendungen dem jeweiligen Ausführungsprogramm zu. Sowohl der
    /// Dispatch in <see cref="BuildCommand"/> als auch die Liste der unterstützten Endungen in der
    /// Fehlermeldung (<see cref="SupportedExtensionsText"/>) werden aus dieser einzigen
    /// Datenstruktur abgeleitet, damit beide bei einer künftigen Erweiterung automatisch
    /// synchron bleiben.
    /// </summary>
    private static readonly IReadOnlyList<(string Extension, CommandBuilder Build)> ScriptExecutionRules =
    [
        (".ps1", (scriptPath, _, _, _) => (PowershellExecutable,
        [
            "-NoProfile",
            "-NonInteractive",
            "-ExecutionPolicy",
            "Bypass",
            "-File",
            scriptPath
        ])),
        (".cmd", (scriptPath, _, _, _) => (CmdExecutable, ["/c", scriptPath])),
        (".bat", (scriptPath, _, _, _) => (CmdExecutable, ["/c", scriptPath])),
        (".exe", (scriptPath, _, _, _) => (scriptPath, [])),
        (".sh", (scriptPath, scriptLabel, getEnvironmentVariable, fileExists) =>
            (ResolveBashExecutable(scriptLabel, getEnvironmentVariable, fileExists), [scriptPath])),
    ];

    private static readonly string SupportedExtensionsText =
        string.Join(", ", ScriptExecutionRules.Select(rule => rule.Extension));

    /// <summary>
    /// Führt ein konfiguriertes Repository-Skript mit dem zur Dateiendung passenden
    /// Ausführungsprogramm aus (unterstützt werden .ps1, .cmd, .bat, .exe und .sh).
    /// </summary>
    /// <param name="repositoryRootPath">Wurzelverzeichnis des Repositories.</param>
    /// <param name="aktiv">Gibt an, ob die Konfiguration aktiv ist; bei <c>false</c> wird nichts ausgeführt.</param>
    /// <param name="relativePath">Relativer Pfad zum Skript innerhalb des Repositories.</param>
    /// <param name="scriptLabel">Sprechender Name des Skripts für Log- und Fehlermeldungen (z. B. "Startskript").</param>
    /// <param name="cliRunner">CLI-Runner zur Ausführung des Skripts.</param>
    /// <param name="logger">Logger für Diagnosemeldungen.</param>
    /// <param name="ct">Abbruch-Token.</param>
    /// <param name="getEnvironmentVariable">
    /// Optionaler Seam zum Auslesen von Umgebungsvariablen, ausschließlich für die PATH-Auflösung
    /// von <c>bash.exe</c> bei .sh-Skripten benötigt; Standard ist
    /// <see cref="Environment.GetEnvironmentVariable(string)"/>. Dient deterministischen Tests.
    /// </param>
    /// <param name="fileExists">
    /// Optionaler Seam zur Existenzprüfung von Dateien während der PATH-Auflösung von
    /// <c>bash.exe</c>; Standard ist <see cref="File.Exists(string?)"/>. Dient deterministischen Tests.
    /// </param>
    public static async Task RunAsync(
        string repositoryRootPath,
        bool aktiv,
        string relativePath,
        string scriptLabel,
        ICliRunner cliRunner,
        ILogger logger,
        CancellationToken ct,
        Func<string, string?>? getEnvironmentVariable = null,
        Func<string, bool>? fileExists = null)
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

        var (command, args) = BuildCommand(
            scriptPath,
            scriptLabel,
            getEnvironmentVariable ?? Environment.GetEnvironmentVariable,
            fileExists ?? File.Exists);
        logger.LogInformation(
            "Starte Repository-{ScriptLabel} '{Script}' für Repository '{RepositoryRootPath}'.",
            scriptLabel, relativePath, repositoryRootPath);

        var result = await cliRunner.RunAsync(
            command,
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

    private static (string Command, IReadOnlyList<string> Args) BuildCommand(
        string scriptPath,
        string scriptLabel,
        Func<string, string?> getEnvironmentVariable,
        Func<string, bool> fileExists)
    {
        var extension = Path.GetExtension(scriptPath);

        foreach (var rule in ScriptExecutionRules)
        {
            if (string.Equals(rule.Extension, extension, StringComparison.OrdinalIgnoreCase))
            {
                return rule.Build(scriptPath, scriptLabel, getEnvironmentVariable, fileExists);
            }
        }

        throw new InvalidOperationException(
            $"Das {scriptLabel} hat einen nicht unterstützten Dateityp '{extension}'. Unterstützt werden: {SupportedExtensionsText}.");
    }

    private static string ResolveBashExecutable(
        string scriptLabel,
        Func<string, string?> getEnvironmentVariable,
        Func<string, bool> fileExists)
    {
        return PathExecutableResolver.TryResolveByFileName(BashExecutable, getEnvironmentVariable, fileExists)
            ?? throw new InvalidOperationException(
                $"Für die Ausführung des {scriptLabel} als .sh-Skript wird '{BashExecutable}' (z. B. über Git for Windows) benötigt, ist im PATH aber nicht auffindbar.");
    }
}
