using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.Domain.Enums;

/// <summary>Tests für Statusübergänge von Aufgaben.</summary>
public sealed class AufgabeStatusTransitionTests : IDisposable
{
    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiededDbContext _db;
    private readonly AufgabeService _sut;
    private readonly Guid _projektId = Guid.NewGuid();

    public AufgabeStatusTransitionTests()
    {
        _db = TestDbContextFactory.Create();
        _sut = new AufgabeService(_db, new Mock<ILogger<AufgabeService>>().Object);

        _db.Projekte.Add(new Projekt
        {
            Id = _projektId,
            Name = "Transition-Testprojekt",
            ErstellungsDatum = DateTimeOffset.UtcNow,
            Status = ProjektStatus.Aktiv
        });
        _db.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    /// <summary>Erlaubte Übergänge: Neu → ArbeitsverzeichnisEingerichtet → Gestartet → InArbeit → Beendet.</summary>
    [Fact]
    public async Task TestStatusTransitions_AllowedSequence_Succeeds()
    {
        var aufgabe = await _sut.CreateAsync(_projektId, "Transition-Test", null);

        await _sut.SetStatusAsync(aufgabe.Id, AufgabeStatus.ArbeitsverzeichnisEingerichtet);
        await _sut.SetStatusAsync(aufgabe.Id, AufgabeStatus.Gestartet);
        await _sut.SetStatusAsync(aufgabe.Id, AufgabeStatus.InArbeit);
        await _sut.SetStatusAsync(aufgabe.Id, AufgabeStatus.Beendet);

        var result = await _sut.GetByIdAsync(aufgabe.Id);
        result!.Status.Should().Be(AufgabeStatus.Beendet);
    }

    /// <summary>Erlaubter Übergang: InArbeit → Wartend → InArbeit → Beendet.</summary>
    [Fact]
    public async Task TestStatusTransitions_WartendSequence_Succeeds()
    {
        var aufgabe = await ErstelleAufgabeInStatus(AufgabeStatus.InArbeit);

        await _sut.SetStatusAsync(aufgabe.Id, AufgabeStatus.Wartend);
        await _sut.SetStatusAsync(aufgabe.Id, AufgabeStatus.InArbeit);
        await _sut.SetStatusAsync(aufgabe.Id, AufgabeStatus.Beendet);

        var result = await _sut.GetByIdAsync(aufgabe.Id);
        result!.Status.Should().Be(AufgabeStatus.Beendet);
    }

    /// <summary>Übergang von jedem Status nach Archiviert ist erlaubt.</summary>
    [Theory]
    [InlineData(AufgabeStatus.Neu)]
    [InlineData(AufgabeStatus.ArbeitsverzeichnisEingerichtet)]
    [InlineData(AufgabeStatus.Gestartet)]
    [InlineData(AufgabeStatus.InArbeit)]
    [InlineData(AufgabeStatus.Wartend)]
    [InlineData(AufgabeStatus.Beendet)]
    public async Task TestStatusTransitions_AnyStatusToArchiviert_IsAllowed(AufgabeStatus fromStatus)
    {
        var aufgabe = await ErstelleAufgabeInStatus(fromStatus);

        var act = () => _sut.SetStatusAsync(aufgabe.Id, AufgabeStatus.Archiviert);
        await act.Should().NotThrowAsync();
    }

    /// <summary>Ungültiger Übergang Neu → InArbeit wirft InvalidStatusTransitionException.</summary>
    [Fact]
    public async Task TestStatusValidation_InvalidTransition_ThrowsException()
    {
        var aufgabe = await _sut.CreateAsync(_projektId, "Ungültiger Übergang", null);

        var act = () => _sut.SetStatusAsync(aufgabe.Id, AufgabeStatus.InArbeit);
        await act.Should().ThrowAsync<InvalidStatusTransitionException>();
    }

    /// <summary>Ungültiger Übergang Beendet → InArbeit wirft InvalidStatusTransitionException.</summary>
    [Fact]
    public async Task TestStatusValidation_BeendetToInArbeit_ThrowsException()
    {
        var aufgabe = await ErstelleAufgabeInStatus(AufgabeStatus.Beendet);

        var act = () => _sut.SetStatusAsync(aufgabe.Id, AufgabeStatus.InArbeit);
        await act.Should().ThrowAsync<InvalidStatusTransitionException>();
    }

    /// <summary>Ungültiger Übergang Archiviert → Neu wirft InvalidStatusTransitionException.</summary>
    [Fact]
    public async Task TestStatusValidation_ArchiviertToNeu_ThrowsException()
    {
        var aufgabe = await ErstelleAufgabeInStatus(AufgabeStatus.Archiviert);

        var act = () => _sut.SetStatusAsync(aufgabe.Id, AufgabeStatus.Neu);
        await act.Should().ThrowAsync<InvalidStatusTransitionException>();
    }

    private async Task<Aufgabe> ErstelleAufgabeInStatus(AufgabeStatus status)
    {
        var aufgabe = new Aufgabe
        {
            Id = Guid.NewGuid(),
            ProjektId = _projektId,
            Titel = $"Aufgabe-{status}",
            Status = status,
            ErstellungsDatum = DateTimeOffset.UtcNow
        };
        _db.Aufgaben.Add(aufgabe);
        await _db.SaveChangesAsync();
        return aufgabe;
    }
}
