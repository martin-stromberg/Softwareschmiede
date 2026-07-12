namespace Softwareschmiede.Infrastructure.Terminal;

/// <summary>Entkoppelt <see cref="PseudoConsoleSession"/> von der konkreten <see cref="PseudoConsole"/>-Klasse.</summary>
internal interface IPseudoConsoleHandle : IDisposable
{
    /// <summary>Ändert die Größe der Pseudo Console.</summary>
    /// <param name="cols">Neue Spaltenanzahl.</param>
    /// <param name="rows">Neue Zeilenanzahl.</param>
    /// <returns><c>true</c>, wenn die Größenänderung erfolgreich war; andernfalls <c>false</c>.</returns>
    bool Resize(short cols, short rows);
}
