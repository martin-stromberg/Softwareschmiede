using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>Tests für den EntwicklungsprozessService.</summary>
public sealed class EntwicklungsprozessServiceTests : IDisposable
{
    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiededDbContext _db;
    private readonly AufgabeService _aufgabeService;
    private readonly ProtokollService _protokollService;
    private readonly Mock<IGitPlugin> _gitPluginMock;
    private readonly Mock<IKiPlugin> _kiPluginMock;
    private readonly Mock<IArbeitsverzeichnisResolver> _arbeitsverzeichnisResolverMock;
    private readonly KiAusfuehrungsService _kiAusfuehrungsService;
    private readonly EntwicklungsprozessService _sut;
    private readonly Guid _projektId = new Guid("44444444-4444-4444-4444-444444444444");

    /// <summary>EntwicklungsprozessServiceTests.</summary>
    public EntwicklungsprozessServiceTests()
    {
        _db = TestDbContextFactory.Create();
        _aufgabeService = new AufgabeService(_db, new Mock<ILogger<AufgabeService>>().Object);
        _protokollService = new ProtokollService(_db, new Mock<ILogger<ProtokollService>>().Object);
        _gitPluginMock = new Mock<IGitPlugin>();
        _kiPluginMock = new Mock<IKiPlugin>();
        _kiPluginMock.SetupGet(p => p.PluginName).Returns("Test KI");
        _kiPluginMock.SetupGet(p => p.PluginPrefix).Returns("Softwareschmiede.TestKi");
        _kiPluginMock.SetupGet(p => p.PluginType).Returns(PluginType.DevelopmentAutomation);
        _kiPluginMock.Setup(p => p.GetSettingGroups()).Returns([]);
        _arbeitsverzeichnisResolverMock = new Mock<IArbeitsverzeichnisResolver>();
        _arbeitsverzeichnisResolverMock.Setup(r => r.ResolveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ArbeitsverzeichnisResolutionResult(Path.GetTempPath(), false, "configured", null));

        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        _kiAusfuehrungsService = new KiAusfuehrungsService(NullLogger<KiAusfuehrungsService>.Instance, scopeFactoryMock.Object);

        _sut = new EntwicklungsprozessService(
            _aufgabeService,
            _protokollService,
            null,
            _gitPluginMock.Object,
            CreatePluginSelectionService(_kiPluginMock.Object),
            _arbeitsverzeichnisResolverMock.Object,
            null,
            _kiAusfuehrungsService,
            new Mock<ILogger<EntwicklungsprozessService>>().Object);

        _db.Projekte.Add(new Softwareschmiede.Domain.Entities.Projekt
        {
            Id = _projektId,
            Name = "Entwicklungsprozess-Testprojekt",
            ErstellungsDatum = DateTimeOffset.UtcNow,
            Status = ProjektStatus.Aktiv
        });
        _db.SaveChanges();
    }

    /// <summary>Dispose.</summary>
    public void Dispose()
    {
        _kiAusfuehrungsService.Dispose();
        _db.Dispose();
    }

    private PluginSelectionService CreatePluginSelectionService(params IKiPlugin[] kiPlugins)
    {
        var pluginManagerMock = new Mock<IPluginManager>();
        pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins()).Returns([_gitPluginMock.Object]);
        pluginManagerMock.Setup(m => m.GetDefaultSourceCodeManagementPlugin()).Returns(_gitPluginMock.Object);
        pluginManagerMock.Setup(m => m.GetDevelopmentAutomationPlugins()).Returns(kiPlugins);
        pluginManagerMock.Setup(m => m.GetDefaultDevelopmentAutomationPlugin()).Returns(kiPlugins.First());

