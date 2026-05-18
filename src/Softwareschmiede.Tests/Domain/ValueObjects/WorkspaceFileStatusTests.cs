using FluentAssertions;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Tests.Domain.ValueObjects;

/// <summary>Tests für <see cref="WorkspaceFileStatus"/>.</summary>
public sealed class WorkspaceFileStatusTests
{
    /// <summary>Prüft die Statusdarstellung für typische Git-Porcelain-Werte.</summary>
    [Theory]
    [InlineData("M ", "M", "git-status-staged", "Staged")]
    [InlineData(" M", "M", "git-status-dirty", "Geändert")]
    [InlineData("MM", "MM", "git-status-staged", "Staged + geändert")]
    [InlineData("A ", "A", "git-status-added", "Staged")]
    [InlineData("??", "??", "git-status-untracked", "Nicht versioniert")]
    [InlineData("UU", "UU", "git-status-conflict", "Merge-Konflikt")]
    [InlineData("D ", "D", "git-status-deleted", "Gelöscht")]
    public void Parse_ShouldMapBadgeTextCssClassAndDescription(string porcelainCode, string expectedBadgeText, string expectedCssClass, string expectedDescription)
    {
        var status = WorkspaceFileStatus.Parse(porcelainCode);

        status.BadgeText.Should().Be(expectedBadgeText);
        status.CssClass.Should().Be(expectedCssClass);
        status.Description.Should().Be(expectedDescription);
    }

    /// <summary>Prüft, dass ungültige Statuscodes abgewiesen werden.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("M")]
    public void Parse_ShouldThrow_WhenPorcelainCodeIsInvalid(string porcelainCode)
    {
        var act = () => WorkspaceFileStatus.Parse(porcelainCode);

        act.Should().Throw<ArgumentException>();
    }
}
