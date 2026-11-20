namespace Softwareschmiede.Domain.ValueObjects;

/// <summary>Beschreibt eine Prozessstartanforderung ohne Abhängigkeit auf <c>System.Diagnostics</c>.</summary>
/// <param name="FileName">Name bzw. Pfad der zu startenden Datei oder des auszuführenden Befehls.</param>
/// <param name="Arguments">Optionale Argumente für den Prozessstart.</param>
/// <param name="UseShellExecute">Gibt an, ob der Prozess über die Shell des Betriebssystems gestartet werden soll.</param>
public sealed record ProcessStartRequest(
    string FileName,
    string? Arguments,
    bool UseShellExecute
);
