using FluentAssertions;
using Moq;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.PluginImpl;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.Domain.PluginImpl;

/// <summary>Tests für <see cref="VisualStudioIdePlugin"/>.</summary>
public sealed class VisualStudioIdePluginTests : IDisposable
{
    private readonly TestTempDirectoryFixture _tempDirectoryFixture = new();

    /// <summary>Löscht alle temporären Testverzeichnisse.</summary>
    public void Dispose() => _tempDirectoryFixture.Dispose();

    /// <summary>Prüft, dass PluginName, PluginPrefix und PluginType korrekt gesetzt sind.</summary>
    [Fact]
    public void Property_AreCorrectSet()
    {
        var sut = CreateSut();

        sut.PluginName.Should().Be("Visual Studio");
        sut.PluginPrefix.Should().Be("Softwareschmiede.VisualStudio");
        sut.PluginType.Should().Be(PluginType.Ide);
        sut.GetSettingGroups().Should().BeEmpty();
    }

    /// <summary>Eine vorhandene .sln-Datei führt zu Explicit.</summary>
    [Fact]
    public async Task CheckCompatibilityAsync_ShouldReturnExplicit_WhenSlnExists()
    {
        var directory = CreateTempDirectory();
        File.WriteAllText(Path.Combine(directory, "Loesung.sln"), string.Empty);
        var sut = CreateSut();

        var result = await sut.CheckCompatibilityAsync(directory, TestContext.Current.CancellationToken);

        result.Should().Be(IdePluginCompatibility.Explicit);
    }

    /// <summary>Eine vorhandene .slnx-Datei führt ebenfalls zu Explicit.</summary>
    [Fact]
    public async Task CheckCompatibilityAsync_ShouldReturnExplicit_WhenSlnxExists()
    {
        var directory = CreateTempDirectory();
        File.WriteAllText(Path.Combine(directory, "Loesung.slnx"), string.Empty);
        var sut = CreateSut();

        var result = await sut.CheckCompatibilityAsync(directory, TestContext.Current.CancellationToken);

        result.Should().Be(IdePluginCompatibility.Explicit);
    }

    /// <summary>Ohne .sln/.slnx-Datei wird Incompatible gemeldet.</summary>
    [Fact]
    public async Task CheckCompatibilityAsync_ShouldReturnIncompatible_WhenNoSlnFound()
    {
        var directory = CreateTempDirectory();
        File.WriteAllText(Path.Combine(directory, "readme.txt"), string.Empty);
        var sut = CreateSut();

        var result = await sut.CheckCompatibilityAsync(directory, TestContext.Current.CancellationToken);

        result.Should().Be(IdePluginCompatibility.Incompatible);
    }

    /// <summary>Ein Null-Pfad wirft eine Exception.</summary>
    [Fact]
    public async Task CheckCompatibilityAsync_ShouldThrowArgumentNullException_WhenPathIsNull()
    {
        var sut = CreateSut();

        var call = async () => await sut.CheckCompatibilityAsync(null!);

        await call.Should().ThrowAsync<ArgumentException>();
    }

    /// <summary>Ein leerer Pfad wirft eine Exception.</summary>
    [Fact]
    public async Task CheckCompatibilityAsync_ShouldThrowArgumentException_WhenPathIsEmpty()
    {
        var sut = CreateSut();

        var call = async () => await sut.CheckCompatibilityAsync(string.Empty);

        await call.Should().ThrowAsync<ArgumentException>();
    }

    /// <summary>Ein nicht existenter Pfad meldet Incompatible statt einer Exception.</summary>
    [Fact]
    public async Task CheckCompatibilityAsync_ShouldReturnIncompatible_WhenPathDoesNotExist()
    {
        var sut = CreateSut();
        var notExistingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        var result = await sut.CheckCompatibilityAsync(notExistingDirectory, TestContext.Current.CancellationToken);

        result.Should().Be(IdePluginCompatibility.Incompatible);
    }

