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
    public static TaskDetailViewModel Create(SoftwareschmiededDbContext db, AufgabeService aufgabeService)
    {
        var dialogServiceMock = new Mock<IDialogService>();
        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        var kiService = new KiAusfuehrungsService(NullLogger<KiAusfuehrungsService>.Instance, scopeFactoryMock.Object);
        var protokollService = new ProtokollService(db, NullLogger<ProtokollService>.Instance);
        var pluginManagerMock = new Mock<IPluginManager>();
        pluginManagerMock.Setup(p => p.GetDevelopmentAutomationPlugins()).Returns([]);
        pluginManagerMock.Setup(p => p.GetSourceCodeManagementPlugins()).Returns([]);
        var pluginDefaultSettingsService = new PluginDefaultSettingsService(db, NullLogger<PluginDefaultSettingsService>.Instance);
        var pluginSelectionService = new PluginSelectionService(pluginManagerMock.Object, pluginDefaultSettingsService, NullLogger<PluginSelectionService>.Instance);
        var gitPluginMock = new Mock<IGitPlugin>();
        var arbeitsverzeichnisMock = new Mock<IArbeitsverzeichnisResolver>();
        var entwicklungsprozessService = new EntwicklungsprozessService(
            aufgabeService,
            protokollService,
            null,
            gitPluginMock.Object,
            pluginSelectionService,
            arbeitsverzeichnisMock.Object,
            null,
            kiService,
            NullLogger<EntwicklungsprozessService>.Instance);

        return new TaskDetailViewModel(
            aufgabeService,
            protokollService,
            kiService,
            entwicklungsprozessService,
            pluginSelectionService,
            dialogServiceMock.Object,
            NullLogger<TaskDetailViewModel>.Instance);
    }
}
