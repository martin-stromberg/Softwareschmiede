using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Softwareschmiede.Domain.Interfaces;
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
            new Mock<ICredentialStore>().Object,
            new Mock<ILogger<KiSimulatorPlugin>>().Object);
    }

    /// <summary><summary>PluginMetadata_ShouldExposeExpectedValues.</summary>.</summary>
    [Fact]
    /// <summary>PluginMetadata_ShouldExposeExpectedValues.</summary>
    public void PluginMetadata_ShouldExposeExpectedValues()
    {
        _sut.PluginName.Should().Be("KI Simulator");
        _sut.PluginPrefix.Should().Be("Softwareschmiede.KiSimulator");
        _sut.ProviderDateiPraefix.Should().Be("simulator");
        _sut.GetSettingGroups().Should().BeEmpty();
    }

    /// <summary><summary>SupportsSessionContinuation_ShouldReturnFalse.</summary>.</summary>
    [Fact]
    /// <summary>SupportsSessionContinuation_ShouldReturnFalse.</summary>
    public void SupportsSessionContinuation_ShouldReturnFalse()
    {
        _sut.SupportsSessionContinuation().Should().BeFalse();
    }

    /// <summary><summary>CheckHealthAsync_ShouldReturnTrue.</summary>.</summary>
    [Fact]
    /// <summary>CheckHealthAsync_ShouldReturnTrue.</summary>
    public async Task CheckHealthAsync_ShouldReturnTrue()
    {
        var result = await _sut.CheckHealthAsync();
        result.Should().BeTrue();
    }

    /// <summary><summary>StartCliAsync_ShouldReturnProcessStartInfoWithCmdExe.</summary>.</summary>
    [Fact]
    /// <summary>StartCliAsync_ShouldReturnProcessStartInfoWithCmdExe.</summary>
    public async Task StartCliAsync_ShouldReturnProcessStartInfoWithCmdExe()
    {
        var psi = await _sut.StartCliAsync(@"C:\repos\demo");

        psi.FileName.Should().Be("cmd.exe");
        psi.WorkingDirectory.Should().Be(@"C:\repos\demo");
        psi.UseShellExecute.Should().BeFalse();
        psi.CreateNoWindow.Should().BeFalse();
    }
}
