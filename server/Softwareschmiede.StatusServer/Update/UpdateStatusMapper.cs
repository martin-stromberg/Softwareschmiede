using msTools.Updater;

namespace Softwareschmiede.StatusServer.Update;

/// <summary>Bildet einen <see cref="AutoUpdateStatusSnapshot"/> zusammen mit der
/// installierten Version (<see cref="IInstalledVersionProvider"/>, aus
/// <c>release-metadata.json</c>) und dem Lock-Alter (<see cref="IAutoUpdatePackageStore.IsLockStale"/>)
/// auf das flache <see cref="StatusServerUpdateStatus"/>-DTO der Admin-Seite ab.</summary>
public sealed class UpdateStatusMapper
{
    private readonly IInstalledVersionProvider _installedProvider;
    private readonly IAutoUpdatePackageStore _packageStore;

    /// <summary>Erstellt den Mapper.</summary>
    /// <param name="installedProvider">Ermittelt die installierte Version.</param>
    /// <param name="packageStore">Beurteilt die Verwaistheit des Installations-Locks.</param>
    public UpdateStatusMapper(IInstalledVersionProvider installedProvider, IAutoUpdatePackageStore packageStore)
    {
        ArgumentNullException.ThrowIfNull(installedProvider);
        ArgumentNullException.ThrowIfNull(packageStore);
        _installedProvider = installedProvider;
        _packageStore = packageStore;
    }

    /// <summary>Bildet <paramref name="snapshot"/> auf <see cref="StatusServerUpdateStatus"/> ab.</summary>
    /// <param name="snapshot">Der abzubildende Status-Snapshot.</param>
    /// <param name="cancellationToken">Abbruchtoken.</param>
    /// <returns>Das abgebildete Status-DTO.</returns>
    public async Task<StatusServerUpdateStatus> MapAsync(AutoUpdateStatusSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        var installed = await _installedProvider.GetAsync(cancellationToken).ConfigureAwait(false);
        var isLockStale = snapshot.IsLocked
            && snapshot.LockCreatedAt is { } lockCreatedAt
            && _packageStore.IsLockStale(lockCreatedAt);

        return new StatusServerUpdateStatus(
            installed.Version,
            snapshot.AvailableVersion,
            snapshot.State,
            snapshot.LastCheckedAt,
            snapshot.LastError,
            snapshot.IsLocked,
            isLockStale,
            snapshot.UpdateLogPath);
    }
}
