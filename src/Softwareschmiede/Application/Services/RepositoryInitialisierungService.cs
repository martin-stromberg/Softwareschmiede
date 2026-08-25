using Microsoft.Extensions.Logging;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Interfaces;

namespace Softwareschmiede.Application.Services;

/// <summary>Führt Repository-Initialisierungsskripte nach dem Klonen aus. Fehler werden geloggt, nicht geworfen.</summary>
public sealed class RepositoryInitialisierungService
{
    private readonly ICliRunner _cliRunner;
    private readonly ILogger<RepositoryInitialisierungService> _logger;

    /// <inheritdoc cref="RepositoryInitialisierungService"/>
    public RepositoryInitialisierungService(
        ICliRunner cliRunner,
        ILogger<RepositoryInitialisierungService> logger)
    {
        _cliRunner = cliRunner;
        _logger = logger;
    }

    /// <summary>Führt das konfigurierte Initialisierungsskript für ein Repository aus.</summary>
    public async Task RunAsync(string repositoryRootPath, RepositoryInitialisierungKonfiguration configuration, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRootPath);
        ArgumentNullException.ThrowIfNull(configuration);

        await RepositoryScriptExecutor.RunAsync(
            repositoryRootPath,
            configuration.Aktiv,
            configuration.InitialisierungsskriptRelativePath,
            "Initialisierungsskript",
            _cliRunner,
            _logger,
            ct).ConfigureAwait(false);
    }
}
