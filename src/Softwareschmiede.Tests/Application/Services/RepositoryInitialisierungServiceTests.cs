using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>RepositoryInitialisierungServiceTests.</summary>
public sealed class RepositoryInitialisierungServiceTests : IDisposable
{
    private readonly string _repositoryRootPath = Path.Combine(Path.GetTempPath(), $"repo-init-script-{Guid.NewGuid():N}");
    private readonly Mock<ICliRunner> _cliRunnerMock = new();

    /// <summary>RepositoryInitialisierungServiceTests.</summary>
    public RepositoryInitialisierungServiceTests()
    {
        Directory.CreateDirectory(_repositoryRootPath);
    }

    /// <summary>Dispose.</summary>
    public void Dispose()
    {
        if (Directory.Exists(_repositoryRootPath))
        {
            Directory.Delete(_repositoryRootPath, recursive: true);
        }
    }

    /// <summary><summary>RunAsync_ShouldSucceed_WhenInitializationScriptExecutes.</summary>.</summary>
    [Fact]
    public async Task RunAsync_ShouldSucceed_WhenInitializationScriptExecutes()
    {
        var scriptPath = CreateScript("scripts/init.ps1");
        var configuration = CreateConfig();
        configuration.InitialisierungsskriptRelativePath = Path.GetRelativePath(_repositoryRootPath, scriptPath);

        _cliRunnerMock
            .Setup(runner => runner.RunAsync(
                It.IsAny<string>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<string?>(),
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, "ok", string.Empty));

        var sut = CreateSut();
        var act = () => sut.RunAsync(_repositoryRootPath, configuration);

        await act.Should().NotThrowAsync();
        _cliRunnerMock.Verify(
            runner => runner.RunAsync(
                "powershell.exe",
                It.IsAny<IEnumerable<string>>(),
                _repositoryRootPath,
                null,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary><summary>RunAsync_ShouldLogWarning_WhenInitializationScriptFails.</summary>.</summary>
    [Fact]
    public async Task RunAsync_ShouldLogWarning_WhenInitializationScriptFails()
    {
        var scriptPath = CreateScript("scripts/init.ps1");
        var configuration = CreateConfig();
        configuration.InitialisierungsskriptRelativePath = Path.GetRelativePath(_repositoryRootPath, scriptPath);

        _cliRunnerMock
            .Setup(runner => runner.RunAsync(
                It.IsAny<string>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<string?>(),
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(1, string.Empty, "failed"));

        var sut = CreateSut();
        var act = () => sut.RunAsync(_repositoryRootPath, configuration);

        // Der Service selbst wirft bei Fehlschlag weiterhin eine Exception (Symmetrie zu
        // RepositoryStartskriptService). Das Nicht-Blockieren der Aufgabe erfolgt im aufrufenden
        // EntwicklungsprozessService, der diese Exception abfängt und nur als Warning loggt
        // (siehe EntwicklungsprozessServiceTests_Initialisierungsskript).
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*fehlgeschlagen*");
    }

    /// <summary><summary>RunAsync_ShouldThrow_WhenPathTraversalAttempted.</summary>.</summary>
    [Fact]
    public async Task RunAsync_ShouldThrow_WhenPathTraversalAttempted()
    {
        var sut = CreateSut();
        var configuration = CreateConfig();
        configuration.InitialisierungsskriptRelativePath = "..\\outside.ps1";

        var act = () => sut.RunAsync(_repositoryRootPath, configuration);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*innerhalb des Repositorys*");
    }

    /// <summary><summary>RunAsync_ShouldSkipExecution_WhenConfigurationIsInactive.</summary>.</summary>
    [Fact]
    public async Task RunAsync_ShouldSkipExecution_WhenConfigurationIsInactive()
    {
        var sut = CreateSut();
        var configuration = CreateConfig();
        configuration.Aktiv = false;

        await sut.RunAsync(_repositoryRootPath, configuration);

        _cliRunnerMock.Verify(
            runner => runner.RunAsync(
                It.IsAny<string>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<string?>(),
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary><summary>RunAsync_ShouldThrow_WhenScriptFileNotFound.</summary>.</summary>
    [Fact]
    public async Task RunAsync_ShouldThrow_WhenScriptFileNotFound()
    {
        var sut = CreateSut();
        var configuration = CreateConfig();
        configuration.InitialisierungsskriptRelativePath = "does-not-exist.ps1";

        var act = () => sut.RunAsync(_repositoryRootPath, configuration);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*nicht gefunden*");
    }

    /// <summary><summary>ResolveScriptPath_ShouldValidatePathBoundary.</summary>.</summary>
    [Fact]
    public async Task ResolveScriptPath_ShouldValidatePathBoundary()
    {
        var scriptPath = CreateScript("nested/init.ps1");
        var configuration = CreateConfig();
        configuration.InitialisierungsskriptRelativePath = Path.GetRelativePath(_repositoryRootPath, scriptPath);

        _cliRunnerMock
            .Setup(runner => runner.RunAsync(
                It.IsAny<string>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<string?>(),
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, IEnumerable<string>, string?, IDictionary<string, string>?, CancellationToken>((_, args, _, _, _) =>
            {
                args.Should().Contain("-File").And.Contain(scriptPath);
            })
            .ReturnsAsync(new CliResult(0, "ok", string.Empty));

        var sut = CreateSut();
        await sut.RunAsync(_repositoryRootPath, configuration);

        _cliRunnerMock.VerifyAll();
    }

    private RepositoryInitialisierungService CreateSut()
        => new(
            _cliRunnerMock.Object,
            NullLogger<RepositoryInitialisierungService>.Instance);

    private static RepositoryInitialisierungKonfiguration CreateConfig()
        => new()
        {
            Id = Guid.NewGuid(),
            GitRepositoryId = Guid.NewGuid(),
            Aktiv = true,
            InitialisierungsskriptRelativePath = "init.ps1"
        };

    private string CreateScript(string relativePath)
    {
        var fullPath = Path.Combine(_repositoryRootPath, relativePath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(fullPath, "Write-Host 'init'");
        return Path.GetFullPath(fullPath);
    }
}
