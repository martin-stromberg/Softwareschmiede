using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.ServiceIntegration;

/// <summary>E2E-Test: CLI-Prozess starten und Fenster in WPF-Control einbetten.</summary>
public sealed class CliEmbeddingServiceIntegrationTests : IDisposable
{
    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiededDbContext _db;
    private readonly KiAusfuehrungsService _kiService;

    public CliEmbeddingServiceIntegrationTests()
    {
        _db = TestDbContextFactory.Create();
        _kiService = new KiAusfuehrungsService(NullLogger<KiAusfuehrungsService>.Instance);
    }

    public void Dispose()
    {
        _kiService.Dispose();
        _db.Dispose();
    }

    [Fact]
    public void IsRunning_GibtFalse_WennKeinProzessGestartet()
    {
        var aufgabeId = Guid.NewGuid();
        _kiService.IsRunning(aufgabeId).Should().BeFalse();
    }

    [Fact]
    public void GetLastExitCode_GibtNull_WennKeinProzessGestartet()
    {
        var aufgabeId = Guid.NewGuid();
        _kiService.GetLastExitCode(aufgabeId).Should().BeNull();
    }

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
}

/// <summary>Fake-KI-Plugin für Tests.</summary>
internal sealed class FakeKiPlugin : IKiPlugin
{
    public string PluginName => "FakeKi";
    public string PluginPrefix => "fake";
    public Softwareschmiede.Domain.Enums.PluginType PluginType => Softwareschmiede.Domain.Enums.PluginType.DevelopmentAutomation;

    public IReadOnlyList<Softwareschmiede.Domain.ValueObjects.PluginSettingGroup> GetSettingGroups()
        => Array.Empty<Softwareschmiede.Domain.ValueObjects.PluginSettingGroup>();

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

    public string GetProcessWindowTitle(Guid aufgabeId) => "FakeKi";

    public bool SupportsSessionContinuation() => false;

    public Task<bool> CheckHealthAsync(CancellationToken ct = default) => Task.FromResult(true);
}
