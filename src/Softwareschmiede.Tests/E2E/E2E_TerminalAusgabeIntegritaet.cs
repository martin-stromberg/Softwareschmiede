using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.Infrastructure.Terminal;

namespace Softwareschmiede.Tests.E2E;

/// <summary>
/// E2E-Test: Mehrzeilige CLI-Ausgabe über eine echte ConPTY-Sitzung wird im TerminalBuffer korrekt
/// untereinander dargestellt - kein Treppeneffekt durch ein bloßes Line-Feed, keine ausgelassenen oder
/// verschobenen Zeilen (Akzeptanzkriterium aus Issue 149).
///
/// Voraussetzungen:
/// - Windows-Desktop-Session (kein Headless-CI), Windows 10 Build 17763 oder neuer
///
/// CI-Regular-Lauf: dotnet test --filter "Category!=OsInterface"
/// </summary>
[Trait("Category", "E2E")]
[OsInterface]
public sealed class E2E_TerminalAusgabeIntegritaet
{
    /// <summary>
    /// Szenario: Über eine echte ConPTY-Sitzung (Win32PseudoConsoleProcessLauncher, dieselbe Pipeline wie im
    /// Produktivbetrieb) wird ein PowerShell-Kommando gesendet, das drei Zeilen ausschließlich durch bloßes
    /// Line-Feed (kein Carriage Return) trennt - genau die Rohform, die laut Anforderung zu einem
    /// Treppeneffekt (Text rutscht spaltenweise nach rechts statt in die nächste Zeile) führte. Nach dem Fix
    /// müssen die drei Zeilen auf drei unmittelbar aufeinanderfolgenden Buffer-Zeilen jeweils in Spalte 0
    /// erscheinen.
    /// </summary>
    [SkippableFact]
    public async Task TerminalAusgabe_MehrzeiligeAusgabeMitBlossemLineFeed_ErscheintUntereinanderOhneTreppeneffekt_E2E()
    {
        Skip.If(!ConPtyEnvironmentProbe.IsAvailable, ConPtyEnvironmentProbe.UnavailableReason);

        var launcher = new Win32PseudoConsoleProcessLauncher(
            NullLogger<Win32PseudoConsoleProcessLauncher>.Instance,
            NullLoggerFactory.Instance);

        var (process, session, _) = launcher.Start(Guid.NewGuid(), Path.GetTempPath(), "powershell-mehrzeilig");
        try
        {
            var command = "powershell -NoProfile -Command \"[Console]::Out.Write('ZEILE_A' + [char]10 + 'ZEILE_B' + [char]10 + 'ZEILE_C')\"\r\n";
            await session.InputStream.WriteAsync(Encoding.UTF8.GetBytes(command));
            await session.InputStream.FlushAsync();

            var (rowA, rowB, rowC) = await WaitForThreeMarkersAsync(
                session, "ZEILE_A", "ZEILE_B", "ZEILE_C", TimeSpan.FromSeconds(15));

            rowB.Should().Be(
                rowA + 1,
                "ZEILE_B muss unmittelbar unter ZEILE_A erscheinen - ein bloßes Line-Feed darf keine zusätzliche/fehlende Zeile erzeugen");
            rowC.Should().Be(
                rowB + 1,
                "ZEILE_C muss unmittelbar unter ZEILE_B erscheinen - ein bloßes Line-Feed darf keine zusätzliche/fehlende Zeile erzeugen");
        }
        finally
        {
            session.Dispose();
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
            }
        }
    }

    private static async Task<(int RowA, int RowB, int RowC)> WaitForThreeMarkersAsync(
        PseudoConsoleSession session, string markerA, string markerB, string markerC, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var rowA = FindRow(session, markerA);
            var rowB = FindRow(session, markerB);
            var rowC = FindRow(session, markerC);
            if (rowA >= 0 && rowB >= 0 && rowC >= 0)
                return (rowA, rowB, rowC);

            await Task.Delay(100);
        }

        throw new TimeoutException(
            $"Marker wurden nicht innerhalb von {timeout.TotalSeconds}s im Terminal-Buffer gefunden.");
    }

    private static int FindRow(PseudoConsoleSession session, string marker)
    {
        for (var r = 0; r < session.Buffer.Rows; r++)
        {
            var sb = new StringBuilder();
            foreach (var cell in session.Buffer.GetRow(r))
                sb.Append(cell.Character);

            if (sb.ToString().Contains(marker, StringComparison.Ordinal))
                return r;
        }

        return -1;
    }
}
