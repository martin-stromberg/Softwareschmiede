using FluentAssertions;
using Moq;
using Softwareschmiede.App.Services;
using Softwareschmiede.App.ViewModels;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Tests.App.ViewModels;

/// <summary>Unit-Tests für TaskDetailViewModel.OeffneArbeitsverzeichnisAsync() und OeffneIdeAsync() bezüglich Nutzung des WorkingDirectoryResolver.</summary>
public sealed class TaskDetailViewModelTests_Arbeitsverzeichnis : TaskDetailViewModelTestsBase
{
    /// <summary>Präfix für über CreateTempDirectory() erzeugte temporäre Verzeichnisse.</summary>
    protected override string TempDirectoryPrefix => "tdvm_arbeitsverzeichnis_tests";

    /// <summary>Bei konfiguriertem Arbeitsverzeichnis wird der über WorkingDirectoryResolver aufgelöste Pfad an ArbeitsverzeichnisOeffnenService übergeben, nicht der Repository-Root.</summary>
    [Fact]
    public async Task OeffneArbeitsverzeichnis_MitKonfiguriertemArbeitsverzeichnis_RuftServiceMitAufgeloestemPfadAuf()
    {
        var arbeitsverzeichnis = CreateTempDirectory();
        Directory.CreateDirectory(Path.Combine(arbeitsverzeichnis, "backend"));
        var workItem = await ErstelleAufgabeMitRepositoryAsync("backend");
        await _aufgabeService.StartenAsync(workItem.Id, "feature/x", arbeitsverzeichnis, ct: TestContext.Current.CancellationToken);

        var prozessStarterMock = new Mock<IProcessStarter>();
        var sut = CreateSut(prozessStarterMock);
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.OeffneArbeitsverzeichnisCommand.Execute(null);

        var erwarteterPfad = Path.GetFullPath(Path.Combine(arbeitsverzeichnis, "backend"));
        prozessStarterMock.Verify(
            p => p.Start(It.Is<ProcessStartRequest>(a => a.Arguments == $"\"{erwarteterPfad}\"")),
            Times.Once);
    }

    /// <summary>Ohne RepositoryStartKonfiguration (bzw. ohne WorkingDirectoryRelativePath) wird weiterhin der Repository-Root (LokalerKlonPfad) verwendet.</summary>
    [Fact]
    public async Task OeffneArbeitsverzeichnis_OhneKonfiguration_RuftServiceMitRepositoryRootAuf()
    {
        var arbeitsverzeichnis = CreateTempDirectory();
        var workItem = await ErstelleAufgabeMitRepositoryAsync(null);
        await _aufgabeService.StartenAsync(workItem.Id, "feature/x", arbeitsverzeichnis, ct: TestContext.Current.CancellationToken);

        var prozessStarterMock = new Mock<IProcessStarter>();
        var sut = CreateSut(prozessStarterMock);
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.OeffneArbeitsverzeichnisCommand.Execute(null);

        prozessStarterMock.Verify(
            p => p.Start(It.Is<ProcessStartRequest>(a => a.Arguments == $"\"{arbeitsverzeichnis}\"")),
            Times.Once);
    }

    /// <summary>Verweist die Startkonfiguration auf ein nicht existierendes Unterverzeichnis, wird eine aussagekräftige FehlerMeldung gesetzt und kein Prozess gestartet.</summary>
    [Fact]
    public async Task OeffneArbeitsverzeichnis_MitUngueltigemArbeitsverzeichnis_ZeigtFehlermeldung()
    {
        var arbeitsverzeichnis = CreateTempDirectory();
        var workItem = await ErstelleAufgabeMitRepositoryAsync("does-not-exist");
        await _aufgabeService.StartenAsync(workItem.Id, "feature/x", arbeitsverzeichnis, ct: TestContext.Current.CancellationToken);

        var prozessStarterMock = new Mock<IProcessStarter>();
        var sut = CreateSut(prozessStarterMock);
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.OeffneArbeitsverzeichnisCommand.Execute(null);

        sut.FehlerMeldung.Should().StartWith("Arbeitsverzeichnis konnte nicht geöffnet werden:");
        prozessStarterMock.Verify(p => p.Start(It.IsAny<ProcessStartRequest>()), Times.Never);
    }

    /// <summary>OeffneIdeAsync findet über das aufgelöste IDE-Plugin (FindEntryPointsAsync) eine Solution im über WorkingDirectoryResolver aufgelösten Arbeitsverzeichnis und öffnet sie, obwohl im Repository-Root keine .sln-Datei liegt.</summary>
    [Fact]
    public async Task OeffneIdeAsync_FindetSolutionImAufgeloestenArbeitsverzeichnis()
    {
        var arbeitsverzeichnis = CreateTempDirectory();
        var unterverzeichnis = Path.Combine(arbeitsverzeichnis, "src", "solutions");
        Directory.CreateDirectory(unterverzeichnis);
        var solutionPfad = Path.Combine(unterverzeichnis, "MyApp.sln");
        File.WriteAllText(solutionPfad, string.Empty);

        var workItem = await ErstelleAufgabeMitRepositoryAsync(Path.Combine("src", "solutions"));
        await _aufgabeService.StartenAsync(workItem.Id, "feature/x", arbeitsverzeichnis, ct: TestContext.Current.CancellationToken);

        var prozessStarterMock = new Mock<IProcessStarter>();
        var sut = CreateSut(prozessStarterMock);
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.KannIdeOeffnen.Should().BeTrue("das konfigurierte Arbeitsverzeichnis existiert");
        sut.OeffneIdeCommand.CanExecute(null).Should().BeTrue();

        await ((AsyncRelayCommand)sut.OeffneIdeCommand).ExecuteAsync();

        prozessStarterMock.Verify(
            p => p.Start(It.Is<ProcessStartRequest>(a => a.FileName == solutionPfad && a.UseShellExecute)),
            Times.Once);
    }
}
