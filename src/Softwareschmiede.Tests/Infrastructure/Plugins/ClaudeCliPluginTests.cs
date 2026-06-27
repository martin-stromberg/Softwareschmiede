using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Infrastructure.Plugins;

namespace Softwareschmiede.Tests.Infrastructure.Plugins;

/// <summary>Tests for the Claude CLI plugin.</summary>
public sealed class ClaudeCliPluginTests
{
    private readonly Mock<ICredentialStore> _credentialStoreMock;
    private readonly ClaudeCliPlugin _sut;

    /// <summary>ClaudeCliPluginTests.</summary>
    public ClaudeCliPluginTests()
    {
        _credentialStoreMock = new Mock<ICredentialStore>();
        _sut = new ClaudeCliPlugin(
            _credentialStoreMock.Object,
            new Mock<ILogger<ClaudeCliPlugin>>().Object);
    }

    /// <summary>Exposes static plugin metadata.</summary>
    [Fact]
    public void PluginMetadata_ShouldExposeExpectedValues()
    {
        _sut.PluginName.Should().Be("Claude CLI");
        _sut.PluginPrefix.Should().Be("Softwareschmiede.ClaudeCli");
        _sut.ProviderDateiPraefix.Should().Be("claude");
        _sut.GetSettingGroups().Single().Fields.Should().ContainSingle(f => f.Key == "Token");
    }

    /// <summary>SupportsSessionContinuation returns true.</summary>
    [Fact]
    public void SupportsSessionContinuation_ShouldReturnTrue()
    {
        _sut.SupportsSessionContinuation().Should().BeTrue();
    }

    /// <summary>StartCliAsync returns ProcessStartInfo with claude as filename (absolute path from PATH or fallback).</summary>
    [Fact]
    public async Task StartCliAsync_ShouldReturnProcessStartInfo_WithClaudeCommand()
    {
        var psi = await _sut.StartCliAsync("/repo/path");

        Path.GetFileNameWithoutExtension(psi.FileName).Should().BeEquivalentTo("claude");
        psi.WorkingDirectory.Should().Be("/repo/path");
    }

    /// <summary>StartCliAsync passes optional parameters as arguments.</summary>
    [Fact]
    public async Task StartCliAsync_ShouldPassParameters_AsArguments()
    {
        var psi = await _sut.StartCliAsync("/repo", "--continue session-123");

        psi.Arguments.Should().Be("--continue session-123");
    }

    /// <summary>StartCliAsync sets ANTHROPIC_API_KEY when credential is stored.</summary>
    [Fact]
    public async Task StartCliAsync_ShouldSetApiKeyEnvironmentVariable_WhenTokenIsStored()
    {
        _credentialStoreMock.Setup(c => c.GetCredential("Softwareschmiede.ClaudeCli.Token"))
            .Returns("sk-ant-test-key");

        var psi = await _sut.StartCliAsync("/repo");

        psi.EnvironmentVariables["ANTHROPIC_API_KEY"].Should().Be("sk-ant-test-key");
    }

    /// <summary>StartCliAsync does not set ANTHROPIC_API_KEY when no credential exists.</summary>
    [Fact]
    public async Task StartCliAsync_ShouldNotSetApiKey_WhenNoTokenStored()
    {
        _credentialStoreMock.Setup(c => c.GetCredential("Softwareschmiede.ClaudeCli.Token"))
            .Returns((string?)null);

        var psi = await _sut.StartCliAsync("/repo");

        psi.EnvironmentVariables.ContainsKey("ANTHROPIC_API_KEY").Should().BeFalse();
    }
}
