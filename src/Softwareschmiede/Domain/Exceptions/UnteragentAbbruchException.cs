namespace Softwareschmiede.Domain.Exceptions;

/// <summary>Wird ausgelöst, wenn für einen Unteragenten eine Abbruchbedingung erkannt wurde (z. B. Tokenlimit- oder Laufzeitüberschreitung).</summary>
public sealed class UnteragentAbbruchException : InvalidOperationException
{
    /// <summary>Erstellt eine neue Instanz der <see cref="UnteragentAbbruchException"/>.</summary>
    /// <param name="agentId">Agent-Identifier des betroffenen Unteragenten.</param>
    /// <param name="grund">Grund für den Abbruch.</param>
    public UnteragentAbbruchException(string agentId, string grund)
        : base($"Unteragent '{agentId}' muss abgebrochen werden: {grund}")
    {
        AgentId = agentId;
        Grund = grund;
    }

    /// <summary>Agent-Identifier des betroffenen Unteragenten.</summary>
    public string AgentId { get; }

    /// <summary>Grund für den Abbruch.</summary>
    public string Grund { get; }
}
