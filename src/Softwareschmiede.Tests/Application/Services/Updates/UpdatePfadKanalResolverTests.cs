using FluentAssertions;
using Softwareschmiede.Application.Services.Updates;

namespace Softwareschmiede.Tests.Application.Services.Updates;

/// <summary>Tests für <see cref="UpdatePathChannelResolver"/>.</summary>
public sealed class UpdatePfadKanalResolverTests
{
    /// <summary>Der Pfad Vorabversionen (auch OrdinalIgnoreCase-/Whitespace-Varianten) ergibt den RC-Kanal, alle anderen Werte den Produktiv-Kanal.</summary>
    [Theory]
    [InlineData("Vorabversionen", UpdateChannel.ReleaseCandidate)]
    [InlineData("vorabversionen", UpdateChannel.ReleaseCandidate)]
    [InlineData("VORABVERSIONEN", UpdateChannel.ReleaseCandidate)]
    [InlineData(" Vorabversionen ", UpdateChannel.ReleaseCandidate)]
    [InlineData("Produktivversionen", UpdateChannel.Stable)]
    [InlineData(null, UpdateChannel.Stable)]
    [InlineData("", UpdateChannel.Stable)]
    [InlineData("   ", UpdateChannel.Stable)]
    [InlineData("unbekannter-pfad", UpdateChannel.Stable)]
    public void ErmittleKanal_LeitetKanalAusPfadAb(string? path, UpdateChannel expected)
        => UpdatePathChannelResolver.ErmittleKanal(path).Should().Be(expected);

    /// <summary>Unbekannte, leere oder fehlende Pfadwerte werden auf Produktivversionen normalisiert; bekannte Werte auf ihre kanonische Form.</summary>
    [Theory]
    [InlineData("Vorabversionen", UpdatePathChannelResolver.PrereleaseVersionsPath)]
    [InlineData("vorabversionen", UpdatePathChannelResolver.PrereleaseVersionsPath)]
    [InlineData(" Vorabversionen ", UpdatePathChannelResolver.PrereleaseVersionsPath)]
    [InlineData("Produktivversionen", UpdatePathChannelResolver.StableVersionsPath)]
    [InlineData(null, UpdatePathChannelResolver.StableVersionsPath)]
    [InlineData("", UpdatePathChannelResolver.StableVersionsPath)]
    [InlineData("unbekannter-pfad", UpdatePathChannelResolver.StableVersionsPath)]
    public void NormalisierePfad_FaelltAufProduktivversionenZurueck(string? path, string expected)
        => UpdatePathChannelResolver.NormalizePath(path).Should().Be(expected);

    /// <summary>Nur die zwei bekannten Pfadwerte gelten als gültig.</summary>
    [Theory]
    [InlineData("Produktivversionen", true)]
    [InlineData("produktivversionen", true)]
    [InlineData("Vorabversionen", true)]
    [InlineData(" vorabversionen ", true)]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("unbekannter-pfad", false)]
    public void IstGueltigerPfad_AkzeptiertNurBekanntePfade(string? path, bool expected)
        => UpdatePathChannelResolver.IstGueltigerPfad(path).Should().Be(expected);

    /// <summary>Ein -rc-Infix im Tag ergibt den RC-Kanal; stabile Tags den Produktiv-Kanal; fehlende Tags konservativ den RC-Kanal.</summary>
    [Theory]
    [InlineData("v1.2.3-rc.1", UpdateChannel.ReleaseCandidate)]
    [InlineData("V1.2.3-RC.1", UpdateChannel.ReleaseCandidate)]
    [InlineData("v1.2.3", UpdateChannel.Stable)]
    [InlineData(null, UpdateChannel.ReleaseCandidate)]
    [InlineData("", UpdateChannel.ReleaseCandidate)]
    public void ErmittleKanalFuerTagName_KlassifiziertInstallationsTags(string? tagName, UpdateChannel expected)
        => UpdatePathChannelResolver.ErmittleKanalFuerTagName(tagName).Should().Be(expected);
}
