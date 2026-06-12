using System.Windows;
using System.Windows.Media;
using Microsoft.Extensions.Logging;
using Softwareschmiede.Domain.Interfaces;

namespace Softwareschmiede.App.Services;

/// <summary>
/// WPF-Implementierung von <see cref="IBenachrichtigungsAudioService"/>.
/// Verwendet <see cref="MediaPlayer"/> für die Audiowiedergabe (MP3, WAV – ohne NuGet).
/// </summary>
public sealed class WpfAudioService : IBenachrichtigungsAudioService
{
    private readonly ILogger<WpfAudioService> _logger;
    private readonly HashSet<MediaPlayer> _activePlayers = new();

    /// <inheritdoc cref="WpfAudioService"/>
    public WpfAudioService(ILogger<WpfAudioService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task PlayAudioAsync(string filePath, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.HasShutdownStarted)
        {
            _logger.LogDebug("Dispatcher nicht verfügbar oder wird beendet – Audio übersprungen: {FilePath}", filePath);
            return;
        }

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var reg = ct.Register(() => tcs.TrySetCanceled(ct));

        var dispatcherOp = dispatcher.InvokeAsync(() =>
        {
            try
            {
                var player = new MediaPlayer();
                _activePlayers.Add(player);

                player.MediaEnded += (_, _) =>
                {
                    _activePlayers.Remove(player);
                    player.Close();
                    tcs.TrySetResult(true);
                };
                player.MediaFailed += (_, args) =>
                {
                    _activePlayers.Remove(player);
                    _logger.LogError("MediaPlayer-Fehler beim Abspielen von '{FilePath}': {Error}", filePath, args.ErrorException.Message);
                    player.Close();
                    tcs.TrySetException(args.ErrorException);
                };

                player.Open(new Uri(filePath, UriKind.Absolute));
                player.Play();

                _logger.LogDebug("Audiodatei '{FilePath}' wird abgespielt.", filePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Initialisieren des MediaPlayer für '{FilePath}'.", filePath);
                tcs.TrySetException(ex);
            }
        });

        if (dispatcherOp.Status == System.Windows.Threading.DispatcherOperationStatus.Aborted)
        {
            _logger.LogDebug("Dispatcher-Operation wurde abgebrochen – Audio übersprungen: {FilePath}", filePath);
            tcs.TrySetCanceled(ct);
            return;
        }

        dispatcherOp.Aborted += (_, _) => tcs.TrySetCanceled();

        await tcs.Task;
    }
}
