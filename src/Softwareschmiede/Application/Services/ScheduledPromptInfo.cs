namespace Softwareschmiede.Application.Services;

/// <summary>Container für einen zeitgesteuert an eine CLI-Session zu versendenden Prompt.</summary>
/// <param name="AufgabeId">ID der Aufgabe, deren Session den Prompt erhalten soll.</param>
/// <param name="PromptText">Der bereits platzhalteraufgelöste Prompttext.</param>
/// <param name="TargetTime">Der Zeitpunkt, zu dem der Prompt versendet werden soll.</param>
/// <returns>Kein Rückgabewert (Datencontainer).</returns>
public sealed record ScheduledPromptInfo(Guid AufgabeId, string PromptText, DateTimeOffset TargetTime);
