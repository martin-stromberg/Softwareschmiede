using Softwareschmiede.Domain.Enums;

namespace Softwareschmiede.Application.Services;

/// <summary>
/// Gemeinsame Statusklassifikation der KI-Ausführung (Anzeige-Konverter der App und
/// fachliche Lauf-Zählung des Auto-Shutdown), damit beide Auswertungen nicht divergieren.
/// Stellt außerdem mit <see cref="IstAktuellPausiert"/> die Referenzdefinition des
/// Pausenkriteriums für alle Pausen-Auswertungen bereit.
/// </summary>
public static class KiAusfuehrungsStatusKlassifikation
{
    /// <summary>
    /// Referenzdefinition des Pausenkriteriums: <c>true</c>, wenn <paramref name="pausiertBisUtc"/>
    /// gesetzt ist und in der Zukunft liegt. <c>null</c> oder ein abgelaufener Zeitpunkt gelten
    /// als nicht pausiert.
    /// </summary>
    /// <param name="pausiertBisUtc">UTC-Pausenende der Aufgabe (<see cref="Domain.Entities.WorkItem.PausedUntilUtc"/>).</param>
    /// <param name="jetzt">Der als „jetzt" zu verwendende Zeitpunkt in UTC.</param>
    /// <returns><c>true</c>, wenn die Aufgabe aktuell pausiert ist, sonst <c>false</c>.</returns>
    public static bool IstAktuellPausiert(this DateTimeOffset? pausiertBisUtc, DateTimeOffset jetzt)
        => pausiertBisUtc > jetzt;

    /// <summary>
    /// Klassifiziert die in <paramref name="status"/> gebündelten Aufgabendaten in einen
    /// <see cref="AiExecutionState"/>. Im Zustand <see cref="AiExecutionState.Paused"/>
    /// enthält <paramref name="pausiertRestzeit"/> die verbleibende Pausenzeit.
    /// </summary>
    /// <param name="status">Die zu klassifizierenden Aufgabendaten.</param>
    /// <param name="pausiertRestzeit">Restliche Pausenzeit im Zustand <see cref="AiExecutionState.Paused"/>, sonst <see cref="TimeSpan.Zero"/>.</param>
    /// <returns>Der klassifizierte Zustand.</returns>
    public static AiExecutionState Klassifiziere(KiAusfuehrungsStatusDaten status, out TimeSpan pausiertRestzeit)
    {
        pausiertRestzeit = default;

        // Der Pausiert-Zweig steht bewusst vor allen übrigen Anzeigen: Eine aktive Pause überlagert
        // den Laufzeitstatus (die Kachel zeigt den Countdown statt "Läuft"/"Prompt in Wartestellung").
        var jetzt = DateTimeOffset.UtcNow;
        if (status.PausedUntilUtc is { } pausiertBis && status.PausedUntilUtc.IstAktuellPausiert(jetzt))
        {
            pausiertRestzeit = pausiertBis - jetzt;
            return AiExecutionState.Paused;
        }

        if (status.HasScheduledPrompt)
        {
            return AiExecutionState.PromptQueued;
        }

        // Live-Zweig (nur-aufwärts): Läuft nachweislich ein gehandelter Prozess, schlägt der
        // Live-Substatus vor der Auswertung der persistierten Laufdaten immer auf Laeuft/Wartet
        // durch — niemals auf Bereit (Issue 342: Fußzeile und Kachel speisen sich aus getrennten
        // Quellen, die ohne diesen Zweig divergieren können).
        if (status.LiveLaufStatus is { } liveLaufStatus)
        {
            return liveLaufStatus == WorkItemRunStatus.WaitingForInput
                ? AiExecutionState.Waiting
                : AiExecutionState.Running;
        }

        if (status.ExecutionStatus != WorkItemExecutionStatus.Active)
        {
            return AiExecutionState.Ready;
        }

        if (AufgabeLaufAktivitaet.IstAktiv(status.ActiveRunId, status.LastHeartbeatUtc, jetzt))
        {
            // LaufStatus bildet den Laufzeit-Substatus der CLI ab (siehe PseudoConsoleSession.RuntimeStatus,
            // persistiert über CliProcessManager/AufgabeService.AktivenLaufSicherstellenAsync). Ohne diesen
            // Substatus (null, z. B. beim klassischen Start ohne ConPTY) bleibt es beim Zustand "Läuft".
            return status.RunStatus == WorkItemRunStatus.WaitingForInput
                ? AiExecutionState.Waiting
                : AiExecutionState.Running;
        }

        if (status.Status == WorkItemStatus.Waiting)
        {
            return AiExecutionState.Waiting;
        }

        return AiExecutionState.Ready;
    }
}
