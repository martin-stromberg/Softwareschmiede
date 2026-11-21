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
/// <see cref="TaskDetailViewModel.OpenIdeSelectionCommand"/>, <see cref="TaskDetailViewModel.SelectCanIde"/>
/// und den SelectEntryPointAsync-Callback.
/// </summary>
public sealed class TaskDetailViewModelTests_IdeSelection : TaskDetailViewModelTestBase
{
    /// <summary>Präfix für über CreateTempDirectory() erzeugte temporäre Verzeichnisse.</summary>
    protected override string TempDirectoryPrefix => "tdvm_ide_auswahl_tests";

    /// <summary>Bei mehreren Solutions ruft OeffneIdeAuswahlCommand OpenIdeSelectionAsync auf, das - anders als OeffneIdeCommand - den Auswahl-Dialog anzeigt.</summary>
    [Fact]
    public async Task OpenIdeSelectionCommand_ExecuteAsync_CallsOpenIdeSelectionAsyncOn()
    {
        var workingDirectory = CreateTempDirectory();
        var firstSolution = Path.Combine(workingDirectory, "Erste.sln");
        var secondSolution = Path.Combine(workingDirectory, "Zweite.sln");
        File.WriteAllText(firstSolution, string.Empty);
        File.WriteAllText(secondSolution, string.Empty);
        var workItem = await CreateWorkItemWithRepositoryAsync(null);
        await _workItemService.StartAsync(workItem.Id, "feature/x", workingDirectory, ct: TestContext.Current.CancellationToken);

        _dialogServiceMock
            .Setup(d => d.ShowSolutionSelectionDialogAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Visual Studio: Zweite.sln");

        var processStarterMock = new Mock<IProcessStarter>();
        var sut = CreateSut(processStarterMock);
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        await ((AsyncRelayCommand)sut.OpenIdeSelectionCommand).ExecuteAsync();

        _dialogServiceMock.Verify(
            d => d.ShowSolutionSelectionDialogAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()),
            Times.Once);
        processStarterMock.Verify(
            p => p.Start(It.Is<ProcessStartRequest>(a => a.FileName == secondSolution && a.UseShellExecute)),
            Times.Once);
    }

    /// <summary>Ohne vorhandenes Arbeitsverzeichnis (KannIdeOeffnen == false) kann OeffneIdeAuswahlCommand nicht ausgeführt werden.</summary>
    [Fact]
    public async Task OpenIdeSelectionCommand_CanExecute_WhenCanIdeOpenFalse_ReturnsFalse()
    {
        var workItem = await CreateWorkItemWithRepositoryAsync(null);
        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.OpenCanIde.Should().BeFalse("ohne LokalerKlonPfad existiert noch kein Arbeitsverzeichnis");
        sut.OpenIdeSelectionCommand.CanExecute(null).Should().BeFalse();
    }

    /// <summary>
    /// Bei genau einem gefundenen Einstiegspunkt bleibt KannIdeAuswaehlen false (Dropdown-Button unsichtbar).
    /// Isoliert über idePlugins-Override auf nur Visual Studio, da CreateSut() standardmäßig zusätzlich
    /// Visual Studio Code aktiviert, das für jedes Repository einen weiteren Fallback-Eintrag liefert - mit
    /// der Multi-Plugin-Aggregation würde die Gesamtanzahl sonst auf 2 steigen.
    /// </summary>
    [Fact]
    public async Task SelectCanIde_WhenOneEntryPoint_ReturnsFalse()
    {
        var workingDirectory = CreateTempDirectory();
        File.WriteAllText(Path.Combine(workingDirectory, "Einzige.sln"), string.Empty);
        var workItem = await CreateWorkItemWithRepositoryAsync(null);
        await _workItemService.StartAsync(workItem.Id, "feature/x", workingDirectory, ct: TestContext.Current.CancellationToken);

        var visualStudioPlugin = new VisualStudioIdePlugin(new Mock<IProcessStarter>().Object);
        var sut = CreateSut(idePlugins: [visualStudioPlugin]);
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        await ((AsyncRelayCommand)sut.OpenIdeCommand).ExecuteAsync();

        sut.SelectCanIde.Should().BeFalse();
    }

