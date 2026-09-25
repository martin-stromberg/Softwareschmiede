namespace Softwareschmiede.Infrastructure.Terminal;

/// <summary>Laufzeitparameter der Terminal-Integration (Konfigurationssektion <c>Terminal</c>).</summary>
public sealed class TerminalSessionOptions
{
    /// <summary>Name des Konfigurationsabschnitts in appsettings.json.</summary>
    public const string SectionName = "Terminal";

    /// <summary>Byte-Budget des <see cref="TerminalReplayBuffer"/> pro Session (Default: 512 KB).</summary>
    public int ReplayBufferByteBudget { get; set; } = 512 * 1024;

    /// <summary>Initiale Spaltenanzahl beim Session-Start.</summary>
    public int DefaultCols { get; set; } = 220;

    /// <summary>Initiale Zeilenanzahl beim Session-Start.</summary>
    public int DefaultRows { get; set; } = 50;
}
