namespace Softwareschmiede.Domain.Interfaces;

/// <summary>
/// Kapselt einen OS-weiten Shutdown-Aufruf.
/// </summary>
public interface ISystemShutdownService
{
    /// <summary>Fordert das Herunterfahren des Host-Systems an.</summary>
    /// <param name="erzwingen"><see langword="true"/> erzwingt das Herunterfahren gegen den Widerstand laufender Anwendungen (unter Windows via <c>/f</c>); unter Linux/macOS ohne Wirkung.</param>
    /// <param name="cancellationToken">Token zum Abbrechen der Operation.</param>
    Task RequestShutdownAsync(bool erzwingen, CancellationToken cancellationToken = default);
}
