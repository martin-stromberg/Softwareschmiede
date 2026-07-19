using FluentAssertions;

namespace Softwareschmiede.Tests.App.Views;

/// <summary>Tests für die statische Struktur der <c>TaskDetailView</c>.</summary>
public sealed class TaskDetailViewTests
{
    /// <summary>Die Pull-Request-Aktion ist als Ribbon-Button mit stabilem Automation-Namen vorhanden.</summary>
    [Fact]
    public void Xaml_ContainsPullRequestActionButton()
    {
        var solutionRoot = FindSolutionRoot();
        var xamlPath = Path.Combine(solutionRoot, "src", "Softwareschmiede.App", "Views", "TaskDetailView.xaml");

        var xaml = File.ReadAllText(xamlPath);

        xaml.Should().Contain("GruppenName=\"Pull Request\"");
        xaml.Should().Contain("AutomationName=\"PullRequestErstellen\"");
        xaml.Should().Contain("ButtonCommand=\"{Binding PullRequestErstellenCommand}\"");
    }

    private static string FindSolutionRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Softwareschmiede.slnx")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Softwareschmiede.slnx wurde nicht gefunden.");
    }
}
