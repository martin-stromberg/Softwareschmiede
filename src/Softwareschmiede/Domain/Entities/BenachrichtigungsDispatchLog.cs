using Softwareschmiede.Domain.Enums;

namespace Softwareschmiede.Domain.Entities;

/// <summary>Audit-Eintrag einer Benachrichtigungsentscheidung.</summary>
public sealed class BenachrichtigungsDispatchLog
{
    public Guid Id { get; set; }

    public Guid EreignisId { get; set; }

    public Guid AufgabeId { get; set; }

    public string BenutzerId { get; set; } = string.Empty;

    public BenachrichtigungsKanal Kanal { get; set; }

    public BenachrichtigungsModus Modus { get; set; }

    public BenachrichtigungsEntscheidung Entscheidung { get; set; }

    public string Grund { get; set; } = string.Empty;

    public DateTimeOffset ErstelltAm { get; set; }
}
