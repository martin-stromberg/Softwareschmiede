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
    public void Eigenschaften_SindKorrektGesetzt()
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
        var verzeichnis = CreateTempDirectory();
        File.WriteAllText(Path.Combine(verzeichnis, "Loesung.sln"), string.Empty);
        var sut = CreateSut();

        var result = await sut.CheckCompatibilityAsync(verzeichnis);

        result.Should().Be(IdePluginCompatibility.Explicit);
    }

    /// <summary>Eine vorhandene .slnx-Datei führt ebenfalls zu Explicit.</summary>
    [Fact]
    public async Task CheckCompatibilityAsync_ShouldReturnExplicit_WhenSlnxExists()
    {
        var verzeichnis = CreateTempDirectory();
        File.WriteAllText(Path.Combine(verzeichnis, "Loesung.slnx"), string.Empty);
        var sut = CreateSut();

        var result = await sut.CheckCompatibilityAsync(verzeichnis);

        result.Should().Be(IdePluginCompatibility.Explicit);
    }

    /// <summary>Ohne .sln/.slnx-Datei wird Incompatible gemeldet.</summary>
    [Fact]
    public async Task CheckCompatibilityAsync_ShouldReturnIncompatible_WhenNoSlnFound()
    {
        var verzeichnis = CreateTempDirectory();
        File.WriteAllText(Path.Combine(verzeichnis, "readme.txt"), string.Empty);
        var sut = CreateSut();

        var result = await sut.CheckCompatibilityAsync(verzeichnis);

        result.Should().Be(IdePluginCompatibility.Incompatible);
    }

    /// <summary>Ein Null-Pfad wirft eine Exception.</summary>
    [Fact]
    public async Task CheckCompatibilityAsync_ShouldThrowArgumentNullException_WhenPathIsNull()
    {
        var sut = CreateSut();

        var aufruf = async () => await sut.CheckCompatibilityAsync(null!);

        await aufruf.Should().ThrowAsync<ArgumentException>();
    }

    /// <summary>Ein leerer Pfad wirft eine Exception.</summary>
    [Fact]
    public async Task CheckCompatibilityAsync_ShouldThrowArgumentException_WhenPathIsEmpty()
    {
        var sut = CreateSut();

        var aufruf = async () => await sut.CheckCompatibilityAsync(string.Empty);

        await aufruf.Should().ThrowAsync<ArgumentException>();
    }

    /// <summary>Ein nicht existenter Pfad meldet Incompatible statt einer Exception.</summary>
    [Fact]
    public async Task CheckCompatibilityAsync_ShouldReturnIncompatible_WhenPathDoesNotExist()
    {
        var sut = CreateSut();
        var nichtExistierendesVerzeichnis = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        var result = await sut.CheckCompatibilityAsync(nichtExistierendesVerzeichnis);

        result.Should().Be(IdePluginCompatibility.Incompatible);
    }

    /// <summary>Bei mehreren .sln-Dateien wird die alphabetisch erste geöffnet.</summary>
    [Fact]
    public async Task OpenRepositoryAsync_ShouldOpenFirstSolution_WhenMultipleExist()
    {
        var verzeichnis = CreateTempDirectory();
        File.WriteAllText(Path.Combine(verzeichnis, "Zweite.sln"), string.Empty);
        File.WriteAllText(Path.Combine(verzeichnis, "Erste.sln"), string.Empty);
        var prozessStarterMock = new Mock<IProzessStarter>();
        var sut = new VisualStudioIdePlugin(prozessStarterMock.Object);

        await sut.OpenRepositoryAsync(verzeichnis);

        prozessStarterMock.Verify(
            p => p.Starten(It.Is<ProzessStartAnfrage>(a =>
                a.DateiName == Path.Combine(verzeichnis, "Erste.sln") &&
                a.ShellAusfuehren == true)),
            Times.Once);
    }

    /// <summary>Bei mehreren .sln-/.slnx-Dateien liefert FindEntryPointsAsync alle Einstiegspunkte alphabetisch sortiert.</summary>
    [Fact]
    public async Task FindEntryPointsAsync_MitMehrerenSln_LiefertAlleAlphabetischSortiert()
    {
        var verzeichnis = CreateTempDirectory();
        File.WriteAllText(Path.Combine(verzeichnis, "Zweite.sln"), string.Empty);
        File.WriteAllText(Path.Combine(verzeichnis, "Erste.sln"), string.Empty);
        var sut = CreateSut();

        var entryPoints = await sut.FindEntryPointsAsync(verzeichnis);

        entryPoints.Should().HaveCount(2);
        entryPoints.Select(ep => ep.Path).Should().BeInAscendingOrder(StringComparer.OrdinalIgnoreCase);
        entryPoints.Select(ep => ep.Path).Should().Contain(Path.Combine(verzeichnis, "Erste.sln"));
        entryPoints.Select(ep => ep.Path).Should().Contain(Path.Combine(verzeichnis, "Zweite.sln"));
    }

    /// <summary>Bei genau einer .sln-Datei liefert FindEntryPointsAsync eine Liste mit genau einem Einstiegspunkt.</summary>
    [Fact]
    public async Task FindEntryPointsAsync_MitGenauEinerSln_LiefertEinen()
    {
        var verzeichnis = CreateTempDirectory();
        var solutionPfad = Path.Combine(verzeichnis, "Einzige.sln");
        File.WriteAllText(solutionPfad, string.Empty);
        var sut = CreateSut();

        var entryPoints = await sut.FindEntryPointsAsync(verzeichnis);

        entryPoints.Should().ContainSingle();
        entryPoints[0].Path.Should().Be(solutionPfad);
    }

    /// <summary>Ohne .sln/.slnx-Datei liefert FindEntryPointsAsync eine leere Liste.</summary>
    [Fact]
    public async Task FindEntryPointsAsync_OhneSln_LiefertLeereListe()
    {
        var verzeichnis = CreateTempDirectory();
        File.WriteAllText(Path.Combine(verzeichnis, "readme.txt"), string.Empty);
        var sut = CreateSut();

        var entryPoints = await sut.FindEntryPointsAsync(verzeichnis);

        entryPoints.Should().BeEmpty();
    }

    /// <summary>OpenEntryPointAsync startet den übergebenen Einstiegspunkt per Shell-Execute über IProzessStarter.</summary>
    [Fact]
    public async Task OpenEntryPointAsync_RuftOpenSolutionFileAuf()
    {
        var verzeichnis = CreateTempDirectory();
        var solutionPfad = Path.Combine(verzeichnis, "Loesung.sln");
        File.WriteAllText(solutionPfad, string.Empty);
        var prozessStarterMock = new Mock<IProzessStarter>();
        var sut = new VisualStudioIdePlugin(prozessStarterMock.Object);

        await sut.OpenEntryPointAsync(new IdeEntryPoint(solutionPfad));

        prozessStarterMock.Verify(
            p => p.Starten(It.Is<ProzessStartAnfrage>(a =>
                a.DateiName == solutionPfad &&
                a.ShellAusfuehren == true)),
            Times.Once);
    }

    /// <summary>OpenEntryPointAsync wirft eine ArgumentNullException, wenn kein Einstiegspunkt übergeben wird.</summary>
    [Fact]
    public async Task OpenEntryPointAsync_MitNull_WirftArgumentNullException()
    {
        var sut = CreateSut();

        var aufruf = async () => await sut.OpenEntryPointAsync(null!);

        await aufruf.Should().ThrowAsync<ArgumentNullException>();
    }

    private string CreateTempDirectory() => _tempDirectoryFixture.CreateTempDirectory("visual_studio_ide_plugin_tests");

    private static VisualStudioIdePlugin CreateSut() => new(new Mock<IProzessStarter>().Object);
}
