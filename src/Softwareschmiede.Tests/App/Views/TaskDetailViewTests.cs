using FluentAssertions;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Softwareschmiede.App.Views;
using static Softwareschmiede.Tests.Helpers.WpfUnitTestHelpers;

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
        xaml.Should().Contain("AutomationName=\"PullRequestsAktualisieren\"");
        xaml.Should().Contain("ButtonCommand=\"{Binding PullRequestsAktualisierenCommand}\"");
        xaml.Should().Contain("AutomationProperties.Name=\"PullRequestUrlOeffnen\"");
        xaml.Should().Contain("Command=\"{Binding DataContext.PullRequestUrlOeffnenCommand, ElementName=Root}\"");
    }

    /// <summary>Die CLI-Konsole liegt in einem vertikalen ScrollViewer, bleibt aber unter ihrem stabilen Automation-Namen erreichbar.</summary>
    [Fact]
    public void Xaml_CliKonsole_IstVertikalScrollbar()
    {
        var solutionRoot = FindSolutionRoot();
        var xamlPath = Path.Combine(solutionRoot, "src", "Softwareschmiede.App", "Views", "TaskDetailView.xaml");

        var xaml = File.ReadAllText(xamlPath);

        xaml.Should().Contain("AutomationProperties.Name=\"TerminalScrollViewer\"");
        xaml.Should().Contain("PreviewMouseDown=\"OnTerminalScrollViewerPreviewMouseDown\"");
        xaml.Should().Contain("VerticalScrollBarVisibility=\"Auto\"");
        xaml.Should().Contain("HorizontalScrollBarVisibility=\"Disabled\"");
        xaml.Should().Contain("CanContentScroll=\"True\"");
        xaml.Should().Contain("x:Name=\"TerminalConsole\"");
        xaml.Should().Contain("AutomationProperties.Name=\"TerminalConsole\"");
    }

    /// <summary>Klicks in die CLI-Fläche sollen den Terminal-Fokuspfad auslösen, Scrollbar-Bedienung nicht.</summary>
    [Fact]
    public void TerminalScrollViewer_Clickziel_SteuertFokuspfad()
    {
        RunOnSta(() =>
        {
            TaskDetailView.ShouldFocusTerminalFromScrollViewerMouseSource(new Border())
                .Should().BeTrue("Klicks in die freie CLI-Fläche müssen TerminalConsole fokussieren");

            TaskDetailView.ShouldFocusTerminalFromScrollViewerMouseSource(new ScrollBar())
                .Should().BeFalse("Klicks auf die Scrollbar sollen nur scrollen und keinen Terminal-Fokus erzwingen");
        });
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
