using System.Threading;
using System.Threading.Tasks;
using Softwareschmiede.Application.Services.Updates;

namespace Softwareschmiede.App.Services.Testing;

/// <summary>
/// Beobachtender <see cref="IUpdateService"/>-Dekorator der Update-E2E-Umgebung. Leitet alle Aufrufe
/// an den echten <see cref="UpdateService"/> weiter und schreibt Prüfergebnis, Vorbereitungsphasen,
/// Vorbereitungsabschluss und Startversuch/-erfolg ins JSONL-Protokoll, ohne Produktionsentscheidungen
/// zu verändern.
/// </summary>
public sealed class ProtokollierenderUpdateService : IUpdateService
{
    private readonly IUpdateService _inner;
    private readonly UpdateE2ETestKontext _kontext;

    /// <inheritdoc cref="ProtokollierenderUpdateService"/>
    /// <param name="inner">Der echte Update-Service.</param>
    /// <param name="kontext">Der Laufzeitkontext der Update-E2E-Umgebung.</param>
    public ProtokollierenderUpdateService(IUpdateService inner, UpdateE2ETestKontext kontext)
    {
        _inner = inner;
        _kontext = kontext;
    }

    /// <inheritdoc/>
    public async Task<UpdateCheckResult> CheckForUpdateAsync(UpdateCheckOptions options, CancellationToken ct = default)
    {
        _kontext.Protokoll.Schreibe(
            UpdateE2EEreignisse.UpdateCheckStarted,
            new { includePrereleases = options.IncludePrereleases });

        var ergebnis = await _inner.CheckForUpdateAsync(options, ct);

        _kontext.Protokoll.Schreibe(
            UpdateE2EEreignisse.UpdateCheckCompleted,
            new
            {
                status = ergebnis.Status.ToString(),
                version = ergebnis.Update?.Version,
                isPrerelease = ergebnis.Update?.IsPrerelease,
                message = ergebnis.Message
            });

        return ergebnis;
    }

    /// <inheritdoc/>
    public async Task<UpdatePreparationResult> PrepareUpdateAsync(
        UpdateInfo update,
        IProgress<UpdatePreparationProgress>? progress,
        CancellationToken ct = default)
    {
        // Synchroner IProgress-Passthrough statt Progress<T>: Progress<T> würde den Report
        // erneut auf den erfassten SynchronizationContext posten. Der doppelte Dispatcher-Hop
        // lieferte die Fortschrittsmeldung erst NACH einem fehlschlagenden await (SetError) zu
        // und könnte den Terminalzustand des Fortschrittsdialogs überschreiben - der Beobachter
        // würde so das zu beobachtende Verhalten verändern.
        var protokollFortschritt = new ProtokollierenderFortschritt(progress, _kontext);

        try
        {
            var ergebnis = await _inner.PrepareUpdateAsync(update, protokollFortschritt, ct);
            _kontext.Protokoll.Schreibe(
                UpdateE2EEreignisse.PreparationCompleted,
                new
                {
                    zipPath = ergebnis.ZipPath,
                    extractedDirectory = ergebnis.ExtractedDirectory,
                    scriptPath = ergebnis.ScriptPath,
                    requiresElevation = ergebnis.RequiresElevation
                });
            return ergebnis;
        }
        catch (Exception ex)
        {
            _kontext.Protokoll.Schreibe(UpdateE2EEreignisse.PreparationFailed, new { error = ex.Message });
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task StartPreparedUpdateAsync(UpdatePreparationResult preparation, CancellationToken ct = default)
    {
        _kontext.Protokoll.Schreibe(
            UpdateE2EEreignisse.UpdateStartAttempt,
            new { scriptPath = preparation.ScriptPath, requiresElevation = preparation.RequiresElevation });

        try
        {
            await _inner.StartPreparedUpdateAsync(preparation, ct);
            _kontext.Protokoll.Schreibe(UpdateE2EEreignisse.UpdateStartSucceeded);
        }
        catch (Exception ex)
        {
            _kontext.Protokoll.Schreibe(UpdateE2EEreignisse.UpdateStartFailed, new { error = ex.Message });
            throw;
        }
    }

    /// <summary>
    /// Synchroner <see cref="IProgress{T}"/>-Passthrough: leitet den Report sofort an den
    /// Aufrufer-Fortschritt weiter (dessen eigene Zustellung übernimmt <see cref="Progress{T}"/>)
    /// und schreibt anschließend das Phasen-Ereignis ins Protokoll. <c>Protokoll.Schreibe</c> ist
    /// thread-sicher und darf daher vom aufrufenden Thread aus erfolgen.
    /// </summary>
    private sealed class ProtokollierenderFortschritt(
        IProgress<UpdatePreparationProgress>? inner,
        UpdateE2ETestKontext kontext) : IProgress<UpdatePreparationProgress>
    {
        /// <inheritdoc/>
        public void Report(UpdatePreparationProgress value)
        {
            inner?.Report(value);
            kontext.Protokoll.Schreibe(
                UpdateE2EEreignisse.PreparationPhase,
                new { phase = value.Phase.ToString(), percent = value.Percent, message = value.Message });
        }
    }
}
