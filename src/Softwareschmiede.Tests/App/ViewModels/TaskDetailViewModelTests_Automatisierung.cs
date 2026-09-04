using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Softwareschmiede.App.Services;
using Softwareschmiede.App.ViewModels;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.App.ViewModels;

/// <summary>Unit-Tests für die Integration der Autonomen-Aufgabe-Detailansicht als "Automatisierung"-Registerkarte in TaskDetailViewModel.</summary>
public sealed class TaskDetailViewModelTests_Automatisierung : TaskDetailViewModelTestsBase
{
    /// <summary>Präfix für über CreateTempDirectory() erzeugte temporäre Verzeichnisse.</summary>
    protected override string TempDirectoryPrefix => "tdvm_automatisierung_tests";

    /// <summary>Erstellt ein einsatzbereites AutonomAufgabeDetailViewModel für eine gegebene Aufgabe mit minimalen (aber echten) Abhängigkeiten.</summary>
    private AutonomAufgabeDetailViewModel ErstelleDetailViewModel(Aufgabe aufgabe)
    {
        var arbeitsverzeichnisPfad = CreateTempDirectory();
        var konfiguration = new AutonomAufgabeKonfiguration
        {
            Id = Guid.NewGuid(),
            AufgabeId = aufgabe.Id,
            ProjektBranchName = "feature/autonom",
            InitialPrompt = "Implementiere die Aufgabe vollständig gemäß Anforderung.",
            PermissionsJsonPfad = Path.Combine(arbeitsverzeichnisPfad, "permissions.json"),
            ArbeitsverzeichnisPfad = arbeitsverzeichnisPfad
        };

        return AutonomAufgabenInitialisierungsServiceTestFactory.CreateAutonomAufgabeDetailViewModel(_db, aufgabe, konfiguration);
    }

    /// <summary>IsAutomatisierungViewSelected ist true, nachdem SetzeAutonomAufgabeDetailViewAsync ein ViewModel gesetzt hat (wechselt automatisch zur Automatisierung-Ansicht).</summary>
    [Fact]
    public async Task AutomatisierungViewSelected_WhenDetailsViewModelSet()
    {
        var aufgabe = await ErstelleAufgabeMitRepositoryAsync(null);
        var sut = CreateSut();
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        await sut.SetzeAutonomAufgabeDetailViewAsync(ErstelleDetailViewModel(aufgabe));

        sut.IsAutomatisierungViewSelected.Should().BeTrue();
    }

    /// <summary>AutomatisierungViewCommand wechselt von einer anderen Ansicht zurück zur Automatisierung-Ansicht.</summary>
    [Fact]
    public async Task AutomatisierungViewCommand_ChangesViewToAutomatisierung()
    {
        var aufgabe = await ErstelleAufgabeMitRepositoryAsync(null);
        var sut = CreateSut();
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();
        await sut.SetzeAutonomAufgabeDetailViewAsync(ErstelleDetailViewModel(aufgabe));

        sut.InfoViewCommand.Execute(null);
        sut.IsAutomatisierungViewSelected.Should().BeFalse();

        sut.AutomatisierungViewCommand.Execute(null);

        sut.IsAutomatisierungViewSelected.Should().BeTrue();
    }

    /// <summary>ShowAutomatisierungPanel ist false ohne initialisierte Autonome Aufgabe und wird nach SetzeAutonomAufgabeDetailViewAsync true.</summary>
    [Fact]
    public async Task ShowAutomatisierungPanel_TrueWhenViewModelSet_FalseWhenNull()
    {
        var aufgabe = await ErstelleAufgabeMitRepositoryAsync(null);
        var sut = CreateSut();
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.ShowAutomatisierungPanel.Should().BeFalse();

        await sut.SetzeAutonomAufgabeDetailViewAsync(ErstelleDetailViewModel(aufgabe));

        sut.ShowAutomatisierungPanel.Should().BeTrue();
    }

    /// <summary>SetzeAutonomAufgabeDetailViewAsync speichert das übergebene ViewModel, löst PropertyChanged aus und wechselt zur Automatisierung-Ansicht.</summary>
    [Fact]
    public async Task SetzeAutonomAufgabeDetailViewAsync_StoresViewModelAndSwitchesToView()
    {
        var aufgabe = await ErstelleAufgabeMitRepositoryAsync(null);
        var sut = CreateSut();
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();
        var detailVm = ErstelleDetailViewModel(aufgabe);

        var geaenderteProperties = new List<string?>();
        sut.PropertyChanged += (_, args) => geaenderteProperties.Add(args.PropertyName);

        await sut.SetzeAutonomAufgabeDetailViewAsync(detailVm);

        sut.AutonomAufgabeDetailViewModel.Should().BeSameAs(detailVm);
        sut.IsAutomatisierungViewSelected.Should().BeTrue();
        geaenderteProperties.Should().Contain(nameof(TaskDetailViewModel.AutonomAufgabeDetailViewModel));
        geaenderteProperties.Should().Contain(nameof(TaskDetailViewModel.ShowAutomatisierungPanel));
        geaenderteProperties.Should().Contain(nameof(TaskDetailViewModel.IsAutomatisierungViewSelected));
    }

