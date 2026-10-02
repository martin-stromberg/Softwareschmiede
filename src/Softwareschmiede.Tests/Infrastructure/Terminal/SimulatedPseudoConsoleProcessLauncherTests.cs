using System.Reflection;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Infrastructure.Terminal;

namespace Softwareschmiede.Tests.Infrastructure.Terminal;

/// <summary>Unit-Tests für <see cref="SimulatedPseudoConsoleProcessLauncher"/>: startet den CLI-Kindprozess
/// ohne echtes ConPTY über gewöhnliche STDIN/STDOUT-Umleitung, damit E2E-Tests unter <c>dotnet test</c>
/// zuverlässig laufen (siehe docs/features/e2e-korrektur/requirement.md).</summary>
public sealed class SimulatedPseudoConsoleProcessLauncherTests
{
    private readonly SimulatedPseudoConsoleProcessLauncher _sut = new(NullLogger<SimulatedPseudoConsoleProcessLauncher>.Instance, NullLoggerFactory.Instance, Options.Create(new TerminalSessionOptions()));

    /// <summary>Nach dem Start muss der Kindprozess laufen und eine funktionsfähige Nicht-PTY-Sitzung
    /// geliefert werden (der simulierte Pfad ermittelt den Exit-Code über <c>Process.ExitCode</c>).</summary>
    [OsInterfaceFact]
    public void Start_LiefertLaufendenProzessUndSession()
    {
        var result = _sut.Start(Guid.NewGuid(), CreateShellSpec());
        var process = result.Process;
        var session = result.Session;
        try
        {
            process.HasExited.Should().BeFalse("der simulierte Kindprozess muss nach dem Start laufen");
            session.Should().NotBeNull();
            result.IsPseudoTerminal.Should().BeFalse();
        }
        finally
        {
            session.Dispose();
            KillIfRunning(process);
        }
    }

    /// <summary>Ein über <see cref="Softwareschmiede.Infrastructure.Terminal.ITerminalSession.InputStream"/>
    /// gesendetes Kommando muss vom laufenden, interaktiven <c>cmd.exe</c> ausgeführt werden und im
    /// <see cref="Softwareschmiede.Infrastructure.Terminal.ITerminalSession.Buffer"/> erscheinen.</summary>
    [OsInterfaceFact]
    public async Task Start_GesendetesKommandoWirdAusgefuehrt()
    {
        var result = _sut.Start(Guid.NewGuid(), CreateShellSpec());
        var process = result.Process;
        var session = result.Session;
        try
        {
            var bytes = Encoding.UTF8.GetBytes("echo MARKER_TEXT\r\n");
            await session.InputStream.WriteAsync(bytes);
            await session.InputStream.FlushAsync();

            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
            var found = false;
            while (DateTime.UtcNow < deadline)
            {
                if (GetBufferText(session).Contains("MARKER_TEXT", StringComparison.Ordinal))
                {
                    found = true;
                    break;
                }
                await Task.Delay(100);
            }

            found.Should().BeTrue("das über InputStream gesendete Kommando muss vom cmd.exe ausgeführt werden und im Buffer erscheinen");
        }
        finally
        {
            session.Dispose();
            KillIfRunning(process);
        }
    }

    /// <summary>Ein über <see cref="Softwareschmiede.Infrastructure.Terminal.ITerminalSession.WritePromptAsync"/>
    /// gesendeter Prompt (abschließendes nacktes <c>\r</c> als Submit — Semantik eines echten ConPTY-Enter) muss
    /// ebenfalls vom interaktiven <c>cmd.exe</c> ausgeführt werden: Die Pipe-Übertragung braucht <c>\r\n</c> als
    /// Zeilenende, weshalb der Launcher den Input-Stream entsprechend übersetzt.</summary>
    [OsInterfaceFact]
    public async Task Start_UeberWritePromptAsyncGesendeterPromptWirdAusgefuehrt()
    {
        var result = _sut.Start(Guid.NewGuid(), CreateShellSpec());
        var process = result.Process;
        var session = result.Session;
        try
        {
            await session.WritePromptAsync("echo MARKER_PROMPT_TEXT", CancellationToken.None);

            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
            var found = false;
            while (DateTime.UtcNow < deadline)
            {
                if (GetBufferText(session).Contains("MARKER_PROMPT_TEXT", StringComparison.Ordinal))
                {
                    found = true;
                    break;
                }
                await Task.Delay(100);
            }

            found.Should().BeTrue("ein per WritePromptAsync gesendeter Prompt (nacktes CR als Submit) muss auf der simulierten Pipe ausgeführt werden und im Buffer erscheinen");
        }
        finally
        {
            session.Dispose();
            KillIfRunning(process);
        }
    }

