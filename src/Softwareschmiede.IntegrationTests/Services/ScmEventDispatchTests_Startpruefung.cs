using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Infrastructure.Data;
using Softwareschmiede.Infrastructure.Terminal;
using Softwareschmiede.TestSupport;

namespace Softwareschmiede.IntegrationTests.Services;

/// <summary>Ereignisgesteuerte Startprüfung (Issue 410, Schritt 3): freiwerdende Plätze und
/// Pausen-Setzpfade lösen dieselbe Startprüfung wie der Poll sofort aus — serialisiert,
/// idempotent und ohne ein Starten über das Limit hinaus.</summary>
public sealed class ScmEventDispatchTests_Startpruefung
{
    /// <summary>Nach Beendigung einer SCM-Ausführung füllt der Trigger den frei gewordenen
    /// Platz unmittelbar mit dem wartenden Ereignis — ohne einen Poll-Durchlauf.</summary>
    [Fact]
    public async Task StartPruefung_CliBeendet_WartendesEreignisStartetOhnePoll()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await fixture.SetSettingAsync(ScmEventExecutionLimitService.GlobalLimitKey, "1");
        var (firstTask, firstId) = await fixture.AddAsync(configured: true);
        var (_, secondId) = await fixture.AddAsync(configured: true);

        await fixture.DispatchAsync(firstId);
        Assert.Equal("Zugestellt", (await fixture.EventAsync(firstId)).Status);
        await fixture.DispatchAsync(secondId);
        var second = await fixture.EventAsync(secondId);
        Assert.Equal("Wartend", second.Status);
        Assert.Contains("globale Limit", second.Reason);
        Assert.Single(fixture.Sessions);