    /// <summary>Bei mehreren .sln-/.slnx-Dateien liefert FindEntryPointsAsync alle Einstiegspunkte alphabetisch sortiert.</summary>
    [Fact]
    public async Task FindEntryPointsAsync_WithSeveralSln_ReturnsAllAlphabeticalSorts()
    {
        var directory = CreateTempDirectory();
        File.WriteAllText(Path.Combine(directory, "Zweite.sln"), string.Empty);
        File.WriteAllText(Path.Combine(directory, "Erste.sln"), string.Empty);
        var sut = CreateSut();

        var entryPoints = await sut.FindEntryPointsAsync(directory, TestContext.Current.CancellationToken);

        entryPoints.Should().HaveCount(2);
        entryPoints.Select(ep => ep.Path).Should().BeInAscendingOrder(StringComparer.OrdinalIgnoreCase);
        entryPoints.Select(ep => ep.Path).Should().Contain(Path.Combine(directory, "Erste.sln"));
        entryPoints.Select(ep => ep.Path).Should().Contain(Path.Combine(directory, "Zweite.sln"));
    }

    /// <summary>Bei genau einer .sln-Datei liefert FindEntryPointsAsync eine Liste mit genau einem Einstiegspunkt.</summary>
    [Fact]
    public async Task FindEntryPointsAsync_WithExactlyASln_ReturnsA()
    {
        var directory = CreateTempDirectory();
        var solutionPath = Path.Combine(directory, "Einzige.sln");
        File.WriteAllText(solutionPath, string.Empty);
        var sut = CreateSut();

        var entryPoints = await sut.FindEntryPointsAsync(directory, TestContext.Current.CancellationToken);

        entryPoints.Should().ContainSingle();
        entryPoints[0].Path.Should().Be(solutionPath);
    }

    /// <summary>Ohne .sln/.slnx-Datei liefert FindEntryPointsAsync eine leere Liste.</summary>
    [Fact]
    public async Task FindEntryPointsAsync_WithoutSln_ReturnsEmptyList()
    {
        var directory = CreateTempDirectory();
        File.WriteAllText(Path.Combine(directory, "readme.txt"), string.Empty);
        var sut = CreateSut();

        var entryPoints = await sut.FindEntryPointsAsync(directory, TestContext.Current.CancellationToken);

        entryPoints.Should().BeEmpty();
    }

    /// <summary>OpenEntryPointAsync startet den übergebenen Einstiegspunkt per Shell-Execute über IProcessStarter.</summary>
    [Fact]
    public async Task OpenEntryPointAsync_CallsOpenSolutionFileOn()
    {
        var directory = CreateTempDirectory();
        var solutionPath = Path.Combine(directory, "Loesung.sln");
        File.WriteAllText(solutionPath, string.Empty);
        var processStarterMock = new Mock<IProcessStarter>();
        var sut = new VisualStudioIdePlugin(processStarterMock.Object);

        await sut.OpenEntryPointAsync(new IdeEntryPoint(solutionPath), TestContext.Current.CancellationToken);

        processStarterMock.Verify(
            p => p.Start(It.Is<ProcessStartRequest>(a =>
                a.FileName == solutionPath &&
                a.UseShellExecute == true)),
            Times.Once);
    }

    /// <summary>OpenEntryPointAsync wirft eine ArgumentNullException, wenn kein Einstiegspunkt übergeben wird.</summary>
    [Fact]
    public async Task OpenEntryPointAsync_WithNull_ThrowsArgumentNullException()
    {
        var sut = CreateSut();

        var call = async () => await sut.OpenEntryPointAsync(null!);

        await call.Should().ThrowAsync<ArgumentNullException>();
    }

    private string CreateTempDirectory() => _tempDirectoryFixture.CreateTempDirectory("visual_studio_ide_plugin_tests");

    private static VisualStudioIdePlugin CreateSut() => new(new Mock<IProcessStarter>().Object);
}
