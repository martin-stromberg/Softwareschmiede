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
        var protokollFortschritt = new Progress<UpdatePreparationProgress>(p =>
        {
            progress?.Report(p);
            _kontext.Protokoll.Schreibe(
                UpdateE2EEreignisse.PreparationPhase,
                new { phase = p.Phase.ToString(), percent = p.Percent, message = p.Message });
        });

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
}
