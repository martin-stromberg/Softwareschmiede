using FluentAssertions;
using Softwareschmiede.App.Behaviors;

namespace Softwareschmiede.Tests.App.Behaviors;

/// <summary>Tests für StatusAenderungsErkennung.</summary>
public sealed class StatusChangeDetectionTests
{
    private readonly StatusChangeDetection _sut = new();

    /// <summary>Die Erstbeobachtung einer Id gilt als Baseline und löst keine Animation aus.</summary>
    [Fact]
    public void HasSelfChanged_ShouldReturnFalse_OnFirstobservation()
    {
        // Arrange
        var workItemId = Guid.NewGuid();

        // Act
        var result = _sut.HasChanged(workItemId, "▶ Läuft");

        // Assert
        result.Should().BeFalse();
    }

    /// <summary>Ein gleichbleibender Status (Routine-Refresh) löst keine Animation aus.</summary>
    [Fact]
    public void HasSelfChanged_ShouldReturnFalse_WhenStatusUnchanged()
    {
        // Arrange
        var workItemId = Guid.NewGuid();
        _sut.HasChanged(workItemId, "▶ Läuft");

        // Act
        var result = _sut.HasChanged(workItemId, "▶ Läuft");

        // Assert
        result.Should().BeFalse();
    }

    /// <summary>Ein abweichender Status gegenüber dem gemerkten Wert wird als echter Wechsel erkannt.</summary>
    [Fact]
    public void HasSelfChanged_ShouldReturnTrue_WhenStatusSwitches()
    {
        // Arrange
        var workItemId = Guid.NewGuid();
        _sut.HasChanged(workItemId, "▶ Läuft");

        // Act
        var result = _sut.HasChanged(workItemId, "⏸ Wartet");

        // Assert
        result.Should().BeTrue();
    }

    /// <summary>Mehrere Aufgabe-Ids werden unabhängig voneinander verfolgt.</summary>
    [Fact]
    public void HasSelfChanged_ShouldTrackIdsIndependent()
    {
        // Arrange
        var workItemIdA = Guid.NewGuid();
        var workItemIdB = Guid.NewGuid();
        _sut.HasChanged(workItemIdA, "▶ Läuft");
        _sut.HasChanged(workItemIdB, "▶ Läuft");

        // Act
        var resultA = _sut.HasChanged(workItemIdA, "⏸ Wartet");
        var resultB = _sut.HasChanged(workItemIdB, "▶ Läuft");

        // Assert
        resultA.Should().BeTrue();
        resultB.Should().BeFalse();
    }
}
