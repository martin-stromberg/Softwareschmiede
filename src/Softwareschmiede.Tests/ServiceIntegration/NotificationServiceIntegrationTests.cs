using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.ServiceIntegration;

/// <summary>E2E-Test: Status-Wechsel triggert Banner/Audio basierend auf Modus.</summary>
public sealed class NotificationServiceIntegrationTests : IDisposable
{
    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiededDbContext _db;
    private readonly BenachrichtigungsEinstellungenService _einstellungenService;
    private readonly BenachrichtigungsAuditService _auditService;

    public NotificationServiceIntegrationTests()
    {
        _db = TestDbContextFactory.Create();
        _einstellungenService = new BenachrichtigungsEinstellungenService(_db);
        _auditService = new BenachrichtigungsAuditService(_db, NullLogger<BenachrichtigungsAuditService>.Instance);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public void BenachrichtigungsModus_Banner_IstVorhanden()
    {
        Enum.GetValues<BenachrichtigungsModus>().Should().Contain(BenachrichtigungsModus.Banner);
    }

    [Fact]
    public void BenachrichtigungsModus_Ton_IstVorhanden()
    {
        Enum.GetValues<BenachrichtigungsModus>().Should().Contain(BenachrichtigungsModus.Ton);
    }

    [Fact]
    public void BenachrichtigungsModus_Deaktiviert_IstVorhanden()
    {
        Enum.GetValues<BenachrichtigungsModus>().Should().Contain(BenachrichtigungsModus.Deaktiviert);
    }

    [Fact]
    public async Task BenachrichtigungsEinstellungen_WerdenGespeichert_UndGeladen()
    {
        var benutzerId = "test-user";
        var dto = new BenachrichtigungsEinstellungenDto(BenachrichtigungsModus.Banner, BenachrichtigungsModus.Ton);

        await _einstellungenService.SaveAsync(benutzerId, dto);
        var geladen = await _einstellungenService.GetAsync(benutzerId);

        geladen.BannerModus.Should().Be(BenachrichtigungsModus.Banner);
        geladen.TonModus.Should().Be(BenachrichtigungsModus.Ton);
    }

    [Fact]
    public async Task BenachrichtigungsDispatch_WirdAuditiert_BeiDeaktiviertemModus()
    {
        var benutzerId = "audit-user";
        var aufgabeId = Guid.NewGuid();

        var benutzerService = new Mock<IBenutzerkontextService>();
        benutzerService.Setup(s => s.GetBenutzerId()).Returns(benutzerId);

        var projekt = new Projekt
        {
            Id = Guid.NewGuid(),
            Name = "Notification-Projekt",
            ErstellungsDatum = DateTimeOffset.UtcNow,
            Status = ProjektStatus.Aktiv
        };
        var aufgabe = new Aufgabe
        {
            Id = aufgabeId,
            ProjektId = projekt.Id,
            Titel = "Notification-Aufgabe",
            Status = AufgabeStatus.Neu,
            ErstellungsDatum = DateTimeOffset.UtcNow
        };
        _db.Projekte.Add(projekt);
        _db.Aufgaben.Add(aufgabe);
        await _db.SaveChangesAsync();

        await _einstellungenService.SaveAsync(
            benutzerId,
            new BenachrichtigungsEinstellungenDto(BenachrichtigungsModus.Deaktiviert, BenachrichtigungsModus.Deaktiviert));

        var sut = new BenachrichtigungsService(
            _einstellungenService,
            _auditService,
            benutzerService.Object,
            NullLogger<BenachrichtigungsService>.Instance);

        await sut.DispatchAsync(aufgabeId, AufgabeStatus.InArbeit);

        var logs = _db.BenachrichtigungsDispatchLogs.Where(l => l.AufgabeId == aufgabeId).ToList();
        logs.Should().NotBeEmpty();
        logs.All(l => l.Entscheidung == BenachrichtigungsEntscheidung.Unterdrueckt).Should().BeTrue();
    }
}
