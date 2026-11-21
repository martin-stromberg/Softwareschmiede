using FluentAssertions;
using Moq;
using Softwareschmiede.App.Services;
using Softwareschmiede.App.ViewModels;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.App.ViewModels;

/// <summary>
/// Unit-Tests für TaskDetailViewModel.OpenIdeAsync() bezüglich des automatischen Visual-Studio-Code-Fallbacks
/// (via PluginSelectionService.ResolveIdePluginAsync, wenn kein Visual-Studio-Plugin explizit kompatibel ist)
/// und der Nutzung des WorkingDirectoryResolver.
/// </summary>
public sealed class TaskDetailViewModelTests_VisualStudioCode : TaskDetailViewModelTestBase
{
    /// <summary>Präfix für über CreateTempDirectory() erzeugte temporäre Verzeichnisse.</summary>
    protected override string TempDirectoryPrefix => "tdvm_vscode_tests";

    /// <summary>Bei konfiguriertem Arbeitsverzeichnis wird der (standardmäßig aktive) Visual-Studio-Code-Fallback mit dem über WorkingDirectoryResolver aufgelösten Pfad gestartet, nicht mit dem Repository-Root.</summary>
    [Fact]
    public async Task OpenIdeAsync_WithoutSolutionWithConfiguredWorkingDirectory_CallsVsCodeWithResolvedPathOn()
    {
        var workingDirectory = CreateTempDirectory();
        Directory.CreateDirectory(Path.Combine(workingDirectory, "backend"));
        var workItem = await CreateWorkItemWithRepositoryAsync("backend");
        await _workItemService.StartAsync(workItem.Id, "feature/x", workingDirectory, ct: TestContext.Current.CancellationToken);

        var processStarterMock = new Mock<IProcessStarter>();
        var locator = new TestVisualStudioCodeLocator(new VisualStudioCodeAvailability(true, "code.cmd"));
        var sut = CreateSut(processStarterMock, locator);
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        await ((AsyncRelayCommand)sut.OpenIdeCommand).ExecuteAsync();

        var expectedPath = Path.GetFullPath(Path.Combine(workingDirectory, "backend"));
        processStarterMock.Verify(
            p => p.Start(It.Is<ProcessStartRequest>(a =>
                a.FileName == "code.cmd"
                && a.Arguments == $"\"{expectedPath}\"")),
            Times.Once);
    }

    /// <summary>Ohne RepositoryStartKonfiguration wird der Visual-Studio-Code-Fallback weiterhin mit dem Repository-Root (LocalClonePath) gestartet.</summary>
    [Fact]
    public async Task OpenIdeAsync_WithoutSolutionWithoutConfiguration_CallsVsCodeWithRepositoryRootOn()
    {
        var workingDirectory = CreateTempDirectory();
        var workItem = await CreateWorkItemWithRepositoryAsync(null);
        await _workItemService.StartAsync(workItem.Id, "feature/x", workingDirectory, ct: TestContext.Current.CancellationToken);

        var processStarterMock = new Mock<IProcessStarter>();
        var locator = new TestVisualStudioCodeLocator(new VisualStudioCodeAvailability(true, "code.cmd"));
        var sut = CreateSut(processStarterMock, locator);
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        await ((AsyncRelayCommand)sut.OpenIdeCommand).ExecuteAsync();

        processStarterMock.Verify(
            p => p.Start(It.Is<ProcessStartRequest>(a =>
                a.FileName == "code.cmd"
                && a.Arguments == $"\"{workingDirectory}\"")),
            Times.Once);
    }

    /// <summary>Ist Visual Studio Code nicht verfügbar, wird eine aussagekräftige ErrorMessage gesetzt und kein Prozess gestartet.</summary>
    [Fact]
    public async Task OpenIdeAsync_WithoutSolutionWithoutVsCode_ShowsErrormessage()
    {
        var workingDirectory = CreateTempDirectory();
        var workItem = await CreateWorkItemWithRepositoryAsync(null);
        await _workItemService.StartAsync(workItem.Id, "feature/x", workingDirectory, ct: TestContext.Current.CancellationToken);

        var processStarterMock = new Mock<IProcessStarter>();
        var locator = new TestVisualStudioCodeLocator(VisualStudioCodeAvailability.NotAvailable);
        var sut = CreateSut(processStarterMock, locator);
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        await ((AsyncRelayCommand)sut.OpenIdeCommand).ExecuteAsync();

        sut.ErrorMessage.Should().Contain("Visual Studio Code");
        processStarterMock.Verify(p => p.Start(It.IsAny<ProcessStartRequest>()), Times.Never);
    }

    /// <summary>Ohne Solutions im aufgelösten (konfigurierten) Arbeitsverzeichnis fällt OpenIdeAsync auf Visual Studio Code mit dem aufgelösten Verzeichnis zurück, selbst wenn im Repository-Root eine .sln-Datei liegt.</summary>
    [Fact]
    public async Task OpenIdeAsync_WithoutSolutionsInWorkingDirectory_FallsToVsCodeBack()
    {
        var workingDirectory = CreateTempDirectory();
        // .sln im Repository-Root anlegen: darf NICHT gefunden werden, da nur das konfigurierte Unterverzeichnis durchsucht werden darf.
        File.WriteAllText(Path.Combine(workingDirectory, "RootLoesung.sln"), string.Empty);
        var subdirectory = Path.Combine(workingDirectory, "backend");
        Directory.CreateDirectory(subdirectory);

        var workItem = await CreateWorkItemWithRepositoryAsync("backend");
        await _workItemService.StartAsync(workItem.Id, "feature/x", workingDirectory, ct: TestContext.Current.CancellationToken);

        var processStarterMock = new Mock<IProcessStarter>();
        var locator = new TestVisualStudioCodeLocator(new VisualStudioCodeAvailability(true, "code.cmd"));
        var sut = CreateSut(processStarterMock, locator);
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        await ((AsyncRelayCommand)sut.OpenIdeCommand).ExecuteAsync();

        var expectedPath = Path.GetFullPath(subdirectory);
        processStarterMock.Verify(
            p => p.Start(It.Is<ProcessStartRequest>(a =>
                a.FileName == "code.cmd"
                && a.Arguments == $"\"{expectedPath}\"")),
            Times.Once);
    }
}
