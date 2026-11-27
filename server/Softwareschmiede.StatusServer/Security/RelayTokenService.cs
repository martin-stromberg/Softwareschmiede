using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Softwareschmiede.StatusServer.Data;

namespace Softwareschmiede.StatusServer.Security;

/// <summary>Verwaltung der Relay-Zugangstokens: Erzeugung (Klartext einmalig, nur
/// SHA-256-Hash persistiert), Auflistung und Widerruf.</summary>
public sealed class RelayTokenService
{
    /// <summary>Maximale Länge eines Token-Labels.</summary>
    public const int MaxLabelLength = 200;

    private readonly StatusServerDbContext _db;
    private readonly TimeProvider _timeProvider;

    /// <summary>Erstellt den Service.</summary>
    /// <param name="db">Der Datenbankkontext.</param>
    /// <param name="timeProvider">Die Zeitquelle.</param>
    public RelayTokenService(StatusServerDbContext db, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _db = db;
        _timeProvider = timeProvider;
    }

    /// <summary>Erzeugt ein neues Relay-Token. Der Klartext wird ausschließlich
    /// im Ergebnis zurückgegeben und ist danach nicht mehr abrufbar.</summary>
    /// <param name="label">Die Bezeichnung des Tokens (1–<see cref="MaxLabelLength"/> Zeichen).</param>
    /// <param name="cancellationToken">Abbruchtoken.</param>
    /// <returns>Der persistierte Datensatz und der einmalig sichtbare Klartext.</returns>
    public async Task<CreatedRelayToken> CreateAsync(string label, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(label);
        label = label.Trim();
        if (label.Length is 0 or > MaxLabelLength)
        {
            throw new ArgumentException(
                $"Das Label muss zwischen 1 und {MaxLabelLength} Zeichen lang sein.", nameof(label));
        }

        var plaintext = Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var entity = new RelayToken
        {
            Label = label,
            TokenHash = HashToken(plaintext),
            CreatedUtc = _timeProvider.GetUtcNow(),
        };
        _db.RelayTokens.Add(entity);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new CreatedRelayToken(entity, plaintext);
    }

    /// <summary>Liefert den Datensatz eines Tokens anhand seiner Id.</summary>
    /// <param name="id">Die Id des Tokens.</param>
    /// <param name="cancellationToken">Abbruchtoken.</param>
    /// <returns>Der Datensatz oder <see langword="null"/>, wenn kein Token existiert.</returns>
    public async Task<RelayToken?> FindAsync(long id, CancellationToken cancellationToken = default)
        => await _db.RelayTokens
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            .ConfigureAwait(false);

    /// <summary>Widerruft ein Token (setzt <see cref="RelayToken.RevokedUtc"/>).</summary>
    /// <param name="id">Die Id des Tokens.</param>
    /// <param name="cancellationToken">Abbruchtoken.</param>
    /// <returns><see langword="true"/>, wenn das Token gefunden und widerrufen wurde.</returns>
    public async Task<bool> RevokeAsync(long id, CancellationToken cancellationToken = default)
    {
        var token = await _db.RelayTokens
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (token is null)
            return false;
        if (token.RevokedUtc is null)
        {
            token.RevokedUtc = _timeProvider.GetUtcNow();
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return true;
    }

    /// <summary>Listet alle Tokens (neueste zuerst).</summary>
    /// <param name="cancellationToken">Abbruchtoken.</param>
    /// <returns>Die Token-Datensätze ohne Klartext.</returns>
    public async Task<IReadOnlyList<RelayToken>> ListAsync(CancellationToken cancellationToken = default)
        => await _db.RelayTokens
            .OrderByDescending(t => t.CreatedUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <summary>Bildet den gespeicherten SHA-256-Hash (Base64) eines Token-Klartexts.</summary>
    /// <param name="plaintext">Der Token-Klartext.</param>
    /// <returns>Der Base64-kodierte SHA-256-Hash.</returns>
    public static string HashToken(string plaintext)
        => Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(plaintext)));

    private static string Base64UrlEncode(byte[] bytes)
        => Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
}

/// <summary>Ergebnis von <see cref="RelayTokenService.CreateAsync"/>:
/// der persistierte Datensatz plus der einmalig sichtbare Klartext.</summary>
/// <param name="Token">Der persistierte Token-Datensatz.</param>
/// <param name="Plaintext">Der Klartext (wird nur hier einmalig zurückgegeben).</param>
public sealed record CreatedRelayToken(RelayToken Token, string Plaintext);
