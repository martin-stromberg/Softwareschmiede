using Softwareschmiede.Domain.Enums;

namespace Softwareschmiede.Application.Services;

/// <summary>
/// Gemeinsame Statusklassifikation der KI-Ausführung (Anzeige-Konverter der App und
/// fachliche Lauf-Zählung des Auto-Shutdown), damit beide Auswertungen nicht divergieren.
/// </summary>
public static class KiAusfuehrungsStatusKlassifikation
{
    /// <summary>
    /// Klassifiziert die in <paramref name="status"/> gebündelten Aufgabendaten in einen
    /// <see cref="KiAusfuehrungsZustand"/>. Im Zustand <see cref="KiAusfuehrungsZustand.Pausiert"/>
    /// enthält <paramref name="pausiertRestzeit"/> die verbleibende Pausenzeit.
    /// </summary>
    /// <param name="status">Die zu klassifizierenden Aufgabendaten.</param>
    /// <param name="pausiertRestzeit">Restliche Pausenzeit im Zustand <see cref="KiAusfuehrungsZustand.Pausiert"/>, sonst <see cref="TimeSpan.Zero"/>.</param>
    /// <returns>Der klassifizierte Zustand.</returns>
    public static KiAusfuehrungsZustand Klassifiziere(KiAusfuehrungsStatusDaten status, out TimeSpan pausiertRestzeit)
    {
        pausiertRestzeit = default;

        // Der Pausiert-Zweig steht bewusst vor allen übrigen Anzeigen: Eine aktive Pause überlagert
        // den Laufzeitstatus (die Kachel zeigt den Countdown statt "Läuft"/"Prompt in Wartestellung").
        if (status.PausiertBisUtc is { } pausiertBis && pausiertBis > DateTimeOffset.UtcNow)
        {
            pausiertRestzeit = pausiertBis - DateTimeOffset.UtcNow;
            return KiAusfuehrungsZustand.Pausiert;
        }

        if (status.HasScheduledPrompt)
        {
            return KiAusfuehrungsZustand.PromptInWartestellung;
        }

        // Live-Zweig (nur-aufwärts): Läuft nachweislich ein gehandelter Prozess, schlägt der
        // Live-Substatus vor der Auswertung der persistierten Laufdaten immer auf Laeuft/Wartet
        // durch — niemals auf Bereit (Issue 342: Fußzeile und Kachel speisen sich aus getrennten
        // Quellen, die ohne diesen Zweig divergieren können).
        if (status.LiveLaufStatus is { } liveLaufStatus)
        {
            return liveLaufStatus == AufgabeLaufStatus.WartetAufEingabe
                ? KiAusfuehrungsZustand.Wartet
                : KiAusfuehrungsZustand.Laeuft;
        }

        if (status.AusfuehrungsStatus != AufgabeAusfuehrungsStatus.Aktiv)
        {
            return KiAusfuehrungsZustand.Bereit;
        }

        if (AufgabeLaufAktivitaet.IstAktiv(status.AktiveRunId, status.LastHeartbeatUtc, DateTimeOffset.UtcNow))
        {
            // LaufStatus bildet den Laufzeit-Substatus der CLI ab (siehe PseudoConsoleSession.RuntimeStatus,
            // persistiert über CliProcessManager/AufgabeService.AktivenLaufSicherstellenAsync). Ohne diesen
            // Substatus (null, z. B. beim klassischen Start ohne ConPTY) bleibt es beim Zustand "Läuft".
            return status.LaufStatus == AufgabeLaufStatus.WartetAufEingabe
                ? KiAusfuehrungsZustand.Wartet
                : KiAusfuehrungsZustand.Laeuft;
        }

        if (status.Status == AufgabeStatus.Wartend)
        {
            return KiAusfuehrungsZustand.Wartet;
        }

        return KiAusfuehrungsZustand.Bereit;
    }
}
