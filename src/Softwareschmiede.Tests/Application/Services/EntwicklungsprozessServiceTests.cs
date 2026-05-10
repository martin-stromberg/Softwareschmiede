using FluentAssertions;
using Microsoft.Extensions.Logging;
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
    private readonly Mock<IAgentPackageService> _agentPackageServiceMock;
    private readonly Mock<IArbeitsverzeichnisResolver> _arbeitsverzeichnisResolverMock;
    private readonly EntwicklungsprozessService _sut;
    private readonly Guid _projektId = new Guid("44444444-4444-4444-4444-444444444444");

    public EntwicklungsprozessServiceTests()
    {
        _db = TestDbContextFactory.Create();
        _aufgabeService = new AufgabeService(_db, new Mock<ILogger<AufgabeService>>().Object);
        _protokollService = new ProtokollService(_db, new Mock<ILogger<ProtokollService>>().Object);
        _gitPluginMock = new Mock<IGitPlugin>();
        _kiPluginMock = new Mock<IKiPlugin>();
        _agentPackageServiceMock = new Mock<IAgentPackageService>();
        _arbeitsverzeichnisResolverMock = new Mock<IArbeitsverzeichnisResolver>();
        _arbeitsverzeichnisResolverMock.Setup(r => r.ResolveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ArbeitsverzeichnisResolutionResult(Path.GetTempPath(), false, "configured", null));

        _sut = new EntwicklungsprozessService(
            _aufgabeService,
            _protokollService,
            _gitPluginMock.Object,
            _kiPluginMock.Object,
            _agentPackageServiceMock.Object,
            _arbeitsverzeichnisResolverMock.Object,
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

    public void Dispose() => _db.Dispose();

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
        updatedAufgabe!.Status.Should().Be(AufgabeStatus.InBearbeitung);
        updatedAufgabe.BranchName.Should().Contain("login-feature");
    }

    /// <summary>ProzessStartenAsync deployt Agentenpaket wenn AgentenpaketName gesetzt ist.</summary>
    [Fact]
    public async Task ProzessStartenAsync_ShouldDeployAgentPackage_WhenAgentenpaketNameIsSet()
    {
        // Arrange
        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "Aufgabe mit Paket", null);
        await _aufgabeService.UpdateAsync(aufgabe.Id, aufgabe.Titel, aufgabe.AnforderungsBeschreibung, "mein-paket", null);

        var paket = new AgentPackageInfo("mein-paket", "/pfad/zum/paket", [], []);
        _agentPackageServiceMock.Setup(a => a.GetPackageAsync("mein-paket", It.IsAny<CancellationToken>()))
            .ReturnsAsync(paket);
        _gitPluginMock.Setup(g => g.CloneRepositoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _gitPluginMock.Setup(g => g.CreateBranchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _kiPluginMock.Setup(k => k.DeployAgentPackageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _kiPluginMock.Setup(k => k.IsAgentPackageCompatibleAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _sut.ProzessStartenAsync(aufgabe.Id, "https://github.com/test/repo");

        // Assert
        _kiPluginMock.Verify(k => k.DeployAgentPackageAsync("/pfad/zum/paket", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
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
    public async Task KiStartenAsync_ShouldThrowInvalidOperationException_WhenKiAlreadyActive()
    {
        // Arrange
        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "KI Doppelstart", null);
        await _aufgabeService.StartenAsync(aufgabe.Id, "branch", "/pfad");
        await _aufgabeService.KiAktiviertAsync(aufgabe.Id);
        var agent = new AgentInfo("test-agent", "Beschreibung", "/pfad/agent.md");

        // Act
        var act = async () =>
        {
            await foreach (var _ in _sut.KiStartenAsync(aufgabe.Id, "prompt", agent)) { }
        };

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*KI ist bereits aktiv*");
    }

    /// <summary>KiStartenAsync wirft Exception wenn kein LokalerKlonPfad gesetzt ist.</summary>
    [Fact]
    public async Task KiStartenAsync_ShouldThrowInvalidOperationException_WhenNoLokalerKlonPfad()
    {
        // Arrange
        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "Keine Klon Aufgabe", null);
        var agent = new AgentInfo("test-agent", "Beschreibung", "/pfad/agent.md");

        // Act
        var act = async () =>
        {
            await foreach (var _ in _sut.KiStartenAsync(aufgabe.Id, "prompt", agent)) { }
        };

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*lokalen Klonpfad*");
    }

    /// <summary>AbschliessenAsync setzt Status auf Abgeschlossen und erstellt Protokolleintrag.</summary>
    [Fact]
    public async Task AbschliessenAsync_ShouldSetStatusAbgeschlossenAndAddProtokoll_WhenAufgabeExists()
    {
        // Arrange
        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "Abzuschließende Aufgabe", null);

        // Act
        await _sut.AbschliessenAsync(aufgabe.Id);

        // Assert
        var result = await _aufgabeService.GetByIdAsync(aufgabe.Id);
        result!.Status.Should().Be(AufgabeStatus.Abgeschlossen);

        var protokoll = await _protokollService.GetByAufgabeAsync(aufgabe.Id);
        protokoll.Should().Contain(p => p.Typ == ProtokollTyp.StatusUebergang);
    }

    /// <summary>AbbrechenAsync setzt Status auf Offen und erstellt Protokolleintrag.</summary>
    [Fact]
    public async Task AbbrechenAsync_ShouldSetStatusOffenAndAddProtokoll_WhenAufgabeExists()
    {
        // Arrange
        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "Abzubrechende Aufgabe", null);
        await _aufgabeService.StartenAsync(aufgabe.Id, "branch", "/pfad");

        // Act
        await _sut.AbbrechenAsync(aufgabe.Id);

        // Assert
        var result = await _aufgabeService.GetByIdAsync(aufgabe.Id);
        result!.Status.Should().Be(AufgabeStatus.Offen);

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

    /// <summary>PushDurchfuehrenAsync wirft Exception wenn kein BranchName gesetzt.</summary>
    [Fact]
    public async Task PushDurchfuehrenAsync_ShouldThrowInvalidOperationException_WhenNoBranchName()
    {
        // Arrange
        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "Push ohne Branch", null);
        await _aufgabeService.StartenAsync(aufgabe.Id, "branch", "/pfad");
        await _aufgabeService.AbschliessenAsync(aufgabe.Id); // löscht BranchName

        // Act
        var act = () => _sut.PushDurchfuehrenAsync(aufgabe.Id);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
    public async Task ProzessStartenAsync_ShouldSkipDeploy_WhenAgentPackageIsMissing()
    {
        // Arrange
        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "Missing Package", null);
        await _aufgabeService.UpdateAsync(aufgabe.Id, aufgabe.Titel, aufgabe.AnforderungsBeschreibung, "missing-package", null);

        _agentPackageServiceMock.Setup(a => a.GetPackageAsync("missing-package", It.IsAny<CancellationToken>()))
            .ReturnsAsync((AgentPackageInfo?)null);
        _gitPluginMock.Setup(g => g.CloneRepositoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _gitPluginMock.Setup(g => g.CreateBranchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _sut.ProzessStartenAsync(aufgabe.Id, "https://github.com/test/repo");

        // Assert
        _kiPluginMock.Verify(k => k.DeployAgentPackageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _agentPackageServiceMock.Verify(a => a.GetPackageAsync("missing-package", It.IsAny<CancellationToken>()), Times.Exactly(2));
    }
}