    /// <summary>Nach <see cref="System.Diagnostics.Process.Kill(bool)"/> mit <c>entireProcessTree: true</c> muss
    /// der Prozess innerhalb kurzer Zeit als beendet erkennbar sein — bestätigt Kompatibilität mit
    /// <c>KiAusfuehrungsService.StopCliAsync</c>, das denselben Aufruf für den Stop-Fallback nutzt.</summary>
    [OsInterfaceFact]
    public async Task Start_ProzessBeendetSichAufKillEntireProcessTree()
    {
        var result = _sut.Start(Guid.NewGuid(), CreateShellSpec());
        var process = result.Process;
        var session = result.Session;
        try
        {
            process.Kill(entireProcessTree: true);

            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
            var exited = false;
            while (DateTime.UtcNow < deadline)
            {
                if (process.HasExited)
                {
                    exited = true;
                    break;
                }
                await Task.Delay(50);
            }

            exited.Should().BeTrue("Kill(entireProcessTree: true) muss den simulierten Kindprozess zuverlässig beenden");
        }
        finally
        {
            session.Dispose();
        }
    }

    /// <summary>Ein am Chunk-Ende stehendes <c>\r</c>, dem im nächsten Schreibaufruf ein <c>\n</c> folgt
    /// (möglich, weil <c>WriteInputAsync</c> in 4-KB-Stücken schreibt), darf nicht zu <c>\r\n\n</c>
    /// werden — das würde auf der Pipe einen leeren Zeilen-Submit auslösen.</summary>
    [Fact]
    public void CrSubmittingInputStream_CrLfUeberChunkGrenze_KeinExtraZeilenvorschub()
    {
        var inner = new MemoryStream();
        using var stream = CreateCrSubmittingInputStream(inner);

        stream.Write([(byte)'a', (byte)'\r']);
        stream.Write([(byte)'\n', (byte)'b']);
        stream.Flush();

        inner.ToArray().Should().Equal("a\r\nb"u8.ToArray(),
            "ein über die Chunk-Grenze getrenntes \\r\\n darf kein zusätzliches \\n erzeugen");
    }

    /// <summary>Ein am Chunk-Ende stehendes <c>\r</c>, dem <em>kein</em> <c>\n</c> folgt, muss wie ein
    /// alleinstehendes CR zu <c>\r\n</c> übersetzt werden.</summary>
    [Fact]
    public void CrSubmittingInputStream_CrAmChunkEndeOhneLf_WirdZuCrLf()
    {
        var inner = new MemoryStream();
        using var stream = CreateCrSubmittingInputStream(inner);

        stream.Write([(byte)'a', (byte)'\r']);
        stream.Write([(byte)'b']);
        stream.Flush();

        inner.ToArray().Should().Equal("a\r\nb"u8.ToArray());
    }

    /// <summary>Ein abschließendes, zurückgehaltenes <c>\r</c> muss spätestens beim Flush als
    /// <c>\r\n</c> ausgegeben werden — sonst bliebe der Submit eines Prompts unzustellt.</summary>
    [Fact]
    public async Task CrSubmittingInputStream_AbschliessendesCr_WirdBeimFlushAusgegeben()
    {
        var inner = new MemoryStream();
        var stream = CreateCrSubmittingInputStream(inner);

        await stream.WriteAsync(new byte[] { (byte)'a', (byte)'\r' });
        inner.ToArray().Should().Equal("a"u8.ToArray(), "das '\\r' am Chunk-Ende wird zunächst zurückgehalten");
        await stream.FlushAsync();

        inner.ToArray().Should().Equal("a\r\n"u8.ToArray());
    }

    /// <summary>Die Grundübersetzung innerhalb eines Chunks bleibt unverändert: alleinstehendes
    /// <c>\r</c> wird zu <c>\r\n</c>, ein vorhandenes <c>\r\n</c>-Paar bleibt unverändert.</summary>
    [Fact]
    public void CrSubmittingInputStream_InnerhalbChunk_UnveraenderteUebersetzung()
    {
        var inner = new MemoryStream();
        using var stream = CreateCrSubmittingInputStream(inner);

        stream.Write("x\ry\r\nz"u8.ToArray());
        stream.Flush();

        inner.ToArray().Should().Equal("x\r\ny\r\nz"u8.ToArray());
    }

    /// <summary>Erzeugt den privaten <c>CrSubmittingInputStream</c> über Reflection über einem
    /// beliebigen Ziel-Stream, damit die Übersetzungslogik ohne echten Prozess testbar ist.</summary>
    private static Stream CreateCrSubmittingInputStream(Stream inner)
    {
        var type = typeof(SimulatedPseudoConsoleProcessLauncher)
            .GetNestedType("CrSubmittingInputStream", BindingFlags.NonPublic)!;
        return (Stream)Activator.CreateInstance(type, inner)!;
    }

    private static void KillIfRunning(System.Diagnostics.Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch
        {
        }
    }

    private static TerminalSessionStartSpec CreateShellSpec()
        => new()
        {
            FileName = "cmd.exe",
            Arguments = "/k",
            WorkingDirectory = Path.GetTempPath(),
        };

    private static string GetBufferText(ITerminalSession session)
    {
        var sb = new StringBuilder();
        for (var row = 0; row < session.Buffer.Rows; row++)
        {
            foreach (var cell in session.Buffer.GetRow(row))
                sb.Append(cell.Character);
            sb.Append('\n');
        }
        return sb.ToString();
    }
}
