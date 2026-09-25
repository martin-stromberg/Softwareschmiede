using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Infrastructure.Terminal;

/// <summary>Zentrale Session-Erzeugung für den interaktiven CLI-Pfad: löst <see cref="TerminalSessionStartSpec.FileName"/>
/// über <see cref="TerminalExecutableResolver"/> auf, führt die Preflight-Diagnose aus, wählt das Backend in fester
/// Reihenfolge und delegiert den eigentlichen Start an den jeweiligen <see cref="IPseudoConsoleProcessLauncher"/>.</summary>
public sealed class TerminalSessionService : ITerminalSessionFactory
{
    /// <summary>AppEinstellungen-Schlüssel des Debug-/Test-Hooks, der den PTY-Verfügbarkeits-Check zum
    /// Fehlschlagen zwingt (Auslöser für den Fallback-Diagnose-Test). Niemals implizit gesetzt.</summary>
    public const string ForcePtyUnavailableKey = "Terminal.ForcePtyUnavailable";

    /// <summary>Umgebungsvariable, die den E2E-Testmodus (Test-Datenbankpfad) kennzeichnet. Zentrale
    /// Definition — referenziert u. a. aus <c>App.xaml.cs</c> (IProzessStarter-Auswahl) und
    /// <see cref="Softwareschmiede.Infrastructure.Plugins.PluginManager"/>.</summary>
    public const string TestDatenbankPfadVariable = "SOFTWARESCHMIEDE_TEST_DB_PATH";

    private readonly IOptions<TerminalSessionOptions> _options;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IPseudoConsoleProcessLauncher _ptyLauncher;
    private readonly IPseudoConsoleProcessLauncher _pipeLauncher;
    private readonly ILogger<TerminalSessionService> _logger;

    /// <summary>Erstellt eine neue Instanz von <see cref="TerminalSessionService"/>.</summary>
    /// <param name="options">Terminal-Laufzeitparameter.</param>
    /// <param name="scopeFactory">Factory für DI-Scopes (Lesen des <see cref="ForcePtyUnavailableKey"/>-Test-Overrides).</param>
    /// <param name="ptyLauncher">Launcher für das PTY-/ConPTY-Backend.</param>
    /// <param name="pipeLauncher">Launcher für das Pipe-Fallback-Backend.</param>
    /// <param name="logger">Logger für Diagnosemeldungen.</param>
    public TerminalSessionService(
        IOptions<TerminalSessionOptions> options,
        IServiceScopeFactory scopeFactory,
        IPseudoConsoleProcessLauncher ptyLauncher,
        IPseudoConsoleProcessLauncher pipeLauncher,
        ILogger<TerminalSessionService> logger)
    {
        _options = options;
        _scopeFactory = scopeFactory;
        _ptyLauncher = ptyLauncher;
        _pipeLauncher = pipeLauncher;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<TerminalSessionStartResult> StartAsync(
        Guid aufgabeId,
        TerminalSessionStartSpec spec,
        ITerminalOutputSink? outputSink,
        Func<CancellationToken, Task<bool>>? healthCheck,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(spec);
        if (string.IsNullOrWhiteSpace(spec.FileName))
            throw new ArgumentException("TerminalSessionStartSpec.FileName darf nicht leer sein.", nameof(spec));

        var options = _options.Value;
        if (options.ReplayBufferByteBudget <= 0)
            throw new ArgumentOutOfRangeException(nameof(options), "TerminalSessionOptions.ReplayBufferByteBudget muss größer als 0 sein.");

        // DefaultCols/DefaultRows werden per short-Cast an PseudoConsole.Create übergeben — außerhalb
        // von 1..short.MaxValue entstünde ein ungültiges ConPTY, daher hier hart validieren.
        if (options.DefaultCols is <= 0 or > short.MaxValue || options.DefaultRows is <= 0 or > short.MaxValue)
            throw new ArgumentOutOfRangeException(
                nameof(options),
                $"TerminalSessionOptions.DefaultCols/DefaultRows müssen zwischen 1 und {short.MaxValue} liegen (aktuell: {options.DefaultCols}x{options.DefaultRows}).");

        var forcePtyUnavailable = await ReadForcePtyUnavailableAsync(ct).ConfigureAwait(false);

        var resolution = TerminalExecutableResolver.Resolve(spec);
        var preflight = await new TerminalSessionDiagnostics(options, _logger)
            .RunPreflightAsync(spec, resolution, healthCheck, forcePtyUnavailable, ct)
            .ConfigureAwait(false);

        if (resolution.Status is TerminalExecutableStatus.NotFound or TerminalExecutableStatus.NotExecutable)
        {
            WriteDiagnosis(outputSink, $"Executable '{spec.FileName}' nicht startbar", preflight);
            throw new InvalidOperationException(
                $"Die CLI-Executable '{spec.FileName}' (Plugin '{spec.PluginName}') kann nicht gestartet werden: {resolution.Detail}");
        }

        if (spec.Capabilities.HasFlag(TerminalProviderCapabilities.RequiresPty) && !preflight.PtyVerfuegbar)
        {
            WriteDiagnosis(outputSink, "Das Plugin erfordert ein Pseudo-Terminal (RequiresPty), das nicht verfügbar ist", preflight);
            throw new InvalidOperationException(
                $"Die CLI '{spec.PluginName}' erfordert ein Pseudo-Terminal (PTY), das auf diesem System nicht verfügbar ist.");
        }

        var launcher = SelectBackend(spec, preflight, outputSink);
        _logger.LogInformation(
            "Terminal-Session für Aufgabe {AufgabeId} starten (Backend: {Backend}, Executable: {FileName} {Arguments}).",
            aufgabeId,
            launcher.IsPseudoTerminal ? "ConPTY" : "Pipe",
            resolution.NormalizedSpec.FileName,
            resolution.NormalizedSpec.Arguments);

        return launcher.Start(aufgabeId, resolution.NormalizedSpec, outputSink);
    }

    /// <summary>Wählt das Backend: Der E2E-Testmodus erzwingt Pipe; ansonsten gilt die im Preflight
    /// abgeleitete <see cref="TerminalPreflightResult.BackendEmpfehlung"/> (einzige Entscheidungslogik).
    /// Die Fehlerfälle (Executable nicht auflösbar, RequiresPty ohne PTY) wurden zuvor bereits abgefangen.</summary>
    private IPseudoConsoleProcessLauncher SelectBackend(TerminalSessionStartSpec spec, TerminalPreflightResult preflight, ITerminalOutputSink? outputSink)
    {
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(TestDatenbankPfadVariable)))
        {
            WriteDiagnosis(outputSink, "Pipe-Backend gewählt (E2E-Testmodus)", preflight);
            return _pipeLauncher;
        }

