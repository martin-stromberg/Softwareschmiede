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

    /// <summary>Anzeigename des liefernden Plugins (für Diagnose/Protokoll).</summary>
    public string PluginName { get; init; } = string.Empty;

    /// <summary>Die beim Spec-Abruf übergebenen optionalen Parameter (für Diagnosezwecke).</summary>
    public string? OptionalParameters { get; init; }
}
