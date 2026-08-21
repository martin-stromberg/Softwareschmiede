using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Exceptions;

namespace Softwareschmiede.Application.Services;

/// <summary>Erzwingt Governance-Regeln für Unteragenten: Isolationsbereich, Permission-Checks, Fehlervalidierung.</summary>
public sealed class UnteragentGovernanceService
{
    private static readonly HashSet<UnteragentAktion> VerboteneAktionen =
    [
        UnteragentAktion.PullRequestErstellen,
        UnteragentAktion.SkillModifizieren
    ];

    private readonly ILogger<UnteragentGovernanceService> _logger;

    /// <inheritdoc cref="UnteragentGovernanceService"/>
    public UnteragentGovernanceService(ILogger<UnteragentGovernanceService> logger) => _logger = logger;

    /// <summary>Validiert, dass ein Unteragent nur in seinem eigenen Bereich (<see cref="UnteragentSpezifikation.AgentDirectory"/>) arbeitet und keine grundsätzlich verbotene Aktion ausführt.</summary>
    /// <param name="unteragent">Der Unteragent, dessen Arbeitsbereich als erlaubter Basispfad dient.</param>
    /// <param name="aktion">Die auszuführende Aktion.</param>
    /// <param name="zielPfad">Der zu prüfende Zielpfad.</param>
    /// <returns><see langword="true"/>, wenn die Aktion erlaubt ist, sonst <see langword="false"/>.</returns>
    public bool VerifiziereBerechtigung(UnteragentSpezifikation unteragent, UnteragentAktion aktion, string zielPfad)
    {
        ArgumentNullException.ThrowIfNull(unteragent);

        return VerifiziereBerechtigung(unteragent.AgentDirectory, aktion, zielPfad, unteragent.AgentId);
    }

    /// <summary>Validiert, dass ein Zielpfad innerhalb eines vorgegebenen Basispfads liegt und keine grundsätzlich verbotene Aktion vorliegt. Dient Aufrufern, die noch keine persistierte <see cref="UnteragentSpezifikation"/> besitzen (z. B. Prüfung des Arbeitsverzeichnisses vor dessen Erzeugung).</summary>
    /// <param name="erlaubterBasisPfad">Der Basispfad, innerhalb dessen der Zielpfad liegen muss.</param>
    /// <param name="aktion">Die auszuführende Aktion.</param>
    /// <param name="zielPfad">Der zu prüfende Zielpfad.</param>
    /// <param name="agentIdFuerLogging">Die Agent-Id, die in Log-Meldungen zur Nachverfolgung verwendet wird.</param>
    /// <returns><see langword="true"/>, wenn die Aktion erlaubt ist, sonst <see langword="false"/>.</returns>
    public bool VerifiziereBerechtigung(string erlaubterBasisPfad, UnteragentAktion aktion, string zielPfad, string agentIdFuerLogging)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(erlaubterBasisPfad);
        ArgumentException.ThrowIfNullOrWhiteSpace(zielPfad);
        ArgumentException.ThrowIfNullOrWhiteSpace(agentIdFuerLogging);

        if (VerboteneAktionen.Contains(aktion))
        {
            _logger.LogWarning("Unteragent {AgentId}: Aktion '{Aktion}' ist grundsätzlich verboten.", agentIdFuerLogging, aktion);
            return false;
        }

        var normalizedBasisPfad = NormalizePath(erlaubterBasisPfad);
        var normalizedZiel = NormalizePath(zielPfad);

        var erlaubt = normalizedZiel.StartsWith(normalizedBasisPfad, StringComparison.OrdinalIgnoreCase);
        if (!erlaubt)
        {
            _logger.LogWarning(
                "Unteragent {AgentId}: Zugriff auf '{ZielPfad}' außerhalb des eigenen Bereichs '{BasisPfad}' verweigert.",
                agentIdFuerLogging,
                zielPfad,
                erlaubterBasisPfad);
        }

        return erlaubt;
    }

    /// <summary>Prüft anhand von task_state.json im Arbeitsbereich des Unteragenten auf Abbruchbedingungen (Tokenlimit, Laufzeitüberschreitung).</summary>
    public async Task ValidiereFehlerBedingungAsync(UnteragentSpezifikation unteragent, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(unteragent);

        var statePfad = Path.Combine(unteragent.AgentDirectory, "task_state.json");
        if (!File.Exists(statePfad))
        {
            return;
        }

        var json = await File.ReadAllTextAsync(statePfad, ct);
        var state = JsonSerializer.Deserialize<UnteragentLaufzeitState>(json);
        if (state is null)
        {
            return;
        }

        if (state.TokenLimit > 0 && state.TokensUsed > state.TokenLimit)
        {
            throw new UnteragentAbbruchException(unteragent.AgentId, $"Tokenlimit überschritten ({state.TokensUsed}/{state.TokenLimit}).");
        }

        if (state.RuntimeLimitMinutes > 0 && DateTimeOffset.UtcNow - state.StartedUtc > TimeSpan.FromMinutes(state.RuntimeLimitMinutes))
        {
            throw new UnteragentAbbruchException(unteragent.AgentId, $"Laufzeitlimit von {state.RuntimeLimitMinutes} Minuten überschritten.");
        }
    }

    private static string NormalizePath(string pfad)
    {
        var full = Path.GetFullPath(pfad).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return full + Path.DirectorySeparatorChar;
    }

    private sealed record UnteragentLaufzeitState(
        [property: JsonPropertyName("tokens_used")] int TokensUsed,
        [property: JsonPropertyName("token_limit")] int TokenLimit,
        [property: JsonPropertyName("started_utc")] DateTimeOffset StartedUtc,
        [property: JsonPropertyName("runtime_limit_minutes")] int RuntimeLimitMinutes
    );
}
