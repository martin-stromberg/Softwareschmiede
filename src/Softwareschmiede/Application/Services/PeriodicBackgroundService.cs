using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Softwareschmiede.Application.Services;

/// <summary>Basisklasse für BackgroundServices mit periodischem Polling: fuehrt <see cref="RunOnceAsync"/> in einer Schleife aus, faengt Fehler pro Durchlauf ab und wartet danach das Polling-Intervall.</summary>
public abstract class PeriodicBackgroundService : BackgroundService
{
    private readonly TimeSpan _pollingInterval;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger _logger;
    private readonly string _errorMessage;

    /// <inheritdoc cref="PeriodicBackgroundService"/>
    /// <param name="pollingInterval">Wartezeit zwischen zwei Durchlaeufen.</param>
    /// <param name="timeProvider">Zeitquelle für das Polling-Delay (testbar via <see cref="TimeProvider"/>).</param>
    /// <param name="logger">Logger für Fehlermeldungen bei fehlgeschlagenen Durchlaeufen.</param>
    /// <param name="errorMessage">Log-Meldung, die bei einem fehlgeschlagenen Durchlauf protokolliert wird.</param>
    protected PeriodicBackgroundService(TimeSpan pollingInterval, TimeProvider timeProvider, ILogger logger, string errorMessage)
    {
        _pollingInterval = pollingInterval;
        _timeProvider = timeProvider;
        _logger = logger;
        _errorMessage = errorMessage;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, _errorMessage);
            }

            try
            {
                await Task.Delay(_pollingInterval, _timeProvider, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
        }
    }

    /// <summary>Fuehrt einen einzelnen Monitoring-Durchlauf aus.</summary>
    /// <param name="ct">Abbruchtoken für den Durchlauf.</param>
    public abstract Task RunOnceAsync(CancellationToken ct = default);
}
