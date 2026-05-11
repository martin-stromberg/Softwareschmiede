using FluentAssertions;
using Microsoft.Extensions.Configuration;
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
        await foreach (var _ in _sut.KiStartenAsync(aufgabe.Id, "Neue Folgeanweisung", agent, null, FolgeanweisungsKontextmodus.KontextIgnorieren))
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
        await foreach (var _ in _sut.KiStartenAsync(aufgabe.Id, "Neue Folgeanweisung", agent, null, FolgeanweisungsKontextmodus.KontextMitgeben))
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
        await foreach (var _ in _sut.KiStartenAsync(aufgabe.Id, "Nur Prompt", agent, null, FolgeanweisungsKontextmodus.KontextMitgeben))
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
        await foreach (var _ in _sut.KiStartenAsync(aufgabe.Id, "Neu anfangen", agent, null, FolgeanweisungsKontextmodus.KontextNeuBeginnen))
        {
        }

        // Assert
        var updated = await File.ReadAllTextAsync(contextPath);
        updated.Should().NotContain("Alter Verlauf");
        updated.Should().Contain("Neu anfangen");
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
            _kiPluginMock.Object,
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
            await foreach (var _ in sut.KiStartenAsync(aufgabe.Id, "Neue Folgeanweisung", agent, null, FolgeanweisungsKontextmodus.KontextMitgeben))
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
            _kiPluginMock.Object,
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
            await foreach (var _ in sut.KiStartenAsync(aufgabe.Id, "Neue Folgeanweisung", agent, null, FolgeanweisungsKontextmodus.KontextMitgeben))
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
        await foreach (var _ in _sut.KiStartenAsync(aufgabe.Id, "Weiter", agent, null, FolgeanweisungsKontextmodus.KontextIgnorieren))
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
}
