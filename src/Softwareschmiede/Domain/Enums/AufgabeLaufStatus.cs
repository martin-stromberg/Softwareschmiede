namespace Softwareschmiede.Domain.Enums;

/// <summary>
/// Feingranularer Laufzeit-Substatus einer Aufgabe, während ein CLI-Prozess für sie aktiv ist
/// (<see cref="Entities.Aufgabe.AktiveRunId"/> ist gesetzt). Bildet ab, ob die CLI gerade aktiv arbeitet
/// oder auf Benutzereingabe wartet — unabhängig vom groben Lebenszyklus-<see cref="AufgabeStatus"/> der
/// Aufgabe (Neu/Gestartet/Wartend/Beendet/Archiviert).
/// </summary>
/// <remarks>
/// Bewusst als eigenständiges Domain-Enum modelliert statt <see cref="AufgabeStatus.Wartend"/>
/// wiederzuverwenden: <see cref="AufgabeStatus.Wartend"/> bedeutet "CLI hat Rate-Limit erreicht; wartet
/// auf Wiederaufnahme" — ein Lebenszyklus-Zustand, der über <c>AufgabeService.SetStatusAsync</c> mit
/// Transitions-Validierung gesetzt wird. Der hier abgebildete Zustand ist ein rein beobachtender
/// Laufzeit-Substatus (abgeleitet aus Terminal-I/O-Aktivität, siehe
/// <c>Infrastructure.Terminal.CliRuntimeStatusEvaluator</c>), der *während* des Status <c>Gestartet</c>
/// mehrfach pro Sekunde wechseln kann und keiner Transitions-Validierung unterliegt. Eine Vermischung
/// beider Konzepte würde entweder die Statusmaschine verletzen oder die Rate-Limit-Semantik verwässern.
/// Absichtlich ohne <c>Infrastructure.Terminal.CliRuntimeStatus</c>-Referenz, damit die Domain-Schicht
/// nicht von der Infrastructure-Schicht abhängt.
/// </remarks>
public enum AufgabeLaufStatus
{
    /// <summary>Die CLI läuft und hat kürzlich Ausgabe oder Eingabe verarbeitet.</summary>
    Laeuft,

    /// <summary>Die CLI läuft, erzeugt aber seit längerer Zeit keine Ausgabe und wartet vermutlich auf Benutzereingabe.</summary>
    WartetAufEingabe
}
