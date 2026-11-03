using FluentAssertions;
using Softwareschmiede.Application.Services.Updates;

namespace Softwareschmiede.Tests.Application.Services.Updates;

/// <summary>Tests für <see cref="UpdatePfadKanalResolver"/>.</summary>
public sealed class UpdatePfadKanalResolverTests
{
    /// <summary>Der Pfad Vorabversionen (auch OrdinalIgnoreCase-/Whitespace-Varianten) ergibt den RC-Kanal, alle anderen Werte den Produktiv-Kanal.</summary>
    [Theory]
    [InlineData("Vorabversionen", UpdateKanal.ReleaseCandidate)]
    [InlineData("vorabversionen", UpdateKanal.ReleaseCandidate)]
    [InlineData("VORABVERSIONEN", UpdateKanal.ReleaseCandidate)]
    [InlineData(" Vorabversionen ", UpdateKanal.ReleaseCandidate)]
    [InlineData("Produktivversionen", UpdateKanal.Produktiv)]
    [InlineData(null, UpdateKanal.Produktiv)]
    [InlineData("", UpdateKanal.Produktiv)]
    [InlineData("   ", UpdateKanal.Produktiv)]
    [InlineData("unbekannter-pfad", UpdateKanal.Produktiv)]
    public void ErmittleKanal_LeitetKanalAusPfadAb(string? pfad, UpdateKanal erwartet)
        => UpdatePfadKanalResolver.ErmittleKanal(pfad).Should().Be(erwartet);

    /// <summary>Unbekannte, leere oder fehlende Pfadwerte werden auf Produktivversionen normalisiert; bekannte Werte auf ihre kanonische Form.</summary>
    [Theory]
    [InlineData("Vorabversionen", UpdatePfadKanalResolver.VorabversionenPfad)]
    [InlineData("vorabversionen", UpdatePfadKanalResolver.VorabversionenPfad)]
    [InlineData(" Vorabversionen ", UpdatePfadKanalResolver.VorabversionenPfad)]
    [InlineData("Produktivversionen", UpdatePfadKanalResolver.ProduktivversionenPfad)]
    [InlineData(null, UpdatePfadKanalResolver.ProduktivversionenPfad)]
    [InlineData("", UpdatePfadKanalResolver.ProduktivversionenPfad)]
    [InlineData("unbekannter-pfad", UpdatePfadKanalResolver.ProduktivversionenPfad)]
    public void NormalisierePfad_FaelltAufProduktivversionenZurueck(string? pfad, string erwartet)
        => UpdatePfadKanalResolver.NormalisierePfad(pfad).Should().Be(erwartet);

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
    public void IstGueltigerPfad_AkzeptiertNurBekanntePfade(string? pfad, bool erwartet)
        => UpdatePfadKanalResolver.IstGueltigerPfad(pfad).Should().Be(erwartet);

    /// <summary>Ein -rc-Infix im Tag ergibt den RC-Kanal; stabile Tags den Produktiv-Kanal; fehlende Tags konservativ den RC-Kanal.</summary>
    [Theory]
    [InlineData("v1.2.3-rc.1", UpdateKanal.ReleaseCandidate)]
    [InlineData("V1.2.3-RC.1", UpdateKanal.ReleaseCandidate)]
    [InlineData("v1.2.3", UpdateKanal.Produktiv)]
    [InlineData(null, UpdateKanal.ReleaseCandidate)]
    [InlineData("", UpdateKanal.ReleaseCandidate)]
    public void ErmittleKanalFuerTagName_KlassifiziertInstallationsTags(string? tagName, UpdateKanal erwartet)
        => UpdatePfadKanalResolver.ErmittleKanalFuerTagName(tagName).Should().Be(erwartet);
}
