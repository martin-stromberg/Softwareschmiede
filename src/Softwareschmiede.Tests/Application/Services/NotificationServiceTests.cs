using FluentAssertions;
using Softwareschmiede.Domain.Enums;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>Tests für Benachrichtigungs-Enums und -Service.</summary>
public sealed class NotificationServiceTests
{
    /// <summary>TestBenachrichtigungNewEnum: Neue Enums (Deaktiviert, Banner, Ton) sind vorhanden.</summary>
    [Fact]
    public void TestNotificationNewEnum()
    {
        var modes = Enum.GetValues<NotificationMode>();
        modes.Should().Contain(NotificationMode.Disabled);
        modes.Should().Contain(NotificationMode.Banner);
        modes.Should().Contain(NotificationMode.Sound);

        var kanaele = Enum.GetValues<NotificationChannel>();
        kanaele.Should().Contain(NotificationChannel.Banner);
        kanaele.Should().Contain(NotificationChannel.Sound);
    }

    /// <summary>Alte Enum-Werte (NurAufgabenseite, Global, Audio, System) sind nicht vorhanden.</summary>
    [Fact]
    public void OldNotificationEnumValues_ShouldNotExist()
    {
        var modeName = Enum.GetNames<NotificationMode>();
        modeName.Should().NotContain("NurAufgabenseite");
        modeName.Should().NotContain("Global");

        var channelName = Enum.GetNames<NotificationChannel>();
        channelName.Should().NotContain("Audio");
        channelName.Should().NotContain("System");
    }

    /// <summary>BenachrichtigungsModus-Werte haben korrekte Integer-Werte.</summary>
    [Fact]
    public void NotificationMode_ShouldHaveCorrectIntegerValues()
    {
        ((int)NotificationMode.Disabled).Should().Be(0);
        ((int)NotificationMode.Banner).Should().Be(1);
        ((int)NotificationMode.Sound).Should().Be(2);
    }

    /// <summary>BenachrichtigungsKanal-Werte haben korrekte Integer-Werte.</summary>
    [Fact]
    public void NotificationChannel_ShouldHaveCorrectIntegerValues()
    {
        ((int)NotificationChannel.Banner).Should().Be(0);
        ((int)NotificationChannel.Sound).Should().Be(1);
    }

    /// <summary>Alle BenachrichtigungsModus-Werte sind gültige Enums.</summary>
    [Theory]
    [InlineData(NotificationMode.Disabled)]
    [InlineData(NotificationMode.Banner)]
    [InlineData(NotificationMode.Sound)]
    public void EachNotificationMode_ShouldBeDefinedInEnum(NotificationMode mode)
    {
        Enum.IsDefined(mode).Should().BeTrue();
    }

    /// <summary>Alle BenachrichtigungsKanal-Werte sind gültige Enums.</summary>
    [Theory]
    [InlineData(NotificationChannel.Banner)]
    [InlineData(NotificationChannel.Sound)]
    public void EachNotificationChannel_ShouldBeDefinedInEnum(NotificationChannel channel)
    {
        Enum.IsDefined(channel).Should().BeTrue();
    }
}
