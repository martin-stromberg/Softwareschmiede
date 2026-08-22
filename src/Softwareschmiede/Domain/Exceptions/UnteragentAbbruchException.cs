namespace Softwareschmiede.Domain.Exceptions;

/// <summary>
/// Wird ausgelöst, wenn für einen Unteragenten eine Abbruchbedingung erkannt wurde (z. B. Tokenlimit- oder Laufzeitüberschreitung).
/// Erbt bewusst von <see cref="SoftwareschmiedeException"/> statt (zusätzlich) von <see cref="InvalidOperationException"/>: C# erlaubt nur
/// Einfachvererbung, und im gesamten Repository existiert kein produktiver <c>catch (InvalidOperationException)</c>-Handler, der auf den
/// Fang dieser Exception angewiesen ist (die einzigen produktiven <c>catch (InvalidOperationException)</c>-Stellen behandeln unabhängige
/// Process-/Prozess-Fehler in <c>CliRunner</c>/<c>TaskDetailView.xaml.cs</c> und umschließen keinen Aufruf von
/// <see cref="Softwareschmiede.Application.Services.UnteragentGovernanceService.ValidiereFehlerBedingungAsync"/>). Auch die bestehenden
/// Tests prüfen bereits explizit auf <see cref="UnteragentAbbruchException"/> statt auf <see cref="InvalidOperationException"/>. Damit war
/// keine Erhaltung der bisherigen impliziten <see cref="InvalidOperationException"/>-Fangbarkeit nötig.
/// </summary>
public sealed class UnteragentAbbruchException : SoftwareschmiedeException
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
