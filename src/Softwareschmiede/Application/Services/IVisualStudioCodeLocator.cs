namespace Softwareschmiede.Application.Services;

/// <summary>Ermittelt, ob Visual Studio Code auf dem System startbar ist.</summary>
public interface IVisualStudioCodeLocator
{
    /// <summary>Liefert den startbaren VS-Code-Befehl oder -Pfad, falls verfügbar.</summary>
    VisualStudioCodeAvailability Locate();
}

/// <summary>Ergebnis der VS-Code-Auflösung.</summary>
/// <param name="IsAvailable">Gibt an, ob VS Code startbar ist.</param>
/// <param name="ExecutablePath">Der startbare Befehl oder Pfad, falls verfügbar.</param>
public sealed record VisualStudioCodeAvailability(bool IsAvailable, string? ExecutablePath)
{
    /// <summary>Nicht verfügbarer Locator-Status.</summary>
    public static VisualStudioCodeAvailability NotAvailable { get; } = new(false, null);
}
