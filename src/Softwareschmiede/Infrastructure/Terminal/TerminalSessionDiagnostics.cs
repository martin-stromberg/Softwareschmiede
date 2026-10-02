using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Infrastructure.Terminal;

/// <summary>Einzelne Preflight-Prüfung eines Terminal-Session-Starts.</summary>
/// <param name="Name">Name der Prüfung.</param>
/// <param name="Ok">Ergebnis der Prüfung.</param>
/// <param name="Detail">Zusätzlicher Diagnosehinweis, oder <c>null</c>.</param>
public sealed record TerminalPreflightCheck(string Name, bool Ok, string? Detail = null);

/// <summary>Empfohlenes Backend aus dem Preflight-Ergebnis.</summary>
public enum TerminalBackendEmpfehlung
{
    /// <summary>PTY-/ConPTY-Backend.</summary>
    Pty,

    /// <summary>Pipe-Fallback-Backend.</summary>
    Pipe,

    /// <summary>Start nicht möglich (harter Fehler).</summary>
    Fehler
}

/// <summary>Aggregiertes Ergebnis der Preflight-Diagnose eines Terminal-Session-Starts.</summary>
/// <param name="Checks">Die durchgeführten Einzelprüfungen.</param>
/// <param name="PtyVerfuegbar">Gibt an, ob ein Pseudo-Terminal (ConPTY) verfügbar ist.</param>
/// <param name="BackendEmpfehlung">Die abgeleitete Backend-Empfehlung.</param>
public sealed record TerminalPreflightResult(
    IReadOnlyList<TerminalPreflightCheck> Checks,
    bool PtyVerfuegbar,
    TerminalBackendEmpfehlung BackendEmpfehlung);

/// <summary>Führt die Preflight-Diagnose vor einem Terminal-Session-Start aus und protokolliert jeden
/// Einzelcheck. Bewusst frei von Datenbankzugriffen — ein Test-Override wird über den Parameter
/// <c>forcePtyUnavailable</c> hereingereicht.</summary>
public sealed class TerminalSessionDiagnostics
{
    private static readonly Version MinimumConPtyBuild = new(10, 0, 17763);

    private readonly TerminalSessionOptions _options;
    private readonly ILogger _logger;

    /// <summary>Erstellt eine neue Instanz von <see cref="TerminalSessionDiagnostics"/>.</summary>
    /// <param name="options">Terminal-Laufzeitparameter (für den Terminalgrößen-Check).</param>
    /// <param name="logger">Logger für die Protokollierung der Einzelchecks (optional).</param>
    public TerminalSessionDiagnostics(TerminalSessionOptions options, ILogger? logger = null)
    {
        _options = options;
        _logger = logger ?? NullLogger.Instance;
    }

