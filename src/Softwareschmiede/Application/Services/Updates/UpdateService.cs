using Microsoft.Extensions.Logging;

namespace Softwareschmiede.Application.Services.Updates;

/// <summary>Orchestriert Update-Prüfung, Vorbereitung und Start des externen Updaters.</summary>
public sealed class UpdateService : IUpdateService
{
    private readonly IApplicationVersionProvider _versionProvider;
    private readonly IUpdateReleaseClient _releaseClient;
    private readonly IUpdatePackageService _packageService;
    private readonly IUpdateScriptService _scriptService;
    private readonly IApplicationShutdownService _shutdownService;
    private readonly ILogger<UpdateService> _logger;
    private readonly SemaphoreSlim _checkGate = new(1, 1);
    private UpdateCheckResult? _cachedResult;

    /// <inheritdoc cref="UpdateService"/>
    public UpdateService(
        IApplicationVersionProvider versionProvider,
        IUpdateReleaseClient releaseClient,
        IUpdatePackageService packageService,
        IUpdateScriptService scriptService,
        IApplicationShutdownService shutdownService,
        ILogger<UpdateService> logger)
    {
        _versionProvider = versionProvider;
        _releaseClient = releaseClient;
        _packageService = packageService;
        _scriptService = scriptService;
        _shutdownService = shutdownService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<UpdateCheckResult> CheckForUpdateAsync(CancellationToken ct = default)
    {
        await _checkGate.WaitAsync(ct);
        try
        {
            var installed = await _versionProvider.GetInstalledVersionAsync(ct);
            if (installed is null)
            {
                _cachedResult = UpdateCheckResult.NichtPruefbar("Lokale Version ist nicht prüfbar.");
                return _cachedResult;
            }

            var latest = await _releaseClient.GetLatestStableReleaseAsync(ct);
            if (latest is null)
            {
                _cachedResult = UpdateCheckResult.NichtPruefbar("GitHub-Release ist nicht prüfbar.");
                return _cachedResult;
            }

            _cachedResult = UpdateVersionComparer.IsNewer(installed.Version, latest.Version)
                ? UpdateCheckResult.UpdateVerfuegbar(latest)
                : UpdateCheckResult.KeinUpdate();

            return _cachedResult;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Update-Prüfung ist fehlgeschlagen.");
            _cachedResult = UpdateCheckResult.NichtPruefbar("Update-Prüfung ist fehlgeschlagen.");
            return _cachedResult;
        }
        finally
        {
            _checkGate.Release();
        }
    }

    /// <inheritdoc/>
    public Task<UpdatePreparationResult> PrepareUpdateAsync(
        UpdateInfo update,
        IProgress<UpdatePreparationProgress>? progress,
        CancellationToken ct = default)
        => _packageService.PreparePackageAsync(update, progress, ct);

    /// <inheritdoc/>
    public async Task StartPreparedUpdateAsync(UpdatePreparationResult preparation, CancellationToken ct = default)
    {
        await _scriptService.StartScriptAsync(preparation, ct);
        _shutdownService.Shutdown();
    }

    /// <summary>Gibt das zuletzt gecachte Prüfergebnis zurück.</summary>
    public UpdateCheckResult? CachedResult => _cachedResult;
}
