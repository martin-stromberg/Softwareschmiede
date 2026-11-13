namespace Softwareschmiede.Application.Services;

/// <summary>
/// Steuert den automatischen OS-Shutdown nach Ende aller laufenden Automatisierungen
/// (Aktivierung, Toleranzzeit-Countdown und Erzwingen-Flag).
/// </summary>
public interface IAutoShutdownOrchestrator
{
    /// <summary>Setzt den aktuellen Aktivierungszustand für Auto-Shutdown.</summary>
    /// <param name="enabled"><see langword="true"/> aktiviert den Auto-Shutdown (sofortige Auswertung), <see langword="false"/> disarmiert einen laufenden Countdown.</param>
    void SetEnabled(bool enabled);

    /// <summary>Setzt atomar Toleranzzeit und Erzwingen-Flag; bei laufendem Countdown und geänderter Toleranz wird der Countdown ab jetzt mit dem neuen Wert neu armiert.</summary>
    /// <param name="toleranzMinuten">Die Toleranzzeit des Countdowns in Minuten (negative Werte werden auf 0 normalisiert).</param>
    /// <param name="erzwingen"><see langword="true"/> erzwingt das Herunterfahren.</param>
    void SetOptionen(int toleranzMinuten, bool erzwingen);

    /// <summary>Der UTC-Endzeitpunkt des armierten Countdowns, oder <c>null</c>, wenn kein Countdown aktiv ist.</summary>
    DateTimeOffset? CountdownEndeUtc { get; }

    /// <summary>Der aktuelle Aktivierungszustand (UI-/Testdiagnose).</summary>
    bool IstAktiviert { get; }

    /// <summary>Wird ausgelöst, wenn sich der Orchestrator-Zustand ändert (Armierung, Disarmierung, Auslösung sowie Options-/Enabled-Wechsel).</summary>
    event Action? ZustandGeaendert;
}
