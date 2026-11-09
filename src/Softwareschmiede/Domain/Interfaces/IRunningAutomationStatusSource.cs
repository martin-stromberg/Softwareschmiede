using Softwareschmiede.Domain.Enums;

namespace Softwareschmiede.Domain.Interfaces;

/// <summary>
/// Liefert Laufzeitinformationen zu aktuell laufenden Automatisierungen.
/// </summary>
public interface IRunningAutomationStatusSource
{
    /// <summary>
    /// Wird ausgelöst, wenn sich die Anzahl laufender Automatisierungen ändert.
    /// Argumente: vorheriger und aktueller Wert.
    /// </summary>
    event Action<int, int>? RunningCountChanged;

    /// <summary>Gibt die Anzahl laufender Automatisierungen zurück.</summary>
    int GetRunningCount();

    /// <summary>Gibt an, ob für eine konkrete Aufgabe aktuell eine Automatisierung läuft.</summary>
    bool IsRunning(Guid aufgabeId);

    /// <summary>
    /// Liefert den Live-Lauf-Substatus einer Aufgabe: <c>null</c>, wenn kein gehandelter Prozess
    /// für die Aufgabe läuft; <see cref="AufgabeLaufStatus.WartetAufEingabe"/>, wenn die Session
    /// Warten auf Eingabe meldet; sonst <see cref="AufgabeLaufStatus.Laeuft"/> (inkl. laufendem
    /// Handle ohne Session).
    /// </summary>
    AufgabeLaufStatus? GetLiveLaufStatus(Guid aufgabeId);
}
