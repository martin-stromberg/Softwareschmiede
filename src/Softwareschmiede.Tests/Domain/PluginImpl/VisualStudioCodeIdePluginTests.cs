using FluentAssertions;
using Moq;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.PluginImpl;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.Domain.PluginImpl;

/// <summary>Tests für <see cref="VisualStudioCodeIdePlugin"/>.</summary>
public sealed class VisualStudioCodeIdePluginTests
{
    /// <summary>Prüft, dass PluginName, PluginPrefix und PluginType korrekt gesetzt sind.</summary>
    [Fact]
    public void Eigenschaften_SindKorrektGesetzt()
    {
        var sut = CreateSut(VisualStudioCodeAvailability.NotAvailable);

        sut.PluginName.Should().Be("Visual Studio Code");
        sut.PluginPrefix.Should().Be("Softwareschmiede.VisualStudioCode");
        sut.PluginType.Should().Be(PluginType.Ide);
        sut.GetSettingGroups().Should().BeEmpty();
    }

    /// <summary>VS Code meldet für einen beliebigen Pfad immer Fallback.</summary>
    [Fact]
    public async Task CheckCompatibilityAsync_ShouldReturnFallback_Always()
    {
        var sut = CreateSut(VisualStudioCodeAvailability.NotAvailable);

        var result = await sut.CheckCompatibilityAsync(@"C:\beliebiges\repository");

        result.Should().Be(IdePluginCompatibility.Fallback);
    }

    /// <summary>Ein Null-Pfad wirft eine Exception.</summary>
    [Fact]
    public async Task CheckCompatibilityAsync_ShouldThrowArgumentNullException_WhenPathIsNull()
    {
        var sut = CreateSut(VisualStudioCodeAvailability.NotAvailable);

        var aufruf = async () => await sut.CheckCompatibilityAsync(null!);

        await aufruf.Should().ThrowAsync<ArgumentException>();
    }

    /// <summary>Ein leerer Pfad wirft eine Exception.</summary>
    [Fact]
    public async Task CheckCompatibilityAsync_ShouldThrowArgumentException_WhenPathIsEmpty()
    {
        var sut = CreateSut(VisualStudioCodeAvailability.NotAvailable);

        var aufruf = async () => await sut.CheckCompatibilityAsync(string.Empty);

        await aufruf.Should().ThrowAsync<ArgumentException>();
    }

    /// <summary>FindEntryPointsAsync liefert für VS Code immer genau einen Einstiegspunkt: das Repository-Root selbst.</summary>
    [Fact]
    public async Task FindEntryPointsAsync_LiefertImmerGenauEinen()
    {
        var repositoryPfad = @"C:\repos\meinprojekt";
        var sut = CreateSut(VisualStudioCodeAvailability.NotAvailable);

        var entryPoints = await sut.FindEntryPointsAsync(repositoryPfad);

        entryPoints.Should().ContainSingle();
        entryPoints[0].Path.Should().Be(repositoryPfad);
        entryPoints[0].DisplayName.Should().Be("Visual Studio Code");
    }

    /// <summary>OpenEntryPointAsync ruft IProzessStarter mit dem aufgelösten VS-Code-Befehl und dem Pfad des Einstiegspunkts auf.</summary>
    [Fact]
    public async Task OpenEntryPointAsync_RuftOpenDirectoryAuf()
    {
        var repositoryPfad = @"C:\repos\meinprojekt";
        var prozessStarterMock = new Mock<IProzessStarter>();
        var sut = new VisualStudioCodeIdePlugin(prozessStarterMock.Object, CreateLocator(new VisualStudioCodeAvailability(true, "code.cmd")));

        await sut.OpenEntryPointAsync(new IdeEntryPoint(repositoryPfad));

        prozessStarterMock.Verify(
            p => p.Starten(It.Is<ProzessStartAnfrage>(a =>
                a.DateiName == "code.cmd" &&
                a.Argumente == $"\"{repositoryPfad}\"" &&
                a.ShellAusfuehren == false)),
            Times.Once);
    }

    /// <summary>OpenEntryPointAsync wirft, wenn VS Code nicht auflösbar ist.</summary>
    [Fact]
    public async Task OpenEntryPointAsync_ShouldThrow_WhenVsCodeNotAvailable()
    {
        var sut = CreateSut(VisualStudioCodeAvailability.NotAvailable);

        var aufruf = async () => await sut.OpenEntryPointAsync(new IdeEntryPoint(@"C:\repos\meinprojekt"));

        await aufruf.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Visual Studio Code*");
    }

    private static VisualStudioCodeIdePlugin CreateSut(VisualStudioCodeAvailability availability)
        => new(new Mock<IProzessStarter>().Object, CreateLocator(availability));

    private static IVisualStudioCodeLocator CreateLocator(VisualStudioCodeAvailability availability)
        => new TestVisualStudioCodeLocator(availability);
}
