using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Infrastructure.Plugins;

namespace Softwareschmiede.Tests.Infrastructure.Plugins;

/// <summary>Tests for the Codex CLI plugin.</summary>
public sealed class CodexPluginTests
{
    private readonly Mock<ICredentialStore> _credentialStoreMock;
    private readonly CodexPlugin _sut;

    /// <summary>CodexPluginTests.</summary>
    public CodexPluginTests()
    {
        _credentialStoreMock = new Mock<ICredentialStore>();
        _sut = new CodexPlugin(
            _credentialStoreMock.Object,
            new Mock<ILogger<CodexPlugin>>().Object);
    }

    /// <summary>Exposes expected plugin metadata.</summary>
    [Fact]
    public void PluginMetadata_ShouldExposeExpectedValues()
    {
        _sut.PluginName.Should().Be("Codex CLI");
        _sut.PluginPrefix.Should().Be("Softwareschmiede.Codex");
        _sut.ProviderDateiPraefix.Should().Be("codex");
        _sut.PluginType.Should().Be(PluginType.DevelopmentAutomation);

        var settings = _sut.GetSettingGroups();
        settings.Should().ContainSingle(group => group.GroupName == "Ausfuehrung");
        settings.SelectMany(group => group.Fields).Should().ContainSingle(field => field.Key == "ExecutablePath");
    }

    /// <summary>SupportsSessionContinuation returns false until a stable local CLI pattern exists.</summary>
    [Fact]
    public void SupportsSessionContinuation_ShouldReturnFalse()
    {
        _sut.SupportsSessionContinuation().Should().BeFalse();
    }

    /// <summary>StartCliAsync uses default codex command when no path is configured.</summary>
    [Fact]
    public async Task StartCliAsync_ShouldUseCodexCommand_WhenNoPathConfigured()
    {
        _credentialStoreMock.Setup(store => store.GetCredential(It.IsAny<string>())).Returns((string?)null);

        var psi = await _sut.StartCliAsync("/repo/path");

        psi.FileName.Should().Be("codex");
        psi.WorkingDirectory.Should().Be("/repo/path");
    }

    /// <summary>StartCliAsync uses configured executable path when provided.</summary>
    [Fact]
    public async Task StartCliAsync_ShouldUseConfiguredPath_WhenExecutablePathIsSet()
    {
        _credentialStoreMock.Setup(store => store.GetCredential("Softwareschmiede.Codex.ExecutablePath"))
            .Returns(@"C:\tools\codex.exe");

        var psi = await _sut.StartCliAsync("/repo");

        psi.FileName.Should().Be(@"C:\tools\codex.exe");
    }

    /// <summary>StartCliAsync trims quotes from configured executable path.</summary>
    [Fact]
    public async Task StartCliAsync_ShouldTrimQuotes_WhenExecutablePathIsSet()
    {
        _credentialStoreMock.Setup(store => store.GetCredential("Softwareschmiede.Codex.ExecutablePath"))
            .Returns("\"C:\\tools\\codex.exe\"");

        var psi = await _sut.StartCliAsync("/repo");

        psi.FileName.Should().Be(@"C:\tools\codex.exe");
    }

    /// <summary>StartCliAsync passes optional parameters as arguments.</summary>
    [Fact]
    public async Task StartCliAsync_ShouldPassParameters_AsArguments()
    {
        var psi = await _sut.StartCliAsync("/repo", "--some-flag");

        psi.Arguments.Should().Be("--some-flag");
    }

    /// <summary>StartCliAsync does not set an auth environment variable by default.</summary>
    [Fact]
    public async Task StartCliAsync_ShouldNotSetAuthEnvironmentVariable()
    {
        var psi = await _sut.StartCliAsync("/repo");

        psi.EnvironmentVariables.ContainsKey("OPENAI_API_KEY").Should().BeFalse();
        psi.EnvironmentVariables.ContainsKey("CODEX_API_KEY").Should().BeFalse();
    }
}
