using Microsoft.EntityFrameworkCore;
using Softwareschmiede.StatusServer.Data;

namespace Softwareschmiede.StatusServer.Security;

/// <summary>Validiert das Relay-Zugangstoken einer eingehenden Hub-Verbindung
/// gegen die gehasht in der Datenbank gespeicherten, nicht widerrufenen Tokens.</summary>
public sealed class RelayTokenValidator
{
    private readonly StatusServerDbContext _db;

    /// <summary>Erstellt den Validator.</summary>
    /// <param name="db">Der Datenbankkontext.</param>
    public RelayTokenValidator(StatusServerDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    /// <summary>Prüft, ob das übergebene Token für den Relay-Zugriff berechtigt ist.</summary>
    /// <param name="token">Das vom Client übergebene Token (Query-Parameter <c>access_token</c>
    /// oder Bearer-Header).</param>
    /// <param name="cancellationToken">Abbruchtoken.</param>
    /// <returns><see langword="true"/>, wenn der Zugriff erlaubt ist.</returns>
    public async Task<bool> IsValidAsync(string? token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
            return false;

        var hash = RelayTokenService.HashToken(token);
        var entity = await _db.RelayTokens
            .FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken)
            .ConfigureAwait(false);
        return entity is not null
            && entity.RevokedUtc is null
            && SecretComparer.FixedTimeEquals(entity.TokenHash, hash);
    }
}
