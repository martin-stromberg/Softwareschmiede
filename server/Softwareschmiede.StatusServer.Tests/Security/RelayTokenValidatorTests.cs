using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using Softwareschmiede.StatusServer.Options;
using Softwareschmiede.StatusServer.Security;

namespace Softwareschmiede.StatusServer.Tests.Security;

/// <summary>Tests für <see cref="RelayTokenValidator"/>.</summary>
public sealed class RelayTokenValidatorTests
{
    private static RelayTokenValidator CreateSut(
        IEnumerable<string>? tokens = null,
        string? devToken = null,
        string environment = "Production")
    {
        var options = Microsoft.Extensions.Options.Options.Create(new StatusServerOptions
        {
            Relay = new RelayOptions
            {
                Tokens = tokens?.ToList() ?? ["token-a", "token-b"],
                DevToken = devToken,
            },
        });
        var env = new Mock<IHostEnvironment>();
        env.SetupGet(e => e.EnvironmentName).Returns(environment);
        return new RelayTokenValidator(options, env.Object);
    }

    /// <summary>Konfigurierte Tokens werden akzeptiert.</summary>
    [Fact]
    public void IsValid_KnownToken_ReturnsTrue()
    {
        var sut = CreateSut();
        sut.IsValid("token-a").Should().BeTrue();
        sut.IsValid("token-b").Should().BeTrue();
    }

    /// <summary>Fremde, leere und fehlende Tokens werden abgelehnt.</summary>
    /// <param name="token">Der zu prüfende Tokenwert.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("token-c")]
    public void IsValid_UnknownOrEmptyToken_ReturnsFalse(string? token)
    {
        var sut = CreateSut();
        sut.IsValid(token).Should().BeFalse();
    }

    /// <summary>Ohne konfigurierte Tokens wird jeder Zugriff abgelehnt.</summary>
    [Fact]
    public void IsValid_EmptyTokenList_RejectsAll()
    {
        var sut = CreateSut(tokens: []);
        sut.IsValid("irgendwas").Should().BeFalse();
    }

    /// <summary>Das Dev-Token gilt nur in der Development-Umgebung.</summary>
    /// <param name="environment">Die Hosting-Umgebung.</param>
    /// <param name="expected">Das erwartete Ergebnis.</param>
    [Theory]
    [InlineData("Development", true)]
    [InlineData("Production", false)]
    [InlineData("Staging", false)]
    public void IsValid_DevToken_OnlyInDevelopment(string environment, bool expected)
    {
        var sut = CreateSut(tokens: [], devToken: "dev-secret", environment: environment);
        sut.IsValid("dev-secret").Should().Be(expected);
    }
}
