using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>Tests für <see cref="ScmEventExecutionLimitService"/> (Issue 410, Schritt 2).</summary>
public sealed class ScmEventExecutionLimitServiceTests : IDisposable
{
    private const string PrefixA = "Softwareschmiede.TestA";
    private const string PrefixB = "Softwareschmiede.TestB";

    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiedeDbContext _db;
    private readonly AppSettingService _settings;
    private readonly Mock<ICliExecutionLimitSource> _cli;
    private readonly ScmEventExecutionLimitService _sut;
    private readonly ScmStartSlotReservationPool _pool = new();
    private readonly Guid _projectId = new Guid("77777777-7777-7777-7777-777777777777");

    /// <summary>ScmEventExecutionLimitServiceTests.</summary>
    public ScmEventExecutionLimitServiceTests()
    {
        _db = TestDbContextFactory.Create();
        _settings = new AppSettingService(_db, NullLogger<AppSettingService>.Instance);
        _cli = new Mock<ICliExecutionLimitSource>();
        _cli.Setup(x => x.GetRunningWorkItemIds()).Returns([]);
        _sut = new ScmEventExecutionLimitService(_db, _settings, _cli.Object, TimeProvider.System, _pool);

        _db.Projects.Add(new Project
        {
            Id = _projectId,
            Name = "Limit-Testprojekt",
            CreatedAt = DateTimeOffset.UtcNow,
            Status = ProjectStatus.Active
        });
        _db.SaveChanges();
    }

    /// <summary>Dispose.</summary>
    public void Dispose()
    {
        _db.Dispose();
    }

    private async Task<Guid> RunningWorkItemAsync(string title, string? aiPluginPrefix,
        Mock<IScmPromptSessionAdapter>? session = null, DateTimeOffset? pausedUntilUtc = null)
    {
        var workItem = new WorkItem
        {
            Id = Guid.NewGuid(),
            Title = title,
            ProjectId = _projectId,
            Status = WorkItemStatus.Started,
            ExecutionStatus = WorkItemExecutionStatus.Active,
            AiPluginPrefix = aiPluginPrefix,
            PausedUntilUtc = pausedUntilUtc
        };
        _db.WorkItems.Add(workItem);
        await _db.SaveChangesAsync();
        _cli.Setup(x => x.GetScmPromptSession(workItem.Id)).Returns(session?.Object);
        return workItem.Id;
    }

    private void SetRunning(params Guid[] ids)
        => _cli.Setup(x => x.GetRunningWorkItemIds()).Returns(ids.ToList());

    private async Task AddEventAsync(Guid workItemId, string status, long? nextAttemptUtc = null)
    {
        _db.ScmIncomingEvents.Add(new ScmIncomingEvent { WorkItemId = workItemId, Status = status, NextAttemptUtc = nextAttemptUtc });
        await _db.SaveChangesAsync();
    }

    private static Mock<IScmPromptSessionAdapter> LiveSession(string pluginPrefix,
        ScmPromptReadyStatus readyStatus = ScmPromptReadyStatus.Unknown, bool isAlive = true)
    {
        var session = new Mock<IScmPromptSessionAdapter>();
        session.SetupGet(x => x.IsAlive).Returns(isAlive);
        session.SetupGet(x => x.PluginPrefix).Returns(pluginPrefix);
        session.SetupGet(x => x.ReadyStatus).Returns(readyStatus);
        return session;
    }

    /// <summary>Ohne gesetzte Grenzen ist jeder Start frei.</summary>
    [Fact]
    public async Task GetStartBlockReasonAsync_WithoutBoundary_ReturnsNull()
    {
        SetRunning(await RunningWorkItemAsync("A", PrefixA));

        (await _sut.GetStartBlockReasonAsync(PrefixA, TestContext.Current.CancellationToken)).Should().BeNull();
    }

    /// <summary>Ungültige Grenzwerte (0, negativ, Text) gelten als unbegrenzt.</summary>
    [Theory]
    [InlineData("0")]
    [InlineData("-3")]
    [InlineData("abc")]
    public async Task GetStartBlockReasonAsync_InvalidBoundary_AppliesAsUnlimited(string value)
    {
        await _settings.SetSettingAsync(ScmEventExecutionLimitService.GlobalLimitKey, value, TestContext.Current.CancellationToken);
        await _settings.SetSettingAsync(ScmEventExecutionLimitService.PluginLimitKey(PrefixA), value, TestContext.Current.CancellationToken);
        SetRunning(await RunningWorkItemAsync("A", PrefixA));

        (await _sut.GetStartBlockReasonAsync(PrefixA, TestContext.Current.CancellationToken)).Should().BeNull();
    }

