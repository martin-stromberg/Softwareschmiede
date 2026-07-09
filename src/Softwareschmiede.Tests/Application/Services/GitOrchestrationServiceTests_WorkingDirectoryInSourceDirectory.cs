using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Infrastructure.Plugins;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>
/// Regressionstest für Issue #98 (zweiter Nacharbeits-Zyklus):
/// <see cref="GitOrchestrationService.ValidateWorkingDirectoryAfterCloneAsync"/> muss ein konfiguriertes
/// relatives Arbeitsverzeichnis auch dann korrekt validieren, wenn <see cref="LocalDirectoryPlugin"/> im
/// Workspace-Modus <c>InSourceDirectory</c> betrieben wird und der übergebene Klon-Pfad nur eine Pointer-Datei
/// auf das tatsächliche Quellverzeichnis enthält.
/// </summary>
public sealed class GitOrchestrationServiceTests_WorkingDirectoryInSourceDirectory : IDisposable
{
    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiededDbContext _db;
    private readonly GitOrchestrationService _sut;
    private readonly string _sourceDir;
    private readonly string _clonePath;
    private readonly LocalDirectoryPlugin _gitPlugin;

    /// <summary>GitOrchestrationServiceTests_WorkingDirectoryInSourceDirectory.</summary>
    public GitOrchestrationServiceTests_WorkingDirectoryInSourceDirectory()
    {
        _db = TestDbContextFactory.Create();
        var aufgabeService = new AufgabeService(_db, new Mock<ILogger<AufgabeService>>().Object);
        var projektService = new ProjektService(_db, new Mock<ILogger<ProjektService>>().Object);
        var protokollService = new ProtokollService(_db, new Mock<ILogger<ProtokollService>>().Object);

        var defaultPluginMock = new Mock<IGitPlugin>();
        defaultPluginMock.SetupGet(p => p.PluginName).Returns("Mock Git");
        defaultPluginMock.SetupGet(p => p.PluginPrefix).Returns("Mock.Git");
        defaultPluginMock.SetupGet(p => p.PluginType).Returns(PluginType.SourceCodeManagement);
        defaultPluginMock.Setup(p => p.GetSettingGroups()).Returns([]);

        var pluginManagerMock = new Mock<IPluginManager>();
        pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins()).Returns([defaultPluginMock.Object]);
        pluginManagerMock.Setup(m => m.GetDefaultSourceCodeManagementPlugin()).Returns(defaultPluginMock.Object);
        pluginManagerMock.Setup(m => m.GetDevelopmentAutomationPlugins()).Returns([]);
        pluginManagerMock.Setup(m => m.GetDefaultDevelopmentAutomationPlugin()).Returns(new Mock<IKiPlugin>().Object);
        var pluginDefaultSettings = new PluginDefaultSettingsService(_db, new Mock<ILogger<PluginDefaultSettingsService>>().Object);
        var pluginSelectionService = new PluginSelectionService(
            pluginManagerMock.Object,
            pluginDefaultSettings,
            new Mock<ILogger<PluginSelectionService>>().Object);

        _sut = new GitOrchestrationService(
            aufgabeService,
            projektService,
            protokollService,
            defaultPluginMock.Object,
            pluginSelectionService,
            new Mock<ILogger<GitOrchestrationService>>().Object);

        _sourceDir = Directory.CreateTempSubdirectory("swst-insrc-validate-source-").FullName;
        Directory.CreateDirectory(Path.Combine(_sourceDir, "backend"));
        _clonePath = Path.Combine(Path.GetTempPath(), "swst-insrc-validate-clone-" + Guid.NewGuid());

        var cliRunnerMock = new Mock<ICliRunner>();
        cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(args => args.SequenceEqual(new[] { "rev-parse", "--is-inside-work-tree" })),
                _sourceDir,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, "true", string.Empty));
        cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(args => args.SequenceEqual(new[] { "status", "--porcelain" })),
                _sourceDir,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, string.Empty, string.Empty));

        var credentialStoreMock = new Mock<ICredentialStore>();
        credentialStoreMock.Setup(s => s.GetCredential("LocalDirectoryPlugin.WorkspaceMode")).Returns("InSourceDirectory");

        _gitPlugin = new LocalDirectoryPlugin(cliRunnerMock.Object, credentialStoreMock.Object, NullLogger<LocalDirectoryPlugin>.Instance);
    }

    /// <summary>Dispose.</summary>
    public void Dispose()
    {
        _db.Dispose();
        if (Directory.Exists(_sourceDir))
        {
            Directory.Delete(_sourceDir, recursive: true);
        }

        if (Directory.Exists(_clonePath))
        {
            Directory.Delete(_clonePath, recursive: true);
        }
    }

    /// <summary>
    /// ValidateWorkingDirectoryAfterCloneAsync validiert das konfigurierte Unterverzeichnis relativ zum
    /// tatsächlichen Quellverzeichnis, wenn ein IGitPlugin im InSourceDirectory-Modus übergeben wird — ohne
    /// Plugin würde die Validierung fälschlich fehlschlagen, da der Klon-Pfad selbst nur eine Pointer-Datei
    /// enthält.
    /// </summary>
    [Fact]
    public async Task ValidateWorkingDirectoryAfterCloneAsync_ShouldSucceed_WhenGitPluginResolvesInSourceDirectoryPath()
    {
        // Arrange
        await _gitPlugin.CloneRepositoryAsync(_sourceDir, _clonePath);
        var startConfig = new RepositoryStartKonfiguration { WorkingDirectoryRelativePath = "backend" };

        // Act
        var act = () => _sut.ValidateWorkingDirectoryAfterCloneAsync(_clonePath, startConfig, _gitPlugin);

        // Assert
        await act.Should().NotThrowAsync();
    }

    /// <summary>
    /// Ohne übergebenes IGitPlugin verhält sich die Validierung wie zuvor und schlägt fehl, weil der Klon-Pfad
    /// im InSourceDirectory-Modus kein "backend"-Unterverzeichnis enthält (nur die Pointer-Datei).
    /// </summary>
    [Fact]
    public async Task ValidateWorkingDirectoryAfterCloneAsync_ShouldThrow_WhenGitPluginNotPassed()
    {
        // Arrange
        await _gitPlugin.CloneRepositoryAsync(_sourceDir, _clonePath);
        var startConfig = new RepositoryStartKonfiguration { WorkingDirectoryRelativePath = "backend" };

        // Act
        var act = () => _sut.ValidateWorkingDirectoryAfterCloneAsync(_clonePath, startConfig);

        // Assert
        await act.Should().ThrowAsync<DirectoryNotFoundException>();
    }
}
