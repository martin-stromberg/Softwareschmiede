using System.Text;
using System.Text.RegularExpressions;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Infrastructure.Terminal;

namespace Softwareschmiede.Application.Services;

/// <summary>Nachgewiesener Eingabestatus, unabhängig von der I/O-Ruhe-Heuristik.</summary>
public enum ScmPromptReadyStatus
{
    /// <summary>Kein verifizierter Eingabeprompt.</summary>
    Unknown,
    /// <summary>Bestätigter KI-Eingabeprompt für den aktuellen Lauf.</summary>
    Ready,
    /// <summary>Ein Turn oder eine Benutzereingabe läuft.</summary>
    Busy,
    /// <summary>Die CLI wartet auf eine Vertrauensentscheidung für das Arbeitsverzeichnis.</summary>
    TrustPrompt,
    /// <summary>Der gebundene Lauf ist beendet oder fehlgeschlagen.</summary>
    Exited,
}

/// <summary>Rungebundene sichere SCM-Texteingabe und Prüfung des lokalen Antwortkanals.</summary>
public interface IScmPromptSessionAdapter
{
    /// <summary>Identität des tatsächlichen Prozessstarts.</summary>
    Guid CliRunId { get; }
    /// <summary>Das beim Start erfasste tatsächliche Arbeitsverzeichnis.</summary>
    string WorkingDirectory { get; }
    /// <summary>Beim Start gewähltes Plugin.</summary>
    string PluginPrefix { get; }
    /// <summary>Gebundene Terminalsession.</summary>
    ITerminalSession Session { get; }
    /// <summary>Prozess und Session leben noch.</summary>
    bool IsAlive { get; }
    /// <summary>Eine Eingabe darf einen beschäftigten Turn nicht unterbrechen.</summary>
    bool IsBusy { get; }
    /// <summary>Aktueller Nachweis einer KI-Eingabe.</summary>
    ScmPromptReadyStatus ReadyStatus { get; }
    /// <summary>Erkannter CLI-Typ und Versionszustand des gebundenen Laufs.</summary>
    ScmCliRecognition CliRecognition { get; }
    /// <summary>Wartet auf ein echtes Promptsignal, nie allein auf I/O-Ruhe.</summary>
    Task<ScmPromptReadyStatus> WaitForInputReadyAsync(CancellationToken ct = default);
    /// <summary>Prüft den Startvertrag und fordert bei Bedarf eine separate tatsächliche Dateitool-Probe an.</summary>
    Task<ScmExchangeAccess> CheckExchangeAccessAsync(string exchangePath, CancellationToken ct = default);
    /// <summary>Explizite einmalige Bestätigung des sichtbaren aktuellen Laufs.</summary>
    bool ConfirmReady(Guid runId);
    /// <summary>Prüft Sessionidentität und Ready erneut unmittelbar vor genau einer Texteingabe.</summary>
    Task WritePromptAsync(string prompt, CancellationToken ct = default);
}

/// <summary>
/// Adapter für die getesteten Codex-/Claude-/Devin-Promptformate und den deterministischen JSON-Simulator.
/// Shells, Alt-Sessions und unbekannte Promptformate bleiben sichtbar ungeklärt; erkannte CLIs mit einer
/// Version außerhalb der unterstützten Bereiche werden mit Name und Version benannt.
/// </summary>
public sealed class ScmPromptSessionAdapter : IScmPromptSessionAdapter, IDisposable
{
    /// <summary>Frist, nach der eine unbeantwortete Kanalprobe bei sichtbarer Bereitschaft erneut zugestellt wird.</summary>
    private static readonly TimeSpan AccessProbeResendDelay = TimeSpan.FromSeconds(60);
    /// <summary>Maximale Anzahl von Kanalprobe-Sendungen pro gebundenem Lauf.</summary>
    private const int AccessProbeMaxSends = 3;
    /// <summary>Bewusst gepinnte Blockierdialog-Texte; unbekannte Dialoge bleiben wie bisher Unknown.</summary>
    private static readonly string[] TrustDialogMarkers =
    [
        "do you trust the files in this folder",
        "is this a project you created or one you trust",
        "do you trust the contents of this directory",
        "do you trust the authors of this directory",
    ];
    private readonly Guid _taskId;
    private readonly ScmExchangeAccess _access;
    private readonly IReadOnlyList<ScmCliConfigurationFile> _files;
    private readonly Func<bool> _isCurrent;
    private readonly TimeProvider _time;
    private readonly object _sync = new();
    private readonly StringBuilder _startupOutput = new();
    private readonly ScmEventExchangeProtocol _probeProtocol = new();
    private ScmExchangeAccessProbe? _accessProbe;
    private DateTimeOffset _accessProbeSentUtc;
    private int _accessProbeSends;
    private TaskCompletionSource _changed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private ScmPromptReadyStatus _ready;
    private ScmCliRecognition _cliRecognition;
    private bool _formatUserConfirmed;
    private bool _manuallyConfirmed;
    private long _inputRevision = -1;
    private long _outputRevision;

