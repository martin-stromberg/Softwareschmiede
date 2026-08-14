using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.PluginImpl;
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
        File.WriteAllText(Path.Combine(verzeichnis, "Dritte.slnx"), string.Empty);
        File.WriteAllText(Path.Combine(verzeichnis, "readme.txt"), string.Empty);
        var service = CreateService();

        var solutions = service.FindeSolutions(verzeichnis);

        solutions.Should().HaveCount(3);
        solutions.Should().BeInAscendingOrder(StringComparer.OrdinalIgnoreCase);
        solutions.Should().Contain(Path.Combine(verzeichnis, "Erste.sln"));
        solutions.Should().Contain(Path.Combine(verzeichnis, "Dritte.slnx"));
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
        var service = new IdeOeffnenService(prozessStarterMock.Object);
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
        var service = new IdeOeffnenService(prozessStarterMock.Object);

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
        var service = new IdeOeffnenService(prozessStarterMock.Object);
        var solutionPfad = Path.Combine(CreateTempDirectory(), "Loesung.sln");

        var aufruf = () => service.OeffneSolution(solutionPfad);

        aufruf.Should().Throw<InvalidOperationException>().Which.Should().BeSameAs(erwarteteAusnahme);
    }

    /// <summary>OpenRepositoryInIdeAsync wirft, wenn kein PluginSelectionService bereitgestellt wurde.</summary>
    [Fact]
    public async Task OpenRepositoryInIdeAsync_OhnePluginSelectionService_Wirft()
    {
        var service = CreateService();
        var repositoryPfad = CreateTempDirectory();

        var aufruf = async () => await service.OpenRepositoryInIdeAsync(repositoryPfad);

        await aufruf.Should().ThrowAsync<InvalidOperationException>();
    }

    /// <summary>OpenRepositoryInIdeAsync wirft bei leerem Repository-Pfad, ohne den PluginSelectionService aufzurufen.</summary>
    /// <param name="repositoryPfad">Der zu prüfende leere/whitespace Repository-Pfad.</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task OpenRepositoryInIdeAsync_MitLeeremPfad_WirftArgumentException(string repositoryPfad)
    {
        var prozessStarterMock = new Mock<IProzessStarter>();
        var idePluginMock = new Mock<IIdePlugin>();
        var pluginSelectionService = CreatePluginSelectionService([idePluginMock.Object]);
        var service = new IdeOeffnenService(prozessStarterMock.Object, pluginSelectionService);

        var aufruf = async () => await service.OpenRepositoryInIdeAsync(repositoryPfad);

        await aufruf.Should().ThrowAsync<ArgumentException>();
        idePluginMock.Verify(p => p.OpenRepositoryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>OpenRepositoryInIdeAsync löst das passende IDE-Plugin über PluginSelectionService auf und öffnet damit.</summary>
    [Fact]
    public async Task OpenRepositoryInIdeAsync_LoestPluginAufUndOeffnet()
    {
        var repositoryPfad = CreateTempDirectory();
        var idePluginMock = new Mock<IIdePlugin>();
        idePluginMock.SetupGet(p => p.PluginName).Returns("Test-IDE");
        idePluginMock.SetupGet(p => p.PluginPrefix).Returns("Softwareschmiede.TestIde");
        idePluginMock.SetupGet(p => p.PluginType).Returns(PluginType.Ide);
        idePluginMock.Setup(p => p.GetSettingGroups()).Returns([]);
        idePluginMock.Setup(p => p.CheckCompatibilityAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(IdePluginCompatibility.Explicit);
        var pluginSelectionService = CreatePluginSelectionService([idePluginMock.Object]);
        var service = new IdeOeffnenService(new Mock<IProzessStarter>().Object, pluginSelectionService);

        await service.OpenRepositoryInIdeAsync(repositoryPfad);

        idePluginMock.Verify(p => p.OpenRepositoryAsync(repositoryPfad, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Bei mehreren gefundenen Solutions und aufgelöstem VisualStudioIdePlugin ruft OpenRepositoryInIdeAsync den übergebenen Auswahl-Callback auf und öffnet die dort gewählte Solution, statt das Plugin direkt zu öffnen.</summary>
    [Fact]
    public async Task OpenRepositoryInIdeAsync_MitMehrerenSolutionsUndVisualStudioPlugin_RuftCallbackAufUndOeffnetGewaehlteSolution()
    {
        var repositoryPfad = CreateTempDirectory();
        File.WriteAllText(Path.Combine(repositoryPfad, "Erste.sln"), string.Empty);
        File.WriteAllText(Path.Combine(repositoryPfad, "Zweite.sln"), string.Empty);
        var prozessStarterMock = new Mock<IProzessStarter>();
        var visualStudioPlugin = new VisualStudioIdePlugin(prozessStarterMock.Object);
        var pluginSelectionService = CreatePluginSelectionService([visualStudioPlugin]);
        var service = new IdeOeffnenService(prozessStarterMock.Object, pluginSelectionService);
        var gewaehlteSolution = Path.Combine(repositoryPfad, "Zweite.sln");
        IReadOnlyList<string>? demCallbackUebergebeneSolutions = null;

        await service.OpenRepositoryInIdeAsync(
            repositoryPfad,
            (solutionPfade, _) =>
            {
                demCallbackUebergebeneSolutions = solutionPfade;
                return Task.FromResult<string?>(gewaehlteSolution);
            });

        demCallbackUebergebeneSolutions.Should().HaveCount(2);
        prozessStarterMock.Verify(
            p => p.Starten(It.Is<ProzessStartAnfrage>(a => a.DateiName == gewaehlteSolution)),
            Times.Once);
    }

    /// <summary>Bricht der Anwender die Solution-Auswahl ab (Callback liefert null), öffnet OpenRepositoryInIdeAsync keine Solution und startet keinen Prozess.</summary>
    [Fact]
    public async Task OpenRepositoryInIdeAsync_MitMehrerenSolutionsUndAbgebrochenerAuswahl_OeffnetNichts()
    {
        var repositoryPfad = CreateTempDirectory();
        File.WriteAllText(Path.Combine(repositoryPfad, "Erste.sln"), string.Empty);
        File.WriteAllText(Path.Combine(repositoryPfad, "Zweite.sln"), string.Empty);
        var prozessStarterMock = new Mock<IProzessStarter>();
        var visualStudioPlugin = new VisualStudioIdePlugin(prozessStarterMock.Object);
        var pluginSelectionService = CreatePluginSelectionService([visualStudioPlugin]);
        var service = new IdeOeffnenService(prozessStarterMock.Object, pluginSelectionService);

        await service.OpenRepositoryInIdeAsync(repositoryPfad, (_, _) => Task.FromResult<string?>(null));

        prozessStarterMock.Verify(p => p.Starten(It.IsAny<ProzessStartAnfrage>()), Times.Never);
    }

    /// <summary>Existiert trotz gesetztem Auswahl-Callback nur eine Solution, ruft OpenRepositoryInIdeAsync den Callback nicht auf und öffnet die eine Solution direkt.</summary>
    [Fact]
    public async Task OpenRepositoryInIdeAsync_MitGenauEinerSolutionUndCallback_RuftCallbackNichtAufUndOeffnetDirekt()
    {
        var repositoryPfad = CreateTempDirectory();
        var einzigeSolution = Path.Combine(repositoryPfad, "Einzige.sln");
        File.WriteAllText(einzigeSolution, string.Empty);
        var prozessStarterMock = new Mock<IProzessStarter>();
        var visualStudioPlugin = new VisualStudioIdePlugin(prozessStarterMock.Object);
        var pluginSelectionService = CreatePluginSelectionService([visualStudioPlugin]);
        var service = new IdeOeffnenService(prozessStarterMock.Object, pluginSelectionService);
        var callbackAufgerufen = false;

        await service.OpenRepositoryInIdeAsync(
            repositoryPfad,
            (_, _) =>
            {
                callbackAufgerufen = true;
                return Task.FromResult<string?>(null);
            });

        callbackAufgerufen.Should().BeFalse();
        prozessStarterMock.Verify(
            p => p.Starten(It.Is<ProzessStartAnfrage>(a => a.DateiName == einzigeSolution)),
            Times.Once);
    }

    private string CreateTempDirectory()
        => _tempDirectoryFixture.CreateTempDirectory("ide_oeffnen_tests");

    private static IdeOeffnenService CreateService()
        => new(new Mock<IProzessStarter>().Object);

    private static PluginSelectionService CreatePluginSelectionService(IReadOnlyList<IIdePlugin> idePlugins)
    {
        var db = TestDbContextFactory.Create();
        var pluginManager = new Mock<IPluginManager>();
        pluginManager.Setup(m => m.GetIdePlugins()).Returns(idePlugins);
        pluginManager.Setup(m => m.GetDefaultIdePlugin()).Returns(idePlugins[0]);
        var appEinstellungService = new AppEinstellungService(db, NullLogger<AppEinstellungService>.Instance);
        var defaultSettings = new PluginDefaultSettingsService(db, NullLogger<PluginDefaultSettingsService>.Instance);
        var activationService = new PluginActivationService(appEinstellungService, pluginManager.Object, NullLogger<PluginActivationService>.Instance);
        return new PluginSelectionService(pluginManager.Object, defaultSettings, activationService, NullLogger<PluginSelectionService>.Instance, appEinstellungService);
    }
}
