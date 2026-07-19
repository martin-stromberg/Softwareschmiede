namespace Softwareschmiede.Domain.ValueObjects;

/// <summary>Beschreibt eine Prozessstartanforderung ohne Abhängigkeit auf <c>System.Diagnostics</c>.</summary>
/// <param name="DateiName">Name bzw. Pfad der zu startenden Datei oder des auszuführenden Befehls.</param>
/// <param name="Argumente">Optionale Argumente für den Prozessstart.</param>
/// <param name="ShellAusfuehren">Gibt an, ob der Prozess über die Shell des Betriebssystems gestartet werden soll.</param>
public sealed record ProzessStartAnfrage(
    string DateiName,
    string? Argumente,
    bool ShellAusfuehren
);
