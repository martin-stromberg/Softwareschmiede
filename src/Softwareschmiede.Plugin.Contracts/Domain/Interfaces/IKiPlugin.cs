using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Domain.Interfaces;

/// <summary>KI-Plugin Interface für KI-gestützte Entwicklung.</summary>
public interface IKiPlugin : IPlugin
{
    /// <summary>Gibt die verfügbaren Agenten im Agentenpaket zurück.</summary>
    /// <param name="agentPackagePath">Pfad zum Agentenpaket.</param>
    /// <param name="ct">Cancellation Token.</param>
    Task<IEnumerable<AgentInfo>> GetAvailableAgentsAsync(string agentPackagePath, CancellationToken ct = default);

    /// <summary>
    /// Prüft ob das Agentenpaket mit diesem KI-Plugin kompatibel ist.
    /// Für GitHub Copilot ist ein Paket kompatibel, wenn es einen <c>.github</c>-Ordner im Root enthält.
    /// </summary>
    /// <param name="agentPackagePath">Pfad zum Agentenpaket.</param>
    /// <param name="ct">Cancellation Token.</param>
    /// <returns><c>true</c> wenn das Paket kompatibel ist, sonst <c>false</c>.</returns>
    Task<bool> IsAgentPackageCompatibleAsync(string agentPackagePath, CancellationToken ct = default);

    /// <summary>Kopiert das Agentenpaket ins Repository (z.B. nach .github/).</summary>
    /// <param name="agentPackagePath">Pfad zum Agentenpaket.</param>
    /// <param name="localRepoPath">Lokaler Pfad des geklonten Repositories.</param>
    /// <param name="ct">Cancellation Token.</param>
    Task DeployAgentPackageAsync(string agentPackagePath, string localRepoPath, CancellationToken ct = default);

    /// <summary>Startet den KI-Entwicklungsprozess und streamt die Ausgabe.</summary>
    /// <param name="prompt">Anforderungsbeschreibung für den Agenten.</param>
    /// <param name="agent">Informationen über den zu verwendenden Agenten.</param>
    /// <param name="localRepoPath">Lokaler Pfad des geklonten Repositories.</param>
    /// <param name="model">
    /// Optionales KI-Modell (z.B. <c>gpt-4o</c>, <c>claude-3-7-sonnet</c>).
    /// Wird <c>null</c> übergeben, erfolgt die Modellauswahl automatisch durch den Anbieter.
    /// </param>
    /// <param name="executionId">
    /// Optionale Ausführungs-ID zur Korrelation eines KI-Laufs.
    /// Erwartet wird eine GUID (beliebiges .NET GUID-Format), intern normalisiert auf Format <c>N</c>.
    /// </param>
    /// <param name="ct">Cancellation Token.</param>
    IAsyncEnumerable<string> StartDevelopmentAsync(
        string prompt,
        AgentInfo agent,
        string localRepoPath,
        string? model,
        string? executionId,
        CancellationToken ct = default);

    /// <summary>Führt Tests im Repository aus.</summary>
    /// <param name="localRepoPath">Lokaler Pfad des geklonten Repositories.</param>
    /// <param name="ct">Cancellation Token.</param>
    Task<TestResult> RunTestsAsync(string localRepoPath, CancellationToken ct = default);

    /// <summary>Prüft ob das Plugin verfügbar ist.</summary>
    /// <param name="ct">Cancellation Token.</param>
    Task<bool> CheckHealthAsync(CancellationToken ct = default);
}
