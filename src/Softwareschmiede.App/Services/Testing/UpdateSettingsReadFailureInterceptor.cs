using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Softwareschmiede.Application.Services;

namespace Softwareschmiede.App.Services.Testing;

/// <summary>
/// EF-Core-Interceptor, der ausschließlich mit dem Abfragetag <see cref="AppEinstellungService.UpdateSettingsReadTag"/>
/// markierte Lesebefehle der Update-Einstellungen gezielt mit einer kontrollierten Exception fehlschlagen lässt.
/// Alle anderen Lese- und Schreibbefehle bleiben unberührt. Der Fehler ist aktivier- und deaktivierbar;
/// jeder erreichte markierte Lesevorgang wird gezählt und über <see cref="SettingsReadReached"/> gemeldet.
/// </summary>
public sealed class UpdateSettingsReadFailureInterceptor : DbCommandInterceptor
{
    private volatile bool _failureEnabled;
    private int _settingsReadCount;

    /// <summary>Wird ausgelöst, sobald ein markierter Update-Settings-Lesebefehl erreicht wird (vor einem etwaigen Fehler).</summary>
    public event EventHandler? SettingsReadReached;

    /// <summary>Anzahl der bisher beobachteten markierten Update-Settings-Lesebefehle.</summary>
    public int SettingsReadCount => _settingsReadCount;

    /// <summary>Gibt an, ob markierte Update-Settings-Lesebefehle aktuell kontrolliert fehlschlagen.</summary>
    public bool FailureEnabled => _failureEnabled;

    /// <summary>Die kontrollierte Exception, die bei aktiviertem Fehler für markierte Lesebefehle geworfen wird.</summary>
    public Exception FailureException { get; init; } = new InvalidOperationException("Kontrollierter Testfehler beim Lesen der Update-Einstellungen.");

    /// <summary>Aktiviert den kontrollierten Lesefehler für markierte Update-Settings-Abfragen.</summary>
    public void ActivateFailure() => _failureEnabled = true;

    /// <summary>Deaktiviert den kontrollierten Lesefehler wieder; markierte Abfragen laufen danach normal.</summary>
    public void DeactivateFailure() => _failureEnabled = false;

    /// <inheritdoc/>
    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result)
    {
        HandleTaggedRead(command);
        return result;
    }

    /// <inheritdoc/>
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        HandleTaggedRead(command);
        return ValueTask.FromResult(result);
    }

    private void HandleTaggedRead(DbCommand command)
    {
        if (!command.CommandText.Contains(AppEinstellungService.UpdateSettingsReadTag, StringComparison.Ordinal))
            return;

        Interlocked.Increment(ref _settingsReadCount);
        SettingsReadReached?.Invoke(this, EventArgs.Empty);

        if (_failureEnabled)
            throw FailureException;
    }
}
