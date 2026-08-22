using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Infrastructure.Data;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>Tests fuer <see cref="UnteragentGovernanceMonitoringService"/>.</summary>
public sealed class UnteragentGovernanceMonitoringServiceTests : IDisposable
{
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 8, 21, 12, 0, 0, TimeSpan.Zero));
    private readonly string _testRoot = Path.Combine(Path.GetTempPath(), "SoftwareschmiedeTests", "UnteragentGovernanceMonitoring", Guid.NewGuid().ToString("N"));

    /// <summary>Dispose.</summary>
    public void Dispose()
    {
        if (Directory.Exists(_testRoot))
        {
            Directory.Delete(_testRoot, recursive: true);
        }
    }

    /// <summary>RunOnceAsync ruft ValidiereFehlerBedingungAsync fuer jeden aktiven Unteragenten auf und aendert bei fehlender Abbruchbedingung nichts an dessen Status.</summary>
    [Fact]
    public async Task RunOnceAsync_PrueftAktivenUnteragenten_UndBelaesstStatusOhneAbbruchbedingung()
    {
        using var db = TestDbContextFactory.Create();
        var (_, konfiguration) = await ErstelleAutonomeAufgabeAsync(db);
        var unteragent = ProjektleiterAgentServiceTestDatenFactory.ErstelleUnteragent(_testRoot, konfiguration.Id);
        unteragent.Status = UnteragentStatus.Ausgefuehrt;
        Directory.CreateDirectory(unteragent.VerzeichnisPfad);
        await SchreibeTaskStateAsync(unteragent.VerzeichnisPfad, tokensUsed: 100, tokenLimit: 10000, gestartetVor: TimeSpan.Zero, laufzeitLimitMinuten: 480);
        db.UnteragentSpezifikationen.Add(unteragent);
        await db.SaveChangesAsync();
        using var provider = BuildProvider(db);
        var sut = provider.GetRequiredService<UnteragentGovernanceMonitoringService>();

        await sut.RunOnceAsync();

        var gespeichert = db.UnteragentSpezifikationen.Single(u => u.Id == unteragent.Id);
        gespeichert.Status.Should().Be(UnteragentStatus.Ausgefuehrt);
        gespeichert.AbschlussDatum.Should().BeNull();
    }

    /// <summary>RunOnceAsync markiert einen Unteragenten bei Tokenlimit-Ueberschreitung als Fehler (Governance-Abbruch) und setzt das Abschlussdatum.</summary>
    [Fact]
    public async Task RunOnceAsync_MarkiertUnteragentenAlsFehler_BeiTokenlimitUeberschreitung()
    {
        using var db = TestDbContextFactory.Create();
        var (_, konfiguration) = await ErstelleAutonomeAufgabeAsync(db);
        var unteragent = ProjektleiterAgentServiceTestDatenFactory.ErstelleUnteragent(_testRoot, konfiguration.Id);
        unteragent.Status = UnteragentStatus.Ausgefuehrt;
        Directory.CreateDirectory(unteragent.VerzeichnisPfad);
        await SchreibeTaskStateAsync(unteragent.VerzeichnisPfad, tokensUsed: 12000, tokenLimit: 10000, gestartetVor: TimeSpan.Zero, laufzeitLimitMinuten: 480);
        db.UnteragentSpezifikationen.Add(unteragent);
        await db.SaveChangesAsync();
        using var provider = BuildProvider(db);
        var sut = provider.GetRequiredService<UnteragentGovernanceMonitoringService>();

        await sut.RunOnceAsync();

        var gespeichert = db.UnteragentSpezifikationen.Single(u => u.Id == unteragent.Id);
        gespeichert.Status.Should().Be(UnteragentStatus.Fehler);
        gespeichert.AbschlussDatum.Should().Be(_timeProvider.GetUtcNow());
    }

    /// <summary>RunOnceAsync ruft den Governance-Service nicht auf, wenn kein aktiver Unteragent existiert (z. B. weil er bereits abgeschlossen ist).</summary>
    [Fact]
    public async Task RunOnceAsync_TutNichts_OhneAktivenUnteragenten()
    {
        using var db = TestDbContextFactory.Create();
        var (_, konfiguration) = await ErstelleAutonomeAufgabeAsync(db);
        var unteragent = ProjektleiterAgentServiceTestDatenFactory.ErstelleUnteragent(_testRoot, konfiguration.Id);
        unteragent.Status = UnteragentStatus.Abgeschlossen;
        unteragent.AbschlussDatum = _timeProvider.GetUtcNow();
        Directory.CreateDirectory(unteragent.VerzeichnisPfad);
        await SchreibeTaskStateAsync(unteragent.VerzeichnisPfad, tokensUsed: 12000, tokenLimit: 10000, gestartetVor: TimeSpan.Zero, laufzeitLimitMinuten: 480);
        db.UnteragentSpezifikationen.Add(unteragent);
        await db.SaveChangesAsync();
        using var provider = BuildProvider(db);
        var sut = provider.GetRequiredService<UnteragentGovernanceMonitoringService>();

        var akt = () => sut.RunOnceAsync();

        await akt.Should().NotThrowAsync();
        var gespeichert = db.UnteragentSpezifikationen.Single(u => u.Id == unteragent.Id);
        gespeichert.Status.Should().Be(UnteragentStatus.Abgeschlossen);
    }

    /// <summary>RunOnceAsync prueft einen laufenden Unteragenten nicht, wenn die uebergeordnete Aufgabe nicht mehr aktiv ist (AusfuehrungsStatus == Beendet), selbst wenn ein Governance-Limit ueberschritten ist.</summary>
    [Fact]
    public async Task RunOnceAsync_PrueftNicht_WennAufgabeNichtMehrAktivIst()
    {
        using var db = TestDbContextFactory.Create();
        var (aufgabe, konfiguration) = await ErstelleAutonomeAufgabeAsync(db);
        aufgabe.AusfuehrungsStatus = AufgabeAusfuehrungsStatus.Beendet;
        await db.SaveChangesAsync();
        var unteragent = ProjektleiterAgentServiceTestDatenFactory.ErstelleUnteragent(_testRoot, konfiguration.Id);
        unteragent.Status = UnteragentStatus.Ausgefuehrt;
        Directory.CreateDirectory(unteragent.VerzeichnisPfad);
        await SchreibeTaskStateAsync(unteragent.VerzeichnisPfad, tokensUsed: 12000, tokenLimit: 10000, gestartetVor: TimeSpan.Zero, laufzeitLimitMinuten: 480);
        db.UnteragentSpezifikationen.Add(unteragent);
        await db.SaveChangesAsync();
        using var provider = BuildProvider(db);
        var sut = provider.GetRequiredService<UnteragentGovernanceMonitoringService>();

        await sut.RunOnceAsync();

        var gespeichert = db.UnteragentSpezifikationen.Single(u => u.Id == unteragent.Id);
        gespeichert.Status.Should().Be(UnteragentStatus.Ausgefuehrt);
        gespeichert.AbschlussDatum.Should().BeNull();
    }

    /// <summary>RunOnceAsync prueft einen laufenden Unteragenten nicht, solange die uebergeordnete Aufgabe pausiert ist (SessionPauseUtc gesetzt), selbst wenn ein Governance-Limit ueberschritten ist.</summary>
    [Fact]
    public async Task RunOnceAsync_PrueftNicht_WennAufgabePausiertIst()
    {
        using var db = TestDbContextFactory.Create();
        var (_, konfiguration) = await ErstelleAutonomeAufgabeAsync(db);
        konfiguration.SessionPauseUtc = _timeProvider.GetUtcNow();
        await db.SaveChangesAsync();
        var unteragent = ProjektleiterAgentServiceTestDatenFactory.ErstelleUnteragent(_testRoot, konfiguration.Id);
        unteragent.Status = UnteragentStatus.Ausgefuehrt;
        Directory.CreateDirectory(unteragent.VerzeichnisPfad);
        await SchreibeTaskStateAsync(unteragent.VerzeichnisPfad, tokensUsed: 12000, tokenLimit: 10000, gestartetVor: TimeSpan.Zero, laufzeitLimitMinuten: 480);
        db.UnteragentSpezifikationen.Add(unteragent);
        await db.SaveChangesAsync();
        using var provider = BuildProvider(db);
        var sut = provider.GetRequiredService<UnteragentGovernanceMonitoringService>();

        await sut.RunOnceAsync();

        var gespeichert = db.UnteragentSpezifikationen.Single(u => u.Id == unteragent.Id);
        gespeichert.Status.Should().Be(UnteragentStatus.Ausgefuehrt);
        gespeichert.AbschlussDatum.Should().BeNull();
    }

    private static async Task<(Aufgabe Aufgabe, AutonomAufgabeKonfiguration Konfiguration)> ErstelleAutonomeAufgabeAsync(SoftwareschmiededDbContext db)
    {
        var projekt = new Projekt
        {
            Id = Guid.NewGuid(),
            Name = "Projekt",
            Beschreibung = "Test",
            Status = ProjektStatus.Aktiv,
            ErstellungsDatum = DateTimeOffset.UtcNow
        };
        db.Projekte.Add(projekt);

        var (aufgabe, konfiguration) = ProjektleiterAgentServiceTestDatenFactory.ErstelleAufgabeUndKonfiguration(db, projekt.Id, Path.Combine(Path.GetTempPath(), "SoftwareschmiedeTests", "UnteragentGovernanceMonitoring", Guid.NewGuid().ToString("N")));
        await db.SaveChangesAsync();
        return (aufgabe, konfiguration);
    }

    private static async Task SchreibeTaskStateAsync(string verzeichnisPfad, int tokensUsed, int tokenLimit, TimeSpan gestartetVor, int laufzeitLimitMinuten)
    {
        var statePfad = Path.Combine(verzeichnisPfad, "task_state.json");
        await File.WriteAllTextAsync(statePfad, JsonSerializer.Serialize(new
        {
            tokens_used = tokensUsed,
            token_limit = tokenLimit,
            started_utc = DateTimeOffset.UtcNow - gestartetVor,
            runtime_limit_minutes = laufzeitLimitMinuten
        }));
    }

    private ServiceProvider BuildProvider(SoftwareschmiededDbContext db)
    {
        var services = new ServiceCollection();
        services.AddSingleton(db);
        services.AddSingleton<TimeProvider>(_timeProvider);
        services.AddSingleton<Microsoft.Extensions.Logging.ILogger<UnteragentGovernanceService>>(NullLogger<UnteragentGovernanceService>.Instance);
        services.AddScoped<UnteragentGovernanceService>();
        services.AddSingleton<Microsoft.Extensions.Logging.ILogger<UnteragentGovernanceMonitoringService>>(NullLogger<UnteragentGovernanceMonitoringService>.Instance);
        services.AddSingleton<UnteragentGovernanceMonitoringService>();
        return services.BuildServiceProvider();
    }
}
