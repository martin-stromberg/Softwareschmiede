using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.IntegrationTests.Infrastructure;
using Softwareschmiede.Infrastructure.Services;

namespace Softwareschmiede.IntegrationTests.Services;

/// <summary>
/// Integrationstests für <see cref="EntwicklungsprozessService"/> mit echter SQLite-Datenbank.
/// CLI-Plugins (IGitPlugin, IKiPlugin) werden gemockt.
/// </summary>
public sealed class EntwicklungsprozessServiceTests
{
    /// <summary>Hilfsmethode: Erstellt Testprojekt und Testaufgabe ohne AgentenpaketName.</summary>
    private static async Task<(Guid ProjektId, Guid AufgabeId)> CreateTestDataAsync(
        DatabaseFixture db,
        string aufgabeTitel = "Entwicklungsaufgabe")
    {
        var projektService = new ProjektService(db.Context, NullLogger<ProjektService>.Instance);
        var aufgabeService = new AufgabeService(db.Context, NullLogger<AufgabeService>.Instance);

        var projekt = await projektService.CreateAsync("Entwicklungstestprojekt", null);
        var aufgabe = await aufgabeService.CreateAsync(projekt.Id, aufgabeTitel, "Mach etwas Tolles");
        return (projekt.Id, aufgabe.Id);
    }

    /// <summary>Erstellt eine konfigurierte Instanz von <see cref="EntwicklungsprozessService"/>.</summary>
    private static EntwicklungsprozessService CreateService(
        DatabaseFixture db,
        Mock<IGitPlugin> gitMock,
        Mock<IKiPlugin> kiMock,
        IArbeitsverzeichnisResolver? arbeitsverzeichnisResolver = null)
    {
        var aufgabeService = new AufgabeService(db.Context, NullLogger<AufgabeService>.Instance);
        var protokollService = new ProtokollService(db.Context, NullLogger<ProtokollService>.Instance);
        arbeitsverzeichnisResolver ??= new ArbeitsverzeichnisResolver(
            new ArbeitsverzeichnisSettingsService(db.Context, NullLogger<ArbeitsverzeichnisSettingsService>.Instance),
            NullLogger<ArbeitsverzeichnisResolver>.Instance);

        return new EntwicklungsprozessService(
            aufgabeService,
            protokollService,
            gitMock.Object,
            CreatePluginSelectionService(db, gitMock.Object, kiMock.Object),
            arbeitsverzeichnisResolver,
            NullLogger<EntwicklungsprozessService>.Instance);
    }

    private static PluginSelectionService CreatePluginSelectionService(
        DatabaseFixture db,
        IGitPlugin gitPlugin,
        IKiPlugin kiPlugin)
    {
        var pluginManagerMock = new Mock<IPluginManager>();
        pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins()).Returns([gitPlugin]);
        pluginManagerMock.Setup(m => m.GetDefaultSourceCodeManagementPlugin()).Returns(gitPlugin);
        pluginManagerMock.Setup(m => m.GetDevelopmentAutomationPlugins()).Returns([kiPlugin]);
        pluginManagerMock.Setup(m => m.GetDefaultDevelopmentAutomationPlugin()).Returns(kiPlugin);

