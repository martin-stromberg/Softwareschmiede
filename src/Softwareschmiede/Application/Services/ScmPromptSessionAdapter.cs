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
/// Adapter für die getesteten Codex-/Claude-Promptformate und den deterministischen JSON-Simulator.
/// Shells, Alt-Sessions und unbekannte CLI-Versionen bleiben sichtbar ungeklärt.
/// </summary>
public sealed class ScmPromptSessionAdapter : IScmPromptSessionAdapter, IDisposable
{
    private readonly Guid _taskId;
    private readonly ScmExchangeAccess _access;
    private readonly IReadOnlyList<ScmCliConfigurationFile> _files;
    private readonly Func<bool> _isCurrent;
    private readonly object _sync = new();
    private readonly StringBuilder _startupOutput = new();
    private readonly ScmEventExchangeProtocol _probeProtocol = new();
    private ScmExchangeAccessProbe? _accessProbe;
    private TaskCompletionSource _changed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private ScmPromptReadyStatus _ready;
    private bool _recognizedCli;
    private bool _manuallyConfirmed;
    private long _inputRevision = -1;
    private long _outputRevision;

    /// <summary>Bindet StartSpec, erfasste Dateirechte und eine Session ohne zusätzliche Freigaben.</summary>
    public ScmPromptSessionAdapter(Guid taskId, Guid runId, string pluginPrefix, TerminalSessionStartSpec spec,
        ITerminalSession session, IReadOnlyList<ScmCliConfigurationFile> files, Func<bool> isCurrent)
    {
        _taskId = taskId;
        CliRunId = runId;
        PluginPrefix = pluginPrefix;
        Session = session;
        WorkingDirectory = string.IsNullOrWhiteSpace(spec.WorkingDirectory) ? string.Empty : Path.GetFullPath(spec.WorkingDirectory);
        _files = files;
        _isCurrent = isCurrent;
        _access = ScmCliAccessEvaluator.Evaluate(pluginPrefix, spec, runId, files);
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
                else if (!_recognizedCli && access.Status == ScmExchangeAccessStatus.Supported)
                    access = access with { Status = ScmExchangeAccessStatus.Unknown, Reason = "CLI-Version oder Initialisierung ist nicht durch ein unterstütztes Promptformat bestätigt." };
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
                                MarkInputStarted();
                                // No event text, event acknowledgement or external action is sent before proof.
                                await Session.WritePromptAsync(ScmEventExchangeProtocol.BuildAccessProbePrompt(_accessProbe), ct).ConfigureAwait(false);
                            }
                            access = access with { Status = ScmExchangeAccessStatus.Unknown, Reason = "Nachweis des tatsächlichen Dateitool-Zugriffs steht aus; separate lokale Kanalprobe muss erfolgreich abgeschlossen werden." };
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException)
            { access = access with { Status = ScmExchangeAccessStatus.Denied, Reason = "Antwortkanal enthält einen unsicheren oder nicht zugänglichen Pfad." }; }
        }
        return access;
    }

    /// <inheritdoc/>
    public bool ConfirmReady(Guid runId)
    {
        if (runId != CliRunId || !IsAlive) return false;
        lock (_sync)
        {
            // Confirmation cannot turn a shell, unknown adapter or known busy turn into an AI prompt.
            if (!_recognizedCli || _ready == ScmPromptReadyStatus.Busy) return false;
            _manuallyConfirmed = true;
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
        // Supported transcript versions are deliberately explicit. New/changed formats require a fixture.
        if (PluginPrefix == "Softwareschmiede.Codex" && text.Contains("OpenAI Codex (v0.114.0)", StringComparison.Ordinal)) _recognizedCli = true;
        if (PluginPrefix == "Softwareschmiede.ClaudeCli" && text.Contains("Claude Code v2.1.0", StringComparison.Ordinal)) _recognizedCli = true;
        if (PluginPrefix == "Softwareschmiede.KiSimulator" && text.Contains("SCM_SIMULATOR_PROTOCOL_V1", StringComparison.Ordinal) &&
            _access.Status is ScmExchangeAccessStatus.Supported or ScmExchangeAccessStatus.Denied) _recognizedCli = true;
    }

    private void RefreshScreen()
    {
        lock (_sync)
        {
            if (Session.Buffer is null || Session.Buffer.Rows == 0) return;
            var lines = Enumerable.Range(0, Session.Buffer.Rows).Select(row => new string(Session.Buffer.GetRow(row).Select(cell => cell.Character).ToArray()).TrimEnd()).ToArray();
            var screen = string.Join('\n', lines);
            RecognizeStartup(screen);
            if (!_recognizedCli) return;
            if (_manuallyConfirmed) return;
            if (_inputRevision == _outputRevision) return;
            var prompt = lines[Math.Clamp(Session.Buffer.CursorRow, 0, lines.Length - 1)].Trim();
            var busy = screen.Contains("esc to interrupt", StringComparison.OrdinalIgnoreCase) || screen.Contains("Esc to cancel", StringComparison.OrdinalIgnoreCase);
            if (PluginPrefix == "Softwareschmiede.KiSimulator")
            {
                // Only the current cursor line is authoritative; old READY rows remain in scrollback.
                if (prompt == "SCM_READY_V1") SetReady(ScmPromptReadyStatus.Ready);
                else if (prompt == "SCM_BUSY_V1") SetReady(ScmPromptReadyStatus.Busy);
                else SetReady(ScmPromptReadyStatus.Unknown);
            }
            else if (busy) SetReady(ScmPromptReadyStatus.Busy);
            else if ((PluginPrefix == "Softwareschmiede.Codex" && prompt is "›" or "› Implement {feature}" or "› Explain this codebase") ||
                     (PluginPrefix == "Softwareschmiede.ClaudeCli" && prompt == "❯"))
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
