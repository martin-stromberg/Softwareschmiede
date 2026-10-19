using Softwareschmiede.Domain.Enums;

namespace Softwareschmiede.Domain.ValueObjects;

/// <summary>Startbeschreibung einer KI-CLI für den interaktiven Terminal-Pfad.
/// Plugins liefern die Spec über <c>IKiPlugin.GetTerminalStartSpecAsync</c>; der Host löst
/// <see cref="FileName"/> anschließend selbst auf und wählt das Backend (PTY oder Pipe).</summary>
public sealed record TerminalSessionStartSpec
{
    /// <summary>Executable-Name oder -Pfad (darf nackter Befehlsname, <c>.cmd</c>/<c>.bat</c>-Shim oder absoluter Pfad sein).</summary>
    public string FileName { get; init; } = string.Empty;

    /// <summary>Argumente für den Start.</summary>
    public string Arguments { get; init; } = string.Empty;

    /// <summary>Arbeitsverzeichnis des CLI-Prozesses.</summary>
    public string WorkingDirectory { get; init; } = string.Empty;

    /// <summary>Zusätzliche Umgebungsvariablen des Prozesses (überschreiben die geerbte Umgebung).</summary>
    public IReadOnlyDictionary<string, string?> EnvironmentVariables { get; init; } = new Dictionary<string, string?>();

    /// <summary>Terminal-Fähigkeiten der CLI (PTY-Unterstützung/-Bedarf).</summary>
    public TerminalProviderCapabilities Capabilities { get; init; } = TerminalProviderCapabilities.SupportsPty;

    /// <summary>Verzögerung, mit der <c>ITerminalSession.WritePromptAsync</c> den abschließenden
    /// Submit (<c>\r</c>) als separaten Eingabe-Batch hinter dem Prompttext sendet.
    /// <c>TimeSpan.Zero</c> (Standard): Text und Submit werden wie bisher in einem einzigen
    /// Schreibvorgang übertragen. Werte &gt; 0 sind für CLIs gedacht, die innerhalb eines
    /// gemeinsamen Eingabe-Batches ein <c>\r</c> nicht als Enter-Submit, sondern als
    /// Zeilenumbruch werten (Paste-Semantik) — z. B. die Devin CLI.</summary>
    public TimeSpan PromptSubmitDelay { get; init; } = TimeSpan.Zero;

    /// <summary>Anzahl zusätzlicher Submit-Versuche (<c>\r</c>) nach dem ersten verzögerten
    /// Submit; wirkt nur bei gesetztem <see cref="PromptSubmitDelay"/>. <c>0</c> (Standard)
    /// sendet den Submit genau einmal. Für CLIs gedacht, deren Burst-/Paste-Erkennung
    /// eingangsgrößenabhängig ein zu früh ankommendes <c>\r</c> als Zeilenumbruch verwirft:
    /// Ein verschluckter Submit ist harmlos (zusätzlicher Zeilenumbruch im Entwurf), ein
    /// späterer Wiederholungsversuch submitted zuverlässig — zusätzliche <c>\r</c> nach einem
    /// bereits erfolgten Submit werden als Enter auf leerem/bearbeitendem Prompt ignoriert.</summary>
    public int PromptSubmitRepeatCount { get; init; }

    /// <summary>Abstand zwischen zwei aufeinanderfolgenden Submit-Versuchen; wirkt nur bei
    /// <see cref="PromptSubmitRepeatCount"/> &gt; 0.</summary>
    public TimeSpan PromptSubmitRepeatInterval { get; init; } = TimeSpan.Zero;

    /// <summary>Anzeigename des liefernden Plugins (für Diagnose/Protokoll).</summary>
    public string PluginName { get; init; } = string.Empty;

    /// <summary>Die beim Spec-Abruf übergebenen optionalen Parameter (für Diagnosezwecke).</summary>
    public string? OptionalParameters { get; init; }
}
