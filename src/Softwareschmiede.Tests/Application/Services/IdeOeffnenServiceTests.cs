using FluentAssertions;
using Moq;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>Tests für den IdeOeffnenService.</summary>
public sealed class IdeOeffnenServiceTests : IDisposable
{
    private readonly TestTempDirectoryFixture _tempDirectoryFixture = new();

    /// <summary>Löscht alle temporären Testverzeichnisse.</summary>
    public void Dispose()
    {
        _tempDirectoryFixture.Dispose();
    }

    /// <summary>Prüft, dass FindeSolutions alle .sln-Dateien der obersten Ebene alphabetisch sortiert zurückgibt.</summary>
    [Fact]
    public void FindeSolutions_LiefertAlleSlnAlphabetischSortiert()
    {
        var verzeichnis = CreateTempDirectory();
        File.WriteAllText(Path.Combine(verzeichnis, "Zweite.sln"), string.Empty);
        File.WriteAllText(Path.Combine(verzeichnis, "Erste.sln"), string.Empty);
        File.WriteAllText(Path.Combine(verzeichnis, "readme.txt"), string.Empty);
        var service = CreateService();

        var solutions = service.FindeSolutions(verzeichnis);

        solutions.Should().HaveCount(2);
        solutions.Should().BeInAscendingOrder(StringComparer.OrdinalIgnoreCase);
        solutions.Should().Contain(Path.Combine(verzeichnis, "Erste.sln"));
        solutions.Should().Contain(Path.Combine(verzeichnis, "Zweite.sln"));
    }

    /// <summary>Prüft, dass FindeSolutions eine leere Liste liefert, wenn das Verzeichnis keine .sln-Datei enthält.</summary>
    [Fact]
    public void FindeSolutions_OhneSln_LiefertLeereListe()
    {
        var service = CreateService();
        var verzeichnisOhneSln = CreateTempDirectory();
        File.WriteAllText(Path.Combine(verzeichnisOhneSln, "readme.txt"), string.Empty);

        service.FindeSolutions(verzeichnisOhneSln).Should().BeEmpty();
    }

    /// <summary>Prüft, dass FindeSolutions eine leere Liste liefert, wenn das Verzeichnis nicht existiert.</summary>
    [Fact]
    public void FindeSolutions_NichtExistierendesVerzeichnis_LiefertLeereListe()
    {
        var service = CreateService();
        var nichtExistierendesVerzeichnis = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        service.FindeSolutions(nichtExistierendesVerzeichnis).Should().BeEmpty();
    }

    /// <summary>Prüft, dass FindeSolutions eine leere Liste liefert, wenn der Pfad null oder leer ist.</summary>
    /// <param name="pfad">Der zu prüfende null/leere Pfad.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void FindeSolutions_LeererPfad_LiefertLeereListe(string? pfad)
    {
        var service = CreateService();

        service.FindeSolutions(pfad).Should().BeEmpty();
    }

    /// <summary>Prüft, dass OeffneSolution die übergebene .sln-Datei per Shell-Execute an IProzessStarter übergibt.</summary>
    [Fact]
    public void OeffneSolution_StartetShellExecuteFuerSln()
    {
        var prozessStarterMock = new Mock<IProzessStarter>();
        var service = new IdeOeffnenService(prozessStarterMock.Object, CreateLocator(VisualStudioCodeAvailability.NotAvailable));
        var solutionPfad = Path.Combine(CreateTempDirectory(), "Loesung.sln");

        service.OeffneSolution(solutionPfad);

        prozessStarterMock.Verify(
            p => p.Starten(It.Is<ProzessStartAnfrage>(a =>
                a.DateiName == solutionPfad &&
                a.ShellAusfuehren == true)),
            Times.Once);
    }

    /// <summary>Prüft, dass OeffneSolution bei leerem/whitespace Solution-Pfad eine ArgumentException wirft, ohne IProzessStarter aufzurufen.</summary>
    /// <param name="solutionPfad">Der zu prüfende leere/whitespace Solution-Pfad.</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void OeffneSolution_MitLeeremPfad_WirftArgumentException(string solutionPfad)
    {
        var prozessStarterMock = new Mock<IProzessStarter>();
        var service = new IdeOeffnenService(prozessStarterMock.Object, CreateLocator(VisualStudioCodeAvailability.NotAvailable));

        var aufruf = () => service.OeffneSolution(solutionPfad);

        aufruf.Should().Throw<ArgumentException>();
        prozessStarterMock.Verify(p => p.Starten(It.IsAny<ProzessStartAnfrage>()), Times.Never);
    }

