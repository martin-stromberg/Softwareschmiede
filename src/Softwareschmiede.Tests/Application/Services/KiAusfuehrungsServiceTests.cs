using System.Runtime.CompilerServices;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>Tests für den KiAusfuehrungsService.</summary>
public sealed class KiAusfuehrungsServiceTests : IDisposable
{
    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiededDbContext _db;
    private readonly AufgabeService _aufgabeService;
    private readonly ProtokollService _protokollService;
    private readonly Mock<IKiPlugin> _kiPluginMock;
    private readonly KiAusfuehrungsService _sut;
    private readonly Guid _projektId = new("66666666-6666-6666-6666-666666666666");

    public KiAusfuehrungsServiceTests()
    {
        _db = TestDbContextFactory.Create();
        _aufgabeService = new AufgabeService(_db, new Mock<ILogger<AufgabeService>>().Object);
        _protokollService = new ProtokollService(_db, new Mock<ILogger<ProtokollService>>().Object);
        _kiPluginMock = new Mock<IKiPlugin>();

        var gitPluginMock = new Mock<IGitPlugin>();
        var agentPackageServiceMock = new Mock<IAgentPackageService>();
        var arbeitsverzeichnisResolverMock = new Mock<IArbeitsverzeichnisResolver>();
        arbeitsverzeichnisResolverMock.Setup(r => r.ResolveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ArbeitsverzeichnisResolutionResult(Path.GetTempPath(), false, "configured", null));

        var entwicklungsprozessService = new EntwicklungsprozessService(
            _aufgabeService,
            _protokollService,
            gitPluginMock.Object,
            _kiPluginMock.Object,
            agentPackageServiceMock.Object,
            arbeitsverzeichnisResolverMock.Object,
            new Mock<ILogger<EntwicklungsprozessService>>().Object);

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddScoped(_ => entwicklungsprozessService);
        var rootProvider = serviceCollection.BuildServiceProvider();

        _sut = new KiAusfuehrungsService(
            rootProvider.GetRequiredService<IServiceScopeFactory>(),
            new Mock<ILogger<KiAusfuehrungsService>>().Object);

        _db.Projekte.Add(new Softwareschmiede.Domain.Entities.Projekt
        {
            Id = _projektId,
            Name = "KiAusfuehrungsService-Testprojekt",
            ErstellungsDatum = DateTimeOffset.UtcNow,
            Status = ProjektStatus.Aktiv
        });
        _db.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    /// <summary>StartKiLauf leitet die ExecutionId unverändert an den Entwicklungsprozess weiter.</summary>
    [Fact]
    public async Task StartKiLauf_ShouldForwardExecutionId_WhenProvided()
    {
        var aufgabe = await CreateStartedAufgabeAsync("ExecutionId weiterleiten");
        var agent = new AgentInfo("test-agent", "Beschreibung", "/pfad/agent.md");
        const string executionId = "8934d2575588473e98829b19d322851b";
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        _kiPluginMock.Setup(k => k.StartDevelopmentAsync(
                "prompt",
                agent,
                "/pfad",
                null,
                executionId,
                It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable("line-1", "line-2"));

        _sut.StartKiLauf(
            aufgabe.Id,
            "prompt",
            agent,
            executionId: executionId,
            onCompleted: fehler => completion.TrySetResult(fehler));

        var isError = await completion.Task.WaitAsync(TimeSpan.FromSeconds(5));

        isError.Should().BeFalse();
        _sut.IsRunning(aufgabe.Id).Should().BeFalse();
        _sut.GetBufferedLines(aufgabe.Id).Should().ContainInOrder("line-1", "line-2");
        _kiPluginMock.Verify(k => k.StartDevelopmentAsync(
            "prompt",
            agent,
            "/pfad",
            null,
            executionId,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>StartKiLauf leitet ein explizit gesetztes Modell an den Entwicklungsprozess weiter.</summary>
    [Fact]
    public async Task StartKiLauf_ShouldForwardModel_WhenProvided()
    {
        var aufgabe = await CreateStartedAufgabeAsync("Model weiterleiten");
        var agent = new AgentInfo("test-agent", "Beschreibung", "/pfad/agent.md");
        const string model = "gpt-5";
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        _kiPluginMock.Setup(k => k.StartDevelopmentAsync(
                "prompt",
                agent,
                "/pfad",
                model,
                null,
                It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable("line-1"));

        _sut.StartKiLauf(
            aufgabe.Id,
            "prompt",
            agent,
            model: model,
            onCompleted: fehler => completion.TrySetResult(fehler));

        var isError = await completion.Task.WaitAsync(TimeSpan.FromSeconds(5));

        isError.Should().BeFalse();
        _kiPluginMock.Verify(k => k.StartDevelopmentAsync(
            "prompt",
            agent,
            "/pfad",
            model,
            null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Whitespace-ExecutionId wird als nicht gesetzt behandelt und als null weitergereicht.</summary>
    [Fact]
    public async Task StartKiLauf_ShouldForwardNullExecutionId_WhenWhitespaceExecutionIdIsProvided()
    {
        var aufgabe = await CreateStartedAufgabeAsync("Whitespace ExecutionId");
        var agent = new AgentInfo("test-agent", "Beschreibung", "/pfad/agent.md");
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        _kiPluginMock.Setup(k => k.StartDevelopmentAsync(
                "prompt",
                agent,
                "/pfad",
                null,
                null,
                It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable("line-1"));

        _sut.StartKiLauf(
            aufgabe.Id,
            "prompt",
            agent,
            executionId: "   ",
            onCompleted: fehler => completion.TrySetResult(fehler));

        var isError = await completion.Task.WaitAsync(TimeSpan.FromSeconds(5));

        isError.Should().BeFalse();
        _kiPluginMock.Verify(k => k.StartDevelopmentAsync(
            "prompt",
            agent,
            "/pfad",
            null,
            null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>StartKiLauf weist einen zweiten Start ab solange bereits ein Lauf aktiv ist.</summary>
    [Fact]
    public async Task StartKiLauf_ShouldRejectSecondStart_WhenSessionIsRunning()
    {
        var aufgabe = await CreateStartedAufgabeAsync("Doppelstart verhindern");
        var agent = new AgentInfo("test-agent", "Beschreibung", "/pfad/agent.md");
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        _kiPluginMock.Setup(k => k.StartDevelopmentAsync(
                It.IsAny<string>(),
                It.IsAny<AgentInfo>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns((string _, AgentInfo _, string _, string? _, string? _, CancellationToken ct) =>
                ToBlockingAsyncEnumerable(gate.Task, ct));

        _sut.StartKiLauf(
            aufgabe.Id,
            "prompt",
            agent,
            onCompleted: fehler => completion.TrySetResult(fehler));

        await WaitUntilAsync(() => _sut.IsRunning(aufgabe.Id));

        _sut.StartKiLauf(aufgabe.Id, "prompt", agent);

        gate.TrySetResult();
        await completion.Task.WaitAsync(TimeSpan.FromSeconds(5));

        _kiPluginMock.Verify(k => k.StartDevelopmentAsync(
            It.IsAny<string>(),
            It.IsAny<AgentInfo>(),
            It.IsAny<string>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>AbortKiLauf bricht den Lauf ab und SessionBereinigen entfernt die abgeschlossene Session.</summary>
    [Fact]
    public async Task AbortKiLauf_ShouldCancelRun_AndSessionBereinigenShouldRemoveSession()
    {
        var aufgabe = await CreateStartedAufgabeAsync("Abbruch");
        var agent = new AgentInfo("test-agent", "Beschreibung", "/pfad/agent.md");
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        _kiPluginMock.Setup(k => k.StartDevelopmentAsync(
                It.IsAny<string>(),
                It.IsAny<AgentInfo>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns((string _, AgentInfo _, string _, string? _, string? _, CancellationToken ct) =>
                ToCancelableAsyncEnumerable(ct));

        _sut.StartKiLauf(
            aufgabe.Id,
            "prompt",
            agent,
            onCompleted: fehler => completion.TrySetResult(fehler));

        await WaitUntilAsync(() => _sut.IsRunning(aufgabe.Id));
        _sut.AbortKiLauf(aufgabe.Id);

        _ = await completion.Task.WaitAsync(TimeSpan.FromSeconds(5));
        _sut.IsRunning(aufgabe.Id).Should().BeFalse();
        var updatedAufgabe = await _aufgabeService.GetByIdAsync(aufgabe.Id);
        updatedAufgabe!.Status.Should().BeOneOf(AufgabeStatus.InBearbeitung, AufgabeStatus.Fehlgeschlagen);

        _sut.SessionBereinigen(aufgabe.Id);
        _sut.GetBufferedLines(aufgabe.Id).Should().BeEmpty();
    }

    private async Task<Softwareschmiede.Domain.Entities.Aufgabe> CreateStartedAufgabeAsync(string titel)
    {
        var aufgabe = await _aufgabeService.CreateAsync(_projektId, titel, null);
        await _aufgabeService.StartenAsync(aufgabe.Id, "branch", "/pfad");
        return aufgabe;
    }

    private static async Task WaitUntilAsync(Func<bool> condition, int timeoutMs = 2000)
    {
        var start = DateTime.UtcNow;
        while (!condition())
        {
            if ((DateTime.UtcNow - start).TotalMilliseconds > timeoutMs)
            {
                throw new TimeoutException("Condition was not met in time.");
            }

            await Task.Delay(20);
        }
    }

    private static async IAsyncEnumerable<string> ToAsyncEnumerable(params string[] lines)
    {
        foreach (var line in lines)
        {
            yield return line;
            await Task.Yield();
        }
    }

    private static async IAsyncEnumerable<string> ToBlockingAsyncEnumerable(
        Task gate,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        yield return "line-1";
        await gate.WaitAsync(ct);
    }

    private static async IAsyncEnumerable<string> ToCancelableAsyncEnumerable(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        yield return "line-1";
        await Task.Delay(Timeout.InfiniteTimeSpan, ct);
    }
}
