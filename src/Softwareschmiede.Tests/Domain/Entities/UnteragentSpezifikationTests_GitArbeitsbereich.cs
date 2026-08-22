using FluentAssertions;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Tests.Domain.Entities;

/// <summary>Tests für den <see cref="UnteragentSpezifikation.GitArbeitsbereich"/>-Wrapper.</summary>
public sealed class UnteragentSpezifikationTests_GitArbeitsbereich
{
    /// <summary>Der Getter liefert eine GitArbeitsbereich-Instanz mit den Werten von Branch und ClonePfad.</summary>
    [Fact]
    public void GitArbeitsbereich_Get_ShouldReturnInstance_WithBranchAndClonePfad()
    {
        // Arrange
        var unteragent = new UnteragentSpezifikation
        {
            Branch = "feature/backend",
            ClonePfad = @"C:\repos\clones\repo_feature_backend"
        };

        // Act
        var gitArbeitsbereich = unteragent.GitArbeitsbereich;

        // Assert
        gitArbeitsbereich.Should().Be(new GitArbeitsbereich("feature/backend", @"C:\repos\clones\repo_feature_backend"));
    }

    /// <summary>Der Setter schreibt BranchName und ClonePfad korrekt in die zugrunde liegenden Felder Branch und ClonePfad.</summary>
    [Fact]
    public void GitArbeitsbereich_Set_ShouldWriteBranchAndClonePfad()
    {
        // Arrange
        var unteragent = new UnteragentSpezifikation();

        // Act
        unteragent.GitArbeitsbereich = new GitArbeitsbereich("feature/frontend", @"C:\repos\clones\repo_feature_frontend");

        // Assert
        unteragent.Branch.Should().Be("feature/frontend");
        unteragent.ClonePfad.Should().Be(@"C:\repos\clones\repo_feature_frontend");
    }
}
