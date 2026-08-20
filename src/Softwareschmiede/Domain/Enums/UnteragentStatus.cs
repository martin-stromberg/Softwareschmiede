namespace Softwareschmiede.Domain.Enums;

/// <summary>Lebenszyklusstatus eines <see cref="Entities.UnteragentSpezifikation"/>.</summary>
public enum UnteragentStatus
{
    /// <summary>Der Unteragent wurde erzeugt, aber noch nicht gestartet.</summary>
    Erzeugt,

    /// <summary>Der Unteragent wird gerade ausgeführt.</summary>
    Ausgefuehrt,

    /// <summary>Der Unteragent hat seine Teilaufgabe erfolgreich abgeschlossen.</summary>
    Abgeschlossen,

    /// <summary>Der Unteragent ist mit einem Fehler abgebrochen.</summary>
    Fehler
}