    /// <summary>Führt alle Preflight-Prüfungen aus.</summary>
    /// <param name="spec">Die vom Plugin gelieferte (bereits normalisierte) Startbeschreibung.</param>
    /// <param name="resolution">Das Ergebnis der Executable-Auflösung.</param>
    /// <param name="healthCheck">Optionale Plugin-Health-Probe (<c>IKiPlugin.CheckHealthAsync</c> als
    /// Delegate); wird nur bei erfolgreicher Executable-Auflösung aufgerufen und ist nicht fatal.</param>
    /// <param name="forcePtyUnavailable">Erzwingt einen Fehlschlag des PTY-Verfügbarkeits-Checks (Debug-/Test-Hook).</param>
    /// <param name="ct">Abbruch-Token.</param>
    /// <returns>Das aggregierte <see cref="TerminalPreflightResult"/>.</returns>
    public async Task<TerminalPreflightResult> RunPreflightAsync(
        TerminalSessionStartSpec spec,
        TerminalExecutableResolution resolution,
        Func<CancellationToken, Task<bool>>? healthCheck,
        bool forcePtyUnavailable,
        CancellationToken ct)
    {
        var checks = new List<TerminalPreflightCheck>();

        var ptyVerfuegbar = !forcePtyUnavailable
            && OperatingSystem.IsWindows()
            && Environment.OSVersion.Version >= MinimumConPtyBuild;
        checks.Add(new TerminalPreflightCheck(
            "ConPTY-Verfügbarkeit",
            ptyVerfuegbar,
            forcePtyUnavailable
                ? "Fehlschlag durch Test-Override erzwungen (Terminal.ForcePtyUnavailable)"
                : $"OS-Build {Environment.OSVersion.Version} (Minimum: {MinimumConPtyBuild})"));

        var executableCheck = resolution.Status switch
        {
            TerminalExecutableStatus.Direct or TerminalExecutableStatus.CmdWrapped
                => new TerminalPreflightCheck("Executable", true, resolution.ResolvedPath),
            TerminalExecutableStatus.NotFound
                => new TerminalPreflightCheck("Executable", false, resolution.Detail ?? $"'{spec.FileName}' nicht gefunden"),
            _ => new TerminalPreflightCheck("Executable", false, resolution.Detail ?? $"'{resolution.ResolvedPath}' ist nicht ausführbar"),
        };
        checks.Add(executableCheck);

        if (executableCheck.Ok && healthCheck is not null)
        {
            try
            {
                var healthy = await healthCheck(ct).ConfigureAwait(false);
                checks.Add(new TerminalPreflightCheck("CLI-Health", healthy, healthy ? null : "Health-Check meldete false (nicht fatal — die Startfähigkeit sichert der Executable-Check)"));
            }
            catch (Exception ex)
            {
                checks.Add(new TerminalPreflightCheck("CLI-Health", false, $"Health-Check warf {ex.GetType().Name}: {ex.Message} (nicht fatal)"));
            }
        }
        else
        {
            checks.Add(new TerminalPreflightCheck("CLI-Health", true, "übersprungen (Executable nicht auflösbar oder kein Health-Check übergeben)"));
        }

        checks.Add(new TerminalPreflightCheck("Encoding", true, "UTF-8"));

        // Dieselbe Grenze wie die Hartvalidierung in TerminalSessionService.StartAsync:
        // DefaultCols/DefaultRows werden per short-Cast an PseudoConsole.Create übergeben.
        var sizeOk = _options.DefaultCols is > 0 and <= short.MaxValue
            && _options.DefaultRows is > 0 and <= short.MaxValue;
        checks.Add(new TerminalPreflightCheck("Terminalgröße", sizeOk, $"{_options.DefaultCols}x{_options.DefaultRows}"));

        checks.Add(new TerminalPreflightCheck("Pluginparameter", true, spec.OptionalParameters ?? spec.Arguments));

        foreach (var check in checks)
        {
            if (check.Ok)
                _logger.LogDebug("Terminal-Preflight {Check}: OK ({Detail})", check.Name, check.Detail);
            else
                _logger.LogWarning("Terminal-Preflight {Check}: fehlgeschlagen ({Detail})", check.Name, check.Detail);
        }

        // Einzige Backend-Entscheidungslogik (TerminalSessionService.SelectBackend konsumiert sie):
        // Fehler bei nicht startbarer Executable oder RequiresPty ohne verfügbare PTY; Pipe, wenn das
        // Plugin keine PTY-Unterstützung deklariert oder keine PTY verfügbar ist; sonst Pty.
        var backendEmpfehlung = !executableCheck.Ok
            ? TerminalBackendEmpfehlung.Fehler
            : spec.Capabilities.HasFlag(TerminalProviderCapabilities.RequiresPty) && !ptyVerfuegbar
                ? TerminalBackendEmpfehlung.Fehler
                : ptyVerfuegbar && spec.Capabilities.HasFlag(TerminalProviderCapabilities.SupportsPty)
                    ? TerminalBackendEmpfehlung.Pty
                    : TerminalBackendEmpfehlung.Pipe;

        return new TerminalPreflightResult(checks, ptyVerfuegbar, backendEmpfehlung);
    }
}
