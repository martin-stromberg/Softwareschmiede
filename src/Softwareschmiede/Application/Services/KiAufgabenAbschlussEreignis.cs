using Softwareschmiede.Domain.Enums;

namespace Softwareschmiede.Application.Services;

/// <summary>Ereignisdaten bei Abschluss einer KI-Aufgabe.</summary>
public sealed record KiAufgabenAbschlussEreignis(
    Guid EreignisId,
    Guid AufgabeId,
    string Aufgabentitel,
    AufgabeStatus AbschlussStatus,
    DateTimeOffset Zeitstempel);
