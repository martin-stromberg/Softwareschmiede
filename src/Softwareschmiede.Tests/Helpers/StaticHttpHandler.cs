using System.Net;
using System.Net.Http;

namespace Softwareschmiede.Tests.Helpers;

/// <summary>Test-Handler, der jede Anfrage mit derselben Statuscode/Body-Antwort beantwortet.</summary>
public sealed class StaticHttpHandler : HttpMessageHandler
{
    private readonly HttpStatusCode _statusCode;
    private readonly string _body;

    /// <inheritdoc cref="StaticHttpHandler"/>
    /// <param name="statusCode">Der zu liefernde HTTP-Statuscode.</param>
    /// <param name="body">Der Antworttext.</param>
    public StaticHttpHandler(HttpStatusCode statusCode, string body)
    {
        _statusCode = statusCode;
        _body = body;
    }

    /// <inheritdoc/>
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return Task.FromResult(new HttpResponseMessage(_statusCode)
        {
            Content = new StringContent(_body)
        });
    }
}
