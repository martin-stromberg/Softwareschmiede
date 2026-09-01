using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
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
    /// <param name="aufgabeService">Der zu verwendende AufgabeService.</param>
    /// <param name="autonomAufgabenOptions">Optionale AutonomAufgabenOptions für das Feature-Flag-Gating, oder null für Standard-Options (Enabled = true, wie im Produktions-Default), konsistent für alle Kollaboratoren des ViewModels.</param>
    /// <returns>Ein vollständig konfiguriertes TaskDetailViewModel.</returns>
    public static TaskDetailViewModel Create(SoftwareschmiededDbContext db, AufgabeService aufgabeService, AutonomAufgabenOptions? autonomAufgabenOptions = null)
    {
        var dialogServiceMock = new Mock<IDialogService>();
        dialogServiceMock
            .Setup(d => d.ShowSolutionSelectionDialogAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        var kiService = TestKiAusfuehrungsServiceFactory.Create();
        var protokollService = new ProtokollService(db, NullLogger<ProtokollService>.Instance);
        var todoService = new TodoService(db, NullLogger<TodoService>.Instance);
        var pluginManagerMock = new Mock<IPluginManager>();
        pluginManagerMock.Setup(p => p.GetDevelopmentAutomationPlugins()).Returns([]);
        pluginManagerMock.Setup(p => p.GetSourceCodeManagementPlugins()).Returns([]);
        pluginManagerMock.Setup(p => p.GetIdePlugins()).Returns([]);
        var appEinstellungService = new AppEinstellungService(db, NullLogger<AppEinstellungService>.Instance);
        var pluginDefaultSettingsService = new PluginDefaultSettingsService(db, NullLogger<PluginDefaultSettingsService>.Instance);
        var pluginActivationService = new PluginActivationService(appEinstellungService, pluginManagerMock.Object, NullLogger<PluginActivationService>.Instance);
        var pluginSelectionService = new PluginSelectionService(pluginManagerMock.Object, pluginDefaultSettingsService, pluginActivationService, NullLogger<PluginSelectionService>.Instance);
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
        var todoListViewModel = new TodoListViewModel(todoService, NullLogger<TodoListViewModel>.Instance);

        var arbeitsverzeichnisOeffnenService = CreateArbeitsverzeichnisOeffnenService();

        var autonomAufgabeStartService = CreateAutonomAufgabeStartService(
            serviceProviderMock.Object,
            dialogServiceMock.Object,
            aufgabeService,
            db,
            autonomAufgabenOptions,
            appEinstellungService);

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
            todoListViewModel,
            arbeitsverzeichnisOeffnenService,
            autonomAufgabeStartService,
            appEinstellungService,
            Options.Create(autonomAufgabenOptions ?? new AutonomAufgabenOptions()));
    }

    /// <summary>Erstellt ein FileExplorerViewModel mit Mock-Abhängigkeiten für Tests, die kein spezielles Diff-/Browser-Verhalten benötigen.</summary>
    /// <returns>Ein einsatzbereites FileExplorerViewModel mit Mock-Services.</returns>
    public static FileExplorerViewModel CreateStub()
        => new(
            new Mock<IGitWorkspaceBrowserService>().Object,
            new Mock<ITextDiffService>().Object,
            NullLogger<FileExplorerViewModel>.Instance);

    /// <summary>Erstellt einen ArbeitsverzeichnisOeffnenService mit dem übergebenen (oder einem neuen) IProzessStarter-Mock.</summary>
    /// <param name="prozessStarterMock">Der zu verwendende IProzessStarter-Mock, oder null um einen neuen Mock zu erstellen.</param>
    /// <returns>Ein einsatzbereiter ArbeitsverzeichnisOeffnenService.</returns>
    public static ArbeitsverzeichnisOeffnenService CreateArbeitsverzeichnisOeffnenService(
        Mock<IProzessStarter>? prozessStarterMock = null)
    {
        prozessStarterMock ??= new Mock<IProzessStarter>();
        return new ArbeitsverzeichnisOeffnenService(prozessStarterMock.Object);
    }

    /// <summary>
    /// Erstellt einen IServiceProvider-Mock, der die von TaskDetailViewModel.LadenAsync via GetRequiredService
    /// aufgelösten Abhängigkeiten für die Wiederherstellung von AutonomAufgabeDetailViewModel bei bereits
    /// autonom konfigurierten Aufgaben bereitstellt (ProjektleiterAgentService, SessionManagementService,
    /// ILogger&lt;AutonomAufgabeDetailViewModel&gt;), ansonsten aber wie ein leerer Mock reagiert (GetService
    /// liefert für alle anderen Typen weiterhin null). Ohne diese Registrierungen würde jeder CreateSut()-Test
    /// mit einer bereits autonom konfigurierten Aufgabe an einer InvalidOperationException aus
    /// GetRequiredService scheitern, sobald LadenAsync (siehe TaskDetailViewModel.cs) versucht, das
    /// ViewModel wiederherzustellen.
    /// </summary>
    /// <param name="db">Der zu verwendende Datenbankkontext.</param>
    /// <param name="kiAusfuehrungsService">Der zu verwendende KiAusfuehrungsService (dieselbe Instanz wie im übrigen Testaufbau).</param>
    /// <returns>Ein IServiceProvider mit den für Autonome-Aufgabe-Rehydrierung nötigen Registrierungen.</returns>
    public static IServiceProvider CreateDefaultServiceProvider(SoftwareschmiededDbContext db, KiAusfuehrungsService kiAusfuehrungsService)
    {
        var mock = new Mock<IServiceProvider>();
        var projektleiterAgentService = AutonomAufgabenInitialisierungsServiceTestFactory.CreateProjektleiterAgentService(db, kiAusfuehrungsService);
        var sessionManagementService = new SessionManagementService(db, NullLogger<SessionManagementService>.Instance);
        mock.Setup(sp => sp.GetService(typeof(ProjektleiterAgentService))).Returns(projektleiterAgentService);
        mock.Setup(sp => sp.GetService(typeof(SessionManagementService))).Returns(sessionManagementService);
        mock.Setup(sp => sp.GetService(typeof(ILogger<AutonomAufgabeDetailViewModel>))).Returns(NullLogger<AutonomAufgabeDetailViewModel>.Instance);
        return mock.Object;
    }

    /// <summary>Erstellt einen AutonomAufgabeStartService mit den übergebenen Abhängigkeiten für Tests.</summary>
    /// <param name="serviceProvider">Der zu verwendende IServiceProvider.</param>
    /// <param name="dialogService">Der zu verwendende IDialogService.</param>
    /// <param name="aufgabeService">Der zu verwendende AufgabeService.</param>
    /// <param name="db">Der zu verwendende Datenbankkontext, aus dem intern ein AppEinstellungService für die Feature-Flag-Guard-Klausel erzeugt wird (sofern <paramref name="appEinstellungService"/> nicht explizit übergeben wird).</param>
    /// <param name="autonomAufgabenOptions">Die zu verwendenden AutonomAufgabenOptions, oder null für Standard-Options (Enabled = true).</param>
    /// <param name="appEinstellungService">Optionaler, bereits vorhandener AppEinstellungService (z. B. um denselben DB-persistierten Feature-Flag-Zustand wie im übrigen Testaufbau zu verwenden); wird sonst aus <paramref name="db"/> neu erstellt.</param>
    /// <returns>Ein einsatzbereiter AutonomAufgabeStartService.</returns>
    public static AutonomAufgabeStartService CreateAutonomAufgabeStartService(
        IServiceProvider serviceProvider,
        IDialogService dialogService,
        AufgabeService aufgabeService,
        SoftwareschmiededDbContext db,
        AutonomAufgabenOptions? autonomAufgabenOptions = null,
        AppEinstellungService? appEinstellungService = null)
        => new(
            serviceProvider,
            dialogService,
            aufgabeService,
            appEinstellungService ?? new AppEinstellungService(db, NullLogger<AppEinstellungService>.Instance),
            Options.Create(autonomAufgabenOptions ?? new AutonomAufgabenOptions()),
            NullLogger<AutonomAufgabeStartService>.Instance);
}
