using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Softwareschmiede.Application.Services;

namespace Softwareschmiede.App.Services.Testing;

/// <summary>
/// EF-Core-Interceptor, der ausschließlich mit dem Abfragetag <see cref="AppSettingService.UpdateSettingsReadTag"/>
/// markierte Lesebefehle der Update--Einstellungen gezielt mit einer kontrollierten Exception fehlschlagen lässt.
/// Alle anderen Lese- und Schreibbefehle bleiben unberührt.
/// </summary>
/// <remarks>
/// Zwei Steuerungsmodi: Ohne <see cref="UpdateE2ETestContext"/> (Komponententests) steuern
/// <see cref="ActivateFailure"/>/<see cref="DeactivateFailure"/> den Fehler global. Mit Kontext
/// (E2E-App-Prozess) wird die Szenariodatei pro markiertem Read neu gelesen; der Fehler greift nur
/// auf Reads innerhalb eines laufenden Updateversuchs ab der konfigurierten Lesegrenze
/// (<c>Initial</c>, <c>BeforePreparation</c>, <c>BeforeUpdaterStart</c>) und bleibt aktiv, bis das
/// Szenario ihn ausdrücklich deaktiviert. Optional wartet der betroffene Read begrenzt auf die
/// Freigabe-Datei <c>settingsReadFailure.release</c> bzw. den Abbruch über <c>settingsReadFailure.cancel</c>.
/// </remarks>
public sealed class UpdateSettingsReadFailureInterceptor : DbCommandInterceptor
{
    private volatile bool _failureEnabled;
    private int _settingsReadCount;
    private readonly UpdateE2ETestContext? _context;

    /// <inheritdoc cref="UpdateSettingsReadFailureInterceptor"/>
    public UpdateSettingsReadFailureInterceptor()
    {
    }

    /// <inheritdoc cref="UpdateSettingsReadFailureInterceptor"/>
    /// <param name="context">Der Laufzeitkontext der Update-E2E-Umgebung.</param>
    public UpdateSettingsReadFailureInterceptor(UpdateE2ETestContext context)
    {
        _context = context;
    }

    /// <summary>Wird ausgelöst, sobald ein markierter Update--Einstellungen-Lesebefehl erreicht wird (vor einem etwaigen Fehler).</summary>
    public event EventHandler? SettingsReadReached;

    /// <summary>Anzahl der bisher beobachteten markierten Update--Einstellungen-Lesebefehle.</summary>
    public int SettingsReadCount => _settingsReadCount;

    /// <summary>Gibt an, ob markierte Update--Einstellungen-Lesebefehle aktuell kontrolliert fehlschlagen.</summary>
    public bool FailureEnabled => _failureEnabled;

    /// <summary>Die kontrollierte Exception, die bei aktiviertem Fehler für markierte Lesebefehle geworfen wird.</summary>
    public Exception FailureException { get; init; } = new InvalidOperationException("Kontrollierter Testfehler beim Lesen der Update--Einstellungen.");

    /// <summary>Aktiviert den kontrollierten Lesefehler für markierte Update--Einstellungen-Abfragen.</summary>
    public void ActivateFailure() => _failureEnabled = true;

    /// <summary>Deaktiviert den kontrollierten Lesefehler wieder; markierte Abfragen laufen danach normal.</summary>
    public void DeactivateFailure() => _failureEnabled = false;

