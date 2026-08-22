namespace Softwareschmiede.Domain.Entities;

/// <summary>Erweiterungsmethoden fuer <see cref="Aufgabe"/>.</summary>
public static class AufgabeExtensions
{
    /// <summary>
    /// Gibt an, ob die Aufgabe eine Autonome Aufgabe unter Steuerung eines Projektleiter-Agenten ist.
    /// Alleiniger Modus-Indikator (regulär vs. autonom); die Ausführungsphase (nicht gestartet/aktiv/beendet)
    /// wird unabhängig davon über <see cref="Aufgabe.AusfuehrungsStatus"/> abgebildet.
    /// </summary>
    /// <remarks>
    /// Achtung: Setzt voraus, dass <see cref="Aufgabe.AutonomKonfiguration"/> geladen ist (z. B. via
    /// <c>.Include(a => a.AutonomKonfiguration)</c>) oder die <see cref="Aufgabe"/> im selben <c>DbContext</c>
    /// bereits getrackt wurde (EF-Relationship-Fixup). Andernfalls liefert diese Methode bei
    /// <c>AsNoTracking()</c>-Queries ohne <c>Include</c> fälschlich <c>false</c>, ohne dass dies zur Compile-Zeit
    /// erkennbar ist.
    /// </remarks>
    /// <param name="aufgabe">Die zu prüfende Aufgabe.</param>
    /// <returns><c>true</c>, wenn die Aufgabe eine Autonome Aufgabe ist; andernfalls <c>false</c>.</returns>
    public static bool IstAutonom(this Aufgabe aufgabe) => aufgabe.AutonomKonfiguration is not null;
}
