using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Enums;

namespace Softwareschmiede.Tests.Application.Services;

public sealed class DiffAlgorithmServiceTests
{
    private readonly DiffAlgorithmService _sut = new(NullLogger<DiffAlgorithmService>.Instance);

    [Fact]
    public async Task GenerateDiffAsync_ShouldThrowArgumentNullException_WhenSourceOrTargetIsEmpty()
    {
        // Act
        var sourceAct = () => _sut.GenerateDiffAsync("", "target", Guid.NewGuid());
        var targetAct = () => _sut.GenerateDiffAsync("source", "", Guid.NewGuid());

        // Assert
        await sourceAct.Should().ThrowAsync<ArgumentNullException>();
        await targetAct.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task GenerateDiffAsync_ShouldReturnExpectedLineStatusesAndCounts()
    {
        // Arrange
        const string source = "a\nb\nc";
        const string target = "a\nx\nc";

        // Act
        var (blocks, added, removed, modified) = await _sut.GenerateDiffAsync(source, target, Guid.NewGuid());

        // Assert
        added.Should().Be(1);
        removed.Should().Be(1);
        modified.Should().Be(0);
        blocks.Should().ContainSingle();
        var lines = blocks.Single().DiffLines;
        lines.Select(l => l.LineStatus).Should().ContainInOrder(
            DiffLineStatus.Context,
            DiffLineStatus.Removed,
            DiffLineStatus.Added,
            DiffLineStatus.Context);
        lines[0].SourceLineNumber.Should().Be(1);
        lines[0].TargetLineNumber.Should().Be(1);
        lines[1].SourceLineNumber.Should().Be(2);
        lines[1].TargetLineNumber.Should().BeNull();
        lines[2].SourceLineNumber.Should().BeNull();
        lines[2].TargetLineNumber.Should().Be(2);
        lines[3].SourceLineNumber.Should().Be(3);
        lines[3].TargetLineNumber.Should().Be(3);
    }

    [Fact]
    public async Task GenerateDiffAsync_ShouldSplitBlocks_WhenChangeTypeSwitchesBetweenRemovedAndAdded()
    {
        // Act
        var (blocks, _, _, _) = await _sut.GenerateDiffAsync("a", "b", Guid.NewGuid());

        // Assert
        blocks.Should().HaveCount(2);
        blocks[0].BlockType.Should().Be(DiffBlockType.Removed);
        blocks[1].BlockType.Should().Be(DiffBlockType.Added);
    }
}
