using FluentAssertions;
using Softwareschmiede.Domain.Enums;

namespace Softwareschmiede.Tests.Domain.Enums;

/// <summary>Tests für <see cref="WorkItemExecutionStatusExtensions"/>.</summary>
public sealed class WorkItemExecutionStatusExtensionsTests
{
    /// <summary>SollCliAnzeigen liefert true, wenn AusfuehrungsStatus Beendet ist und AufgabeStatus Gestartet ist.</summary>
    [Fact]
    public void ShowShouldCli_WhenExecutionStatusIsCompleted_AndWorkItemStatusIsStarted_ReturnsTrue()
    {
        var result = WorkItemExecutionStatus.Completed.ShouldShowCli(WorkItemStatus.Started);

        result.Should().BeTrue();
    }

    /// <summary>SollCliAnzeigen liefert true, wenn AusfuehrungsStatus Beendet ist und AufgabeStatus Wartend ist.</summary>
    [Fact]
    public void ShowShouldCli_WhenExecutionStatusIsCompleted_AndWorkItemStatusIsWaiting_ReturnsTrue()
    {
        var result = WorkItemExecutionStatus.Completed.ShouldShowCli(WorkItemStatus.Waiting);

        result.Should().BeTrue();
    }

    /// <summary>SollCliAnzeigen liefert false, wenn AufgabeStatus Beendet ist, obwohl AusfuehrungsStatus Beendet ist.</summary>
    [Fact]
    public void ShowShouldCli_WhenExecutionStatusIsCompleted_AndWorkItemStatusIsCompleted_ReturnsFalse()
    {
        var result = WorkItemExecutionStatus.Completed.ShouldShowCli(WorkItemStatus.Completed);

        result.Should().BeFalse();
    }

    /// <summary>SollCliAnzeigen liefert false, wenn AufgabeStatus Archiviert ist, obwohl AusfuehrungsStatus Beendet ist.</summary>
    [Fact]
    public void ShowShouldCli_WhenExecutionStatusIsCompleted_AndWorkItemStatusIsArchived_ReturnsFalse()
    {
        var result = WorkItemExecutionStatus.Completed.ShouldShowCli(WorkItemStatus.Archived);

        result.Should().BeFalse();
    }

    /// <summary>SollCliAnzeigen liefert weiterhin true, wenn AusfuehrungsStatus Aktiv ist und AufgabeStatus Gestartet ist.</summary>
    [Fact]
    public void ShowShouldCli_WhenExecutionStatusIsActive_AndWorkItemStatusIsStarted_ReturnsTrue()
    {
        var result = WorkItemExecutionStatus.Active.ShouldShowCli(WorkItemStatus.Started);

        result.Should().BeTrue();
    }

    /// <summary>SollCliAnzeigen liefert false, wenn AusfuehrungsStatus NichtGestartet ist, auch wenn AufgabeStatus Gestartet ist.</summary>
    [Fact]
    public void ShowShouldCli_WhenExecutionStatusIsNotStarted_AndWorkItemStatusIsStarted_ReturnsFalse()
    {
        var result = WorkItemExecutionStatus.NotStarted.ShouldShowCli(WorkItemStatus.Started);

        result.Should().BeFalse();
    }
}
