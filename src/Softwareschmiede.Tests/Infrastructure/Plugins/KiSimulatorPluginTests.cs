using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Softwareschmiede.Infrastructure.Plugins;

namespace Softwareschmiede.Tests.Infrastructure.Plugins;

/// <summary>KiSimulatorPluginTests.</summary>
public sealed class KiSimulatorPluginTests
{
    private readonly KiSimulatorPlugin _sut;

    /// <summary>KiSimulatorPluginTests.</summary>
    public KiSimulatorPluginTests()
    {
        _sut = new KiSimulatorPlugin(
            new Mock<ILogger<KiSimulatorPlugin>>().Object);
    }

    /// <summary><summary>PluginMetadata_ShouldExposeExpectedValues.</summary>.</summary>
    [Fact]
    public void PluginMetadata_ShouldExposeExpectedValues()
    {
        _sut.PluginName.Should().Be("KI Simulator");
        _sut.PluginPrefix.Should().Be("Softwareschmiede.KiSimulator");
        _sut.ProviderDateiPraefix.Should().Be("simulator");
        _sut.GetSettingGroups().Should().BeEmpty();
    }

    /// <summary><summary>SupportsSessionContinuation_ShouldReturnFalse.</summary>.</summary>
    [Fact]
    public void SupportsSessionContinuation_ShouldReturnFalse()
    {
        _sut.SupportsSessionContinuation().Should().BeFalse();
    }

    /// <summary><summary>CheckHealthAsync_ShouldReturnTrue.</summary>.</summary>
    [Fact]
    public async Task CheckHealthAsync_ShouldReturnTrue()
    {
        var result = await _sut.CheckHealthAsync();
        result.Should().BeTrue();
    }

    /// <summary><summary>StartCliAsync_ShouldReturnProcessStartInfoWithCmdExe.</summary>.</summary>
    [Fact]
    public async Task StartCliAsync_ShouldReturnProcessStartInfoWithCmdExe()
    {
        var psi = await _sut.StartCliAsync(@"C:\repos\demo");

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
        var spec = await _sut.GetTerminalStartSpecAsync(@"C:\repos\demo");

        spec.FileName.Should().Be("cmd.exe");
        spec.Arguments.Should().Contain("/k");
        spec.Arguments.Should().Contain("KI-Simulator läuft...");
        spec.WorkingDirectory.Should().Be(@"C:\repos\demo");
        spec.Capabilities.Should().Be(Softwareschmiede.Domain.Enums.TerminalProviderCapabilities.SupportsPty);
        spec.PluginName.Should().Be("KI Simulator");
    }
}
