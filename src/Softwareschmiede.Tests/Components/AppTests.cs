using System.Text.RegularExpressions;
using FluentAssertions;

namespace Softwareschmiede.Tests.Components;

public sealed class AppTests
{
    /// <summary>
    /// Verifies that App.razor defines all SVG favicon link variants for browser support.
    /// </summary>
    [Fact]
    public void AppMarkup_ShouldContainSvgFaviconLinkTags()
    {
        var appMarkup = ReadAppRazor();

        appMarkup.Should().Contain("<link rel=\"icon\" type=\"image/svg+xml\" sizes=\"any\" href=\"favicon-hammer-pick.svg\" />");
        appMarkup.Should().Contain("<link rel=\"shortcut icon\" type=\"image/svg+xml\" href=\"favicon-hammer-pick.svg\" />");
        appMarkup.Should().Contain("<link rel=\"mask-icon\" href=\"favicon-hammer-pick.svg\" color=\"#f59e0b\" />");
    }

    /// <summary>
    /// Verifies that legacy .ico/.png favicon references are no longer used in App.razor.
    /// </summary>
    [Fact]
    public void AppMarkup_ShouldNotContainLegacyFaviconReferences()
    {
        var appMarkup = ReadAppRazor();

        appMarkup.Should().NotContain("favicon.ico");
        appMarkup.Should().NotContain("favicon.png");
    }

    /// <summary>
    /// Verifies that the new hammer-pick SVG favicon is referenced exactly three times.
    /// </summary>
    [Fact]
    public void AppMarkup_ShouldReferenceFaviconHammerPickSvgExactlyThreeTimes()
    {
        var appMarkup = ReadAppRazor();

        Regex.Matches(appMarkup, "favicon-hammer-pick\\.svg")
            .Should()
            .HaveCount(3);
    }

    private static string ReadAppRazor()
    {
        var root = FindRepositoryRoot();
        var appRazorPath = Path.Combine(root, "src", "Softwareschmiede", "Components", "App.razor");
        return File.ReadAllText(appRazorPath);
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
