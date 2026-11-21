using FluentAssertions;
using Moq;
using Softwareschmiede.App.Services;
using Softwareschmiede.App.ViewModels;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Tests.App.ViewModels;

/// <summary>Unit-Tests für TaskDetailViewModel.OeffneArbeitsverzeichnisAsync() und OpenIdeAsync() bezüglich Nutzung des WorkingDirectoryResolver.</summary>
public sealed class TaskDetailViewModelTests_WorkingDirectory : TaskDetailViewModelTestBase
{
    /// <summary>Präfix für über CreateTempDirectory() erzeugte temporäre Verzeichnisse.</summary>
    protected override string TempDirectoryPrefix => "tdvm_arbeitsverzeichnis_tests";

    /// <summary>Bei konfiguriertem Arbeitsverzeichnis wird der über WorkingDirectoryResolver aufgelöste Pfad an ArbeitsverzeichnisOeffnenService übergeben, nicht der Repository-Root.</summary>
    [Fact]
    public async Task OpenWorkingDirectory_WithConfiguredWorkingDirectory_CallsServiceWithResolvedPathOn()
    {
        var workingDirectory = CreateTempDirectory();
        Directory.CreateDirectory(Path.Combine(workingDirectory, "backend"));
        var workItem = await CreateWorkItemWithRepositoryAsync("backend");
        await _workItemService.StartAsync(workItem.Id, "feature/x", workingDirectory, ct: TestContext.Current.CancellationToken);

        var processStarterMock = new Mock<IProcessStarter>();
        var sut = CreateSut(processStarterMock);
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.OpenWorkingDirectoryCommand.Execute(null);

        var expectedPath = Path.GetFullPath(Path.Combine(workingDirectory, "backend"));
        processStarterMock.Verify(
            p => p.Start(It.Is<ProcessStartRequest>(a => a.Arguments == $"\"{expectedPath}\"")),
            Times.Once);
    }

    /// <summary>Ohne RepositoryStartKonfiguration (bzw. ohne WorkingDirectoryRelativePath) wird weiterhin der Repository-Root (LocalClonePath) verwendet.</summary>
    [Fact]
    public async Task OpenWorkingDirectory_WithoutConfiguration_CallsServiceWithRepositoryRootOn()
    {
        var workingDirectory = CreateTempDirectory();
        var workItem = await CreateWorkItemWithRepositoryAsync(null);
        await _workItemService.StartAsync(workItem.Id, "feature/x", workingDirectory, ct: TestContext.Current.CancellationToken);

        var processStarterMock = new Mock<IProcessStarter>();
        var sut = CreateSut(processStarterMock);
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.OpenWorkingDirectoryCommand.Execute(null);

        processStarterMock.Verify(
            p => p.Start(It.Is<ProcessStartRequest>(a => a.Arguments == $"\"{workingDirectory}\"")),
            Times.Once);
    }

    /// <summary>Verweist die Startkonfiguration auf ein nicht existierendes Unterverzeichnis, wird eine aussagekräftige ErrorMessage gesetzt und kein Prozess gestartet.</summary>
    [Fact]
    public async Task OpenWorkingDirectory_WithInvalidWorkingDirectory_ShowsErrormessage()
    {
        var workingDirectory = CreateTempDirectory();
        var workItem = await CreateWorkItemWithRepositoryAsync("does-not-exist");
        await _workItemService.StartAsync(workItem.Id, "feature/x", workingDirectory, ct: TestContext.Current.CancellationToken);

        var processStarterMock = new Mock<IProcessStarter>();
        var sut = CreateSut(processStarterMock);
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.OpenWorkingDirectoryCommand.Execute(null);

        sut.ErrorMessage.Should().StartWith("Arbeitsverzeichnis konnte nicht geöffnet werden:");
        processStarterMock.Verify(p => p.Start(It.IsAny<ProcessStartRequest>()), Times.Never);
    }

    /// <summary>OpenIdeAsync findet über das aufgelöste IDE-Plugin (FindEntryPointsAsync) eine Solution im über WorkingDirectoryResolver aufgelösten Arbeitsverzeichnis und öffnet sie, obwohl im Repository-Root keine .sln-Datei liegt.</summary>
    [Fact]
    public async Task OpenIdeAsync_FindsSolutionInResolvedWorkingDirectory()
    {
        var workingDirectory = CreateTempDirectory();
        var subdirectory = Path.Combine(workingDirectory, "src", "solutions");
        Directory.CreateDirectory(subdirectory);
        var solutionPath = Path.Combine(subdirectory, "MyApp.sln");
        File.WriteAllText(solutionPath, string.Empty);

        var workItem = await CreateWorkItemWithRepositoryAsync(Path.Combine("src", "solutions"));
        await _workItemService.StartAsync(workItem.Id, "feature/x", workingDirectory, ct: TestContext.Current.CancellationToken);

        var processStarterMock = new Mock<IProcessStarter>();
        var sut = CreateSut(processStarterMock);
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.OpenCanIde.Should().BeTrue("das konfigurierte Arbeitsverzeichnis existiert");
        sut.OpenIdeCommand.CanExecute(null).Should().BeTrue();

        await ((AsyncRelayCommand)sut.OpenIdeCommand).ExecuteAsync();

        processStarterMock.Verify(
            p => p.Start(It.Is<ProcessStartRequest>(a => a.FileName == solutionPath && a.UseShellExecute)),
            Times.Once);
    }
}
