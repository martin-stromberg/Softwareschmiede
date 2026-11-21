using System.Collections.ObjectModel;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Softwareschmiede.App.Services;
using Softwareschmiede.App.ViewModels;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.App.ViewModels;

/// <summary>Unit-Tests für DashboardViewModel.</summary>
public sealed class DashboardViewModelTests : IDisposable
{
    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiedeDbContext _db;
    private readonly ProjectService _projectService;
    private readonly WorkItemService _workItemService;
    private readonly WorkItemRecoveryService _recoveryService;
    private readonly Guid _projectId = new Guid("33333333-3333-3333-3333-333333333333");

    /// <summary>DashboardViewModelTests.</summary>
    public DashboardViewModelTests()
    {
        _db = TestDbContextFactory.Create();
        _projectService = new ProjectService(_db, NullLogger<ProjectService>.Instance);
        _workItemService = new WorkItemService(_db, NullLogger<WorkItemService>.Instance, new TodoService(_db, NullLogger<TodoService>.Instance));
        var runningStatusSourceMock = new Mock<IRunningAutomationStatusSource>();
        _recoveryService = new WorkItemRecoveryService(_db, runningStatusSourceMock.Object, NullLogger<WorkItemRecoveryService>.Instance);

        _db.Projects.Add(new Softwareschmiede.Domain.Entities.Project
        {
            Id = _projectId,
            Name = "Testprojekt",
            CreatedAt = DateTimeOffset.UtcNow,
            Status = ProjectStatus.Active
        });
        _db.SaveChanges();
    }

    /// <summary>Dispose.</summary>
    public void Dispose() => _db.Dispose();

    private DashboardViewModel CreateSut(WorkItemListModeService? workItemsListModeService = null) =>
        new(_projectService, _workItemService, _recoveryService, NullLogger<DashboardViewModel>.Instance,
            workItemsListModeService);

    /// <summary>Initialize setzt AktiveAufgabenListe auf die übergebene Instanz, um die gemeinsame Datenquelle des MainWindowViewModel zu übernehmen.</summary>
    [Fact]
    public void Initialize_ShouldSetActiveWorkItemsListToProvidedInstance_WhenCalled()
    {
        // Arrange
        var sut = CreateSut();
        var splitList = new ObservableCollection<ActiveWorkItemPanelItem>();

        // Act
        sut.Initialize(splitList, _ => { });

        // Assert
        sut.ActiveWorkItemsList.Should().BeSameAs(splitList);
    }

    /// <summary>LadenAsync lädt aktive Aufgaben nicht mehr selbst, sondern lässt die über Initialize gesetzte AktiveAufgabenListe unverändert (einzige gemeinsame Datenquelle im MainWindowViewModel).</summary>
    [Fact]
    public async Task LoadAsync_ShouldNotModifyActiveWorkItemsList_WhenCalled()
    {
        // Arrange
        var workItem = await _workItemService.CreateAsync(_projectId, "Zu startende Aufgabe", null, ct: TestContext.Current.CancellationToken);
        await _workItemService.StartAsync(workItem.Id, "feature/test", "/tmp/klon", ct: TestContext.Current.CancellationToken);
        var sut = CreateSut();
        var splitList = new ObservableCollection<ActiveWorkItemPanelItem>();
        sut.Initialize(splitList, _ => { });

        // Act
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        // Assert
        sut.ActiveWorkItemsList.Should().BeSameAs(splitList);
        sut.ActiveWorkItemsList.Should().BeEmpty();
    }

    /// <summary>NavigateZuAufgabeCommand delegiert an die über Initialize gesetzte NavigateZuAufgabeAction.</summary>
    [Fact]
    public void NavigateToWorkItemCommand_ShouldInvokeNavigateToWorkItemAction_WhenExecuted()
    {
        // Arrange
        var sut = CreateSut();
        Guid? invokedWorkItemId = null;
        sut.Initialize(new ObservableCollection<ActiveWorkItemPanelItem>(), id => invokedWorkItemId = id);
        var workItemId = Guid.NewGuid();

        // Act
        ((RelayCommand<Guid>)sut.NavigateToWorkItemCommand).Execute(workItemId);

        // Assert
        invokedWorkItemId.Should().Be(workItemId);
    }

