using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Softwareschmiede.Infrastructure.Services;

namespace Softwareschmiede.Tests.Infrastructure.Services;

/// <summary>Tests für den AgentPackageReader.</summary>
public sealed class AgentPackageReaderTests : IDisposable
{
    private readonly string _baseDir;
    private readonly AgentPackageReader _sut;

    public AgentPackageReaderTests()
    {
        _baseDir = Path.Combine(Path.GetTempPath(), "agent-packages-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_baseDir);

        var envMock = new Mock<IWebHostEnvironment>();
        envMock.Setup(e => e.ContentRootPath).Returns(_baseDir);

        _sut = new AgentPackageReader(
            new Mock<ILogger<AgentPackageReader>>().Object,
            envMock.Object);
    }

    public void Dispose()
    {
        if (Directory.Exists(_baseDir))
            Directory.Delete(_baseDir, recursive: true);
    }

    /// <summary>GetPackagesAsync gibt leere Liste zurück wenn keine Pakete vorhanden sind.</summary>
    [Fact]
    public async Task GetPackagesAsync_ShouldReturnEmpty_WhenNoPackagesExist()
    {
        // Act
        var result = await _sut.GetPackagesAsync();

        // Assert
        result.Should().BeEmpty();
    }

    /// <summary>GetPackagesAsync gibt alle Pakete zurück wenn Unterverzeichnisse vorhanden sind.</summary>
    [Fact]
    public async Task GetPackagesAsync_ShouldReturnPackages_WhenPackageDirectoriesExist()
    {
        // Arrange
        var packagesPath = Path.Combine(_baseDir, "agent-packages");
        Directory.CreateDirectory(Path.Combine(packagesPath, "paket-a"));
        Directory.CreateDirectory(Path.Combine(packagesPath, "paket-b"));

        // Act
        var result = (await _sut.GetPackagesAsync()).ToList();

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(p => p.Name == "paket-a");
        result.Should().Contain(p => p.Name == "paket-b");
    }

    /// <summary>GetPackageAsync gibt null zurück wenn Paket nicht existiert.</summary>
    [Fact]
    public async Task GetPackageAsync_ShouldReturnNull_WhenPackageDoesNotExist()
    {
        // Act
        var result = await _sut.GetPackageAsync("nicht-vorhanden");

        // Assert
        result.Should().BeNull();
    }

    /// <summary>GetPackageAsync gibt Paket zurück mit erkannten .agent.md Agenten.</summary>
    [Fact]
    public async Task GetPackageAsync_ShouldReturnPackageWithAgents_WhenAgentMdFilesExist()
    {
        // Arrange
        var packagesPath = Path.Combine(_baseDir, "agent-packages");
        var paketPath = Path.Combine(packagesPath, "mein-paket");
        Directory.CreateDirectory(paketPath);
        await File.WriteAllTextAsync(
            Path.Combine(paketPath, "developer.agent.md"),
            "description: Developer Agent\n");
        await File.WriteAllTextAsync(
            Path.Combine(paketPath, "reviewer.agent.md"),
            "description: Reviewer Agent\n");

        // Act
        var result = await _sut.GetPackageAsync("mein-paket");

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("mein-paket");
        result.Agenten.Should().HaveCount(2);
        result.Agenten.Should().Contain(a => a.Name == "developer");
        result.Agenten.Should().Contain(a => a.Name == "reviewer");
    }

    /// <summary>GetPackageAsync gibt alle Dateien im Paket zurück.</summary>
    [Fact]
    public async Task GetPackageAsync_ShouldReturnFilesList_WhenFilesExistInPackage()
    {
        // Arrange
        var packagesPath = Path.Combine(_baseDir, "agent-packages");
        var paketPath = Path.Combine(packagesPath, "datei-paket");
        Directory.CreateDirectory(paketPath);
        await File.WriteAllTextAsync(Path.Combine(paketPath, "agent.agent.md"), "content");
        await File.WriteAllTextAsync(Path.Combine(paketPath, "config.json"), "{}");

        // Act
        var result = await _sut.GetPackageAsync("datei-paket");

        // Assert
        result!.Dateien.Should().HaveCount(2);
    }
}
