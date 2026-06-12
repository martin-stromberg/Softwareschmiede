using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.E2E;

/// <summary>E2E-Test: Projekt erstellen und Aufgabe hinzufügen.</summary>
public sealed class ProjectE2ETests : IDisposable
{
    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiededDbContext _db;
    private readonly ProjektService _projektService;
    private readonly AufgabeService _aufgabeService;

    public ProjectE2ETests()
    {
        _db = TestDbContextFactory.Create();
        _projektService = new ProjektService(_db, NullLogger<ProjektService>.Instance);
        _aufgabeService = new AufgabeService(_db, NullLogger<AufgabeService>.Instance);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
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

    [Fact]
    public async Task MehrereProjekte_KoennenErstellt_UndGeladen_Werden()
    {
        await _projektService.CreateAsync("Projekt A", null);
        await _projektService.CreateAsync("Projekt B", null);

        var alle = await _projektService.GetAllAsync();
        alle.Count.Should().BeGreaterOrEqualTo(2);
    }

    [Fact]
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
