using FluentAssertions;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Tests.Domain.Entities;

/// <summary>Tests für den <see cref="WorkItem.GitWorkspace"/>-Wrapper.</summary>
public sealed class WorkItemTests_GitWorkarea
{
    /// <summary>Der Getter liefert eine GitArbeitsbereich-Instanz mit den erwarteten Werten, wenn beide zugrunde liegenden Felder gesetzt sind.</summary>
    [Fact]
    public void GitWorkarea_Get_ShouldReturnInstance_WhenBranchNameAndLocalClonePathAreSet()
    {
        // Arrange
        var workItem = new WorkItem
        {
            BranchName = "feature/x",
            LocalClonePath = @"C:\repos\task-1"
        };

        // Act
        var gitWorkarea = workItem.GitWorkspace;

        // Assert
        gitWorkarea.Should().Be(new GitWorkspace("feature/x", @"C:\repos\task-1"));
    }

    /// <summary>Der Setter schreibt BranchName und LocalClonePath korrekt in die zugrunde liegenden Felder.</summary>
    [Fact]
    public void GitWorkarea_Set_ShouldWriteBranchNameAndLocalClonePath()
    {
        // Arrange
        var workItem = new WorkItem();

        // Act
        workItem.GitWorkspace = new GitWorkspace("feature/y", @"C:\repos\task-2");

        // Assert
        workItem.BranchName.Should().Be("feature/y");
        workItem.LocalClonePath.Should().Be(@"C:\repos\task-2");
    }

    /// <summary>Der Setter mit null löscht BranchName und LocalClonePath.</summary>
    [Fact]
    public void GitWorkarea_SetNull_ShouldClearBranchNameAndLocalClonePath()
    {
        // Arrange
        var workItem = new WorkItem
        {
            BranchName = "feature/z",
            LocalClonePath = @"C:\repos\task-3"
        };

        // Act
        workItem.GitWorkspace = null;

        // Assert
        workItem.BranchName.Should().BeNull();
        workItem.LocalClonePath.Should().BeNull();
    }

    /// <summary>Der Getter liefert null, wenn nur LocalClonePath gesetzt ist und BranchName fehlt (Teilzustand).</summary>
    [Fact]
    public void GitWorkarea_Get_ShouldReturnNull_WhenOnlyLocalClonePathIsSet()
    {
        // Arrange
        var workItem = new WorkItem
        {
            BranchName = null,
            LocalClonePath = @"C:\repos\task-4"
        };

        // Act
        var gitWorkarea = workItem.GitWorkspace;

        // Assert
        gitWorkarea.Should().BeNull();
    }

    /// <summary>Der Getter liefert null, wenn nur BranchName gesetzt ist und LocalClonePath fehlt (Teilzustand).</summary>
    [Fact]
    public void GitWorkarea_Get_ShouldReturnNull_WhenOnlyBranchNameIsSet()
    {
        // Arrange
        var workItem = new WorkItem
        {
            BranchName = "feature/only-branch",
            LocalClonePath = null
        };

        // Act
        var gitWorkarea = workItem.GitWorkspace;

        // Assert
        gitWorkarea.Should().BeNull();
    }

    /// <summary>Der Getter liefert null, wenn beide Felder unbesetzt sind.</summary>
    [Fact]
    public void GitWorkarea_Get_ShouldReturnNull_WhenBothFieldsAreNull()
    {
        // Arrange
        var workItem = new WorkItem
        {
            BranchName = null,
            LocalClonePath = null
        };

        // Act
        var gitWorkarea = workItem.GitWorkspace;

        // Assert
        gitWorkarea.Should().BeNull();
    }
}
