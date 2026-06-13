using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Interfaces;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>Tests für den KiAusfuehrungsService.</summary>
public sealed class KiAusfuehrungsServiceTests : IDisposable
{
    private readonly KiAusfuehrungsService _sut;

    public KiAusfuehrungsServiceTests()
    {
        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        _sut = new KiAusfuehrungsService(NullLogger<KiAusfuehrungsService>.Instance, scopeFactoryMock.Object);
    }

    public void Dispose() => _sut.Dispose();

    /// <summary>IsRunning returns false for unknown task.</summary>
    [Fact]
    public void IsRunning_ShouldReturnFalse_WhenNoProcessStarted()
    {
        _sut.IsRunning(Guid.NewGuid()).Should().BeFalse();
    }

    /// <summary>GetRunningCount returns zero initially.</summary>
    [Fact]
    public void GetRunningCount_ShouldReturnZero_WhenNoProcessStarted()
    {
        _sut.GetRunningCount().Should().Be(0);
    }

    /// <summary>GetLastExitCode returns null for unknown task.</summary>
    [Fact]
    public void GetLastExitCode_ShouldReturnNull_WhenNoProcessStarted()
    {
        _sut.GetLastExitCode(Guid.NewGuid()).Should().BeNull();
    }

    /// <summary>StopCliAsync does nothing for unknown task.</summary>
    [Fact]
    public async Task StopCliAsync_ShouldNotThrow_WhenNoProcessStarted()
    {
        var act = () => _sut.StopCliAsync(Guid.NewGuid());
        await act.Should().NotThrowAsync();
    }

    /// <summary>UpdateHeartbeat does nothing for unknown task.</summary>
    [Fact]
    public void UpdateHeartbeat_ShouldNotThrow_WhenNoProcessStarted()
    {
        var act = () => _sut.UpdateHeartbeat(Guid.NewGuid());
        act.Should().NotThrow();
    }

    /// <summary>TestCliStartAsync: CLI wird gestartet, ProcessHandle wird zurückgegeben.</summary>
    [Fact]
    public async Task TestCliStartAsync()
    {
        // Arrange
        var aufgabeId = Guid.NewGuid();
        var pluginMock = new Mock<IKiPlugin>();
        pluginMock.Setup(p => p.StartCliAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c exit 0",
                UseShellExecute = false,
                CreateNoWindow = true,
            });

        // Act
        var handle = await _sut.StartCliAsync(aufgabeId, pluginMock.Object, Path.GetTempPath());

        // Assert
        handle.Should().NotBeNull();
        handle.AufgabeId.Should().Be(aufgabeId);
        _sut.IsRunning(aufgabeId).Should().BeTrue();
        _sut.GetRunningCount().Should().BeGreaterThan(0);
    }

    /// <summary>StartCliAsync returns handle when plugin provides valid ProcessStartInfo.</summary>
    [Fact]
    public async Task StartCliAsync_ShouldReturnHandle_WhenPluginProvidesValidProcessStartInfo()
    {
        var aufgabeId = Guid.NewGuid();
        var pluginMock = new Mock<IKiPlugin>();
        pluginMock.Setup(p => p.StartCliAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c exit 0",
                UseShellExecute = false,
                CreateNoWindow = true,
            });

        var handle = await _sut.StartCliAsync(aufgabeId, pluginMock.Object, Path.GetTempPath());

        handle.Should().NotBeNull();
        handle.AufgabeId.Should().Be(aufgabeId);
    }
}
