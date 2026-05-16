using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Interfaces;

namespace Softwareschmiede.Application.Services;

/// <summary>Führt Repository-Startskripte mit freier Portzuweisung aus.</summary>
public sealed class RepositoryStartskriptService
{
    private const string PowershellExecutable = "powershell.exe";

    private readonly ICliRunner _cliRunner;
    private readonly ILogger<RepositoryStartskriptService> _logger;

    /// <inheritdoc cref="RepositoryStartskriptService"/>
    public RepositoryStartskriptService(
        ICliRunner cliRunner,
        ILogger<RepositoryStartskriptService> logger)
    {
        _cliRunner = cliRunner;
        _logger = logger;
    }

    /// <summary>Führt das konfigurierte Startskript für ein Repository aus.</summary>
    public async Task RunAsync(string repositoryRootPath, RepositoryStartKonfiguration configuration, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRootPath);
        ArgumentNullException.ThrowIfNull(configuration);

        if (!configuration.Aktiv)
        {
            _logger.LogInformation("Repository-Startskript ist deaktiviert.");
            return;
        }

        var scriptPath = ResolveScriptPath(repositoryRootPath, configuration.StartScriptRelativePath);
        if (!File.Exists(scriptPath))
        {
            throw new InvalidOperationException($"Das Startskript wurde nicht gefunden: {configuration.StartScriptRelativePath}");
        }

        var args = BuildArguments(scriptPath);
        _logger.LogInformation("Starte Repository-Skript '{Script}' für Repository '{RepositoryRootPath}'.", configuration.StartScriptRelativePath, repositoryRootPath);

        var result = await _cliRunner.RunAsync(
            PowershellExecutable,
            args,
            repositoryRootPath,
            null,
            ct).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            throw new InvalidOperationException(
                $"Das Repository-Startskript '{configuration.StartScriptRelativePath}' ist fehlgeschlagen: {result.StdErr ?? result.StdOut}");
        }
    }

    private static string ResolveScriptPath(string repositoryRootPath, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            throw new InvalidOperationException("Für die Repository-Startkonfiguration ist ein Skriptpfad erforderlich.");
        }

        var combined = Path.GetFullPath(Path.Combine(repositoryRootPath, relativePath));
        var normalizedRoot = Path.GetFullPath(repositoryRootPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        if (!combined.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Das Startskript muss innerhalb des Repositorys liegen.");
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
