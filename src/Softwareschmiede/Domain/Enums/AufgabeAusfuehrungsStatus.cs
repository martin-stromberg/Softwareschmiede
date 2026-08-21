namespace Softwareschmiede.Domain.Enums;

/// <summary>Persistierter Lebenszyklusstatus der KI-Ausfuehrung einer Aufgabe.</summary>
public enum AufgabeAusfuehrungsStatus
{
    /// <summary>Die KI-Ausfuehrung wurde noch nicht gestartet.</summary>
    NichtGestartet,

    /// <summary>Die KI-Ausfuehrung ist aktiv oder soll nach einem App-Neustart wiederhergestellt werden.</summary>
    Aktiv,

    /// <summary>Die KI-Ausfuehrung wurde beendet; ein erneuter Start muss explizit ausgeloest werden.</summary>
    Beendet,

    /// <summary>Die Aufgabe ist eine Autonome Aufgabe unter Steuerung eines Projektleiter-Agenten.</summary>
    AutonomAufgabe
}
