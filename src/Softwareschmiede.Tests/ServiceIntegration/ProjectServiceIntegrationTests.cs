using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.ServiceIntegration;

/// <summary>E2E-Test: Projekt erstellen und Aufgabe hinzufügen.</summary>
public sealed class ProjectServiceIntegrationTests : IDisposable
{
    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiededDbContext _db;
    private readonly ProjektService _projektService;
    private readonly AufgabeService _aufgabeService;

    /// <summary>ProjectServiceIntegrationTests.</summary>
    public ProjectServiceIntegrationTests()
    {
        _db = TestDbContextFactory.Create();
        _projektService = new ProjektService(_db, NullLogger<ProjektService>.Instance);
        _aufgabeService = new AufgabeService(_db, NullLogger<AufgabeService>.Instance);
    }

    /// <summary>Dispose.</summary>
    public void Dispose() => _db.Dispose();

    /// <summary><summary>ProjektErstellen_UndAufgabeHinzufuegen_StatusIstNeu.</summary>.</summary>
    [Fact]
    /// <summary>ProjektErstellen_UndAufgabeHinzufuegen_StatusIstNeu.</summary>
    public async Task ProjektErstellen_UndAufgabeHinzufuegen_StatusIstNeu()
    {
        var projekt = await _projektService.CreateAsync("Neues Projekt", null);
        projekt.Id.Should().NotBeEmpty();
        projekt.Name.Should().Be("Neues Projekt");

        var aufgabe = await _aufgabeService.CreateAsync(projekt.Id, "Erste Aufgabe", "Beschreibung");
        aufgabe.Id.Should().NotBeEmpty();
        aufgabe.ProjektId.Should().Be(projekt.Id);
        aufgabe.Status.Should().Be(AufgabeStatus.Neu);
    }

    /// <summary><summary>MehrereProjekte_KoennenErstellt_UndGeladen_Werden.</summary>.</summary>
    [Fact]
    /// <summary>MehrereProjekte_KoennenErstellt_UndGeladen_Werden.</summary>
    public async Task MehrereProjekte_KoennenErstellt_UndGeladen_Werden()
    {
        await _projektService.CreateAsync("Projekt A", null);
        await _projektService.CreateAsync("Projekt B", null);

        var alle = await _projektService.GetAllAsync();
        alle.Count.Should().BeGreaterThanOrEqualTo(2);
    }

    /// <summary><summary>Aufgabe_KannInProjektGeladen_Werden.</summary>.</summary>
    [Fact]
    /// <summary>Aufgabe_KannInProjektGeladen_Werden.</summary>
    public async Task Aufgabe_KannInProjektGeladen_Werden()
    {
        var projekt = await _projektService.CreateAsync("Test Projekt", null);
        await _aufgabeService.CreateAsync(projekt.Id, "Aufgabe 1", null);
        await _aufgabeService.CreateAsync(projekt.Id, "Aufgabe 2", null);

        var aufgaben = await _aufgabeService.GetByProjektAsync(projekt.Id);
        aufgaben.Should().HaveCount(2);
        aufgaben.All(a => a.ProjektId == projekt.Id).Should().BeTrue();
    }
}
