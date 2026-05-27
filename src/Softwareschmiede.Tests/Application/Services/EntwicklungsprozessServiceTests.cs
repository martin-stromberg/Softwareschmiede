using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Abstractions;
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
        _kiPluginMock.SetupGet(p => p.PluginName).Returns("Test KI");
        _kiPluginMock.SetupGet(p => p.PluginPrefix).Returns("Softwareschmiede.TestKi");
        _kiPluginMock.SetupGet(p => p.PluginType).Returns(PluginType.DevelopmentAutomation);
        _kiPluginMock.Setup(p => p.GetSettingGroups()).Returns([]);
        _agentPackageServiceMock = new Mock<IAgentPackageService>();
        _arbeitsverzeichnisResolverMock = new Mock<IArbeitsverzeichnisResolver>();
        _arbeitsverzeichnisResolverMock.Setup(r => r.ResolveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ArbeitsverzeichnisResolutionResult(Path.GetTempPath(), false, "configured", null));

        _sut = new EntwicklungsprozessService(
            _aufgabeService,
            _protokollService,
            _gitPluginMock.Object,
            CreatePluginSelectionService(_kiPluginMock.Object),
            _agentPackageServiceMock.Object,
            _arbeitsverzeichnisResolverMock.Object,
            new ConfigurationBuilder().Build(),
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
        updatedAufgabe!.Status.Should().Be(AufgabeStatus.InBearbeitung);
        updatedAufgabe.BranchName.Should().Contain("login-feature");
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
            _agentPackageServiceMock.Object,
            _arbeitsverzeichnisResolverMock.Object,
            repositoryStartskriptService,
            new ConfigurationBuilder().Build(),
            new Mock<ILogger<EntwicklungsprozessService>>().Object);

        // Act
        await sut.ProzessStartenAsync(aufgabe.Id, repository.RepositoryUrl);

        // Assert
        var updatedAufgabe = await _aufgabeService.GetByIdAsync(aufgabe.Id);
        updatedAufgabe.Should().NotBeNull();
        updatedAufgabe!.Status.Should().Be(AufgabeStatus.InBearbeitung);
        var protokoll = await _protokollService.GetByAufgabeAsync(aufgabe.Id);
        protokoll.Should().Contain(entry =>
            entry.Typ == ProtokollTyp.GitAktion
            && entry.Inhalt.Contains("Hinweis: Das Repository-Startskript konnte nicht ausgeführt werden", StringComparison.Ordinal));
    }

    /// <summary>RepositoryStartskriptAusfuehrenAsync liefert einen Hinweis statt Exception bei Skriptfehler.</summary>
    [Fact]
    public async Task RepositoryStartskriptAusfuehrenAsync_ShouldReturnHint_WhenScriptExecutionFails()
    {
        // Arrange
        var repository = new GitRepository
        {
            Id = Guid.NewGuid(),
            ProjektId = _projektId,
            PluginTyp = "Softwareschmiede.GitHub",
            RepositoryUrl = "https://github.com/test/repo-manual-start",
            RepositoryName = "repo-manual-start",
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

        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "Manuelles Startskript", null, repository.Id);
        var lokalerPfad = Path.Combine(Path.GetTempPath(), $"manual-start-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(lokalerPfad, "scripts"));
        await File.WriteAllTextAsync(Path.Combine(lokalerPfad, "scripts", "start.ps1"), "Write-Host 'test'");
        await _aufgabeService.StartenAsync(aufgabe.Id, "task/manual", lokalerPfad);

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
            _agentPackageServiceMock.Object,
            _arbeitsverzeichnisResolverMock.Object,
            repositoryStartskriptService,
            new ConfigurationBuilder().Build(),
            new Mock<ILogger<EntwicklungsprozessService>>().Object);

        try
        {
            // Act
            var result = await sut.RepositoryStartskriptAusfuehrenAsync(aufgabe.Id);

            // Assert
            result.Success.Should().BeFalse();
            result.Message.Should().Contain("Hinweis: Das Repository-Startskript konnte nicht ausgeführt werden");
            var protokoll = await _protokollService.GetByAufgabeAsync(aufgabe.Id);
            protokoll.Should().Contain(entry =>
                entry.Typ == ProtokollTyp.GitAktion
                && entry.Inhalt.Contains("Hinweis: Das Repository-Startskript konnte nicht ausgeführt werden", StringComparison.Ordinal));
        }
        finally
        {
            if (Directory.Exists(lokalerPfad))
            {
                Directory.Delete(lokalerPfad, recursive: true);
            }
        }
    }

    [Fact]
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

    /// <summary>ProzessStartenAsync bricht vor Clone ab, wenn Agentenpaket inkompatibel ist.</summary>
    [Fact]
    public async Task ProzessStartenAsync_ShouldThrowAndSkipClone_WhenAgentPackageIsIncompatible()
    {
        // Arrange
        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "Inkompatibles Paket", null);
        await _aufgabeService.UpdateAsync(aufgabe.Id, aufgabe.Titel, aufgabe.AnforderungsBeschreibung, "inkompatibel", null);
        var paket = new AgentPackageInfo("inkompatibel", "/pfad/zum/paket", [], []);

        _agentPackageServiceMock.Setup(a => a.GetPackageAsync("inkompatibel", It.IsAny<CancellationToken>()))
            .ReturnsAsync(paket);
        _kiPluginMock.Setup(k => k.IsAgentPackageCompatibleAsync("/pfad/zum/paket", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _kiPluginMock.SetupGet(k => k.PluginName).Returns("Claude CLI");

        // Act
        var act = () => _sut.ProzessStartenAsync(aufgabe.Id, "https://github.com/test/repo");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*nicht kompatibel*");
        _gitPluginMock.Verify(g => g.CloneRepositoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
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

    [Fact]
    public async Task KiStartenAsync_ShouldForwardSelectedAgentToPlugin()
    {
        // Arrange
        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "Agent Forwarding", null);
        await _aufgabeService.StartenAsync(aufgabe.Id, "branch", "/repo");
        var expectedAgent = new AgentInfo("agent-alt", "Beschreibung", "/pfad/agent.md");

        _kiPluginMock.Setup(k => k.StartDevelopmentAsync(
                It.IsAny<string>(),
                It.IsAny<AgentInfo>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns<string, AgentInfo, string, string?, CancellationToken>((_, _, _, _, _) => StreamSingleLine("ok"));

        // Act
        await foreach (var _ in _sut.KiStartenAsync(aufgabe.Id, "Folgeprompt", expectedAgent))
        {
        }

        // Assert
        _kiPluginMock.Verify(k => k.StartDevelopmentAsync(
            "Folgeprompt",
            It.Is<AgentInfo>(a => a.Name == "agent-alt"),
            "/repo",
            null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task KiStartenAsync_ShouldUseStoredTaskKiPluginPrefix_WhenNoSelectedPrefixProvided()
    {
        // Arrange
        var defaultPluginMock = new Mock<IKiPlugin>();
        defaultPluginMock.SetupGet(plugin => plugin.PluginName).Returns("Default KI");
        defaultPluginMock.SetupGet(plugin => plugin.PluginPrefix).Returns("Softwareschmiede.KiA");
        defaultPluginMock.SetupGet(plugin => plugin.PluginType).Returns(PluginType.DevelopmentAutomation);
        defaultPluginMock.Setup(plugin => plugin.GetSettingGroups()).Returns([]);
        defaultPluginMock.Setup(plugin => plugin.StartDevelopmentAsync(
                It.IsAny<string>(),
                It.IsAny<AgentInfo>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns<string, AgentInfo, string, string?, CancellationToken>((_, _, _, _, _) => StreamSingleLine("default"));

        var storedPluginMock = new Mock<IKiPlugin>();
        storedPluginMock.SetupGet(plugin => plugin.PluginName).Returns("Stored KI");
        storedPluginMock.SetupGet(plugin => plugin.PluginPrefix).Returns("Softwareschmiede.KiB");
        storedPluginMock.SetupGet(plugin => plugin.PluginType).Returns(PluginType.DevelopmentAutomation);
        storedPluginMock.Setup(plugin => plugin.GetSettingGroups()).Returns([]);
        storedPluginMock.Setup(plugin => plugin.StartDevelopmentAsync(
                It.IsAny<string>(),
                It.IsAny<AgentInfo>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns<string, AgentInfo, string, string?, CancellationToken>((_, _, _, _, _) => StreamSingleLine("stored"));

        var sut = new EntwicklungsprozessService(
            _aufgabeService,
            _protokollService,
            _gitPluginMock.Object,
            CreatePluginSelectionService(defaultPluginMock.Object, storedPluginMock.Object),
            _agentPackageServiceMock.Object,
            _arbeitsverzeichnisResolverMock.Object,
            new ConfigurationBuilder().Build(),
            new Mock<ILogger<EntwicklungsprozessService>>().Object);

        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "Stored KI Prefix", null);
        await _aufgabeService.StartenAsync(aufgabe.Id, "branch", "/repo");
        await _aufgabeService.UpdateAsync(
            aufgabe.Id,
            aufgabe.Titel,
            aufgabe.AnforderungsBeschreibung,
            null,
            null,
            "Softwareschmiede.KiB");
        var agent = new AgentInfo("agent", "Beschreibung", "/pfad/agent.md");

        // Act
        await foreach (var _ in sut.KiStartenAsync(aufgabe.Id, "Prompt", agent))
        {
        }

        // Assert
        storedPluginMock.Verify(plugin => plugin.StartDevelopmentAsync(
            It.IsAny<string>(),
            It.IsAny<AgentInfo>(),
            "/repo",
            null,
            It.IsAny<CancellationToken>()), Times.Once);
        defaultPluginMock.Verify(plugin => plugin.StartDevelopmentAsync(
            It.IsAny<string>(),
            It.IsAny<AgentInfo>(),
            It.IsAny<string>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task KiStartenAsync_ShouldPreferExplicitSelectedKiPluginPrefix_OverStoredTaskPrefix()
    {
        // Arrange
        var selectedPluginMock = new Mock<IKiPlugin>();
        selectedPluginMock.SetupGet(plugin => plugin.PluginName).Returns("Selected KI");
        selectedPluginMock.SetupGet(plugin => plugin.PluginPrefix).Returns("Softwareschmiede.KiA");
        selectedPluginMock.SetupGet(plugin => plugin.PluginType).Returns(PluginType.DevelopmentAutomation);
        selectedPluginMock.Setup(plugin => plugin.GetSettingGroups()).Returns([]);
        selectedPluginMock.Setup(plugin => plugin.StartDevelopmentAsync(
                It.IsAny<string>(),
                It.IsAny<AgentInfo>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns<string, AgentInfo, string, string?, CancellationToken>((_, _, _, _, _) => StreamSingleLine("selected"));

        var storedPluginMock = new Mock<IKiPlugin>();
        storedPluginMock.SetupGet(plugin => plugin.PluginName).Returns("Stored KI");
        storedPluginMock.SetupGet(plugin => plugin.PluginPrefix).Returns("Softwareschmiede.KiB");
        storedPluginMock.SetupGet(plugin => plugin.PluginType).Returns(PluginType.DevelopmentAutomation);
        storedPluginMock.Setup(plugin => plugin.GetSettingGroups()).Returns([]);
        storedPluginMock.Setup(plugin => plugin.StartDevelopmentAsync(
                It.IsAny<string>(),
                It.IsAny<AgentInfo>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns<string, AgentInfo, string, string?, CancellationToken>((_, _, _, _, _) => StreamSingleLine("stored"));

        var sut = new EntwicklungsprozessService(
            _aufgabeService,
            _protokollService,
            _gitPluginMock.Object,
            CreatePluginSelectionService(selectedPluginMock.Object, storedPluginMock.Object),
            _agentPackageServiceMock.Object,
            _arbeitsverzeichnisResolverMock.Object,
            new ConfigurationBuilder().Build(),
            new Mock<ILogger<EntwicklungsprozessService>>().Object);

        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "Explicit KI Prefix", null);
        await _aufgabeService.StartenAsync(aufgabe.Id, "branch", "/repo");
        await _aufgabeService.UpdateAsync(
            aufgabe.Id,
            aufgabe.Titel,
            aufgabe.AnforderungsBeschreibung,
            null,
            null,
            "Softwareschmiede.KiB");
        var agent = new AgentInfo("agent", "Beschreibung", "/pfad/agent.md");

        // Act
        await foreach (var _ in sut.KiStartenAsync(aufgabe.Id, "Prompt", agent, "Softwareschmiede.KiA"))
        {
        }

        // Assert
        selectedPluginMock.Verify(plugin => plugin.StartDevelopmentAsync(
            It.IsAny<string>(),
            It.IsAny<AgentInfo>(),
            "/repo",
            null,
            It.IsAny<CancellationToken>()), Times.Once);
        storedPluginMock.Verify(plugin => plugin.StartDevelopmentAsync(
            It.IsAny<string>(),
            It.IsAny<AgentInfo>(),
            It.IsAny<string>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task KiStartenAsync_ShouldPersistMarkdownArbeitsprotokoll_WithDateHeadingAndSeparatedSteps()
    {
        // Arrange
        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "Markdown Protokoll", null);
        await _aufgabeService.StartenAsync(aufgabe.Id, "branch", "/repo");
        var agent = new AgentInfo("test-agent", "Beschreibung", "/pfad/agent.md");

        _kiPluginMock.Setup(k => k.StartDevelopmentAsync(
                It.IsAny<string>(),
                It.IsAny<AgentInfo>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns<string, AgentInfo, string, string?, CancellationToken>((_, _, _, _, _) => StreamLines("Analyse", "Implementierung"));

        // Act
        await foreach (var _ in _sut.KiStartenAsync(aufgabe.Id, "Prompt", agent))
        {
        }

        // Assert
        var protokoll = (await _protokollService.GetByAufgabeAsync(aufgabe.Id)).ToList();
        var kiAntwort = protokoll.Last(p => p.Typ == ProtokollTyp.KiAntwort).Inhalt;
        kiAntwort.Should().MatchRegex(@"^# \d{4}-\d{2}-\d{2}");
        kiAntwort.Should().MatchRegex(@"- RunId: `[0-9a-fA-F-]{36}`");
        kiAntwort.Should().Contain("## Schritt 1");
        kiAntwort.Should().Contain("Analyse");
        kiAntwort.Should().Contain("## Schritt 2");
        kiAntwort.Should().Contain("Implementierung");
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("\t")]
    [InlineData("   \t   ")]
    public async Task KiStartenAsync_ShouldPersistFallbackStep_WhenKiOutputIsWhitespaceOnly(string kiAntwortRoh)
    {
        // Arrange
        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "Markdown Fallback", null);
        await _aufgabeService.StartenAsync(aufgabe.Id, "branch", "/repo");
        var agent = new AgentInfo("test-agent", "Beschreibung", "/pfad/agent.md");

        _kiPluginMock.Setup(k => k.StartDevelopmentAsync(
                It.IsAny<string>(),
                It.IsAny<AgentInfo>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns<string, AgentInfo, string, string?, CancellationToken>((_, _, _, _, _) => StreamSingleLine(kiAntwortRoh));

        // Act
        await foreach (var _ in _sut.KiStartenAsync(aufgabe.Id, "Prompt", agent))
        {
        }

        // Assert
        var protokoll = (await _protokollService.GetByAufgabeAsync(aufgabe.Id)).ToList();
        var kiAntwort = protokoll.Last(p => p.Typ == ProtokollTyp.KiAntwort).Inhalt;
        kiAntwort.Should().MatchRegex(@"- RunId: `[0-9a-fA-F-]{36}`");
        kiAntwort.Should().Contain("## Schritt 1");
        kiAntwort.Should().Contain("Keine Ausgabe vorhanden.");
        kiAntwort.Should().NotContain("## Schritt 2");
    }

    [Fact]
    public async Task KiStartenAsync_ShouldNormalizeLineBreaks_AndKeepStepOrder()
    {
        // Arrange
        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "Markdown Normalisierung", null);
        await _aufgabeService.StartenAsync(aufgabe.Id, "branch", "/repo");
        var agent = new AgentInfo("test-agent", "Beschreibung", "/pfad/agent.md");

        _kiPluginMock.Setup(k => k.StartDevelopmentAsync(
                It.IsAny<string>(),
                It.IsAny<AgentInfo>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns<string, AgentInfo, string, string?, CancellationToken>((_, _, _, _, _) =>
                StreamSingleLine("Analyse   \r\n\r\nImplementierung\t  \n\nReview  "));

        // Act
        await foreach (var _ in _sut.KiStartenAsync(aufgabe.Id, "Prompt", agent))
        {
        }

        // Assert
        var protokoll = (await _protokollService.GetByAufgabeAsync(aufgabe.Id)).ToList();
        var kiAntwort = protokoll.Last(p => p.Typ == ProtokollTyp.KiAntwort).Inhalt;
        kiAntwort.Should().MatchRegex(@"## Schritt 1\s+Analyse\s+## Schritt 2\s+Implementierung\s+## Schritt 3\s+Review");
        kiAntwort.Should().NotContain("## Schritt 4");
        kiAntwort.Should().NotContain("Analyse   ");
        kiAntwort.Should().NotContain("Implementierung\t");
        kiAntwort.Should().NotContain("Review  ");
    }

    [Fact]
    public async Task KiStartenAsync_ShouldAppendContextFile_ForKontextIgnorieren()
    {
        // Arrange
        var repoPath = Path.Combine(Path.GetTempPath(), $"ctx-ignore-{Guid.NewGuid():N}");
        Directory.CreateDirectory(repoPath);
        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "Kontext ignorieren", null);
        await _aufgabeService.StartenAsync(aufgabe.Id, "branch", repoPath);
        var agent = new AgentInfo("test-agent", "Beschreibung", "/pfad/agent.md");

        _kiPluginMock.Setup(k => k.StartDevelopmentAsync(
                It.IsAny<string>(),
                It.IsAny<AgentInfo>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns<string, AgentInfo, string, string?, CancellationToken>((_, _, _, _, _) => StreamSingleLine("Antwort"));

        // Act
        await foreach (var _ in _sut.KiStartenAsync(aufgabe.Id, "Neue Folgeanweisung", agent, null, null, FolgeanweisungsKontextmodus.KontextIgnorieren))
        {
        }

        // Assert
        _kiPluginMock.Verify(k => k.StartDevelopmentAsync(
            "Neue Folgeanweisung",
            It.IsAny<AgentInfo>(),
            repoPath,
            null,
            It.IsAny<CancellationToken>()), Times.Once);

        var contextPath = Path.Combine(repoPath, $"{aufgabe.Id}.copilot.context.md");
        File.Exists(contextPath).Should().BeTrue();
        var content = await File.ReadAllTextAsync(contextPath);
        content.Should().Contain("Neue Folgeanweisung");
        content.Should().Contain("Antwort");
    }

    [Fact]
    public async Task KiStartenAsync_ShouldUseProviderSpecificContextFile_ForCliPluginBase()
    {
        // Arrange
        var repoPath = Path.Combine(Path.GetTempPath(), $"ctx-provider-{Guid.NewGuid():N}");
        Directory.CreateDirectory(repoPath);
        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "Provider Kontext", null);
        await _aufgabeService.StartenAsync(aufgabe.Id, "branch", repoPath);
        var agent = new AgentInfo("test-agent", "Beschreibung", "/pfad/agent.md");
        var plugin = new TestCliKiPluginBase("claude", StreamSingleLine("Antwort"));
        var sut = new EntwicklungsprozessService(
            _aufgabeService,
            _protokollService,
            _gitPluginMock.Object,
            CreatePluginSelectionService(plugin),
            _agentPackageServiceMock.Object,
            _arbeitsverzeichnisResolverMock.Object,
            new ConfigurationBuilder().Build(),
            new Mock<ILogger<EntwicklungsprozessService>>().Object);

        // Act
        await foreach (var _ in sut.KiStartenAsync(aufgabe.Id, "Neue Folgeanweisung", agent, null, null, FolgeanweisungsKontextmodus.KontextIgnorieren))
        {
        }

        // Assert
        var claudeContextPath = Path.Combine(repoPath, $"{aufgabe.Id}.claude.context.md");
        var copilotContextPath = Path.Combine(repoPath, $"{aufgabe.Id}.copilot.context.md");
        File.Exists(claudeContextPath).Should().BeTrue();
        File.Exists(copilotContextPath).Should().BeFalse();
    }

    [Fact]
    public async Task KiStartenAsync_ShouldReadAndAppendProviderContext_ForKontextMitgeben()
    {
        // Arrange
        var repoPath = Path.Combine(Path.GetTempPath(), $"ctx-provider-include-{Guid.NewGuid():N}");
        Directory.CreateDirectory(repoPath);
        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "Provider Kontext Mitgeben", null);
        await _aufgabeService.StartenAsync(aufgabe.Id, "branch", repoPath);

        var plugin = new TestCliKiPluginBase("claude", StreamSingleLine("Antwort"));
        var claudeContextPath = Path.Combine(repoPath, $"{aufgabe.Id}.claude.context.md");
        await File.WriteAllTextAsync(claudeContextPath, "Claude-Kontext");

        var agent = new AgentInfo("test-agent", "Beschreibung", "/pfad/agent.md");
        var sut = new EntwicklungsprozessService(
            _aufgabeService,
            _protokollService,
            _gitPluginMock.Object,
            CreatePluginSelectionService(plugin),
            _agentPackageServiceMock.Object,
            _arbeitsverzeichnisResolverMock.Object,
            new ConfigurationBuilder().Build(),
            new Mock<ILogger<EntwicklungsprozessService>>().Object);

        // Act
        await foreach (var _ in sut.KiStartenAsync(aufgabe.Id, "Neue Folgeanweisung", agent, null, null, FolgeanweisungsKontextmodus.KontextMitgeben))
        {
        }

        // Assert
        plugin.LastPrompt.Should().NotBeNull();
        plugin.LastPrompt.Should().StartWith("Claude-Kontext");
        plugin.LastPrompt.Should().Contain("\n\n---\n\nNeue Folgeanweisung");

        var copilotContextPath = Path.Combine(repoPath, $"{aufgabe.Id}.copilot.context.md");
        File.Exists(claudeContextPath).Should().BeTrue();
        File.Exists(copilotContextPath).Should().BeFalse();
    }

    [Fact]
    public async Task KiStartenAsync_ShouldResetOnlyProviderContext_ForKontextNeuBeginnen()
    {
        // Arrange
        var repoPath = Path.Combine(Path.GetTempPath(), $"ctx-provider-reset-{Guid.NewGuid():N}");
        Directory.CreateDirectory(repoPath);
        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "Provider Kontext Reset", null);
        await _aufgabeService.StartenAsync(aufgabe.Id, "branch", repoPath);

        var plugin = new TestCliKiPluginBase("claude", StreamSingleLine("Antwort"));
        var claudeContextPath = Path.Combine(repoPath, $"{aufgabe.Id}.claude.context.md");
        var copilotContextPath = Path.Combine(repoPath, $"{aufgabe.Id}.copilot.context.md");
        await File.WriteAllTextAsync(claudeContextPath, "Alter Claude-Kontext");
        await File.WriteAllTextAsync(copilotContextPath, "Unveraenderter Copilot-Kontext");

        var agent = new AgentInfo("test-agent", "Beschreibung", "/pfad/agent.md");
        var sut = new EntwicklungsprozessService(
            _aufgabeService,
            _protokollService,
            _gitPluginMock.Object,
            CreatePluginSelectionService(plugin),
            _agentPackageServiceMock.Object,
            _arbeitsverzeichnisResolverMock.Object,
            new ConfigurationBuilder().Build(),
            new Mock<ILogger<EntwicklungsprozessService>>().Object);

        // Act
        await foreach (var _ in sut.KiStartenAsync(aufgabe.Id, "Neu anfangen", agent, null, null, FolgeanweisungsKontextmodus.KontextNeuBeginnen))
        {
        }

        // Assert
        var claudeContent = await File.ReadAllTextAsync(claudeContextPath);
        var copilotContent = await File.ReadAllTextAsync(copilotContextPath);

        claudeContent.Should().Contain("Reset durch Folgeanweisung");
        claudeContent.Should().Contain($"# Kontextverlauf Aufgabe {aufgabe.Id}");
        copilotContent.Should().Be("Unveraenderter Copilot-Kontext");
    }

    [Fact]
    public async Task KiStartenAsync_ShouldPrependContext_ForKontextMitgeben()
    {
        // Arrange
        var repoPath = Path.Combine(Path.GetTempPath(), $"ctx-include-{Guid.NewGuid():N}");
        Directory.CreateDirectory(repoPath);
        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "Kontext mitgeben", null);
        await _aufgabeService.StartenAsync(aufgabe.Id, "branch", repoPath);
        var contextPath = Path.Combine(repoPath, $"{aufgabe.Id}.copilot.context.md");
        await File.WriteAllTextAsync(contextPath, "Bisheriger Kontext");
        var agent = new AgentInfo("test-agent", "Beschreibung", "/pfad/agent.md");

        _kiPluginMock.Setup(k => k.StartDevelopmentAsync(
                It.IsAny<string>(),
                It.IsAny<AgentInfo>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns<string, AgentInfo, string, string?, CancellationToken>((_, _, _, _, _) => StreamSingleLine("Antwort"));

        // Act
        await foreach (var _ in _sut.KiStartenAsync(aufgabe.Id, "Neue Folgeanweisung", agent, null, null, FolgeanweisungsKontextmodus.KontextMitgeben))
        {
        }

        // Assert
        _kiPluginMock.Verify(k => k.StartDevelopmentAsync(
            It.Is<string>(p => p.StartsWith("Bisheriger Kontext", StringComparison.Ordinal) && p.Contains("\n\n---\n\nNeue Folgeanweisung")),
            It.IsAny<AgentInfo>(),
            repoPath,
            null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task KiStartenAsync_ShouldUsePromptOnly_WhenKontextMitgebenAndContextFileMissing()
    {
        // Arrange
        var repoPath = Path.Combine(Path.GetTempPath(), $"ctx-missing-{Guid.NewGuid():N}");
        Directory.CreateDirectory(repoPath);
        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "Kontext fehlt", null);
        await _aufgabeService.StartenAsync(aufgabe.Id, "branch", repoPath);
        var agent = new AgentInfo("test-agent", "Beschreibung", "/pfad/agent.md");

        _kiPluginMock.Setup(k => k.StartDevelopmentAsync(
                It.IsAny<string>(),
                It.IsAny<AgentInfo>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns<string, AgentInfo, string, string?, CancellationToken>((_, _, _, _, _) => StreamSingleLine("Antwort"));

        // Act
        await foreach (var _ in _sut.KiStartenAsync(aufgabe.Id, "Nur Prompt", agent, null, null, FolgeanweisungsKontextmodus.KontextMitgeben))
        {
        }

        // Assert
        _kiPluginMock.Verify(k => k.StartDevelopmentAsync(
            "Nur Prompt",
            It.IsAny<AgentInfo>(),
            repoPath,
            null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task KiStartenAsync_ShouldResetContext_ForKontextNeuBeginnen()
    {
        // Arrange
        var repoPath = Path.Combine(Path.GetTempPath(), $"ctx-reset-{Guid.NewGuid():N}");
        Directory.CreateDirectory(repoPath);
        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "Kontext reset", null);
        await _aufgabeService.StartenAsync(aufgabe.Id, "branch", repoPath);
        var contextPath = Path.Combine(repoPath, $"{aufgabe.Id}.copilot.context.md");
        await File.WriteAllTextAsync(contextPath, "Alter Verlauf");
        var agent = new AgentInfo("test-agent", "Beschreibung", "/pfad/agent.md");

        _kiPluginMock.Setup(k => k.StartDevelopmentAsync(
                It.IsAny<string>(),
                It.IsAny<AgentInfo>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns<string, AgentInfo, string, string?, CancellationToken>((_, _, _, _, _) => StreamSingleLine("Antwort"));

        // Act
        await foreach (var _ in _sut.KiStartenAsync(aufgabe.Id, "Neu anfangen", agent, null, null, FolgeanweisungsKontextmodus.KontextNeuBeginnen))
        {
        }

        // Assert
        var updated = await File.ReadAllTextAsync(contextPath);
        updated.Should().NotContain("Alter Verlauf");
        updated.Should().Contain("Neu anfangen");
    }

    [Fact]
    public async Task KiStartenAsync_ShouldKeepPreviousEntries_WhenPromptsUseKontextMitgebenConsecutively()
    {
        // Arrange
        var repoPath = Path.Combine(Path.GetTempPath(), $"ctx-mitgeben-sequence-{Guid.NewGuid():N}");
        Directory.CreateDirectory(repoPath);
        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "Kontext fortschreiben", null);
        await _aufgabeService.StartenAsync(aufgabe.Id, "branch", repoPath);
        var agent = new AgentInfo("test-agent", "Beschreibung", "/pfad/agent.md");
        var prompts = new List<string>();
        var answers = new Queue<string>(["Antwort 1", "Antwort 2"]);

        _kiPluginMock.Setup(k => k.StartDevelopmentAsync(
                It.IsAny<string>(),
                It.IsAny<AgentInfo>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns<string, AgentInfo, string, string?, CancellationToken>((prompt, _, _, _, _) =>
            {
                prompts.Add(prompt);
                return StreamSingleLine(answers.Dequeue());
            });

        // Act
        await foreach (var _ in _sut.KiStartenAsync(aufgabe.Id, "Prompt 1", agent, null, null, FolgeanweisungsKontextmodus.KontextMitgeben))
        {
        }

        await foreach (var _ in _sut.KiStartenAsync(aufgabe.Id, "Prompt 2", agent, null, null, FolgeanweisungsKontextmodus.KontextMitgeben))
        {
        }

        // Assert
        prompts.Should().HaveCount(2);
        prompts[0].Should().Be("Prompt 1");
        prompts[1].Should().Contain("Prompt 1");
        prompts[1].Should().Contain("Antwort 1");
        prompts[1].Should().Contain("Prompt 2");

        var contextPath = Path.Combine(repoPath, $"{aufgabe.Id}.copilot.context.md");
        var context = await File.ReadAllTextAsync(contextPath);
        context.Should().Contain("Prompt 1");
        context.Should().Contain("Antwort 1");
        context.Should().Contain("Prompt 2");
        context.Should().Contain("Antwort 2");
    }

    [Fact]
    public async Task KiStartenAsync_ShouldKeepCompleteContextHistory_WhenCompressionIsUsedForPrompt()
    {
        // Arrange
        var repoPath = Path.Combine(Path.GetTempPath(), $"ctx-keep-history-{Guid.NewGuid():N}");
        Directory.CreateDirectory(repoPath);
        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "Kontext Historie", null);
        await _aufgabeService.StartenAsync(aufgabe.Id, "branch", repoPath);
        var agent = new AgentInfo("test-agent", "Beschreibung", "/pfad/agent.md");

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["KiKontext:SoftLimitChars"] = "120",
                ["KiKontext:HardLimitChars"] = "5000"
            })
            .Build();

        var runResponses = new Queue<string>(
        [
            $"Antwort 1 {new string('x', 240)}",
            "Antwort 2"
        ]);
        _kiPluginMock.Setup(k => k.StartDevelopmentAsync(
                It.IsAny<string>(),
                It.IsAny<AgentInfo>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns<string, AgentInfo, string, string?, CancellationToken>((prompt, _, _, _, _) =>
            {
                if (prompt.Contains("Komprimiere den folgenden Projektkontext", StringComparison.Ordinal))
                {
                    return StreamSingleLine(
                        "## Ziel\nKurz\n\n## Offene Punkte\nKurz\n\n## Letzte Entscheidungen\nKurz");
                }

                return StreamSingleLine(runResponses.Dequeue());
            });

        var sut = new EntwicklungsprozessService(
            _aufgabeService,
            _protokollService,
            _gitPluginMock.Object,
            CreatePluginSelectionService(_kiPluginMock.Object),
            _agentPackageServiceMock.Object,
            _arbeitsverzeichnisResolverMock.Object,
            config,
            new Mock<ILogger<EntwicklungsprozessService>>().Object);

        // Act
        await foreach (var _ in sut.KiStartenAsync(aufgabe.Id, "Prompt 1", agent, null, null, FolgeanweisungsKontextmodus.KontextIgnorieren))
        {
        }

        await foreach (var _ in sut.KiStartenAsync(aufgabe.Id, "Prompt 2", agent, null, null, FolgeanweisungsKontextmodus.KontextMitgeben))
        {
        }

        // Assert
        var contextPath = Path.Combine(repoPath, $"{aufgabe.Id}.copilot.context.md");
        var context = await File.ReadAllTextAsync(contextPath);
        context.Should().Contain("Prompt 1");
        context.Should().Contain("Antwort 1");
        context.Should().Contain("Prompt 2");
        context.Should().Contain("Antwort 2");
    }

    [Fact]
    public async Task KiStartenAsync_ShouldAbort_WhenHardLimitStillExceededAfterCompression()
    {
        // Arrange
        var repoPath = Path.Combine(Path.GetTempPath(), $"ctx-hardlimit-{Guid.NewGuid():N}");
        Directory.CreateDirectory(repoPath);
        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "Hard limit", null);
        await _aufgabeService.StartenAsync(aufgabe.Id, "branch", repoPath);
        var contextPath = Path.Combine(repoPath, $"{aufgabe.Id}.copilot.context.md");
        await File.WriteAllTextAsync(contextPath, new string('x', 300));
        var agent = new AgentInfo("test-agent", "Beschreibung", "/pfad/agent.md");

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["KiKontext:SoftLimitChars"] = "100",
                ["KiKontext:HardLimitChars"] = "150"
            })
            .Build();

        var sut = new EntwicklungsprozessService(
            _aufgabeService,
            _protokollService,
            _gitPluginMock.Object,
            CreatePluginSelectionService(_kiPluginMock.Object),
            _agentPackageServiceMock.Object,
            _arbeitsverzeichnisResolverMock.Object,
            config,
            new Mock<ILogger<EntwicklungsprozessService>>().Object);

        _kiPluginMock.Setup(k => k.StartDevelopmentAsync(
                It.Is<string>(p => p.Contains("Komprimiere den folgenden Projektkontext")),
                It.IsAny<AgentInfo>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns<string, AgentInfo, string, string?, CancellationToken>((_, _, _, _, _) => StreamSingleLine(
                $"## Ziel{Environment.NewLine}{new string('a', 80)}{Environment.NewLine}{Environment.NewLine}" +
                $"## Offene Punkte{Environment.NewLine}{new string('b', 80)}{Environment.NewLine}{Environment.NewLine}" +
                $"## Letzte Entscheidungen{Environment.NewLine}{new string('c', 80)}"));

        // Act
        var act = async () =>
        {
            await foreach (var _ in sut.KiStartenAsync(aufgabe.Id, "Neue Folgeanweisung", agent, null, null, FolgeanweisungsKontextmodus.KontextMitgeben))
            {
            }
        };

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Hard-Limit*");
    }

    [Fact]
    public async Task KiStartenAsync_ShouldWritePreflightErrorContext_WhenCompressionMissesMandatorySections()
    {
        // Arrange
        var repoPath = Path.Combine(Path.GetTempPath(), $"ctx-mandatory-{Guid.NewGuid():N}");
        Directory.CreateDirectory(repoPath);
        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "Komprimierung Pflichtabschnitte", null);
        await _aufgabeService.StartenAsync(aufgabe.Id, "branch", repoPath);
        var contextPath = Path.Combine(repoPath, $"{aufgabe.Id}.copilot.context.md");
        await File.WriteAllTextAsync(contextPath, new string('x', 260));
        var agent = new AgentInfo("test-agent", "Beschreibung", "/pfad/agent.md");

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["KiKontext:SoftLimitChars"] = "100",
                ["KiKontext:HardLimitChars"] = "1000"
            })
            .Build();

        var sut = new EntwicklungsprozessService(
            _aufgabeService,
            _protokollService,
            _gitPluginMock.Object,
            CreatePluginSelectionService(_kiPluginMock.Object),
            _agentPackageServiceMock.Object,
            _arbeitsverzeichnisResolverMock.Object,
            config,
            new Mock<ILogger<EntwicklungsprozessService>>().Object);

        _kiPluginMock.Setup(k => k.StartDevelopmentAsync(
                It.Is<string>(p => p.Contains("Komprimiere den folgenden Projektkontext", StringComparison.Ordinal)),
                It.IsAny<AgentInfo>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns<string, AgentInfo, string, string?, CancellationToken>((_, _, _, _, _) => StreamSingleLine("## Ziel"));

        // Act
        var act = async () =>
        {
            await foreach (var _ in sut.KiStartenAsync(aufgabe.Id, "Neue Folgeanweisung", agent, null, null, FolgeanweisungsKontextmodus.KontextMitgeben))
            {
            }
        };

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Pflichtabschnitte*");
        var written = await File.ReadAllTextAsync(contextPath);
        written.Should().Contain("Status: Fehler");
        written.Should().Contain("Vor dem KI-Start abgebrochen");
    }

    [Fact]
    public async Task KiStartenAsync_ShouldAppendErrorContextEntry_WhenPluginThrowsDuringFollowUp()
    {
        // Arrange
        var repoPath = Path.Combine(Path.GetTempPath(), $"ctx-plugin-error-{Guid.NewGuid():N}");
        Directory.CreateDirectory(repoPath);
        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "Plugin Fehler", null);
        await _aufgabeService.StartenAsync(aufgabe.Id, "branch", repoPath);
        var agent = new AgentInfo("test-agent", "Beschreibung", "/pfad/agent.md");

        _kiPluginMock.Setup(k => k.StartDevelopmentAsync(
                It.IsAny<string>(),
                It.IsAny<AgentInfo>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns<string, AgentInfo, string, string?, CancellationToken>((_, _, _, _, _) => StreamThrows("plugin boom"));

        // Act
        await foreach (var _ in _sut.KiStartenAsync(aufgabe.Id, "Weiter", agent, null, null, FolgeanweisungsKontextmodus.KontextIgnorieren))
        {
        }

        // Assert
        var contextPath = Path.Combine(repoPath, $"{aufgabe.Id}.copilot.context.md");
        var context = await File.ReadAllTextAsync(contextPath);
        context.Should().Contain("Status: Fehler");
        context.Should().Contain("plugin boom");
        var updatedAufgabe = await _aufgabeService.GetByIdAsync(aufgabe.Id);
        updatedAufgabe!.Status.Should().Be(AufgabeStatus.Fehlgeschlagen);
        var protokoll = (await _protokollService.GetByAufgabeAsync(aufgabe.Id)).ToList();
        var kiAntwort = protokoll.Last(p => p.Typ == ProtokollTyp.KiAntwort).Inhalt;
        kiAntwort.Should().MatchRegex(@"^# \d{4}-\d{2}-\d{2}");
        kiAntwort.Should().MatchRegex(@"- RunId: `[0-9a-fA-F-]{36}`");
        kiAntwort.Should().Contain("## Schritt 1");
        kiAntwort.Should().Contain("Fehler: plugin boom");
    }

    [Fact]
    public async Task KiStartenAsync_ShouldPublishCompletionEvent_WhenRunSucceeds()
    {
        // Arrange
        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "Benachrichtigung Erfolg", null);
        await _aufgabeService.StartenAsync(aufgabe.Id, "branch", "/repo");
        var agent = new AgentInfo("test-agent", "Beschreibung", "/pfad/agent.md");
        _kiPluginMock.Setup(k => k.StartDevelopmentAsync(
                It.IsAny<string>(),
                It.IsAny<AgentInfo>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns<string, AgentInfo, string, string?, CancellationToken>((_, _, _, _, _) => StreamSingleLine("ok"));
        var hub = new KiAufgabenBenachrichtigungsHub(NullLogger<KiAufgabenBenachrichtigungsHub>.Instance);
        var events = new List<KiAufgabenAbschlussEreignis>();
        using var _ = hub.Subscribe(e =>
        {
            events.Add(e);
            return Task.CompletedTask;
        });
        var sut = new EntwicklungsprozessService(
            _aufgabeService,
            _protokollService,
            _gitPluginMock.Object,
            CreatePluginSelectionService(_kiPluginMock.Object),
            _agentPackageServiceMock.Object,
            _arbeitsverzeichnisResolverMock.Object,
            hub,
            new ConfigurationBuilder().Build(),
            new Mock<ILogger<EntwicklungsprozessService>>().Object);

        // Act
        await foreach (var _line in sut.KiStartenAsync(aufgabe.Id, "prompt", agent))
        {
        }

        // Assert
        events.Should().ContainSingle();
        events[0].AufgabeId.Should().Be(aufgabe.Id);
        events[0].Aufgabentitel.Should().Be(aufgabe.Titel);
        events[0].AbschlussStatus.Should().Be(AufgabeStatus.InBearbeitung);
    }

    [Fact]
    public async Task KiStartenAsync_ShouldPublishCompletionEvent_WhenRunFails()
    {
        // Arrange
        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "Benachrichtigung Fehler", null);
        await _aufgabeService.StartenAsync(aufgabe.Id, "branch", "/repo");
        var agent = new AgentInfo("test-agent", "Beschreibung", "/pfad/agent.md");
        _kiPluginMock.Setup(k => k.StartDevelopmentAsync(
                It.IsAny<string>(),
                It.IsAny<AgentInfo>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns<string, AgentInfo, string, string?, CancellationToken>((_, _, _, _, _) => StreamThrows("boom"));
        var hub = new KiAufgabenBenachrichtigungsHub(NullLogger<KiAufgabenBenachrichtigungsHub>.Instance);
        var events = new List<KiAufgabenAbschlussEreignis>();
        using var _ = hub.Subscribe(e =>
        {
            events.Add(e);
            return Task.CompletedTask;
        });
        var sut = new EntwicklungsprozessService(
            _aufgabeService,
            _protokollService,
            _gitPluginMock.Object,
            CreatePluginSelectionService(_kiPluginMock.Object),
            _agentPackageServiceMock.Object,
            _arbeitsverzeichnisResolverMock.Object,
            hub,
            new ConfigurationBuilder().Build(),
            new Mock<ILogger<EntwicklungsprozessService>>().Object);

        // Act
        await foreach (var _line in sut.KiStartenAsync(aufgabe.Id, "prompt", agent))
        {
        }

        // Assert
        events.Should().ContainSingle();
        events[0].AufgabeId.Should().Be(aufgabe.Id);
        events[0].AbschlussStatus.Should().Be(AufgabeStatus.Fehlgeschlagen);
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

    [Fact]
    public async Task ProzessStartenAsync_ShouldPreferExplicitSelectedKiPluginPrefix_OverStoredTaskPrefix()
    {
        // Arrange
        var selectedPluginMock = new Mock<IKiPlugin>();
        selectedPluginMock.SetupGet(plugin => plugin.PluginName).Returns("Selected KI");
        selectedPluginMock.SetupGet(plugin => plugin.PluginPrefix).Returns("Softwareschmiede.KiA");
        selectedPluginMock.SetupGet(plugin => plugin.PluginType).Returns(PluginType.DevelopmentAutomation);
        selectedPluginMock.Setup(plugin => plugin.GetSettingGroups()).Returns([]);
        selectedPluginMock.Setup(plugin => plugin.IsAgentPackageCompatibleAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        selectedPluginMock.Setup(plugin => plugin.DeployAgentPackageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var storedPluginMock = new Mock<IKiPlugin>();
        storedPluginMock.SetupGet(plugin => plugin.PluginName).Returns("Stored KI");
        storedPluginMock.SetupGet(plugin => plugin.PluginPrefix).Returns("Softwareschmiede.KiB");
        storedPluginMock.SetupGet(plugin => plugin.PluginType).Returns(PluginType.DevelopmentAutomation);
        storedPluginMock.Setup(plugin => plugin.GetSettingGroups()).Returns([]);
        storedPluginMock.Setup(plugin => plugin.IsAgentPackageCompatibleAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        storedPluginMock.Setup(plugin => plugin.DeployAgentPackageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = new EntwicklungsprozessService(
            _aufgabeService,
            _protokollService,
            _gitPluginMock.Object,
            CreatePluginSelectionService(selectedPluginMock.Object, storedPluginMock.Object),
            _agentPackageServiceMock.Object,
            _arbeitsverzeichnisResolverMock.Object,
            new ConfigurationBuilder().Build(),
            new Mock<ILogger<EntwicklungsprozessService>>().Object);

        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "Explicit KI Prefix beim Start", null);
        await _aufgabeService.UpdateAsync(
            aufgabe.Id,
            aufgabe.Titel,
            aufgabe.AnforderungsBeschreibung,
            "mein-paket",
            null,
            "Softwareschmiede.KiB");

        _agentPackageServiceMock.Setup(service => service.GetPackageAsync("mein-paket", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentPackageInfo("mein-paket", "/pfad/zum/paket", [], []));
        _gitPluginMock.Setup(git => git.CloneRepositoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _gitPluginMock.Setup(git => git.CreateBranchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await sut.ProzessStartenAsync(
            aufgabe.Id,
            "https://github.com/test/repo",
            selectedKiPluginPrefix: "Softwareschmiede.KiA");

        // Assert
        selectedPluginMock.Verify(plugin => plugin.DeployAgentPackageAsync("/pfad/zum/paket", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        storedPluginMock.Verify(plugin => plugin.DeployAgentPackageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
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
            _agentPackageServiceMock.Object,
            _arbeitsverzeichnisResolverMock.Object,
            null,
            new ConfigurationBuilder().Build(),
            new Mock<ILogger<EntwicklungsprozessService>>().Object);

        // Act
        var act = () => sut.ProzessStartenAsync(aufgabe.Id, "https://github.com/test/unknown");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*kein eindeutiges Repository*");
        _gitPluginMock.Verify(git => git.CloneRepositoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>TestsAusfuehrenAsync speichert bei Erfolg Test-Ergebnisprotokolle.</summary>
    [Fact]
    public async Task TestsAusfuehrenAsync_ShouldPersistSuccessfulTestSummary()
    {
        // Arrange
        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "Tests erfolgreich", null);
        await _aufgabeService.StartenAsync(aufgabe.Id, "branch", "/repo");
        var expected = new TestResult(true, [new TestErgebnisInfo("Passed UnitTest", TestStatus.Bestanden, null, TimeSpan.Zero)]);
        _kiPluginMock.Setup(k => k.RunTestsAsync("/repo", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Act
        var result = await _sut.TestsAusfuehrenAsync(aufgabe.Id);

        // Assert
        result.Should().BeEquivalentTo(expected);
        var protokoll = (await _protokollService.GetByAufgabeAsync(aufgabe.Id)).ToList();
        var testEintrag = protokoll.Single(p => p.Typ == ProtokollTyp.TestErgebnis);
        testEintrag.Inhalt.Should().Be("Alle 1 Tests bestanden.");
        testEintrag.TestErgebnisse.Should().ContainSingle(e => e.TestName == "Passed UnitTest" && e.Status == TestStatus.Bestanden);
    }

    /// <summary>TestsAusfuehrenAsync speichert bei Fehlern die Fehlerschwelle korrekt.</summary>
    [Fact]
    public async Task TestsAusfuehrenAsync_ShouldPersistFailureSummary_WhenTestsFail()
    {
        // Arrange
        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "Tests fehlschlagen", null);
        await _aufgabeService.StartenAsync(aufgabe.Id, "branch", "/repo");
        var expected = new TestResult(false,
        [
            new TestErgebnisInfo("Passed UnitTest", TestStatus.Bestanden, null, TimeSpan.Zero),
            new TestErgebnisInfo("Failed UnitTest", TestStatus.Fehlgeschlagen, "boom", TimeSpan.Zero)
        ]);
        _kiPluginMock.Setup(k => k.RunTestsAsync("/repo", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Act
        var result = await _sut.TestsAusfuehrenAsync(aufgabe.Id);

        // Assert
        result.Should().BeEquivalentTo(expected);
        var protokoll = (await _protokollService.GetByAufgabeAsync(aufgabe.Id)).ToList();
        var testEintrag = protokoll.Single(p => p.Typ == ProtokollTyp.TestErgebnis);
        testEintrag.Inhalt.Should().Be("1 von 2 Tests fehlgeschlagen.");
        testEintrag.TestErgebnisse.Should().HaveCount(2);
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
            _agentPackageServiceMock.Object,
            _arbeitsverzeichnisResolverMock.Object,
            new ConfigurationBuilder().Build(),
            new Mock<ILogger<EntwicklungsprozessService>>().Object);

        // Act
        var result = (await sut.GetRemoteBranchesAsync("https://github.com/test/repo", "  softwaRESchmiede.githUB  ")).ToArray();

        // Assert
        result.Should().BeEquivalentTo(["develop", "release/1.0"]);
        selectedGitPluginMock.Verify(plugin => plugin.GetRemoteBranchesAsync("https://github.com/test/repo", It.IsAny<CancellationToken>()), Times.Once);
        defaultGitPluginMock.Verify(plugin => plugin.GetRemoteBranchesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static async IAsyncEnumerable<string> StreamSingleLine(string line)
    {
        yield return line;
        await Task.CompletedTask;
    }

    private static async IAsyncEnumerable<string> StreamLines(params string[] lines)
    {
        foreach (var line in lines)
        {
            yield return line;
        }

        await Task.CompletedTask;
    }

    private static async IAsyncEnumerable<string> StreamThrows(string message)
    {
        await Task.Yield();
        throw new InvalidOperationException(message);
#pragma warning disable CS0162
        yield break;
#pragma warning restore CS0162
    }

    private sealed class TestCliKiPluginBase : CliKiPluginBase
    {
        private readonly IAsyncEnumerable<string> _stream;

        public TestCliKiPluginBase(string providerDateiPraefix, IAsyncEnumerable<string> stream)
        {
            ProviderDateiPraefix = providerDateiPraefix;
            _stream = stream;
        }

        public override string ProviderDateiPraefix { get; }
        public string? LastPrompt { get; private set; }
        public override string PluginName => "Test";
        public override string PluginPrefix => "Softwareschmiede.Test";
        public override PluginType PluginType => PluginType.DevelopmentAutomation;
        public override IReadOnlyList<PluginSettingGroup> GetSettingGroups() => [];
        public override Task<IEnumerable<AgentInfo>> GetAvailableAgentsAsync(string agentPackagePath, CancellationToken ct = default) => Task.FromResult<IEnumerable<AgentInfo>>([]);
        public override Task<bool> IsAgentPackageCompatibleAsync(string agentPackagePath, CancellationToken ct = default) => Task.FromResult(true);
        public override Task DeployAgentPackageAsync(string agentPackagePath, string localRepoPath, CancellationToken ct = default) => Task.CompletedTask;
        public override IAsyncEnumerable<string> StartDevelopmentAsync(string prompt, AgentInfo agent, string localRepoPath, string? model = null, CancellationToken ct = default)
        {
            LastPrompt = prompt;
            return _stream;
        }
        public override Task<TestResult> RunTestsAsync(string localRepoPath, CancellationToken ct = default) => Task.FromResult(new TestResult(true, []));
        public override Task<bool> CheckHealthAsync(CancellationToken ct = default) => Task.FromResult(true);
    }
}