    /// <summary>Bei mehreren gefundenen Einstiegspunkten wird KannIdeAuswaehlen true (Dropdown-Button sichtbar).</summary>
    [Fact]
    public async Task SelectCanIde_WhenMultipleEntryPoints_ReturnsTrue()
    {
        var workingDirectory = CreateTempDirectory();
        File.WriteAllText(Path.Combine(workingDirectory, "Erste.sln"), string.Empty);
        File.WriteAllText(Path.Combine(workingDirectory, "Zweite.sln"), string.Empty);
        var workItem = await CreateWorkItemWithRepositoryAsync(null);
        await _workItemService.StartAsync(workItem.Id, "feature/x", workingDirectory, ct: TestContext.Current.CancellationToken);

        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        await ((AsyncRelayCommand)sut.OpenIdeCommand).ExecuteAsync();

        sut.SelectCanIde.Should().BeTrue();
    }

    /// <summary>
    /// Regressionstest: KannIdeAuswaehlen wird bereits am Ende von LadenAsync einmalig berechnet, ohne dass
    /// zuvor OeffneIdeCommand/OeffneIdeAuswahlCommand ausgeführt werden muss - der Dropdown-Button des
    /// Split-Buttons ist damit beim ersten Anzeigen der View korrekt sichtbar, statt (wie vor dem Fix) immer
    /// mit KannIdeAuswaehlen == false zu starten. Kein Einstiegspunkt wird dabei geöffnet.
    /// </summary>
    [Fact]
    public async Task SelectCanIde_LoadAfterAsync_WhenMultipleEntryPoints_OpenReturnsTrueWithout()
    {
        var workingDirectory = CreateTempDirectory();
        File.WriteAllText(Path.Combine(workingDirectory, "Erste.sln"), string.Empty);
        File.WriteAllText(Path.Combine(workingDirectory, "Zweite.sln"), string.Empty);
        var workItem = await CreateWorkItemWithRepositoryAsync(null);
        await _workItemService.StartAsync(workItem.Id, "feature/x", workingDirectory, ct: TestContext.Current.CancellationToken);

        var processStarterMock = new Mock<IProcessStarter>();
        var sut = CreateSut(processStarterMock);
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.SelectCanIde.Should().BeTrue();
        processStarterMock.Verify(p => p.Start(It.IsAny<ProcessStartRequest>()), Times.Never);
    }

    /// <summary>
    /// Regressionstest: Bei genau einem Einstiegspunkt bleibt KannIdeAuswaehlen bereits nach LadenAsync false,
    /// ohne dass ein Öffnen-Versuch nötig ist. Isoliert über idePlugins-Override auf nur Visual Studio (siehe
    /// Begründung bei KannIdeAuswaehlen_WhenOneEntryPoint_ReturnsFalse).
    /// </summary>
    [Fact]
    public async Task SelectCanIde_LoadAfterAsync_WhenOneEntryPoint_ReturnsFalse()
    {
        var workingDirectory = CreateTempDirectory();
        File.WriteAllText(Path.Combine(workingDirectory, "Einzige.sln"), string.Empty);
        var workItem = await CreateWorkItemWithRepositoryAsync(null);
        await _workItemService.StartAsync(workItem.Id, "feature/x", workingDirectory, ct: TestContext.Current.CancellationToken);

        var visualStudioPlugin = new VisualStudioIdePlugin(new Mock<IProcessStarter>().Object);
        var sut = CreateSut(idePlugins: [visualStudioPlugin]);
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.SelectCanIde.Should().BeFalse();
    }

    /// <summary>Regressionstest: Ohne gefundene Einstiegspunkte bleibt KannIdeAuswaehlen bereits nach LadenAsync false, ohne dass dabei eine ErrorMessage angezeigt wird (das Laden der Aufgabe selbst war erfolgreich).</summary>
    [Fact]
    public async Task SelectCanIde_LoadAfterAsync_WhenNoEntryPoints_ReturnsFalseWithoutErrorMessage()
    {
        var workingDirectory = CreateTempDirectory();
        var workItem = await CreateWorkItemWithRepositoryAsync(null);
        await _workItemService.StartAsync(workItem.Id, "feature/x", workingDirectory, ct: TestContext.Current.CancellationToken);

        var sut = CreateSut(visualStudioCodeLocator: new TestVisualStudioCodeLocator(VisualStudioCodeAvailability.NotAvailable));
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.SelectCanIde.Should().BeFalse();
        sut.ErrorMessage.Should().BeNullOrEmpty("ein Ermittlungsfehler beim Laden darf nicht als ErrorMessage angezeigt werden");
    }

