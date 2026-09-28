namespace Softwareschmiede.App.ViewModels;

/// <summary>Zeilenmodell der Quell-Ansicht im Konsolentestfenster (ein Eintrag pro aufgezeichnetem Chunk).</summary>
public sealed class CliChunkAnzeigeEintrag
{
    /// <summary>Laufende Nummer des Chunks in der Aufzeichnung (1-basiert, Spalte „#") —
    /// stimmt mit der Positionszählung des PositionsText („Chunk n/y" = n angewendete
    /// Chunks, Zeile „#n" ist der zuletzt angewendete) überein.</summary>
    public required int Index { get; init; }

    /// <summary>Zeitlicher Offset des Chunks seit Aufzeichnungsbeginn.</summary>
    public required TimeSpan Offset { get; init; }

    /// <summary>Länge des Chunks in Bytes.</summary>
    public required int Laenge { get; init; }

    /// <summary>Quelltext des Chunks mit sichtbar gemachten Steuersequenzen.</summary>
    public required string Quelltext { get; init; }
}