    /// <inheritdoc/>
    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result)
    {
        if (CheckMarkedRead(command) is { } failureCase)
            WaitAndThrow(failureCase, CancellationToken.None);

        return result;
    }

    /// <inheritdoc/>
    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        // Die Gate-Wartezeit muss asynchron ablaufen: Markierte Reads laufen im Update-Flow auf dem
        // UI-Thread, ein synchrones Blockieren würde die Oberfläche (und damit FlaUI-Zugriffe wie
        // GetMainWindow) für die gesamte Gate-Dauer einfrieren.
        if (CheckMarkedRead(command) is { } failureCase)
            await WaitAndThrowAsync(failureCase, cancellationToken);

        return result;
    }

    /// <summary>Ein erkannter, anzuwendender Lesefehler-Fall inklusive des Read-Zeitpunkts im Versuch.</summary>
    private sealed record FailureCase(UpdateE2EReadFailure ReadFailure, string? Attempt, int AttemptIndex, int Ordinal);

    /// <summary>
    /// Prüft einen Lesebefehl gegen den Abfragetag und die Szenariokonfiguration. Liefert den
    /// anzuwendenden Fehlerfall oder <c>null</c>, wenn der Read ungehindert laufen darf.
    /// Protokolliert jeden markierten Read als <see cref="UpdateE2EEvents.UpdateSettingsReadReached"/>.
    /// </summary>
    private FailureCase? CheckMarkedRead(DbCommand command)
    {
        if (!command.CommandText.Contains(AppSettingService.UpdateSettingsReadTag, StringComparison.Ordinal))
            return null;

        Interlocked.Increment(ref _settingsReadCount);
        SettingsReadReached?.Invoke(this, EventArgs.Empty);

        if (_context is null)
        {
            if (_failureEnabled)
                throw FailureException;
            return null;
        }

        // Ordinalzählung und Versuchszuordnung liegen im Singleton-Kontext, damit sie
        // unabhängig davon gelten, ob derselbe Interceptor oder eine neue Instanz je
        // Scoped-DbContext läuft - instanzbezogene Zähler könnten Reads verscopen.
        var ordinal = _context.NextMarkedReadOrdinal();
        var attempt = _context.CurrentAttempt;
        var attemptIndex = _context.CurrentAttemptIndex;
        var readFailure = _context.ReadScenario()?.ReadFailure;

        _context.Log.Write(
            UpdateE2EEvents.UpdateSettingsReadReached,
            new
            {
                versuch = attempt,
                versuchIndex = attemptIndex,
                ordinal,
                grenze = readFailure?.Limit,
                fehlerAktiv = readFailure?.Active == true || _failureEnabled
            });

        if (_failureEnabled)
            throw FailureException;

        if (readFailure?.Active != true
            || attempt is null
            || !readFailure.MatchesAttempt(attempt)
            || ordinal < readFailure.BoundaryOrdinal())
        {
            return null;
        }

        return new FailureCase(readFailure, attempt, attemptIndex, ordinal);
    }

    private void WaitAndThrow(FailureCase failureCase, CancellationToken cancellationToken)
    {
        var reason = "sofort";
        if (failureCase.ReadFailure.WaitForRelease)
        {
            var released = _context!.WaitOnGate(
                UpdateE2ETestContext.ReadFailureGateName, cancellationToken, WaitTimeout(failureCase));
            reason = ResolveWaitReason(released);
        }

        ThrowLogErrorAnd(failureCase, reason);
    }

    private async Task WaitAndThrowAsync(FailureCase failureCase, CancellationToken cancellationToken)
    {
        var reason = "sofort";
        if (failureCase.ReadFailure.WaitForRelease)
        {
            bool released;
            try
            {
                released = await _context!.WaitOnGateAsync(
                    UpdateE2ETestContext.ReadFailureGateName, cancellationToken, WaitTimeout(failureCase));
            }
            catch (OperationCanceledException)
            {
                released = false;
            }
            reason = ResolveWaitReason(released);
        }

        ThrowLogErrorAnd(failureCase, reason);
    }

    /// <summary>Die konfigurierte Wartezeit des Lesefehlers, mindestens eine Sekunde.</summary>
    private static TimeSpan WaitTimeout(FailureCase failureCase)
        => TimeSpan.FromSeconds(Math.Max(1, failureCase.ReadFailure.WaitTimeoutSeconds));

    /// <summary>
    /// WarteAufGate liefert bei Abbruch und Timeout beide <c>false</c> - die Abbruch-Datei
    /// entscheidet, ob der Aufrufer das Gate explizit abgebrochen hat.
    /// </summary>
    private string ResolveWaitReason(bool released)
        => released
            ? "freigegeben"
            : _context!.CheckGate(UpdateE2ETestContext.ReadFailureGateName) == false
                ? "abgebrochen"
                : "timeout";

    private void ThrowLogErrorAnd(FailureCase failureCase, string reason)
    {
        _context!.Log.Write(
            UpdateE2EEvents.UpdateSettingsReadFailed,
            new
            {
                versuch = failureCase.Attempt,
                versuchIndex = failureCase.AttemptIndex,
                ordinal = failureCase.Ordinal,
                grenze = failureCase.ReadFailure.Limit,
                grund = reason
            });

        throw FailureException;
    }
}
