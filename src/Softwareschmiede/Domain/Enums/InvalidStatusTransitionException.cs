namespace Softwareschmiede.Domain.Enums;

/// <summary>Wird ausgelöst, wenn ein ungültiger Statusübergang versucht wird.</summary>
public sealed class InvalidStatusTransitionException : InvalidOperationException
{
    /// <summary>Erstellt eine neue Instanz der <see cref="InvalidStatusTransitionException"/>.</summary>
    public InvalidStatusTransitionException(WorkItemStatus von, WorkItemStatus nach)
        : base($"Statusübergang von '{von.ToDisplayText()}' nach '{nach.ToDisplayText()}' ist nicht erlaubt.")
    {
        From = von;
        To = nach;
    }

    /// <summary>Ausgangsstatus.</summary>
    public WorkItemStatus From { get; }

    /// <summary>Zielstatus.</summary>
    public WorkItemStatus To { get; }
}
