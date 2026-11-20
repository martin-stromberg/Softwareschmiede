using FluentAssertions;
using Softwareschmiede.App.Behaviors;

namespace Softwareschmiede.Tests.App.Behaviors;

/// <summary>Tests für StatusAenderungsErkennung.</summary>
public sealed class StatusAenderungsErkennungTests
{
    private readonly StatusAenderungsErkennung _sut = new();

    /// <summary>Die Erstbeobachtung einer Id gilt als Baseline und löst keine Animation aus.</summary>
    [Fact]
    public void HatSichGeaendert_ShouldReturnFalse_OnErstbeobachtung()
    {
        // Arrange
        var workItemId = Guid.NewGuid();

        // Act
        var result = _sut.HatSichGeaendert(workItemId, "▶ Läuft");

        // Assert
        result.Should().BeFalse();
    }

    /// <summary>Ein gleichbleibender Status (Routine-Refresh) löst keine Animation aus.</summary>
    [Fact]
    public void HatSichGeaendert_ShouldReturnFalse_WhenStatusUnveraendert()
    {
        // Arrange
        var workItemId = Guid.NewGuid();
        _sut.HatSichGeaendert(workItemId, "▶ Läuft");

        // Act
        var result = _sut.HatSichGeaendert(workItemId, "▶ Läuft");

        // Assert
        result.Should().BeFalse();
    }

    /// <summary>Ein abweichender Status gegenüber dem gemerkten Wert wird als echter Wechsel erkannt.</summary>
    [Fact]
    public void HatSichGeaendert_ShouldReturnTrue_WhenStatusWechselt()
    {
        // Arrange
        var workItemId = Guid.NewGuid();
        _sut.HatSichGeaendert(workItemId, "▶ Läuft");

        // Act
        var result = _sut.HatSichGeaendert(workItemId, "⏸ Wartet");

        // Assert
        result.Should().BeTrue();
    }

    /// <summary>Mehrere Aufgabe-Ids werden unabhängig voneinander verfolgt.</summary>
    [Fact]
    public void HatSichGeaendert_ShouldTrackIdsUnabhaengig()
    {
        // Arrange
        var aufgabeIdA = Guid.NewGuid();
        var aufgabeIdB = Guid.NewGuid();
        _sut.HatSichGeaendert(aufgabeIdA, "▶ Läuft");
        _sut.HatSichGeaendert(aufgabeIdB, "▶ Läuft");

        // Act
        var resultA = _sut.HatSichGeaendert(aufgabeIdA, "⏸ Wartet");
        var resultB = _sut.HatSichGeaendert(aufgabeIdB, "▶ Läuft");

        // Assert
        resultA.Should().BeTrue();
        resultB.Should().BeFalse();
    }
}
