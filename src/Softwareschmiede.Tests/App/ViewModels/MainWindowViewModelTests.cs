using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Softwareschmiede.App.Services;
using Softwareschmiede.App.ViewModels;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.App.ViewModels;

/// <summary>Unit-Tests für MainWindowViewModel.</summary>
public sealed class MainWindowViewModelTests : IDisposable
{
    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiededDbContext _db;
    private readonly AufgabeService _aufgabeService;
    private readonly Mock<IServiceProvider> _serviceProviderMock;
    private readonly Guid _projektId = new Guid("22222222-2222-2222-2222-222222222222");

    /// <summary>MainWindowViewModelTests.</summary>
    public MainWindowViewModelTests()
    {
        _db = TestDbContextFactory.Create();
        _aufgabeService = new AufgabeService(_db, NullLogger<AufgabeService>.Instance);
        _serviceProviderMock = new Mock<IServiceProvider>();

        _db.Projekte.Add(new Softwareschmiede.Domain.Entities.Projekt
        {
            Id = _projektId,
            Name = "Testprojekt",
            ErstellungsDatum = DateTimeOffset.UtcNow,
            Status = ProjektStatus.Aktiv
        });
        _db.SaveChanges();

        var projektService = new ProjektService(_db, NullLogger<ProjektService>.Instance);
        var runningStatusSourceMock = new Mock<IRunningAutomationStatusSource>();
        var recoveryService = new AufgabeRecoveryService(_db, runningStatusSourceMock.Object, NullLogger<AufgabeRecoveryService>.Instance);
        var dashboardViewModel = new DashboardViewModel(projektService, _aufgabeService, recoveryService, NullLogger<DashboardViewModel>.Instance);

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(DashboardViewModel)))
            .Returns(dashboardViewModel);
        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(TaskDetailViewModel)))
            .Returns(() => TaskDetailViewModelTestFactory.Create(_db, _aufgabeService));
    }

    /// <summary>Dispose.</summary>
    public void Dispose() => _db.Dispose();

    private MainWindowViewModel CreateSut()
    {
        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        var darkModeService = new DarkModeService(scopeFactoryMock.Object, NullLogger<DarkModeService>.Instance);
        return new MainWindowViewModel(darkModeService, _serviceProviderMock.Object, _aufgabeService, NullLogger<MainWindowViewModel>.Instance);
    }

    /// <summary>AktiveAufgabenAktualisierenAsync befüllt die AktiveAufgabenListe-Collection.</summary>
    [Fact]
    public async Task AktiveAufgabenAktualisierenAsync_ShouldFillObservableCollection_WhenCalled()
    {
        // Arrange
        await _aufgabeService.CreateAsync(_projektId, "Gestartete Aufgabe", null);
        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "Zu startende Aufgabe", null);
        await _aufgabeService.StartenAsync(aufgabe.Id, "feature/test", "/tmp/klon");
        var sut = CreateSut();

        // Act
        await sut.AktiveAufgabenAktualisierenAsync();

        // Assert
        sut.AktiveAufgabenListe.Should().ContainSingle(a => a.Id == aufgabe.Id);
    }

    /// <summary>IsDashboardVisible ist true, wenn CurrentView ein DashboardViewModel ist.</summary>
    [Fact]
    public void IsDashboardVisible_ShouldReturnTrue_WhenCurrentViewIsDashboardViewModel()
    {
        // Arrange
        var sut = CreateSut();

        // Act & Assert (Konstruktor navigiert bereits zum Dashboard)
        sut.IsDashboardVisible.Should().BeTrue();
    }

    /// <summary>IsDashboardVisible ist false, wenn CurrentView kein DashboardViewModel ist.</summary>
    [Fact]
    public async Task IsDashboardVisible_ShouldReturnFalse_WhenCurrentViewIsNotDashboard()
    {
        // Arrange
        var sut = CreateSut();
        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "Detail-Aufgabe", null);

        // Act
        ((RelayCommand<Guid>)sut.NavigateZuAufgabeCommand).Execute(aufgabe.Id);

        // Assert
        sut.IsDashboardVisible.Should().BeFalse();
    }

    /// <summary>NavigateZuAufgabeCommand erstellt eine neue TaskDetailViewModel-Instanz und setzt sie als CurrentView.</summary>
    [Fact]
    public async Task NavigateZuAufgabeCommand_ShouldCreateTaskDetailViewModelAndSetCurrentView_WhenExecutedWithAufgabeId()
    {
        // Arrange
        var sut = CreateSut();
        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "Aufgabe für Navigation", null);

        // Act
        ((RelayCommand<Guid>)sut.NavigateZuAufgabeCommand).Execute(aufgabe.Id);

        // Assert
        sut.CurrentView.Should().BeOfType<TaskDetailViewModel>();
        ((TaskDetailViewModel)sut.CurrentView!).AufgabeId.Should().Be(aufgabe.Id);
    }

    /// <summary>Der CurrentView-Setter löst AktiveAufgabenAktualisierenAsync per SafeFireAndForget aus, ohne dass der Aufrufer den Task awaiten muss.</summary>
    [Fact]
    public async Task CurrentView_Setter_UsesFireAndForgetSafely()
    {
        // Arrange
        var sut = CreateSut();
        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "FireAndForget-Aufgabe", null);
        await _aufgabeService.StartenAsync(aufgabe.Id, "feature/fire-and-forget", "/tmp/klon");

        // Act: CurrentView-Wechsel löst im Setter SafeFireAndForget(AktiveAufgabenAktualisierenAsync) aus
        ((RelayCommand<Guid>)sut.NavigateZuAufgabeCommand).Execute(aufgabe.Id);

        // Assert: die Aktualisierung läuft asynchron im Hintergrund und füllt die Liste
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (DateTime.UtcNow < deadline && !sut.AktiveAufgabenListe.Any(a => a.Id == aufgabe.Id))
            await Task.Delay(50);

        sut.AktiveAufgabenListe.Should().Contain(a => a.Id == aufgabe.Id,
            "der CurrentView-Setter muss AktiveAufgabenAktualisierenAsync per SafeFireAndForget auslösen");
    }

    /// <summary>NavigateToDashboard teilt die AktiveAufgabenListe mit dem DashboardViewModel, statt eine eigene Abfrage im Dashboard auszulösen (einzige gemeinsame Datenquelle).</summary>
    [Fact]
    public void NavigateToDashboard_ShouldShareAktiveAufgabenListeWithDashboardViewModel_WhenCalled()
    {
        // Arrange & Act (Konstruktor navigiert bereits zum Dashboard)
        var sut = CreateSut();

        // Assert
        var dashboardViewModel = (DashboardViewModel)sut.CurrentView!;
        dashboardViewModel.AktiveAufgabenListe.Should().BeSameAs(sut.AktiveAufgabenListe);
    }

    /// <summary>NavigateToDashboard setzt die NavigateZuAufgabeAction des DashboardViewModel, sodass dessen NavigateZuAufgabeCommand zur Aufgabendetailansicht navigiert (Delegate-Muster statt versteckter Kopplung).</summary>
    [Fact]
    public async Task NavigateToDashboard_ShouldWireNavigateZuAufgabeActionOnDashboardViewModel_WhenCalled()
    {
        // Arrange
        var sut = CreateSut();
        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "Aufgabe für Dashboard-Navigation", null);
        var dashboardViewModel = (DashboardViewModel)sut.CurrentView!;

        // Act
        ((RelayCommand<Guid>)dashboardViewModel.NavigateZuAufgabeCommand).Execute(aufgabe.Id);

        // Assert
        sut.CurrentView.Should().BeOfType<TaskDetailViewModel>();
        ((TaskDetailViewModel)sut.CurrentView!).AufgabeId.Should().Be(aufgabe.Id);
    }
}
