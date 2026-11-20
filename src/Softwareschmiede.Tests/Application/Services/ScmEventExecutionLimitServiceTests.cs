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
    private readonly AppSettingService _einstellungen;
    private readonly Mock<ICliExecutionLimitSource> _cli;
    private readonly ScmEventExecutionLimitService _sut;
    private readonly ScmStartSlotReservationPool _pool = new();
    private readonly Guid _projectId = new Guid("77777777-7777-7777-7777-777777777777");

    /// <summary>ScmEventExecutionLimitServiceTests.</summary>
    public ScmEventExecutionLimitServiceTests()
    {
        _db = TestDbContextFactory.Create();
        _einstellungen = new AppSettingService(_db, NullLogger<AppSettingService>.Instance);
        _cli = new Mock<ICliExecutionLimitSource>();
        _cli.Setup(x => x.GetRunningWorkItemIds()).Returns([]);
        _sut = new ScmEventExecutionLimitService(_db, _einstellungen, _cli.Object, TimeProvider.System, _pool);

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

    private async Task<Guid> LaufendeAufgabeAsync(string title, string? aiPluginPrefix,
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

    private void SetzeLaufende(params Guid[] ids)
        => _cli.Setup(x => x.GetRunningWorkItemIds()).Returns(ids.ToList());

    private static Mock<IScmPromptSessionAdapter> LebendeSession(string pluginPrefix)
    {
        var session = new Mock<IScmPromptSessionAdapter>();
        session.SetupGet(x => x.IsAlive).Returns(true);
        session.SetupGet(x => x.PluginPrefix).Returns(pluginPrefix);
        return session;
    }

    /// <summary>Ohne gesetzte Grenzen ist jeder Start frei.</summary>
    [Fact]
    public async Task GetStartBlockReasonAsync_OhneGrenzen_GibtNull()
    {
        SetzeLaufende(await LaufendeAufgabeAsync("A", PrefixA));

        (await _sut.GetStartBlockReasonAsync(PrefixA, TestContext.Current.CancellationToken)).Should().BeNull();
    }

    /// <summary>Ungültige Grenzwerte (0, negativ, Text) gelten als unbegrenzt.</summary>
    [Theory]
    [InlineData("0")]
    [InlineData("-3")]
    [InlineData("abc")]
    public async Task GetStartBlockReasonAsync_UngueltigeGrenze_GiltAlsUnbegrenzt(string value)
    {
        await _einstellungen.SetSettingAsync(ScmEventExecutionLimitService.GlobalLimitKey, value, TestContext.Current.CancellationToken);
        await _einstellungen.SetSettingAsync(ScmEventExecutionLimitService.PluginLimitKey(PrefixA), value, TestContext.Current.CancellationToken);
        SetzeLaufende(await LaufendeAufgabeAsync("A", PrefixA));

        (await _sut.GetStartBlockReasonAsync(PrefixA, TestContext.Current.CancellationToken)).Should().BeNull();
    }

    /// <summary>Erreichtes globales Limit sperrt jeden Plugin-Prefix.</summary>
    [Fact]
    public async Task GetStartBlockReasonAsync_GlobalesLimitErreicht_Sperrt()
    {
        await _einstellungen.SetSettingAsync(ScmEventExecutionLimitService.GlobalLimitKey, "1", TestContext.Current.CancellationToken);
        SetzeLaufende(await LaufendeAufgabeAsync("A", PrefixA));

        var reason = await _sut.GetStartBlockReasonAsync(PrefixB, TestContext.Current.CancellationToken);

        reason.Should().Contain("globale Limit").And.Contain("1");
    }

    /// <summary>Unter dem globalen Limit bleibt der Start frei.</summary>
    [Fact]
    public async Task GetStartBlockReasonAsync_GlobalesLimitNichtErreicht_GibtNull()
    {
        await _einstellungen.SetSettingAsync(ScmEventExecutionLimitService.GlobalLimitKey, "2", TestContext.Current.CancellationToken);
        SetzeLaufende(await LaufendeAufgabeAsync("A", PrefixA));

        (await _sut.GetStartBlockReasonAsync(PrefixB, TestContext.Current.CancellationToken)).Should().BeNull();
    }

    /// <summary>Erreichtes Plugin-Limit sperrt nur dieses Plugin.</summary>
    [Fact]
    public async Task GetStartBlockReasonAsync_PluginLimitErreicht_SperrtNurDiesesPlugin()
    {
        await _einstellungen.SetSettingAsync(ScmEventExecutionLimitService.PluginLimitKey(PrefixA), "1", TestContext.Current.CancellationToken);
        SetzeLaufende(await LaufendeAufgabeAsync("A", PrefixA));

        (await _sut.GetStartBlockReasonAsync(PrefixA, TestContext.Current.CancellationToken)).Should().Contain($"'{PrefixA}'");
        (await _sut.GetStartBlockReasonAsync(PrefixB, TestContext.Current.CancellationToken)).Should().BeNull();
    }

    /// <summary>Beide Grenzen wirken gleichzeitig; das globale Limit meldet zuerst.</summary>
    [Fact]
    public async Task GetStartBlockReasonAsync_BeideLimitsErreicht_MeldetGlobalesLimit()
    {
        await _einstellungen.SetSettingAsync(ScmEventExecutionLimitService.GlobalLimitKey, "1", TestContext.Current.CancellationToken);
        await _einstellungen.SetSettingAsync(ScmEventExecutionLimitService.PluginLimitKey(PrefixA), "1", TestContext.Current.CancellationToken);
        SetzeLaufende(await LaufendeAufgabeAsync("A", PrefixA));

        (await _sut.GetStartBlockReasonAsync(PrefixA, TestContext.Current.CancellationToken)).Should().Contain("globale Limit");
    }

    /// <summary>Gemischte Plugins zählen getrennt: Fremde Läufe belegen kein Plugin-Limit.</summary>
    [Fact]
    public async Task GetStartBlockReasonAsync_GemischtePlugins_ZaehltNurEigenenPrefix()
    {
        await _einstellungen.SetSettingAsync(ScmEventExecutionLimitService.PluginLimitKey(PrefixA), "2", TestContext.Current.CancellationToken);
        SetzeLaufende(
            await LaufendeAufgabeAsync("A1", PrefixA),
            await LaufendeAufgabeAsync("B1", PrefixB),
            await LaufendeAufgabeAsync("B2", PrefixB));

        (await _sut.GetStartBlockReasonAsync(PrefixA, TestContext.Current.CancellationToken)).Should().BeNull();
        await _einstellungen.SetSettingAsync(ScmEventExecutionLimitService.PluginLimitKey(PrefixA), "1", TestContext.Current.CancellationToken);
        (await _sut.GetStartBlockReasonAsync(PrefixA, TestContext.Current.CancellationToken)).Should().NotBeNull();
    }

    /// <summary>Eine lebende SCM-Sitzung attribuiert zum tatsächlich gestarteten Plugin, nicht zum gespeicherten.</summary>
    [Fact]
    public async Task GetStartBlockReasonAsync_LebendeSession_ZaehltTatsaechlichesPlugin()
    {
        // Aufgabe hat PrefixA gespeichert, läuft aber tatsächlich mit PrefixB (Fallback).
        var session = LebendeSession(PrefixB);
        await _einstellungen.SetSettingAsync(ScmEventExecutionLimitService.PluginLimitKey(PrefixA), "1", TestContext.Current.CancellationToken);
        await _einstellungen.SetSettingAsync(ScmEventExecutionLimitService.PluginLimitKey(PrefixB), "1", TestContext.Current.CancellationToken);
        SetzeLaufende(await LaufendeAufgabeAsync("A", PrefixA, session));

        (await _sut.GetStartBlockReasonAsync(PrefixA, TestContext.Current.CancellationToken)).Should().BeNull();
        (await _sut.GetStartBlockReasonAsync(PrefixB, TestContext.Current.CancellationToken)).Should().NotBeNull();
    }

    /// <summary>Beendete/entfernte SCM-Sitzungen fallen auf das gespeicherte Plugin der Aufgabe zurück.</summary>
    [Fact]
    public async Task GetStartBlockReasonAsync_SessionTot_FaelltAufAufgabenPrefixZurueck()
    {
        var session = new Mock<IScmPromptSessionAdapter>();
        session.SetupGet(x => x.IsAlive).Returns(false);
        session.SetupGet(x => x.PluginPrefix).Returns(PrefixB);
        await _einstellungen.SetSettingAsync(ScmEventExecutionLimitService.PluginLimitKey(PrefixA), "1", TestContext.Current.CancellationToken);
        SetzeLaufende(await LaufendeAufgabeAsync("A", PrefixA, session));

        (await _sut.GetStartBlockReasonAsync(PrefixA, TestContext.Current.CancellationToken)).Should().NotBeNull();
    }

    /// <summary>Aufgaben ohne Plugin-Zuordnung zählen global, aber für kein Plugin-Limit.</summary>
    [Fact]
    public async Task GetStartBlockReasonAsync_OhnePrefix_ZaehltNurGlobal()
    {
        await _einstellungen.SetSettingAsync(ScmEventExecutionLimitService.PluginLimitKey(PrefixA), "1", TestContext.Current.CancellationToken);
        await _einstellungen.SetSettingAsync(ScmEventExecutionLimitService.GlobalLimitKey, "2", TestContext.Current.CancellationToken);
        SetzeLaufende(await LaufendeAufgabeAsync("A", null));

        (await _sut.GetStartBlockReasonAsync(PrefixA, TestContext.Current.CancellationToken)).Should().BeNull();
    }

    /// <summary>Reine Auswertung: globales Limit schlägt Plugin-Limit; frei bei Platz.</summary>
    [Fact]
    public void GetStartBlockReason_PrueftGlobalesVorPluginLimit()
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
    public void FuerPlugin_IgnoriertGrossKleinschreibung()
    {
        var occupancy = new CliExecutionOccupancy(1, new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            [PrefixA] = 1
        });

        occupancy.FuerPlugin(PrefixA.ToLowerInvariant()).Should().Be(1);
        occupancy.FuerPlugin("").Should().Be(0);
    }

    /// <summary>Bei unbegrenzter Konfiguration ist die Zulassung frei und belegt keinen Platz.</summary>
    [Fact]
    public async Task PruefeUndReserviereStartPlatzAsync_OhneGrenzen_FreiOhneReservierung()
    {
        var admission = await _sut.CheckAndReserveStartSlotAsync(PrefixA, TestContext.Current.CancellationToken);

        admission.WaitReason.Should().BeNull();
        admission.Reservation.Should().BeNull();
    }

    /// <summary>Eine gehaltene Reservierung belegt den Platz: Der nächste Start wartet,
    /// die Freigabe gibt den Platz wieder frei — ohne dass je ein Prozess lief.</summary>
    [Fact]
    public async Task PruefeUndReserviereStartPlatzAsync_Reservierung_BelegtGlobalenPlatz()
    {
        await _einstellungen.SetSettingAsync(ScmEventExecutionLimitService.GlobalLimitKey, "1", TestContext.Current.CancellationToken);

        var erste = await _sut.CheckAndReserveStartSlotAsync(PrefixA, TestContext.Current.CancellationToken);
        erste.WaitReason.Should().BeNull();
        erste.Reservation.Should().NotBeNull();

        // Die Reservierung zählt sofort als belegter Platz — auch für die reine Prüfung.
        (await _sut.CheckAndReserveStartSlotAsync(PrefixB, TestContext.Current.CancellationToken)).WaitReason.Should().Contain("globale Limit");
        (await _sut.GetStartBlockReasonAsync(PrefixB, TestContext.Current.CancellationToken)).Should().Contain("globale Limit");

        erste.Reservation!.Dispose();

        var dritte = await _sut.CheckAndReserveStartSlotAsync(PrefixB, TestContext.Current.CancellationToken);
        dritte.WaitReason.Should().BeNull();
        dritte.Reservation!.Dispose();
    }

    /// <summary>Plugin-Reservierungen belegen nur das eigene Plugin-Limit.</summary>
    [Fact]
    public async Task PruefeUndReserviereStartPlatzAsync_Reservierung_BelegtNurEigenesPluginLimit()
    {
        await _einstellungen.SetSettingAsync(ScmEventExecutionLimitService.PluginLimitKey(PrefixA), "1", TestContext.Current.CancellationToken);

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
    public async Task PruefeUndReserviereStartPlatzAsync_ParalleleVersuche_NurEineReservierung()
    {
        const int attempt = 8;
        await _einstellungen.SetSettingAsync(ScmEventExecutionLimitService.GlobalLimitKey, "1", TestContext.Current.CancellationToken);
        var services = Enumerable.Range(0, attempt).Select(_ =>
        {
            var db = TestDbContextFactory.Create();
            var settings = new AppSettingService(db, NullLogger<AppSettingService>.Instance);
            settings.SetSettingAsync(ScmEventExecutionLimitService.GlobalLimitKey, "1").GetAwaiter().GetResult();
            return (Dienst: new ScmEventExecutionLimitService(db, settings, _cli.Object, TimeProvider.System, _pool), Db: db);
        }).ToList();
        try
        {
            var zulassungen = await Task.WhenAll(services.Select(x =>
                Task.Run(() => x.Dienst.CheckAndReserveStartSlotAsync(PrefixA))));

            zulassungen.Count(x => x.Reservation is not null).Should().Be(1);
            zulassungen.Where(x => x.Reservation is null).Should().OnlyContain(x => x.WaitReason != null && x.WaitReason.Contains("globale Limit"));

            // Der vergebene Platz wird nach Freigabe wieder nutzbar.
            zulassungen.Single(x => x.Reservation is not null).Reservation!.Dispose();
            var frei = await _sut.CheckAndReserveStartSlotAsync(PrefixA, TestContext.Current.CancellationToken);
            frei.Reservation.Should().NotBeNull();
            frei.Reservation!.Dispose();
        }
        finally
        {
            foreach (var x in services) x.Db.Dispose();
        }
    }

    /// <summary>Bleibt der Start hängen, verfällt die Reservierung nach ihrer Lebensdauer
    /// und der Platz wird ohne Freigabe wieder nutzbar.</summary>
    [Fact]
    public async Task PruefeUndReserviereStartPlatzAsync_Reservierung_VerfaelltNachLebensdauer()
    {
        var zeit = new FakeTimeProvider();
        var sut = new ScmEventExecutionLimitService(_db, _einstellungen, _cli.Object, zeit, _pool);
        await _einstellungen.SetSettingAsync(ScmEventExecutionLimitService.GlobalLimitKey, "1", TestContext.Current.CancellationToken);

        var hängende = await sut.CheckAndReserveStartSlotAsync(PrefixA, TestContext.Current.CancellationToken);
        hängende.Reservation.Should().NotBeNull();
        (await sut.CheckAndReserveStartSlotAsync(PrefixB, TestContext.Current.CancellationToken)).WaitReason.Should().NotBeNull();

        zeit.Advance(ScmEventExecutionLimitService.ReservationLifetime + TimeSpan.FromSeconds(1));

        var frei = await sut.CheckAndReserveStartSlotAsync(PrefixB, TestContext.Current.CancellationToken);
        frei.WaitReason.Should().BeNull();
        frei.Reservation!.Dispose();
    }

    /// <summary>Eine pausierte Aufgabe mit laufendem Prozess belegt keinen globalen Platz.</summary>
    [Fact]
    public async Task GetStartBlockReasonAsync_PausierteAufgabe_GibtGlobalenPlatzFrei()
    {
        await _einstellungen.SetSettingAsync(ScmEventExecutionLimitService.GlobalLimitKey, "1", TestContext.Current.CancellationToken);
        SetzeLaufende(await LaufendeAufgabeAsync("Pausiert", PrefixA,
            pausedUntilUtc: DateTimeOffset.UtcNow.AddHours(1)));

        (await _sut.GetStartBlockReasonAsync(PrefixB, TestContext.Current.CancellationToken)).Should().BeNull();
    }

    /// <summary>Eine pausierte Aufgabe belegt auch das Plugin-Limit ihres Prefix nicht.</summary>
    [Fact]
    public async Task GetStartBlockReasonAsync_PausierteAufgabe_GibtPluginPlatzFrei()
    {
        await _einstellungen.SetSettingAsync(ScmEventExecutionLimitService.PluginLimitKey(PrefixA), "1", TestContext.Current.CancellationToken);
        SetzeLaufende(await LaufendeAufgabeAsync("Pausiert", PrefixA,
            pausedUntilUtc: DateTimeOffset.UtcNow.AddHours(1)));

        (await _sut.GetStartBlockReasonAsync(PrefixA, TestContext.Current.CancellationToken)).Should().BeNull();
    }

    /// <summary>Eine pausierte Aufgabe ohne auflösbare Plugin-Attribution entfällt vollständig
    /// aus der Zählung — sie hinterlässt auch keinen Restbeleg im globalen Zähler.</summary>
    [Fact]
    public async Task GetStartBlockReasonAsync_PausierteAufgabeOhnePrefix_EntfaelltAusBeidenZaehlern()
    {
        await _einstellungen.SetSettingAsync(ScmEventExecutionLimitService.GlobalLimitKey, "1", TestContext.Current.CancellationToken);
        SetzeLaufende(await LaufendeAufgabeAsync("Pausiert", null,
            pausedUntilUtc: DateTimeOffset.UtcNow.AddHours(1)));

        (await _sut.GetStartBlockReasonAsync(PrefixA, TestContext.Current.CancellationToken)).Should().BeNull();
    }

    /// <summary>Eine abgelaufene, aber noch gesetzte Pausenzeit zählt wieder als belegt —
    /// global und je Plugin (Fortsetzungs-Semantik: gesetzt markiert Fortsetzung, keine Pause).</summary>
    [Fact]
    public async Task GetStartBlockReasonAsync_AbgelaufenePause_ZaehltAlsBelegt()
    {
        await _einstellungen.SetSettingAsync(ScmEventExecutionLimitService.GlobalLimitKey, "1", TestContext.Current.CancellationToken);
        await _einstellungen.SetSettingAsync(ScmEventExecutionLimitService.PluginLimitKey(PrefixA), "1", TestContext.Current.CancellationToken);
        SetzeLaufende(await LaufendeAufgabeAsync("Abgelaufen", PrefixA,
            pausedUntilUtc: DateTimeOffset.UtcNow.AddMinutes(-5)));

        (await _sut.GetStartBlockReasonAsync(PrefixA, TestContext.Current.CancellationToken)).Should().Contain("globale Limit");
    }

    /// <summary>Gemischte Belegung: Die pausierte Aufgabe zählt nicht, die aktive weiterhin —
    /// bei globalem Limit 2 bleibt der Start frei, bei Limit 1 belegt die aktive den Platz.</summary>
    [Fact]
    public async Task GetStartBlockReasonAsync_GemischtPausiertUndAktiv_ZaehltNurAktive()
    {
        await _einstellungen.SetSettingAsync(ScmEventExecutionLimitService.GlobalLimitKey, "2", TestContext.Current.CancellationToken);
        SetzeLaufende(
            await LaufendeAufgabeAsync("Pausiert", PrefixA, pausedUntilUtc: DateTimeOffset.UtcNow.AddHours(1)),
            await LaufendeAufgabeAsync("Aktiv", PrefixA));

        (await _sut.GetStartBlockReasonAsync(PrefixA, TestContext.Current.CancellationToken)).Should().BeNull();

        await _einstellungen.SetSettingAsync(ScmEventExecutionLimitService.GlobalLimitKey, "1", TestContext.Current.CancellationToken);
        (await _sut.GetStartBlockReasonAsync(PrefixA, TestContext.Current.CancellationToken)).Should().Contain("globale Limit");
    }

    /// <summary>Ein pausierter Lauf gibt den Handle-Platz frei, eine offene Reservierung
    /// belegt ihn aber weiterhin regulär — bis zu ihrer Freigabe.</summary>
    [Fact]
    public async Task PruefeUndReserviereStartPlatzAsync_PausierterLaufUndOffeneReservierung_ReservierungBelegtPlatz()
    {
        await _einstellungen.SetSettingAsync(ScmEventExecutionLimitService.GlobalLimitKey, "1", TestContext.Current.CancellationToken);
        SetzeLaufende(await LaufendeAufgabeAsync("Pausiert", PrefixA,
            pausedUntilUtc: DateTimeOffset.UtcNow.AddHours(1)));

        var erste = await _sut.CheckAndReserveStartSlotAsync(PrefixA, TestContext.Current.CancellationToken);
        erste.WaitReason.Should().BeNull();
        erste.Reservation.Should().NotBeNull();

        (await _sut.CheckAndReserveStartSlotAsync(PrefixB, TestContext.Current.CancellationToken)).WaitReason.Should().Contain("globale Limit");

        erste.Reservation!.Dispose();

        var frei = await _sut.CheckAndReserveStartSlotAsync(PrefixB, TestContext.Current.CancellationToken);
        frei.WaitReason.Should().BeNull();
        frei.Reservation!.Dispose();
    }
}
