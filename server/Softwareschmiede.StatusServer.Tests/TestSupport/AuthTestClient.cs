using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Softwareschmiede.StatusServer.Tests.TestSupport;

/// <summary>Hilfs-Client für authentifizierte HTTP-Flows gegen den StatusServer:
/// Cookie-Verwaltung, Antiforgery-Extraktion aus gerendertem HTML und Formular-POSTs.</summary>
public sealed class AuthTestClient : IDisposable
{
    private static readonly Regex TokenFieldRegex = new(
        "name=\"(?<field>[^\"]*VerificationToken[^\"]*)\"\\s+value=\"(?<value>[^\"]+)\"",
        RegexOptions.Compiled);

    private AuthTestClient(HttpClient client) => Http = client;

    /// <summary>Der zugrunde liegende <see cref="HttpClient"/> (Cookie-Verwaltung aktiv).</summary>
    public HttpClient Http { get; }

    /// <summary>Der Feldname des zuletzt extrahierten Antiforgery-Hidden-Felds.</summary>
    public string AntiforgeryFieldName { get; private set; } = "__RequestVerificationToken";

    /// <summary>Das zuletzt extrahierte Antiforgery-Request-Token.</summary>
    public string AntiforgeryToken { get; private set; } = string.Empty;

    /// <summary>Erstellt einen cookie-fähigen Client für die übergebene Factory.</summary>
    /// <param name="factory">Die Server-Factory.</param>
    /// <param name="allowAutoRedirect">Ob Redirects automatisch gefolgt werden soll
    /// (Standard: <see langword="false"/>, damit Redirect-Ziele assertierbar bleiben).</param>
    /// <returns>Den Test-Client.</returns>
    public static AuthTestClient Create(StatusWebApplicationFactory factory, bool allowAutoRedirect = false)
        => new(factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
            AllowAutoRedirect = allowAutoRedirect,
        }));

    /// <summary>Ruft eine Seite ab und extrahiert das darin versteckte Antiforgery-Token.</summary>
    /// <param name="path">Der abzurufende Pfad (muss ein Formular mit Antiforgery-Feld rendern).</param>
    /// <param name="cancellationToken">Abbruchtoken.</param>
    /// <returns>Der HTML-Inhalt der Seite.</returns>
    public async Task<string> RefreshAntiforgeryAsync(string path, CancellationToken cancellationToken = default)
    {
        var html = await Http.GetStringAsync(path, cancellationToken).ConfigureAwait(false);
        ExtractAntiforgery(html, path);
        return html;
    }

    /// <summary>Extrahiert das Antiforgery-Token aus einem bereits vorliegenden
    /// HTML-Dokument (z. B. aus der Antwortseite eines POST).</summary>
    /// <param name="html">Das HTML-Dokument.</param>
    /// <param name="source">Optionale Quellangabe für die Fehlermeldung.</param>
    public void ExtractAntiforgery(string html, string source = "HTML-Antwort")
    {
        var match = TokenFieldRegex.Match(html);
        if (!match.Success)
            throw new InvalidOperationException($"Kein Antiforgery-Feld in '{source}' gefunden.");

        AntiforgeryFieldName = match.Groups["field"].Value;
        AntiforgeryToken = match.Groups["value"].Value;
    }

    /// <summary>Sendet ein Formular als <c>application/x-www-form-urlencoded</c>-POST.</summary>
    /// <param name="path">Der Zielpfad.</param>
    /// <param name="fields">Die Formularfelder (Antiforgery-Feld wird bei
    /// <paramref name="includeAntiforgery"/> automatisch ergänzt).</param>
    /// <param name="includeAntiforgery">Ob das extrahierte Antiforgery-Token mitgesendet wird.</param>
    /// <param name="cancellationToken">Abbruchtoken.</param>
    /// <returns>Die Server-Antwort.</returns>
    public async Task<HttpResponseMessage> PostFormAsync(
        string path,
        IEnumerable<KeyValuePair<string, string>>? fields = null,
        bool includeAntiforgery = true,
        CancellationToken cancellationToken = default)
    {
        var list = fields?.ToList() ?? [];
        if (includeAntiforgery)
            list.Add(new KeyValuePair<string, string>(AntiforgeryFieldName, AntiforgeryToken));

        return await Http.PostAsync(path, new FormUrlEncodedContent(list), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Folgt dem Location-Header einer Redirect-Antwort und liefert den
    /// HTML-Inhalt der Zielseite (Post/Redirect/Get-Flows).</summary>
    /// <param name="response">Die Redirect-Antwort eines POST.</param>
    /// <param name="cancellationToken">Abbruchtoken.</param>
    /// <returns>Der HTML-Inhalt des Redirect-Ziels.</returns>
    public async Task<string> FollowRedirectAsync(HttpResponseMessage response, CancellationToken cancellationToken = default)
    {
        var location = response.Headers.Location?.OriginalString
            ?? throw new InvalidOperationException("Antwort enthält keinen Location-Header.");
        return await Http.GetStringAsync(location, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Durchläuft die Erst-Registrierung: holt das Antiforgery-Token von
    /// <c>GET /register</c> und sendet das Formular.</summary>
    /// <param name="userName">Der Benutzername.</param>
    /// <param name="password">Das Passwort (für beide Passwortfelder).</param>
    /// <param name="cancellationToken">Abbruchtoken.</param>
    /// <returns>Die Server-Antwort des POST.</returns>
    public async Task<HttpResponseMessage> RegisterFirstUserAsync(
        string userName, string password, CancellationToken cancellationToken = default)
    {
        await RefreshAntiforgeryAsync("/register", cancellationToken).ConfigureAwait(false);
        return await PostFormAsync("/register",
        [
            new KeyValuePair<string, string>("username", userName),
            new KeyValuePair<string, string>("password", password),
            new KeyValuePair<string, string>("confirmPassword", password),
        ], cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Meldet einen Benutzer an: holt das Antiforgery-Token von
    /// <c>GET /login</c> und sendet das Formular.</summary>
    /// <param name="userName">Der Benutzername.</param>
    /// <param name="password">Das Passwort.</param>
    /// <param name="cancellationToken">Abbruchtoken.</param>
    /// <returns>Die Server-Antwort des POST.</returns>
    public async Task<HttpResponseMessage> LoginAsync(
        string userName, string password, CancellationToken cancellationToken = default)
    {
        await RefreshAntiforgeryAsync("/login", cancellationToken).ConfigureAwait(false);
        return await PostFormAsync("/login",
        [
            new KeyValuePair<string, string>("username", userName),
            new KeyValuePair<string, string>("password", password),
        ], cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Meldet den aktuellen Benutzer ab: holt das Antiforgery-Token von
    /// <c>GET /</c> und sendet <c>POST /logout</c>.</summary>
    /// <param name="cancellationToken">Abbruchtoken.</param>
    /// <returns>Die Server-Antwort des POST.</returns>
    public async Task<HttpResponseMessage> LogoutAsync(CancellationToken cancellationToken = default)
    {
        await RefreshAntiforgeryAsync("/", cancellationToken).ConfigureAwait(false);
        return await PostFormAsync("/logout", cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public void Dispose() => Http.Dispose();
}
