using FluentAssertions;
using Moq;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>Tests für den ArbeitsverzeichnisOeffnenService.</summary>
public sealed class WorkingDirectoryOpenServiceTests
{
    /// <summary>Prüft, dass Oeffne unter Windows den Befehl "explorer.exe" mit dem gequoteten Verzeichnis als Argument an IProzessStarter übergibt. Läuft nur unter Windows, da ArbeitsverzeichnisOeffnenService auf den Windows-Fall reduziert ist (siehe Oeffne_AufNichtWindows_WirftPlatformNotSupportedException).</summary>
    [Fact]
    public void Open_StartsPlatformcommandWithDirectory()
    {
        Assert.SkipWhen(!OperatingSystem.IsWindows(), "Test gilt nur für Windows, siehe ArbeitsverzeichnisOeffnenService.ResolveOeffnenBefehl.");

        var processStarterMock = new Mock<IProcessStarter>();
        var service = new WorkingDirectoryOpenService(processStarterMock.Object);
        var workingDirectory = Path.GetTempPath();

        service.Open(workingDirectory);

        processStarterMock.Verify(
            p => p.Start(It.Is<ProcessStartRequest>(a =>
                a.FileName == "explorer.exe" &&
                a.Arguments == $"\"{workingDirectory}\"" &&
                a.UseShellExecute == false)),
            Times.Once);
    }

    /// <summary>Prüft, dass Oeffne auf Nicht-Windows-Betriebssystemen eine PlatformNotSupportedException wirft, ohne IProzessStarter aufzurufen. Läuft nur auf Nicht-Windows-Runnern; auf Windows wird der Test übersprungen, da ResolveOeffnenBefehl dort den Windows-Zweig nimmt.</summary>
    [Fact]
    public void Open_OnNotWindows_ThrowsPlatformNotSupportedException()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Test gilt nur für Nicht-Windows-Betriebssysteme, siehe ArbeitsverzeichnisOeffnenService.ResolveOeffnenBefehl.");

        var processStarterMock = new Mock<IProcessStarter>();
        var service = new WorkingDirectoryOpenService(processStarterMock.Object);

        var call = () => service.Open(Path.GetTempPath());

        call.Should().Throw<PlatformNotSupportedException>();
        processStarterMock.Verify(p => p.Start(It.IsAny<ProcessStartRequest>()), Times.Never);
    }

    /// <summary>Prüft, dass Oeffne bei leerem/whitespace Arbeitsverzeichnis eine ArgumentException wirft, ohne IProzessStarter aufzurufen.</summary>
    /// <param name="workingDirectory">Der zu prüfende leere/whitespace Verzeichnispfad.</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Open_WithEmptyDirectory_ThrowsArgumentException(string workingDirectory)
    {
        var processStarterMock = new Mock<IProcessStarter>();
        var service = new WorkingDirectoryOpenService(processStarterMock.Object);

        var call = () => service.Open(workingDirectory);

        call.Should().Throw<ArgumentException>();
        processStarterMock.Verify(p => p.Start(It.IsAny<ProcessStartRequest>()), Times.Never);
    }

    /// <summary>Prüft, dass eine von IProzessStarter.Starten geworfene Ausnahme unverändert an den Aufrufer weitergereicht wird.</summary>
    [Fact]
    public void Open_ThrowsWhenProcessStarter_SufficesExceptionUnchangedFurther()
    {
        var processStarterMock = new Mock<IProcessStarter>();
        var expectedException = new InvalidOperationException("Prozess konnte nicht gestartet werden.");
        processStarterMock.Setup(p => p.Start(It.IsAny<ProcessStartRequest>())).Throws(expectedException);
        var service = new WorkingDirectoryOpenService(processStarterMock.Object);

        var call = () => service.Open(Path.GetTempPath());

        call.Should().Throw<InvalidOperationException>().Which.Should().BeSameAs(expectedException);
    }
}