    /// <summary>Bindet StartSpec, erfasste Dateirechte und eine Session ohne zusätzliche Freigaben.</summary>
    public ScmPromptSessionAdapter(Guid taskId, Guid runId, string pluginPrefix, TerminalSessionStartSpec spec,
        ITerminalSession session, IReadOnlyList<ScmCliConfigurationFile> files, Func<bool> isCurrent,
        TimeProvider? timeProvider = null)
    {
        _taskId = taskId;
        CliRunId = runId;
        PluginPrefix = pluginPrefix;
        Session = session;
        WorkingDirectory = string.IsNullOrWhiteSpace(spec.WorkingDirectory) ? string.Empty : Path.GetFullPath(spec.WorkingDirectory);
        _files = files;
        _isCurrent = isCurrent;
        _time = timeProvider ?? TimeProvider.System;
        _access = ScmCliAccessEvaluator.Evaluate(pluginPrefix, spec, runId, files);
        _cliRecognition = ScmCliPromptProfile.TryGet(pluginPrefix, out var profile)
            ? new ScmCliRecognition(ScmCliRecognitionState.Unknown, profile.DisplayName, null, profile.SupportedVersionsText)
            : new ScmCliRecognition(ScmCliRecognitionState.Unknown, null, null, null);
        session.OutputChunk += OnOutput;
        session.BufferChanged += OnBufferChanged;
        session.Exited += OnExited;
        session.Failed += OnFailed;
        RefreshScreen();
    }

    /// <inheritdoc/>
    public Guid CliRunId { get; }
    /// <inheritdoc/>
    public string WorkingDirectory { get; }
    /// <inheritdoc/>
    public string PluginPrefix { get; }
    /// <inheritdoc/>
    public ITerminalSession Session { get; }
    /// <inheritdoc/>
    public bool IsAlive
    {
        get
        {
            try { return _isCurrent() && Session.Failure is null && Session.ExitCode is null && !Session.Process.HasExited; }
            catch (InvalidOperationException) { return false; }
        }
    }
    /// <inheritdoc/>
    public bool IsBusy => ReadyStatus == ScmPromptReadyStatus.Busy;
    /// <inheritdoc/>
    public ScmPromptReadyStatus ReadyStatus { get { if (!IsAlive) return ScmPromptReadyStatus.Exited; lock (_sync) return _ready; } }
    /// <inheritdoc/>
    public ScmCliRecognition CliRecognition { get { lock (_sync) return _cliRecognition; } }

    /// <inheritdoc/>
    public async Task<ScmPromptReadyStatus> WaitForInputReadyAsync(CancellationToken ct = default)
    {
        RefreshScreen();
        var current = ReadyStatus;
        if (current is ScmPromptReadyStatus.Ready or ScmPromptReadyStatus.Exited) return current;
        Task signal;
        lock (_sync) signal = _changed.Task;
        await Task.WhenAny(signal, Task.Delay(TimeSpan.FromSeconds(2), ct)).ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();
        return ReadyStatus;
    }

    /// <inheritdoc/>
    public Task<ScmExchangeAccess> CheckExchangeAccessAsync(string exchangePath, CancellationToken ct = default)
        => ScmTaskCoordinator.Shared.RunAsync(_taskId, () => CheckExchangeAccessCoreAsync(exchangePath, ct), ct);

