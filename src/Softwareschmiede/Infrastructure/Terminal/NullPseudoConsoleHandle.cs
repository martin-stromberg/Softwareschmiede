namespace Softwareschmiede.Infrastructure.Terminal;

/// <summary>No-Op-Implementierung von <see cref="IPseudoConsoleHandle"/> für simulierte, nicht über ConPTY laufende Sitzungen.</summary>
internal sealed class NullPseudoConsoleHandle : IPseudoConsoleHandle
{
    /// <summary>Die einzige Instanz von <see cref="NullPseudoConsoleHandle"/>.</summary>
    /// <value>Die Singleton-Instanz.</value>
    internal static readonly NullPseudoConsoleHandle Instance = new();

    private NullPseudoConsoleHandle()
    {
    }

    /// <inheritdoc/>
    public bool Resize(short cols, short rows) => true;

    /// <inheritdoc/>
    public void Dispose()
    {
    }
}
