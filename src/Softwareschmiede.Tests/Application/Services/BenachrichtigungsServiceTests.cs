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
        var modi = Enum.GetValues<NotificationMode>();
        modi.Should().Contain(NotificationMode.Disabled);
        modi.Should().Contain(NotificationMode.Banner);
        modi.Should().Contain(NotificationMode.Sound);

        var kanaele = Enum.GetValues<NotificationChannel>();
        kanaele.Should().Contain(NotificationChannel.Banner);
        kanaele.Should().Contain(NotificationChannel.Sound);
    }

    /// <summary>Alte Enum-Werte (NurAufgabenseite, Global, Audio, System) sind nicht vorhanden.</summary>
    [Fact]
    public void OldBenachrichtigungsEnumValues_ShouldNotExist()
    {
        var modusNames = Enum.GetNames<NotificationMode>();
        modusNames.Should().NotContain("NurAufgabenseite");
        modusNames.Should().NotContain("Global");

        var kanalNames = Enum.GetNames<NotificationChannel>();
        kanalNames.Should().NotContain("Audio");
        kanalNames.Should().NotContain("System");
    }

    /// <summary>BenachrichtigungsModus-Werte haben korrekte Integer-Werte.</summary>
    [Fact]
    public void BenachrichtigungsModus_ShouldHaveCorrectIntegerValues()
    {
        ((int)NotificationMode.Disabled).Should().Be(0);
        ((int)NotificationMode.Banner).Should().Be(1);
        ((int)NotificationMode.Sound).Should().Be(2);
    }

    /// <summary>BenachrichtigungsKanal-Werte haben korrekte Integer-Werte.</summary>
    [Fact]
    public void BenachrichtigungsKanal_ShouldHaveCorrectIntegerValues()
    {
        ((int)NotificationChannel.Banner).Should().Be(0);
        ((int)NotificationChannel.Sound).Should().Be(1);
    }

    /// <summary>Alle BenachrichtigungsModus-Werte sind gültige Enums.</summary>
    [Theory]
    [InlineData(NotificationMode.Disabled)]
    [InlineData(NotificationMode.Banner)]
    [InlineData(NotificationMode.Sound)]
    public void EachBenachrichtigungsModus_ShouldBeDefinedInEnum(NotificationMode mode)
    {
        Enum.IsDefined(mode).Should().BeTrue();
    }

    /// <summary>Alle BenachrichtigungsKanal-Werte sind gültige Enums.</summary>
    [Theory]
    [InlineData(NotificationChannel.Banner)]
    [InlineData(NotificationChannel.Sound)]
    public void EachBenachrichtigungsKanal_ShouldBeDefinedInEnum(NotificationChannel kanal)
    {
        Enum.IsDefined(kanal).Should().BeTrue();
    }
}
