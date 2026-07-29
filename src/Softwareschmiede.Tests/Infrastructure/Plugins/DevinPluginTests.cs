using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Infrastructure.Plugins;

namespace Softwareschmiede.Tests.Infrastructure.Plugins;

/// <summary>Tests fuer das Devin-CLI-Plugin.</summary>
public sealed class DevinPluginTests
{
    private readonly Mock<ICredentialStore> _credentialStoreMock;
    private readonly DevinPlugin _sut;

    /// <summary>Initialisiert eine neue Instanz der <see cref="DevinPluginTests"/>.</summary>
    public DevinPluginTests()
    {
        _credentialStoreMock = new Mock<ICredentialStore>();
        _sut = new DevinPlugin(
            _credentialStoreMock.Object,
            new Mock<ILogger<DevinPlugin>>().Object);
    }

    /// <summary>Exposes expected plugin metadata.</summary>
    [Fact]
    public void PluginMetadata_ShouldExposeExpectedValues()
    {
        _sut.PluginName.Should().Be("Devin CLI");
        _sut.PluginPrefix.Should().Be("Softwareschmiede.Devin");
        _sut.ProviderDateiPraefix.Should().Be("devin");
        _sut.PluginType.Should().Be(PluginType.DevelopmentAutomation);

        var settings = _sut.GetSettingGroups();
        settings.Should().HaveCount(2);
        settings.Should().Contain(group => group.GroupName == "Ausfuehrung");
        settings.Should().Contain(group => group.GroupName == "CLI-Konfiguration");
        settings.SelectMany(g => g.Fields).Should().Contain(f => f.Key == "ExecutablePath");
        settings.SelectMany(g => g.Fields).Should().Contain(f => f.Key == "CommandLineParameters");
        settings.SelectMany(g => g.Fields).Should().NotContain(f =>
            f.Key.Contains("Token", StringComparison.OrdinalIgnoreCase)
            || f.Key.Contains("ApiKey", StringComparison.OrdinalIgnoreCase)
            || f.FieldType == PluginSettingFieldType.Secret);
    }

    /// <summary>SupportsSessionContinuation returns true because Devin supports CLI session resume flags.</summary>
    [Fact]
    public void SupportsSessionContinuation_ShouldReturnTrue()
    {
        _sut.SupportsSessionContinuation().Should().BeTrue();
    }

    /// <summary>StartCliAsync uses default devin command when no path is configured.</summary>
    [Fact]
    public async Task StartCliAsync_ShouldUseDevinCommand_WhenNoPathConfigured()
    {
        _credentialStoreMock.Setup(store => store.GetCredential(It.IsAny<string>())).Returns((string?)null);

        var psi = await _sut.StartCliAsync("/repo/path");

        psi.FileName.Should().Be("devin");
        psi.WorkingDirectory.Should().Be("/repo/path");
        psi.Arguments.Should().BeEmpty();
        psi.UseShellExecute.Should().BeFalse();
        psi.CreateNoWindow.Should().BeFalse();
    }

    /// <summary>StartCliAsync uses configured executable path when provided.</summary>
    [Fact]
    public async Task StartCliAsync_ShouldUseConfiguredPath_WhenExecutablePathIsSet()
    {
        _credentialStoreMock.Setup(store => store.GetCredential("Softwareschmiede.Devin.ExecutablePath"))
            .Returns(@"C:\tools\devin.exe");

        var psi = await _sut.StartCliAsync("/repo");

        psi.FileName.Should().Be(@"C:\tools\devin.exe");
    }

    /// <summary>StartCliAsync trims quotes from configured executable path.</summary>
    [Fact]
    public async Task StartCliAsync_ShouldTrimQuotes_WhenExecutablePathIsSet()
    {
        _credentialStoreMock.Setup(store => store.GetCredential("Softwareschmiede.Devin.ExecutablePath"))
            .Returns("\"C:\\tools\\devin.exe\"");

        var psi = await _sut.StartCliAsync("/repo");

        psi.FileName.Should().Be(@"C:\tools\devin.exe");
    }

    /// <summary>StartCliAsync passes optional Devin CLI parameters as arguments.</summary>
    [Theory]
    [InlineData("Build the project")]
    [InlineData("--continue")]
    [InlineData("-c")]
    [InlineData("--resume session-123")]
    [InlineData("-r session-123")]
    [InlineData("--print")]
    [InlineData("--prompt-file prompt.md")]
    [InlineData("--model sonnet")]
    [InlineData("--permission-mode plan")]
    public async Task StartCliAsync_ShouldPassOptionalDevinParameters_AsArguments(string parameters)
    {
        var psi = await _sut.StartCliAsync("/repo", parameters);

        psi.Arguments.Should().Be(parameters);
    }

    /// <summary>StartCliAsync appends CommandLineParameters from credential store to arguments.</summary>
    [Fact]
    public async Task StartCliAsync_ShouldIncludeCommandLineParameters_InProcessStartInfo()
    {
        _credentialStoreMock.Setup(store => store.GetCredential("Softwareschmiede.Devin.CommandLineParameters"))
            .Returns("--permission-mode plan --model sonnet");

        var psi = await _sut.StartCliAsync("/repo", "--continue");

        psi.Arguments.Should().Be("--continue --permission-mode plan --model sonnet");
    }

    /// <summary>StartCliAsync does not create Devin credential arguments or environment variables.</summary>
    [Fact]
    public async Task StartCliAsync_ShouldNotSetAuthEnvironmentVariablesOrCredentialArguments()
    {
        _credentialStoreMock.Setup(store => store.GetCredential("Softwareschmiede.Devin.Token"))
            .Returns("token-must-not-be-used");
        _credentialStoreMock.Setup(store => store.GetCredential("Softwareschmiede.Devin.ApiKey"))
            .Returns("key-must-not-be-used");

        var psi = await _sut.StartCliAsync("/repo");

        psi.Arguments.Should().NotContain("token-must-not-be-used");
        psi.Arguments.Should().NotContain("key-must-not-be-used");
        psi.EnvironmentVariables.ContainsKey("DEVIN_API_KEY").Should().BeFalse();
        psi.EnvironmentVariables.ContainsKey("DEVIN_TOKEN").Should().BeFalse();
    }
}