        await fixture.StartPruefung.StartAsync(CancellationToken.None);
        try
        {
            await fixture.Cli.StopCliAsync(firstTask, TestContext.Current.CancellationToken);
            // Wie PseudoConsoleSession: das Session-Ende wird als Exited-Ereignis gemeldet.
            fixture.Sessions[firstTask].Raise(x => x.Exited += null, new TerminalSessionExitedEventArgs(0));

            await WaitForAsync(async () => fixture.Sessions.Count == 2
                    && (await fixture.EventAsync(secondId)).Status == "Zugestellt",
                "Das wartende Ereignis wurde nach dem Freiwerden des Platzes nicht ereignisgesteuert gestartet.");
            Assert.Equal(2, fixture.Prompts.Count);
        }
        finally
        {
            await fixture.StartPruefung.StopAsync(CancellationToken.None);
        }
    }

    /// <summary>Nach dem Aufheben einer Pause startet ein wartendes Ereignis sofort —
    /// die Prüfung wird durch den Pausen-Setzpfad ausgelöst, nicht durch den Poll.</summary>
    [Fact]
    public async Task StartPruefung_PauseAufgehoben_EreignisStartetOhnePoll()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        var (taskId, eventId) = await fixture.AddAsync(AufgabeStatus.Gestartet, configured: true);
        using (var scope = fixture.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<AufgabeService>()
                .SetPauseAsync(taskId, DateTimeOffset.UtcNow.AddMinutes(10), TestContext.Current.CancellationToken);
        }

        await fixture.DispatchAsync(eventId);
        Assert.Equal("Aufgabe pausiert; Start gesperrt.", (await fixture.EventAsync(eventId)).Reason);
        Assert.Empty(fixture.Sessions);

        await fixture.StartPruefung.StartAsync(CancellationToken.None);
        try
        {
            using (var scope = fixture.Services.CreateScope())
            {
                await scope.ServiceProvider.GetRequiredService<AufgabeService>()
                    .SetPauseAsync(taskId, null, TestContext.Current.CancellationToken);
            }

            await WaitForAsync(async () => (await fixture.EventAsync(eventId)).Status == "Zugestellt",
                "Das wartende Ereignis wurde nach dem Aufheben der Pause nicht ereignisgesteuert gestartet.");
            Assert.Single(fixture.Sessions);
        }
        finally
        {
            await fixture.StartPruefung.StopAsync(CancellationToken.None);
        }
    }

    /// <summary>Die Verzögerungsdirektive pausiert die Aufgabe über
    /// <see cref="ScmEventStore"/> — der Pausen-Setzpfad löst die Startprüfung aus.</summary>
    [Fact]
    public async Task StartPruefung_VerzoegerteAufnahme_LoestSignalAus()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        var angefordert = 0;
        fixture.Ausloeser.Angefordert += () => angefordert++;

        var (taskId, _) = await fixture.AddAsync(configured: true, body: "$Softwareschmiede bitte #verzögert 2h");

        Assert.True(angefordert >= 1, "Die durch die Verzögerungsdirektive gesetzte Pause hat keinen Auslöser gemeldet.");
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
            Assert.NotNull(await db.Aufgaben.Where(x => x.Id == taskId).Select(x => x.PausiertBisUtc).SingleAsync(cancellationToken: TestContext.Current.CancellationToken));
        }
    }

    /// <summary>Die Session-Limit-Pause des <see cref="KiPluginLimitService"/> ist ein
    /// Pausen-Setzpfad und löst die Startprüfung aus.</summary>
    [Fact]
    public async Task StartPruefung_RateLimitPause_LoestSignalAus()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        var (taskId, _) = await fixture.AddAsync(AufgabeStatus.Gestartet, configured: true);
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
            await db.Aufgaben.Where(x => x.Id == taskId)
                .ExecuteUpdateAsync(x => x.SetProperty(p => p.AusfuehrungsStatus, AufgabeAusfuehrungsStatus.Aktiv), cancellationToken: TestContext.Current.CancellationToken);
        }
        var angefordert = 0;
        fixture.Ausloeser.Angefordert += () => angefordert++;

        using (var scope = fixture.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<KiPluginLimitService>()
                .VerarbeiteRateLimitAsync(taskId, DateTimeOffset.UtcNow.AddHours(1), TestContext.Current.CancellationToken);
        }

        Assert.True(angefordert >= 1, "Die Session-Limit-Pause hat keinen Auslöser gemeldet.");
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
            Assert.NotNull(await db.Aufgaben.Where(x => x.Id == taskId).Select(x => x.PausiertBisUtc).SingleAsync(cancellationToken: TestContext.Current.CancellationToken));
        }
    }

    /// <summary>Überlappende Auslöser (Signal, Trigger-Durchlauf, Poll) führen zu keinem
    /// Doppelstart und zu keinem Start über das Limit hinaus.</summary>
    [Fact]
    public async Task StartPruefung_UeberlappendeAusloeser_KeinDoppelstart()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await fixture.SetSettingAsync(ScmEventExecutionLimitService.GlobalLimitKey, "1");
        var (_, firstId) = await fixture.AddAsync(configured: true);
        var (_, secondId) = await fixture.AddAsync(configured: true);
        await fixture.DispatchAsync(firstId);
        Assert.Equal("Zugestellt", (await fixture.EventAsync(firstId)).Status);

        // Mehrere gleichzeitige Prüfungen bei weiterhin belegtem Platz.
        fixture.Ausloeser.StartpruefungAnfordern();
        fixture.Ausloeser.StartpruefungAnfordern();
        await Task.WhenAll(
            fixture.StartPruefung.RunOnceAsync(TestContext.Current.CancellationToken),
            fixture.StartPruefung.RunOnceAsync(TestContext.Current.CancellationToken),
            fixture.Monitor.RunOnceAsync(TestContext.Current.CancellationToken));
        fixture.Ausloeser.StartpruefungAnfordern();
        await fixture.StartPruefung.RunOnceAsync(TestContext.Current.CancellationToken);

        Assert.Single(fixture.Sessions);
        var second = await fixture.EventAsync(secondId);
        Assert.Equal("Wartend", second.Status);
        Assert.Contains("globale Limit", second.Reason);
    }

    /// <summary>Bei deaktivierter SCM-Überwachung stellt auch die ereignisgesteuerte
    /// Prüfung nichts zu — der Laufzeit-Schalter gilt wie beim Poll.</summary>
    [Fact]
    public async Task StartPruefung_MonitoringDeaktiviert_KeinDispatch()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await fixture.SetSettingAsync(AppEinstellungService.ScmMonitoringEnabledKey, "false");
        var (_, eventId) = await fixture.AddAsync(configured: true);

        fixture.Ausloeser.StartpruefungAnfordern();
        await fixture.StartPruefung.RunOnceAsync(TestContext.Current.CancellationToken);

        Assert.Empty(fixture.Sessions);
        Assert.NotEqual("Zugestellt", (await fixture.EventAsync(eventId)).Status);
    }

    /// <summary>Ein Nicht-SCM-Lauf löst die Prüfung über dieselbe Laufzählung aus und
    /// belegt den Platz korrekt: Solange er läuft, wartet das Ereignis; sobald er endet,
    /// startet das Ereignis — die Zählung wird nicht verfälscht.</summary>
    [Fact]
    public async Task StartPruefung_NichtScmLauf_ZaehlungKorrektUndLoestAus()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await fixture.SetSettingAsync(ScmEventExecutionLimitService.GlobalLimitKey, "1");
        var (nonScmTask, _) = await fixture.AddAsync(AufgabeStatus.Gestartet, configured: true);
        var (_, eventId) = await fixture.AddAsync(configured: true);
        using (var scope = fixture.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<EntwicklungsprozessService>()
                .CliNeustartenAsync(nonScmTask, ScmDispatchFixture.KiPrefix, null, TestContext.Current.CancellationToken, strictPluginSelection: true);
        }
        Assert.True(fixture.Cli.IsRunning(nonScmTask));

        await fixture.StartPruefung.RunOnceAsync(TestContext.Current.CancellationToken);
        var waiting = await fixture.EventAsync(eventId);
        Assert.Equal("Wartend", waiting.Status);
        Assert.Contains("globale Limit", waiting.Reason);

        await fixture.StartPruefung.StartAsync(CancellationToken.None);
        try
        {
            await fixture.Cli.StopCliAsync(nonScmTask, TestContext.Current.CancellationToken);
            fixture.Sessions[nonScmTask].Raise(x => x.Exited += null, new TerminalSessionExitedEventArgs(0));
            await WaitForAsync(async () => (await fixture.EventAsync(eventId)).Status == "Zugestellt",
                "Das wartende Ereignis wurde nach dem Ende des Nicht-SCM-Laufs nicht ereignisgesteuert gestartet.");
        }
        finally
        {
            await fixture.StartPruefung.StopAsync(CancellationToken.None);
        }
    }

    private static async Task WaitForAsync(Func<Task<bool>> condition, string because)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            if (await condition()) return;
            await Task.Delay(100);
        }
        Assert.True(await condition(), because);
    }
}
