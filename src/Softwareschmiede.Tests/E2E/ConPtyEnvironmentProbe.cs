using System.Diagnostics;

namespace Softwareschmiede.Tests.E2E;

/// <summary>
/// Prüft einmalig pro Testlauf, ob ein per ConPTY gestarteter Kindprozess in dieser
/// Ausführungsumgebung tatsächlich an die Pseudo-Konsole angebunden wird, statt an eine
/// Ambient-Konsole mit sofortigem EOF auf stdin zu geraten (siehe
/// docs/features/task/issue-114-.../e2e-timeout-analyse.md: belegt durch leeren
/// Terminal-Buffer + im rohen Testkonsolen-Output sichtbaren cmd.exe-Prompt).
///
/// WICHTIG: Die Probe startet den ConPTY-Testprozess NICHT direkt aus dem Test-Host
/// (testhost.exe/vstest.console.exe), sondern aus einer echten <c>Softwareschmiede.App.exe</c>-
/// Instanz heraus (Flag <c>--conpty-probe</c>, siehe App.xaml.cs.RunConPtyProbeAndExit). Ein
/// erster Versuch, direkt aus dem Test-Host zu pruefen, lieferte in einer Umgebung ein
/// falsch-negatives Ergebnis (Visual Studio, wo die betroffenen 14 Tests nachweislich liefen) -
/// vermutlich weil die Prozessbaum-Tiefe fuer Windows' Konsolen-/Session-Vererbung relevant ist:
/// die echte Anwendung erzeugt den ConPTY-Kindprozess als Enkelkind des Test-Hosts
/// (Test-Host -> Softwareschmiede.App.exe -> cmd.exe), waehrend ein aus dem Test-Host direkt
/// gestarteter ConPTY-Kindprozess nur ein Kind ist (Test-Host -> cmd.exe) - ein strukturell
/// anderer, nicht repraesentativer Codepfad. Die Probe repliziert daher exakt den echten
/// Prozessbaum.
/// </summary>
internal static class ConPtyEnvironmentProbe
{
    private const string BuildConfigDebug = "Debug";
    private const string BuildConfigRelease = "Release";
    private const string TargetFramework = "net10.0-windows10.0.17763.0";
    private const int Attempts = 2;
    private static readonly TimeSpan PerAttemptTimeout = TimeSpan.FromSeconds(25);

    private static readonly Lazy<(bool Available, string? Reason)> Result = new(RunProbe);

    /// <summary>
    /// <c>true</c>, wenn ConPTY-Kindprozesse in dieser Umgebung nachweislich isoliert laufen
    /// (Sentinel-Ausgabe kam im Buffer der per <c>--conpty-probe</c> gestarteten App-Instanz an,
    /// in mindestens einem von <see cref="Attempts"/> Versuchen); sonst <c>false</c>. Ergebnis
    /// wird pro Testlauf gecacht.
    /// </summary>
    internal static bool IsAvailable => Result.Value.Available;

    /// <summary>
    /// Diagnosetext, warum die Probe fehlgeschlagen ist, oder <c>null</c>, wenn
    /// <see cref="IsAvailable"/> <c>true</c> ist. Wird im Skip-Grund der betroffenen Tests
    /// ausgegeben, damit ein falsch-negatives Ergebnis nachvollziehbar bleibt.
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
        string appExePath;
        try
        {
            appExePath = ResolveAppExePath();
        }
        catch (Exception ex)
        {
            return (false, $"Softwareschmiede.App.exe nicht gefunden: {ex.Message}");
        }

        Process? process = null;
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = appExePath,
                Arguments = "--conpty-probe",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
            };

            process = Process.Start(psi);
            if (process is null)
                return (false, "Softwareschmiede.App.exe --conpty-probe konnte nicht gestartet werden (Process.Start lieferte null).");

            var output = process.StandardOutput.ReadToEnd();
            var exited = process.WaitForExit((int)PerAttemptTimeout.TotalMilliseconds);
            if (!exited)
            {
                TryKill(process);
                return (false, $"Timeout nach {PerAttemptTimeout.TotalSeconds}s - Softwareschmiede.App.exe --conpty-probe hat nicht rechtzeitig geantwortet.");
            }

            // Die Ausgabe kann mehrere Zeilen enthalten (siehe App.xaml.cs-Kommentar: die
            // gestartete cmd.exe kann ihrerseits Text auf denselben, geerbten stdout-Handle
            // schreiben, falls die ConPTY-Isolation - wie in der urspruenglich untersuchten
            // Sandbox - nicht funktioniert). Massgeblich ist die letzte "OK"/"FAIL:..."-Zeile.
            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var resultLine = lines.LastOrDefault(l => l == "OK" || l.StartsWith("FAIL:", StringComparison.Ordinal));

            if (resultLine == "OK")
                return (true, "");
            if (resultLine is not null)
                return (false, resultLine["FAIL:".Length..]);

            return (false, $"Unerwartete/leere Ausgabe von Softwareschmiede.App.exe --conpty-probe: '{output.Trim()}'");
        }
        catch (Exception ex)
        {
            return (false, $"{ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            process?.Dispose();
        }
    }

    private static void TryKill(Process process)
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

    private static string ResolveAppExePath()
    {
        var baseDir = AppContext.BaseDirectory;

        var candidates = new[]
        {
            Path.GetFullPath(Path.Combine(
                baseDir, "..", "..", "..", "..",
                "Softwareschmiede.App", "bin", BuildConfigDebug, TargetFramework,
                "Softwareschmiede.App.exe")),
            Path.GetFullPath(Path.Combine(
                baseDir, "..", "..", "..", "..",
                "Softwareschmiede.App", "bin", BuildConfigRelease, TargetFramework,
                "Softwareschmiede.App.exe")),
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
                return candidate;
        }

        throw new FileNotFoundException(
            $"Gesuchte Pfade:{Environment.NewLine}{string.Join(Environment.NewLine, candidates)}");
    }
}
