using Microsoft.Extensions.Logging;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Interfaces;

namespace Softwareschmiede.Application.Services;

/// <summary>Führt Repository-Startskripte mit freier Portzuweisung aus.</summary>
public sealed class RepositoryStartskriptService
{
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

        await RepositoryScriptExecutor.RunAsync(
            repositoryRootPath,
            configuration.Aktiv,
            configuration.StartScriptRelativePath,
            "Startskript",
            _cliRunner,
            _logger,
            ct).ConfigureAwait(false);
    }
}