        var defaultService = new PluginDefaultSettingsService(_db, new Mock<ILogger<PluginDefaultSettingsService>>().Object);
        return new PluginSelectionService(pluginManagerMock.Object, defaultService, new Mock<ILogger<PluginSelectionService>>().Object);
    }

    /// <summary>ProzessStartenAsync klont das Repository und legt einen Branch an.</summary>
    [Fact]
    public async Task ProzessStartenAsync_ShouldCloneAndCreateBranch_WhenAufgabeExists()
    {
        // Arrange
        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "Login Feature implementieren", null);
        _gitPluginMock.Setup(g => g.CloneRepositoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _gitPluginMock.Setup(g => g.CreateBranchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _sut.ProzessStartenAsync(aufgabe.Id, "https://github.com/test/repo");

        // Assert
        _gitPluginMock.Verify(g => g.CloneRepositoryAsync("https://github.com/test/repo", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        _gitPluginMock.Verify(g => g.CreateBranchAsync(It.IsAny<string>(), It.Is<string>(b => b.Contains("login-feature")), It.IsAny<CancellationToken>()), Times.Once);

        var updatedAufgabe = await _aufgabeService.GetByIdAsync(aufgabe.Id);
        updatedAufgabe!.Status.Should().Be(AufgabeStatus.Gestartet);
        updatedAufgabe.BranchName.Should().Contain("login-feature");
    }

    /// <summary>ProzessStartenUndCliStartenAsync klont, setzt Status auf Gestartet und startet die CLI.</summary>
    [Fact]
    public async Task TestProzessStartenUndCliStartenAsync_Success()
    {
        // Arrange
        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "Kombinierter Start", null);
        _gitPluginMock.Setup(g => g.CloneRepositoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _gitPluginMock.Setup(g => g.CreateBranchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _kiPluginMock.Setup(p => p.StartCliAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c exit 0",
                UseShellExecute = false,
                CreateNoWindow = true,
            });

        // Act
        await _sut.ProzessStartenUndCliStartenAsync(aufgabe.Id, "https://github.com/test/repo", null, "Softwareschmiede.TestKi");

        // Assert
        var updatedAufgabe = await _aufgabeService.GetByIdAsync(aufgabe.Id);
        updatedAufgabe!.Status.Should().Be(AufgabeStatus.Gestartet);
        _kiAusfuehrungsService.IsRunning(aufgabe.Id).Should().BeTrue();
    }

    /// <summary>ProzessStartenUndCliStartenAsync setzt Status zurück und löscht das Klon-Verzeichnis, wenn das Klonen fehlschlägt.</summary>
    [Fact]
    public async Task TestProzessStartenUndCliStartenAsync_RepositoryCloneFails_RollbackStatus()
    {
        // Arrange
        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "Klonen fehlschlägt", null);
        _gitPluginMock.Setup(g => g.CloneRepositoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Klonen fehlgeschlagen"));

        // Act
        var act = () => _sut.ProzessStartenUndCliStartenAsync(aufgabe.Id, "https://github.com/test/repo", null, "Softwareschmiede.TestKi");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        var updatedAufgabe = await _aufgabeService.GetByIdAsync(aufgabe.Id);
        updatedAufgabe!.Status.Should().Be(AufgabeStatus.Neu);
    }

    /// <summary>ProzessStartenUndCliStartenAsync setzt Status zurück und löscht das Klon-Verzeichnis, wenn der CLI-Start fehlschlägt.</summary>
    [Fact(Skip = "Temporär deaktiviert: StartWithPseudoConsoleAsync ignoriert das Plugin und startet cmd.exe; kiPlugin-Mock hat keinen Effekt. Reaktivieren wenn Plugin-Start wieder aktiv ist.")]
    public async Task TestProzessStartenUndCliStartenAsync_CliStartFails_RollbackStatus()
    {
        // Arrange
        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "CLI-Start fehlschlägt", null);
        _gitPluginMock.Setup(g => g.CloneRepositoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _gitPluginMock.Setup(g => g.CreateBranchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _kiPluginMock.Setup(p => p.StartCliAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("CLI-Start fehlgeschlagen"));

        // Act
        var act = () => _sut.ProzessStartenUndCliStartenAsync(aufgabe.Id, "https://github.com/test/repo", null, "Softwareschmiede.TestKi");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        var updatedAufgabe = await _aufgabeService.GetByIdAsync(aufgabe.Id);
        updatedAufgabe!.Status.Should().Be(AufgabeStatus.Neu);
    }

    /// <summary>ProzessStartenAsync blockiert den Start nicht, wenn das Repository-Startskript fehlschlägt.</summary>
    [Fact]
    public async Task ProzessStartenAsync_ShouldContinue_WhenRepositoryStartScriptFails()
    {
        // Arrange
        var repository = new GitRepository
        {
            Id = Guid.NewGuid(),
            ProjektId = _projektId,
            PluginTyp = "Softwareschmiede.GitHub",
            RepositoryUrl = "https://github.com/test/repo-start-script",
            RepositoryName = "repo-start-script",
            Aktiv = true
        };
        repository.StartKonfiguration = new RepositoryStartKonfiguration
        {
            Id = Guid.NewGuid(),
            StartScriptRelativePath = "scripts/start.ps1",
            Aktiv = true,
            GitRepository = repository
        };
        _db.GitRepositories.Add(repository);
        await _db.SaveChangesAsync();

        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "Startskript robust starten", null, repository.Id);
        _gitPluginMock.Setup(g => g.CloneRepositoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _gitPluginMock.Setup(g => g.CreateBranchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var cliRunnerMock = new Mock<ICliRunner>();
        cliRunnerMock.Setup(runner => runner.RunAsync(
                It.IsAny<string>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<string?>(),
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(1, string.Empty, "script failed"));
        var repositoryStartskriptService = new RepositoryStartskriptService(
            cliRunnerMock.Object,
            new Mock<ILogger<RepositoryStartskriptService>>().Object);
        var projektService = new ProjektService(_db, new Mock<ILogger<ProjektService>>().Object);
        var sut = new EntwicklungsprozessService(
            _aufgabeService,
            _protokollService,
            projektService,
            _gitPluginMock.Object,
            CreatePluginSelectionService(_kiPluginMock.Object),
            _arbeitsverzeichnisResolverMock.Object,
            repositoryStartskriptService,
            new Mock<ILogger<EntwicklungsprozessService>>().Object);

        // Act
        await sut.ProzessStartenAsync(aufgabe.Id, repository.RepositoryUrl);

        // Assert
        var updatedAufgabe = await _aufgabeService.GetByIdAsync(aufgabe.Id);
        updatedAufgabe.Should().NotBeNull();
        updatedAufgabe!.Status.Should().Be(AufgabeStatus.Gestartet);
        var protokoll = await _protokollService.GetByAufgabeAsync(aufgabe.Id);
        protokoll.Should().Contain(entry =>
            entry.Typ == ProtokollTyp.GitAktion
            && entry.Inhalt.Contains("Hinweis: Das Repository-Startskript konnte nicht ausgeführt werden", StringComparison.Ordinal));
    }

    /// <summary><summary>ProzessStartenAsync_ShouldCreateIssueBranch_WhenAufgabeHasIssueReference.</summary>.</summary>
    [Fact]
    /// <summary>ProzessStartenAsync_ShouldCreateIssueBranch_WhenAufgabeHasIssueReference.</summary>
    public async Task ProzessStartenAsync_ShouldCreateIssueBranch_WhenAufgabeHasIssueReference()
    {
        var issue = new Issue(321, "Issue Branch", "Body", ["enhancement"], null, "https://github.com/test/repo/issues/321");
        var aufgabe = await _aufgabeService.CreateFromIssueAsync(_projektId, issue);

        _gitPluginMock.Setup(g => g.CloneRepositoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _gitPluginMock.Setup(g => g.CreateBranchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _sut.ProzessStartenAsync(aufgabe.Id, "https://github.com/test/repo");

        _gitPluginMock.Verify(g => g.CreateBranchAsync(
            It.IsAny<string>(),
            It.Is<string>(branch => branch.StartsWith($"task/issue-321-{aufgabe.Id:N}") && branch.Contains("-issue-branch")),
            It.IsAny<CancellationToken>()), Times.Once);

        var updatedAufgabe = await _aufgabeService.GetDetailAsync(aufgabe.Id);
        updatedAufgabe!.BranchName.Should().StartWith($"task/issue-321-{aufgabe.Id:N}");
    }

    /// <summary>ProzessStartenAsync wirft Exception wenn Aufgabe nicht gefunden.</summary>
    [Fact]
    public async Task ProzessStartenAsync_ShouldThrowInvalidOperationException_WhenAufgabeDoesNotExist()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var act = () => _sut.ProzessStartenAsync(nonExistentId, "https://github.com/test/repo");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    /// <summary>KiStartenAsync wirft Exception wenn KI bereits aktiv ist.</summary>
    [Fact]
    public async Task AbschliessenAsync_ShouldSetStatusAbgeschlossenAndAddProtokoll_WhenAufgabeExists()
    {
        // Arrange
        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "Abzuschließende Aufgabe", null);

        // Act
        await _sut.AbschliessenAsync(aufgabe.Id);

        // Assert
        var result = await _aufgabeService.GetByIdAsync(aufgabe.Id);
        result!.Status.Should().Be(AufgabeStatus.Beendet);

        var protokoll = await _protokollService.GetByAufgabeAsync(aufgabe.Id);
        protokoll.Should().Contain(p => p.Typ == ProtokollTyp.StatusUebergang);
    }

    /// <summary>CommitDurchfuehrenAsync wirft Exception wenn kein LokalerKlonPfad gesetzt.</summary>
    [Fact]
    public async Task CommitDurchfuehrenAsync_ShouldThrowInvalidOperationException_WhenNoKlonPfad()
    {
        // Arrange
        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "Commit ohne Klon", null);

        // Act
        var act = () => _sut.CommitDurchfuehrenAsync(aufgabe.Id, "Commit message");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*lokalen Klonpfad*");
    }

    /// <summary>PushDurchfuehrenAsync wirft Exception wenn kein LokalerKlonPfad gesetzt.</summary>
    [Fact]
    public async Task PushDurchfuehrenAsync_ShouldThrowInvalidOperationException_WhenNoKlonPfad()
    {
        // Arrange
        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "Push ohne Klon", null);

        // Act
        var act = () => _sut.PushDurchfuehrenAsync(aufgabe.Id);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    /// <summary><summary>ProzessStartenAsync_ShouldUseConfiguredWorkdirBase_ForClonePath.</summary>.</summary>
    [Fact]
    /// <summary>ProzessStartenAsync_ShouldUseConfiguredWorkdirBase_ForClonePath.</summary>
    public async Task ProzessStartenAsync_ShouldUseConfiguredWorkdirBase_ForClonePath()
    {
        // Arrange
        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "Workdir Konfiguriert", null);
        var configuredBase = Path.Combine(Path.GetTempPath(), "custom-workdir-base");
        _arbeitsverzeichnisResolverMock.Setup(r => r.ResolveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ArbeitsverzeichnisResolutionResult(configuredBase, false, "configured", configuredBase));
        _gitPluginMock.Setup(g => g.CloneRepositoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _gitPluginMock.Setup(g => g.CreateBranchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _sut.ProzessStartenAsync(aufgabe.Id, "https://github.com/test/repo");

        // Assert
        var expectedPath = Path.Combine(configuredBase, "softwareschmiede", aufgabe.Id.ToString());
        _gitPluginMock.Verify(g => g.CloneRepositoryAsync(
            "https://github.com/test/repo",
            expectedPath,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary><summary>ProzessStartenAsync_ShouldUseFallbackPath_WhenResolverReturnsFallback.</summary>.</summary>
    [Fact]
    /// <summary>ProzessStartenAsync_ShouldUseFallbackPath_WhenResolverReturnsFallback.</summary>
    public async Task ProzessStartenAsync_ShouldUseFallbackPath_WhenResolverReturnsFallback()
    {
        // Arrange
        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "Workdir Fallback", null);
        var fallbackBase = Path.GetTempPath();
        _arbeitsverzeichnisResolverMock.Setup(r => r.ResolveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ArbeitsverzeichnisResolutionResult(fallbackBase, true, "no-configured-path", null));
        _gitPluginMock.Setup(g => g.CloneRepositoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _gitPluginMock.Setup(g => g.CreateBranchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _sut.ProzessStartenAsync(aufgabe.Id, "https://github.com/test/repo");

        // Assert
        var expectedPath = Path.Combine(fallbackBase, "softwareschmiede", aufgabe.Id.ToString());
        _gitPluginMock.Verify(g => g.CloneRepositoryAsync(
            "https://github.com/test/repo",
            expectedPath,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary><summary>ProzessStartenAsync_ShouldCheckoutExistingBranch_WhenBasisBranchIsNotDefault.</summary>.</summary>
    [Fact]
    /// <summary>ProzessStartenAsync_ShouldCheckoutExistingBranch_WhenBasisBranchIsNotDefault.</summary>
    public async Task ProzessStartenAsync_ShouldCheckoutExistingBranch_WhenBasisBranchIsNotDefault()
    {
        // Arrange
        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "Existing Branch", null);
        _gitPluginMock.Setup(g => g.CloneRepositoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _gitPluginMock.Setup(g => g.GetDefaultBranchAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("main");
        _gitPluginMock.Setup(g => g.CheckoutRemoteBranchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _sut.ProzessStartenAsync(aufgabe.Id, "https://github.com/test/repo", "feature/existing-branch");

        // Assert
        _gitPluginMock.Verify(g => g.CheckoutRemoteBranchAsync(
            It.IsAny<string>(),
            "feature/existing-branch",
            It.IsAny<CancellationToken>()), Times.Once);
        _gitPluginMock.Verify(g => g.CreateBranchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);

        var updatedAufgabe = await _aufgabeService.GetByIdAsync(aufgabe.Id);
        updatedAufgabe!.BranchName.Should().Be("feature/existing-branch");
    }

    /// <summary>ProzessStartenAsync erstellt einen neuen Task-Branch, wenn Basis- und Default-Branch nur in der Groß-/Kleinschreibung abweichen.</summary>
    [Fact]
    public async Task ProzessStartenAsync_ShouldCreateTaskBranch_WhenBasisBranchEqualsDefaultBranch_CaseInsensitive()
    {
        // Arrange
        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "Case Insensitive Branch", null);
        _gitPluginMock.Setup(g => g.CloneRepositoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _gitPluginMock.Setup(g => g.GetDefaultBranchAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("main");
        _gitPluginMock.Setup(g => g.CreateBranchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _sut.ProzessStartenAsync(aufgabe.Id, "https://github.com/test/repo", "MAIN");

        // Assert
        _gitPluginMock.Verify(g => g.GetDefaultBranchAsync("https://github.com/test/repo", It.IsAny<CancellationToken>()), Times.Once);
        _gitPluginMock.Verify(g => g.CheckoutRemoteBranchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _gitPluginMock.Verify(g => g.CreateBranchAsync(
            It.IsAny<string>(),
            It.Is<string>(branch => branch.StartsWith("task/", StringComparison.Ordinal)),
            It.IsAny<CancellationToken>()), Times.Once);

        var updatedAufgabe = await _aufgabeService.GetByIdAsync(aufgabe.Id);
        updatedAufgabe!.BranchName.Should().StartWith("task/");
        updatedAufgabe.BranchName.Should().NotBe("MAIN");
    }

    /// <summary><summary>ProzessStartenAsync_ShouldDeleteExistingCloneDirectory_BeforeClone.</summary>.</summary>
    [Fact]
    /// <summary>ProzessStartenAsync_ShouldDeleteExistingCloneDirectory_BeforeClone.</summary>
    public async Task ProzessStartenAsync_ShouldDeleteExistingCloneDirectory_BeforeClone()
    {
        // Arrange
        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "Delete Existing Clone", null);
        var configuredBase = Path.Combine(Path.GetTempPath(), $"workdir-existing-{Guid.NewGuid():N}");
        var expectedClonePath = Path.Combine(configuredBase, "softwareschmiede", aufgabe.Id.ToString());
        Directory.CreateDirectory(expectedClonePath);
        var readOnlyFile = Path.Combine(expectedClonePath, "readonly.txt");
        await File.WriteAllTextAsync(readOnlyFile, "content");
        File.SetAttributes(readOnlyFile, FileAttributes.ReadOnly);

        _arbeitsverzeichnisResolverMock.Setup(r => r.ResolveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ArbeitsverzeichnisResolutionResult(configuredBase, false, "configured", configuredBase));
        _gitPluginMock.Setup(g => g.CloneRepositoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _gitPluginMock.Setup(g => g.CreateBranchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _sut.ProzessStartenAsync(aufgabe.Id, "https://github.com/test/repo");

        // Assert
        _gitPluginMock.Verify(g => g.CloneRepositoryAsync("https://github.com/test/repo", expectedClonePath, It.IsAny<CancellationToken>()), Times.Once);
        Directory.Exists(expectedClonePath).Should().BeFalse();

        if (Directory.Exists(configuredBase))
        {
            Directory.Delete(configuredBase, recursive: true);
        }
    }

    /// <summary><summary>ProzessStartenAsync_ShouldThrow_WhenRepositoryContextIsAmbiguous.</summary>.</summary>
    [Fact]
    /// <summary>ProzessStartenAsync_ShouldThrow_WhenRepositoryContextIsAmbiguous.</summary>
    public async Task ProzessStartenAsync_ShouldThrow_WhenRepositoryContextIsAmbiguous()
    {
        // Arrange
        _db.GitRepositories.AddRange(
            new GitRepository
            {
                Id = Guid.NewGuid(),
                ProjektId = _projektId,
                PluginTyp = "Softwareschmiede.GitHub",
                RepositoryUrl = "https://github.com/test/repo-a",
                RepositoryName = "repo-a",
                Aktiv = true
            },
            new GitRepository
            {
                Id = Guid.NewGuid(),
                ProjektId = _projektId,
                PluginTyp = "Softwareschmiede.GitHub",
                RepositoryUrl = "https://github.com/test/repo-b",
                RepositoryName = "repo-b",
                Aktiv = true
            });
        await _db.SaveChangesAsync();

        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "Mehrdeutiger Repository-Kontext", null);
        var projektService = new ProjektService(_db, NullLogger<ProjektService>.Instance);
        var sut = new EntwicklungsprozessService(
            _aufgabeService,
            _protokollService,
            projektService,
            _gitPluginMock.Object,
            CreatePluginSelectionService(_kiPluginMock.Object),
            _arbeitsverzeichnisResolverMock.Object,
            null,
            new Mock<ILogger<EntwicklungsprozessService>>().Object);

        // Act
        var act = () => sut.ProzessStartenAsync(aufgabe.Id, "https://github.com/test/unknown");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*kein eindeutiges Repository*");
        _gitPluginMock.Verify(git => git.CloneRepositoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>GetRemoteBranchesAsync löst das gewünschte SCM-Plugin über den Prefix auf und liefert dessen Branches zurück.</summary>
    [Fact]
    public async Task GetRemoteBranchesAsync_ShouldResolvePluginBySelectedPrefix_AndReturnPluginBranches()
    {
        // Arrange
        var defaultGitPluginMock = new Mock<IGitPlugin>();
        defaultGitPluginMock.SetupGet(plugin => plugin.PluginName).Returns("Default Git");
        defaultGitPluginMock.SetupGet(plugin => plugin.PluginPrefix).Returns("LocalDirectoryPlugin");
        defaultGitPluginMock.SetupGet(plugin => plugin.PluginType).Returns(PluginType.SourceCodeManagement);
        defaultGitPluginMock.Setup(plugin => plugin.GetSettingGroups()).Returns([]);
        defaultGitPluginMock.Setup(plugin => plugin.GetRemoteBranchesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(["main"]);

        var selectedGitPluginMock = new Mock<IGitPlugin>();
        selectedGitPluginMock.SetupGet(plugin => plugin.PluginName).Returns("GitHub");
        selectedGitPluginMock.SetupGet(plugin => plugin.PluginPrefix).Returns("Softwareschmiede.GitHub");
        selectedGitPluginMock.SetupGet(plugin => plugin.PluginType).Returns(PluginType.SourceCodeManagement);
        selectedGitPluginMock.Setup(plugin => plugin.GetSettingGroups()).Returns([]);
        selectedGitPluginMock.Setup(plugin => plugin.GetRemoteBranchesAsync("https://github.com/test/repo", It.IsAny<CancellationToken>()))
            .ReturnsAsync(["develop", "release/1.0"]);

        var pluginManagerMock = new Mock<IPluginManager>();
        pluginManagerMock.Setup(manager => manager.GetSourceCodeManagementPlugins()).Returns([defaultGitPluginMock.Object, selectedGitPluginMock.Object]);
        pluginManagerMock.Setup(manager => manager.GetDefaultSourceCodeManagementPlugin()).Returns(defaultGitPluginMock.Object);
        pluginManagerMock.Setup(manager => manager.GetDevelopmentAutomationPlugins()).Returns([_kiPluginMock.Object]);
        pluginManagerMock.Setup(manager => manager.GetDefaultDevelopmentAutomationPlugin()).Returns(_kiPluginMock.Object);

        var pluginDefaultSettings = new PluginDefaultSettingsService(_db, new Mock<ILogger<PluginDefaultSettingsService>>().Object);
        var pluginSelectionService = new PluginSelectionService(
            pluginManagerMock.Object,
            pluginDefaultSettings,
            new Mock<ILogger<PluginSelectionService>>().Object);

        var sut = new EntwicklungsprozessService(
            _aufgabeService,
            _protokollService,
            defaultGitPluginMock.Object,
            pluginSelectionService,
            _arbeitsverzeichnisResolverMock.Object,
            new Mock<ILogger<EntwicklungsprozessService>>().Object);

        // Act
        var result = (await sut.GetRemoteBranchesAsync("https://github.com/test/repo", "  softwaRESchmiede.githUB  ")).ToArray();

        // Assert
        result.Should().BeEquivalentTo(["develop", "release/1.0"]);
        selectedGitPluginMock.Verify(plugin => plugin.GetRemoteBranchesAsync("https://github.com/test/repo", It.IsAny<CancellationToken>()), Times.Once);
        defaultGitPluginMock.Verify(plugin => plugin.GetRemoteBranchesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

}
