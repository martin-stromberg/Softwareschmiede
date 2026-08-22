namespace Softwareschmiede.Domain.Enums;

/// <summary>Persistenz-Modus einer <see cref="Entities.AutonomAufgabeKonfiguration"/> für Session-Wiederaufnahmen.</summary>
public enum PersistenzModus
{
    /// <summary>Der Zustand wird beim Fortsetzen unverändert übernommen.</summary>
    Standard,

    /// <summary>Die Session wird beim Fortsetzen zurückgesetzt.</summary>
    SitzungZuruecksetzen
}
