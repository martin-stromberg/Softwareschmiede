using Microsoft.Extensions.Logging;
using Softwareschmiede.Domain.Interfaces;

namespace Softwareschmiede.Application.Services;

/// <summary>
/// Gemeinsame Hilfsmethode zum Anlegen eines lokalen Git-Branches im tatsächlich aufgelösten Repository-Pfad,
/// <b>ohne</b> das Arbeitsverzeichnis des Repositories auf den neuen Branch umzuschalten (nur <c>git branch</c>,
/// kein <c>checkout</c>). Wird ausschließlich für Unteragenten-Branches verwendet (siehe
/// <c>UnteragentGitProvisioningService</c>), da dort mehrere Unteragenten nacheinander denselben, gemeinsam
/// genutzten <c>repo_main</c>-Pfad verwenden und ein Checkout dort zu einem Wettlauf um den Checkout-Status
/// führen würde. Für den Projektbranch (der <c>repo_main</c> tatsächlich als Basis etablieren soll) wird
/// stattdessen direkt <see cref="IGitPlugin.CreateBranchAsync"/> verwendet (siehe
/// <c>AutonomAufgabenInitialisierungsService.ErstelleProjektbranchAsync</c>).
/// </summary>
internal static class GitBranchHelper
{
    /// <summary>
    /// Löst den tatsächlichen Repository-Pfad unter <paramref name="repoPfad"/> auf (manche Plugins, z. B.
    /// <c>LocalDirectoryPlugin</c> im InSourceDirectory-Modus, legen dort nur eine Pointer-Datei ab, statt
    /// tatsächlich zu klonen; siehe <see cref="IGitPlugin.ResolveEffectiveRepositoryPathAsync"/>) und legt darin
    /// per <c>git branch</c> über <paramref name="cliRunner"/> den angegebenen lokalen Branch an.
    /// </summary>
    /// <param name="cliRunner">Führt den rohen <c>git branch</c>-Befehl aus.</param>
    /// <param name="gitPlugin">Löst den tatsächlichen Repository-Pfad auf.</param>
    /// <param name="repoPfad">Der (möglicherweise nur logische) Pfad zum Repository.</param>
    /// <param name="branchName">Der anzulegende lokale Branch-Name.</param>
    /// <param name="logger">Logger für Diagnosemeldungen.</param>
    /// <param name="fehlerKontext">Kontexttext, dem bei einem Fehler die Git-Fehlermeldung angehängt wird.</param>
    /// <param name="ct">Abbruch-Token.</param>
    /// <returns>
    /// Den bereits aufgelösten, tatsächlichen Repository-Pfad (siehe <see cref="IGitPlugin.ResolveEffectiveRepositoryPathAsync"/>),
    /// damit Aufrufer ihn für nachfolgende Operationen (z. B. einen Klon) wiederverwenden können, statt ihn erneut aufzulösen.
    /// </returns>
    /// <exception cref="InvalidOperationException">Wenn <c>git branch</c> fehlschlägt.</exception>
    public static async Task<string> ErstelleLokalenBranchAsync(
        ICliRunner cliRunner,
        IGitPlugin gitPlugin,
        string repoPfad,
        string branchName,
        ILogger logger,
        string fehlerKontext,
        CancellationToken ct)
    {
        var effektiverRepoPfad = await gitPlugin.ResolveEffectiveRepositoryPathAsync(repoPfad, ct);
        logger.LogDebug("Lege lokalen Branch {BranchName} in {RepoPfad} an.", branchName, effektiverRepoPfad);

        var ergebnis = await cliRunner.RunAsync("git", ["branch", branchName], effektiverRepoPfad, null, ct);
        if (!ergebnis.IsSuccess)
        {
            throw new InvalidOperationException($"{fehlerKontext}: {ergebnis.StdErr}");
        }

        return effektiverRepoPfad;
    }
}
