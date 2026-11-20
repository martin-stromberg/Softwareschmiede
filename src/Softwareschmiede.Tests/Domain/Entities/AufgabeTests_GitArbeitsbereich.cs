using FluentAssertions;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Tests.Domain.Entities;

/// <summary>Tests für den <see cref="WorkItem.GitWorkspace"/>-Wrapper.</summary>
public sealed class AufgabeTests_GitArbeitsbereich
{
    /// <summary>Der Getter liefert eine GitArbeitsbereich-Instanz mit den erwarteten Werten, wenn beide zugrunde liegenden Felder gesetzt sind.</summary>
    [Fact]
    public void GitArbeitsbereich_Get_ShouldReturnInstance_WhenBranchNameAndLokalerKlonPfadAreSet()
    {
        // Arrange
        var workItem = new WorkItem
        {
            BranchName = "feature/x",
            LocalClonePath = @"C:\repos\task-1"
        };

        // Act
        var gitArbeitsbereich = workItem.GitWorkspace;

        // Assert
        gitArbeitsbereich.Should().Be(new GitWorkspace("feature/x", @"C:\repos\task-1"));
    }

    /// <summary>Der Setter schreibt BranchName und LokalerKlonPfad korrekt in die zugrunde liegenden Felder.</summary>
    [Fact]
    public void GitArbeitsbereich_Set_ShouldWriteBranchNameAndLokalerKlonPfad()
    {
        // Arrange
        var workItem = new WorkItem();

        // Act
        workItem.GitWorkspace = new GitWorkspace("feature/y", @"C:\repos\task-2");

        // Assert
        workItem.BranchName.Should().Be("feature/y");
        workItem.LocalClonePath.Should().Be(@"C:\repos\task-2");
    }

    /// <summary>Der Setter mit null löscht BranchName und LokalerKlonPfad.</summary>
    [Fact]
    public void GitArbeitsbereich_SetNull_ShouldClearBranchNameAndLokalerKlonPfad()
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

    /// <summary>Der Getter liefert null, wenn nur LokalerKlonPfad gesetzt ist und BranchName fehlt (Teilzustand).</summary>
    [Fact]
    public void GitArbeitsbereich_Get_ShouldReturnNull_WhenOnlyLokalerKlonPfadIsSet()
    {
        // Arrange
        var workItem = new WorkItem
        {
            BranchName = null,
            LocalClonePath = @"C:\repos\task-4"
        };

        // Act
        var gitArbeitsbereich = workItem.GitWorkspace;

        // Assert
        gitArbeitsbereich.Should().BeNull();
    }

    /// <summary>Der Getter liefert null, wenn nur BranchName gesetzt ist und LokalerKlonPfad fehlt (Teilzustand).</summary>
    [Fact]
    public void GitArbeitsbereich_Get_ShouldReturnNull_WhenOnlyBranchNameIsSet()
    {
        // Arrange
        var workItem = new WorkItem
        {
            BranchName = "feature/only-branch",
            LocalClonePath = null
        };

        // Act
        var gitArbeitsbereich = workItem.GitWorkspace;

        // Assert
        gitArbeitsbereich.Should().BeNull();
    }

    /// <summary>Der Getter liefert null, wenn beide Felder unbesetzt sind.</summary>
    [Fact]
    public void GitArbeitsbereich_Get_ShouldReturnNull_WhenBothFieldsAreNull()
    {
        // Arrange
        var workItem = new WorkItem
        {
            BranchName = null,
            LocalClonePath = null
        };

        // Act
        var gitArbeitsbereich = workItem.GitWorkspace;

        // Assert
        gitArbeitsbereich.Should().BeNull();
    }
}
