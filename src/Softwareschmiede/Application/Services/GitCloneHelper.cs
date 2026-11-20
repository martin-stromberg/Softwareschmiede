using Microsoft.Extensions.Logging;
using Softwareschmiede.Domain.Interfaces;

namespace Softwareschmiede.Application.Services;

/// <summary>Gemeinsame Hilfsmethode zum Klonen eines Git-Repositories, sofern das Zielverzeichnis noch nicht existiert oder leer ist.</summary>
internal static class GitCloneHelper
{
    /// <summary>Klont <paramref name="quellPfad"/> nach <paramref name="targetPath"/> (optional auf einen bestimmten Branch), sofern das Zielverzeichnis nicht bereits einen Klon enthält.</summary>
    public static async Task CloneIfMissingAsync(
        ICliRunner cliRunner,
        string quellPfad,
        string targetPath,
        string? branch,
        ILogger logger,
        string fehlerKontext,
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

        string[] argumente = branch is null
            ? ["clone", quellPfad, targetPath]
            : ["clone", "--branch", branch, quellPfad, targetPath];

        var result = await cliRunner.RunAsync("git", argumente, null, null, ct);
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException($"{fehlerKontext}: {result.StdErr}");
        }
    }
}
