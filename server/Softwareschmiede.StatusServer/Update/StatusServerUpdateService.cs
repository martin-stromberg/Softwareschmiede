using msTools.Updater;

namespace Softwareschmiede.StatusServer.Update;

/// <summary>Dünne Adapterschicht über der <c>msTools.Updater</c>-Bibliothek für die
/// Admin-Endpunkte: Statusabfrage (inkl. Neustart-Rekonziliation), Anwenden der
/// persistierten Admin-Einstellungen auf die Laufzeitoptionen, manuell ausgelöste
/// Prüfung/Download/Installation und Zurücksetzen verwaister Installations-Locks.
/// Die Bibliothek signalisiert Fehlschläge über <see cref="AutoUpdateResult"/>
/// statt Exceptions — die Endpunkte werten <see cref="AutoUpdateResult.Outcome"/> aus.</summary>
public sealed class StatusServerUpdateService
{
    /// <summary>Prozessweiter Schalter, der alle Mutatoren des prozessweiten
    /// <see cref="AutoUpdateOptions"/>-Singletons serialisiert: manuelle
    /// Enabled-Overrides (<see cref="RunWithManualOverrideAsync{T}"/>) und das
    /// Anwenden persistierter Admin-Einstellungen
    /// (<see cref="ApplySettingsAsync"/>). Zwei parallele manuelle Läufe (jeweils
    /// eigene Service-Instanz aus dem Request-Scope) dürften sich den Flag nicht
    /// wechselseitig zurücksetzen, sonst stellt der erste finishende Lauf
    /// <c>Enabled=false</c> wieder her, während der zweite noch Bibliotheksaufrufe
    /// ausführt, die den Schalter prüfen; ebenso darf der
    /// <see langword="finally"/>-Restore eines manuellen Laufs keinen parallel
    /// gespeicherten Einstellungswert überschreiben.</summary>
    private static readonly SemaphoreSlim s_manualOverrideLock = new(1, 1);

    private readonly IAutoUpdateOrchestrator _orchestrator;
    private readonly AutoUpdateStatusService _statusService;
    private readonly IAutoUpdatePackageStore _packageStore;
    private readonly AutoUpdateOptions _autoUpdateOptions;
    private readonly StatusServerUpdateSourceFactory _sourceFactory;
    private readonly UpdateStatusMapper _statusMapper;

    /// <summary>Erstellt den Service.</summary>
    /// <param name="orchestrator">Der Bibliotheks-Orchestrator für Statusabfragen
    /// (inkl. Neustart-Rekonziliation) und manuell ausgelöste Check-/Install-Läufe.</param>
    /// <param name="statusService">Liest den Status-Snapshot unmittelbar nach einem
    /// Kommando und persistiert die Status-Korrektur beim Lock-Reset.</param>
    /// <param name="packageStore">Prüft, bewertet und löscht den Installations-Lock.</param>
    /// <param name="autoUpdateOptions">Die laufzeitveränderbaren Bibliotheksoptionen;
    /// für manuell ausgelöste Aktionen wird <see cref="AutoUpdateOptions.Enabled"/>
    /// kurzzeitig erzwungen (siehe <see cref="RunWithManualOverrideAsync{T}"/>).</param>
    /// <param name="sourceFactory">Baut <see cref="AutoUpdateOptions.Source"/> nach
    /// Einstellungsänderungen token- und kanalbewusst neu.</param>
    /// <param name="statusMapper">Bildet Snapshots auf das UI-DTO ab.</param>
    public StatusServerUpdateService(
        IAutoUpdateOrchestrator orchestrator,
        AutoUpdateStatusService statusService,
        IAutoUpdatePackageStore packageStore,
        AutoUpdateOptions autoUpdateOptions,
        StatusServerUpdateSourceFactory sourceFactory,
        UpdateStatusMapper statusMapper)
    {
        ArgumentNullException.ThrowIfNull(orchestrator);
        ArgumentNullException.ThrowIfNull(statusService);
        ArgumentNullException.ThrowIfNull(packageStore);
        ArgumentNullException.ThrowIfNull(autoUpdateOptions);
        ArgumentNullException.ThrowIfNull(sourceFactory);
        ArgumentNullException.ThrowIfNull(statusMapper);
        _orchestrator = orchestrator;
        _statusService = statusService;
        _packageStore = packageStore;
        _autoUpdateOptions = autoUpdateOptions;
        _sourceFactory = sourceFactory;
        _statusMapper = statusMapper;
    }

