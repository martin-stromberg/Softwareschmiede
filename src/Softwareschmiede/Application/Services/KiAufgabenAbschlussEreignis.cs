using Softwareschmiede.Domain.Enums;

namespace Softwareschmiede.Application.Services;

/// <summary>Ereignisdaten bei Abschluss einer KI-Aufgabe.</summary>
public sealed record KiAufgabenAbschlussEreignis(
    Guid EventId,
    Guid WorkItemId,
    string Aufgabentitel,
    WorkItemStatus AbschlussStatus,
    DateTimeOffset Timestamp);
