namespace Softwareschmiede.Domain.Entities;

/// <summary>Persistierter Datensatz eines geplanten zeitgesteuerten Prompts (max. ein Plan pro Aufgabe).</summary>
public sealed class ZeitgesteuerterPrompt
{
    /// <summary>Eindeutige ID des Datensatzes.</summary>
    public Guid Id { get; set; }

    /// <summary>ID der Aufgabe, deren CLI-Session den Prompt erhalten soll.</summary>
    public Guid AufgabeId { get; set; }

    /// <summary>Der bereits platzhalteraufgelöste Prompttext.</summary>
    public string PromptText { get; set; } = string.Empty;

    /// <summary>UTC-Zielzeitpunkt, zu dem der Prompt versendet werden soll.</summary>
    public DateTimeOffset ZielZeitpunktUtc { get; set; }

    /// <summary>Erstellungszeitpunkt des Datensatzes.</summary>
    public DateTimeOffset ErstelltAmUtc { get; set; }

    /// <summary>Letzter Aktualisierungszeitpunkt (Ersetzen, Pausenverschiebung).</summary>
    public DateTimeOffset AktualisiertAmUtc { get; set; }
}
