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
        var values = Enum.GetValues<WorkItemStatus>();

        values.Should().Contain(WorkItemStatus.New);
        values.Should().Contain(WorkItemStatus.Started);
        values.Should().Contain(WorkItemStatus.Waiting);
        values.Should().Contain(WorkItemStatus.Completed);
        values.Should().Contain(WorkItemStatus.Archived);
    }

    /// <summary>Alte Enum-Werte (Offen, InBearbeitung, KiAktiv, Abgeschlossen) sind nicht mehr vorhanden.</summary>
    [Fact]
    public void OldStatusValues_ShouldNotExist()
    {
        var names = Enum.GetNames<WorkItemStatus>();

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
    [InlineData(WorkItemStatus.New)]
    [InlineData(WorkItemStatus.Started)]
    [InlineData(WorkItemStatus.Waiting)]
    [InlineData(WorkItemStatus.Completed)]
    [InlineData(WorkItemStatus.Archived)]
    public void EachStatus_ShouldBeDefinedInEnum(WorkItemStatus status)
    {
        Enum.IsDefined(status).Should().BeTrue();
    }
}
