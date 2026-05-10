using FluentAssertions;
using Softwareschmiede.Application.Services;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>Tests für die KI-Ausgabe-Pufferung.</summary>
public sealed class KiAusfuehrungsServiceTests
{
    /// <summary>Startet für neue Blöcke mit Leerzeile und Zeitstempel.</summary>
    [Fact]
    public void AddLine_ShouldInsertBlockHeader_WhenOutputGapExceedsThreshold()
    {
        // Arrange
        var current = new DateTimeOffset(2026, 5, 10, 13, 0, 0, TimeSpan.Zero);
        var session = new KiSession(() => current);

        // Act
        session.AddLine("Erste Zeile");
        current = current.AddSeconds(3);
        session.AddLine("Zweite Zeile");

        // Assert
        var lines = session.GetLines();
        lines.Should().HaveCount(6);
        lines[0].Should().BeEmpty();
        lines[1].Should().MatchRegex(@"^\d{2}\.\d{2}\.\d{4} \d{2}:\d{2}:\d{2}$");
        lines[2].Should().Be("Erste Zeile");
        lines[3].Should().BeEmpty();
        lines[4].Should().MatchRegex(@"^\d{2}\.\d{2}\.\d{4} \d{2}:\d{2}:\d{2}$");
        lines[5].Should().Be("Zweite Zeile");
    }

    /// <summary>Fasst nahe Ausgaben zu einem Block zusammen.</summary>
    [Fact]
    public void AddLine_ShouldKeepLinesInOneBlock_WhenOutputGapStaysBelowThreshold()
    {
        // Arrange
        var current = new DateTimeOffset(2026, 5, 10, 13, 0, 0, TimeSpan.Zero);
        var session = new KiSession(() => current);

        // Act
        session.AddLine("Erste Zeile");
        current = current.AddSeconds(1);
        session.AddLine("Zweite Zeile");

        // Assert
        var lines = session.GetLines();
        lines.Should().HaveCount(4);
        lines[0].Should().BeEmpty();
        lines[1].Should().MatchRegex(@"^\d{2}\.\d{2}\.\d{4} \d{2}:\d{2}:\d{2}$");
        lines[2].Should().Be("Erste Zeile");
        lines[3].Should().Be("Zweite Zeile");
    }
}
