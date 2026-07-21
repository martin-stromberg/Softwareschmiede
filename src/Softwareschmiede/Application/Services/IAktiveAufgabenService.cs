using Softwareschmiede.Domain.Entities;

namespace Softwareschmiede.Application.Services;

/// <summary>Stellt die Datenquelle fuer die Seitenleisten-Anzeige aktiver Aufgaben bereit.</summary>
public interface IAktiveAufgabenService
{
    /// <summary>Gibt alle aktiven Aufgaben fuer die Seitenleisten-Anzeige zurueck.</summary>
    /// <param name="ct">Token zum Abbrechen der Operation.</param>
    /// <returns>Die aktiven Aufgaben.</returns>
    Task<List<Aufgabe>> GetAktiveAufgabenAsync(CancellationToken ct = default);
}
