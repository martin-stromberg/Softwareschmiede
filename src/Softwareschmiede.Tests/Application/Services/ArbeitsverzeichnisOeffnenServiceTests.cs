using FluentAssertions;
using Moq;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>Tests für den ArbeitsverzeichnisOeffnenService.</summary>
public sealed class ArbeitsverzeichnisOeffnenServiceTests
{
    /// <summary>Prüft, dass Oeffne unter Windows den Befehl "explorer.exe" mit dem gequoteten Verzeichnis als Argument an IProzessStarter übergibt. Läuft nur unter Windows, da ArbeitsverzeichnisOeffnenService auf den Windows-Fall reduziert ist (siehe Oeffne_AufNichtWindows_WirftPlatformNotSupportedException).</summary>
    [SkippableFact]
    public void Oeffne_StartetPlattformbefehlMitVerzeichnis()
    {
        Skip.If(!OperatingSystem.IsWindows(), "Test gilt nur für Windows, siehe ArbeitsverzeichnisOeffnenService.ResolveOeffnenBefehl.");

        var prozessStarterMock = new Mock<IProzessStarter>();
        var service = new ArbeitsverzeichnisOeffnenService(prozessStarterMock.Object);
        var arbeitsverzeichnis = Path.GetTempPath();

        service.Oeffne(arbeitsverzeichnis);

        prozessStarterMock.Verify(
            p => p.Starten(It.Is<ProzessStartAnfrage>(a =>
                a.DateiName == "explorer.exe" &&
                a.Argumente == $"\"{arbeitsverzeichnis}\"" &&
                a.ShellAusfuehren == false)),
            Times.Once);
    }

    /// <summary>Prüft, dass Oeffne auf Nicht-Windows-Betriebssystemen eine PlatformNotSupportedException wirft, ohne IProzessStarter aufzurufen. Läuft nur auf Nicht-Windows-Runnern; auf Windows wird der Test übersprungen, da ResolveOeffnenBefehl dort den Windows-Zweig nimmt.</summary>
    [SkippableFact]
    public void Oeffne_AufNichtWindows_WirftPlatformNotSupportedException()
    {
        Skip.If(OperatingSystem.IsWindows(), "Test gilt nur für Nicht-Windows-Betriebssysteme, siehe ArbeitsverzeichnisOeffnenService.ResolveOeffnenBefehl.");

        var prozessStarterMock = new Mock<IProzessStarter>();
        var service = new ArbeitsverzeichnisOeffnenService(prozessStarterMock.Object);

        var aufruf = () => service.Oeffne(Path.GetTempPath());

        aufruf.Should().Throw<PlatformNotSupportedException>();
        prozessStarterMock.Verify(p => p.Starten(It.IsAny<ProzessStartAnfrage>()), Times.Never);
    }

    /// <summary>Prüft, dass Oeffne bei leerem/whitespace Arbeitsverzeichnis eine ArgumentException wirft, ohne IProzessStarter aufzurufen.</summary>
    /// <param name="arbeitsverzeichnis">Der zu prüfende leere/whitespace Verzeichnispfad.</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Oeffne_MitLeeremVerzeichnis_WirftArgumentException(string arbeitsverzeichnis)
    {
        var prozessStarterMock = new Mock<IProzessStarter>();
        var service = new ArbeitsverzeichnisOeffnenService(prozessStarterMock.Object);

        var aufruf = () => service.Oeffne(arbeitsverzeichnis);

        aufruf.Should().Throw<ArgumentException>();
        prozessStarterMock.Verify(p => p.Starten(It.IsAny<ProzessStartAnfrage>()), Times.Never);
    }

    /// <summary>Prüft, dass eine von IProzessStarter.Starten geworfene Ausnahme unverändert an den Aufrufer weitergereicht wird.</summary>
    [Fact]
    public void Oeffne_WennProzessStarterWirft_ReichtAusnahmeUnveraendertWeiter()
    {
        var prozessStarterMock = new Mock<IProzessStarter>();
        var erwarteteAusnahme = new InvalidOperationException("Prozess konnte nicht gestartet werden.");
        prozessStarterMock.Setup(p => p.Starten(It.IsAny<ProzessStartAnfrage>())).Throws(erwarteteAusnahme);
        var service = new ArbeitsverzeichnisOeffnenService(prozessStarterMock.Object);

        var aufruf = () => service.Oeffne(Path.GetTempPath());

        aufruf.Should().Throw<InvalidOperationException>().Which.Should().BeSameAs(erwarteteAusnahme);
    }
}
