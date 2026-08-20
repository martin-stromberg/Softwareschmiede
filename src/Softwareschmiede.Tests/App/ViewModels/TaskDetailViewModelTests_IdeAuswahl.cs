using FluentAssertions;
using Moq;
using Softwareschmiede.App.Services;
using Softwareschmiede.App.ViewModels;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.PluginImpl;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.App.ViewModels;

/// <summary>
/// Unit-Tests für das Split-Button-Verhalten der IDE-Öffnen-Funktion in TaskDetailViewModel:
/// <see cref="TaskDetailViewModel.OeffneIdeAuswahlCommand"/>, <see cref="TaskDetailViewModel.KannIdeAuswaehlen"/>
/// und den waehleEntryPointAsync-Callback.
/// </summary>
public sealed class TaskDetailViewModelTests_IdeAuswahl : TaskDetailViewModelTestsBase
{
    /// <summary>Präfix für über CreateTempDirectory() erzeugte temporäre Verzeichnisse.</summary>
    protected override string TempDirectoryPrefix => "tdvm_ide_auswahl_tests";

    /// <summary>Bei mehreren Solutions ruft OeffneIdeAuswahlCommand OeffneIdeAuswahlAsync auf, das - anders als OeffneIdeCommand - den Auswahl-Dialog anzeigt.</summary>
    [Fact]
    public async Task OeffneIdeAuswahlCommand_ExecuteAsync_RuftOeffneIdeAuswahlAsyncAuf()
    {
        var arbeitsverzeichnis = CreateTempDirectory();
        var ersteSolution = Path.Combine(arbeitsverzeichnis, "Erste.sln");
        var zweiteSolution = Path.Combine(arbeitsverzeichnis, "Zweite.sln");
        File.WriteAllText(ersteSolution, string.Empty);
        File.WriteAllText(zweiteSolution, string.Empty);
        var aufgabe = await ErstelleAufgabeMitRepositoryAsync(null);
        await _aufgabeService.StartenAsync(aufgabe.Id, "feature/x", arbeitsverzeichnis);

        _dialogServiceMock
            .Setup(d => d.ShowSolutionSelectionDialogAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Visual Studio: Zweite.sln");

        var prozessStarterMock = new Mock<IProzessStarter>();
        var sut = CreateSut(prozessStarterMock);
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        await ((AsyncRelayCommand)sut.OeffneIdeAuswahlCommand).ExecuteAsync();

        _dialogServiceMock.Verify(
            d => d.ShowSolutionSelectionDialogAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()),
            Times.Once);
        prozessStarterMock.Verify(
            p => p.Starten(It.Is<ProzessStartAnfrage>(a => a.DateiName == zweiteSolution && a.ShellAusfuehren)),
            Times.Once);
    }

    /// <summary>Ohne vorhandenes Arbeitsverzeichnis (KannIdeOeffnen == false) kann OeffneIdeAuswahlCommand nicht ausgeführt werden.</summary>
    [Fact]
    public async Task OeffneIdeAuswahlCommand_CanExecute_WhenKannIdeOeffnenFalse_ReturnsFalse()
    {
        var aufgabe = await ErstelleAufgabeMitRepositoryAsync(null);
        var sut = CreateSut();
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.KannIdeOeffnen.Should().BeFalse("ohne LokalerKlonPfad existiert noch kein Arbeitsverzeichnis");
        sut.OeffneIdeAuswahlCommand.CanExecute(null).Should().BeFalse();
    }

    /// <summary>
    /// Bei genau einem gefundenen Einstiegspunkt bleibt KannIdeAuswaehlen false (Dropdown-Button unsichtbar).
    /// Isoliert über idePlugins-Override auf nur Visual Studio, da CreateSut() standardmäßig zusätzlich
    /// Visual Studio Code aktiviert, das für jedes Repository einen weiteren Fallback-Eintrag liefert - mit
    /// der Multi-Plugin-Aggregation würde die Gesamtanzahl sonst auf 2 steigen.
    /// </summary>
    [Fact]
    public async Task KannIdeAuswaehlen_WhenOneEntryPoint_ReturnsFalse()
    {
        var arbeitsverzeichnis = CreateTempDirectory();
        File.WriteAllText(Path.Combine(arbeitsverzeichnis, "Einzige.sln"), string.Empty);
        var aufgabe = await ErstelleAufgabeMitRepositoryAsync(null);
        await _aufgabeService.StartenAsync(aufgabe.Id, "feature/x", arbeitsverzeichnis);

        var visualStudioPlugin = new VisualStudioIdePlugin(new Mock<IProzessStarter>().Object);
        var sut = CreateSut(idePlugins: [visualStudioPlugin]);
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        await ((AsyncRelayCommand)sut.OeffneIdeCommand).ExecuteAsync();

        sut.KannIdeAuswaehlen.Should().BeFalse();
    }