    /// <summary>Erreichtes globales Limit sperrt jeden Plugin-Prefix.</summary>
    [Fact]
    public async Task GetStartBlockReasonAsync_GlobalLimitReaches_Locks()
    {
        await _settings.SetSettingAsync(ScmEventExecutionLimitService.GlobalLimitKey, "1", TestContext.Current.CancellationToken);
        SetRunning(await RunningWorkItemAsync("A", PrefixA));

        var reason = await _sut.GetStartBlockReasonAsync(PrefixB, TestContext.Current.CancellationToken);

        reason.Should().Contain("globale Limit").And.Contain("1");
    }

    /// <summary>Unter dem globalen Limit bleibt der Start frei.</summary>
    [Fact]
    public async Task GetStartBlockReasonAsync_GlobalLimitNotReaches_ReturnsNull()
    {
        await _settings.SetSettingAsync(ScmEventExecutionLimitService.GlobalLimitKey, "2", TestContext.Current.CancellationToken);
        SetRunning(await RunningWorkItemAsync("A", PrefixA));

        (await _sut.GetStartBlockReasonAsync(PrefixB, TestContext.Current.CancellationToken)).Should().BeNull();
    }

    /// <summary>Erreichtes Plugin-Limit sperrt nur dieses Plugin.</summary>
    [Fact]
    public async Task GetStartBlockReasonAsync_PluginLimitReaches_LocksOnlyThisPlugin()
    {
        await _settings.SetSettingAsync(ScmEventExecutionLimitService.PluginLimitKey(PrefixA), "1", TestContext.Current.CancellationToken);
        SetRunning(await RunningWorkItemAsync("A", PrefixA));

        (await _sut.GetStartBlockReasonAsync(PrefixA, TestContext.Current.CancellationToken)).Should().Contain($"'{PrefixA}'");
        (await _sut.GetStartBlockReasonAsync(PrefixB, TestContext.Current.CancellationToken)).Should().BeNull();
    }

    /// <summary>Beide Grenzen wirken gleichzeitig; das globale Limit meldet zuerst.</summary>
    [Fact]
    public async Task GetStartBlockReasonAsync_BothLimitReaches_ReportsGlobalLimit()
    {
        await _settings.SetSettingAsync(ScmEventExecutionLimitService.GlobalLimitKey, "1", TestContext.Current.CancellationToken);
        await _settings.SetSettingAsync(ScmEventExecutionLimitService.PluginLimitKey(PrefixA), "1", TestContext.Current.CancellationToken);
        SetRunning(await RunningWorkItemAsync("A", PrefixA));

        (await _sut.GetStartBlockReasonAsync(PrefixA, TestContext.Current.CancellationToken)).Should().Contain("globale Limit");
    }

    /// <summary>Gemischte Plugins zählen getrennt: Fremde Läufe belegen kein Plugin-Limit.</summary>
    [Fact]
    public async Task GetStartBlockReasonAsync_MixedPlugins_CountsOnlyOwnPrefix()
    {
        await _settings.SetSettingAsync(ScmEventExecutionLimitService.PluginLimitKey(PrefixA), "2", TestContext.Current.CancellationToken);
        SetRunning(
            await RunningWorkItemAsync("A1", PrefixA),
            await RunningWorkItemAsync("B1", PrefixB),
            await RunningWorkItemAsync("B2", PrefixB));

        (await _sut.GetStartBlockReasonAsync(PrefixA, TestContext.Current.CancellationToken)).Should().BeNull();
        await _settings.SetSettingAsync(ScmEventExecutionLimitService.PluginLimitKey(PrefixA), "1", TestContext.Current.CancellationToken);
        (await _sut.GetStartBlockReasonAsync(PrefixA, TestContext.Current.CancellationToken)).Should().NotBeNull();
    }

