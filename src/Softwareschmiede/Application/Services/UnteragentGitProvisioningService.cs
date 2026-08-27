using Microsoft.Extensions.Logging;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Interfaces;

namespace Softwareschmiede.Application.Services;

/// <summary>Übernimmt die Git-/Verzeichnis-Provisionierung eines Unteragenten: Arbeitsverzeichnis anlegen, Feature-Branch erstellen und den Klon anlegen.</summary>
public sealed class UnteragentGitProvisioningService
{
    private readonly ICliRunner _cliRunner;
    private readonly IGitPlugin _gitPlugin;
    private readonly ILogger<UnteragentGitProvisioningService> _logger;

    /// <inheritdoc cref="UnteragentGitProvisioningService"/>
    public UnteragentGitProvisioningService(ICliRunner cliRunner, IGitPlugin gitPlugin, ILogger<UnteragentGitProvisioningService> logger)
    {
        _cliRunner = cliRunner;
        _gitPlugin = gitPlugin;
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

        // ErstelleLokalenBranchAsync löst repoMainPfad intern bereits über
        // IGitPlugin.ResolveEffectiveRepositoryPathAsync auf (manche Plugins, z. B. LocalDirectoryPlugin im
        // InSourceDirectory-Modus, legen dort nur eine Pointer-Datei ab, statt dort tatsächlich zu klonen) und
        // gibt den aufgelösten Pfad zurück, damit er hier für den Klon wiederverwendet werden kann, statt ihn
        // ein zweites Mal aufzulösen.
        var effektiverRepoMainPfad = await GitBranchHelper.ErstelleLokalenBranchAsync(
            _cliRunner,
            _gitPlugin,
            repoMainPfad,
            unteragent.GitArbeitsbereich.BranchName,
            _logger,
            $"Branch '{unteragent.GitArbeitsbereich.BranchName}' für Unteragent '{unteragent.ExterneAgentId}' konnte nicht angelegt werden",
            ct);

        await GitKlonHelper.KloneFallsNichtVorhandenAsync(
            _cliRunner,
            effektiverRepoMainPfad,
            unteragent.GitArbeitsbereich.ClonePfad,
            unteragent.GitArbeitsbereich.BranchName,
            _logger,
            $"Klon für Unteragent '{unteragent.ExterneAgentId}' fehlgeschlagen",
            ct);
    }
}
