using Softwareschmiede.Domain.Enums;

namespace Softwareschmiede.Domain.Interfaces;

/// <summary>IDE-Plugin Interface. Prüft die Kompatibilität zu einem Repository und öffnet es in der IDE.</summary>
public interface IIdePlugin : IPlugin
{
    /// <summary>Prüft die Kompatibilität des Plugins zum angegebenen Repository.</summary>
    /// <param name="repositoryPath">Pfad des zu prüfenden Repositories.</param>
    /// <param name="ct">Cancellation Token.</param>
    /// <returns>Das Kompatibilitätsergebnis für das Repository.</returns>
    Task<IdePluginCompatibility> CheckCompatibilityAsync(string repositoryPath, CancellationToken ct = default);

    /// <summary>Öffnet das Repository in der IDE.</summary>
    /// <param name="repositoryPath">Pfad des zu öffnenden Repositories.</param>
    /// <param name="ct">Cancellation Token.</param>
    Task OpenRepositoryAsync(string repositoryPath, CancellationToken ct = default);
}
