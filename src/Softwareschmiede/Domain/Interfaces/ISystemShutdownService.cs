namespace Softwareschmiede.Domain.Interfaces;

/// <summary>
/// Kapselt einen OS-weiten Shutdown-Aufruf.
/// </summary>
public interface ISystemShutdownService
{
    /// <summary>Fordert das Herunterfahren des Host-Systems an.</summary>
    Task RequestShutdownAsync(CancellationToken cancellationToken = default);
}
