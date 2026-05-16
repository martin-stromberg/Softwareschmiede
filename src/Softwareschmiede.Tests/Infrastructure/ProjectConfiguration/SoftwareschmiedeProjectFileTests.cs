using System.Xml.Linq;
using FluentAssertions;

namespace Softwareschmiede.Tests.Infrastructure.ProjectConfiguration;

/// <summary>
/// Contract tests for the Softwareschmiede project file.
/// </summary>
public sealed class SoftwareschmiedeProjectFileTests
{
    /// <summary>
    /// Verifies that the root start.ps1 is linked as a None item in Softwareschmiede.csproj.
    /// </summary>
    [Fact]
    public void SoftwareschmiedeCsproj_ShouldContainLinkedStartScriptItem()
    {
        // Arrange
        var repositoryRoot = ResolveRepositoryRoot();
        var projectFilePath = Path.Combine(repositoryRoot, "src", "Softwareschmiede", "Softwareschmiede.csproj");
        var document = XDocument.Load(projectFilePath);

        // Act
        var linkedItem = document
            .Descendants()
            .FirstOrDefault(element =>
                element.Name.LocalName == "None" &&
                string.Equals(element.Attribute("Include")?.Value, "..\\..\\start.ps1", StringComparison.OrdinalIgnoreCase));

        // Assert
        linkedItem.Should().NotBeNull();
        linkedItem!.Attribute("Link")?.Value.Should().Be("start.ps1");
    }

    private static string ResolveRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var startScriptPath = Path.Combine(current.FullName, "start.ps1");
            var projectFilePath = Path.Combine(current.FullName, "src", "Softwareschmiede", "Softwareschmiede.csproj");

            if (File.Exists(startScriptPath) && File.Exists(projectFilePath))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Repository root could not be resolved.");
    }
}
