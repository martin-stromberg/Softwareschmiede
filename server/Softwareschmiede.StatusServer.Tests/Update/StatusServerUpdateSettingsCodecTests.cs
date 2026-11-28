using System.Globalization;
using FluentAssertions;
using Softwareschmiede.StatusServer.Update;

namespace Softwareschmiede.StatusServer.Tests.Update;

/// <summary>Tests für <see cref="StatusServerUpdateSettingsCodec"/>:
/// Enum-Mapping-Roundtrips (Pflegepunkt für neue Enum-Mitglieder) und die
/// kulturinvariante Uhrzeit-Verarbeitung (Browser-<c>input[type=time]</c>).</summary>
public sealed class StatusServerUpdateSettingsCodecTests
{
    /// <summary>Jedes Kanal-Enum-Mitglied übersteht den Format→Parse-Roundtrip —
    /// ein neu hinzukommendes Mitglied ohne Codec-Mapping schlägt hier fehl.</summary>
    [Fact]
    public void Channel_RoundtripsAllMembers()
    {
        foreach (var channel in Enum.GetValues<StatusServerUpdateChannel>())
            StatusServerUpdateSettingsCodec.TryParseChannel(
                StatusServerUpdateSettingsCodec.FormatChannel(channel)).Should().Be(channel);
    }

    /// <summary>Jedes Modus-Enum-Mitglied übersteht den Format→Parse-Roundtrip.</summary>
    [Fact]
    public void Mode_RoundtripsAllMembers()
    {
        foreach (var mode in Enum.GetValues<StatusServerAutomationMode>())
            StatusServerUpdateSettingsCodec.TryParseMode(
                StatusServerUpdateSettingsCodec.FormatMode(mode)).Should().Be(mode);
    }

    /// <summary>Unbekannte bzw. fehlende Werte ergeben <see langword="null"/> —
    /// der Aufrufer fällt auf Default bzw. Validierungsfehler zurück.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("bogus")]
    [InlineData("Stable")]
    public void TryParse_UnknownValues_ReturnNull(string? value)
    {
        StatusServerUpdateSettingsCodec.TryParseChannel(value).Should().BeNull();
        StatusServerUpdateSettingsCodec.TryParseMode(value).Should().BeNull();
    }

    /// <summary>Formatieren und Parsen der Installationsuhrzeit arbeiten
    /// kulturinvariant: auch unter einer Kultur mit Zeitseparator <c>.</c>
    /// (fi-FI) rendert <c>HH:mm</c> mit Doppelpunkt — sonst wäre der Wert kein
    /// gültiges <c>input[type=time]</c>-Format und das Parsen des Browser-Werts
    /// würde je nach Server-Kultur scheitern.</summary>
    [Fact]
    public void InstallTime_IsCultureInvariant()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("fi-FI");

            var formatted = StatusServerUpdateSettingsCodec.FormatInstallTime(new TimeOnly(3, 30));
            formatted.Should().Be("03:30");
            StatusServerUpdateSettingsCodec.TryParseInstallTime(formatted, out var parsed)
                .Should().BeTrue();
            parsed.Should().Be(new TimeOnly(3, 30));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    /// <summary>Ungültige Uhrzeit-Werte werden als <see langword="false"/>
    /// signalisiert statt zu werfen.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("keine-uhrzeit")]
    [InlineData("25:00")]
    public void TryParseInstallTime_Invalid_ReturnsFalse(string? value)
        => StatusServerUpdateSettingsCodec.TryParseInstallTime(value, out _).Should().BeFalse();
}
