using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Infrastructure.Terminal;

/// <summary>
/// Startet den CLI-Kindprozess ohne echtes ConPTY über gewöhnliche STDIN/STDOUT-Umleitung direkt aus
/// einer normalisierten <see cref="TerminalSessionStartSpec"/>. Dient als explizit diagnostizierter
/// Pipe-Fallback und im E2E-Testmodus anstelle von <see cref="Win32PseudoConsoleProcessLauncher"/>, da
/// ein über die Windows-Pseudo-Console-API angehängter Kindprozess unter <c>dotnet test</c>/
/// <c>vstest.console.exe</c> unmittelbar nach dem Start beendet wird (siehe
/// docs/features/e2e-korrektur/requirement.md).
/// </summary>
public sealed class SimulatedPseudoConsoleProcessLauncher : IPseudoConsoleProcessLauncher
{
    private readonly ILogger<SimulatedPseudoConsoleProcessLauncher> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IOptions<TerminalSessionOptions> _options;

    /// <summary>Erstellt eine neue Instanz von <see cref="SimulatedPseudoConsoleProcessLauncher"/>.</summary>
    /// <param name="logger">Logger für Diagnosemeldungen.</param>
    /// <param name="loggerFactory">Factory zum Erzeugen des <see cref="PseudoConsoleSession"/>-Loggers.</param>
    /// <param name="options">Terminal-Laufzeitparameter (Replay-Budget, initiale Buffer-Größe).</param>
    public SimulatedPseudoConsoleProcessLauncher(ILogger<SimulatedPseudoConsoleProcessLauncher> logger, ILoggerFactory loggerFactory, IOptions<TerminalSessionOptions> options)
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
        _options = options;
    }

    /// <inheritdoc/>
    public bool IsPseudoTerminal => false;

    /// <inheritdoc/>
    public TerminalSessionStartResult Start(Guid aufgabeId, TerminalSessionStartSpec spec, ITerminalOutputSink? outputSink = null)
    {
        var workingDir = !string.IsNullOrEmpty(spec.WorkingDirectory) && Directory.Exists(spec.WorkingDirectory)
            ? spec.WorkingDirectory
            : Path.GetTempPath();
        var psi = new ProcessStartInfo
        {
            FileName = spec.FileName,
            Arguments = spec.Arguments,
            WorkingDirectory = workingDir,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var entry in spec.EnvironmentVariables)
            psi.EnvironmentVariables[entry.Key] = entry.Value;

        _logger.LogInformation("CLI-Prozess (Pipe-Backend, {FileName} {Arguments}) für Aufgabe {AufgabeId} starten.", spec.FileName, spec.Arguments, aufgabeId);

        var process = Process.Start(psi)
            ?? throw new InvalidOperationException("CLI-Prozess auf dem Pipe-Backend konnte nicht gestartet werden.");
        process.BeginErrorReadLine();

        PseudoConsoleSession session;
        try
        {
            session = new PseudoConsoleSession(
                NullPseudoConsoleHandle.Instance,
                process,
                new CrSubmittingInputStream(process.StandardInput.BaseStream),
                process.StandardOutput.BaseStream,
                new PseudoConsoleSessionContext
                {
                    Logger = _loggerFactory.CreateLogger<PseudoConsoleSession>(),
                    OutputSink = outputSink,
                    Options = _options.Value,
                });
        }
        catch
        {
            // Der Kindprozess läuft bereits — ohne Session muss er aufgeräumt werden, sonst läuft
            // ein verwaister Prozess ohne zugehörige Sitzung weiter (analog Win32PseudoConsoleProcessLauncher).
            try { process.Kill(entireProcessTree: true); } catch { /* Best Effort. */ }
            try { process.Dispose(); } catch { /* Best Effort. */ }
            throw;
        }

        return new TerminalSessionStartResult(process, session, IsPseudoTerminal: false);
    }

    /// <summary>
    /// Übersetzt ein alleinstehendes <c>\r</c> (CR) auf dem Input-Stream in <c>\r\n</c>.
    /// <see cref="PseudoConsoleSession.WritePromptAsync"/> sendet den abschließenden Submit bewusst als
    /// nacktes CR, weil ein echtes ConPTY damit einen Tastatur-Enter emuliert. Auf einer umgeleiteten
    /// STDIN-Pipe terminiert <c>cmd.exe</c> eine Zeile dagegen nur per <c>\r\n</c> — ohne Übersetzung
    /// bliebe der Prompt unzustellt im Eingabepuffer liegen. Bereits vorhandene <c>\r\n</c>-Sequenzen
    /// werden unverändert durchgereicht.
    /// </summary>
    private sealed class CrSubmittingInputStream : Stream
    {
        private static readonly byte[] CrLf = [(byte)'\r', (byte)'\n'];

        private readonly Stream _inner;
        private bool _pendingCr;

        public CrSubmittingInputStream(Stream inner)
        {
            _inner = inner;
        }

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => _inner.CanWrite;
        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
            FlushPendingCarriageReturn();
            _inner.Flush();
        }

        public override async Task FlushAsync(CancellationToken cancellationToken)
        {
            // Ein zurückgehaltenes '\r' muss spätestens beim Flush als abgeschlossenes '\r\n'
            // ausgegeben werden — der Aufrufer (PseudoConsoleSession.WriteInputAsync) flusht nach
            // jedem Schreibvorgang, sonst bliebe ein abschließender Submit ('\r') unzustellt.
            if (_pendingCr)
            {
                _pendingCr = false;
                await _inner.WriteAsync(CrLf, cancellationToken).ConfigureAwait(false);
            }

            await _inner.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
            => _inner.Write(Translate(buffer.AsSpan(offset, count)));

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            var translated = Translate(buffer.AsSpan(offset, count));
            return _inner.WriteAsync(translated, 0, translated.Length, cancellationToken);
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
            => _inner.WriteAsync(Translate(buffer.Span), cancellationToken);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try
                {
                    FlushPendingCarriageReturn();
                }
                catch
                {
                    // Best Effort beim Schließen — ein zurückgehaltenes '\r' soll nicht still verloren gehen.
                }

                _inner.Dispose();
            }

            base.Dispose(disposing);
        }

        private void FlushPendingCarriageReturn()
        {
            if (!_pendingCr)
            {
                return;
            }

            _pendingCr = false;
            _inner.Write(CrLf, 0, CrLf.Length);
        }

        /// <summary>Übersetzt alleinstehende <c>\r</c> in <c>\r\n</c> und lässt bestehende <c>\r\n</c>-Paare
        /// unverändert. Ein <c>\r</c> als letztes Byte des Chunks wird zurückgehalten (<see cref="_pendingCr"/>),
        /// weil <c>WriteInputAsync</c> in 4-KB-Stücken schreibt und ein <c>\r\n</c>-Paar genau an der
        /// Chunk-Grenze getrennt werden kann — erst der nächste Schreibaufruf (oder Flush) entscheidet,
        /// ob ein <c>\n</c> folgt.</summary>
        private byte[] Translate(ReadOnlySpan<byte> input)
        {
            var pendingCr = _pendingCr;
            var carryCr = input.Length > 0 && input[^1] == '\r';
            var effective = carryCr ? input[..^1] : input;
            _pendingCr = carryCr;

            // Ein zurückgehaltenes '\r' wird jetzt aufgelöst: Folgt ein '\n', bildet es ein normales
            // '\r\n'-Paar; sonst wird wie bei einem alleinstehenden CR ein '\n' nachgeschoben.
            var pendingNeedsLf = pendingCr && (effective.Length == 0 || effective[0] != '\n');

            var extra = 0;
            for (var i = 0; i < effective.Length; i++)
            {
                if (effective[i] == '\r' && (i + 1 >= effective.Length || effective[i + 1] != '\n'))
                {
                    extra++;
                }
            }

            var output = new byte[effective.Length + (pendingCr ? 1 : 0) + (pendingNeedsLf ? 1 : 0) + extra];
            var pos = 0;
            if (pendingCr)
            {
                output[pos++] = (byte)'\r';
                if (pendingNeedsLf)
                {
                    output[pos++] = (byte)'\n';
                }
            }

            for (var i = 0; i < effective.Length; i++)
            {
                output[pos++] = effective[i];
                if (effective[i] == '\r' && (i + 1 >= effective.Length || effective[i + 1] != '\n'))
                {
                    output[pos++] = (byte)'\n';
                }
            }

            return output;
        }
    }
}
