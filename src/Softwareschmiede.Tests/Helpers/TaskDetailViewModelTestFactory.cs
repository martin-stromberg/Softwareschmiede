using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Softwareschmiede.App.Services;
using Softwareschmiede.App.ViewModels;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Infrastructure.Data;

namespace Softwareschmiede.Tests.Helpers;

/// <summary>Erstellt ein voll konfiguriertes TaskDetailViewModel inkl. aller Abhängigkeiten für Tests.</summary>
public static class TaskDetailViewModelTestFactory
{
    /// <summary>Erstellt ein TaskDetailViewModel mit Mock-Abhängigkeiten und dem übergebenen DbContext/AufgabeService.</summary>
    /// <param name="db">Der zu verwendende Datenbankkontext.</param>
    /// <param name="workItemService">Der zu verwendende AufgabeService.</param>
    /// <returns>Ein vollständig konfiguriertes TaskDetailViewModel.</returns>
    public static TaskDetailViewModel Create(SoftwareschmiedeDbContext db, WorkItemService workItemService)
    {
        var dialogServiceMock = new Mock<IDialogService>();
        dialogServiceMock
            .Setup(d => d.ShowSolutionSelectionDialogAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        var aiService = TestAiExecutionServiceFactory.Create();
        var logService = new LogService(db, NullLogger<LogService>.Instance);
        var todoService = new TodoService(db, NullLogger<TodoService>.Instance);
        var pluginManagerMock = new Mock<IPluginManager>();
        pluginManagerMock.Setup(p => p.GetDevelopmentAutomationPlugins()).Returns([]);
        pluginManagerMock.Setup(p => p.GetSourceCodeManagementPlugins()).Returns([]);
        pluginManagerMock.Setup(p => p.GetIdePlugins()).Returns([]);
        var appSettingService = new AppSettingService(db, NullLogger<AppSettingService>.Instance);
        var pluginDefaultSettingsService = new PluginDefaultSettingsService(db, NullLogger<PluginDefaultSettingsService>.Instance);
        var pluginActivationService = new PluginActivationService(appSettingService, pluginManagerMock.Object, NullLogger<PluginActivationService>.Instance);
        var pluginSelectionService = new PluginSelectionService(pluginManagerMock.Object, pluginDefaultSettingsService, pluginActivationService, NullLogger<PluginSelectionService>.Instance);
        var promptTemplatesService = new PromptTemplateService(db, NullLogger<PromptTemplateService>.Instance);
        var promptTemplatesPlaceholderService = new PromptTemplatePlaceholderService();
        var promptScheduleDispatchService = new PromptScheduleDispatchService(aiService, TimeProvider.System, NullLogger<PromptScheduleDispatchService>.Instance);
        var gitPluginMock = new Mock<IGitPlugin>();
        var workingDirectoryMock = new Mock<IWorkingDirectoryResolver>();
        var developmentprocessService = new DevelopmentProcessService(
            workItemService,
            logService,
            gitPluginMock.Object,
            pluginSelectionService,
            workingDirectoryMock.Object,
            new DevelopmentProcessServiceOptions(AiExecutionService: aiService, PromptScheduleDispatchService: promptScheduleDispatchService),
            NullLogger<DevelopmentProcessService>.Instance);

        var serviceProviderMock = new Mock<IServiceProvider>();

        var fileExplorerViewModel = CreateStub();
        var todoListViewModel = new TodoListViewModel(todoService, NullLogger<TodoListViewModel>.Instance);

        var workingDirectoryOpenService = CreateWorkingDirectoryOpenService();

        return new TaskDetailViewModel(
            workItemService,
            logService,
            aiService,
            developmentprocessService,
            pluginSelectionService,
            promptTemplatesService,
            promptTemplatesPlaceholderService,
            promptScheduleDispatchService,
            dialogServiceMock.Object,
            pluginManagerMock.Object,
            serviceProviderMock.Object,
            NullLogger<TaskDetailViewModel>.Instance,
            TimeProvider.System,
            fileExplorerViewModel,
            todoListViewModel,
            workingDirectoryOpenService);
    }

    /// <summary>Erstellt ein FileExplorerViewModel mit Mock-Abhängigkeiten für Tests, die kein spezielles Diff-/Browser-Verhalten benötigen.</summary>
    /// <returns>Ein einsatzbereites FileExplorerViewModel mit Mock-Services.</returns>
    public static FileExplorerViewModel CreateStub()
        => new(
            new Mock<IGitWorkspaceBrowserService>().Object,
            new Mock<ITextDiffService>().Object,
            NullLogger<FileExplorerViewModel>.Instance);

    /// <summary>Erstellt einen ArbeitsverzeichnisOeffnenService mit dem übergebenen (oder einem neuen) IProzessStarter-Mock.</summary>
    /// <param name="processStarterMock">Der zu verwendende IProzessStarter-Mock, oder null um einen neuen Mock zu erstellen.</param>
    /// <returns>Ein einsatzbereiter ArbeitsverzeichnisOeffnenService.</returns>
    public static WorkingDirectoryOpenService CreateWorkingDirectoryOpenService(
        Mock<IProcessStarter>? processStarterMock = null)
    {
        processStarterMock ??= new Mock<IProcessStarter>();
        return new WorkingDirectoryOpenService(processStarterMock.Object);
    }
}
