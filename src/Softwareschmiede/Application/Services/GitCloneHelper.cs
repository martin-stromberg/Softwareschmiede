using Microsoft.Extensions.Logging;
using Softwareschmiede.Domain.Interfaces;

namespace Softwareschmiede.Application.Services;

/// <summary>Gemeinsame Hilfsmethode zum Klonen eines Git-Repositories, sofern das Zielverzeichnis noch nicht existiert oder leer ist.</summary>
internal static class GitCloneHelper
{
    /// <summary>Klont <paramref name="sourcePath"/> nach <paramref name="targetPath"/> (optional auf einen bestimmten Branch), sofern das Zielverzeichnis nicht bereits einen Klon enthält.</summary>
    public static async Task CloneIfMissingAsync(
        ICliRunner cliRunner,
        string sourcePath,
        string targetPath,
        string? branch,
        ILogger logger,
        string errorContext,
        CancellationToken ct)
    {
        if (Directory.Exists(targetPath) && Directory.EnumerateFileSystemEntries(targetPath).Any())
        {
            logger.LogInformation("Klon existiert bereits unter {ZielPfad}, überspringe Klonvorgang.", targetPath);
            return;
        }

        var targetParentPath = Path.GetDirectoryName(targetPath);
        if (targetParentPath is not null)
        {
            Directory.CreateDirectory(targetParentPath);
        }

        string[] arguments = branch is null
            ? ["clone", sourcePath, targetPath]
            : ["clone", "--branch", branch, sourcePath, targetPath];

        var result = await cliRunner.RunAsync("git", arguments, null, null, ct);
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException($"{errorContext}: {result.StdErr}");
        }
    }
}
