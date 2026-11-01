using System.Net.Http;

namespace Softwareschmiede.Tests.Helpers;

/// <summary>Testhilfe: einfacher delegierender <see cref="HttpMessageHandler"/>.</summary>
public sealed class DelegateHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
{
    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => send(request, cancellationToken);
}
