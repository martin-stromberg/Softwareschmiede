using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Tests.Application.Services;

public sealed class RepositoryStartskriptServiceTests : IDisposable
{
    private readonly string _repositoryRootPath = Path.Combine(Path.GetTempPath(), $"repo-start-script-{Guid.NewGuid():N}");
    private readonly Mock<ICliRunner> _cliRunnerMock = new();

    public RepositoryStartskriptServiceTests()
    {
        Directory.CreateDirectory(_repositoryRootPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_repositoryRootPath))
        {
            Directory.Delete(_repositoryRootPath, recursive: true);
        }
    }

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

    [Fact]
    public async Task RunAsync_ShouldThrow_WhenScriptPathEscapesRepositoryRoot()
    {
        var sut = CreateSut();
        var configuration = CreateConfig();
        configuration.StartScriptRelativePath = "..\\outside.ps1";

        var act = () => sut.RunAsync(_repositoryRootPath, configuration);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*innerhalb des Repositorys*");
    }

    [Fact]
    public async Task RunAsync_ShouldPassOnlyScriptArgumentsWithoutPortContract_WhenScriptExecutionSucceeds()
    {
        var scriptPath = CreateScript("scripts/start.ps1");
        var configuration = CreateConfig();
        configuration.StartScriptRelativePath = Path.GetRelativePath(_repositoryRootPath, scriptPath);

        string? capturedCommand = null;
        IReadOnlyList<string>? capturedArgs = null;
        IDictionary<string, string>? capturedEnvironment = null;
        string? capturedWorkingDirectory = null;

        _cliRunnerMock
            .Setup(runner => runner.RunAsync(
                It.IsAny<string>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<string?>(),
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, IEnumerable<string>, string?, IDictionary<string, string>?, CancellationToken>((command, args, workingDirectory, environment, _) =>
            {
                capturedCommand = command;
                capturedArgs = args.ToList();
                capturedEnvironment = environment;
                capturedWorkingDirectory = workingDirectory;
            })
            .ReturnsAsync(new CliResult(0, "ok", string.Empty));

        var sut = CreateSut();
        await sut.RunAsync(_repositoryRootPath, configuration);

        capturedCommand.Should().Be("powershell.exe");
        capturedWorkingDirectory.Should().Be(_repositoryRootPath);
        capturedEnvironment.Should().BeNull();
        capturedArgs.Should().Contain("-File").And.Contain(scriptPath);
        capturedArgs.Should().NotContain("-Port");
        capturedArgs.Should().NotContain("-RepositoryPath");
    }

    [Fact]
    public async Task RunAsync_ShouldThrow_WhenCliExecutionFails()
    {
        var scriptPath = CreateScript("scripts/start.ps1");
        var configuration = CreateConfig();
        configuration.StartScriptRelativePath = Path.GetRelativePath(_repositoryRootPath, scriptPath);

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

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*fehlgeschlagen*");
    }

    private RepositoryStartskriptService CreateSut()
        => new(
            _cliRunnerMock.Object,
            NullLogger<RepositoryStartskriptService>.Instance);

    private static RepositoryStartKonfiguration CreateConfig()
        => new()
        {
            Id = Guid.NewGuid(),
            GitRepositoryId = Guid.NewGuid(),
            Aktiv = true,
            StartScriptRelativePath = "start.ps1"
        };

    private string CreateScript(string relativePath)
    {
        var fullPath = Path.Combine(_repositoryRootPath, relativePath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(fullPath, "Write-Host 'hello'");
        return Path.GetFullPath(fullPath);
    }
}
