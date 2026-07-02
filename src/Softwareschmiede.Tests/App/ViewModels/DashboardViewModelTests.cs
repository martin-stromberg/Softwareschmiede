using System.Collections.ObjectModel;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
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
    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiededDbContext _db;
    private readonly ProjektService _projektService;
    private readonly AufgabeService _aufgabeService;
    private readonly AufgabeRecoveryService _recoveryService;
    private readonly Guid _projektId = new Guid("33333333-3333-3333-3333-333333333333");

    /// <summary>DashboardViewModelTests.</summary>
    public DashboardViewModelTests()
    {
        _db = TestDbContextFactory.Create();
        _projektService = new ProjektService(_db, NullLogger<ProjektService>.Instance);
        _aufgabeService = new AufgabeService(_db, NullLogger<AufgabeService>.Instance);
        var runningStatusSourceMock = new Mock<IRunningAutomationStatusSource>();
        _recoveryService = new AufgabeRecoveryService(_db, runningStatusSourceMock.Object, NullLogger<AufgabeRecoveryService>.Instance);

        _db.Projekte.Add(new Softwareschmiede.Domain.Entities.Projekt
        {
            Id = _projektId,
            Name = "Testprojekt",
            ErstellungsDatum = DateTimeOffset.UtcNow,
            Status = ProjektStatus.Aktiv
        });
        _db.SaveChanges();
    }

    /// <summary>Dispose.</summary>
    public void Dispose() => _db.Dispose();

    private DashboardViewModel CreateSut() =>
        new(_projektService, _aufgabeService, _recoveryService, NullLogger<DashboardViewModel>.Instance);

    /// <summary>Initialize setzt AktiveAufgabenListe auf die übergebene Instanz, um die gemeinsame Datenquelle des MainWindowViewModel zu übernehmen.</summary>
    [Fact]
    public void Initialize_ShouldSetAktiveAufgabenListeToProvidedInstance_WhenCalled()
    {
        // Arrange
        var sut = CreateSut();
        var geteilteListe = new ObservableCollection<Aufgabe>();

        // Act
        sut.Initialize(geteilteListe, _ => { });

        // Assert
        sut.AktiveAufgabenListe.Should().BeSameAs(geteilteListe);
    }

    /// <summary>LadenAsync lädt aktive Aufgaben nicht mehr selbst, sondern lässt die über Initialize gesetzte AktiveAufgabenListe unverändert (einzige gemeinsame Datenquelle im MainWindowViewModel).</summary>
    [Fact]
    public async Task LadenAsync_ShouldNotModifyAktiveAufgabenListe_WhenCalled()
    {
        // Arrange
        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "Zu startende Aufgabe", null);
        await _aufgabeService.StartenAsync(aufgabe.Id, "feature/test", "/tmp/klon");
        var sut = CreateSut();
        var geteilteListe = new ObservableCollection<Aufgabe>();
        sut.Initialize(geteilteListe, _ => { });

        // Act
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        // Assert
        sut.AktiveAufgabenListe.Should().BeSameAs(geteilteListe);
        sut.AktiveAufgabenListe.Should().BeEmpty();
    }

    /// <summary>NavigateZuAufgabeCommand delegiert an die über Initialize gesetzte NavigateZuAufgabeAction.</summary>
    [Fact]
    public void NavigateZuAufgabeCommand_ShouldInvokeNavigateZuAufgabeAction_WhenExecuted()
    {
        // Arrange
        var sut = CreateSut();
        Guid? aufgerufeneAufgabeId = null;
        sut.Initialize(new ObservableCollection<Aufgabe>(), id => aufgerufeneAufgabeId = id);
        var aufgabeId = Guid.NewGuid();

        // Act
        ((RelayCommand<Guid>)sut.NavigateZuAufgabeCommand).Execute(aufgabeId);

        // Assert
        aufgerufeneAufgabeId.Should().Be(aufgabeId);
    }

    /// <summary>NavigateZuAufgabeCommand wirft keine Ausnahme, wenn Initialize noch nicht aufgerufen wurde.</summary>
    [Fact]
    public void NavigateZuAufgabeCommand_ShouldNotThrow_WhenNavigateZuAufgabeActionNotSet()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var act = () => ((RelayCommand<Guid>)sut.NavigateZuAufgabeCommand).Execute(Guid.NewGuid());

        // Assert
        act.Should().NotThrow();
    }
}
