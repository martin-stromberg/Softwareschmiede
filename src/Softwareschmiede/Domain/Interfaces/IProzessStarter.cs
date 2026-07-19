using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Domain.Interfaces;

/// <summary>Gateway über den OS-Prozessstart; entkoppelt <c>Process.Start</c> für Tests.</summary>
public interface IProzessStarter
{
    /// <summary>Startet einen Prozess anhand der übergebenen <see cref="ProzessStartAnfrage"/>.</summary>
    /// <param name="anfrage">Die zu startende Prozessstartanforderung.</param>
    void Starten(ProzessStartAnfrage anfrage);
}