    private async Task<ScmExchangeAccess> CheckExchangeAccessCoreAsync(string exchangePath, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var access = _access;
        // Einmaliger Schnappschuss unter dem Sperrobjekt: Bedingung und gemeldeter Grund müssen
        // aus demselben Erkennungsstand stammen, auch wenn parallel Session-Ereignisse eintreffen.
        ScmCliRecognition recognition;
        bool formatUserConfirmed;
        lock (_sync) { recognition = _cliRecognition; formatUserConfirmed = _formatUserConfirmed; }
        if (!IsAlive) access = access with { Status = ScmExchangeAccessStatus.Unknown, Reason = "CLI-Sitzung hat sich geändert oder wurde beendet." };
        else if (!ScmCliAccessEvaluator.IsUnchanged(_files))
            access = access with { Status = ScmExchangeAccessStatus.Unknown, Reason = "CLI-Konfiguration wurde seit dem Start geändert; regulärer Neustart erforderlich." };
        else
        {
            try
            {
                ScmEventExchangeProtocol.EnsureSafePath(exchangePath);
                var root = Path.Combine(WorkingDirectory, ScmEventExchangeProtocol.DirectoryName);
                var relative = Path.GetRelativePath(root, Path.GetFullPath(exchangePath));
                if (relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) || Path.IsPathRooted(relative))
                    access = access with { Status = ScmExchangeAccessStatus.Denied, Reason = "Antwortkanal liegt außerhalb der erfassten CLI-Schreibwurzel." };
                else if (recognition.State != ScmCliRecognitionState.Supported && !formatUserConfirmed &&
                    access.Status == ScmExchangeAccessStatus.Supported)
                    access = access with { Status = ScmExchangeAccessStatus.Unknown, Reason = ScmCliWaitReason.Describe(recognition) };
                else if (access.Status == ScmExchangeAccessStatus.Supported)
                {
                    // A normal CLI parent says nothing about the token/sandbox of its file tools.
                    // The simulator has an explicit in-process transport contract; real CLIs must
                    // demonstrate actual I/O under the identical private channel ACL first.
                    if (PluginPrefix != "Softwareschmiede.KiSimulator")
                    {
                        var capabilities = ScmRestrictedProcessAccess.CaptureWorkspaceCapabilities(WorkingDirectory);
                        if (_accessProbe is not null && !_accessProbe.RestrictedWriteSids.SequenceEqual(capabilities))
                            access = access with { Status = ScmExchangeAccessStatus.Unknown, Reason = "Schreibidentitäten des Arbeitsbereichs wurden geändert; regulärer Neustart erforderlich." };
                        else if (_accessProbe is not null && await _probeProtocol.ReadAccessProbeAsync(_accessProbe, ct).ConfigureAwait(false))
                            access = access with { RestrictedWriteSids = capabilities, Reason = "Dateitool hat Lesen, Schreiben und Umbenennen im rungebundenen Antwortkanal nachgewiesen." };
                        else
                        {
                            if (_accessProbe is null && ReadyStatus == ScmPromptReadyStatus.Ready)
                            {
                                _accessProbe = await _probeProtocol.PrepareAccessProbeAsync(CliRunId, access.ConfigurationFingerprint,
                                    WorkingDirectory, capabilities, ct).ConfigureAwait(false);
                                if (!IsAlive || ReadyStatus != ScmPromptReadyStatus.Ready)
                                    return access with { Status = ScmExchangeAccessStatus.Unknown, Reason = "CLI ist vor der Dateitool-Probe nicht mehr eingabebereit; regulärer Neustart erforderlich." };
                                await SendAccessProbeAsync(ct).ConfigureAwait(false);
                            }
                            else if (_accessProbe is not null && ReadyStatus == ScmPromptReadyStatus.Ready &&
                                _accessProbeSends < AccessProbeMaxSends &&
                                _time.GetUtcNow() - _accessProbeSentUtc >= AccessProbeResendDelay)
                            {
                                // A probe swallowed by a blocking dialog (for example the workspace trust
                                // prompt) never reaches the file tool. The identical challenge is sent
                                // again only while a real input prompt is visibly ready — still without
                                // any event content before the channel proof.
                                await SendAccessProbeAsync(ct).ConfigureAwait(false);
                            }
                            access = access with
                            {
                                Status = ScmExchangeAccessStatus.Unknown,
                                Reason = _accessProbeSends >= AccessProbeMaxSends
                                ? "Nachweis des tatsächlichen Dateitool-Zugriffs steht aus; die lokale Kanalprobe wurde wiederholt ohne Nachweis gesendet. CLI-Sitzung regulär neu starten."
                                : "Nachweis des tatsächlichen Dateitool-Zugriffs steht aus; separate lokale Kanalprobe muss erfolgreich abgeschlossen werden."
                            };
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException)
            { access = access with { Status = ScmExchangeAccessStatus.Denied, Reason = "Antwortkanal enthält einen unsicheren oder nicht zugänglichen Pfad." }; }
        }
        return access;
    }

    /// <summary>Sendet die ausstehende Kanalprobe; der Probe-Prompt enthält niemals Ereignisinhalte.</summary>
    private async Task SendAccessProbeAsync(CancellationToken ct)
    {
        MarkInputStarted();
        // No event text, event acknowledgement or external action is sent before proof.
        await Session.WritePromptAsync(ScmEventExchangeProtocol.BuildAccessProbePrompt(_accessProbe!), ct).ConfigureAwait(false);
        _accessProbeSends++;
        _accessProbeSentUtc = _time.GetUtcNow();
    }

    /// <inheritdoc/>
    public bool ConfirmReady(Guid runId)
    {
        if (runId != CliRunId || !IsAlive) return false;
        lock (_sync)
        {
            RefreshScreen();
            // Confirmation cannot turn a shell, unknown adapter, blocking dialog or known busy
            // turn into an AI prompt. A recognized but out-of-range version may be confirmed
            // explicitly: the file-tool channel probe remains the actual safety boundary.
            if (_cliRecognition.State == ScmCliRecognitionState.Unknown ||
                _ready is ScmPromptReadyStatus.Busy or ScmPromptReadyStatus.TrustPrompt) return false;
            _manuallyConfirmed = true;
            if (_cliRecognition.State == ScmCliRecognitionState.Unsupported) _formatUserConfirmed = true;
            SetReady(ScmPromptReadyStatus.Ready);
            return true;
        }
    }

    /// <summary>Verwirft automatische/manuelle Bereitschaft bei jeder manuellen oder automatischen Eingabe.</summary>
    public void MarkInputStarted()
    {
        lock (_sync)
        {
            _manuallyConfirmed = false;
            _inputRevision = _outputRevision;
            SetReady(ScmPromptReadyStatus.Busy);
        }
    }

    /// <inheritdoc/>
    public Task WritePromptAsync(string prompt, CancellationToken ct = default)
        => ScmTaskCoordinator.Shared.RunAsync(_taskId, async () =>
        {
            if (prompt.Any(char.IsControl)) throw new ArgumentException("SCM-Prompt darf keine Steuerzeichen enthalten.", nameof(prompt));
            var access = await CheckExchangeAccessAsync(Path.Combine(WorkingDirectory, ScmEventExchangeProtocol.DirectoryName), ct).ConfigureAwait(false);
            if (access.Status != ScmExchangeAccessStatus.Supported) throw new InvalidOperationException(access.Reason);
            if (!IsAlive || ReadyStatus != ScmPromptReadyStatus.Ready)
                throw new InvalidOperationException("CLI ist nicht mehr am bestätigten Eingabeprompt.");
            MarkInputStarted();
            await Session.WritePromptAsync(prompt, ct).ConfigureAwait(false);
        }, ct);

    private void OnOutput(object? sender, TerminalOutputChunkEventArgs args)
    {
        lock (_sync)
        {
            if (_startupOutput.Length < 32768) _startupOutput.Append(Encoding.UTF8.GetString(args.Data.Span));
            RecognizeStartup(_startupOutput.ToString());
        }
    }

    private void RecognizeStartup(string text)
    {
        // Supported transcript formats are deliberately explicit. New/changed formats require a fixture.
        var match = ScmCliPromptProfile.TryMatch(PluginPrefix, text);
        // Die Erkennung ist monoton: Ein einmal unterstützt klassifizierter Lauf wird durch
        // Folgeanker nicht mehr herabgestuft. Der flüchtige Bildschirmpuffer kann den Anker ohne
        // Versionszeile zeigen (Scrollback-Grenze, erneuter Namensabdruck), während das
        // kumulative Startup-Transkript die Version längst enthält.
        if (match is null || _cliRecognition.State == ScmCliRecognitionState.Supported) return;
        if (match.Profile.VersionPattern is null)
        {
            // The simulator contract is unversioned; only a run the start contract already granted
            // or denied counts as an identified channel, never a plain shell.
            if (_access.Status is ScmExchangeAccessStatus.Supported or ScmExchangeAccessStatus.Denied)
                _cliRecognition = new(ScmCliRecognitionState.Supported, match.Profile.DisplayName, null, match.Profile.SupportedVersionsText);
            else if (_cliRecognition.ChannelReason is null)
                // Anker ohne Kanalvertrag (z. B. Simulator-Shell ohne SCM-Modus): Der Zugriffsgrund
                // des Evaluators wird gemeldet statt fälschlich ein fehlendes Banner zu behaupten.
                _cliRecognition = new(ScmCliRecognitionState.Unknown, match.Profile.DisplayName, null,
                    match.Profile.SupportedVersionsText)
                { ChannelReason = _access.Reason };
            return;
        }
        // Nur ein geparstes Versionstoken darf einen einmal klassifizierten Zustand verändern;
        // ein tokenloser Anker verfeinert höchstens den noch unbekannten Zustand.
        if (match.Version is null)
        {
            if (_cliRecognition.State == ScmCliRecognitionState.Unknown)
                _cliRecognition = new(ScmCliRecognitionState.Unsupported, match.Profile.DisplayName, null, match.Profile.SupportedVersionsText);
            return;
        }
        _cliRecognition = new(match.IsSupported ? ScmCliRecognitionState.Supported : ScmCliRecognitionState.Unsupported,
            match.Profile.DisplayName, match.Version, match.Profile.SupportedVersionsText);
    }

    private void RefreshScreen()
    {
        lock (_sync)
        {
            if (Session.Buffer is null || Session.Buffer.Rows == 0) return;
            var lines = Enumerable.Range(0, Session.Buffer.Rows).Select(row => new string(Session.Buffer.GetRow(row).Select(cell => cell.Character).ToArray()).TrimEnd()).ToArray();
            var screen = string.Join('\n', lines);
            RecognizeStartup(screen);
            // A confirmed out-of-range format keeps the regular prompt/busy evaluation so the
            // channel probe lifecycle (send, resend, re-readiness) works like a supported version.
            if (_cliRecognition.State != ScmCliRecognitionState.Supported && !_formatUserConfirmed) return;
            if (_manuallyConfirmed) return;
            if (_inputRevision == _outputRevision) return;
            var prompt = lines[Math.Clamp(Session.Buffer.CursorRow, 0, lines.Length - 1)].Trim();
            var busy = screen.Contains("esc to interrupt", StringComparison.OrdinalIgnoreCase) ||
                screen.Contains("Esc to cancel", StringComparison.OrdinalIgnoreCase) ||
                screen.Contains("esc twice to interrupt", StringComparison.OrdinalIgnoreCase) ||
                screen.Contains("esc again to interrupt", StringComparison.OrdinalIgnoreCase);
            if (TrustDialogMarkers.Any(marker => screen.Contains(marker, StringComparison.OrdinalIgnoreCase)))
            {
                // A blocking trust dialog swallows submitted text; it is a distinct, resolvable
                // wait state rather than an unknown or ready prompt.
                SetReady(ScmPromptReadyStatus.TrustPrompt);
            }
            else if (PluginPrefix == "Softwareschmiede.KiSimulator")
            {
                // Only the current cursor line is authoritative; old READY rows remain in scrollback.
                if (prompt == "SCM_READY_V1") SetReady(ScmPromptReadyStatus.Ready);
                else if (prompt == "SCM_BUSY_V1") SetReady(ScmPromptReadyStatus.Busy);
                else SetReady(ScmPromptReadyStatus.Unknown);
            }
            else if (busy) SetReady(ScmPromptReadyStatus.Busy);
            else if ((PluginPrefix == "Softwareschmiede.Codex" && prompt is "›" or "› Implement {feature}" or "› Explain this codebase") ||
                     (PluginPrefix == "Softwareschmiede.ClaudeCli" && prompt == "❯") ||
                     (PluginPrefix == "Softwareschmiede.Devin" && (prompt == "❭" || prompt.StartsWith("❭ ", StringComparison.Ordinal))))
                SetReady(ScmPromptReadyStatus.Ready);
            else SetReady(ScmPromptReadyStatus.Unknown);
        }
    }

    private void SetReady(ScmPromptReadyStatus ready)
    {
        if (_ready == ready) return;
        _ready = ready;
        var previous = _changed;
        _changed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        previous.TrySetResult();
    }

    private void OnBufferChanged(object? sender, EventArgs args) { lock (_sync) _outputRevision++; RefreshScreen(); }
    private void OnExited(object? sender, TerminalSessionExitedEventArgs args) { lock (_sync) SetReady(ScmPromptReadyStatus.Exited); }
    private void OnFailed(object? sender, TerminalSessionFailedEventArgs args) { lock (_sync) SetReady(ScmPromptReadyStatus.Exited); }

    /// <inheritdoc/>
    public void Dispose()
    {
        Session.OutputChunk -= OnOutput;
        Session.BufferChanged -= OnBufferChanged;
        Session.Exited -= OnExited;
        Session.Failed -= OnFailed;
    }
}
