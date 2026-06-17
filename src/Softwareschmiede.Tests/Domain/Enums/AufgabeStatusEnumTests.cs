using FluentAssertions;
using Softwareschmiede.Domain.Enums;

namespace Softwareschmiede.Tests.Domain.Enums;

/// <summary>Tests für den AufgabeStatus-Enum.</summary>
public sealed class AufgabeStatusEnumTests
{
    /// <summary>Neue Enum-Werte sind vorhanden und typsicher.</summary>
    [Fact]
    public void TestNewStatusEnum()
    {
        var values = Enum.GetValues<AufgabeStatus>();

        values.Should().Contain(AufgabeStatus.Neu);
        values.Should().Contain(AufgabeStatus.Gestartet);
        values.Should().Contain(AufgabeStatus.Wartend);
        values.Should().Contain(AufgabeStatus.Beendet);
        values.Should().Contain(AufgabeStatus.Archiviert);
    }

    /// <summary>Alte Enum-Werte (Offen, InBearbeitung, KiAktiv, Abgeschlossen) sind nicht mehr vorhanden.</summary>
    [Fact]
    public void OldStatusValues_ShouldNotExist()
    {
        var names = Enum.GetNames<AufgabeStatus>();

        names.Should().NotContain("Offen");
        names.Should().NotContain("InBearbeitung");
        names.Should().NotContain("KiAktiv");
        names.Should().NotContain("Abgeschlossen");
        names.Should().NotContain("Fehlgeschlagen");
        names.Should().NotContain("ArbeitsverzeichnisEingerichtet");
        names.Should().NotContain("InArbeit");
    }

    /// <summary>Alle neuen Enum-Werte sind gültige Integerwerte.</summary>
    [Theory]
    [InlineData(AufgabeStatus.Neu)]
    [InlineData(AufgabeStatus.Gestartet)]
    [InlineData(AufgabeStatus.Wartend)]
    [InlineData(AufgabeStatus.Beendet)]
    [InlineData(AufgabeStatus.Archiviert)]
    public void EachStatus_ShouldBeDefinedInEnum(AufgabeStatus status)
    {
        Enum.IsDefined(status).Should().BeTrue();
    }
}
