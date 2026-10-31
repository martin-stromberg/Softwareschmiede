using System.Net;
using System.Net.Http;
using FluentAssertions;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Infrastructure.Plugins;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.Infrastructure.Plugins;

/// <summary>Transport-Tests des prozesslosen HTTP-Clients (nur Fake-Tokens, kein Netzwerk).</summary>
public sealed class GitHubHttpApiClientTests
{
    /// <summary>github.com-Hosts werden auf api.github.com abgebildet, GHE-Hosts auf {host}/api/v3.</summary>
    [Theory]
    [InlineData("github.com", "repos/o/r/issues?state=open", "https://api.github.com/repos/o/r/issues?state=open")]
    [InlineData("ghe.example.com", "repos/o/r/issues", "https://ghe.example.com/api/v3/repos/o/r/issues")]
    [InlineData("github.com", "graphql", "https://api.github.com/graphql")]
    [InlineData("ghe.example.com", "graphql", "https://ghe.example.com/api/graphql")]
    public async Task SendAsync_MapsHostToCorrectBaseUrl(string host, string endpoint, string expected)
    {
        HttpRequestMessage? captured = null;
        var client = Client(request =>
        {
            captured = request;
            return Task.FromResult(Response(HttpStatusCode.OK, "{}"));
        });
        await client.SendAsync(new(host, "GET", endpoint));
        captured!.RequestUri!.ToString().Should().Be(expected);
    }

    /// <summary>Ein ungültiger Host wird als permanenter Providerfehler abgelehnt.</summary>
    [Fact]
    public async Task SendAsync_InvalidHost_ThrowsPermanent()
    {
        var client = Client(_ => Task.FromResult(Response(HttpStatusCode.OK, "{}")));
        var error = await Assert.ThrowsAsync<ScmProviderException>(
            () => client.SendAsync(new("not a host !!", "GET", "user")));
        error.FailureKind.Should().Be(ScmProviderFailureKind.Permanent);
    }

    /// <summary>Ein Host mit UserInfo-, Port-, Pfad-, Query- oder Fragment-Bestandteilen wird
    /// abgelehnt, bevor der Bearer-Token an einen fremden Origin gehen könnte.</summary>
    [Theory]
    [InlineData("user@evil.com")]
    [InlineData("evil.com:444")]
    [InlineData("evil.com/pfad")]
    [InlineData("evil.com?x=1")]
    [InlineData("evil.com#frag")]
    public async Task SendAsync_HostWithForeignComponents_ThrowsPermanent(string host)
    {
        var called = false;
        var client = Client(_ =>
        {
            called = true;
            return Task.FromResult(Response(HttpStatusCode.OK, "{}"));
        });
        var error = await Assert.ThrowsAsync<ScmProviderException>(
            () => client.SendAsync(new(host, "GET", "user")));
        error.FailureKind.Should().Be(ScmProviderFailureKind.Permanent);
        called.Should().BeFalse();
    }

    /// <summary>Pflicht-Header werden gesetzt; der Bearer-Token kommt nur bei vorhandenem Token.</summary>
    [Theory]
    [InlineData("fake-token", true)]
    [InlineData(null, false)]
    public async Task SendAsync_RequiredHeaders_BearerOnlyWithToken(string? token, bool expectAuth)
    {
        HttpRequestMessage? captured = null;
        var client = Client(request =>
        {
            captured = request;
            return Task.FromResult(Response(HttpStatusCode.OK, "{}"));
        }, () => token);
        await client.SendAsync(new("github.com", "GET", "user"));
        captured!.Headers.Accept.ToString().Should().Be("application/vnd.github+json");
        captured.Headers.GetValues("X-GitHub-Api-Version").Should().Equal("2022-11-28");
        captured.Headers.UserAgent.ToString().Should().Be("Softwareschmiede");
        if (expectAuth)
        {
            captured.Headers.Authorization!.Scheme.Should().Be("Bearer");
            captured.Headers.Authorization.Parameter.Should().Be(token);
        }
        else
        {
            captured.Headers.Authorization.Should().BeNull();
        }
    }

    /// <summary>Der Token-Selektor wird pro Request ausgewertet (Wechsel wirkt ohne Neustart).</summary>
    [Fact]
    public async Task SendAsync_TokenProviderIsEvaluatedPerRequest()
    {
        var token = "token-a";
        var client = Client(_ => Task.FromResult(Response(HttpStatusCode.OK, "{}")), () => token);
        await client.SendAsync(new("github.com", "GET", "user"));
        token = "token-b";
        HttpRequestMessage? captured = null;
        var second = Client(request =>
        {
            captured = request;
            return Task.FromResult(Response(HttpStatusCode.OK, "{}"));
        }, () => token);
        await second.SendAsync(new("github.com", "GET", "user"));
        captured!.Headers.Authorization!.Parameter.Should().Be("token-b");
    }

