namespace Softwareschmiede.Tests.E2E;

/// <summary>
/// Ob echte Fenster-E2E-Tests (FlaUI treibt das reale WPF-Hauptfenster einer gestarteten
/// Softwareschmiede.App.exe) in dieser Ausführungsumgebung übersprungen werden sollen -
/// gesteuert über die Umgebungsvariable <see cref="SkipEnvVar"/>, nicht über eine automatische
/// Laufzeit-Erkennung (gleiches Muster wie <see cref="ConPtyEnvironmentProbe"/>).
///
/// Hintergrund: Diese Tests benötigen eine interaktive Windows-Desktop-Session. Auf
/// GitHub-gehosteten Runnern (Job "os-interface tests" in staging-ci.yml/pr-staging-ci.yml)
/// existiert kein interaktiver Desktop: Der App-Prozess startet zwar, das Hauptfenster
/// erscheint aber nie für UI Automation, sodass jeder Test erst nach seinem Fenster-Timeout
/// mit einer nichtssagenden <see cref="TimeoutException"/> scheitert. Dort wird die Variable
/// daher gesetzt und die Tests melden ein ehrliches "Skipped"; lokal (Visual Studio,
/// Entwickler-PC) bleibt sie ungesetzt und die Lane deckt die volle Breite ab. Zusätzlich
/// ist sie der Sandbox-Skip für Agenten-Umgebungen ohne Desktop-Session.
/// </summary>
internal static class WpfE2EEnvironmentProbe
{
    private const string SkipEnvVar = "SOFTWARESCHMIEDE_SKIP_WPF_E2E_TESTS";

    /// <summary>
    /// <c>true</c>, wenn echte Fenster-E2E-Tests in dieser Ausführungsumgebung laufen sollen
    /// (Standardfall); <c>false</c>, wenn <see cref="SkipEnvVar"/> auf <c>"1"</c> gesetzt ist.
    /// </summary>
    internal static bool IsAvailable => Environment.GetEnvironmentVariable(SkipEnvVar) != "1";

    /// <summary>Diagnosetext für den Skip-Grund, wenn <see cref="IsAvailable"/> <c>false</c> ist.</summary>
    internal const string UnavailableReason =
        $"Umgebungsvariable {SkipEnvVar}=1 gesetzt - keine interaktive Windows-Desktop-Session " +
        "in dieser Ausführungsumgebung (z. B. GitHub-gehosteter CI-Runner oder Sandbox); " +
        "echte Fenster-E2E-Tests werden übersprungen statt mit TimeoutException zu scheitern.";
}