        var defaultService = new PluginDefaultSettingsService(db.Context, NullLogger<PluginDefaultSettingsService>.Instance);
        return new PluginSelectionService(pluginManagerMock.Object, defaultService, NullLogger<PluginSelectionService>.Instance);
    }

    /// <summary>
    /// Testet, dass ProzessStartenAsync BranchName und LokalerKlonPfad in der DB persistiert.
    /// </summary>
    [Fact]
    public async Task ProzessStartenAsync_ShouldPersistBranchAndKlonPfad_WhenAufgabeExists()
    {
        // Arrange
        await using var db = await DatabaseFixture.CreateAsync();
        var (_, aufgabeId) = await CreateTestDataAsync(db, "Login-Feature implementieren");

        var gitMock = new Mock<IGitPlugin>();
        gitMock.Setup(g => g.CloneRepositoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        gitMock.Setup(g => g.CreateBranchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var kiMock = new Mock<IKiPlugin>();

        var service = CreateService(db, gitMock, kiMock);

        // Act
        await service.ProzessStartenAsync(aufgabeId, "https://github.com/test/repo");

        // Assert – Branch und Klonpfad wurden in der DB gespeichert
        await using var db2 = db.CreateNewContext();
        var loaded = await db2.Aufgaben.FindAsync(aufgabeId);

        loaded!.Status.Should().Be(AufgabeStatus.ArbeitsverzeichnisEingerichtet);
        loaded.BranchName.Should().StartWith("task/");
        loaded.BranchName.Should().Contain(aufgabeId.ToString("N"));
        loaded.LokalerKlonPfad.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ProzessStartenAsync_ShouldPersistIssueBasedBranch_WhenAufgabeWasCreatedFromIssue()
    {
        await using var db = await DatabaseFixture.CreateAsync();
        var projektService = new ProjektService(db.Context, NullLogger<ProjektService>.Instance);
        var aufgabeService = new AufgabeService(db.Context, NullLogger<AufgabeService>.Instance);
        var projekt = await projektService.CreateAsync("Issue-Branch-Projekt", null);
        var issue = new Issue(55, "Issue-basierte Aufgabe", "Beschreibung", ["feature"], null, "https://github.com/test/repo/issues/55");
        var aufgabe = await aufgabeService.CreateFromIssueAsync(projekt.Id, issue);

        var gitMock = new Mock<IGitPlugin>();
        gitMock.Setup(g => g.CloneRepositoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        gitMock.Setup(g => g.CreateBranchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = CreateService(db, gitMock, new Mock<IKiPlugin>());

        await service.ProzessStartenAsync(aufgabe.Id, "https://github.com/test/repo");

        await using var db2 = db.CreateNewContext();
        var loaded = await db2.Aufgaben.FindAsync(aufgabe.Id);
        loaded!.BranchName.Should().StartWith($"task/issue-55-{aufgabe.Id:N}");
    }

    /// <summary>
    /// Testet, dass ProzessStartenAsync einen GitAktion-Protokolleintrag in der DB anlegt.
    /// </summary>
    [Fact]
    public async Task ProzessStartenAsync_ShouldCreateGitAktionProtokollEintrag_WhenProcessStarted()
    {
        // Arrange
        await using var db = await DatabaseFixture.CreateAsync();
        var (_, aufgabeId) = await CreateTestDataAsync(db, "Feature Aufgabe");

        var gitMock = new Mock<IGitPlugin>();
        gitMock.Setup(g => g.CloneRepositoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        gitMock.Setup(g => g.CreateBranchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var kiMock = new Mock<IKiPlugin>();

        var service = CreateService(db, gitMock, kiMock);

        // Act
        await service.ProzessStartenAsync(aufgabeId, "https://github.com/test/repo");

        // Assert – Protokolleintrag wurde angelegt
        await using var db2 = db.CreateNewContext();
        var protokolle = await db2.Protokolleintraege
            .Where(p => p.AufgabeId == aufgabeId && p.Typ == ProtokollTyp.GitAktion)
            .ToListAsync();

        protokolle.Should().Contain(p => p.Inhalt.Contains("Klon und Branch angelegt"));
    }

    /// <summary>
    /// Testet, dass AbschliessenAsync den Status auf Beendet setzt und einen Statusübergang protokolliert.
    /// </summary>
    [Fact]
    public async Task AbschliessenAsync_ShouldSetStatusBeendetAndCreateStatusUebergang_WhenAufgabeInBearbeitung()
    {
        // Arrange
        await using var db = await DatabaseFixture.CreateAsync();
        var (_, aufgabeId) = await CreateTestDataAsync(db);

        // Aufgabe zuerst starten
        var aufgabeService = new AufgabeService(db.Context, NullLogger<AufgabeService>.Instance);
        await aufgabeService.StartenAsync(aufgabeId, "task/test-branch", @"C:\nicht\vorhanden\pfad");

        var gitMock = new Mock<IGitPlugin>();
        var kiMock = new Mock<IKiPlugin>();

        var service = CreateService(db, gitMock, kiMock);

        // Act
        await service.AbschliessenAsync(aufgabeId);

        // Assert – Status und Protokolleintrag
        await using var db2 = db.CreateNewContext();
        var loaded = await db2.Aufgaben.FindAsync(aufgabeId);
        var statusEintrag = await db2.Protokolleintraege
            .FirstOrDefaultAsync(p => p.AufgabeId == aufgabeId && p.Typ == ProtokollTyp.StatusUebergang);

        loaded!.Status.Should().Be(AufgabeStatus.Beendet);
        statusEintrag.Should().NotBeNull();
        statusEintrag!.Inhalt.Should().Contain("Beendet");
    }

    [Fact]
    public async Task ProzessStartenAsync_ShouldUseConfiguredBasePath_WhenWorkdirSettingExists()
    {
        // Arrange
        await using var db = await DatabaseFixture.CreateAsync();
        var (_, aufgabeId) = await CreateTestDataAsync(db, "Konfiguriertes Arbeitsverzeichnis");
        var configuredBasePath = Path.Combine(Path.GetTempPath(), $"workdir-config-{Guid.NewGuid():N}");

        var workdirSettings = new ArbeitsverzeichnisSettingsService(db.Context, NullLogger<ArbeitsverzeichnisSettingsService>.Instance);
        await workdirSettings.SaveArbeitsverzeichnisAsync(configuredBasePath);

        var gitMock = new Mock<IGitPlugin>();
        gitMock.Setup(g => g.CloneRepositoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        gitMock.Setup(g => g.CreateBranchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = CreateService(db, gitMock, new Mock<IKiPlugin>());

        // Act
        await service.ProzessStartenAsync(aufgabeId, "https://github.com/test/repo");

        // Assert
        var expected = Path.Combine(configuredBasePath, "softwareschmiede", aufgabeId.ToString());
        gitMock.Verify(g => g.CloneRepositoryAsync("https://github.com/test/repo", expected, It.IsAny<CancellationToken>()), Times.Once);

        // Cleanup
        if (Directory.Exists(configuredBasePath))
        {
            Directory.Delete(configuredBasePath, recursive: true);
        }
    }

    [Fact]
    public async Task ProzessStartenAsync_ShouldUseTempFallback_WhenNoWorkdirSettingExists()
    {
        // Arrange
        await using var db = await DatabaseFixture.CreateAsync();
        var (_, aufgabeId) = await CreateTestDataAsync(db, "Fallback ohne Konfiguration");

        var gitMock = new Mock<IGitPlugin>();
        gitMock.Setup(g => g.CloneRepositoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        gitMock.Setup(g => g.CreateBranchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = CreateService(db, gitMock, new Mock<IKiPlugin>());

        // Act
        await service.ProzessStartenAsync(aufgabeId, "https://github.com/test/repo");

        // Assert
        var expected = Path.Combine(Path.GetTempPath(), "softwareschmiede", aufgabeId.ToString());
        gitMock.Verify(g => g.CloneRepositoryAsync("https://github.com/test/repo", expected, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProzessStartenAsync_ShouldFallbackToTemp_WhenConfiguredPathIsUnavailable()
    {
        // Arrange
        await using var db = await DatabaseFixture.CreateAsync();
        var (_, aufgabeId) = await CreateTestDataAsync(db, "Fallback bei ungültigem Laufzeitpfad");

        var invalidAsFile = Path.Combine(Path.GetTempPath(), $"workdir-file-{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(invalidAsFile, "not-a-directory");

        db.Context.AppEinstellungen.Add(new AppEinstellung
        {
            Id = Guid.NewGuid(),
            Schluessel = ArbeitsverzeichnisSettingsService.RepositoriesWorkdirKey,
            Wert = invalidAsFile,
            AktualisiertAm = DateTimeOffset.UtcNow
        });
        await db.Context.SaveChangesAsync();

        var gitMock = new Mock<IGitPlugin>();
        gitMock.Setup(g => g.CloneRepositoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        gitMock.Setup(g => g.CreateBranchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = CreateService(db, gitMock, new Mock<IKiPlugin>());

        // Act
        await service.ProzessStartenAsync(aufgabeId, "https://github.com/test/repo");

        // Assert
        var expectedFallback = Path.Combine(Path.GetTempPath(), "softwareschmiede", aufgabeId.ToString());
        gitMock.Verify(g => g.CloneRepositoryAsync("https://github.com/test/repo", expectedFallback, It.IsAny<CancellationToken>()), Times.Once);

        // Cleanup
        if (File.Exists(invalidAsFile))
        {
            File.Delete(invalidAsFile);
        }
    }

    [Fact]
    public async Task ProzessStartenAsync_ShouldLogFallbackEntry_WhenResolverUsesFallback()
    {
        // Arrange
        await using var db = await DatabaseFixture.CreateAsync();
        var (_, aufgabeId) = await CreateTestDataAsync(db, "Fallback Log");

        var gitMock = new Mock<IGitPlugin>();
        gitMock.Setup(g => g.CloneRepositoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        gitMock.Setup(g => g.CreateBranchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var resolverMock = new Mock<IArbeitsverzeichnisResolver>();
        resolverMock.Setup(r => r.ResolveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ArbeitsverzeichnisResolutionResult(Path.GetTempPath(), true, "no-configured-path", null));

        var service = CreateService(db, gitMock, new Mock<IKiPlugin>(), resolverMock.Object);

        // Act
        await service.ProzessStartenAsync(aufgabeId, "https://github.com/test/repo");

        // Assert
        await using var db2 = db.CreateNewContext();
        var eintraege = await db2.Protokolleintraege
            .Where(p => p.AufgabeId == aufgabeId && p.Typ == ProtokollTyp.GitAktion)
            .Select(p => p.Inhalt)
            .ToListAsync();

        eintraege.Should().Contain(e => e.Contains("Arbeitsverzeichnis-Fallback aktiv"));
    }
}
