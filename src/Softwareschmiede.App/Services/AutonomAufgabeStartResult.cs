using Softwareschmiede.App.ViewModels;
using Softwareschmiede.Domain.Entities;

namespace Softwareschmiede.App.Services;

/// <summary>Ergebnis des Autonome-Aufgabe-Start-Ablaufs aus <see cref="AutonomAufgabeStartService"/>.</summary>
/// <param name="AktualisierteAufgabe">Die neu geladene Aufgabe nach der Initialisierung, sofern verfügbar.</param>
/// <param name="FehlerMeldung">Fehlermeldung, falls die Detail-Ansicht nicht angezeigt werden konnte, sonst <see langword="null"/>.</param>
/// <param name="DetailViewModel">Das bei erfolgreicher Initialisierung erzeugte ViewModel der Automatisierung-Ansicht, oder <see langword="null"/> bei Fehlern oder Abbruch.</param>
public sealed record AutonomAufgabeStartResult(Aufgabe? AktualisierteAufgabe, string? FehlerMeldung, AutonomAufgabeDetailViewModel? DetailViewModel);
