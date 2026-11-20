using Softwareschmiede.Domain.Enums;

namespace Softwareschmiede.Application.Services;

/// <summary>Ereignisdaten bei Abschluss einer KI-Aufgabe.</summary>
public sealed record AiWorkItemCompletionEvent(
    Guid EventId,
    Guid WorkItemId,
    string WorkItemTitle,
    WorkItemStatus CompletionStatus,
    DateTimeOffset Timestamp);
