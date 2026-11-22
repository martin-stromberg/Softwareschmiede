using FluentAssertions;
using Softwareschmiede.Domain.Entities;

namespace Softwareschmiede.Tests.Domain.Entities;

/// <summary>Tests für die verbindliche Freigabe-Einteilung der SCM-Ereignis-Stati:
/// Exakt die vier finalen oder terminal klärbaren Werte geben Abschlussprüfung und
/// Ereignisreihenfolge frei; jeder andere — auch ein unbekannter — Wert blockiert
/// konservativ weiter.</summary>
public sealed class ScmIncomingEventStatusTests
{
    /// <summary>Die Freigabeliste enthält exakt die vier ausdrücklich freigegebenen Stati.</summary>
    [Fact]
    public void NonBlocking_ShouldContainExactlyTheFourReleasedStatuses()
    {
        ScmIncomingEventStatus.NonBlocking.Should().BeEquivalentTo(
            ScmIncomingEventStatus.Verarbeitet,
            ScmIncomingEventStatus.Abgebrochen,
            ScmIncomingEventStatus.VerarbeitungUngeklärt,
            ScmIncomingEventStatus.ZustellungUngewiss);
    }

    /// <summary>Alle zehn bekannten Stati sind über die Konstanten korrekt benannt.</summary>
    [Fact]
    public void Constants_ShouldNameAllTenKnownStatuses()
    {
        ScmIncomingEventStatus.Aufgenommen.Should().Be("Aufgenommen");
        ScmIncomingEventStatus.Verzögert.Should().Be("Verzögert");
        ScmIncomingEventStatus.Wartend.Should().Be("Wartend");
        ScmIncomingEventStatus.ZustellungVorbereitet.Should().Be("ZustellungVorbereitet");
        ScmIncomingEventStatus.Zugestellt.Should().Be("Zugestellt");
        ScmIncomingEventStatus.ZustellungUngewiss.Should().Be("ZustellungUngewiss");
        ScmIncomingEventStatus.InBearbeitung.Should().Be("InBearbeitung");
        ScmIncomingEventStatus.VerarbeitungUngeklärt.Should().Be("VerarbeitungUngeklärt");
        ScmIncomingEventStatus.Verarbeitet.Should().Be("Verarbeitet");
        ScmIncomingEventStatus.Abgebrochen.Should().Be("Abgebrochen");
    }

    /// <summary>Jeder bekannte Status ist je nach Freigabeliste blockierend oder freigegeben.</summary>
    [Theory]
    [InlineData(ScmIncomingEventStatus.Aufgenommen, true)]
    [InlineData(ScmIncomingEventStatus.Verzögert, true)]
    [InlineData(ScmIncomingEventStatus.Wartend, true)]
    [InlineData(ScmIncomingEventStatus.ZustellungVorbereitet, true)]
    [InlineData(ScmIncomingEventStatus.Zugestellt, true)]
    [InlineData(ScmIncomingEventStatus.ZustellungUngewiss, false)]
    [InlineData(ScmIncomingEventStatus.InBearbeitung, true)]
    [InlineData(ScmIncomingEventStatus.VerarbeitungUngeklärt, false)]
    [InlineData(ScmIncomingEventStatus.Verarbeitet, false)]
    [InlineData(ScmIncomingEventStatus.Abgebrochen, false)]
    public void NonBlocking_ShouldClassifyAllKnownStatuses(string status, bool blocking)
    {
        ScmIncomingEventStatus.NonBlocking.Contains(status).Should().Be(!blocking);
    }

    /// <summary>Ein unbekannter oder zukünftiger Status blockiert konservativ weiter.</summary>
    [Theory]
    [InlineData("ZukuenftigerStatus")]
    [InlineData("")]
    public void NonBlocking_ShouldExcludeUnknownStatuses(string status)
    {
        ScmIncomingEventStatus.NonBlocking.Contains(status).Should().BeFalse();
    }
}