    /// <summary>WaehleAnsicht (über AutomatisierungViewCommand) fällt auf Info zurück, solange ShowAutomatisierungPanel false ist (keine initialisierte Autonome Aufgabe).</summary>
    [Fact]
    public async Task WaehleAnsicht_RejectsAutomatisierungIfPanelNotShown()
    {
        var aufgabe = await ErstelleAufgabeMitRepositoryAsync(null);
        var sut = CreateSut();
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.AutomatisierungViewCommand.Execute(null);

        sut.IsAutomatisierungViewSelected.Should().BeFalse();
        sut.IsInfoViewSelected.Should().BeTrue();
    }

    /// <summary>Beim Wechsel zu einer anderen Aufgabe wird das gemerkte AutonomAufgabeDetailViewModel verworfen (ShowAutomatisierungPanel fällt zurück auf false).</summary>
    [Fact]
    public async Task WaehleStandardAnsicht_CleansUpAutonomAufgabeViewModelOnTaskSwitch()
    {
        var aufgabeA = await ErstelleAufgabeMitRepositoryAsync(null);
        var aufgabeB = await ErstelleAufgabeMitRepositoryAsync(null);
        var sut = CreateSut();
        sut.AufgabeId = aufgabeA.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();
        await sut.SetzeAutonomAufgabeDetailViewAsync(ErstelleDetailViewModel(aufgabeA));
        sut.ShowAutomatisierungPanel.Should().BeTrue();

        sut.AufgabeId = aufgabeB.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.ShowAutomatisierungPanel.Should().BeFalse();
        sut.AutonomAufgabeDetailViewModel.Should().BeNull();
        sut.IsAutomatisierungViewSelected.Should().BeFalse();
    }

    /// <summary>
    /// Ein erneutes Laden derselben Aufgabe (z. B. nach einer Datenaktualisierung wie Issue-Anlage oder
    /// CLI-Stop, die Aufgabe für dieselbe AufgabeId neu zuweisen, ohne die Aufgabe zu wechseln) darf das
    /// gemerkte AutonomAufgabeDetailViewModel NICHT verwerfen — nur ein echter Wechsel zu einer anderen
    /// AufgabeId (siehe <see cref="WaehleStandardAnsicht_CleansUpAutonomAufgabeViewModelOnTaskSwitch"/>)
    /// darf die Automatisierung-Ansicht bereinigen.
    /// </summary>
    [Fact]
    public async Task SetzeAutonomAufgabeDetailViewAsync_BleibtErhalten_BeimErneutenLadenDerselbenAufgabe()
    {
        var aufgabe = await ErstelleAufgabeMitRepositoryAsync(null);
        var sut = CreateSut();
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();
        var detailVm = ErstelleDetailViewModel(aufgabe);
        await sut.SetzeAutonomAufgabeDetailViewAsync(detailVm);
        sut.ShowAutomatisierungPanel.Should().BeTrue();

        // Simuliert ein erneutes Laden derselben Aufgabe (dieselbe AufgabeId), wie es z. B. nach
        // IssueAnlegenAsync/IssueZuweisenAsync oder dem Stoppen der regulären CLI-Ausführung passiert.
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.ShowAutomatisierungPanel.Should().BeTrue();
        sut.AutonomAufgabeDetailViewModel.Should().BeSameAs(detailVm);
    }

