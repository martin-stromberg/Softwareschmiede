using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Softwareschmiede.Infrastructure.Plugins;

namespace Softwareschmiede.Tests.Infrastructure.Plugins;

/// <summary>KiSimulatorPluginTests.</summary>
public sealed class KiSimulatorPluginTests
{
    private readonly AiSimulatorPlugin _sut;

    /// <summary>KiSimulatorPluginTests.</summary>
    public KiSimulatorPluginTests()
    {
        _sut = new AiSimulatorPlugin(
            new Mock<ILogger<AiSimulatorPlugin>>().Object);
    }

    /// <summary>PluginMetadata_ShouldExposeExpectedValues.</summary>
    [Fact]
    public void PluginMetadata_ShouldExposeExpectedValues()
    {
        _sut.PluginName.Should().Be("KI Simulator");
        _sut.PluginPrefix.Should().Be("Softwareschmiede.KiSimulator");
        _sut.ProviderFilePrefix.Should().Be("simulator");
        _sut.GetSettingGroups().Should().BeEmpty();
    }

    /// <summary>SupportsSessionContinuation_ShouldReturnFalse.</summary>
    [Fact]
    public void SupportsSessionContinuation_ShouldReturnFalse()
    {
        _sut.SupportsSessionContinuation().Should().BeFalse();
    }

    /// <summary>CheckHealthAsync_ShouldReturnTrue.</summary>
    [Fact]
    public async Task CheckHealthAsync_ShouldReturnTrue()
    {
        var result = await _sut.CheckHealthAsync(TestContext.Current.CancellationToken);
        result.Should().BeTrue();
    }

    /// <summary>StartCliAsync_ShouldReturnProcessStartInfoWithCmdExe.</summary>
    [Fact]
    public async Task StartCliAsync_ShouldReturnProcessStartInfoWithCmdExe()
    {
        var psi = await _sut.StartCliAsync(@"C:\repos\demo", ct: TestContext.Current.CancellationToken);

        psi.FileName.Should().Be("cmd.exe");
        psi.WorkingDirectory.Should().Be(@"C:\repos\demo");
        psi.UseShellExecute.Should().BeFalse();
        psi.CreateNoWindow.Should().BeFalse();
    }

    /// <summary>Der Simulator startet eine interaktive Shell (cmd.exe /k mit Banner), damit stdin-Befehle
    /// zeilenweise ausgeführt werden — auf PTY wie Pipe-Backend gleichermaßen.</summary>
    [Fact]
    public async Task GetTerminalStartSpecAsync_LiefertInteraktiveShellMitBanner()
    {
        var spec = await _sut.GetTerminalStartSpecAsync(@"C:\repos\demo", ct: TestContext.Current.CancellationToken);

        spec.FileName.Should().Be("cmd.exe");
        spec.Arguments.Should().Contain("/k");
        spec.Arguments.Should().Contain("KI-Simulator läuft...");
        spec.WorkingDirectory.Should().Be(@"C:\repos\demo");
        spec.Capabilities.Should().Be(Softwareschmiede.Domain.Enums.TerminalProviderCapabilities.SupportsPty);
        spec.PluginName.Should().Be("KI Simulator");
    }
}
