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
    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiedeDbContext _db;
    private readonly BenachrichtigungsEinstellungenService _einstellungenService;
    private readonly BenachrichtigungsAuditService _auditService;

    /// <summary>NotificationServiceIntegrationTests.</summary>
    public NotificationServiceIntegrationTests()
    {
        _db = TestDbContextFactory.Create();
        _einstellungenService = new BenachrichtigungsEinstellungenService(_db);
        _auditService = new BenachrichtigungsAuditService(_db, NullLogger<BenachrichtigungsAuditService>.Instance);
    }

    /// <summary>Dispose.</summary>
    public void Dispose() => _db.Dispose();

    /// <summary><summary>BenachrichtigungsModus_Banner_IstVorhanden.</summary>.</summary>
    [Fact]
    public void BenachrichtigungsModus_Banner_IstVorhanden()
    {
        Enum.GetValues<NotificationMode>().Should().Contain(NotificationMode.Banner);
    }

    /// <summary><summary>BenachrichtigungsModus_Ton_IstVorhanden.</summary>.</summary>
    [Fact]
    public void BenachrichtigungsModus_Ton_IstVorhanden()
    {
        Enum.GetValues<NotificationMode>().Should().Contain(NotificationMode.Sound);
    }

    /// <summary><summary>BenachrichtigungsModus_Deaktiviert_IstVorhanden.</summary>.</summary>
    [Fact]
    public void BenachrichtigungsModus_Deaktiviert_IstVorhanden()
    {
        Enum.GetValues<NotificationMode>().Should().Contain(NotificationMode.Disabled);
    }

    /// <summary><summary>BenachrichtigungsEinstellungen_WerdenGespeichert_UndGeladen.</summary>.</summary>
    [Fact]
    public async Task BenachrichtigungsEinstellungen_WerdenGespeichert_UndGeladen()
    {
        var benutzerId = "test-user";
        var dto = new BenachrichtigungsEinstellungenDto(NotificationMode.Banner, NotificationMode.Sound);

        await _einstellungenService.SaveAsync(benutzerId, dto, TestContext.Current.CancellationToken);
        var geladen = await _einstellungenService.GetAsync(benutzerId, TestContext.Current.CancellationToken);

        geladen.BannerMode.Should().Be(NotificationMode.Banner);
        geladen.SoundMode.Should().Be(NotificationMode.Sound);
    }

    /// <summary><summary>BenachrichtigungsDispatch_WirdAuditiert_BeiDeaktiviertemModus.</summary>.</summary>
    [Fact]
    public async Task BenachrichtigungsDispatch_WirdAuditiert_BeiDeaktiviertemModus()
    {
        var benutzerId = "audit-user";
        var workItemId = Guid.NewGuid();

        var benutzerService = new Mock<IUserContextService>();
        benutzerService.Setup(s => s.GetUserId()).Returns(benutzerId);

        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = "Notification-Projekt",
            CreatedAt = DateTimeOffset.UtcNow,
            Status = ProjectStatus.Active
        };
        var workItem = new WorkItem
        {
            Id = workItemId,
            ProjectId = project.Id,
            Title = "Notification-Aufgabe",
            Status = WorkItemStatus.New,
            CreatedAt = DateTimeOffset.UtcNow
        };
        _db.Projects.Add(project);
        _db.WorkItems.Add(workItem);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await _einstellungenService.SaveAsync(benutzerId, new BenachrichtigungsEinstellungenDto(NotificationMode.Disabled, NotificationMode.Disabled), TestContext.Current.CancellationToken);

        var sut = new BenachrichtigungsService(
            _einstellungenService,
            _auditService,
            benutzerService.Object,
            NullLogger<BenachrichtigungsService>.Instance);

        await sut.DispatchAsync(workItemId, WorkItemStatus.Started, TestContext.Current.CancellationToken);

        var logs = _db.NotificationDispatchLogs.Where(l => l.WorkItemId == workItemId).ToList();
        logs.Should().NotBeEmpty();
        logs.All(l => l.Decision == NotificationDecision.Suppressed).Should().BeTrue();
    }
}
