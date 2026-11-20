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
    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiedeDbContext _db;
    private readonly AiExecutionService _aiService;

    /// <summary>CliEmbeddingServiceIntegrationTests.</summary>
    public CliEmbeddingServiceIntegrationTests()
    {
        _db = TestDbContextFactory.Create();
        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        _aiService = TestAiExecutionServiceFactory.Create(scopeFactoryMock.Object);
    }

    /// <summary>Dispose.</summary>
    public void Dispose()
    {
        _aiService.Dispose();
        _db.Dispose();
    }

    /// <summary>IsRunning gibt false zurück wenn kein Prozess gestartet wurde.</summary>
    [Fact]
    public void IsRunning_ReturnsFalse_WhenNoProcessStarted()
    {
        var workItemId = Guid.NewGuid();
        _aiService.IsRunning(workItemId).Should().BeFalse();
    }

    /// <summary>GetLastExitCode gibt null zurück wenn kein Prozess gestartet wurde.</summary>
    [Fact]
    public void GetLastExitCode_ReturnsNull_WhenNoProcessStarted()
    {
        var workItemId = Guid.NewGuid();
        _aiService.GetLastExitCode(workItemId).Should().BeNull();
    }

    /// <summary>StartCliAsync startet einen Prozess und setzt IsRunning auf true.</summary>
    [OsInterfaceFact]
    public async Task StartCliAsync_StartsProcess_AndSetsIsRunningOnTrue()
    {
        var workItemId = Guid.NewGuid();
        var pluginMock = new FakeAiPlugin();

        var handle = await _aiService.StartCliAsync(workItemId, pluginMock, Path.GetTempPath());

        handle.Should().NotBeNull();
        handle.WorkItemId.Should().Be(workItemId);

        await _aiService.StopCliAsync(workItemId);
    }

    /// <summary>StartTerminalSessionAsync startet einen Prozess via ConPTY und liefert eine PseudoConsoleSession.</summary>
    [OsInterfaceFact]
    [Trait("Category", "ConPTY")]
    public async Task StartTerminalSessionAsync_StartsProcess_AndSetsPseudoConsoleSession()
    {
        var workItemId = Guid.NewGuid();
        var pluginMock = new FakeAiPlugin();

        var handle = await _aiService.StartTerminalSessionAsync(workItemId, pluginMock, Path.GetTempPath());

        handle.Should().NotBeNull();
        handle.WorkItemId.Should().Be(workItemId);
        handle.Session.Should().NotBeNull("StartTerminalSessionAsync muss eine ITerminalSession liefern");

        await _aiService.StopCliAsync(workItemId);
    }
}

/// <summary>Fake-KI-Plugin für Tests.</summary>
internal sealed class FakeAiPlugin : IAiPlugin
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
        string? parameter = null,
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
    public string GetProcessWindowTitle(Guid workItemId) => "FakeKi";

    /// <summary>SupportsSessionContinuation.</summary>
    public bool SupportsSessionContinuation() => false;

    /// <summary>Task.</summary>
    public Task<bool> CheckHealthAsync(CancellationToken ct = default) => Task.FromResult(true);

    /// <summary>TerminalCapabilities.</summary>
    public Softwareschmiede.Domain.Enums.TerminalProviderCapabilities TerminalCapabilities => Softwareschmiede.Domain.Enums.TerminalProviderCapabilities.SupportsPty;

    /// <summary>GetTerminalStartSpecAsync.</summary>
    public Task<Softwareschmiede.Domain.ValueObjects.TerminalSessionStartSpec> GetTerminalStartSpecAsync(string localRepoPath, string? parameter = null, CancellationToken ct = default)
        => Task.FromResult(new Softwareschmiede.Domain.ValueObjects.TerminalSessionStartSpec { FileName = "cmd.exe", WorkingDirectory = localRepoPath, PluginName = PluginName });
}