        if (preflight.BackendEmpfehlung == TerminalBackendEmpfehlung.Pty)
            return _ptyLauncher;

        WriteDiagnosis(
            outputSink,
            !spec.Capabilities.HasFlag(TerminalProviderCapabilities.SupportsPty)
                ? $"Pipe-Backend gewählt (Plugin '{spec.PluginName}' deklariert keine PTY-Unterstützung)"
                : "Pipe-Backend gewählt (PTY nicht verfügbar)",
            preflight);
        return _pipeLauncher;
    }

    /// <summary>Schreibt eine <c>[Terminal-Diagnose]</c>-Markerzeile ausschließlich in die Protokoll-Senke
    /// (CliOutput-Protokoll — nicht in <c>TerminalReplayBuffer</c>/Terminal-Anzeige) und ins Log.</summary>
    private void WriteDiagnosis(ITerminalOutputSink? outputSink, string grund, TerminalPreflightResult preflight)
    {
        var checks = string.Join(", ", preflight.Checks.Select(c => $"{c.Name}={(c.Ok ? "OK" : "Fehler")}{(c.Detail is null ? string.Empty : $" [{c.Detail}]")}"));
        var message = $"[Terminal-Diagnose] {grund} | PtyVerfuegbar={preflight.PtyVerfuegbar} | Checks: {checks}";

        if (preflight.Checks.Any(c => !c.Ok))
            _logger.LogWarning("{Message}", message);
        else
            _logger.LogInformation("{Message}", message);

        try
        {
            outputSink?.OnOutputChunk(Encoding.UTF8.GetBytes(message + "\r\n"));
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Terminal-Diagnose-Marker konnte nicht an die Output-Senke geschrieben werden.");
        }
    }

    private async Task<bool> ReadForcePtyUnavailableAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var einstellungen = scope.ServiceProvider.GetRequiredService<AppEinstellungService>();
            var wert = await einstellungen.GetSettingAsync(ForcePtyUnavailableKey, ct).ConfigureAwait(false);
            return string.Equals(wert, "true", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Der Test-Override '{Key}' konnte nicht gelesen werden — wird als nicht gesetzt behandelt.", ForcePtyUnavailableKey);
            return false;
        }
    }
}
