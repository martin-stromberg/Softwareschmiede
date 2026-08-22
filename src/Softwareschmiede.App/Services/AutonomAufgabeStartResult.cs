using Softwareschmiede.Domain.Entities;

namespace Softwareschmiede.App.Services;

/// <summary>Ergebnis des Autonome-Aufgabe-Start-Ablaufs aus <see cref="AutonomAufgabeStartService"/>.</summary>
/// <param name="AktualisierteAufgabe">Die neu geladene Aufgabe nach der Initialisierung, sofern verfügbar.</param>
/// <param name="FehlerMeldung">Fehlermeldung, falls die Detail-Ansicht nicht angezeigt werden konnte, sonst <see langword="null"/>.</param>
public sealed record AutonomAufgabeStartResult(Aufgabe? AktualisierteAufgabe, string? FehlerMeldung);
