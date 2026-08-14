namespace Softwareschmiede.Domain.ValueObjects;

/// <summary>Ergebnis der VS-Code-Auflösung.</summary>
/// <param name="IsAvailable">Gibt an, ob VS Code startbar ist.</param>
/// <param name="ExecutablePath">Der startbare Befehl oder Pfad, falls verfügbar.</param>
public sealed record VisualStudioCodeAvailability(bool IsAvailable, string? ExecutablePath)
{
    /// <summary>Nicht verfügbarer Locator-Status.</summary>
    public static VisualStudioCodeAvailability NotAvailable { get; } = new(false, null);
}