    /// <summary>Liefert den aktuellen Update-Status für die Admin-Anzeige.</summary>
    /// <param name="cancellationToken">Abbruchtoken.</param>
    /// <returns>Der Update-Status.</returns>
    public async Task<StatusServerUpdateStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await _orchestrator.GetStatusAsync(cancellationToken).ConfigureAwait(false);
        return await _statusMapper.MapAsync(snapshot, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Wendet persistierte Admin-Einstellungen auf die Laufzeitoptionen an
    /// und baut die Update-Quelle passend zum Kanal neu (Kanalwechsel wirkt sofort).
    /// Läuft unter <see cref="s_manualOverrideLock"/>, damit der
    /// <see langword="finally"/>-Restore von <see cref="AutoUpdateOptions.Enabled"/>
    /// eines parallel laufenden manuellen Overrides den gerade gespeicherten
    /// Modus nicht wieder überschreibt — der kritische Abschnitt enthält nur die
    /// synchrone Options-Mutation, keine Netzwerkaufrufe.</summary>
    /// <param name="settings">Die anzuwendenden Einstellungen.</param>
    /// <returns>Eine abgeschlossene Aufgabe.</returns>
    public async Task ApplySettingsAsync(StatusServerUpdateSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        await s_manualOverrideLock.WaitAsync().ConfigureAwait(false);
        try
        {
            StatusServerAutoUpdateOptionsMapper.ApplySettings(_autoUpdateOptions, settings);
            _sourceFactory.ReplaceSource(_autoUpdateOptions, settings);
        }
        finally
        {
            s_manualOverrideLock.Release();
        }
    }

    /// <summary>Manuell ausgelöste Update-Prüfung mit anschließendem Download bei
    /// gefundenem Paket (inkl. SHA-256-Verifikation gegen das Manifest). Läuft auch
    /// bei deaktivierter Automation (<see cref="AutoUpdateOptions.Enabled"/>
    /// = <see langword="false"/>), weil der Admin die Aktion explizit angefordert hat.</summary>
    /// <param name="cancellationToken">Abbruchtoken.</param>
    /// <returns>Das Ergebnis von Prüfung bzw. Download.</returns>
    public async Task<AutoUpdateResult> CheckAndDownloadAsync(CancellationToken cancellationToken = default)
        => await RunWithManualOverrideAsync(async () =>
        {
            var checkResult = await _orchestrator.CheckForUpdateAsync(cancellationToken).ConfigureAwait(false);
            return checkResult.Outcome == AutoUpdateOutcome.Success
                ? await _orchestrator.DownloadAsync(cancellationToken).ConfigureAwait(false)
                : checkResult;
        }, cancellationToken).ConfigureAwait(false);

    /// <summary>Manuell ausgelöste Installation: liegt nur ein gefundenes, noch nicht
    /// geladenes Paket vor (<see cref="AutoUpdateState.UpdateAvailable"/> ohne
    /// Download-Ergebnis), wird zunächst nachgeladen; danach startet
    /// <see cref="IAutoUpdateOrchestrator.InstallAsync"/> das detached laufende
    /// Installationsskript (Stopp/Start des Dienstes obliegt dem Skript).</summary>
    /// <param name="confirmDowntime">Muss <see langword="true"/> sein — die
    /// Installation startet den Server neu.</param>
    /// <param name="cancellationToken">Abbruchtoken.</param>
    /// <returns>Das Ergebnis des Installationsanlaufs.</returns>
    public async Task<AutoUpdateResult> StartInstallAsync(bool confirmDowntime, CancellationToken cancellationToken = default)
        => await RunWithManualOverrideAsync(async () =>
        {
            if (confirmDowntime)
            {
                var snapshot = _statusService.GetSnapshot();
                if (snapshot.State == AutoUpdateState.UpdateAvailable && snapshot.LastDownloadResult is null)
                {
                    var downloadResult = await _orchestrator.DownloadAsync(cancellationToken).ConfigureAwait(false);
                    if (downloadResult.Outcome == AutoUpdateOutcome.Failed)
                        return downloadResult;
                }
            }

            return await _orchestrator.InstallAsync(confirmDowntime, force: false, cancellationToken).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);

    /// <summary>Setzt einen verwaisten Installations-Lock zurück (<c>update.lock</c>):
    /// nur wenn ein Lock existiert <em>und</em> <see cref="IAutoUpdatePackageStore.IsLockStale"/>
    /// ihn als abgelaufen bewertet. Nach dem Löschen wird der persistierte Status korrigiert.</summary>
    /// <param name="cancellationToken">Abbruchtoken.</param>
    /// <returns><see langword="true"/>, wenn der Lock zurückgesetzt wurde.</returns>
    public async Task<bool> ResetLockAsync(CancellationToken cancellationToken = default)
    {
        var lockCreatedAt = await _packageStore.GetLockCreatedAtAsync(cancellationToken).ConfigureAwait(false);
        if (!lockCreatedAt.HasValue || !_packageStore.IsLockStale(lockCreatedAt.Value))
            return false;

        var deleted = await _packageStore.DeleteLockAsync(cancellationToken).ConfigureAwait(false);
        if (!deleted)
            return false;

        await _statusService.UpdateAsync(s => s with { IsLocked = false, LockCreatedAt = null }, cancellationToken)
            .ConfigureAwait(false);
        return true;
    }

    /// <summary>Führt <paramref name="action"/> aus, während
    /// <see cref="AutoUpdateOptions.Enabled"/> bei Bedarf kurzzeitig auf
    /// <see langword="true"/> steht: „Enabled" ist der Master-Schalter, der auch die
    /// Hintergrund-Automation steuert — ein explizit ausgelöster Admin-Klick muss
    /// aber trotz deaktivierter Automation laufen. Der komplette Override-Block
    /// (Setzen → Aktion → Wiederherstellen) läuft unter
    /// <see cref="s_manualOverrideLock"/>, sodass parallele manuelle Läufe
    /// nacheinander statt verschachtelt ablaufen und der Flag erst
    /// wiederhergestellt wird, wenn kein Override mehr aktiv ist. Der
    /// ursprüngliche Wert (der die Hintergrund-Dienste weiterhin deaktiviert
    /// lässt) wird auch bei einer werfenden Aktion wiederhergestellt.</summary>
    /// <typeparam name="T">Der Ergebnistyp von <paramref name="action"/>.</typeparam>
    /// <param name="action">Die manuell ausgelöste Orchestrator-Operation.</param>
    /// <param name="cancellationToken">Abbruchtoken für das Warten auf den Override.</param>
    /// <returns>Das Ergebnis von <paramref name="action"/>.</returns>
    private async Task<T> RunWithManualOverrideAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken)
    {
        await s_manualOverrideLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        var wasEnabled = _autoUpdateOptions.Enabled;
        if (!wasEnabled)
            _autoUpdateOptions.Enabled = true;

        try
        {
            return await action();
        }
        finally
        {
            if (!wasEnabled)
                _autoUpdateOptions.Enabled = wasEnabled;
            s_manualOverrideLock.Release();
        }
    }
}
