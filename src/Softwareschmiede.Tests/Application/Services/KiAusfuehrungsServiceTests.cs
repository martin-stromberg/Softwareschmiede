using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>Tests für die KI-Ausgabe-Pufferung.</summary>
public sealed class KiAusfuehrungsServiceTests
{
    /// <summary>Startet für neue Blöcke mit Leerzeile und Zeitstempel.</summary>
    [Fact]
    public void AddLine_ShouldInsertBlockHeader_WhenOutputGapExceedsThreshold()
    {
        // Arrange
        var current = new DateTimeOffset(2026, 5, 10, 13, 0, 0, TimeSpan.Zero);
        var session = new KiSession(() => current);

        // Act
        session.AddLine("Erste Zeile");
        current = current.AddSeconds(3);
        session.AddLine("Zweite Zeile");

        // Assert
        var lines = session.GetLines();
        lines.Should().HaveCount(6);
        lines[0].Should().BeEmpty();
        lines[1].Should().MatchRegex(@"^\d{2}\.\d{2}\.\d{4} \d{2}:\d{2}:\d{2}$");
        lines[2].Should().Be("Erste Zeile");
        lines[3].Should().BeEmpty();
        lines[4].Should().MatchRegex(@"^\d{2}\.\d{2}\.\d{4} \d{2}:\d{2}:\d{2}$");
        lines[5].Should().Be("Zweite Zeile");
    }

    /// <summary>Fasst nahe Ausgaben zu einem Block zusammen.</summary>
    [Fact]
    public void AddLine_ShouldKeepLinesInOneBlock_WhenOutputGapStaysBelowThreshold()
    {
        // Arrange
        var current = new DateTimeOffset(2026, 5, 10, 13, 0, 0, TimeSpan.Zero);
        var session = new KiSession(() => current);

        // Act
        session.AddLine("Erste Zeile");
        current = current.AddSeconds(1);
        session.AddLine("Zweite Zeile");

        // Assert
        var lines = session.GetLines();
        lines.Should().HaveCount(4);
        lines[0].Should().BeEmpty();
        lines[1].Should().MatchRegex(@"^\d{2}\.\d{2}\.\d{4} \d{2}:\d{2}:\d{2}$");
        lines[2].Should().Be("Erste Zeile");
        lines[3].Should().Be("Zweite Zeile");
    }

    /// <summary>Startet KI-Lauf und ruft Callbacks im Erfolgsfall auf.</summary>
    [Fact]
    public async Task StartKiLauf_ShouldInvokeCallbacksAndBufferOutput_WhenRunSucceeds()
    {
        // Arrange
        await using var harness = await KiAusfuehrungsServiceHarness.CreateAsync(() => StreamFromLines("Zeile A", "Zeile B"));
        var startedCount = 0;
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        // Act
        harness.Service.StartKiLauf(
            harness.AufgabeId,
            "Prompt",
            harness.Agent,
            onStarted: () => startedCount++,
            onCompleted: hasError => completion.TrySetResult(hasError));
        var hasError = await completion.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        startedCount.Should().Be(1);
        hasError.Should().BeFalse();
        harness.Service.IsRunning(harness.AufgabeId).Should().BeFalse();
        harness.Service.GetBufferedLines(harness.AufgabeId).Should().Contain(l => l.Contains("Zeile A", StringComparison.Ordinal));
    }