    /// <summary>Der JSON-Body wird als application/json-Content gesendet.</summary>
    [Fact]
    public async Task SendAsync_SendsJsonBody()
    {
        string? capturedBody = null;
        string? capturedContentType = null;
        var client = Client(async request =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync();
            capturedContentType = request.Content.Headers.ContentType!.MediaType;
            return Response(HttpStatusCode.OK, "{}");
        });
        await client.SendAsync(new("github.com", "POST", "repos/o/r/issues", "{\"title\":\"t\"}"));
        capturedBody.Should().Be("{\"title\":\"t\"}");
        capturedContentType.Should().Be("application/json");
    }

    /// <summary>Response- und Content-Header werden für Pagination und Backoff weitergereicht.</summary>
    [Fact]
    public async Task SendAsync_PassesResponseHeadersThrough()
    {
        var client = Client(_ =>
        {
            var response = Response(HttpStatusCode.OK, "[]");
            response.Headers.TryAddWithoutValidation("Link", "<https://api.github.com/x?page=2>; rel=\"next\"");
            response.Headers.TryAddWithoutValidation("X-RateLimit-Remaining", "42");
            response.Headers.TryAddWithoutValidation("Retry-After", "30");
            return Task.FromResult(response);
        });
        var result = await client.SendAsync(new("github.com", "GET", "notifications"));
        result.StatusCode.Should().Be(200);
        result.Body.Should().Be("[]");
        result.Headers!["Link"].Should().Contain("page=2");
        result.Headers["X-RateLimit-Remaining"].Should().Be("42");
        result.Headers["Retry-After"].Should().Be("30");
    }

    /// <summary>Nicht-2xx-Status werden mit Body zurückgegeben statt als Exception geworfen.</summary>
    [Fact]
    public async Task SendAsync_ErrorStatus_ReturnsStatusAndBody()
    {
        var client = Client(_ => Task.FromResult(Response(HttpStatusCode.NotFound, "{\"message\":\"Not Found\"}")));
        var result = await client.SendAsync(new("github.com", "GET", "repos/o/r"));
        result.StatusCode.Should().Be(404);
        result.Body.Should().Contain("Not Found");
    }

    /// <summary>Transportfehler werden Temporary mit der outcomeUncertain-Matrix des bisherigen gh-Transports.</summary>
    [Theory]
    [InlineData("GET", "repos/o/r", false)]
    [InlineData("GET", "graphql", false)]
    [InlineData("POST", "graphql", false)]
    [InlineData("POST", "repos/o/r/issues", true)]
    [InlineData("PUT", "repos/o/r/issues/1", true)]
    public async Task SendAsync_TransportError_ThrowsTemporary(string method, string endpoint, bool uncertain)
    {
        var client = Client(_ => throw new HttpRequestException("refused secret-token"));
        var error = await Assert.ThrowsAsync<ScmProviderException>(
            () => client.SendAsync(new("github.com", method, endpoint, method == "GET" ? null : "{}")));
        error.FailureKind.Should().Be(ScmProviderFailureKind.Temporary);
        error.OutcomeUncertain.Should().Be(uncertain);
    }

    /// <summary>Aufrufer-Cancellation wird durchgereicht statt als Transportfehler gemappt.</summary>
    [Fact]
    public async Task SendAsync_CallerCancellation_Propagates()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var client = Client(_ => throw new OperationCanceledException(cts.Token));
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => client.SendAsync(new("github.com", "GET", "user"), cts.Token));
    }

    /// <summary>Der Token taucht weder in URL noch in Exception-Messages oder Response-Artefakten auf.</summary>
    [Fact]
    public async Task SendAsync_TokenNeverAppearsInOutputs()
    {
        const string token = "ghp_FakeSecretToken123";
        HttpRequestMessage? captured = null;
        var client = Client(request =>
        {
            captured = request;
            return Task.FromResult(Response(HttpStatusCode.Unauthorized, "{\"message\":\"Bad credentials\"}"));
        }, () => token);
        var result = await client.SendAsync(new("github.com", "GET", "user"));
        captured!.RequestUri!.ToString().Should().NotContain(token);
        result.Body.Should().NotContain(token);
        result.Headers!.Values.Should().OnlyContain(v => !v.Contains(token));

        var failing = Client(_ => throw new HttpRequestException("network down"), () => token);
        var error = await Assert.ThrowsAsync<ScmProviderException>(
            () => failing.SendAsync(new("github.com", "GET", "user")));
        error.Message.Should().NotContain(token);
        error.ToString().Should().NotContain(token);
    }

    private static GitHubHttpApiClient Client(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond, Func<string?>? token = null)
        => new(token ?? (() => "fake-token"), new DelegateHandler((request, _) => respond(request)));

    private static HttpResponseMessage Response(HttpStatusCode status, string body)
        => new(status) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };
}