    /// <summary>AutonomAufgabeInitialisierenAsync ruft nach erfolgreichem StarteAsync-Aufruf SetzeAutonomAufgabeDetailViewAsync mit dem im Ergebnis enthaltenen DetailViewModel auf und aktiviert dadurch die Automatisierung-Ansicht.</summary>
    [Fact]
    public async Task AutonomAufgabeInitialisierenAsync_RuftSetzeAutonomAufgabeDetailViewAsync_MitErgebnis()
    {
        var testRoot = CreateTempDirectory();
        var aufgabe = AutonomAufgabenInitialisierungsServiceTestFactory.ErstelleAufgabeMitLokalemKlon(
            _db, _projektId, testRoot, "Testaufgabe für Automatisierung-Ansicht");
        await _db.SaveChangesAsync();

        var cliRunnerMock = AutonomAufgabenInitialisierungsServiceTestFactory.CreateCliRunnerMockMitErfolgreicherGitAusfuehrung();
        var gitPluginMock = AutonomAufgabenInitialisierungsServiceTestFactory.CreateGitPluginMockMitErfolgreichemKlon();
        var initialisierungsService = AutonomAufgabenInitialisierungsServiceTestFactory.CreateService(_db, cliRunnerMock.Object, gitPluginMock.Object);

        var pluginManagerMock = new Mock<IPluginManager>();
        pluginManagerMock.Setup(p => p.GetSourceCodeManagementPlugins()).Returns([gitPluginMock.Object]);

        var arbeitsverzeichnisPfad = Path.Combine(testRoot, "arbeitsverzeichnis");
        Directory.CreateDirectory(arbeitsverzeichnisPfad);
        var konfiguration = new AutonomAufgabeKonfiguration
        {
            Id = Guid.NewGuid(),
            AufgabeId = aufgabe.Id,
            ProjektBranchName = "feature/autonom",
            InitialPrompt = "Implementiere die Aufgabe vollständig gemäß Anforderung.",
            PermissionsJsonPfad = Path.Combine(arbeitsverzeichnisPfad, "permissions.json"),
            ArbeitsverzeichnisPfad = arbeitsverzeichnisPfad
        };

        _dialogServiceMock
            .Setup(d => d.ShowAutonomAufgabeInitialisierungsDialogAsync(It.IsAny<AutonomAufgabeInitialisierungsDialogViewModel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(konfiguration);

        var serviceProvider = AutonomAufgabenInitialisierungsServiceTestFactory.CreateAutonomAufgabeStartServiceProvider(
            _db, initialisierungsService, pluginManagerMock.Object);

        var sut = CreateSut(serviceProvider: serviceProvider);
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        await ((AsyncRelayCommand)sut.AutonomAufgabeInitialisierenCommand).ExecuteAsync();

        sut.AutonomAufgabeDetailViewModel.Should().NotBeNull();
        sut.ShowAutomatisierungPanel.Should().BeTrue();
        sut.IsAutomatisierungViewSelected.Should().BeTrue();
    }

    /// <summary>
    /// Reproduziert einen realen Fehler: Wird eine bereits als Autonome Aufgabe konfigurierte Aufgabe
    /// (AutonomAufgabeKonfiguration existiert bereits in der DB) über eine neue TaskDetailViewModel-Instanz
    /// geladen — z. B. weil der Anwender die Aufgabe erneut öffnet, ohne den Initialisierungs-Assistenten
    /// erneut zu durchlaufen —, blieb AutonomAufgabeDetailViewModel bislang null und ShowAutomatisierungPanel
    /// false, weil ausschließlich AutonomAufgabeInitialisierenAsync (über SetzeAutonomAufgabeDetailViewAsync)
    /// das ViewModel setzte. Die Ribbon-Buttons "Start"/"Stop"/"Resume" der Gruppe "Autonome Aufgabe" sind an
    /// ShowAutomatisierungPanel gebunden (siehe TaskDetailView.xaml) und blieben dadurch nach jedem erneuten
    /// Öffnen der Aufgabe unsichtbar bzw. ohne Command.
    /// </summary>
    [Fact]
    public async Task LadenAsync_StelltAutonomAufgabeDetailViewModelWieder_WennAufgabeBereitsAutonomKonfiguriertIst()
    {
        var aufgabe = await ErstelleAufgabeMitRepositoryAsync(null);
        var arbeitsverzeichnisPfad = CreateTempDirectory();
        _db.AutonomAufgabeKonfigurationen.Add(new AutonomAufgabeKonfiguration
        {
            Id = Guid.NewGuid(),
            AufgabeId = aufgabe.Id,
            ProjektBranchName = "feature/autonom",
            InitialPrompt = "Implementiere die Aufgabe vollständig gemäß Anforderung.",
            PermissionsJsonPfad = Path.Combine(arbeitsverzeichnisPfad, "permissions.json"),
            ArbeitsverzeichnisPfad = arbeitsverzeichnisPfad
        });
        await _db.SaveChangesAsync();

        // Neue TaskDetailViewModel-Instanz, OHNE den Initialisierungs-Assistenten zu durchlaufen - simuliert
        // das erneute Öffnen einer bereits konfigurierten Autonomen Aufgabe (z. B. nach Navigation weg und
        // zurück oder nach einem App-Neustart).
        var sut = CreateSut();
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.AutonomAufgabeDetailViewModel.Should().NotBeNull();
        sut.ShowAutomatisierungPanel.Should().BeTrue("die Ribbon-Buttons 'Start'/'Stop'/'Resume' sind daran gebunden");
    }

    /// <summary>
    /// AutonomAufgabeInitialisierenCommand darf für eine bereits autonom konfigurierte Aufgabe nicht erneut
    /// ausführbar sein: AutonomAufgabenInitialisierungsService.InitialisiereAsync prüft nicht, ob bereits eine
    /// AutonomAufgabeKonfiguration existiert, und würde bei erneuter Ausführung entweder eine zweite
    /// Konfiguration anlegen oder beim erneuten Klonen ins bereits vorhandene Arbeitsverzeichnis fehlschlagen.
    /// </summary>
    [Fact]
    public async Task AutonomAufgabeInitialisierenCommand_CanExecute_FalseWennAufgabeBereitsAutonomKonfiguriertIst()
    {
        var aufgabe = await ErstelleAufgabeMitRepositoryAsync(null);
        var arbeitsverzeichnisPfad = CreateTempDirectory();
        _db.AutonomAufgabeKonfigurationen.Add(new AutonomAufgabeKonfiguration
        {
            Id = Guid.NewGuid(),
            AufgabeId = aufgabe.Id,
            ProjektBranchName = "feature/autonom",
            InitialPrompt = "Implementiere die Aufgabe vollständig gemäß Anforderung.",
            PermissionsJsonPfad = Path.Combine(arbeitsverzeichnisPfad, "permissions.json"),
            ArbeitsverzeichnisPfad = arbeitsverzeichnisPfad
        });
        await _db.SaveChangesAsync();

        var sut = CreateSut();
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.AutonomAufgabeInitialisierenCommand.CanExecute(null).Should().BeFalse();
    }

    /// <summary>
    /// Bei deaktiviertem Feature-Flag "Autonome Aufgaben" (Issue 205) darf der Ribbon-Button "Autonome Aufgabe
    /// starten" weder ausführbar noch sichtbar sein: CanAutonomAufgabeInitialisieren steuert dessen Visibility-
    /// Binding in TaskDetailView.xaml und muss false liefern, sobald das Flag deaktiviert ist - unabhängig davon,
    /// ob die Aufgabe bereits autonom konfiguriert ist. Zusätzlich muss auch die gesamte Ribbon-Gruppe "Autonome
    /// Aufgabe" ausgeblendet werden (ShowAutonomAufgabeRibbonGruppe), da bei deaktiviertem Flag alle darin
    /// enthaltenen Buttons (Initialisieren, Start, Stop, Resume) unsichtbar sind und eine leere Gruppe sonst
    /// sichtbar bliebe.
    /// </summary>
    [Fact]
    public async Task AutonomAufgabeInitialisierenCommand_CanExecute_FalseWennFeatureFlagDeaktiviertIst()
    {
        var aufgabe = await ErstelleAufgabeMitRepositoryAsync(null);
        var sut = CreateSut(autonomAufgabenOptions: Options.Create(new AutonomAufgabenOptions { Enabled = false }));
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.CanAutonomAufgabeInitialisieren.Should().BeFalse();
        sut.AutonomAufgabeInitialisierenCommand.CanExecute(null).Should().BeFalse();
        sut.ShowAutonomAufgabeRibbonGruppe.Should().BeFalse();
    }

    /// <summary>
    /// Bei aktiviertem Feature-Flag und einer noch nicht autonom konfigurierten Aufgabe muss die Ribbon-Gruppe
    /// "Autonome Aufgabe" sichtbar sein (der Button "Autonome Aufgabe starten" ist darin ausführbar/sichtbar),
    /// damit Anwender überhaupt Zugriff auf den Einstiegspunkt haben.
    /// </summary>
    [Fact]
    public async Task ShowAutonomAufgabeRibbonGruppe_TrueWennFeatureFlagAktiviertIst()
    {
        var aufgabe = await ErstelleAufgabeMitRepositoryAsync(null);
        var sut = CreateSut(autonomAufgabenOptions: Options.Create(new AutonomAufgabenOptions { Enabled = true }));
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.CanAutonomAufgabeInitialisieren.Should().BeTrue();
        sut.ShowAutonomAufgabeRibbonGruppe.Should().BeTrue();
    }
}