    /// <summary>Verhindert Doppeltstart für dieselbe Aufgabe.</summary>
    [Fact]
    public async Task StartKiLauf_ShouldRejectSecondStart_WhenAlreadyRunning()
    {
        // Arrange
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var harness = await KiAusfuehrungsServiceHarness.CreateAsync(() => StreamWithGate(release.Task));
        var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondStartedCount = 0;

        // Act
        harness.Service.StartKiLauf(
            harness.AufgabeId,
            "Prompt 1",
            harness.Agent,
            onStarted: () => firstStarted.TrySetResult(),
            onCompleted: _ => firstCompleted.TrySetResult());
        await firstStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        harness.Service.IsRunning(harness.AufgabeId).Should().BeTrue();

        harness.Service.StartKiLauf(
            harness.AufgabeId,
            "Prompt 2",
            harness.Agent,
            onStarted: () => secondStartedCount++,
            onCompleted: _ => secondStartedCount++);

        release.TrySetResult();
        await firstCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        secondStartedCount.Should().Be(0);
        harness.KiPluginMock.Verify(
            k => k.StartDevelopmentAsync(
                It.IsAny<string>(),
                It.IsAny<AgentInfo>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>Markiert Lauf als Fehler und puffert Fehlermeldung bei Ausnahme.</summary>
    [Fact]
    public async Task StartKiLauf_ShouldMarkError_WhenBackgroundRunThrows()
    {
        // Arrange
        await using var harness = await KiAusfuehrungsServiceHarness.CreateAsync(() => StreamFromLines("unused"), startTask: false);
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        // Act
        harness.Service.StartKiLauf(
            harness.AufgabeId,
            "Prompt",
            harness.Agent,
            onCompleted: hasError => completion.TrySetResult(hasError));
        var hasError = await completion.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        hasError.Should().BeTrue();
        harness.Service.IsRunning(harness.AufgabeId).Should().BeFalse();
        harness.Service.GetBufferedLines(harness.AufgabeId).Should().Contain(l => l.Contains("[Fehler]", StringComparison.Ordinal));
    }

    private static async IAsyncEnumerable<string> StreamFromLines(params string[] lines)
    {
        foreach (var line in lines)
        {
            yield return line;
            await Task.Yield();
        }
    }

    private static async IAsyncEnumerable<string> StreamWithGate(Task gate)
    {
        yield return "läuft";
        await gate;
        yield return "fertig";
    }

    private sealed class KiAusfuehrungsServiceHarness : IAsyncDisposable
    {
        private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiededDbContext _db;
        private readonly string _repoPath;
        private readonly ServiceProvider _serviceProvider;

        private KiAusfuehrungsServiceHarness(
            Softwareschmiede.Infrastructure.Data.SoftwareschmiededDbContext db,
            string repoPath,
            ServiceProvider serviceProvider,
            KiAusfuehrungsService service,
            Mock<IKiPlugin> kiPluginMock,
            Guid aufgabeId,
            AgentInfo agent)
        {
            _db = db;
            _repoPath = repoPath;
            _serviceProvider = serviceProvider;
            Service = service;
            KiPluginMock = kiPluginMock;
            AufgabeId = aufgabeId;
            Agent = agent;
        }

        public KiAusfuehrungsService Service { get; }

        public Mock<IKiPlugin> KiPluginMock { get; }

        public Guid AufgabeId { get; }

        public AgentInfo Agent { get; }

        public static async Task<KiAusfuehrungsServiceHarness> CreateAsync(
            Func<IAsyncEnumerable<string>> streamFactory,
            bool startTask = true)
        {
            var db = TestDbContextFactory.Create();
            var aufgabeService = new AufgabeService(db, NullLogger<AufgabeService>.Instance);
            var protokollService = new ProtokollService(db, NullLogger<ProtokollService>.Instance);
            var repoPath = Path.Combine(Path.GetTempPath(), $"ki-ausfuehrung-{Guid.NewGuid():N}");
            Directory.CreateDirectory(repoPath);

            var projekt = new Projekt
            {
                Id = Guid.NewGuid(),
                Name = "KiAusfuehrung Projekt",
                Status = ProjektStatus.Aktiv,
                ErstellungsDatum = DateTimeOffset.UtcNow
            };
            db.Projekte.Add(projekt);
            await db.SaveChangesAsync();

            var aufgabe = await aufgabeService.CreateAsync(projekt.Id, "KiAusfuehrung Aufgabe", null);
            if (startTask)
            {
                await aufgabeService.StartenAsync(aufgabe.Id, "feature/test", repoPath);
            }

            var kiPluginMock = new Mock<IKiPlugin>();
            kiPluginMock
                .Setup(k => k.StartDevelopmentAsync(
                    It.IsAny<string>(),
                    It.IsAny<AgentInfo>(),
                    It.IsAny<string>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()))
                .Returns<string, AgentInfo, string, string?, CancellationToken>((_, _, _, _, _) => streamFactory());

            var entwicklungsprozessService = new EntwicklungsprozessService(
                aufgabeService,
                protokollService,
                new Mock<IGitPlugin>().Object,
                kiPluginMock.Object,
                new Mock<IAgentPackageService>().Object,
                new Mock<IArbeitsverzeichnisResolver>().Object,
                new ConfigurationBuilder().Build(),
                NullLogger<EntwicklungsprozessService>.Instance);

            var services = new ServiceCollection();
            services.AddScoped(_ => entwicklungsprozessService);
            var serviceProvider = services.BuildServiceProvider();
            var service = new KiAusfuehrungsService(
                serviceProvider.GetRequiredService<IServiceScopeFactory>(),
                NullLogger<KiAusfuehrungsService>.Instance);

            return new KiAusfuehrungsServiceHarness(
                db,
                repoPath,
                serviceProvider,
                service,
                kiPluginMock,
                aufgabe.Id,
                new AgentInfo("test-agent", "Beschreibung", "test.agent.md"));
        }

        public ValueTask DisposeAsync()
        {
            _serviceProvider.Dispose();
            _db.Dispose();
            if (Directory.Exists(_repoPath))
            {
                Directory.Delete(_repoPath, recursive: true);
            }

            return ValueTask.CompletedTask;
        }
    }
}
