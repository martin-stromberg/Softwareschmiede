using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Domain.Interfaces;

/// <summary>Gateway über den OS-Prozessstart; entkoppelt <c>Process.Start</c> für Tests.</summary>
public interface IProcessStarter
{
    /// <summary>Startet einen Prozess anhand der übergebenen <see cref="ProcessStartRequest"/>.</summary>
    /// <param name="request">Die zu startende Prozessstartanforderung.</param>
    void Start(ProcessStartRequest request);
}
