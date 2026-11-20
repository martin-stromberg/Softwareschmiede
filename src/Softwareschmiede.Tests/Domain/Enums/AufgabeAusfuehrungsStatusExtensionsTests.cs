using FluentAssertions;
using Softwareschmiede.Domain.Enums;

namespace Softwareschmiede.Tests.Domain.Enums;

/// <summary>Tests für <see cref="WorkItemExecutionStatusExtensions"/>.</summary>
public sealed class AufgabeAusfuehrungsStatusExtensionsTests
{
    /// <summary>SollCliAnzeigen liefert true, wenn AusfuehrungsStatus Beendet ist und AufgabeStatus Gestartet ist.</summary>
    [Fact]
    public void SollCliAnzeigen_WhenAusfuehrungsStatusIsBeendet_AndAufgabeStatusIsGestartet_ReturnsTrue()
    {
        var result = WorkItemExecutionStatus.Completed.ShouldShowCli(WorkItemStatus.Started);

        result.Should().BeTrue();
    }

    /// <summary>SollCliAnzeigen liefert true, wenn AusfuehrungsStatus Beendet ist und AufgabeStatus Wartend ist.</summary>
    [Fact]
    public void SollCliAnzeigen_WhenAusfuehrungsStatusIsBeendet_AndAufgabeStatusIsWartend_ReturnsTrue()
    {
        var result = WorkItemExecutionStatus.Completed.ShouldShowCli(WorkItemStatus.Waiting);

        result.Should().BeTrue();
    }

    /// <summary>SollCliAnzeigen liefert false, wenn AufgabeStatus Beendet ist, obwohl AusfuehrungsStatus Beendet ist.</summary>
    [Fact]
    public void SollCliAnzeigen_WhenAusfuehrungsStatusIsBeendet_AndAufgabeStatusIsBeendet_ReturnsFalse()
    {
        var result = WorkItemExecutionStatus.Completed.ShouldShowCli(WorkItemStatus.Completed);

        result.Should().BeFalse();
    }

    /// <summary>SollCliAnzeigen liefert false, wenn AufgabeStatus Archiviert ist, obwohl AusfuehrungsStatus Beendet ist.</summary>
    [Fact]
    public void SollCliAnzeigen_WhenAusfuehrungsStatusIsBeendet_AndAufgabeStatusIsArchiviert_ReturnsFalse()
    {
        var result = WorkItemExecutionStatus.Completed.ShouldShowCli(WorkItemStatus.Archived);

        result.Should().BeFalse();
    }

    /// <summary>SollCliAnzeigen liefert weiterhin true, wenn AusfuehrungsStatus Aktiv ist und AufgabeStatus Gestartet ist.</summary>
    [Fact]
    public void SollCliAnzeigen_WhenAusfuehrungsStatusIsAktiv_AndAufgabeStatusIsGestartet_ReturnsTrue()
    {
        var result = WorkItemExecutionStatus.Active.ShouldShowCli(WorkItemStatus.Started);

        result.Should().BeTrue();
    }

    /// <summary>SollCliAnzeigen liefert false, wenn AusfuehrungsStatus NichtGestartet ist, auch wenn AufgabeStatus Gestartet ist.</summary>
    [Fact]
    public void SollCliAnzeigen_WhenAusfuehrungsStatusIsNichtGestartet_AndAufgabeStatusIsGestartet_ReturnsFalse()
    {
        var result = WorkItemExecutionStatus.NotStarted.ShouldShowCli(WorkItemStatus.Started);

        result.Should().BeFalse();
    }
}
