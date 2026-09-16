using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Softwareschmiede.Infrastructure.Terminal;

/// <summary>
/// Startet den CLI-Kindprozess ohne echtes ConPTY über gewöhnliche STDIN/STDOUT-Umleitung.
/// Wird im E2E-Testmodus anstelle von <see cref="Win32PseudoConsoleProcessLauncher"/> verwendet, da
/// ein über die Windows-Pseudo-Console-API angehängter Kindprozess unter <c>dotnet test</c>/
/// <c>vstest.console.exe</c> unmittelbar nach dem Start beendet wird (siehe
/// docs/features/e2e-korrektur/requirement.md).
/// </summary>
public sealed class SimulatedPseudoConsoleProcessLauncher : IPseudoConsoleProcessLauncher
{
    private readonly ILogger<SimulatedPseudoConsoleProcessLauncher> _logger;
    private readonly ILoggerFactory _loggerFactory;

    /// <summary>Erstellt eine neue Instanz von <see cref="SimulatedPseudoConsoleProcessLauncher"/>.</summary>
    /// <param name="logger">Logger für Diagnosemeldungen.</param>
    /// <param name="loggerFactory">Factory zum Erzeugen des <see cref="PseudoConsoleSession"/>-Loggers.</param>
    public SimulatedPseudoConsoleProcessLauncher(ILogger<SimulatedPseudoConsoleProcessLauncher> logger, ILoggerFactory loggerFactory)
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
    }

    /// <inheritdoc/>
    public (Process Process, PseudoConsoleSession Session, IntPtr NativeProcessHandle) Start(Guid aufgabeId, string effectiveWorkingDirectory, string pluginCommand, ITerminalOutputSink? outputSink = null)
    {
        var workingDir = !string.IsNullOrEmpty(effectiveWorkingDirectory) && Directory.Exists(effectiveWorkingDirectory)
            ? effectiveWorkingDirectory
            : Path.GetTempPath();
        var psi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            WorkingDirectory = workingDir,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        psi.EnvironmentVariables["PATH"] = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;

        _logger.LogInformation("CLI-Prozess (simuliert, cmd.exe → {Command}) für Aufgabe {AufgabeId} starten.", pluginCommand, aufgabeId);

        var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Simulierter CLI-Prozess konnte nicht gestartet werden.");
        process.BeginErrorReadLine();

        var session = new PseudoConsoleSession(
            NullPseudoConsoleHandle.Instance,
            process,
            new CrSubmittingInputStream(process.StandardInput.BaseStream),
            process.StandardOutput.BaseStream,
            _loggerFactory.CreateLogger<PseudoConsoleSession>(),
            outputSink);

        return (process, session, IntPtr.Zero);
    }

    /// <summary>
    /// Übersetzt ein alleinstehendes <c>\r</c> (CR) auf dem Input-Stream in <c>\r\n</c>.
    /// <see cref="PseudoConsoleSession.WritePromptAsync"/> sendet den abschließenden Submit bewusst als
    /// nacktes CR, weil ein echtes ConPTY damit einen Tastatur-Enter emuliert. Auf einer umgeleiteten
    /// STDIN-Pipe terminiert <c>cmd.exe</c> eine Zeile dagegen nur per <c>\r\n</c> — ohne Übersetzung
    /// bliebe der Prompt unzustellt im Eingabepuffer liegen. Bereits vorhandene <c>\r\n</c>-Sequenzen
    /// (z. B. der Plugin-Startbefehl aus <c>KiAusfuehrungsService.SendCommandDelayedAsync</c>) werden
    /// unverändert durchgereicht.
    /// </summary>
    private sealed class CrSubmittingInputStream : Stream
    {
        private readonly Stream _inner;

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

        public override void Flush() => _inner.Flush();

        public override Task FlushAsync(CancellationToken cancellationToken) => _inner.FlushAsync(cancellationToken);

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
                _inner.Dispose();
            }

            base.Dispose(disposing);
        }

        private static byte[] Translate(ReadOnlySpan<byte> input)
        {
            var extra = 0;
            for (var i = 0; i < input.Length; i++)
            {
                if (input[i] == '\r' && (i + 1 >= input.Length || input[i + 1] != '\n'))
                {
                    extra++;
                }
            }

            if (extra == 0)
            {
                return input.ToArray();
            }

            var output = new byte[input.Length + extra];
            var pos = 0;
            for (var i = 0; i < input.Length; i++)
            {
                output[pos++] = input[i];
                if (input[i] == '\r' && (i + 1 >= input.Length || input[i + 1] != '\n'))
                {
                    output[pos++] = (byte)'\n';
                }
            }

            return output;
        }
    }
}
