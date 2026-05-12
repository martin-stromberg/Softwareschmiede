using FluentAssertions;
using Softwareschmiede.Domain.Abstractions;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Tests.Domain.Abstractions;

/// <summary>Tests for file/path helpers in CliKiPluginBase.</summary>
public sealed class CliKiPluginBaseTests
{
    /// <summary>Builds expected provider-specific context and task names.</summary>
    [Fact]
    public void BuildFileNames_ShouldIncludeProviderPrefix()
    {
        var sut = new TestCliKiPlugin("claude");
        var id = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var runId = Guid.Parse("11111111-2222-3333-4444-555555555555");

        sut.BuildContextFileName(id).Should().Be("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee.claude.context.md");
        sut.BuildTaskFileName(runId).Should().Be("11111111-2222-3333-4444-555555555555.claude-task.md");
    }

    /// <summary>Builds expected provider-specific context and task paths.</summary>
    [Fact]
    public void BuildFilePaths_ShouldCombineRepoPathAndFileName()
    {
        var sut = new TestCliKiPlugin("copilot");
        var repoPath = @"C:\repos\demo";
        var id = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var runId = Guid.Parse("11111111-2222-3333-4444-555555555555");

        sut.BuildContextFilePath(repoPath, id)
            .Should().Be(Path.Combine(repoPath, "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee.copilot.context.md"));
        sut.BuildTaskFilePath(repoPath, runId)
            .Should().Be(Path.Combine(repoPath, "11111111-2222-3333-4444-555555555555.copilot-task.md"));
    }

    /// <summary>Preserves unusual provider prefixes for deterministic naming.</summary>
    [Fact]
    public void BuildFileNames_ShouldPreserveCustomPrefix()
    {
        var sut = new TestCliKiPlugin("custom.prefix");
        var id = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

        sut.BuildContextFileName(id).Should().Be("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee.custom.prefix.context.md");
    }

    private sealed class TestCliKiPlugin(string prefix) : CliKiPluginBase
    {
        public override string ProviderDateiPraefix { get; } = prefix;
        public override string PluginName => "Test";
        public override string PluginPrefix => "Softwareschmiede.Test";
        public override PluginType PluginType => PluginType.DevelopmentAutomation;
        public override IReadOnlyList<PluginSettingGroup> GetSettingGroups() => [];
        public override Task<IEnumerable<AgentInfo>> GetAvailableAgentsAsync(string agentPackagePath, CancellationToken ct = default) => Task.FromResult<IEnumerable<AgentInfo>>([]);
        public override Task<bool> IsAgentPackageCompatibleAsync(string agentPackagePath, CancellationToken ct = default) => Task.FromResult(true);
        public override Task DeployAgentPackageAsync(string agentPackagePath, string localRepoPath, CancellationToken ct = default) => Task.CompletedTask;
        public override IAsyncEnumerable<string> StartDevelopmentAsync(string prompt, AgentInfo agent, string localRepoPath, string? model = null, CancellationToken ct = default) => EmptyStream();
        public override Task<TestResult> RunTestsAsync(string localRepoPath, CancellationToken ct = default) => Task.FromResult(new TestResult(true, []));
        public override Task<bool> CheckHealthAsync(CancellationToken ct = default) => Task.FromResult(true);

        private static async IAsyncEnumerable<string> EmptyStream()
        {
            await Task.CompletedTask;
            yield break;
        }
    }
}
