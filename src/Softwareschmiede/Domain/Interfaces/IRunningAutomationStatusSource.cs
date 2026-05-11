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
}
