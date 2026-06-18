using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Infrastructure.Plugins;

namespace Softwareschmiede.Tests.Infrastructure.Plugins;

public sealed class KiSimulatorPluginTests
{
    private readonly KiSimulatorPlugin _sut;

    public KiSimulatorPluginTests()
    {
        _sut = new KiSimulatorPlugin(
            new Mock<ICredentialStore>().Object,
            new Mock<ILogger<KiSimulatorPlugin>>().Object);
    }

    [Fact]
    public void PluginMetadata_ShouldExposeExpectedValues()
    {
        _sut.PluginName.Should().Be("KI Simulator");
        _sut.PluginPrefix.Should().Be("Softwareschmiede.KiSimulator");
        _sut.ProviderDateiPraefix.Should().Be("simulator");
        _sut.GetSettingGroups().Should().BeEmpty();
    }

    [Fact]
    public void SupportsSessionContinuation_ShouldReturnFalse()
    {
        _sut.SupportsSessionContinuation().Should().BeFalse();
    }

    [Fact]
    public async Task CheckHealthAsync_ShouldReturnTrue()
    {
        var result = await _sut.CheckHealthAsync();
        result.Should().BeTrue();
    }

    [Fact]
    public async Task StartCliAsync_ShouldReturnProcessStartInfoWithCmdExe()
    {
        var psi = await _sut.StartCliAsync(@"C:\repos\demo");

        psi.FileName.Should().Be("cmd.exe");
        psi.WorkingDirectory.Should().Be(@"C:\repos\demo");
    }
}
