namespace Softwareschmiede.Application.Services;

/// <summary>
/// Zählt die fachlich „laufenden" Automatisierungen — d. h. die aktiven Aufgaben, deren
/// <see cref="KiAusfuehrungsStatusKlassifikation"/> den Zustand
/// <see cref="Domain.Enums.KiAusfuehrungsZustand.Laeuft"/> ergibt.
/// </summary>
public interface ILaufendeAutomatisierungenZaehlung
{
    /// <summary>Zählt die aktuell als „läuft" klassifizierten Automatisierungen.</summary>
    /// <param name="ct">Token zum Abbrechen der Operation.</param>
    /// <returns>Die Anzahl laufender Automatisierungen.</returns>
    Task<int> ZaehleAsync(CancellationToken ct = default);
}
