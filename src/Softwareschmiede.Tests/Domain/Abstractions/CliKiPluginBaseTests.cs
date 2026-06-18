using System.Diagnostics;
using FluentAssertions;
using Softwareschmiede.Domain.Abstractions;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Tests.Domain.Abstractions;

/// <summary>Tests für die Basisklasse CliKiPluginBase.</summary>
public sealed class CliKiPluginBaseTests
{
    /// <summary>StartCliAsync gibt ProcessStartInfo aus BuildProcessStartInfo zurück.</summary>
    [Fact]
    public async Task StartCliAsync_ShouldReturnProcessStartInfo_FromBuildProcessStartInfo()
    {
        var sut = new TestCliKiPlugin(supportsSession: false);

        var result = await sut.StartCliAsync("/repo/path");

        result.FileName.Should().Be("test-cli");
        result.WorkingDirectory.Should().Be("/repo/path");
    }

    /// <summary>SupportsSessionContinuation gibt den konfigurierten Wert zurück.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SupportsSessionContinuation_ShouldReturnConfiguredValue(bool expected)
    {
        var sut = new TestCliKiPlugin(supportsSession: expected);

        sut.SupportsSessionContinuation().Should().Be(expected);
    }

    /// <summary>StartCliAsync gibt den übergebenen Parameter an BuildProcessStartInfo weiter.</summary>
    [Fact]
    public async Task StartCliAsync_ShouldPassParameters_ToBuildProcessStartInfo()
    {
        var sut = new TestCliKiPlugin(supportsSession: true);

        var result = await sut.StartCliAsync("/repo", "session-123");

        result.Arguments.Should().Be("session-123");
    }

    private sealed class TestCliKiPlugin(bool supportsSession) : CliKiPluginBase
    {
        public override string ProviderDateiPraefix => "test";
        public override string PluginName => "Test";
        public override string PluginPrefix => "Softwareschmiede.Test";
        public override PluginType PluginType => PluginType.DevelopmentAutomation;
        public override IReadOnlyList<PluginSettingGroup> GetSettingGroups() => [];
        public override bool SupportsSessionContinuation() => supportsSession;
        public override Task<bool> CheckHealthAsync(CancellationToken ct = default) => Task.FromResult(true);

        protected override ProcessStartInfo BuildProcessStartInfo(string localRepoPath, string? parameters)
        {
            return new ProcessStartInfo
            {
                FileName = "test-cli",
                Arguments = parameters ?? string.Empty,
                WorkingDirectory = localRepoPath,
            };
        }
    }
}
