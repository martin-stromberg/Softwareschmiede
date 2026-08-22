using FluentAssertions;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Tests.Domain.Entities;

/// <summary>Tests für den <see cref="Aufgabe.GitArbeitsbereich"/>-Wrapper.</summary>
public sealed class AufgabeTests_GitArbeitsbereich
{
    /// <summary>Der Getter liefert eine GitArbeitsbereich-Instanz mit den erwarteten Werten, wenn beide zugrunde liegenden Felder gesetzt sind.</summary>
    [Fact]
    public void GitArbeitsbereich_Get_ShouldReturnInstance_WhenBranchNameAndLokalerKlonPfadAreSet()
    {
        // Arrange
        var aufgabe = new Aufgabe
        {
            BranchName = "feature/x",
            LokalerKlonPfad = @"C:\repos\task-1"
        };

        // Act
        var gitArbeitsbereich = aufgabe.GitArbeitsbereich;

        // Assert
        gitArbeitsbereich.Should().Be(new GitArbeitsbereich("feature/x", @"C:\repos\task-1"));
    }

    /// <summary>Der Setter schreibt BranchName und LokalerKlonPfad korrekt in die zugrunde liegenden Felder.</summary>
    [Fact]
    public void GitArbeitsbereich_Set_ShouldWriteBranchNameAndLokalerKlonPfad()
    {
        // Arrange
        var aufgabe = new Aufgabe();

        // Act
        aufgabe.GitArbeitsbereich = new GitArbeitsbereich("feature/y", @"C:\repos\task-2");

        // Assert
        aufgabe.BranchName.Should().Be("feature/y");
        aufgabe.LokalerKlonPfad.Should().Be(@"C:\repos\task-2");
    }

    /// <summary>Der Setter mit null löscht BranchName und LokalerKlonPfad.</summary>
    [Fact]
    public void GitArbeitsbereich_SetNull_ShouldClearBranchNameAndLokalerKlonPfad()
    {
        // Arrange
        var aufgabe = new Aufgabe
        {
            BranchName = "feature/z",
            LokalerKlonPfad = @"C:\repos\task-3"
        };

        // Act
        aufgabe.GitArbeitsbereich = null;

        // Assert
        aufgabe.BranchName.Should().BeNull();
        aufgabe.LokalerKlonPfad.Should().BeNull();
    }

    /// <summary>Der Getter liefert null, wenn nur LokalerKlonPfad gesetzt ist und BranchName fehlt (Teilzustand).</summary>
    [Fact]
    public void GitArbeitsbereich_Get_ShouldReturnNull_WhenOnlyLokalerKlonPfadIsSet()
    {
        // Arrange
        var aufgabe = new Aufgabe
        {
            BranchName = null,
            LokalerKlonPfad = @"C:\repos\task-4"
        };

        // Act
        var gitArbeitsbereich = aufgabe.GitArbeitsbereich;

        // Assert
        gitArbeitsbereich.Should().BeNull();
    }

    /// <summary>Der Getter liefert null, wenn nur BranchName gesetzt ist und LokalerKlonPfad fehlt (Teilzustand).</summary>
    [Fact]
    public void GitArbeitsbereich_Get_ShouldReturnNull_WhenOnlyBranchNameIsSet()
    {
        // Arrange
        var aufgabe = new Aufgabe
        {
            BranchName = "feature/only-branch",
            LokalerKlonPfad = null
        };

        // Act
        var gitArbeitsbereich = aufgabe.GitArbeitsbereich;

        // Assert
        gitArbeitsbereich.Should().BeNull();
    }

    /// <summary>Der Getter liefert null, wenn beide Felder unbesetzt sind.</summary>
    [Fact]
    public void GitArbeitsbereich_Get_ShouldReturnNull_WhenBothFieldsAreNull()
    {
        // Arrange
        var aufgabe = new Aufgabe
        {
            BranchName = null,
            LokalerKlonPfad = null
        };

        // Act
        var gitArbeitsbereich = aufgabe.GitArbeitsbereich;

        // Assert
        gitArbeitsbereich.Should().BeNull();
    }
}
