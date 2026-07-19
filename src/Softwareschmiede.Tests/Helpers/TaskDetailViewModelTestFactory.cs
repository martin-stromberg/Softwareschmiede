using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Softwareschmiede.App.Services;
using Softwareschmiede.App.ViewModels;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Infrastructure.Data;

namespace Softwareschmiede.Tests.Helpers;

/// <summary>Erstellt ein voll konfiguriertes TaskDetailViewModel inkl. aller Abhängigkeiten für Tests.</summary>
public static class TaskDetailViewModelTestFactory
{
    /// <summary>Erstellt ein TaskDetailViewModel mit Mock-Abhängigkeiten und dem übergebenen DbContext/AufgabeService.</summary>
    /// <param name="db">Der zu verwendende Datenbankkontext.</param>
    /// <param name="aufgabeService">Der zu verwendende AufgabeService.</param>
    /// <returns>Ein vollständig konfiguriertes TaskDetailViewModel.</returns>
    public static TaskDetailViewModel Create(SoftwareschmiededDbContext db, AufgabeService aufgabeService)
    {
        var dialogServiceMock = new Mock<IDialogService>();
        dialogServiceMock
            .Setup(d => d.ShowSolutionSelectionDialogAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        var kiService = TestKiAusfuehrungsServiceFactory.Create();
        var protokollService = new ProtokollService(db, NullLogger<ProtokollService>.Instance);
        var pluginManagerMock = new Mock<IPluginManager>();
        pluginManagerMock.Setup(p => p.GetDevelopmentAutomationPlugins()).Returns([]);
        pluginManagerMock.Setup(p => p.GetSourceCodeManagementPlugins()).Returns([]);
        var pluginDefaultSettingsService = new PluginDefaultSettingsService(db, NullLogger<PluginDefaultSettingsService>.Instance);
        var pluginSelectionService = new PluginSelectionService(pluginManagerMock.Object, pluginDefaultSettingsService, NullLogger<PluginSelectionService>.Instance);
        var promptVorlagenService = new PromptVorlagenService(db, NullLogger<PromptVorlagenService>.Instance);
        var promptVorlagenPlatzhalterService = new PromptVorlagenPlatzhalterService();
        var promptZeitVersandService = new PromptZeitVersandService(kiService, TimeProvider.System, NullLogger<PromptZeitVersandService>.Instance);
        var gitPluginMock = new Mock<IGitPlugin>();
        var arbeitsverzeichnisMock = new Mock<IArbeitsverzeichnisResolver>();
        var entwicklungsprozessService = new EntwicklungsprozessService(
            aufgabeService,
            protokollService,
            gitPluginMock.Object,
            pluginSelectionService,
            arbeitsverzeichnisMock.Object,
            new EntwicklungsprozessServiceOptions(KiAusfuehrungsService: kiService),
            NullLogger<EntwicklungsprozessService>.Instance);

        var serviceProviderMock = new Mock<IServiceProvider>();

        var fileExplorerViewModel = CreateStub();

        var (arbeitsverzeichnisOeffnenService, ideOeffnenService) = CreateVerzeichnisAktionenServices();

        return new TaskDetailViewModel(
            aufgabeService,
            protokollService,
            kiService,
            entwicklungsprozessService,
            pluginSelectionService,
            promptVorlagenService,
            promptVorlagenPlatzhalterService,
            promptZeitVersandService,
            dialogServiceMock.Object,
            pluginManagerMock.Object,
            serviceProviderMock.Object,
            NullLogger<TaskDetailViewModel>.Instance,
            TimeProvider.System,
            fileExplorerViewModel,
            arbeitsverzeichnisOeffnenService,
            ideOeffnenService);
    }

    /// <summary>Erstellt ein FileExplorerViewModel mit Mock-Abhängigkeiten für Tests, die kein spezielles Diff-/Browser-Verhalten benötigen.</summary>
    /// <returns>Ein einsatzbereites FileExplorerViewModel mit Mock-Services.</returns>
    public static FileExplorerViewModel CreateStub()
        => new(
            new Mock<IGitWorkspaceBrowserService>().Object,
            new Mock<ITextDiffService>().Object,
            NullLogger<FileExplorerViewModel>.Instance);

    /// <summary>Erstellt ArbeitsverzeichnisOeffnenService und IdeOeffnenService, die denselben IProzessStarter-Mock verwenden.</summary>
    /// <param name="prozessStarterMock">Der zu verwendende IProzessStarter-Mock, oder null um einen neuen Mock zu erstellen.</param>
    /// <returns>Ein Tupel aus ArbeitsverzeichnisOeffnenService und IdeOeffnenService.</returns>
    public static (ArbeitsverzeichnisOeffnenService ArbeitsverzeichnisOeffnenService, IdeOeffnenService IdeOeffnenService) CreateVerzeichnisAktionenServices(
        Mock<IProzessStarter>? prozessStarterMock = null)
    {
        prozessStarterMock ??= new Mock<IProzessStarter>();
        return (
            new ArbeitsverzeichnisOeffnenService(prozessStarterMock.Object),
            new IdeOeffnenService(prozessStarterMock.Object));
    }
}
