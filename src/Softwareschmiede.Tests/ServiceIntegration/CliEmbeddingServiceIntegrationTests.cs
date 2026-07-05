using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.ServiceIntegration;

/// <summary>E2E-Test: CLI-Prozess starten und Fenster in WPF-Control einbetten.</summary>
public sealed class CliEmbeddingServiceIntegrationTests : IDisposable
{
    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiededDbContext _db;
    private readonly KiAusfuehrungsService _kiService;

    /// <summary>CliEmbeddingServiceIntegrationTests.</summary>
    public CliEmbeddingServiceIntegrationTests()
    {
        _db = TestDbContextFactory.Create();
        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        _kiService = new KiAusfuehrungsService(NullLogger<KiAusfuehrungsService>.Instance, NullLoggerFactory.Instance, scopeFactoryMock.Object);
    }

    /// <summary>Dispose.</summary>
    public void Dispose()
    {
        _kiService.Dispose();
        _db.Dispose();
    }

    /// <summary>IsRunning gibt false zurück wenn kein Prozess gestartet wurde.</summary>
    [Fact]
    public void IsRunning_GibtFalse_WennKeinProzessGestartet()
    {
        var aufgabeId = Guid.NewGuid();
        _kiService.IsRunning(aufgabeId).Should().BeFalse();
    }

    /// <summary>GetLastExitCode gibt null zurück wenn kein Prozess gestartet wurde.</summary>
    [Fact]
    public void GetLastExitCode_GibtNull_WennKeinProzessGestartet()
    {
        var aufgabeId = Guid.NewGuid();
        _kiService.GetLastExitCode(aufgabeId).Should().BeNull();
    }

    /// <summary>StartCliAsync startet einen Prozess und setzt IsRunning auf true.</summary>
    [Fact]
    public async Task StartCliAsync_StartetProzess_UndSetztIsRunningAufTrue()
    {
        var aufgabeId = Guid.NewGuid();
        var pluginMock = new FakeKiPlugin();

        var handle = await _kiService.StartCliAsync(aufgabeId, pluginMock, Path.GetTempPath());

        handle.Should().NotBeNull();
        handle.AufgabeId.Should().Be(aufgabeId);

        await _kiService.StopCliAsync(aufgabeId);
    }

    /// <summary>StartWithPseudoConsoleAsync startet einen Prozess via ConPTY und liefert eine PseudoConsoleSession.</summary>
    [Fact]
    [Trait("Category", "ConPTY")]
    public async Task StartWithPseudoConsoleAsync_StartetProzess_UndSetztPseudoConsoleSession()
    {
        var aufgabeId = Guid.NewGuid();
        var pluginMock = new FakeKiPlugin();

        var handle = await _kiService.StartWithPseudoConsoleAsync(aufgabeId, pluginMock, Path.GetTempPath());

        handle.Should().NotBeNull();
        handle.AufgabeId.Should().Be(aufgabeId);
        handle.PseudoConsoleSession.Should().NotBeNull("StartWithPseudoConsoleAsync muss eine PseudoConsoleSession liefern");

        await _kiService.StopCliAsync(aufgabeId);
    }
}

/// <summary>Fake-KI-Plugin für Tests.</summary>
internal sealed class FakeKiPlugin : IKiPlugin
{
    public string PluginName => "FakeKi";
    public string PluginPrefix => "fake";
    public Softwareschmiede.Domain.Enums.PluginType PluginType => Softwareschmiede.Domain.Enums.PluginType.DevelopmentAutomation;

    /// <summary>IReadOnlyList.</summary>
    public IReadOnlyList<Softwareschmiede.Domain.ValueObjects.PluginSettingGroup> GetSettingGroups()
        => Array.Empty<Softwareschmiede.Domain.ValueObjects.PluginSettingGroup>();

    /// <summary>Task.</summary>
    public Task<System.Diagnostics.ProcessStartInfo> StartCliAsync(
        string localRepoPath,
        string? parameters = null,
        CancellationToken ct = default)
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = "/c exit 0",
            CreateNoWindow = true,
            UseShellExecute = false
        };
        return Task.FromResult(psi);
    }

    /// <summary>GetProcessWindowTitle.</summary>
    public string GetProcessWindowTitle(Guid aufgabeId) => "FakeKi";

    /// <summary>SupportsSessionContinuation.</summary>
    public bool SupportsSessionContinuation() => false;

    /// <summary>Task.</summary>
    public Task<bool> CheckHealthAsync(CancellationToken ct = default) => Task.FromResult(true);
}
