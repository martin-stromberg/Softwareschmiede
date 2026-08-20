namespace Softwareschmiede.Domain.Enums;

/// <summary>Erweiterungsmethoden fuer den persistenten KI-Ausfuehrungsstatus.</summary>
public static class AufgabeAusfuehrungsStatusExtensions
{
    /// <summary>Gibt an, ob fuer die Aufgabe eine KI-Ausfuehrung explizit gestartet werden darf.</summary>
    public static bool DarfAusfuehrungStarten(this AufgabeAusfuehrungsStatus ausfuehrungsStatus, AufgabeStatus aufgabeStatus)
        => aufgabeStatus is not AufgabeStatus.Beendet and not AufgabeStatus.Archiviert
            && ausfuehrungsStatus is (AufgabeAusfuehrungsStatus.NichtGestartet or AufgabeAusfuehrungsStatus.Beendet);

    /// <summary>Gibt an, ob die CLI-Ansicht fuer die Aufgabe angezeigt werden soll.</summary>
    public static bool SollCliAnzeigen(this AufgabeAusfuehrungsStatus ausfuehrungsStatus, AufgabeStatus aufgabeStatus)
        => aufgabeStatus.IstAktivOderWartend()
            && ausfuehrungsStatus == AufgabeAusfuehrungsStatus.Aktiv;
}