    /// <summary>Eine lebende SCM-Sitzung attribuiert zum tatsächlich gestarteten Plugin, nicht zum gespeicherten.</summary>
    [Fact]
    public async Task GetStartBlockReasonAsync_LiveSession_CountsActualPlugin()
    {
        // Aufgabe hat PrefixA gespeichert, läuft aber tatsächlich mit PrefixB (Fallback).
        var session = LiveSession(PrefixB);
        await _settings.SetSettingAsync(ScmEventExecutionLimitService.PluginLimitKey(PrefixA), "1", TestContext.Current.CancellationToken);
        await _settings.SetSettingAsync(ScmEventExecutionLimitService.PluginLimitKey(PrefixB), "1", TestContext.Current.CancellationToken);
        SetRunning(await RunningWorkItemAsync("A", PrefixA, session));

        (await _sut.GetStartBlockReasonAsync(PrefixA, TestContext.Current.CancellationToken)).Should().BeNull();
        (await _sut.GetStartBlockReasonAsync(PrefixB, TestContext.Current.CancellationToken)).Should().NotBeNull();
    }

    /// <summary>Beendete/entfernte SCM-Sitzungen fallen auf das gespeicherte Plugin der Aufgabe zurück.</summary>
    [Fact]
    public async Task GetStartBlockReasonAsync_SessionDead_FallsOnWorkItemsPrefixBack()
    {
        var session = new Mock<IScmPromptSessionAdapter>();
        session.SetupGet(x => x.IsAlive).Returns(false);
        session.SetupGet(x => x.PluginPrefix).Returns(PrefixB);
        await _settings.SetSettingAsync(ScmEventExecutionLimitService.PluginLimitKey(PrefixA), "1", TestContext.Current.CancellationToken);
        SetRunning(await RunningWorkItemAsync("A", PrefixA, session));

        (await _sut.GetStartBlockReasonAsync(PrefixA, TestContext.Current.CancellationToken)).Should().NotBeNull();
    }

    /// <summary>Aufgaben ohne Plugin-Zuordnung zählen global, aber für kein Plugin-Limit.</summary>
    [Fact]
    public async Task GetStartBlockReasonAsync_WithoutPrefix_CountsOnlyGlobal()
    {
        await _settings.SetSettingAsync(ScmEventExecutionLimitService.PluginLimitKey(PrefixA), "1", TestContext.Current.CancellationToken);
        await _settings.SetSettingAsync(ScmEventExecutionLimitService.GlobalLimitKey, "2", TestContext.Current.CancellationToken);
        SetRunning(await RunningWorkItemAsync("A", null));

        (await _sut.GetStartBlockReasonAsync(PrefixA, TestContext.Current.CancellationToken)).Should().BeNull();
    }

    /// <summary>Reine Auswertung: globales Limit schlägt Plugin-Limit; frei bei Platz.</summary>
    [Fact]
    public void GetStartBlockReason_ChecksGlobalBeforePluginLimit()
    {
        var occupancy = new CliExecutionOccupancy(2, new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            [PrefixA] = 2
        });