    /// <summary>NavigateZuAufgabeCommand wirft keine Ausnahme, wenn Initialize noch nicht aufgerufen wurde.</summary>
    [Fact]
    public void NavigateToWorkItemCommand_ShouldNotThrow_WhenNavigateToWorkItemActionNotSet()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var act = () => ((RelayCommand<Guid>)sut.NavigateToWorkItemCommand).Execute(Guid.NewGuid());

        // Assert
        act.Should().NotThrow();
    }

    /// <summary>AufgabenListenModus startet mit dem Default Datum.</summary>
    [Fact]
    public void WorkItemsListsMode_ShouldDefaultToDate()
    {
        var sut = CreateSut();

        sut.WorkItemListMode.Should().Be(WorkItemListMode.Date);
    }

    /// <summary>AufgabenListenModus ist setzbar und löst PropertyChanged aus.</summary>
    [Fact]
    public void WorkItemsListsMode_ShouldBeSettableAndRaisePropertyChanged()
    {
        var sut = CreateSut();
        var changed = new List<string?>();
        sut.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        sut.WorkItemListMode = WorkItemListMode.Repository;

        sut.WorkItemListMode.Should().Be(WorkItemListMode.Repository);
        changed.Should().Contain(nameof(DashboardViewModel.WorkItemListMode));
    }

    /// <summary>Ein persistierter Dashboard-Modus wird über GespeichertenAufgabenListenModusLadenAsync wiederhergestellt.</summary>
    [Fact]
    public async Task WorkItemsListsMode_ShouldRestorePersistedDashboardMode()
    {
        // Arrange
        var (modeService, provider) = WorkItemsListsModeServiceTestFactory.CreateModeService();
        using (provider)
        {
            using (var scope = provider.CreateScope())
            {
                var settings = scope.ServiceProvider.GetRequiredService<AppSettingService>();
                await settings.SetSettingAsync(AppSettingService.ActiveWorkItemsListModeDashboardKey, "Ki", TestContext.Current.CancellationToken);
            }

            var sut = CreateSut(modeService);

            // Act
            await sut.LoadSavedWorkItemsListModeAsync(TestContext.Current.CancellationToken);

            // Assert
            sut.WorkItemListMode.Should().Be(WorkItemListMode.Ai);
        }
    }

    /// <summary>Eine Modusänderung wird unter dem Dashboard-Schlüssel persistiert und lässt den Menü-Schlüssel unberührt.</summary>
    [Fact]
    public async Task WorkItemsListsMode_ShouldPersistDashboardModeOnChange_WithoutTouchingMenuKey()
    {
        // Arrange
        var (modeService, provider) = WorkItemsListsModeServiceTestFactory.CreateModeService();
        using (provider)
        {
            var sut = CreateSut(modeService);

            // Act
            sut.WorkItemListMode = WorkItemListMode.Repository;

            // Assert (Polling, da das Speichern fire-and-forget läuft)
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
            string? value = null;
            string? menuValue = null;
            while (DateTime.UtcNow < deadline && value != "Repository")
            {
                using var scope = provider.CreateScope();
                var settings = scope.ServiceProvider.GetRequiredService<AppSettingService>();
                value = await settings.GetSettingAsync(AppSettingService.ActiveWorkItemsListModeDashboardKey, TestContext.Current.CancellationToken);
                menuValue = await settings.GetSettingAsync(AppSettingService.ActiveWorkItemsListModeKey, TestContext.Current.CancellationToken);
                if (value != "Repository")
                    await Task.Delay(50, TestContext.Current.CancellationToken);
            }
            value.Should().Be("Repository");
            menuValue.Should().BeNull("der Dashboard-Moduswechsel darf den Menü-Schlüssel nicht schreiben");
        }
    }
}
