using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Domain.Interfaces;

/// <summary>IDE-Plugin Interface. Prüft die Kompatibilität zu einem Repository und öffnet es in der IDE.</summary>
public interface IIdePlugin : IPlugin
{
    /// <summary>Prüft die Kompatibilität des Plugins zum angegebenen Repository.</summary>
    /// <param name="repositoryPath">Pfad des zu prüfenden Repositories.</param>
    /// <param name="ct">Cancellation Token.</param>
    /// <returns>Das Kompatibilitätsergebnis für das Repository.</returns>
    Task<IdePluginCompatibility> CheckCompatibilityAsync(string repositoryPath, CancellationToken ct = default);

    /// <summary>Ermittelt alle verfügbaren Einstiegspunkte für das angegebene Repository.</summary>
    /// <param name="repositoryPath">Pfad des zu durchsuchenden Repositories.</param>
    /// <param name="ct">Cancellation Token.</param>
    /// <returns>Die gefundenen Einstiegspunkte; kann leer sein, wenn keine Kandidaten vorhanden sind.</returns>
    Task<IReadOnlyList<IdeEntryPoint>> FindEntryPointsAsync(string repositoryPath, CancellationToken ct = default);

    /// <summary>Öffnet den übergebenen Einstiegspunkt in der IDE.</summary>
    /// <param name="entryPoint">Der zu öffnende Einstiegspunkt.</param>
    /// <param name="ct">Cancellation Token.</param>
    Task OpenEntryPointAsync(IdeEntryPoint entryPoint, CancellationToken ct = default);
}
