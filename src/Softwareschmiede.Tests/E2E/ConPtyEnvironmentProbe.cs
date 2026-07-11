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
/// sollen sich dann selbst überspringen statt mit einem nichtssagenden Timeout zu scheitern.
///
/// Zwei Sicherungen gegen falsch-negative Ergebnisse (beobachtet: Probe meldete in einer
/// Umgebung "nicht verfügbar", in der die 14 betroffenen Tests zuvor nachweislich liefen):
/// 1. Zwei Versuche statt einem - ein einzelner kalter/langsamer erster Prozessstart (JIT-Warmup,
///    Virenscanner-Prüfung der frisch erzeugten cmd.exe, System unter Last durch parallele
///    Testausführung) darf nicht das Ergebnis für den kompletten Testlauf verfälschen.
/// 2. Grosszuegiges Timeout (8s statt der urspruenglichen 3s) je Versuch.
/// Schlaegt die Probe dennoch fehl, wird der tatsaechliche Grund (Exception oder "Timeout ohne
/// Sentinel") in <see cref="UnavailableReason"/> festgehalten und im Skip-Grund der betroffenen
/// Tests ausgegeben, statt ihn stillschweigend zu verschlucken.
/// </summary>
internal static class ConPtyEnvironmentProbe
{
    private const string Sentinel = "CONPTY_PROBE_OK";
    private const int Attempts = 2;
    private static readonly TimeSpan PerAttemptTimeout = TimeSpan.FromSeconds(8);

    private static readonly Lazy<(bool Available, string? Reason)> Result = new(RunProbe);

    /// <summary>
    /// <c>true</c>, wenn ConPTY-Kindprozesse in dieser Umgebung nachweislich isoliert laufen
    /// (Sentinel-Ausgabe kam im Buffer an, in mindestens einem von <see cref="Attempts"/>
    /// Versuchen); sonst <c>false</c>. Ergebnis wird pro Testlauf gecacht.
    /// </summary>
    internal static bool IsAvailable => Result.Value.Available;

    /// <summary>
    /// Diagnosetext, warum die Probe fehlgeschlagen ist (Exception-Meldung oder "Timeout"), oder
    /// <c>null</c>, wenn <see cref="IsAvailable"/> <c>true</c> ist. Wird im Skip-Grund der
    /// betroffenen Tests ausgegeben, damit ein falsch-negatives Ergebnis nachvollziehbar bleibt.
    /// </summary>
    internal static string? UnavailableReason => Result.Value.Reason;

    private static (bool Available, string? Reason) RunProbe()
    {
        string? lastReason = null;
        for (var attempt = 1; attempt <= Attempts; attempt++)
        {
            var (ok, reason) = TryOnce();
            if (ok)
                return (true, null);
            lastReason = reason;
        }

        return (false, lastReason);
    }

    private static (bool Ok, string Reason) TryOnce()
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

            var deadline = DateTime.UtcNow.Add(PerAttemptTimeout);
            while (DateTime.UtcNow < deadline)
            {
                for (var row = 0; row < 5; row++)
                {
                    var text = string.Concat(session.Buffer.GetRow(row).Select(c => c.Character));
                    if (text.Contains(Sentinel, StringComparison.Ordinal))
                        return (true, "");
                }

                Thread.Sleep(50);
            }

            return (false, $"Timeout nach {PerAttemptTimeout.TotalSeconds}s ohne Sentinel-Ausgabe im Terminal-Buffer.");
        }
        catch (Exception ex)
        {
            return (false, $"{ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            session?.Dispose();
            pseudoConsole?.Dispose();
        }
    }
}
