namespace Softwareschmiede.Tests.E2E;

/// <summary>
/// Ob ConPTY-abhängige E2E-Tests in dieser Ausführungsumgebung übersprungen werden sollen -
/// gesteuert über die Umgebungsvariable <see cref="SkipEnvVar"/>, nicht über eine automatische
/// Laufzeit-Erkennung.
///
/// Vorgeschichte (siehe docs/features/task/issue-114-.../e2e-timeout-analyse.md): Es gab zwei
/// Anläufe mit einer dynamischen Probe, die per echtem ConPTY-Sentinel-Test zur Laufzeit prüfte,
/// ob die Konsolen-Isolation funktioniert (zuerst aus dem Test-Host heraus, dann - nach einem
/// gemeldeten falschen Negativ - aus einer echten Softwareschmiede.App.exe-Instanz heraus, um den
/// Prozessbaum der echten Anwendung nachzubilden). Beide Varianten meldeten "nicht verfügbar" auch
/// in einer Umgebung (Visual Studio), in der dieselben 14 Tests nachweislich liefen - der genaue
/// Unterschied zur echten Anwendung (interaktives cmd.exe nach vollständigem WPF-Start, statt
/// `cmd.exe /c` synchron in App.OnStartup) ließ sich ohne Zugriff auf eine funktionierende
/// Referenzumgebung nicht zuverlässig weiter eingrenzen. Eine automatische Erkennung, die in einer
/// funktionierenden Umgebung fälschlich "nicht verfügbar" meldet, ist schlimmer als der
/// ursprüngliche Zustand (echte Tests werden unbemerkt übersprungen) - deshalb jetzt stattdessen
/// eine explizite, vom aufrufenden Kontext gesetzte Umgebungsvariable: Der KI-Agent setzt sie
/// gezielt für die eine Sandbox, in der die Limitation bestätigt reproduzierbar ist (siehe
/// CLAUDE.md, Abschnitt Testing); jede andere Umgebung (Visual Studio, ein Entwickler-PC, CI sofern
/// nicht explizit anders konfiguriert) lässt sie unverändert und die Tests laufen normal.
/// </summary>
internal static class ConPtyEnvironmentProbe
{
    private const string SkipEnvVar = "SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS";

    /// <summary>
    /// <c>true</c>, wenn die 14 ConPTY-abhängigen E2E-Tests in dieser Ausführungsumgebung laufen
    /// sollen (Standardfall); <c>false</c>, wenn <see cref="SkipEnvVar"/> auf <c>"1"</c> gesetzt ist.
    /// </summary>
    internal static bool IsAvailable => Environment.GetEnvironmentVariable(SkipEnvVar) != "1";

    /// <summary>Diagnosetext für den Skip-Grund, wenn <see cref="IsAvailable"/> <c>false</c> ist.</summary>
    internal const string UnavailableReason =
        $"Umgebungsvariable {SkipEnvVar}=1 gesetzt (siehe CLAUDE.md, Abschnitt Testing) - " +
        "bestätigte Sandbox-Limitation dieses Ausführungskontexts, keine automatische Erkennung.";
}
