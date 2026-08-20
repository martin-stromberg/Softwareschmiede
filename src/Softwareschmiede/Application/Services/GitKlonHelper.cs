using Microsoft.Extensions.Logging;
using Softwareschmiede.Domain.Interfaces;

namespace Softwareschmiede.Application.Services;

/// <summary>Gemeinsame Hilfsmethode zum Klonen eines Git-Repositories, sofern das Zielverzeichnis noch nicht existiert oder leer ist.</summary>
internal static class GitKlonHelper
{
    /// <summary>Klont <paramref name="quellPfad"/> nach <paramref name="zielPfad"/> (optional auf einen bestimmten Branch), sofern das Zielverzeichnis nicht bereits einen Klon enthält.</summary>
    public static async Task KloneFallsNichtVorhandenAsync(
        ICliRunner cliRunner,
        string quellPfad,
        string zielPfad,
        string? branch,
        ILogger logger,
        string fehlerKontext,
        CancellationToken ct)
    {
        if (Directory.Exists(zielPfad) && Directory.EnumerateFileSystemEntries(zielPfad).Any())
        {
            logger.LogInformation("Klon existiert bereits unter {ZielPfad}, überspringe Klonvorgang.", zielPfad);
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(zielPfad)!);

        string[] argumente = branch is null
            ? ["clone", quellPfad, zielPfad]
            : ["clone", "--branch", branch, quellPfad, zielPfad];

        var ergebnis = await cliRunner.RunAsync("git", argumente, null, null, ct);
        if (!ergebnis.IsSuccess)
        {
            throw new InvalidOperationException($"{fehlerKontext}: {ergebnis.StdErr}");
        }
    }
}
