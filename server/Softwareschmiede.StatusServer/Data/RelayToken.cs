namespace Softwareschmiede.StatusServer.Data;

/// <summary>Datensatz eines Relay-Zugangstokens. Der Klartext wird niemals gespeichert;
/// in der Datenbank liegt ausschließlich der SHA-256-Hash (<see cref="TokenHash"/>).</summary>
public sealed class RelayToken
{
    /// <summary>Primärschlüssel.</summary>
    public long Id { get; set; }

    /// <summary>Frei vergebene Bezeichnung zur Unterscheidung in der Verwaltung.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>SHA-256-Hash des Token-Klartexts (Base64-kodiert, Unique-Index).</summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>Erstellungszeitpunkt (UTC).</summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>Widerrufszeitpunkt (UTC); <see langword="null"/>, solange das Token gültig ist.</summary>
    public DateTimeOffset? RevokedUtc { get; set; }
}
