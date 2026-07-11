using System.Diagnostics;
using Microsoft.Win32.SafeHandles;
using Softwareschmiede.Infrastructure.Terminal;

namespace Softwareschmiede.Tests.E2E;

/// <summary>
/// Prüft einmalig pro Testlauf, ob ein per ConPTY gestarteter Kindprozess in dieser
/// Ausführungsumgebung tatsächlich an die Pseudo-Konsole angebunden wird, statt an eine
/// Ambient-Konsole mit sofortigem EOF auf stdin zu geraten (siehe
/// docs/features/task/issue-114-.../e2e-timeout-analyse.md, Abschnitt "Nachtrag Iteration 5":
/// belegt durch leeren Terminal-Buffer + im rohen Testkonsolen-Output sichtbaren cmd.exe-Prompt).
///
/// Startet dazu denselben Codepfad wie <c>KiAusfuehrungsService.StartPseudoConsoleProcess</c>
/// (PseudoConsole.Create + PseudoConsoleProcessStarter.Start), lässt cmd.exe ein eindeutiges
/// Sentinel echoen und prüft, ob es im <see cref="PseudoConsoleSession.Buffer"/> ankommt. Kommt es
/// nicht an, ist ConPTY-Konsolen-Isolation in dieser Umgebung nicht verfügbar - betroffene Tests
/// sollen sich dann selbst überspringen (<c>Assert.Skip</c>) statt mit einem nichtssagenden
/// Timeout zu scheitern.
/// </summary>
internal static class ConPtyEnvironmentProbe
{
    private const string Sentinel = "CONPTY_PROBE_OK";

    private static readonly Lazy<bool> IsAvailableLazy = new(Probe);

    /// <summary>
    /// <c>true</c>, wenn ConPTY-Kindprozesse in dieser Umgebung nachweislich isoliert laufen
    /// (Sentinel-Ausgabe kam im Buffer an); sonst <c>false</c>. Ergebnis wird pro Testlauf gecacht.
    /// </summary>
    internal static bool IsAvailable => IsAvailableLazy.Value;

    private static bool Probe()
    {
        PseudoConsole? pseudoConsole = null;
        PseudoConsoleSession? session = null;
        try
        {
            pseudoConsole = PseudoConsole.Create(80, 25);

            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c echo {Sentinel}",
                UseShellExecute = false,
            };

            var startResult = PseudoConsoleProcessStarter.Start(psi, pseudoConsole);

            Process process;
            try
            {
                process = Process.GetProcessById(startResult.Pid);
            }
            catch
            {
                PseudoConsoleNativeMethods.CloseHandle(startResult.ProcessHandle);
                throw;
            }

            var inputStream = new FileStream(
                new SafeFileHandle(pseudoConsole.InputWritePipe, ownsHandle: false),
                FileAccess.Write, bufferSize: 1, isAsync: false);
            var outputStream = new FileStream(
                new SafeFileHandle(pseudoConsole.OutputReadPipe, ownsHandle: false),
                FileAccess.Read, bufferSize: 4096, isAsync: false);

            session = new PseudoConsoleSession(pseudoConsole, process, inputStream, outputStream);

            var deadline = DateTime.UtcNow.AddSeconds(3);
            while (DateTime.UtcNow < deadline)
            {
                for (var row = 0; row < 3; row++)
                {
                    var text = string.Concat(session.Buffer.GetRow(row).Select(c => c.Character));
                    if (text.Contains(Sentinel, StringComparison.Ordinal))
                        return true;
                }

                Thread.Sleep(50);
            }

            return false;
        }
        catch
        {
            return false;
        }
        finally
        {
            session?.Dispose();
            pseudoConsole?.Dispose();
        }
    }
}
