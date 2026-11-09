using Softwareschmiede.Domain.Enums;

namespace Softwareschmiede.Infrastructure.Terminal;

/// <summary>
/// Zentrales Mapping des Laufzeitstatus einer CLI-Sitzung (<see cref="CliRuntimeStatus"/>) auf den
/// persistierten Domain-Substatus <see cref="AufgabeLaufStatus"/>. Einzige Quelle der Wahrheit für
/// diese Abbildung — vorher existierte sie als identischer Switch an mehreren Stellen
/// (CliProcessManager, TaskDetailViewModel, KiAusfuehrungsService) und konnte bei einer
/// Enum-Erweiterung auseinanderlaufen.
/// </summary>
public static class CliRuntimeStatusExtensions
{
    /// <summary>
    /// Bildet einen <see cref="CliRuntimeStatus"/> auf <see cref="AufgabeLaufStatus"/> ab:
    /// <see cref="CliRuntimeStatus.WartetAufEingabe"/> → <see cref="AufgabeLaufStatus.WartetAufEingabe"/>,
    /// <see cref="CliRuntimeStatus.Laeuft"/> → <see cref="AufgabeLaufStatus.Laeuft"/>,
    /// <see cref="CliRuntimeStatus.Inaktiv"/> → <c>null</c> (kein persistierbarer Substatus — das
    /// Beenden des Laufs läuft über die separaten <c>CliProcessStatus</c>-Ereignisse).
    /// Der <c>null</c>-Rückgabewert hat je nach Aufrufkontext unterschiedliche Bedeutung; Aufrufer
    /// wählen ihren Fallback selbst (z. B. <c>?? AufgabeLaufStatus.Laeuft</c> in
    /// <c>KiAusfuehrungsService.GetLiveLaufStatus</c>: kein/aktiver Substatus meldet sich dort als
    /// „Läuft").
    /// </summary>
    /// <param name="status">Der abzubildende Laufzeitstatus.</param>
    /// <returns>Der persistierbare Substatus, oder <c>null</c>.</returns>
    public static AufgabeLaufStatus? ToAufgabeLaufStatus(this CliRuntimeStatus status)
        => status switch
        {
            CliRuntimeStatus.WartetAufEingabe => AufgabeLaufStatus.WartetAufEingabe,
            CliRuntimeStatus.Laeuft => AufgabeLaufStatus.Laeuft,
            _ => null
        };
}
