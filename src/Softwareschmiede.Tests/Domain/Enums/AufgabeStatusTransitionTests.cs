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

    /// <summary>Direkter Übergang Neu → Gestartet ist erlaubt.</summary>
    [Fact]
    public async Task TestStatusTransitions_NeuToGestartet_Direct_Succeeds()
    {
        var aufgabe = await _sut.CreateAsync(_projektId, "Transition-Test", null);

        await _sut.SetStatusAsync(aufgabe.Id, AufgabeStatus.Gestartet);

        var result = await _sut.GetByIdAsync(aufgabe.Id);
        result!.Status.Should().Be(AufgabeStatus.Gestartet);
    }

    /// <summary>ArbeitsverzeichnisEingerichtet existiert nicht mehr im Enum.</summary>
    [Fact]
    public void TestStatusTransitions_OldArbeitsverzeichnisStatus_Removed()
    {
        Enum.GetNames<AufgabeStatus>().Should().NotContain("ArbeitsverzeichnisEingerichtet");
    }

    /// <summary>InArbeit existiert nicht mehr im Enum.</summary>
    [Fact]
    public void TestStatusTransitions_OldInArbeitStatus_Removed()
    {
        Enum.GetNames<AufgabeStatus>().Should().NotContain("InArbeit");
    }

    /// <summary>Übergang Gestartet → Wartend ist erlaubt.</summary>
    [Fact]
    public async Task TestStatusValidation_GestartetToWartend_IsAllowed()
    {
        var aufgabe = await ErstelleAufgabeInStatus(AufgabeStatus.Gestartet);

        var act = () => _sut.SetStatusAsync(aufgabe.Id, AufgabeStatus.Wartend);
        await act.Should().NotThrowAsync();
    }

    /// <summary>Übergang Gestartet → Beendet ist erlaubt.</summary>
    [Fact]
    public async Task TestStatusValidation_GestartetToBeendet_IsAllowed()
    {
        var aufgabe = await ErstelleAufgabeInStatus(AufgabeStatus.Gestartet);

        var act = () => _sut.SetStatusAsync(aufgabe.Id, AufgabeStatus.Beendet);
        await act.Should().NotThrowAsync();
    }

    /// <summary>Erlaubter Übergang: Gestartet → Wartend → Gestartet → Beendet.</summary>
    [Fact]
    public async Task TestStatusTransitions_WartendSequence_Succeeds()
    {
        var aufgabe = await ErstelleAufgabeInStatus(AufgabeStatus.Gestartet);

        await _sut.SetStatusAsync(aufgabe.Id, AufgabeStatus.Wartend);
        await _sut.SetStatusAsync(aufgabe.Id, AufgabeStatus.Gestartet);
        await _sut.SetStatusAsync(aufgabe.Id, AufgabeStatus.Beendet);

        var result = await _sut.GetByIdAsync(aufgabe.Id);
        result!.Status.Should().Be(AufgabeStatus.Beendet);
    }

    /// <summary>Übergang von jedem Status nach Archiviert ist erlaubt.</summary>
    [Theory]
    [InlineData(AufgabeStatus.Neu)]
    [InlineData(AufgabeStatus.Gestartet)]
    [InlineData(AufgabeStatus.Wartend)]
    [InlineData(AufgabeStatus.Beendet)]
    public async Task TestStatusTransitions_AnyStatusToArchiviert_IsAllowed(AufgabeStatus fromStatus)
    {
        var aufgabe = await ErstelleAufgabeInStatus(fromStatus);

        var act = () => _sut.SetStatusAsync(aufgabe.Id, AufgabeStatus.Archiviert);
        await act.Should().NotThrowAsync();
    }

    /// <summary>Ungültiger Übergang Neu → Beendet wirft InvalidStatusTransitionException.</summary>
    [Fact]
    public async Task TestStatusValidation_InvalidTransition_ThrowsException()
    {
        var aufgabe = await _sut.CreateAsync(_projektId, "Ungültiger Übergang", null);

        var act = () => _sut.SetStatusAsync(aufgabe.Id, AufgabeStatus.Beendet);
        await act.Should().ThrowAsync<InvalidStatusTransitionException>();
    }

    /// <summary>Ungültiger Übergang Beendet → Gestartet wirft InvalidStatusTransitionException.</summary>
    [Fact]
    public async Task TestStatusValidation_BeendetToGestartet_ThrowsException()
    {
        var aufgabe = await ErstelleAufgabeInStatus(AufgabeStatus.Beendet);

        var act = () => _sut.SetStatusAsync(aufgabe.Id, AufgabeStatus.Gestartet);
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
