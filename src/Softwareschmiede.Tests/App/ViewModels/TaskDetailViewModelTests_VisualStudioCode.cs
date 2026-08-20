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
        var arbeitsverzeichnis = CreateTempDirectory();
        Directory.CreateDirectory(Path.Combine(arbeitsverzeichnis, "backend"));
        var aufgabe = await ErstelleAufgabeMitRepositoryAsync("backend");
        await _aufgabeService.StartenAsync(aufgabe.Id, "feature/x", arbeitsverzeichnis);

        var prozessStarterMock = new Mock<IProzessStarter>();
        var locator = new TestVisualStudioCodeLocator(new VisualStudioCodeAvailability(true, "code.cmd"));
        var sut = CreateSut(prozessStarterMock, locator);
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        await ((AsyncRelayCommand)sut.OeffneIdeCommand).ExecuteAsync();

        var erwarteterPfad = Path.GetFullPath(Path.Combine(arbeitsverzeichnis, "backend"));
        prozessStarterMock.Verify(
            p => p.Starten(It.Is<ProzessStartAnfrage>(a =>
                a.DateiName == "code.cmd"
                && a.Argumente == $"\"{erwarteterPfad}\"")),
            Times.Once);
    }

    /// <summary>Ohne RepositoryStartKonfiguration wird der Visual-Studio-Code-Fallback weiterhin mit dem Repository-Root (LokalerKlonPfad) gestartet.</summary>
    [Fact]
    public async Task OeffneIdeAsync_OhneSolutionOhneKonfiguration_RuftVsCodeMitRepositoryRootAuf()
    {
        var arbeitsverzeichnis = CreateTempDirectory();
        var aufgabe = await ErstelleAufgabeMitRepositoryAsync(null);
        await _aufgabeService.StartenAsync(aufgabe.Id, "feature/x", arbeitsverzeichnis);

        var prozessStarterMock = new Mock<IProzessStarter>();
        var locator = new TestVisualStudioCodeLocator(new VisualStudioCodeAvailability(true, "code.cmd"));
        var sut = CreateSut(prozessStarterMock, locator);
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        await ((AsyncRelayCommand)sut.OeffneIdeCommand).ExecuteAsync();

        prozessStarterMock.Verify(
            p => p.Starten(It.Is<ProzessStartAnfrage>(a =>
                a.DateiName == "code.cmd"
                && a.Argumente == $"\"{arbeitsverzeichnis}\"")),
            Times.Once);
    }

    /// <summary>Ist Visual Studio Code nicht verfügbar, wird eine aussagekräftige FehlerMeldung gesetzt und kein Prozess gestartet.</summary>
    [Fact]
    public async Task OeffneIdeAsync_OhneSolutionOhneVsCode_ZeigtFehlermeldung()
    {
        var arbeitsverzeichnis = CreateTempDirectory();
        var aufgabe = await ErstelleAufgabeMitRepositoryAsync(null);
        await _aufgabeService.StartenAsync(aufgabe.Id, "feature/x", arbeitsverzeichnis);

        var prozessStarterMock = new Mock<IProzessStarter>();
        var locator = new TestVisualStudioCodeLocator(VisualStudioCodeAvailability.NotAvailable);
        var sut = CreateSut(prozessStarterMock, locator);
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        await ((AsyncRelayCommand)sut.OeffneIdeCommand).ExecuteAsync();

        sut.FehlerMeldung.Should().Contain("Visual Studio Code");
        prozessStarterMock.Verify(p => p.Starten(It.IsAny<ProzessStartAnfrage>()), Times.Never);
    }

    /// <summary>Ohne Solutions im aufgelösten (konfigurierten) Arbeitsverzeichnis fällt OeffneIdeAsync auf Visual Studio Code mit dem aufgelösten Verzeichnis zurück, selbst wenn im Repository-Root eine .sln-Datei liegt.</summary>
    [Fact]
    public async Task OeffneIdeAsync_OhneLoesungenImArbeitsverzeichnis_FaelltZuVsCodeZurueck()
    {
        var arbeitsverzeichnis = CreateTempDirectory();
        // .sln im Repository-Root anlegen: darf NICHT gefunden werden, da nur das konfigurierte Unterverzeichnis durchsucht werden darf.
        File.WriteAllText(Path.Combine(arbeitsverzeichnis, "RootLoesung.sln"), string.Empty);
        var unterverzeichnis = Path.Combine(arbeitsverzeichnis, "backend");
        Directory.CreateDirectory(unterverzeichnis);

        var aufgabe = await ErstelleAufgabeMitRepositoryAsync("backend");
        await _aufgabeService.StartenAsync(aufgabe.Id, "feature/x", arbeitsverzeichnis);

        var prozessStarterMock = new Mock<IProzessStarter>();
        var locator = new TestVisualStudioCodeLocator(new VisualStudioCodeAvailability(true, "code.cmd"));
        var sut = CreateSut(prozessStarterMock, locator);
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        await ((AsyncRelayCommand)sut.OeffneIdeCommand).ExecuteAsync();

        var erwarteterPfad = Path.GetFullPath(unterverzeichnis);
        prozessStarterMock.Verify(
            p => p.Starten(It.Is<ProzessStartAnfrage>(a =>
                a.DateiName == "code.cmd"
                && a.Argumente == $"\"{erwarteterPfad}\"")),
            Times.Once);
    }
}
