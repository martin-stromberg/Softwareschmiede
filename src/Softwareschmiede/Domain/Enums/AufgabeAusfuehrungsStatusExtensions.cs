namespace Softwareschmiede.Domain.Enums;

/// <summary>Erweiterungsmethoden fuer den persistenten KI-Ausfuehrungsstatus.</summary>
public static class AufgabeAusfuehrungsStatusExtensions
{
    /// <summary>Gibt an, ob fuer die Aufgabe eine KI-Ausfuehrung explizit gestartet werden darf.</summary>
    public static bool DarfAusfuehrungStarten(this AufgabeAusfuehrungsStatus ausfuehrungsStatus, AufgabeStatus aufgabeStatus)
        => aufgabeStatus is not AufgabeStatus.Beendet and not AufgabeStatus.Archiviert
            && ausfuehrungsStatus is (AufgabeAusfuehrungsStatus.NichtGestartet or AufgabeAusfuehrungsStatus.Beendet);

    /// <summary>Gibt an, ob die CLI-Ansicht fuer die Aufgabe angezeigt werden soll. Dies ist der Fall, wenn die Aufgabe aktiv oder wartend ist und die KI-Ausfuehrung aktiv oder beendet ist.</summary>
    /// <param name="ausfuehrungsStatus">Der zu pruefende KI-Ausfuehrungsstatus.</param>
    /// <param name="aufgabeStatus">Der zu pruefende Aufgabenstatus.</param>
    /// <returns><c>true</c>, wenn die CLI-Ansicht angezeigt werden soll; andernfalls <c>false</c>.</returns>
    public static bool SollCliAnzeigen(this AufgabeAusfuehrungsStatus ausfuehrungsStatus, AufgabeStatus aufgabeStatus)
        => aufgabeStatus.IstAktivOderWartend()
            && ausfuehrungsStatus is (AufgabeAusfuehrungsStatus.Aktiv or AufgabeAusfuehrungsStatus.Beendet);
}
