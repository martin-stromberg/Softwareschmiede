using FluentAssertions;
using Softwareschmiede.Application.Services.Updates;

namespace Softwareschmiede.Tests.Application.Services.Updates;

/// <summary>Tests für <see cref="UpdateVersionComparer"/>.</summary>
public sealed class UpdateVersionComparerTests
{
    /// <summary>IsNewer akzeptiert Versionen mit und ohne führendes v.</summary>
    [Theory]
    [InlineData("1.2.3", "1.2.4", true)]
    [InlineData("v1.2.3", "v1.3.0", true)]
    [InlineData("1.2.3", "1.2.3", false)]
    [InlineData("1.2.3", "1.2.2", false)]
    [InlineData("ungueltig", "1.2.4", false)]
    [InlineData("1.2.3", "1.2.4+build.5", true)]
    [InlineData("1.2.3", "1.2.4-beta.1", false)]
    public void IsNewer_ShouldCompareSemVerValues(string installed, string candidate, bool expected)
    {
        UpdateVersionComparer.IsNewer(installed, candidate).Should().Be(expected);
    }

    /// <summary>Nur stabile SemVer-Tags werden normalisiert; ungültige und Pre-Release-Tags werden ignoriert.</summary>
    [Theory]
    [InlineData("v1.2.3", true)]
    [InlineData("1.2.3+build.7", true)]
    [InlineData("v1.2", false)]
    [InlineData("version-1.2.3", false)]
    [InlineData("v1.2.3-beta.1", false)]
    public void TryParse_ShouldAcceptOnlyStableSemVerTags(string value, bool expected)
    {
        UpdateVersionComparer.TryParse(value, out _).Should().Be(expected);
    }
}
