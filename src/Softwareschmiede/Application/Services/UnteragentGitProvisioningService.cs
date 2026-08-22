using Microsoft.Extensions.Logging;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Interfaces;

namespace Softwareschmiede.Application.Services;

/// <summary>Übernimmt die Git-/Verzeichnis-Provisionierung eines Unteragenten: Arbeitsverzeichnis anlegen, Feature-Branch erstellen und den Klon anlegen.</summary>
public sealed class UnteragentGitProvisioningService
{
    private readonly ICliRunner _cliRunner;
    private readonly ILogger<UnteragentGitProvisioningService> _logger;

    /// <inheritdoc cref="UnteragentGitProvisioningService"/>
    public UnteragentGitProvisioningService(ICliRunner cliRunner, ILogger<UnteragentGitProvisioningService> logger)
    {
        _cliRunner = cliRunner;
        _logger = logger;
    }

    /// <summary>Erstellt das Arbeitsverzeichnis des Unteragenten, legt seinen Feature-Branch im Hauptrepository an und klont diesen Branch in den Arbeitsbereich des Unteragenten.</summary>
    /// <param name="unteragent">Der Unteragent, für den provisioniert wird.</param>
    /// <param name="repoMainPfad">Pfad zum Hauptrepository (Klon-Quelle), aus dem der Branch angelegt und geklont wird.</param>
    /// <param name="ct">Abbruch-Token.</param>
    public async Task ProvisioniereAsync(UnteragentSpezifikation unteragent, string repoMainPfad, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(unteragent);

        await DirectoryAccessGuard.AusfuehrenAsync(unteragent.VerzeichnisPfad, () =>
        {
            Directory.CreateDirectory(unteragent.VerzeichnisPfad);
            return Task.CompletedTask;
        });

        var branchErgebnis = await _cliRunner.RunAsync("git", ["branch", unteragent.GitArbeitsbereich.BranchName], repoMainPfad, null, ct);
        if (!branchErgebnis.IsSuccess)
        {
            throw new InvalidOperationException($"Branch '{unteragent.GitArbeitsbereich.BranchName}' für Unteragent '{unteragent.ExterneAgentId}' konnte nicht angelegt werden: {branchErgebnis.StdErr}");
        }

        await GitKlonHelper.KloneFallsNichtVorhandenAsync(
            _cliRunner,
            repoMainPfad,
            unteragent.GitArbeitsbereich.ClonePfad,
            unteragent.GitArbeitsbereich.BranchName,
            _logger,
            $"Klon für Unteragent '{unteragent.ExterneAgentId}' fehlgeschlagen",
            ct);
    }
}
