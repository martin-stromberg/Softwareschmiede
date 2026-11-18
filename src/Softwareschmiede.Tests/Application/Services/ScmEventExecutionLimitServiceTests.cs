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

    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiededDbContext _db;
    private readonly AppEinstellungService _einstellungen;
    private readonly Mock<ICliExecutionLimitSource> _cli;
    private readonly ScmEventExecutionLimitService _sut;
    private readonly Guid _projektId = new Guid("77777777-7777-7777-7777-777777777777");

    /// <summary>ScmEventExecutionLimitServiceTests.</summary>
    public ScmEventExecutionLimitServiceTests()
    {
        ScmEventExecutionLimitService.ReservierungenZuruecksetzen();
        _db = TestDbContextFactory.Create();
        _einstellungen = new AppEinstellungService(_db, NullLogger<AppEinstellungService>.Instance);
        _cli = new Mock<ICliExecutionLimitSource>();
        _cli.Setup(x => x.GetLaufendeAufgabenIds()).Returns([]);
        _sut = new ScmEventExecutionLimitService(_db, _einstellungen, _cli.Object, TimeProvider.System);

        _db.Projekte.Add(new Projekt
        {
            Id = _projektId,
            Name = "Limit-Testprojekt",
            ErstellungsDatum = DateTimeOffset.UtcNow,
            Status = ProjektStatus.Aktiv
        });
        _db.SaveChanges();
    }

    /// <summary>Dispose.</summary>
    public void Dispose()
    {
        ScmEventExecutionLimitService.ReservierungenZuruecksetzen();
        _db.Dispose();
    }

    private async Task<Guid> LaufendeAufgabeAsync(string titel, string? kiPluginPrefix,
        Mock<IScmPromptSessionAdapter>? session = null)
    {
        var aufgabe = new Aufgabe
        {
            Id = Guid.NewGuid(),
            Titel = titel,
            ProjektId = _projektId,
            Status = AufgabeStatus.Gestartet,
            AusfuehrungsStatus = AufgabeAusfuehrungsStatus.Aktiv,
            KiPluginPrefix = kiPluginPrefix
        };
        _db.Aufgaben.Add(aufgabe);
        await _db.SaveChangesAsync();
        _cli.Setup(x => x.GetScmPromptSession(aufgabe.Id)).Returns(session?.Object);
        return aufgabe.Id;
    }

    private void SetzeLaufende(params Guid[] ids)
        => _cli.Setup(x => x.GetLaufendeAufgabenIds()).Returns(ids.ToList());

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
    public async Task GetStartBlockReasonAsync_UngueltigeGrenze_GiltAlsUnbegrenzt(string wert)
    {
        await _einstellungen.SetSettingAsync(ScmEventExecutionLimitService.GlobalLimitKey, wert, TestContext.Current.CancellationToken);
        await _einstellungen.SetSettingAsync(ScmEventExecutionLimitService.PluginLimitKey(PrefixA), wert, TestContext.Current.CancellationToken);
        SetzeLaufende(await LaufendeAufgabeAsync("A", PrefixA));

        (await _sut.GetStartBlockReasonAsync(PrefixA, TestContext.Current.CancellationToken)).Should().BeNull();
    }

    /// <summary>Erreichtes globales Limit sperrt jeden Plugin-Prefix.</summary>
    [Fact]
    public async Task GetStartBlockReasonAsync_GlobalesLimitErreicht_Sperrt()
    {
        await _einstellungen.SetSettingAsync(ScmEventExecutionLimitService.GlobalLimitKey, "1", TestContext.Current.CancellationToken);
        SetzeLaufende(await LaufendeAufgabeAsync("A", PrefixA));

        var grund = await _sut.GetStartBlockReasonAsync(PrefixB, TestContext.Current.CancellationToken);

        grund.Should().Contain("globale Limit").And.Contain("1");
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
        var belegung = new CliAusfuehrungsBelegung(2, new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            [PrefixA] = 2
        });

        ScmEventExecutionLimitService.GetStartBlockReason(PrefixA, globalLimit: 2, pluginLimit: 1, belegung)
            .Should().Contain("globale Limit");
        ScmEventExecutionLimitService.GetStartBlockReason(PrefixA, globalLimit: 3, pluginLimit: 1, belegung)
            .Should().Contain($"'{PrefixA}'");
        ScmEventExecutionLimitService.GetStartBlockReason(PrefixB, globalLimit: 3, pluginLimit: 1, belegung)
            .Should().BeNull();
        ScmEventExecutionLimitService.GetStartBlockReason(PrefixA, globalLimit: null, pluginLimit: null, belegung)
            .Should().BeNull();
    }

    /// <summary>Die Plugin-Zählung ignoriert Groß-/Kleinschreibung des Prefix.</summary>
    [Fact]
    public void FuerPlugin_IgnoriertGrossKleinschreibung()
    {
        var belegung = new CliAusfuehrungsBelegung(1, new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            [PrefixA] = 1
        });

        belegung.FuerPlugin(PrefixA.ToLowerInvariant()).Should().Be(1);
        belegung.FuerPlugin("").Should().Be(0);
    }

    /// <summary>Bei unbegrenzter Konfiguration ist die Zulassung frei und belegt keinen Platz.</summary>
    [Fact]
    public async Task PruefeUndReserviereStartPlatzAsync_OhneGrenzen_FreiOhneReservierung()
    {
        var zulassung = await _sut.PruefeUndReserviereStartPlatzAsync(PrefixA, TestContext.Current.CancellationToken);

        zulassung.Wartegrund.Should().BeNull();
        zulassung.Reservierung.Should().BeNull();
    }

    /// <summary>Eine gehaltene Reservierung belegt den Platz: Der nächste Start wartet,
    /// die Freigabe gibt den Platz wieder frei — ohne dass je ein Prozess lief.</summary>
    [Fact]
    public async Task PruefeUndReserviereStartPlatzAsync_Reservierung_BelegtGlobalenPlatz()
    {
        await _einstellungen.SetSettingAsync(ScmEventExecutionLimitService.GlobalLimitKey, "1", TestContext.Current.CancellationToken);

        var erste = await _sut.PruefeUndReserviereStartPlatzAsync(PrefixA, TestContext.Current.CancellationToken);
        erste.Wartegrund.Should().BeNull();
        erste.Reservierung.Should().NotBeNull();

        // Die Reservierung zählt sofort als belegter Platz — auch für die reine Prüfung.
        (await _sut.PruefeUndReserviereStartPlatzAsync(PrefixB, TestContext.Current.CancellationToken)).Wartegrund.Should().Contain("globale Limit");
        (await _sut.GetStartBlockReasonAsync(PrefixB, TestContext.Current.CancellationToken)).Should().Contain("globale Limit");

        erste.Reservierung!.Dispose();

        var dritte = await _sut.PruefeUndReserviereStartPlatzAsync(PrefixB, TestContext.Current.CancellationToken);
        dritte.Wartegrund.Should().BeNull();
        dritte.Reservierung!.Dispose();
    }

    /// <summary>Plugin-Reservierungen belegen nur das eigene Plugin-Limit.</summary>
    [Fact]
    public async Task PruefeUndReserviereStartPlatzAsync_Reservierung_BelegtNurEigenesPluginLimit()
    {
        await _einstellungen.SetSettingAsync(ScmEventExecutionLimitService.PluginLimitKey(PrefixA), "1", TestContext.Current.CancellationToken);

        var reservierung = (await _sut.PruefeUndReserviereStartPlatzAsync(PrefixA, TestContext.Current.CancellationToken)).Reservierung;
        reservierung.Should().NotBeNull();

        (await _sut.PruefeUndReserviereStartPlatzAsync(PrefixA, TestContext.Current.CancellationToken)).Wartegrund.Should().Contain($"'{PrefixA}'");
        (await _sut.PruefeUndReserviereStartPlatzAsync(PrefixB, TestContext.Current.CancellationToken)).Wartegrund.Should().BeNull();

        reservierung!.Dispose();
        (await _sut.PruefeUndReserviereStartPlatzAsync(PrefixA, TestContext.Current.CancellationToken)).Reservierung!.Dispose();
    }

    /// <summary>Atomarität der Zulassung: Viele gleichzeitige Versuche auf getrennten
    /// Service-Instanzen (eigene DbContexts wie in eigenen DI-Scopes) erhalten bei
    /// globalem Limit 1 genau eine Reservierung; alle übrigen warten mit Limit-Grund.</summary>
    [Fact]
    public async Task PruefeUndReserviereStartPlatzAsync_ParalleleVersuche_NurEineReservierung()
    {
        const int versuche = 8;
        await _einstellungen.SetSettingAsync(ScmEventExecutionLimitService.GlobalLimitKey, "1", TestContext.Current.CancellationToken);
        var dienste = Enumerable.Range(0, versuche).Select(_ =>
        {
            var db = TestDbContextFactory.Create();
            var einstellungen = new AppEinstellungService(db, NullLogger<AppEinstellungService>.Instance);
            einstellungen.SetSettingAsync(ScmEventExecutionLimitService.GlobalLimitKey, "1").GetAwaiter().GetResult();
            return (Dienst: new ScmEventExecutionLimitService(db, einstellungen, _cli.Object, TimeProvider.System), Db: db);
        }).ToList();
        try
        {
            var zulassungen = await Task.WhenAll(dienste.Select(x =>
                Task.Run(() => x.Dienst.PruefeUndReserviereStartPlatzAsync(PrefixA))));

            zulassungen.Count(x => x.Reservierung is not null).Should().Be(1);
            zulassungen.Where(x => x.Reservierung is null).Should().OnlyContain(x => x.Wartegrund != null && x.Wartegrund.Contains("globale Limit"));

            // Der vergebene Platz wird nach Freigabe wieder nutzbar.
            zulassungen.Single(x => x.Reservierung is not null).Reservierung!.Dispose();
            var frei = await _sut.PruefeUndReserviereStartPlatzAsync(PrefixA, TestContext.Current.CancellationToken);
            frei.Reservierung.Should().NotBeNull();
            frei.Reservierung!.Dispose();
        }
        finally
        {
            foreach (var x in dienste) x.Db.Dispose();
        }
    }

    /// <summary>Bleibt der Start hängen, verfällt die Reservierung nach ihrer Lebensdauer
    /// und der Platz wird ohne Freigabe wieder nutzbar.</summary>
    [Fact]
    public async Task PruefeUndReserviereStartPlatzAsync_Reservierung_VerfaelltNachLebensdauer()
    {
        var zeit = new FakeTimeProvider();
        var sut = new ScmEventExecutionLimitService(_db, _einstellungen, _cli.Object, zeit);
        await _einstellungen.SetSettingAsync(ScmEventExecutionLimitService.GlobalLimitKey, "1", TestContext.Current.CancellationToken);

        var hängende = await sut.PruefeUndReserviereStartPlatzAsync(PrefixA, TestContext.Current.CancellationToken);
        hängende.Reservierung.Should().NotBeNull();
        (await sut.PruefeUndReserviereStartPlatzAsync(PrefixB, TestContext.Current.CancellationToken)).Wartegrund.Should().NotBeNull();

        zeit.Advance(ScmEventExecutionLimitService.ReservierungsLebensdauer + TimeSpan.FromSeconds(1));

        var frei = await sut.PruefeUndReserviereStartPlatzAsync(PrefixB, TestContext.Current.CancellationToken);
        frei.Wartegrund.Should().BeNull();
        frei.Reservierung!.Dispose();
    }
}
