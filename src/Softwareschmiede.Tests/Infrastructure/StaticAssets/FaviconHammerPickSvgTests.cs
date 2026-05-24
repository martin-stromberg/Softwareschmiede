using FluentAssertions;

namespace Softwareschmiede.Tests.Infrastructure.StaticAssets;

public sealed class FaviconHammerPickSvgTests
{
    /// <summary>
    /// Verifies that the favicon SVG file is present in the web root.
    /// </summary>
    [Fact]
    public void FaviconHammerPickSvg_ShouldExistInWwwroot()
    {
        var svgPath = GetSvgPath();

        File.Exists(svgPath).Should().BeTrue();
    }

    /// <summary>
    /// Verifies that the favicon SVG contains expected structural and branding markers.
    /// </summary>
    [Fact]
    public void FaviconHammerPickSvg_ShouldContainRequiredMarkers()
    {
        var svgPath = GetSvgPath();
        var svg = File.ReadAllText(svgPath);

        svg.Should().Contain("<svg");
        svg.Should().Contain("xmlns=\"http://www.w3.org/2000/svg\"");
        svg.Should().Contain("viewBox=\"0 0 64 64\"");
        svg.Should().Contain("<title>Softwareschmiede Favicon</title>");
        svg.Should().Contain("<desc>Crossed hammer and pick symbol for the Softwareschmiede web application.</desc>");
        svg.Should().Contain("#f59e0b");
    }

    private static string GetSvgPath()
    {
        var root = FindRepositoryRoot();
        return Path.Combine(root, "src", "Softwareschmiede", "wwwroot", "favicon-hammer-pick.svg");
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Softwareschmiede.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Repository root with Softwareschmiede.slnx not found.");
    }
}
