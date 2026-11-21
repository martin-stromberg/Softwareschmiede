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
    public void Property_AreCorrectSet()
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

        var result = await sut.CheckCompatibilityAsync(@"C:\beliebiges\repository", TestContext.Current.CancellationToken);

        result.Should().Be(IdePluginCompatibility.Fallback);
    }

    /// <summary>Ein Null-Pfad wirft eine Exception.</summary>
    [Fact]
    public async Task CheckCompatibilityAsync_ShouldThrowArgumentNullException_WhenPathIsNull()
    {
        var sut = CreateSut(VisualStudioCodeAvailability.NotAvailable);

        var call = async () => await sut.CheckCompatibilityAsync(null!);

        await call.Should().ThrowAsync<ArgumentException>();
    }

    /// <summary>Ein leerer Pfad wirft eine Exception.</summary>
    [Fact]
    public async Task CheckCompatibilityAsync_ShouldThrowArgumentException_WhenPathIsEmpty()
    {
        var sut = CreateSut(VisualStudioCodeAvailability.NotAvailable);

        var call = async () => await sut.CheckCompatibilityAsync(string.Empty);

        await call.Should().ThrowAsync<ArgumentException>();
    }

    /// <summary>FindEntryPointsAsync liefert für VS Code immer genau einen Einstiegspunkt: das Repository-Root selbst.</summary>
    [Fact]
    public async Task FindEntryPointsAsync_ReturnsAlwaysExactlyA()
    {
        var repositoryPath = @"C:\repos\meinprojekt";
        var sut = CreateSut(VisualStudioCodeAvailability.NotAvailable);

        var entryPoints = await sut.FindEntryPointsAsync(repositoryPath, TestContext.Current.CancellationToken);

        entryPoints.Should().ContainSingle();
        entryPoints[0].Path.Should().Be(repositoryPath);
        entryPoints[0].DisplayName.Should().Be("Visual Studio Code");
    }

    /// <summary>OpenEntryPointAsync ruft IProcessStarter mit dem aufgelösten VS-Code-Befehl und dem Pfad des Einstiegspunkts auf.</summary>
    [Fact]
    public async Task OpenEntryPointAsync_CallsOpenDirectoryOn()
    {
        var repositoryPath = @"C:\repos\meinprojekt";
        var processStarterMock = new Mock<IProcessStarter>();
        var sut = new VisualStudioCodeIdePlugin(processStarterMock.Object, CreateLocator(new VisualStudioCodeAvailability(true, "code.cmd")));

        await sut.OpenEntryPointAsync(new IdeEntryPoint(repositoryPath), TestContext.Current.CancellationToken);

        processStarterMock.Verify(
            p => p.Start(It.Is<ProcessStartRequest>(a =>
                a.FileName == "code.cmd" &&
                a.Arguments == $"\"{repositoryPath}\"" &&
                a.UseShellExecute == false)),
            Times.Once);
    }

    /// <summary>OpenEntryPointAsync wirft, wenn VS Code nicht auflösbar ist.</summary>
    [Fact]
    public async Task OpenEntryPointAsync_ShouldThrow_WhenVsCodeNotAvailable()
    {
        var sut = CreateSut(VisualStudioCodeAvailability.NotAvailable);

        var call = async () => await sut.OpenEntryPointAsync(new IdeEntryPoint(@"C:\repos\meinprojekt"));

        await call.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Visual Studio Code*");
    }

    private static VisualStudioCodeIdePlugin CreateSut(VisualStudioCodeAvailability availability)
        => new(new Mock<IProcessStarter>().Object, CreateLocator(availability));

    private static IVisualStudioCodeLocator CreateLocator(VisualStudioCodeAvailability availability)
        => new TestVisualStudioCodeLocator(availability);
}