    /// <summary>Bei mehreren gefundenen Einstiegspunkten wird KannIdeAuswaehlen true (Dropdown-Button sichtbar).</summary>
    [Fact]
    public async Task KannIdeAuswaehlen_WhenMultipleEntryPoints_ReturnsTrue()
    {
        var arbeitsverzeichnis = CreateTempDirectory();
        File.WriteAllText(Path.Combine(arbeitsverzeichnis, "Erste.sln"), string.Empty);
        File.WriteAllText(Path.Combine(arbeitsverzeichnis, "Zweite.sln"), string.Empty);
        var aufgabe = await ErstelleAufgabeMitRepositoryAsync(null);
        await _aufgabeService.StartenAsync(aufgabe.Id, "feature/x", arbeitsverzeichnis);

        var sut = CreateSut();
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        await ((AsyncRelayCommand)sut.OeffneIdeCommand).ExecuteAsync();

        sut.KannIdeAuswaehlen.Should().BeTrue();
    }

    /// <summary>
    /// Regressionstest: KannIdeAuswaehlen wird bereits am Ende von LadenAsync einmalig berechnet, ohne dass
    /// zuvor OeffneIdeCommand/OeffneIdeAuswahlCommand ausgeführt werden muss - der Dropdown-Button des
    /// Split-Buttons ist damit beim ersten Anzeigen der View korrekt sichtbar, statt (wie vor dem Fix) immer
    /// mit KannIdeAuswaehlen == false zu starten. Kein Einstiegspunkt wird dabei geöffnet.
    /// </summary>
    [Fact]
    public async Task KannIdeAuswaehlen_NachLadenAsync_WhenMultipleEntryPoints_ReturnsTrueOhneOeffnen()
    {
        var arbeitsverzeichnis = CreateTempDirectory();
        File.WriteAllText(Path.Combine(arbeitsverzeichnis, "Erste.sln"), string.Empty);
        File.WriteAllText(Path.Combine(arbeitsverzeichnis, "Zweite.sln"), string.Empty);
        var aufgabe = await ErstelleAufgabeMitRepositoryAsync(null);
        await _aufgabeService.StartenAsync(aufgabe.Id, "feature/x", arbeitsverzeichnis);

        var prozessStarterMock = new Mock<IProzessStarter>();
        var sut = CreateSut(prozessStarterMock);
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.KannIdeAuswaehlen.Should().BeTrue();
        prozessStarterMock.Verify(p => p.Starten(It.IsAny<ProzessStartAnfrage>()), Times.Never);
    }

    /// <summary>
    /// Regressionstest: Bei genau einem Einstiegspunkt bleibt KannIdeAuswaehlen bereits nach LadenAsync false,
    /// ohne dass ein Öffnen-Versuch nötig ist. Isoliert über idePlugins-Override auf nur Visual Studio (siehe
    /// Begründung bei KannIdeAuswaehlen_WhenOneEntryPoint_ReturnsFalse).
    /// </summary>
    [Fact]
    public async Task KannIdeAuswaehlen_NachLadenAsync_WhenOneEntryPoint_ReturnsFalse()
    {
        var arbeitsverzeichnis = CreateTempDirectory();
        File.WriteAllText(Path.Combine(arbeitsverzeichnis, "Einzige.sln"), string.Empty);
        var aufgabe = await ErstelleAufgabeMitRepositoryAsync(null);
        await _aufgabeService.StartenAsync(aufgabe.Id, "feature/x", arbeitsverzeichnis);

        var visualStudioPlugin = new VisualStudioIdePlugin(new Mock<IProzessStarter>().Object);
        var sut = CreateSut(idePlugins: [visualStudioPlugin]);
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.KannIdeAuswaehlen.Should().BeFalse();
    }

