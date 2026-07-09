using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Infrastructure.Plugins;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>
/// Regressionstest für Issue #98 (zweiter Nacharbeits-Zyklus): <see cref="KiAusfuehrungsService"/> muss ein
/// konfiguriertes relatives Arbeitsverzeichnis auch dann korrekt auflösen, wenn <see cref="LocalDirectoryPlugin"/>
/// im Workspace-Modus <c>InSourceDirectory</c> betrieben wird. In diesem Modus enthält der an
/// <see cref="KiAusfuehrungsService.StartCliAsync"/> übergebene "Klon-Pfad" nur eine Pointer-Datei auf das
/// tatsächliche Quellverzeichnis — die eigentliche Verzeichnisstruktur (z. B. ein Unterverzeichnis "backend")
/// befindet sich dort, nicht im Klon-Pfad selbst. Vor dem Fix wurde der Klon-Pfad unverändert mit dem relativen
/// Pfad kombiniert, was zu einer <see cref="DirectoryNotFoundException"/> führte, obwohl "backend" im
/// tatsächlichen Repository existierte.
/// </summary>
public sealed class KiAusfuehrungsServiceTests_WorkingDirectory_InSourceDirectory : IDisposable
{
    private readonly string _sourceDir;
    private readonly string _clonePath;
    private readonly LocalDirectoryPlugin _gitPlugin;
    private readonly KiAusfuehrungsService _kiAusfuehrungsService;

    /// <summary>KiAusfuehrungsServiceTests_WorkingDirectory_InSourceDirectory.</summary>
    public KiAusfuehrungsServiceTests_WorkingDirectory_InSourceDirectory()
    {
        _sourceDir = Directory.CreateTempSubdirectory("swst-insrc-source-").FullName;
        Directory.CreateDirectory(Path.Combine(_sourceDir, "backend"));

        _clonePath = Path.Combine(Path.GetTempPath(), "swst-insrc-clone-" + Guid.NewGuid());

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

        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        _kiAusfuehrungsService = new KiAusfuehrungsService(NullLogger<KiAusfuehrungsService>.Instance, NullLoggerFactory.Instance, scopeFactoryMock.Object);
    }

    /// <summary>Dispose.</summary>
    public void Dispose()
    {
        _kiAusfuehrungsService.Dispose();
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
    /// StartCliAsync löst das konfigurierte Unterverzeichnis relativ zum tatsächlichen Quellverzeichnis auf
    /// (nicht relativ zum Pointer-Verzeichnis), wenn das übergebene <see cref="IGitPlugin"/> im
    /// InSourceDirectory-Modus arbeitet.
    /// </summary>
    [Fact]
    public async Task StartCliAsync_ShouldResolveConfiguredWorkingDirectory_ViaSourcePath_WhenPluginIsInSourceDirectoryMode()
    {
        // Arrange
        await _gitPlugin.CloneRepositoryAsync(_sourceDir, _clonePath);

        var startConfig = new RepositoryStartKonfiguration { WorkingDirectoryRelativePath = "backend" };

        string? usedPath = null;
        var kiPluginMock = new Mock<IKiPlugin>();
        kiPluginMock.Setup(p => p.StartCliAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback<string, string?, CancellationToken>((path, _, _) => usedPath = path)
            .ReturnsAsync(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c exit 0",
                UseShellExecute = false,
                CreateNoWindow = true,
            });

        // Act
        await _kiAusfuehrungsService.StartCliAsync(
            Guid.NewGuid(),
            kiPluginMock.Object,
            _clonePath,
            startConfig: startConfig,
            gitPlugin: _gitPlugin);

        // Assert
        usedPath.Should().Be(Path.GetFullPath(Path.Combine(_sourceDir, "backend")));
    }

    /// <summary>
    /// Ohne übergebenes <see cref="IGitPlugin"/> verhält sich StartCliAsync wie zuvor: Der Klon-Pfad wird
    /// unverändert mit dem relativen Pfad kombiniert. Da im Klon-Pfad selbst kein "backend"-Verzeichnis
    /// existiert (InSourceDirectory-Modus schreibt dort nur die Pointer-Datei), schlägt die Auflösung fehl.
    /// Dieser Test dokumentiert den ursprünglichen Bug als Kontrastfolie zum Fix.
    /// </summary>
    [Fact]
    public async Task StartCliAsync_ShouldThrow_WhenGitPluginNotPassed_EvenThoughSourceHasWorkingDirectory()
    {
        // Arrange
        await _gitPlugin.CloneRepositoryAsync(_sourceDir, _clonePath);

        var startConfig = new RepositoryStartKonfiguration { WorkingDirectoryRelativePath = "backend" };

        var kiPluginMock = new Mock<IKiPlugin>();
        kiPluginMock.Setup(p => p.StartCliAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c exit 0",
                UseShellExecute = false,
                CreateNoWindow = true,
            });

        // Act
        var act = () => _kiAusfuehrungsService.StartCliAsync(
            Guid.NewGuid(),
            kiPluginMock.Object,
            _clonePath,
            startConfig: startConfig);

        // Assert
        await act.Should().ThrowAsync<DirectoryNotFoundException>();
    }
}
