using Microsoft.Extensions.Options;
using Softwareschmiede.StatusServer.Options;

namespace Softwareschmiede.StatusServer.Security;

/// <summary>Validiert das Relay-Zugangstoken einer eingehenden Hub-Verbindung.</summary>
public sealed class RelayTokenValidator
{
    private readonly StatusServerOptions _options;
    private readonly bool _isDevelopment;

    /// <summary>Erstellt den Validator.</summary>
    /// <param name="options">Die Server-Konfiguration.</param>
    /// <param name="environment">Die aktuelle Hosting-Umgebung.</param>
    public RelayTokenValidator(
        IOptions<StatusServerOptions> options,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(environment);

        _options = options.Value;
        _isDevelopment = environment.IsDevelopment();
    }

    /// <summary>Prüft, ob das übergebene Token für den Relay-Zugriff berechtigt ist.</summary>
    /// <param name="token">Das vom Client übergebene Token (Query-Parameter <c>access_token</c>).</param>
    /// <returns><see langword="true"/>, wenn der Zugriff erlaubt ist.</returns>
    public bool IsValid(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return false;

        if (_isDevelopment && !string.IsNullOrEmpty(_options.Relay.DevToken)
            && SecretComparer.FixedTimeEquals(token, _options.Relay.DevToken))
            return true;

        return _options.Relay.Tokens.Any(allowed => SecretComparer.FixedTimeEquals(token, allowed));
    }
}