    /// <summary>Regressionstest: Ohne gefundene Einstiegspunkte bleibt KannIdeAuswaehlen bereits nach LadenAsync false, ohne dass dabei eine FehlerMeldung angezeigt wird (das Laden der Aufgabe selbst war erfolgreich).</summary>
    [Fact]
    public async Task KannIdeAuswaehlen_NachLadenAsync_WhenNoEntryPoints_ReturnsFalseOhneFehlerMeldung()
    {
        var arbeitsverzeichnis = CreateTempDirectory();
        var aufgabe = await ErstelleAufgabeMitRepositoryAsync(null);
        await _aufgabeService.StartenAsync(aufgabe.Id, "feature/x", arbeitsverzeichnis);

        var sut = CreateSut(visualStudioCodeLocator: new TestVisualStudioCodeLocator(VisualStudioCodeAvailability.NotAvailable));
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.KannIdeAuswaehlen.Should().BeFalse();
        sut.FehlerMeldung.Should().BeNullOrEmpty("ein Ermittlungsfehler beim Laden darf nicht als FehlerMeldung angezeigt werden");
    }

    /// <summary>Ohne gefundene Einstiegspunkte (kein .sln, Visual Studio Code nicht verfügbar) bleibt KannIdeAuswaehlen false und eine Fehlermeldung wird gesetzt.</summary>
    [Fact]
    public async Task KannIdeAuswaehlen_WhenNoEntryPoints_ReturnsFalse()
    {
        var arbeitsverzeichnis = CreateTempDirectory();
        var aufgabe = await ErstelleAufgabeMitRepositoryAsync(null);
        await _aufgabeService.StartenAsync(aufgabe.Id, "feature/x", arbeitsverzeichnis);

        var sut = CreateSut(visualStudioCodeLocator: new TestVisualStudioCodeLocator(VisualStudioCodeAvailability.NotAvailable));
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        await ((AsyncRelayCommand)sut.OeffneIdeCommand).ExecuteAsync();

        sut.KannIdeAuswaehlen.Should().BeFalse();
        sut.FehlerMeldung.Should().NotBeNullOrEmpty();
    }

