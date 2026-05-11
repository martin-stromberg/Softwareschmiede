using Microsoft.Extensions.Logging;
using Softwareschmiede.Domain.Interfaces;

namespace Softwareschmiede.Application.Services;

/// <summary>
/// Reagiert auf Running-Count-Übergänge und fordert bei aktivem Toggle genau einmal
/// pro Übergang von >0 auf 0 einen Shutdown an.
/// </summary>
public sealed class AutoShutdownOrchestrator : IAutoShutdownOrchestrator, IDisposable
{
    private readonly IRunningAutomationStatusSource _runningAutomationStatusSource;
    private readonly ISystemShutdownService _systemShutdownService;
    private readonly ILogger<AutoShutdownOrchestrator> _logger;
    private readonly Lock _syncLock = new();

    private bool _autoShutdownEnabled;
    private bool _shutdownRequestedForCurrentZeroTransition;

    public AutoShutdownOrchestrator(
        IRunningAutomationStatusSource runningAutomationStatusSource,
        ISystemShutdownService systemShutdownService,
        ILogger<AutoShutdownOrchestrator> logger)
    {
        _runningAutomationStatusSource = runningAutomationStatusSource;
        _systemShutdownService = systemShutdownService;
        _logger = logger;

        _runningAutomationStatusSource.RunningCountChanged += OnRunningCountChanged;
    }

    /// <inheritdoc />
    public void SetEnabled(bool enabled)
    {
        lock (_syncLock)
        {
            _autoShutdownEnabled = enabled;
        }
    }

    private void OnRunningCountChanged(int previousCount, int currentCount)
    {
        bool shouldRequestShutdown;
        lock (_syncLock)
        {
            if (currentCount > 0)
            {
                _shutdownRequestedForCurrentZeroTransition = false;
                return;
            }

            shouldRequestShutdown =
                _autoShutdownEnabled
                && previousCount > 0
                && currentCount == 0
                && !_shutdownRequestedForCurrentZeroTransition;

            if (shouldRequestShutdown)
            {
                _shutdownRequestedForCurrentZeroTransition = true;
            }
        }

        if (!shouldRequestShutdown)
        {
            return;
        }

        _ = TryRequestShutdownAsync();
    }

    private async Task TryRequestShutdownAsync()
    {
        try
        {
            var currentCount = _runningAutomationStatusSource.GetRunningCount();
            if (currentCount > 0)
            {
                lock (_syncLock)
                {
                    _shutdownRequestedForCurrentZeroTransition = false;
                }

                _logger.LogInformation(
                    "Auto-Shutdown verworfen, da vor Final-Recheck wieder Läufe aktiv sind (Count: {RunningCount}).",
                    currentCount);
                return;
            }

            lock (_syncLock)
            {
                if (!_autoShutdownEnabled)
                {
                    _shutdownRequestedForCurrentZeroTransition = false;
                    return;
                }
            }

            _logger.LogWarning("Auto-Shutdown wird ausgelöst (letzter laufender Prozess abgeschlossen).");
            await _systemShutdownService.RequestShutdownAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Auto-Shutdown konnte nicht ausgeführt werden.");
        }
    }

    public void Dispose()
    {
        _runningAutomationStatusSource.RunningCountChanged -= OnRunningCountChanged;
    }
}