        ScmEventExecutionLimitService.GetStartBlockReason(PrefixA, globalLimit: 2, pluginLimit: 1, occupancy)
            .Should().Contain("globale Limit");
        ScmEventExecutionLimitService.GetStartBlockReason(PrefixA, globalLimit: 3, pluginLimit: 1, occupancy)
            .Should().Contain($"'{PrefixA}'");
        ScmEventExecutionLimitService.GetStartBlockReason(PrefixB, globalLimit: 3, pluginLimit: 1, occupancy)
            .Should().BeNull();
        ScmEventExecutionLimitService.GetStartBlockReason(PrefixA, globalLimit: null, pluginLimit: null, occupancy)
            .Should().BeNull();
    }

    /// <summary>Die Plugin-Zählung ignoriert Groß-/Kleinschreibung des Prefix.</summary>
    [Fact]
    public void ForPlugin_IgnoresLargeLowercase()
    {
        var occupancy = new CliExecutionOccupancy(1, new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            [PrefixA] = 1
        });

        occupancy.ForPlugin(PrefixA.ToLowerInvariant()).Should().Be(1);
        occupancy.ForPlugin("").Should().Be(0);
    }

    /// <summary>Bei unbegrenzter Konfiguration ist die Zulassung frei und belegt keinen Platz.</summary>
    [Fact]
    public async Task CheckAndReserveStartSlotAsync_WithoutBoundary_FreeWithoutReservation()
    {
        var admission = await _sut.CheckAndReserveStartSlotAsync(PrefixA, TestContext.Current.CancellationToken);

        admission.WaitReason.Should().BeNull();
        admission.Reservation.Should().BeNull();
    }

    /// <summary>Eine gehaltene Reservierung belegt den Platz: Der nächste Start wartet,
    /// die Freigabe gibt den Platz wieder frei — ohne dass je ein Prozess lief.</summary>
    [Fact]
    public async Task CheckAndReserveStartSlotAsync_Reservation_OccupiedGlobalSlot()
    {
        await _settings.SetSettingAsync(ScmEventExecutionLimitService.GlobalLimitKey, "1", TestContext.Current.CancellationToken);

        var first = await _sut.CheckAndReserveStartSlotAsync(PrefixA, TestContext.Current.CancellationToken);
        first.WaitReason.Should().BeNull();
        first.Reservation.Should().NotBeNull();

        // Die Reservierung zählt sofort als belegter Platz — auch für die reine Prüfung.
        (await _sut.CheckAndReserveStartSlotAsync(PrefixB, TestContext.Current.CancellationToken)).WaitReason.Should().Contain("globale Limit");
        (await _sut.GetStartBlockReasonAsync(PrefixB, TestContext.Current.CancellationToken)).Should().Contain("globale Limit");

        first.Reservation!.Dispose();

        var third = await _sut.CheckAndReserveStartSlotAsync(PrefixB, TestContext.Current.CancellationToken);
        third.WaitReason.Should().BeNull();
        third.Reservation!.Dispose();
    }

    /// <summary>Plugin-Reservierungen belegen nur das eigene Plugin-Limit.</summary>
    [Fact]
    public async Task CheckAndReserveStartSlotAsync_Reservation_OccupiedOnlyOwnPluginLimit()
    {
        await _settings.SetSettingAsync(ScmEventExecutionLimitService.PluginLimitKey(PrefixA), "1", TestContext.Current.CancellationToken);

        var reservation = (await _sut.CheckAndReserveStartSlotAsync(PrefixA, TestContext.Current.CancellationToken)).Reservation;
        reservation.Should().NotBeNull();

        (await _sut.CheckAndReserveStartSlotAsync(PrefixA, TestContext.Current.CancellationToken)).WaitReason.Should().Contain($"'{PrefixA}'");
        (await _sut.CheckAndReserveStartSlotAsync(PrefixB, TestContext.Current.CancellationToken)).WaitReason.Should().BeNull();

        reservation!.Dispose();
        (await _sut.CheckAndReserveStartSlotAsync(PrefixA, TestContext.Current.CancellationToken)).Reservation!.Dispose();
    }

    /// <summary>Atomarität der Zulassung: Viele gleichzeitige Versuche auf getrennten
    /// Service-Instanzen (eigene DbContexts wie in eigenen DI-Scopes) erhalten bei
    /// globalem Limit 1 genau eine Reservierung; alle übrigen warten mit Limit-Grund.</summary>
    [Fact]
    public async Task CheckAndReserveStartSlotAsync_AttemptsParallel_OnlyAReservation()
    {
        const int attempt = 8;
        await _settings.SetSettingAsync(ScmEventExecutionLimitService.GlobalLimitKey, "1", TestContext.Current.CancellationToken);
        var services = Enumerable.Range(0, attempt).Select(_ =>
        {
            var db = TestDbContextFactory.Create();
            var settings = new AppSettingService(db, NullLogger<AppSettingService>.Instance);
            settings.SetSettingAsync(ScmEventExecutionLimitService.GlobalLimitKey, "1").GetAwaiter().GetResult();
            return (Service: new ScmEventExecutionLimitService(db, settings, _cli.Object, TimeProvider.System, _pool), Db: db);
        }).ToList();
        try
        {
            var zulassungen = await Task.WhenAll(services.Select(x =>
                Task.Run(() => x.Service.CheckAndReserveStartSlotAsync(PrefixA))));

            zulassungen.Count(x => x.Reservation is not null).Should().Be(1);
            zulassungen.Where(x => x.Reservation is null).Should().OnlyContain(x => x.WaitReason != null && x.WaitReason.Contains("globale Limit"));

            // Der vergebene Platz wird nach Freigabe wieder nutzbar.
            zulassungen.Single(x => x.Reservation is not null).Reservation!.Dispose();
            var free = await _sut.CheckAndReserveStartSlotAsync(PrefixA, TestContext.Current.CancellationToken);
            free.Reservation.Should().NotBeNull();
            free.Reservation!.Dispose();
        }
        finally
        {
            foreach (var x in services) x.Db.Dispose();
        }
    }

    /// <summary>Bleibt der Start hängen, verfällt die Reservierung nach ihrer Lebensdauer
    /// und der Platz wird ohne Freigabe wieder nutzbar.</summary>
    [Fact]
    public async Task CheckAndReserveStartSlotAsync_Reservation_DecaysAfterLifetime()
    {
        var time = new FakeTimeProvider();
        var sut = new ScmEventExecutionLimitService(_db, _settings, _cli.Object, time, _pool);
        await _settings.SetSettingAsync(ScmEventExecutionLimitService.GlobalLimitKey, "1", TestContext.Current.CancellationToken);

        var hängende = await sut.CheckAndReserveStartSlotAsync(PrefixA, TestContext.Current.CancellationToken);
        hängende.Reservation.Should().NotBeNull();
        (await sut.CheckAndReserveStartSlotAsync(PrefixB, TestContext.Current.CancellationToken)).WaitReason.Should().NotBeNull();

        time.Advance(ScmEventExecutionLimitService.ReservationLifetime + TimeSpan.FromSeconds(1));

        var free = await sut.CheckAndReserveStartSlotAsync(PrefixB, TestContext.Current.CancellationToken);
        free.WaitReason.Should().BeNull();
        free.Reservation!.Dispose();
    }

    /// <summary>Eine pausierte Aufgabe mit laufendem Prozess belegt keinen globalen Platz.</summary>
    [Fact]
    public async Task GetStartBlockReasonAsync_PausedWorkItem_ReturnsGlobalSlotFree()
    {
        await _settings.SetSettingAsync(ScmEventExecutionLimitService.GlobalLimitKey, "1", TestContext.Current.CancellationToken);
        SetRunning(await RunningWorkItemAsync("Pausiert", PrefixA,
            pausedUntilUtc: DateTimeOffset.UtcNow.AddHours(1)));

        (await _sut.GetStartBlockReasonAsync(PrefixB, TestContext.Current.CancellationToken)).Should().BeNull();
    }

    /// <summary>Eine pausierte Aufgabe belegt auch das Plugin-Limit ihres Prefix nicht.</summary>
    [Fact]
    public async Task GetStartBlockReasonAsync_PausedWorkItem_ReturnsPluginSlotFree()
    {
        await _settings.SetSettingAsync(ScmEventExecutionLimitService.PluginLimitKey(PrefixA), "1", TestContext.Current.CancellationToken);
        SetRunning(await RunningWorkItemAsync("Pausiert", PrefixA,
            pausedUntilUtc: DateTimeOffset.UtcNow.AddHours(1)));

        (await _sut.GetStartBlockReasonAsync(PrefixA, TestContext.Current.CancellationToken)).Should().BeNull();
    }

    /// <summary>Eine pausierte Aufgabe ohne auflösbare Plugin-Attribution entfällt vollständig
    /// aus der Zählung — sie hinterlässt auch keinen Restbeleg im globalen Zähler.</summary>
    [Fact]
    public async Task GetStartBlockReasonAsync_PausedWorkItemWithoutPrefix_OmittedOutBothCount()
    {
        await _settings.SetSettingAsync(ScmEventExecutionLimitService.GlobalLimitKey, "1", TestContext.Current.CancellationToken);
        SetRunning(await RunningWorkItemAsync("Pausiert", null,
            pausedUntilUtc: DateTimeOffset.UtcNow.AddHours(1)));

        (await _sut.GetStartBlockReasonAsync(PrefixA, TestContext.Current.CancellationToken)).Should().BeNull();
    }

    /// <summary>Eine abgelaufene, aber noch gesetzte Pausenzeit zählt wieder als belegt —
    /// global und je Plugin (Fortsetzungs-Semantik: gesetzt markiert Fortsetzung, keine Pause).</summary>
    [Fact]
    public async Task GetStartBlockReasonAsync_ExpiredPause_CountsAsOccupied()
    {
        await _settings.SetSettingAsync(ScmEventExecutionLimitService.GlobalLimitKey, "1", TestContext.Current.CancellationToken);
        await _settings.SetSettingAsync(ScmEventExecutionLimitService.PluginLimitKey(PrefixA), "1", TestContext.Current.CancellationToken);
        SetRunning(await RunningWorkItemAsync("Abgelaufen", PrefixA,
            pausedUntilUtc: DateTimeOffset.UtcNow.AddMinutes(-5)));

        (await _sut.GetStartBlockReasonAsync(PrefixA, TestContext.Current.CancellationToken)).Should().Contain("globale Limit");
    }

    /// <summary>Gemischte Belegung: Die pausierte Aufgabe zählt nicht, die aktive weiterhin —
    /// bei globalem Limit 2 bleibt der Start frei, bei Limit 1 belegt die aktive den Platz.</summary>
    [Fact]
    public async Task GetStartBlockReasonAsync_MixedPausedAndActive_CountsOnlyActive()
    {
        await _settings.SetSettingAsync(ScmEventExecutionLimitService.GlobalLimitKey, "2", TestContext.Current.CancellationToken);
        SetRunning(
            await RunningWorkItemAsync("Pausiert", PrefixA, pausedUntilUtc: DateTimeOffset.UtcNow.AddHours(1)),
            await RunningWorkItemAsync("Aktiv", PrefixA));

        (await _sut.GetStartBlockReasonAsync(PrefixA, TestContext.Current.CancellationToken)).Should().BeNull();

        await _settings.SetSettingAsync(ScmEventExecutionLimitService.GlobalLimitKey, "1", TestContext.Current.CancellationToken);
        (await _sut.GetStartBlockReasonAsync(PrefixA, TestContext.Current.CancellationToken)).Should().Contain("globale Limit");
    }

    /// <summary>Ein pausierter Lauf gibt den Handle-Platz frei, eine offene Reservierung
    /// belegt ihn aber weiterhin regulär — bis zu ihrer Freigabe.</summary>
    [Fact]
    public async Task CheckAndReserveStartSlotAsync_PausedRunAndOpenReservation_ReservationOccupiedSlot()
    {
        await _settings.SetSettingAsync(ScmEventExecutionLimitService.GlobalLimitKey, "1", TestContext.Current.CancellationToken);
        SetRunning(await RunningWorkItemAsync("Pausiert", PrefixA,
            pausedUntilUtc: DateTimeOffset.UtcNow.AddHours(1)));

        var first = await _sut.CheckAndReserveStartSlotAsync(PrefixA, TestContext.Current.CancellationToken);
        first.WaitReason.Should().BeNull();
        first.Reservation.Should().NotBeNull();

        (await _sut.CheckAndReserveStartSlotAsync(PrefixB, TestContext.Current.CancellationToken)).WaitReason.Should().Contain("globale Limit");

        first.Reservation!.Dispose();

        var free = await _sut.CheckAndReserveStartSlotAsync(PrefixB, TestContext.Current.CancellationToken);
        free.WaitReason.Should().BeNull();
        free.Reservation!.Dispose();
    }

    /// <summary>Eine Aufgabe am verifizierten Eingabeprompt ohne offenes Ereignis belegt keinen globalen Platz.</summary>
    [Fact]
    public async Task GetStartBlockReasonAsync_ReadySessionNoPending_ReturnsGlobalSlotFree()
    {
        await _settings.SetSettingAsync(ScmEventExecutionLimitService.GlobalLimitKey, "1", TestContext.Current.CancellationToken);
        SetRunning(await RunningWorkItemAsync("Bereit", PrefixA, LiveSession(PrefixA, ScmPromptReadyStatus.Ready)));

        (await _sut.GetStartBlockReasonAsync(PrefixB, TestContext.Current.CancellationToken)).Should().BeNull();
    }

    /// <summary>Die Prompt-Freigabe belegt auch das Plugin-Limit ihres Prefix nicht.</summary>
    [Fact]
    public async Task GetStartBlockReasonAsync_ReadySessionNoPending_ReturnsPluginSlotFree()
    {
        await _settings.SetSettingAsync(ScmEventExecutionLimitService.PluginLimitKey(PrefixA), "1", TestContext.Current.CancellationToken);
        SetRunning(await RunningWorkItemAsync("Bereit", PrefixA, LiveSession(PrefixA, ScmPromptReadyStatus.Ready)));

        (await _sut.GetStartBlockReasonAsync(PrefixA, TestContext.Current.CancellationToken)).Should().BeNull();
    }

    /// <summary>Ein In-Flight-Ereignis der Aufgabe (Zustellung/Verarbeitung läuft) hält den Platz
    /// trotz verifiziertem Eingabeprompt belegt — ebenso konservativ ein unbekannter Status.</summary>
    [Theory]
    [InlineData("ZustellungVorbereitet")]
    [InlineData("Zugestellt")]
    [InlineData("InBearbeitung")]
    [InlineData("ZukuenftigerStatus")]
    public async Task GetStartBlockReasonAsync_ReadySessionInFlightEvent_CountsAsOccupied(string status)
    {
        await _settings.SetSettingAsync(ScmEventExecutionLimitService.GlobalLimitKey, "1", TestContext.Current.CancellationToken);
        var workItemId = await RunningWorkItemAsync("Bereit", PrefixA, LiveSession(PrefixA, ScmPromptReadyStatus.Ready));
        await AddEventAsync(workItemId, status);
        SetRunning(workItemId);

        (await _sut.GetStartBlockReasonAsync(PrefixB, TestContext.Current.CancellationToken)).Should().Contain("globale Limit");
    }

    /// <summary>Ein noch nicht zugestelltes oder terminal klärbares Ereignis benötigt keinen
    /// neuen Startplatz — die Prompt-Freigabe greift trotz offenem Ereignis.</summary>
    [Theory]
    [InlineData("Aufgenommen")]
    [InlineData("Verzögert")]
    [InlineData("Wartend")]
    [InlineData("ZustellungUngewiss")]
    [InlineData("VerarbeitungUngeklärt")]
    public async Task GetStartBlockReasonAsync_ReadySessionNotYetDeliveredEvent_FreesSlot(string status)
    {
        await _settings.SetSettingAsync(ScmEventExecutionLimitService.GlobalLimitKey, "1", TestContext.Current.CancellationToken);
        var workItemId = await RunningWorkItemAsync("Bereit", PrefixA, LiveSession(PrefixA, ScmPromptReadyStatus.Ready));
        await AddEventAsync(workItemId, status);
        SetRunning(workItemId);

        (await _sut.GetStartBlockReasonAsync(PrefixB, TestContext.Current.CancellationToken)).Should().BeNull();
    }

    /// <summary>Ein dauerhaft wartendes Ereignis (manueller Neuversand nötig) hält den Platz
    /// nicht belegt — es bewegt sich ohne Benutzereingriff ohnehin nicht weiter.</summary>
    [Fact]
    public async Task GetStartBlockReasonAsync_ReadySessionWaitingWithMaxAttempt_FreesSlot()
    {
        await _settings.SetSettingAsync(ScmEventExecutionLimitService.GlobalLimitKey, "1", TestContext.Current.CancellationToken);
        var workItemId = await RunningWorkItemAsync("Bereit", PrefixA, LiveSession(PrefixA, ScmPromptReadyStatus.Ready));
        await AddEventAsync(workItemId, "Wartend", nextAttemptUtc: long.MaxValue);
        SetRunning(workItemId);

        (await _sut.GetStartBlockReasonAsync(PrefixB, TestContext.Current.CancellationToken)).Should().BeNull();
    }

    /// <summary>Ereignisse im Endstatus gelten nicht als offen — die Prompt-Freigabe greift.</summary>
    [Theory]
    [InlineData("Verarbeitet")]
    [InlineData("Abgebrochen")]
    public async Task GetStartBlockReasonAsync_ReadySessionEventInEndStatus_ReturnsSlotFree(string status)
    {
        await _settings.SetSettingAsync(ScmEventExecutionLimitService.GlobalLimitKey, "1", TestContext.Current.CancellationToken);
        var workItemId = await RunningWorkItemAsync("Bereit", PrefixA, LiveSession(PrefixA, ScmPromptReadyStatus.Ready));
        await AddEventAsync(workItemId, status);
        SetRunning(workItemId);

        (await _sut.GetStartBlockReasonAsync(PrefixB, TestContext.Current.CancellationToken)).Should().BeNull();
    }

    /// <summary>Ohne verifiziertes Ready zählt die Aufgabe konservativ als belegt.</summary>
    [Theory]
    [InlineData(ScmPromptReadyStatus.Unknown)]
    [InlineData(ScmPromptReadyStatus.Busy)]
    [InlineData(ScmPromptReadyStatus.TrustPrompt)]
    [InlineData(ScmPromptReadyStatus.Exited)]
    public async Task GetStartBlockReasonAsync_SessionNotReady_CountsAsOccupied(ScmPromptReadyStatus readyStatus)
    {
        await _settings.SetSettingAsync(ScmEventExecutionLimitService.GlobalLimitKey, "1", TestContext.Current.CancellationToken);
        SetRunning(await RunningWorkItemAsync("Aktiv", PrefixA, LiveSession(PrefixA, readyStatus)));

        (await _sut.GetStartBlockReasonAsync(PrefixB, TestContext.Current.CancellationToken)).Should().Contain("globale Limit");
    }

    /// <summary>Eine nicht mehr lebende Sitzung gibt den Platz trotz gemeldetem Ready nicht frei.</summary>
    [Fact]
    public async Task GetStartBlockReasonAsync_SessionDead_CountsAsOccupied()
    {
        await _settings.SetSettingAsync(ScmEventExecutionLimitService.GlobalLimitKey, "1", TestContext.Current.CancellationToken);
        SetRunning(await RunningWorkItemAsync("Beendet", PrefixA, LiveSession(PrefixA, ScmPromptReadyStatus.Ready, isAlive: false)));

        (await _sut.GetStartBlockReasonAsync(PrefixB, TestContext.Current.CancellationToken)).Should().Contain("globale Limit");
    }

    /// <summary>Ein offenes Ereignis einer anderen Aufgabe blockiert die Freigabe nicht.</summary>
    [Fact]
    public async Task GetStartBlockReasonAsync_ReadySession_PendingOfOtherTask_Ignored()
    {
        await _settings.SetSettingAsync(ScmEventExecutionLimitService.GlobalLimitKey, "2", TestContext.Current.CancellationToken);
        var ready = await RunningWorkItemAsync("Bereit", PrefixA, LiveSession(PrefixA, ScmPromptReadyStatus.Ready));
        var other = await RunningWorkItemAsync("Aktiv", PrefixA, LiveSession(PrefixA, ScmPromptReadyStatus.Busy));
        await AddEventAsync(other, "InBearbeitung");
        SetRunning(ready, other);

        (await _sut.GetStartBlockReasonAsync(PrefixA, TestContext.Current.CancellationToken)).Should().BeNull();
    }

    /// <summary>Gemischte Belegung: Die am Prompt wartende Aufgabe zählt nicht, die aktive
    /// weiterhin — bei globalem Limit 2 bleibt der Start frei, bei Limit 1 belegt sie den Platz.</summary>
    [Fact]
    public async Task GetStartBlockReasonAsync_MixedReadyFreeAndActive_CountsOnlyActive()
    {
        await _settings.SetSettingAsync(ScmEventExecutionLimitService.GlobalLimitKey, "2", TestContext.Current.CancellationToken);
        SetRunning(
            await RunningWorkItemAsync("Bereit", PrefixA, LiveSession(PrefixA, ScmPromptReadyStatus.Ready)),
            await RunningWorkItemAsync("Aktiv", PrefixA, LiveSession(PrefixA, ScmPromptReadyStatus.Busy)));

        (await _sut.GetStartBlockReasonAsync(PrefixA, TestContext.Current.CancellationToken)).Should().BeNull();

        await _settings.SetSettingAsync(ScmEventExecutionLimitService.GlobalLimitKey, "1", TestContext.Current.CancellationToken);
        (await _sut.GetStartBlockReasonAsync(PrefixA, TestContext.Current.CancellationToken)).Should().Contain("globale Limit");
    }

    /// <summary>Ein am Prompt wartender Lauf gibt den Handle-Platz frei, eine offene Reservierung
    /// belegt ihn aber weiterhin regulär — bis zu ihrer Freigabe.</summary>
    [Fact]
    public async Task CheckAndReserveStartSlotAsync_ReadyRunAndOpenReservation_ReservationOccupiedSlot()
    {
        await _settings.SetSettingAsync(ScmEventExecutionLimitService.GlobalLimitKey, "1", TestContext.Current.CancellationToken);
        SetRunning(await RunningWorkItemAsync("Bereit", PrefixA, LiveSession(PrefixA, ScmPromptReadyStatus.Ready)));

        var first = await _sut.CheckAndReserveStartSlotAsync(PrefixA, TestContext.Current.CancellationToken);
        first.WaitReason.Should().BeNull();
        first.Reservation.Should().NotBeNull();

        (await _sut.CheckAndReserveStartSlotAsync(PrefixB, TestContext.Current.CancellationToken)).WaitReason.Should().Contain("globale Limit");

        first.Reservation!.Dispose();

        var free = await _sut.CheckAndReserveStartSlotAsync(PrefixB, TestContext.Current.CancellationToken);
        free.WaitReason.Should().BeNull();
        free.Reservation!.Dispose();
    }
}
