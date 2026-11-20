namespace Softwareschmiede.App.Services;

/// <summary>Ergebnis des Dialogs zum Einstellen oder Lift einer Aufgaben-Pause.</summary>
/// <param name="PausedUntilUtc">Der gewaehlte Pause-Endzeitpunkt in UTC, oder null bei Lift.</param>
/// <param name="Lift">true, wenn eine bestehende Pause aufgehoben werden soll.</param>
public sealed record PauseWorkItemResult(DateTimeOffset? PausedUntilUtc, bool Lift);
