using FluentAssertions;
using Moq;
using Softwareschmiede.App.Services;
using Softwareschmiede.App.ViewModels;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.App.ViewModels;

/// <summary>
/// Unit-Tests für TaskDetailViewModel.OeffneIdeAsync() bezüglich des automatischen Visual-Studio-Code-Fallbacks
/// (via PluginSelectionService.ResolveIdePluginAsync, wenn kein Visual-Studio-Plugin explizit kompatibel ist)
/// und der Nutzung des WorkingDirectoryResolver.
/// </summary>
public sealed class TaskDetailViewModelTests_VisualStudioCode : TaskDetailViewModelTestsBase
{
    /// <summary>Präfix für über CreateTempDirectory() erzeugte temporäre Verzeichnisse.</summary>
    protected override string TempDirectoryPrefix => "tdvm_vscode_tests";

    /// <summary>Bei konfiguriertem Arbeitsverzeichnis wird der (standardmäßig aktive) Visual-Studio-Code-Fallback mit dem über WorkingDirectoryResolver aufgelösten Pfad gestartet, nicht mit dem Repository-Root.</summary>
    [Fact]
    public async Task OeffneIdeAsync_OhneSolutionMitKonfiguriertemArbeitsverzeichnis_RuftVsCodeMitAufgeloestemPfadAuf()
    {
        var workingDirectory = CreateTempDirectory();
        Directory.CreateDirectory(Path.Combine(workingDirectory, "backend"));
        var workItem = await ErstelleAufgabeMitRepositoryAsync("backend");
        await _aufgabeService.StartenAsync(workItem.Id, "feature/x", workingDirectory, ct: TestContext.Current.CancellationToken);

        var prozessStarterMock = new Mock<IProcessStarter>();
        var locator = new TestVisualStudioCodeLocator(new VisualStudioCodeAvailability(true, "code.cmd"));
        var sut = CreateSut(prozessStarterMock, locator);
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        await ((AsyncRelayCommand)sut.OeffneIdeCommand).ExecuteAsync();

        var erwarteterPfad = Path.GetFullPath(Path.Combine(workingDirectory, "backend"));
        prozessStarterMock.Verify(
            p => p.Start(It.Is<ProcessStartRequest>(a =>
                a.FileName == "code.cmd"
                && a.Arguments == $"\"{erwarteterPfad}\"")),
            Times.Once);
    }

    /// <summary>Ohne RepositoryStartKonfiguration wird der Visual-Studio-Code-Fallback weiterhin mit dem Repository-Root (LokalerKlonPfad) gestartet.</summary>
    [Fact]
    public async Task OeffneIdeAsync_OhneSolutionOhneKonfiguration_RuftVsCodeMitRepositoryRootAuf()
    {
        var workingDirectory = CreateTempDirectory();
        var workItem = await ErstelleAufgabeMitRepositoryAsync(null);
        await _aufgabeService.StartenAsync(workItem.Id, "feature/x", workingDirectory, ct: TestContext.Current.CancellationToken);

        var prozessStarterMock = new Mock<IProcessStarter>();
        var locator = new TestVisualStudioCodeLocator(new VisualStudioCodeAvailability(true, "code.cmd"));
        var sut = CreateSut(prozessStarterMock, locator);
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        await ((AsyncRelayCommand)sut.OeffneIdeCommand).ExecuteAsync();

        prozessStarterMock.Verify(
            p => p.Start(It.Is<ProcessStartRequest>(a =>
                a.FileName == "code.cmd"
                && a.Arguments == $"\"{workingDirectory}\"")),
            Times.Once);
    }

    /// <summary>Ist Visual Studio Code nicht verfügbar, wird eine aussagekräftige FehlerMeldung gesetzt und kein Prozess gestartet.</summary>
    [Fact]
    public async Task OeffneIdeAsync_OhneSolutionOhneVsCode_ZeigtFehlermeldung()
    {
        var workingDirectory = CreateTempDirectory();
        var workItem = await ErstelleAufgabeMitRepositoryAsync(null);
        await _aufgabeService.StartenAsync(workItem.Id, "feature/x", workingDirectory, ct: TestContext.Current.CancellationToken);

        var prozessStarterMock = new Mock<IProcessStarter>();
        var locator = new TestVisualStudioCodeLocator(VisualStudioCodeAvailability.NotAvailable);
        var sut = CreateSut(prozessStarterMock, locator);
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        await ((AsyncRelayCommand)sut.OeffneIdeCommand).ExecuteAsync();

        sut.FehlerMeldung.Should().Contain("Visual Studio Code");
        prozessStarterMock.Verify(p => p.Start(It.IsAny<ProcessStartRequest>()), Times.Never);
    }

    /// <summary>Ohne Solutions im aufgelösten (konfigurierten) Arbeitsverzeichnis fällt OeffneIdeAsync auf Visual Studio Code mit dem aufgelösten Verzeichnis zurück, selbst wenn im Repository-Root eine .sln-Datei liegt.</summary>
    [Fact]
    public async Task OeffneIdeAsync_OhneLoesungenImArbeitsverzeichnis_FaelltZuVsCodeZurueck()
    {
        var workingDirectory = CreateTempDirectory();
        // .sln im Repository-Root anlegen: darf NICHT gefunden werden, da nur das konfigurierte Unterverzeichnis durchsucht werden darf.
        File.WriteAllText(Path.Combine(workingDirectory, "RootLoesung.sln"), string.Empty);
        var unterverzeichnis = Path.Combine(workingDirectory, "backend");
        Directory.CreateDirectory(unterverzeichnis);

        var workItem = await ErstelleAufgabeMitRepositoryAsync("backend");
        await _aufgabeService.StartenAsync(workItem.Id, "feature/x", workingDirectory, ct: TestContext.Current.CancellationToken);

        var prozessStarterMock = new Mock<IProcessStarter>();
        var locator = new TestVisualStudioCodeLocator(new VisualStudioCodeAvailability(true, "code.cmd"));
        var sut = CreateSut(prozessStarterMock, locator);
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        await ((AsyncRelayCommand)sut.OeffneIdeCommand).ExecuteAsync();

        var erwarteterPfad = Path.GetFullPath(unterverzeichnis);
        prozessStarterMock.Verify(
            p => p.Start(It.Is<ProcessStartRequest>(a =>
                a.FileName == "code.cmd"
                && a.Arguments == $"\"{erwarteterPfad}\"")),
            Times.Once);
    }
}
