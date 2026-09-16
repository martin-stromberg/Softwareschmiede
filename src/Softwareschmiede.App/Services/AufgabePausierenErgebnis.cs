namespace Softwareschmiede.App.Services;

/// <summary>Ergebnis des Dialogs zum Einstellen oder Aufheben einer Aufgaben-Pause.</summary>
/// <param name="PausiertBisUtc">Der gewaehlte Pause-Endzeitpunkt in UTC, oder null bei Aufheben.</param>
/// <param name="Aufheben">true, wenn eine bestehende Pause aufgehoben werden soll.</param>
public sealed record AufgabePausierenErgebnis(DateTimeOffset? PausiertBisUtc, bool Aufheben);
