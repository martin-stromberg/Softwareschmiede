using System.Net;
using System.Net.Http;

namespace Softwareschmiede.Tests.Helpers;

/// <summary>
/// Test-Handler, der HTTP-Anfragen anhand der exakten Anfrage-URL auf konfigurierte Antworten
/// routet. Nicht konfigurierte URLs liefern 404 mit leerer Liste; alle abgerufenen URLs werden
/// in <see cref="RequestedUrls"/> protokolliert.
/// </summary>
public sealed class RoutingHttpHandler : HttpMessageHandler
{
    private readonly Dictionary<string, Func<CancellationToken, Task<HttpResponseMessage>>> _routes = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _requestedUrls = [];

    /// <inheritdoc cref="RoutingHttpHandler"/>
    public RoutingHttpHandler()
    {
    }

    /// <inheritdoc cref="RoutingHttpHandler"/>
    /// <param name="url">Die URL der Seite.</param>
    /// <param name="body">Der JSON-Antworttext der Seite.</param>
    public RoutingHttpHandler(string url, string body)
    {
        AddPage(url, body);
    }

    /// <summary>Die in Aufrufreihenfolge angefragten URLs.</summary>
    public IReadOnlyList<string> RequestedUrls => _requestedUrls;

    /// <summary>Registriert eine Seitenantwort, optional mit Link-Header auf eine Folgeseite.</summary>
    /// <param name="url">Die URL der Seite.</param>
    /// <param name="body">Der JSON-Antworttext.</param>
    /// <param name="nextUrl">Optionale Folgeseiten-URL für den Link-Header.</param>
    /// <param name="statusCode">Der zu liefernde HTTP-Statuscode.</param>
    public void AddPage(string url, string body, string? nextUrl = null, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        _routes[url] = _ =>
        {
            var response = new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(body)
            };
            if (nextUrl is not null)
                response.Headers.TryAddWithoutValidation("Link", $"<{nextUrl}>; rel=\"next\"");

            return Task.FromResult(response);
        };
    }

    /// <summary>Registriert einen frei definierbaren Handler für eine URL (z. B. für Timeout-Simulation).</summary>
    /// <param name="url">Die URL.</param>
    /// <param name="handler">Der Handler-Delegate.</param>
    public void AddHandler(string url, Func<CancellationToken, Task<HttpResponseMessage>> handler)
        => _routes[url] = handler;

    /// <inheritdoc/>
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        _requestedUrls.Add(request.RequestUri!.AbsoluteUri);
        return _routes.TryGetValue(request.RequestUri!.AbsoluteUri, out var handler)
            ? handler(cancellationToken)
            : Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("[]")
            });
    }
}
