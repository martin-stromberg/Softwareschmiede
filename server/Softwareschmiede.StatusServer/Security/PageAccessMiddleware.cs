using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using Softwareschmiede.StatusServer.Options;

namespace Softwareschmiede.StatusServer.Security;

/// <summary>
/// Optionaler Basic-Auth-Zugriffsschutz für die Seiten-Endpunkte (<c>/</c>, <c>/status.json</c>).
/// Der Relay-Endpunkt <c>/relay</c> ist ausgenommen (dort gilt die Token-Prüfung des Hubs).
/// Ohne konfigurierte Zugangsdaten ist der Schutz deaktiviert (lokaler Dev-Modus).
/// </summary>
public sealed class PageAccessMiddleware
{
    private readonly RequestDelegate _next;
    private readonly StatusServerOptions _options;

    /// <summary>Erstellt die Middleware.</summary>
    /// <param name="next">Der nächste Middleware-Delegat.</param>
    /// <param name="options">Die Server-Konfiguration.</param>
    public PageAccessMiddleware(RequestDelegate next, IOptions<StatusServerOptions> options)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(options);

        _next = next;
        _options = options.Value;
    }

    /// <summary>Prüft die Basic-Auth-Anmeldedaten für geschützte Pfade.</summary>
    /// <param name="context">Der HTTP-Kontext.</param>
    /// <returns>Eine Aufgabe für die Pipeline.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Request.Path.StartsWithSegments("/relay")
            || string.IsNullOrEmpty(_options.Page.Username)
            || string.IsNullOrEmpty(_options.Page.Password)
            || IsAuthorized(context))
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.Headers.WWWAuthenticate = "Basic realm=\"Softwareschmiede-Status\", charset=\"UTF-8\"";
        await context.Response.WriteAsync("Zugriff verweigert.").ConfigureAwait(false);
    }

    private bool IsAuthorized(HttpContext context)
    {
        var header = context.Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
            return false;

        try
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(header[6..].Trim()));
            var separatorIndex = decoded.IndexOf(':', StringComparison.Ordinal);
            if (separatorIndex < 0)
                return false;

            var username = decoded[..separatorIndex];
            var password = decoded[(separatorIndex + 1)..];
            return SecretComparer.FixedTimeEquals(username, _options.Page.Username!)
                && SecretComparer.FixedTimeEquals(password, _options.Page.Password!);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
