using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.Infrastructure.Terminal;

namespace Softwareschmiede.Tests.Infrastructure.Terminal;

/// <summary>Unit-Tests für <see cref="SimulatedPseudoConsoleProcessLauncher"/>: startet den CLI-Kindprozess
/// ohne echtes ConPTY über gewöhnliche STDIN/STDOUT-Umleitung, damit E2E-Tests unter <c>dotnet test</c>
/// zuverlässig laufen (siehe docs/features/e2e-korrektur/requirement.md).</summary>
public sealed class SimulatedPseudoConsoleProcessLauncherTests
{
    private readonly SimulatedPseudoConsoleProcessLauncher _sut = new(NullLogger<SimulatedPseudoConsoleProcessLauncher>.Instance, NullLoggerFactory.Instance);

    /// <summary>Nach dem Start muss der Kindprozess laufen und eine funktionsfähige Sitzung mit
    /// <see cref="IntPtr.Zero"/> als natives Prozess-Handle geliefert werden.</summary>
    [Fact]
    public void Start_LiefertLaufendenProzessUndSession()
    {
        var (process, session, nativeProcessHandle) = _sut.Start(Guid.NewGuid(), Path.GetTempPath(), "echo simuliert");
        try
        {
            process.HasExited.Should().BeFalse("der simulierte Kindprozess muss nach dem Start laufen");
            session.Should().NotBeNull();
            nativeProcessHandle.Should().Be(IntPtr.Zero, "der simulierte Pfad ermittelt den Exit-Code über Process.ExitCode, nicht über GetExitCodeProcess");
        }
        finally
        {
            session.Dispose();
            KillIfRunning(process);
        }
    }

    /// <summary>Ein über <see cref="Softwareschmiede.Infrastructure.Terminal.PseudoConsoleSession.InputStream"/>
    /// gesendetes Kommando muss vom laufenden, interaktiven <c>cmd.exe</c> ausgeführt werden und im
    /// <see cref="Softwareschmiede.Infrastructure.Terminal.PseudoConsoleSession.Buffer"/> erscheinen — genau die
    /// Choreografie, die <c>KiAusfuehrungsService.SendCommandDelayedAsync</c> im Produktivpfad verwendet.</summary>
    [Fact]
    public async Task Start_GesendetesKommandoWirdAusgefuehrt()
    {
        var (process, session, _) = _sut.Start(Guid.NewGuid(), Path.GetTempPath(), "echo simuliert");
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

    /// <summary>Nach <see cref="System.Diagnostics.Process.Kill(bool)"/> mit <c>entireProcessTree: true</c> muss
    /// der Prozess innerhalb kurzer Zeit als beendet erkennbar sein — bestätigt Kompatibilität mit
    /// <c>KiAusfuehrungsService.StopCliAsync</c>, das denselben Aufruf für den Stop-Fallback nutzt.</summary>
    [Fact]
    public async Task Start_ProzessBeendetSichAufKillEntireProcessTree()
    {
        var (process, session, _) = _sut.Start(Guid.NewGuid(), Path.GetTempPath(), "echo simuliert");
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

    private static string GetBufferText(PseudoConsoleSession session)
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