    /// <summary>Prüft, dass eine von IProzessStarter.Starten geworfene Ausnahme unverändert an den Aufrufer weitergereicht wird.</summary>
    [Fact]
    public void OeffneSolution_WennProzessStarterWirft_ReichtAusnahmeUnveraendertWeiter()
    {
        var prozessStarterMock = new Mock<IProzessStarter>();
        var erwarteteAusnahme = new InvalidOperationException("Prozess konnte nicht gestartet werden.");
        prozessStarterMock.Setup(p => p.Starten(It.IsAny<ProzessStartAnfrage>())).Throws(erwarteteAusnahme);
        var service = new IdeOeffnenService(prozessStarterMock.Object, CreateLocator(VisualStudioCodeAvailability.NotAvailable));
        var solutionPfad = Path.Combine(CreateTempDirectory(), "Loesung.sln");

        var aufruf = () => service.OeffneSolution(solutionPfad);

        aufruf.Should().Throw<InvalidOperationException>().Which.Should().BeSameAs(erwarteteAusnahme);
    }

    /// <summary>OeffneVisualStudioCode startet den aufgelösten VS-Code-Befehl ohne ShellExecute.</summary>
    [Fact]
    public void OeffneVisualStudioCode_StartetAufgeloestenBefehl()
    {
        var arbeitsverzeichnis = CreateTempDirectory();
        var prozessStarterMock = new Mock<IProzessStarter>();
        var executable = Path.Combine(CreateTempDirectory(), "code.cmd");
        var service = new IdeOeffnenService(
            prozessStarterMock.Object,
            CreateLocator(new VisualStudioCodeAvailability(true, executable)));

        service.OeffneVisualStudioCode(arbeitsverzeichnis);

        prozessStarterMock.Verify(
            p => p.Starten(It.Is<ProzessStartAnfrage>(a =>
                a.DateiName == executable
                && a.Argumente == $"\"{arbeitsverzeichnis}\""
                && a.ShellAusfuehren == false)),
            Times.Once);
    }

    /// <summary>OeffneVisualStudioCode validiert leere und fehlende Arbeitsverzeichnisse.</summary>
    [Fact]
    public void OeffneVisualStudioCode_MitFehlendemArbeitsverzeichnis_Wirft()
    {
        var prozessStarterMock = new Mock<IProzessStarter>();
        var service = new IdeOeffnenService(
            prozessStarterMock.Object,
            CreateLocator(new VisualStudioCodeAvailability(true, "code.cmd")));

        var aufruf = () => service.OeffneVisualStudioCode(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));

        aufruf.Should().Throw<DirectoryNotFoundException>();
        prozessStarterMock.Verify(p => p.Starten(It.IsAny<ProzessStartAnfrage>()), Times.Never);
    }

    /// <summary>OeffneVisualStudioCode wirft prüfbar, wenn VS Code nicht auflösbar ist.</summary>
    [Fact]
    public void OeffneVisualStudioCode_WennVsCodeNichtVerfuegbar_Wirft()
    {
        var arbeitsverzeichnis = CreateTempDirectory();
        var prozessStarterMock = new Mock<IProzessStarter>();
        var service = new IdeOeffnenService(prozessStarterMock.Object, CreateLocator(VisualStudioCodeAvailability.NotAvailable));

        var aufruf = () => service.OeffneVisualStudioCode(arbeitsverzeichnis);

        aufruf.Should().Throw<InvalidOperationException>().WithMessage("*Visual Studio Code*");
        prozessStarterMock.Verify(p => p.Starten(It.IsAny<ProzessStartAnfrage>()), Times.Never);
    }

    private string CreateTempDirectory()
        => _tempDirectoryFixture.CreateTempDirectory("ide_oeffnen_tests");

    private static IdeOeffnenService CreateService()
        => new(new Mock<IProzessStarter>().Object, CreateLocator(VisualStudioCodeAvailability.NotAvailable));

    private static IVisualStudioCodeLocator CreateLocator(VisualStudioCodeAvailability availability)
        => new TestVisualStudioCodeLocator(availability);

    private sealed class TestVisualStudioCodeLocator(VisualStudioCodeAvailability availability) : IVisualStudioCodeLocator
    {
        public VisualStudioCodeAvailability Locate() => availability;
    }
}
