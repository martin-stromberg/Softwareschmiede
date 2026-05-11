namespace Softwareschmiede.Application.Services;

/// <summary>
/// Steuert, ob ein automatischer Shutdown nach Ende aller Läufe erlaubt ist.
/// </summary>
public interface IAutoShutdownOrchestrator
{
    /// <summary>Setzt den aktuellen Aktivierungszustand für Auto-Shutdown.</summary>
    void SetEnabled(bool enabled);
}
