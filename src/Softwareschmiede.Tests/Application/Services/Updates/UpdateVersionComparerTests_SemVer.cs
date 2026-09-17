using FluentAssertions;
using Softwareschmiede.Application.Services.Updates;

namespace Softwareschmiede.Tests.Application.Services.Updates;

/// <summary>SemVer-Präzedenz- und Normalisierungstests für <see cref="UpdateVersionComparer"/> und <see cref="SemanticUpdateVersion"/>.</summary>
public sealed class UpdateVersionComparerTests_SemVer
{
    /// <summary>Normalize entfernt Leerzeichen und führendes v, erhält aber Prerelease-Suffix und Build-Metadaten.</summary>
    [Theory]
    [InlineData("v1.3.0-rc.1+build.7", "1.3.0-rc.1+build.7")]
    [InlineData("1.3.0-rc.1", "1.3.0-rc.1")]
    [InlineData("  v1.2.3  ", "1.2.3")]
    [InlineData("V2.0.0-beta.2+meta.1", "2.0.0-beta.2+meta.1")]
    public void Normalize_PreservesPrereleaseAndMetadata(string value, string expected)
    {
        UpdateVersionComparer.Normalize(value).Should().Be(expected);
    }

    /// <summary>Normalize wirft FormatException bei ungültigen Versionsangaben.</summary>
    [Theory]
    [InlineData("v1.2")]
    [InlineData("1.2.3-01")]
    [InlineData("ungueltig")]
    public void Normalize_ThrowsFormatException_WhenVersionIsInvalid(string value)
    {
        var act = () => UpdateVersionComparer.Normalize(value);

        act.Should().Throw<FormatException>();
    }

    /// <summary>SemVer-Präzedenz: alpha &lt; beta &lt; rc.1 &lt; rc.2 &lt; rc.10 &lt; stable.</summary>
    [Fact]
    public void Compare_PrereleasePrecedence()
    {
        var ordered = new[]
        {
            "1.3.0-alpha",
            "1.3.0-beta",
            "1.3.0-rc.1",
            "1.3.0-rc.2",
            "1.3.0-rc.10",
            "1.3.0"
        };

        for (var i = 0; i < ordered.Length; i++)
        {
            for (var j = i + 1; j < ordered.Length; j++)
            {
                UpdateVersionComparer.IsNewer(ordered[i], ordered[j]).Should().BeTrue(
                    $"{ordered[j]} muss neuer als {ordered[i]} sein");
                UpdateVersionComparer.IsNewer(ordered[j], ordered[i]).Should().BeFalse(
                    $"{ordered[i]} darf nicht neuer als {ordered[j]} sein");
            }
        }
    }

    /// <summary>Numerische Prerelease-Identifier sind kleiner als nichtnumerische; kürzere identische Identifierfolgen sind kleiner als deren Verlängerung.</summary>
    [Theory]
    [InlineData("1.3.0-1", "1.3.0-alpha", true)]
    [InlineData("1.3.0-alpha", "1.3.0-alpha.1", true)]
    [InlineData("1.3.0-alpha.1", "1.3.0-alpha", false)]
    [InlineData("1.3.0-alpha", "1.3.0-BETA", false)]
    [InlineData("1.3.0-rc.2", "1.3.0-rc.10", true)]
    public void Compare_PrereleaseIdentifierRules(string installed, string candidate, bool expected)
    {
        UpdateVersionComparer.IsNewer(installed, candidate).Should().Be(expected);
    }

    /// <summary>Build-Metadaten beeinflussen die Rangfolge nicht.</summary>
    [Theory]
    [InlineData("1.2.4+build.5", "1.2.4+build.9")]
    [InlineData("1.2.4-rc.1+build.5", "1.2.4-rc.1")]
    public void Compare_MetadataDoesNotAffectPrecedence(string left, string right)
    {
        UpdateVersionComparer.IsNewer(left, right).Should().BeFalse();
        UpdateVersionComparer.IsNewer(right, left).Should().BeFalse();
    }

    /// <summary>Unvollständige Kernversionen, leere Identifier und führende Nullen in numerischen Prerelease-Identifiern werden abgelehnt.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1.2")]
    [InlineData("1.2.3.4")]
    [InlineData("01.2.3")]
    [InlineData("1.2.3-")]
    [InlineData("1.2.3-rc..1")]
    [InlineData("1.2.3-01")]
    [InlineData("1.2.3-rc.01")]
    [InlineData("1.2.3-rc_1")]
    [InlineData("1.2.3+")]
    [InlineData("1.2.3+meta..1")]
    [InlineData("version-1.2.3")]
    public void TryParse_RejectsInvalidIdentifiers(string? value)
    {
        UpdateVersionComparer.TryParse(value, out _).Should().BeFalse();
        SemanticUpdateVersion.TryParse(value, out _).Should().BeFalse();
    }

    /// <summary>Geparste Prerelease-Versionen führen ihre Identifier und Metadaten mit.</summary>
    [Fact]
    public void TryParse_ExposesPrereleaseIdentifiersAndMetadata()
    {
        UpdateVersionComparer.TryParse("v1.3.0-rc.1+build.7", out var version).Should().BeTrue();

        version.Should().NotBeNull();
        version!.IsPrerelease.Should().BeTrue();
        version.PrereleaseIdentifiers.Should().Equal("rc", "1");
        version.BuildMetadata.Should().Be("build.7");
        version.Major.Should().Be(1);
        version.Minor.Should().Be(3);
        version.Patch.Should().Be(0);
    }
}
