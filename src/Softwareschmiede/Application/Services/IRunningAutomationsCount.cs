namespace Softwareschmiede.Application.Services;

/// <summary>
/// Zählt die fachlich „laufenden" Automatisierungen — d. h. die aktiven Aufgaben, deren
/// <see cref="AiExecutionStatusClassification"/> den Zustand
/// <see cref="Domain.Enums.AiExecutionState.Running"/> ergibt.
/// </summary>
public interface IRunningAutomationsCount
{
    /// <summary>Zählt die aktuell als „läuft" klassifizierten Automatisierungen.</summary>
    /// <param name="ct">Token zum Abbrechen der Operation.</param>
    /// <returns>Die Anzahl laufender Automatisierungen.</returns>
    Task<int> CountAsync(CancellationToken ct = default);
}
