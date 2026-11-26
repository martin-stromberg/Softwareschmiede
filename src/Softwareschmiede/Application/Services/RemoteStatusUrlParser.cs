namespace Softwareschmiede.Application.Services;

/// <summary>Normalisiert und validiert die konfigurierte Basis-URL der Remote-Übersicht
/// zu einer Hub-URL (<c>http(s)</c>/<c>ws(s)</c> → Hub-Pfad <c>/relay</c>).</summary>
public static class RemoteStatusUrlParser
{
    /// <summary>Der Hub-Pfad des Status-Servers.</summary>
    public const string RelayHubPath = "/relay";

    /// <summary>Erzeugt die Hub-URL aus der Benutzereingabe.</summary>
    /// <param name="input">Die konfigurierte Basis-URL (<c>http(s)://</c> oder <c>ws(s)://</c>).</param>
    /// <param name="hubUrl">Die normalisierte Hub-URL oder <see langword="null"/> bei Fehler.</param>
    /// <param name="error">Eine verständliche Fehlermeldung oder <see langword="null"/>.</param>
    /// <returns><see langword="true"/>, wenn die Eingabe zu einer gültigen Hub-URL führt.</returns>
    public static bool TryCreateHubUrl(string? input, out Uri? hubUrl, out string? error)
    {
        hubUrl = null;
        error = null;

        if (string.IsNullOrWhiteSpace(input))
        {
            error = "Die Server-URL fehlt.";
            return false;
        }

        if (!Uri.TryCreate(input.Trim(), UriKind.Absolute, out var uri) || string.IsNullOrEmpty(uri.Host))
        {
            error = "Die Server-URL ist keine gültige absolute Adresse.";
            return false;
        }

        var scheme = uri.Scheme.ToLowerInvariant() switch
        {
            "ws" => "http",
            "wss" => "https",
            var s => s,
        };
        if (scheme is not ("http" or "https"))
        {
            error = "Die Server-URL muss mit http(s):// oder ws(s):// beginnen.";
            return false;
        }

        var builder = new UriBuilder(uri) { Scheme = scheme };
        var path = builder.Path?.TrimEnd('/') ?? string.Empty;
        builder.Path = path.EndsWith(RelayHubPath, StringComparison.OrdinalIgnoreCase)
            ? path
            : path + RelayHubPath;

        hubUrl = builder.Uri;
        return true;
    }
}
