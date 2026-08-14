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
        var aufgabe = await ErstelleAufgabeMitRepositoryAsync("backend");
        await _aufgabeService.StartenAsync(aufgabe.Id, "feature/x", arbeitsverzeichnis);

        var prozessStarterMock = new Mock<IProzessStarter>();
        var sut = CreateSut(prozessStarterMock);
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.OeffneArbeitsverzeichnisCommand.Execute(null);

        var erwarteterPfad = Path.GetFullPath(Path.Combine(arbeitsverzeichnis, "backend"));
        prozessStarterMock.Verify(
            p => p.Starten(It.Is<ProzessStartAnfrage>(a => a.Argumente == $"\"{erwarteterPfad}\"")),
            Times.Once);
    }

    /// <summary>Ohne RepositoryStartKonfiguration (bzw. ohne WorkingDirectoryRelativePath) wird weiterhin der Repository-Root (LokalerKlonPfad) verwendet.</summary>
    [Fact]
    public async Task OeffneArbeitsverzeichnis_OhneKonfiguration_RuftServiceMitRepositoryRootAuf()
    {
        var arbeitsverzeichnis = CreateTempDirectory();
        var aufgabe = await ErstelleAufgabeMitRepositoryAsync(null);
        await _aufgabeService.StartenAsync(aufgabe.Id, "feature/x", arbeitsverzeichnis);

        var prozessStarterMock = new Mock<IProzessStarter>();
        var sut = CreateSut(prozessStarterMock);
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.OeffneArbeitsverzeichnisCommand.Execute(null);

        prozessStarterMock.Verify(
            p => p.Starten(It.Is<ProzessStartAnfrage>(a => a.Argumente == $"\"{arbeitsverzeichnis}\"")),
            Times.Once);
    }

    /// <summary>Verweist die Startkonfiguration auf ein nicht existierendes Unterverzeichnis, wird eine aussagekräftige FehlerMeldung gesetzt und kein Prozess gestartet.</summary>
    [Fact]
    public async Task OeffneArbeitsverzeichnis_MitUngueltigemArbeitsverzeichnis_ZeigtFehlermeldung()
    {
        var arbeitsverzeichnis = CreateTempDirectory();
        var aufgabe = await ErstelleAufgabeMitRepositoryAsync("does-not-exist");
        await _aufgabeService.StartenAsync(aufgabe.Id, "feature/x", arbeitsverzeichnis);

        var prozessStarterMock = new Mock<IProzessStarter>();
        var sut = CreateSut(prozessStarterMock);
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.OeffneArbeitsverzeichnisCommand.Execute(null);

        sut.FehlerMeldung.Should().StartWith("Arbeitsverzeichnis konnte nicht geöffnet werden:");
        prozessStarterMock.Verify(p => p.Starten(It.IsAny<ProzessStartAnfrage>()), Times.Never);
    }

    /// <summary>OeffneIdeAsync ruft IdeOeffnenService.FindeSolutions() mit dem über WorkingDirectoryResolver aufgelösten Arbeitsverzeichnis auf und öffnet eine dort gefundene Solution, obwohl im Repository-Root keine .sln-Datei liegt.</summary>
    [Fact]
    public async Task OeffneIdeAsync_FindetSolutionImAufgeloestenArbeitsverzeichnis()
    {
        var arbeitsverzeichnis = CreateTempDirectory();
        var unterverzeichnis = Path.Combine(arbeitsverzeichnis, "src", "solutions");
        Directory.CreateDirectory(unterverzeichnis);
        var solutionPfad = Path.Combine(unterverzeichnis, "MyApp.sln");
        File.WriteAllText(solutionPfad, string.Empty);

        var aufgabe = await ErstelleAufgabeMitRepositoryAsync(Path.Combine("src", "solutions"));
        await _aufgabeService.StartenAsync(aufgabe.Id, "feature/x", arbeitsverzeichnis);

        var prozessStarterMock = new Mock<IProzessStarter>();
        var sut = CreateSut(prozessStarterMock);
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.KannIdeOeffnen.Should().BeTrue("das konfigurierte Arbeitsverzeichnis existiert");
        sut.OeffneIdeCommand.CanExecute(null).Should().BeTrue();

        await ((AsyncRelayCommand)sut.OeffneIdeCommand).ExecuteAsync();

        prozessStarterMock.Verify(
            p => p.Starten(It.Is<ProzessStartAnfrage>(a => a.DateiName == solutionPfad && a.ShellAusfuehren)),
            Times.Once);
    }
}
