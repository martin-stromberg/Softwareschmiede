using System.Diagnostics;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Domain.Interfaces;

/// <summary>KI-Plugin Interface. Startet den CLI-Prozess und gibt ihn zurück.</summary>
public interface IKiPlugin : IPlugin
{
    /// <summary>Startet den CLI-Prozess mit optionalen Parametern und gibt ProcessStartInfo zurück.</summary>
    /// <param name="localRepoPath">Lokaler Pfad des Arbeitsverzeichnisses.</param>
    /// <param name="parameters">Optionale Parameter (z.B. Session-ID für --continue).</param>
    /// <param name="ct">Cancellation Token.</param>
    /// <returns>ProcessStartInfo zum Starten des CLI-Prozesses.</returns>
    Task<ProcessStartInfo> StartCliAsync(string localRepoPath, string? parameters = null, CancellationToken ct = default);

    /// <summary>Gibt einen Hinweis auf den erwarteten Fenstertitel des CLI-Prozesses zurück (optional).</summary>
    /// <param name="aufgabeId">Aufgaben-ID für Identifikation.</param>
    string GetProcessWindowTitle(Guid aufgabeId);

    /// <summary>Gibt an, ob das Plugin Session-Fortsetzung unterstützt.</summary>
    bool SupportsSessionContinuation();

    /// <summary>Prüft ob das Plugin verfügbar ist.</summary>
    /// <param name="ct">Cancellation Token.</param>
    Task<bool> CheckHealthAsync(CancellationToken ct = default);
}
