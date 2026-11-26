using FluentAssertions;
using Softwareschmiede.Application.Services;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>Tests der URL-Normalisierung der Remote-Übersicht (<see cref="RemoteStatusUrlParser"/>).</summary>
public sealed class RemoteStatusUrlParserTests
{
    /// <summary>Leere oder ungültige Eingaben liefern einen Fehler statt einer Hub-URL.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("keine-url")]
    [InlineData("ftp://status.example.org")]
    public void TryCreateHubUrl_RejectsInvalidInputs(string? input)
    {
        RemoteStatusUrlParser.TryCreateHubUrl(input, out var hubUrl, out var error)
            .Should().BeFalse();
        hubUrl.Should().BeNull();
        error.Should().NotBeNullOrEmpty();
    }

    /// <summary>Ein http(s)-Host ohne Pfad erhält automatisch den Hub-Pfad /relay.</summary>
    [Theory]
    [InlineData("http://localhost:5055", "http://localhost:5055/relay")]
    [InlineData("https://status.example.org", "https://status.example.org/relay")]
    [InlineData("https://status.example.org/", "https://status.example.org/relay")]
    [InlineData("https://status.example.org/sub", "https://status.example.org/sub/relay")]
    public void TryCreateHubUrl_AppendsRelayPath(string input, string expected)
    {
        RemoteStatusUrlParser.TryCreateHubUrl(input, out var hubUrl, out var error)
            .Should().BeTrue();
        error.Should().BeNull();
        hubUrl.Should().Be(new Uri(expected));
    }

    /// <summary>ws(s)-URLs werden auf http(s) normalisiert.</summary>
    [Theory]
    [InlineData("ws://localhost:5055", "http://localhost:5055/relay")]
    [InlineData("wss://status.example.org", "https://status.example.org/relay")]
    public void TryCreateHubUrl_NormalizesWebSocketScheme(string input, string expected)
    {
        RemoteStatusUrlParser.TryCreateHubUrl(input, out var hubUrl, out var error)
            .Should().BeTrue();
        error.Should().BeNull();
        hubUrl.Should().Be(new Uri(expected));
    }

    /// <summary>Eine bereits auf /relay endende URL wird nicht doppelt erweitert.</summary>
    [Theory]
    [InlineData("http://localhost:5055/relay", "http://localhost:5055/relay")]
    [InlineData("http://localhost:5055/relay/", "http://localhost:5055/relay")]
    public void TryCreateHubUrl_KeepsExistingRelayPath(string input, string expected)
    {
        RemoteStatusUrlParser.TryCreateHubUrl(input, out var hubUrl, out var error)
            .Should().BeTrue();
        hubUrl.Should().Be(new Uri(expected));
    }
}
