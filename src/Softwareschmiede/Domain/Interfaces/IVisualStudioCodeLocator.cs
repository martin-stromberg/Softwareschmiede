using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Domain.Interfaces;

/// <summary>Ermittelt, ob Visual Studio Code auf dem System startbar ist.</summary>
public interface IVisualStudioCodeLocator
{
    /// <summary>Liefert den startbaren VS-Code-Befehl oder -Pfad, falls verfügbar.</summary>
    /// <returns>Das Ergebnis der VS-Code-Auflösung.</returns>
    VisualStudioCodeAvailability Locate();
}