    /// <summary>Ohne gefundene Einstiegspunkte (kein .sln, Visual Studio Code nicht verfügbar) bleibt KannIdeAuswaehlen false und eine Fehlermeldung wird gesetzt.</summary>
    [Fact]
    public async Task SelectCanIde_WhenNoEntryPoints_ReturnsFalse()
    {
        var workingDirectory = CreateTempDirectory();
        var workItem = await CreateWorkItemWithRepositoryAsync(null);
        await _workItemService.StartAsync(workItem.Id, "feature/x", workingDirectory, ct: TestContext.Current.CancellationToken);

        var sut = CreateSut(visualStudioCodeLocator: new TestVisualStudioCodeLocator(VisualStudioCodeAvailability.NotAvailable));
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        await ((AsyncRelayCommand)sut.OpenIdeCommand).ExecuteAsync();

        sut.SelectCanIde.Should().BeFalse();
        sut.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    /// <summary>Bei mehreren Einstiegspunkten zeigt der SelectEntryPointAsync-Callback (über OeffneIdeAuswahlCommand) den Auswahl-Dialog an und öffnet den dort gewählten Einstiegspunkt.</summary>
    [Fact]
    public async Task SelectEntryPointAsync_WithMultipleEntryPoints_ShowsDialogAndReturnsSelected()
    {
        var workingDirectory = CreateTempDirectory();
        var firstSolution = Path.Combine(workingDirectory, "Erste.sln");
        var secondSolution = Path.Combine(workingDirectory, "Zweite.sln");
        File.WriteAllText(firstSolution, string.Empty);
        File.WriteAllText(secondSolution, string.Empty);
        var workItem = await CreateWorkItemWithRepositoryAsync(null);
        await _workItemService.StartAsync(workItem.Id, "feature/x", workingDirectory, ct: TestContext.Current.CancellationToken);

        _dialogServiceMock
            .Setup(d => d.ShowSolutionSelectionDialogAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Visual Studio: Zweite.sln");

        var processStarterMock = new Mock<IProcessStarter>();
        var sut = CreateSut(processStarterMock);
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        await ((AsyncRelayCommand)sut.OpenIdeSelectionCommand).ExecuteAsync();

        _dialogServiceMock.Verify(
            d => d.ShowSolutionSelectionDialogAsync(
                It.Is<IReadOnlyList<string>>(list => list.Contains("Visual Studio: Erste.sln") && list.Contains("Visual Studio: Zweite.sln")),
                It.IsAny<CancellationToken>()),
            Times.Once);
        processStarterMock.Verify(
            p => p.Start(It.Is<ProcessStartRequest>(a => a.FileName == secondSolution && a.UseShellExecute)),
            Times.Once);
    }

    /// <summary>Bricht der Anwender den Auswahl-Dialog ab (Rückgabe null), öffnet der SelectEntryPointAsync-Callback nichts.</summary>
    [Fact]
    public async Task SelectEntryPointAsync_WithDialogAbort_ReturnsNull()
    {
        var workingDirectory = CreateTempDirectory();
        File.WriteAllText(Path.Combine(workingDirectory, "Erste.sln"), string.Empty);
        File.WriteAllText(Path.Combine(workingDirectory, "Zweite.sln"), string.Empty);
        var workItem = await CreateWorkItemWithRepositoryAsync(null);
        await _workItemService.StartAsync(workItem.Id, "feature/x", workingDirectory, ct: TestContext.Current.CancellationToken);

        _dialogServiceMock
            .Setup(d => d.ShowSolutionSelectionDialogAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var processStarterMock = new Mock<IProcessStarter>();
        var sut = CreateSut(processStarterMock);
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        await ((AsyncRelayCommand)sut.OpenIdeSelectionCommand).ExecuteAsync();

        processStarterMock.Verify(p => p.Start(It.IsAny<ProcessStartRequest>()), Times.Never);
        sut.ErrorMessage.Should().BeNullOrEmpty("ein Abbruch des Auswahl-Dialogs ist kein Fehler");
    }

    /// <summary>Der SelectEntryPointAsync-Callback nutzt IdeEntryPoint.DisplayName (statt Path) für die Anzeige im Auswahl-Dialog, sofern gesetzt.</summary>
    [Fact]
    public async Task SelectEntryPointAsync_UsesDisplayNameInDialog()
    {
        var workingDirectory = CreateTempDirectory();
        var firstEntryPoint = CreateEntryPointWithDisplayName(Path.Combine(workingDirectory, "erste.sln"), "Erste Solution");
        var secondEntryPoint = CreateEntryPointWithDisplayName(Path.Combine(workingDirectory, "zweite.sln"), "Zweite Solution");
        var idePluginMock = CreateTestIdePluginMock([firstEntryPoint, secondEntryPoint]);
        idePluginMock.Setup(p => p.OpenEntryPointAsync(It.IsAny<IdeEntryPoint>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var workItem = await CreateWorkItemWithRepositoryAsync(null);
        await _workItemService.StartAsync(workItem.Id, "feature/x", workingDirectory, ct: TestContext.Current.CancellationToken);

        _dialogServiceMock
            .Setup(d => d.ShowSolutionSelectionDialogAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync($"Test-IDE: {secondEntryPoint.DisplayName}");

        var sut = CreateSut(idePlugins: [idePluginMock.Object]);
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        await ((AsyncRelayCommand)sut.OpenIdeSelectionCommand).ExecuteAsync();

        _dialogServiceMock.Verify(
            d => d.ShowSolutionSelectionDialogAsync(
                It.Is<IReadOnlyList<string>>(list => list.Contains("Test-IDE: Erste Solution") && list.Contains("Test-IDE: Zweite Solution")),
                It.IsAny<CancellationToken>()),
            Times.Once);
        idePluginMock.Verify(p => p.OpenEntryPointAsync(secondEntryPoint, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Regressionstest: Schlägt OpenEntryPointAsync trotz mehrerer gefundener Einstiegspunkte fehl (z. B. IDE-Prozess
    /// kann nicht gestartet werden), bleibt KannIdeAuswaehlen weiterhin true - der Dropdown-Button des Split-Buttons
    /// darf nach einem fehlgeschlagenen Öffnen-Versuch nicht verschwinden, da der Anwender genau jetzt über den
    /// Dropdown einen anderen Einstiegspunkt probieren könnte.
    /// </summary>
    [Fact]
    public async Task SelectCanIde_WhenOpenEntryPointFailsWithMultipleEntryPoints_RemainsTrue()
    {
        var workingDirectory = CreateTempDirectory();
        var firstEntryPoint = CreateEntryPointWithDisplayName(Path.Combine(workingDirectory, "erste.sln"), "Erste Solution");
        var secondEntryPoint = CreateEntryPointWithDisplayName(Path.Combine(workingDirectory, "zweite.sln"), "Zweite Solution");
        var idePluginMock = CreateTestIdePluginMock([firstEntryPoint, secondEntryPoint]);
        idePluginMock.Setup(p => p.OpenEntryPointAsync(It.IsAny<IdeEntryPoint>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("IDE-Prozess konnte nicht gestartet werden."));

        var workItem = await CreateWorkItemWithRepositoryAsync(null);
        await _workItemService.StartAsync(workItem.Id, "feature/x", workingDirectory, ct: TestContext.Current.CancellationToken);

        var sut = CreateSut(idePlugins: [idePluginMock.Object]);
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        await ((AsyncRelayCommand)sut.OpenIdeCommand).ExecuteAsync();

        sut.ErrorMessage.Should().NotBeNullOrEmpty();
        sut.SelectCanIde.Should().BeTrue("die Anzahl der gefundenen Einstiegspunkte hat sich durch den Öffnen-Fehler nicht geändert");
    }

    /// <summary>Ohne gefundene Einstiegspunkte zeigt OpenIdeSelectionAsync dieselbe Fehlermeldung wie OpenIdeAsync.</summary>
    [Fact]
    public async Task OpenIdeSelectionAsync_WithNoEntryPoints_ShowsError()
    {
        var workingDirectory = CreateTempDirectory();
        var workItem = await CreateWorkItemWithRepositoryAsync(null);
        await _workItemService.StartAsync(workItem.Id, "feature/x", workingDirectory, ct: TestContext.Current.CancellationToken);

        var processStarterMock = new Mock<IProcessStarter>();
        var sut = CreateSut(
            processStarterMock,
            visualStudioCodeLocator: new TestVisualStudioCodeLocator(VisualStudioCodeAvailability.NotAvailable));
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        await ((AsyncRelayCommand)sut.OpenIdeSelectionCommand).ExecuteAsync();

        sut.ErrorMessage.Should().NotBeNullOrEmpty();
        processStarterMock.Verify(p => p.Start(It.IsAny<ProcessStartRequest>()), Times.Never);
    }

    /// <summary>
    /// Bei aktiviertem VS (mit .sln) und VS Code (Fallback) enthält die an ShowSolutionSelectionDialogAsync
    /// übergebene Liste sowohl den formatierten VS-Eintrag als auch "Visual Studio Code" - die Aggregation
    /// erfasst also Einstiegspunkte beider kompatiblen Plugins, nicht nur des einen priorisierten.
    /// </summary>
    [Fact]
    public async Task SelectEntryPointAsync_WithEntryPointsFromTwoPlugins_ShowsBothInDialog()
    {
        var workingDirectory = CreateTempDirectory();
        File.WriteAllText(Path.Combine(workingDirectory, "MyProject.sln"), string.Empty);
        var workItem = await CreateWorkItemWithRepositoryAsync(null);
        await _workItemService.StartAsync(workItem.Id, "feature/x", workingDirectory, ct: TestContext.Current.CancellationToken);

        _dialogServiceMock
            .Setup(d => d.ShowSolutionSelectionDialogAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        await ((AsyncRelayCommand)sut.OpenIdeSelectionCommand).ExecuteAsync();

        _dialogServiceMock.Verify(
            d => d.ShowSolutionSelectionDialogAsync(
                It.Is<IReadOnlyList<string>>(list => list.Contains("Visual Studio: MyProject.sln") && list.Contains("Visual Studio Code")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Wählt der Anwender den VS-Code-Eintrag aus dem Dialog (obwohl VS als Explicit-Plugin von
    /// ResolveIdePluginAsync bevorzugt würde), wird OpenEntryPointAsync auf dem VS-Code-Plugin aufgerufen,
    /// nicht auf VS.
    /// </summary>
    [Fact]
    public async Task SelectEntryPointAsync_SelectingEntryFromFallbackPlugin_OpensViaThatPlugin_NotViaResolvedPlugin()
    {
        var workingDirectory = CreateTempDirectory();
        File.WriteAllText(Path.Combine(workingDirectory, "MyProject.sln"), string.Empty);
        var workItem = await CreateWorkItemWithRepositoryAsync(null);
        await _workItemService.StartAsync(workItem.Id, "feature/x", workingDirectory, ct: TestContext.Current.CancellationToken);

        _dialogServiceMock
            .Setup(d => d.ShowSolutionSelectionDialogAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Visual Studio Code");

        var processStarterMock = new Mock<IProcessStarter>();
        var locator = new TestVisualStudioCodeLocator(new VisualStudioCodeAvailability(true, "code.cmd"));
        var sut = CreateSut(processStarterMock, locator);
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        await ((AsyncRelayCommand)sut.OpenIdeSelectionCommand).ExecuteAsync();

        processStarterMock.Verify(
            p => p.Start(It.Is<ProcessStartRequest>(a => a.FileName == "code.cmd" && !a.UseShellExecute)),
            Times.Once);
        processStarterMock.Verify(
            p => p.Start(It.Is<ProcessStartRequest>(a => a.UseShellExecute)),
            Times.Never);
    }

    /// <summary>Die an den Dialog übergebene Liste ordnet Einträge kompatibler Explicit-Plugins vor Einträgen von Fallback-Plugins - unabhängig von der Registrierungsreihenfolge der Plugins.</summary>
    [Fact]
    public async Task SelectEntryPointAsync_OrdersExplicitPluginEntriesBeforeFallbackPluginEntries()
    {
        var workingDirectory = CreateTempDirectory();
        File.WriteAllText(Path.Combine(workingDirectory, "MyProject.sln"), string.Empty);
        var workItem = await CreateWorkItemWithRepositoryAsync(null);
        await _workItemService.StartAsync(workItem.Id, "feature/x", workingDirectory, ct: TestContext.Current.CancellationToken);

        var processStarterMock = new Mock<IProcessStarter>();
        var visualStudioCodePlugin = new VisualStudioCodeIdePlugin(
            processStarterMock.Object,
            new TestVisualStudioCodeLocator(VisualStudioCodeAvailability.NotAvailable));
        var visualStudioPlugin = new VisualStudioIdePlugin(processStarterMock.Object);

        IReadOnlyList<string>? displayedValues = null;
        _dialogServiceMock
            .Setup(d => d.ShowSolutionSelectionDialogAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<string>, CancellationToken>((list, _) => displayedValues = list)
            .ReturnsAsync((string?)null);

        // VS Code (Fallback) wird zuerst registriert, VS (Explicit) danach - die Rückgabeliste muss trotzdem Explicit-vor-Fallback ordnen.
        var sut = CreateSut(processStarterMock, idePlugins: [visualStudioCodePlugin, visualStudioPlugin]);
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        await ((AsyncRelayCommand)sut.OpenIdeSelectionCommand).ExecuteAsync();

        displayedValues.Should().NotBeNull();
        var valuesList = displayedValues!.ToList();
        valuesList.IndexOf("Visual Studio: MyProject.sln").Should().BeLessThan(valuesList.IndexOf("Visual Studio Code"));
    }

    /// <summary>Für einen VS-Einstiegspunkt (DisplayName == null) liefert die Formatierung "Visual Studio: {Dateiname}".</summary>
    [Fact]
    public async Task FormatDisplayValue_ForVisualStudioEntryPoint_UsesPluginNamePrefixAndFileName()
    {
        var workingDirectory = CreateTempDirectory();
        File.WriteAllText(Path.Combine(workingDirectory, "MyProject.sln"), string.Empty);
        var workItem = await CreateWorkItemWithRepositoryAsync(null);
        await _workItemService.StartAsync(workItem.Id, "feature/x", workingDirectory, ct: TestContext.Current.CancellationToken);

        IReadOnlyList<string>? displayedValues = null;
        _dialogServiceMock
            .Setup(d => d.ShowSolutionSelectionDialogAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<string>, CancellationToken>((list, _) => displayedValues = list)
            .ReturnsAsync((string?)null);

        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        await ((AsyncRelayCommand)sut.OpenIdeSelectionCommand).ExecuteAsync();

        displayedValues.Should().Contain("Visual Studio: MyProject.sln");
    }

    /// <summary>Für den VS-Code-Einstiegspunkt (DisplayName == PluginName) liefert die Formatierung nur "Visual Studio Code" ohne Doppelung.</summary>
    [Fact]
    public async Task FormatDisplayValue_ForVisualStudioCodeEntryPoint_UsesPluginNameOnly()
    {
        var workingDirectory = CreateTempDirectory();
        File.WriteAllText(Path.Combine(workingDirectory, "MyProject.sln"), string.Empty);
        var workItem = await CreateWorkItemWithRepositoryAsync(null);
        await _workItemService.StartAsync(workItem.Id, "feature/x", workingDirectory, ct: TestContext.Current.CancellationToken);

        IReadOnlyList<string>? displayedValues = null;
        _dialogServiceMock
            .Setup(d => d.ShowSolutionSelectionDialogAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<string>, CancellationToken>((list, _) => displayedValues = list)
            .ReturnsAsync((string?)null);

        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        await ((AsyncRelayCommand)sut.OpenIdeSelectionCommand).ExecuteAsync();

        displayedValues.Should().Contain("Visual Studio Code");
        displayedValues!.Should().NotContain(value => value.StartsWith("Visual Studio Code:", StringComparison.Ordinal));
    }

    /// <summary>
    /// Regressionstest für die zentrale Anforderung: VS liefert genau 1 .sln, VS Code liefert genau 1
    /// Fallback-Eintrag → aggregiert 2 Einstiegspunkte → KannIdeAuswaehlen == true, obwohl kein einzelnes
    /// Plugin für sich genommen mehrere Einstiegspunkte hat.
    /// </summary>
    [Fact]
    public async Task SelectCanIde_WhenEachCompatiblePluginHasExactlyOneEntryPoint_ButMultiplePluginsCompatible_ReturnsTrue()
    {
        var workingDirectory = CreateTempDirectory();
        File.WriteAllText(Path.Combine(workingDirectory, "Einzige.sln"), string.Empty);
        var workItem = await CreateWorkItemWithRepositoryAsync(null);
        await _workItemService.StartAsync(workItem.Id, "feature/x", workingDirectory, ct: TestContext.Current.CancellationToken);

        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.SelectCanIde.Should().BeTrue(
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
