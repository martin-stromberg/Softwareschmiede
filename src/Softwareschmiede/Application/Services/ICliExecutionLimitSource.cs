namespace Softwareschmiede.Application.Services;

/// <summary>
/// Liefert die für die Ausführungsbegrenzung benötigte Belegung laufender CLI-Ausführungen:
/// welche Aufgaben gerade einen laufenden CLI-Prozess besitzen und welche lebende
/// SCM-Promptsitzung daran gebunden ist. Implementiert von <see cref="AiExecutionService"/>.
/// </summary>
public interface ICliExecutionLimitSource
{
    /// <summary>Gibt die Aufgaben-IDs mit aktuell laufendem CLI-Prozess-Handle zurück.</summary>
    /// <returns>Aufgaben-IDs laufender CLI-Ausführungen (höchstens ein Eintrag je Aufgabe).</returns>
    IReadOnlyCollection<Guid> GetLaufendeAufgabenIds();

    /// <summary>Gibt die für eine Aufgabe registrierte SCM-Promptsitzung zurück, sofern vorhanden.</summary>
    /// <param name="workItemId">Aufgaben-ID.</param>
    /// <returns>Adapter der SCM-Promptsitzung oder <c>null</c>.</returns>
    IScmPromptSessionAdapter? GetScmPromptSession(Guid workItemId);
}