    /// <summary>Bei mehreren Einstiegspunkten zeigt der waehleEntryPointAsync-Callback (über OeffneIdeAuswahlCommand) den Auswahl-Dialog an und öffnet den dort gewählten Einstiegspunkt.</summary>
    [Fact]
    public async Task WaehleEntryPointAsync_WithMultipleEntryPoints_ShowsDialogAndReturnsSelected()
    {
        var arbeitsverzeichnis = CreateTempDirectory();
        var ersteSolution = Path.Combine(arbeitsverzeichnis, "Erste.sln");
        var zweiteSolution = Path.Combine(arbeitsverzeichnis, "Zweite.sln");
        File.WriteAllText(ersteSolution, string.Empty);
        File.WriteAllText(zweiteSolution, string.Empty);
        var aufgabe = await ErstelleAufgabeMitRepositoryAsync(null);
        await _aufgabeService.StartenAsync(aufgabe.Id, "feature/x", arbeitsverzeichnis);

        _dialogServiceMock
            .Setup(d => d.ShowSolutionSelectionDialogAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Visual Studio: Zweite.sln");

        var prozessStarterMock = new Mock<IProzessStarter>();
        var sut = CreateSut(prozessStarterMock);
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        await ((AsyncRelayCommand)sut.OeffneIdeAuswahlCommand).ExecuteAsync();

        _dialogServiceMock.Verify(
            d => d.ShowSolutionSelectionDialogAsync(
                It.Is<IReadOnlyList<string>>(liste => liste.Contains("Visual Studio: Erste.sln") && liste.Contains("Visual Studio: Zweite.sln")),
                It.IsAny<CancellationToken>()),
            Times.Once);
        prozessStarterMock.Verify(
            p => p.Starten(It.Is<ProzessStartAnfrage>(a => a.DateiName == zweiteSolution && a.ShellAusfuehren)),
            Times.Once);
    }

    /// <summary>Bricht der Anwender den Auswahl-Dialog ab (Rückgabe null), öffnet der waehleEntryPointAsync-Callback nichts.</summary>
    [Fact]
    public async Task WaehleEntryPointAsync_WithDialogAbort_ReturnsNull()
    {
        var arbeitsverzeichnis = CreateTempDirectory();
        File.WriteAllText(Path.Combine(arbeitsverzeichnis, "Erste.sln"), string.Empty);
        File.WriteAllText(Path.Combine(arbeitsverzeichnis, "Zweite.sln"), string.Empty);
        var aufgabe = await ErstelleAufgabeMitRepositoryAsync(null);
        await _aufgabeService.StartenAsync(aufgabe.Id, "feature/x", arbeitsverzeichnis);

        _dialogServiceMock
            .Setup(d => d.ShowSolutionSelectionDialogAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var prozessStarterMock = new Mock<IProzessStarter>();
        var sut = CreateSut(prozessStarterMock);
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        await ((AsyncRelayCommand)sut.OeffneIdeAuswahlCommand).ExecuteAsync();

        prozessStarterMock.Verify(p => p.Starten(It.IsAny<ProzessStartAnfrage>()), Times.Never);
        sut.FehlerMeldung.Should().BeNullOrEmpty("ein Abbruch des Auswahl-Dialogs ist kein Fehler");
    }

    /// <summary>Der waehleEntryPointAsync-Callback nutzt IdeEntryPoint.DisplayName (statt Path) für die Anzeige im Auswahl-Dialog, sofern gesetzt.</summary>
    [Fact]
    public async Task WaehleEntryPointAsync_UsesDisplayNameInDialog()
    {
        var arbeitsverzeichnis = CreateTempDirectory();
        var ersterEntryPoint = ErzeugeEntryPointMitDisplayName(Path.Combine(arbeitsverzeichnis, "erste.sln"), "Erste Solution");
        var zweiterEntryPoint = ErzeugeEntryPointMitDisplayName(Path.Combine(arbeitsverzeichnis, "zweite.sln"), "Zweite Solution");
        var idePluginMock = CreateTestIdePluginMock([ersterEntryPoint, zweiterEntryPoint]);
        idePluginMock.Setup(p => p.OpenEntryPointAsync(It.IsAny<IdeEntryPoint>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var aufgabe = await ErstelleAufgabeMitRepositoryAsync(null);
        await _aufgabeService.StartenAsync(aufgabe.Id, "feature/x", arbeitsverzeichnis);

        _dialogServiceMock
            .Setup(d => d.ShowSolutionSelectionDialogAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync($"Test-IDE: {zweiterEntryPoint.DisplayName}");

        var sut = CreateSut(idePlugins: [idePluginMock.Object]);
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        await ((AsyncRelayCommand)sut.OeffneIdeAuswahlCommand).ExecuteAsync();

        _dialogServiceMock.Verify(
            d => d.ShowSolutionSelectionDialogAsync(
                It.Is<IReadOnlyList<string>>(liste => liste.Contains("Test-IDE: Erste Solution") && liste.Contains("Test-IDE: Zweite Solution")),
                It.IsAny<CancellationToken>()),
            Times.Once);
        idePluginMock.Verify(p => p.OpenEntryPointAsync(zweiterEntryPoint, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Regressionstest: Schlägt OpenEntryPointAsync trotz mehrerer gefundener Einstiegspunkte fehl (z. B. IDE-Prozess
    /// kann nicht gestartet werden), bleibt KannIdeAuswaehlen weiterhin true - der Dropdown-Button des Split-Buttons
    /// darf nach einem fehlgeschlagenen Öffnen-Versuch nicht verschwinden, da der Anwender genau jetzt über den
    /// Dropdown einen anderen Einstiegspunkt probieren könnte.
    /// </summary>
    [Fact]
    public async Task KannIdeAuswaehlen_WhenOpenEntryPointFailsWithMultipleEntryPoints_BleibtTrue()
    {
        var arbeitsverzeichnis = CreateTempDirectory();
        var ersterEntryPoint = ErzeugeEntryPointMitDisplayName(Path.Combine(arbeitsverzeichnis, "erste.sln"), "Erste Solution");
        var zweiterEntryPoint = ErzeugeEntryPointMitDisplayName(Path.Combine(arbeitsverzeichnis, "zweite.sln"), "Zweite Solution");
        var idePluginMock = CreateTestIdePluginMock([ersterEntryPoint, zweiterEntryPoint]);
        idePluginMock.Setup(p => p.OpenEntryPointAsync(It.IsAny<IdeEntryPoint>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("IDE-Prozess konnte nicht gestartet werden."));

        var aufgabe = await ErstelleAufgabeMitRepositoryAsync(null);
        await _aufgabeService.StartenAsync(aufgabe.Id, "feature/x", arbeitsverzeichnis);

        var sut = CreateSut(idePlugins: [idePluginMock.Object]);
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        await ((AsyncRelayCommand)sut.OeffneIdeCommand).ExecuteAsync();

        sut.FehlerMeldung.Should().NotBeNullOrEmpty();
        sut.KannIdeAuswaehlen.Should().BeTrue("die Anzahl der gefundenen Einstiegspunkte hat sich durch den Öffnen-Fehler nicht geändert");
    }

    /// <summary>Ohne gefundene Einstiegspunkte zeigt OeffneIdeAuswahlAsync dieselbe Fehlermeldung wie OeffneIdeAsync.</summary>
    [Fact]
    public async Task OeffneIdeAuswahlAsync_WithNoEntryPoints_ShowsError()
    {
        var arbeitsverzeichnis = CreateTempDirectory();
        var aufgabe = await ErstelleAufgabeMitRepositoryAsync(null);
        await _aufgabeService.StartenAsync(aufgabe.Id, "feature/x", arbeitsverzeichnis);

        var prozessStarterMock = new Mock<IProzessStarter>();
        var sut = CreateSut(
            prozessStarterMock,
            visualStudioCodeLocator: new TestVisualStudioCodeLocator(VisualStudioCodeAvailability.NotAvailable));
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        await ((AsyncRelayCommand)sut.OeffneIdeAuswahlCommand).ExecuteAsync();

        sut.FehlerMeldung.Should().NotBeNullOrEmpty();
        prozessStarterMock.Verify(p => p.Starten(It.IsAny<ProzessStartAnfrage>()), Times.Never);
    }

    /// <summary>
    /// Bei aktiviertem VS (mit .sln) und VS Code (Fallback) enthält die an ShowSolutionSelectionDialogAsync
    /// übergebene Liste sowohl den formatierten VS-Eintrag als auch "Visual Studio Code" - die Aggregation
    /// erfasst also Einstiegspunkte beider kompatiblen Plugins, nicht nur des einen priorisierten.
    /// </summary>
    [Fact]
    public async Task WaehleEntryPointAsync_WithEntryPointsFromTwoPlugins_ShowsBothInDialog()
    {
        var arbeitsverzeichnis = CreateTempDirectory();
        File.WriteAllText(Path.Combine(arbeitsverzeichnis, "MyProject.sln"), string.Empty);
        var aufgabe = await ErstelleAufgabeMitRepositoryAsync(null);
        await _aufgabeService.StartenAsync(aufgabe.Id, "feature/x", arbeitsverzeichnis);

        _dialogServiceMock
            .Setup(d => d.ShowSolutionSelectionDialogAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var sut = CreateSut();
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        await ((AsyncRelayCommand)sut.OeffneIdeAuswahlCommand).ExecuteAsync();

        _dialogServiceMock.Verify(
            d => d.ShowSolutionSelectionDialogAsync(
                It.Is<IReadOnlyList<string>>(liste => liste.Contains("Visual Studio: MyProject.sln") && liste.Contains("Visual Studio Code")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Wählt der Anwender den VS-Code-Eintrag aus dem Dialog (obwohl VS als Explicit-Plugin von
    /// ResolveIdePluginAsync bevorzugt würde), wird OpenEntryPointAsync auf dem VS-Code-Plugin aufgerufen,
    /// nicht auf VS.
    /// </summary>
    [Fact]
    public async Task WaehleEntryPointAsync_SelectingEntryFromFallbackPlugin_OpensViaThatPlugin_NotViaResolvedPlugin()
    {
        var arbeitsverzeichnis = CreateTempDirectory();
        File.WriteAllText(Path.Combine(arbeitsverzeichnis, "MyProject.sln"), string.Empty);
        var aufgabe = await ErstelleAufgabeMitRepositoryAsync(null);
        await _aufgabeService.StartenAsync(aufgabe.Id, "feature/x", arbeitsverzeichnis);

        _dialogServiceMock
            .Setup(d => d.ShowSolutionSelectionDialogAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Visual Studio Code");

        var prozessStarterMock = new Mock<IProzessStarter>();
        var locator = new TestVisualStudioCodeLocator(new VisualStudioCodeAvailability(true, "code.cmd"));
        var sut = CreateSut(prozessStarterMock, locator);
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        await ((AsyncRelayCommand)sut.OeffneIdeAuswahlCommand).ExecuteAsync();

        prozessStarterMock.Verify(
            p => p.Starten(It.Is<ProzessStartAnfrage>(a => a.DateiName == "code.cmd" && !a.ShellAusfuehren)),
            Times.Once);
        prozessStarterMock.Verify(
            p => p.Starten(It.Is<ProzessStartAnfrage>(a => a.ShellAusfuehren)),
            Times.Never);
    }

    /// <summary>Die an den Dialog übergebene Liste ordnet Einträge kompatibler Explicit-Plugins vor Einträgen von Fallback-Plugins - unabhängig von der Registrierungsreihenfolge der Plugins.</summary>
    [Fact]
    public async Task WaehleEntryPointAsync_OrdersExplicitPluginEntriesBeforeFallbackPluginEntries()
    {
        var arbeitsverzeichnis = CreateTempDirectory();
        File.WriteAllText(Path.Combine(arbeitsverzeichnis, "MyProject.sln"), string.Empty);
        var aufgabe = await ErstelleAufgabeMitRepositoryAsync(null);
        await _aufgabeService.StartenAsync(aufgabe.Id, "feature/x", arbeitsverzeichnis);

        var prozessStarterMock = new Mock<IProzessStarter>();
        var visualStudioCodePlugin = new VisualStudioCodeIdePlugin(
            prozessStarterMock.Object,
            new TestVisualStudioCodeLocator(VisualStudioCodeAvailability.NotAvailable));
        var visualStudioPlugin = new VisualStudioIdePlugin(prozessStarterMock.Object);

        IReadOnlyList<string>? angezeigteWerte = null;
        _dialogServiceMock
            .Setup(d => d.ShowSolutionSelectionDialogAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<string>, CancellationToken>((liste, _) => angezeigteWerte = liste)
            .ReturnsAsync((string?)null);

        // VS Code (Fallback) wird zuerst registriert, VS (Explicit) danach - die Rückgabeliste muss trotzdem Explicit-vor-Fallback ordnen.
        var sut = CreateSut(prozessStarterMock, idePlugins: [visualStudioCodePlugin, visualStudioPlugin]);
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        await ((AsyncRelayCommand)sut.OeffneIdeAuswahlCommand).ExecuteAsync();

        angezeigteWerte.Should().NotBeNull();
        var werteListe = angezeigteWerte!.ToList();
        werteListe.IndexOf("Visual Studio: MyProject.sln").Should().BeLessThan(werteListe.IndexOf("Visual Studio Code"));
    }

    /// <summary>Für einen VS-Einstiegspunkt (DisplayName == null) liefert die Formatierung "Visual Studio: {Dateiname}".</summary>
    [Fact]
    public async Task FormatiereAnzeigeWert_ForVisualStudioEntryPoint_UsesPluginNamePrefixAndFileName()
    {
        var arbeitsverzeichnis = CreateTempDirectory();
        File.WriteAllText(Path.Combine(arbeitsverzeichnis, "MyProject.sln"), string.Empty);
        var aufgabe = await ErstelleAufgabeMitRepositoryAsync(null);
        await _aufgabeService.StartenAsync(aufgabe.Id, "feature/x", arbeitsverzeichnis);

        IReadOnlyList<string>? angezeigteWerte = null;
        _dialogServiceMock
            .Setup(d => d.ShowSolutionSelectionDialogAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<string>, CancellationToken>((liste, _) => angezeigteWerte = liste)
            .ReturnsAsync((string?)null);

        var sut = CreateSut();
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        await ((AsyncRelayCommand)sut.OeffneIdeAuswahlCommand).ExecuteAsync();

        angezeigteWerte.Should().Contain("Visual Studio: MyProject.sln");
    }

    /// <summary>Für den VS-Code-Einstiegspunkt (DisplayName == PluginName) liefert die Formatierung nur "Visual Studio Code" ohne Doppelung.</summary>
    [Fact]
    public async Task FormatiereAnzeigeWert_ForVisualStudioCodeEntryPoint_UsesPluginNameOnly()
    {
        var arbeitsverzeichnis = CreateTempDirectory();
        File.WriteAllText(Path.Combine(arbeitsverzeichnis, "MyProject.sln"), string.Empty);
        var aufgabe = await ErstelleAufgabeMitRepositoryAsync(null);
        await _aufgabeService.StartenAsync(aufgabe.Id, "feature/x", arbeitsverzeichnis);

        IReadOnlyList<string>? angezeigteWerte = null;
        _dialogServiceMock
            .Setup(d => d.ShowSolutionSelectionDialogAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<string>, CancellationToken>((liste, _) => angezeigteWerte = liste)
            .ReturnsAsync((string?)null);

        var sut = CreateSut();
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        await ((AsyncRelayCommand)sut.OeffneIdeAuswahlCommand).ExecuteAsync();

        angezeigteWerte.Should().Contain("Visual Studio Code");
        angezeigteWerte!.Should().NotContain(wert => wert.StartsWith("Visual Studio Code:", StringComparison.Ordinal));
    }

    /// <summary>
    /// Regressionstest für die zentrale Anforderung: VS liefert genau 1 .sln, VS Code liefert genau 1
    /// Fallback-Eintrag → aggregiert 2 Einstiegspunkte → KannIdeAuswaehlen == true, obwohl kein einzelnes
    /// Plugin für sich genommen mehrere Einstiegspunkte hat.
    /// </summary>
    [Fact]
    public async Task KannIdeAuswaehlen_WhenEachCompatiblePluginHasExactlyOneEntryPoint_ButMultiplePluginsCompatible_ReturnsTrue()
    {
        var arbeitsverzeichnis = CreateTempDirectory();
        File.WriteAllText(Path.Combine(arbeitsverzeichnis, "Einzige.sln"), string.Empty);
        var aufgabe = await ErstelleAufgabeMitRepositoryAsync(null);
        await _aufgabeService.StartenAsync(aufgabe.Id, "feature/x", arbeitsverzeichnis);

        var sut = CreateSut();
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.KannIdeAuswaehlen.Should().BeTrue(
            "VS liefert 1 .sln und VS Code liefert 1 Fallback-Eintrag - aggregiert 2 Einstiegspunkte, obwohl kein einzelnes Plugin für sich genommen mehrere Einstiegspunkte hat");
    }

    /// <summary>
    /// Erzeugt einen <see cref="Mock{T}"/> von <see cref="IIdePlugin"/> mit den für Einstiegspunkt-Tests
    /// gemeinsam benötigten Setups (PluginName, PluginPrefix, PluginType, GetSettingGroups,
    /// CheckCompatibilityAsync als <see cref="IdePluginCompatibility.Explicit"/> und FindEntryPointsAsync mit
    /// den übergebenen <paramref name="entryPoints"/>). Das jeweils individuelle OpenEntryPointAsync-Setup
    /// bleibt Sache des einzelnen Testfalls.
    /// </summary>
    /// <param name="entryPoints">Die von FindEntryPointsAsync zurückgegebenen Einstiegspunkte.</param>
    /// <param name="pluginName">Der zurückgegebene PluginName.</param>
    /// <param name="pluginPrefix">Der zurückgegebene PluginPrefix.</param>
    /// <returns>Der fertig konfigurierte <see cref="Mock{T}"/> von <see cref="IIdePlugin"/>.</returns>
    private static Mock<IIdePlugin> CreateTestIdePluginMock(
        IReadOnlyList<IdeEntryPoint> entryPoints,
        string pluginName = "Test-IDE",
        string pluginPrefix = "Softwareschmiede.TestIde")
    {
        var idePluginMock = new Mock<IIdePlugin>();
        idePluginMock.SetupGet(p => p.PluginName).Returns(pluginName);
        idePluginMock.SetupGet(p => p.PluginPrefix).Returns(pluginPrefix);
        idePluginMock.SetupGet(p => p.PluginType).Returns(PluginType.Ide);
        idePluginMock.Setup(p => p.GetSettingGroups()).Returns([]);
        idePluginMock.Setup(p => p.CheckCompatibilityAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(IdePluginCompatibility.Explicit);
        idePluginMock.Setup(p => p.FindEntryPointsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(entryPoints);
        return idePluginMock;
    }
}
