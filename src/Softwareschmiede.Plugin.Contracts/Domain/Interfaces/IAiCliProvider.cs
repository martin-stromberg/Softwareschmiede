using System.Diagnostics;

namespace Softwareschmiede.Domain.Interfaces;

/// <summary>Interface für KI-CLI-Provider-Plugins. Startet den CLI-Prozess und gibt ihn zurück.</summary>
public interface IAiCliProvider : IPlugin
{
    /// <summary>Startet den CLI-Prozess im angegebenen Arbeitsverzeichnis und gibt den Prozess zurück.</summary>
    /// <param name="workingDirectory">Lokales Arbeitsverzeichnis (geklontes Repository).</param>
    /// <param name="sessionParameter">Optionaler Session-Parameter (z.B. für --continue-session).</param>
    /// <param name="ct">Cancellation Token.</param>
    /// <returns>Der gestartete CLI-Prozess.</returns>
    Task<Process> StartCliAsync(string workingDirectory, string? sessionParameter = null, CancellationToken ct = default);

    /// <summary>Gibt an, ob das Plugin Session-Fortsetzung unterstützt (z.B. --continue-session).</summary>
    bool SupportsSessionContinuation();
}
