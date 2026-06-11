using FluentAssertions;
using Softwareschmiede.Domain.Enums;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>Tests für Benachrichtigungs-Enums und -Service.</summary>
public sealed class BenachrichtigungsServiceTests
{
    /// <summary>TestBenachrichtigungNewEnum: Neue Enums (Deaktiviert, Banner, Ton) sind vorhanden.</summary>
    [Fact]
    public void TestBenachrichtigungNewEnum()
    {
        var modi = Enum.GetValues<BenachrichtigungsModus>();
        modi.Should().Contain(BenachrichtigungsModus.Deaktiviert);
        modi.Should().Contain(BenachrichtigungsModus.Banner);
        modi.Should().Contain(BenachrichtigungsModus.Ton);

        var kanaele = Enum.GetValues<BenachrichtigungsKanal>();
        kanaele.Should().Contain(BenachrichtigungsKanal.Banner);
        kanaele.Should().Contain(BenachrichtigungsKanal.Ton);
    }

    /// <summary>Alte Enum-Werte (NurAufgabenseite, Global, Audio, System) sind nicht vorhanden.</summary>
    [Fact]
    public void OldBenachrichtigungsEnumValues_ShouldNotExist()
    {
        var modusNames = Enum.GetNames<BenachrichtigungsModus>();
        modusNames.Should().NotContain("NurAufgabenseite");
        modusNames.Should().NotContain("Global");

        var kanalNames = Enum.GetNames<BenachrichtigungsKanal>();
        kanalNames.Should().NotContain("Audio");
        kanalNames.Should().NotContain("System");
    }

    /// <summary>BenachrichtigungsModus-Werte haben korrekte Integer-Werte.</summary>
    [Fact]
    public void BenachrichtigungsModus_ShouldHaveCorrectIntegerValues()
    {
        ((int)BenachrichtigungsModus.Deaktiviert).Should().Be(0);
        ((int)BenachrichtigungsModus.Banner).Should().Be(1);
        ((int)BenachrichtigungsModus.Ton).Should().Be(2);
    }

    /// <summary>BenachrichtigungsKanal-Werte haben korrekte Integer-Werte.</summary>
    [Fact]
    public void BenachrichtigungsKanal_ShouldHaveCorrectIntegerValues()
    {
        ((int)BenachrichtigungsKanal.Banner).Should().Be(0);
        ((int)BenachrichtigungsKanal.Ton).Should().Be(1);
    }

    /// <summary>Alle BenachrichtigungsModus-Werte sind gültige Enums.</summary>
    [Theory]
    [InlineData(BenachrichtigungsModus.Deaktiviert)]
    [InlineData(BenachrichtigungsModus.Banner)]
    [InlineData(BenachrichtigungsModus.Ton)]
    public void EachBenachrichtigungsModus_ShouldBeDefinedInEnum(BenachrichtigungsModus modus)
    {
        Enum.IsDefined(modus).Should().BeTrue();
    }

    /// <summary>Alle BenachrichtigungsKanal-Werte sind gültige Enums.</summary>
    [Theory]
    [InlineData(BenachrichtigungsKanal.Banner)]
    [InlineData(BenachrichtigungsKanal.Ton)]
    public void EachBenachrichtigungsKanal_ShouldBeDefinedInEnum(BenachrichtigungsKanal kanal)
    {
        Enum.IsDefined(kanal).Should().BeTrue();
    }
}
