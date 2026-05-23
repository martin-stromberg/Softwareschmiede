using Softwareschmiede.Domain.Enums;

namespace Softwareschmiede.Domain.Entities;

/// <summary>Benutzerbezogene Einstellungen für KI-Aufgaben-Benachrichtigungen.</summary>
public sealed class BenachrichtigungsEinstellung
{
    public Guid Id { get; set; }

    public string BenutzerId { get; set; } = string.Empty;

    public BenachrichtigungsModus ToastModus { get; set; } = BenachrichtigungsModus.Global;

    public BenachrichtigungsModus TonModus { get; set; } = BenachrichtigungsModus.NurAufgabenseite;

    public DateTimeOffset AktualisiertAm { get; set; }
}
