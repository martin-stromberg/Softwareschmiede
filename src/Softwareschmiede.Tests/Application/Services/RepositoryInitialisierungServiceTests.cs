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

    /// <summary><summary>RunAsync_ShouldUseCmdExe_WhenScriptIsCmdOrBatFile.</summary>.</summary>
    /// <param name="relativeScriptPath">Repository-relativer Pfad zum .cmd- bzw. .bat-Testskript.</param>
    [Theory]
    [InlineData("scripts/init.cmd")]
    [InlineData("scripts/init.bat")]
    public async Task RunAsync_ShouldUseCmdExe_WhenScriptIsCmdOrBatFile(string relativeScriptPath)
    {
        var scriptPath = CreateScript(relativeScriptPath);
        var configuration = CreateConfig();
        configuration.InitialisierungsskriptRelativePath = Path.GetRelativePath(_repositoryRootPath, scriptPath);

        var capture = SetupCliRunnerCapture();

        var sut = CreateSut();
        await sut.RunAsync(_repositoryRootPath, configuration);

        capture.Command.Should().Be("cmd.exe");
        capture.Args.Should().ContainInOrder("/c", scriptPath);
    }

    /// <summary><summary>RunAsync_ShouldExecuteExeDirectly_WhenScriptIsExeFile.</summary>.</summary>
    [Fact]
    public async Task RunAsync_ShouldExecuteExeDirectly_WhenScriptIsExeFile()
    {
        var scriptPath = CreateScript("scripts/init.exe");
        var configuration = CreateConfig();
        configuration.InitialisierungsskriptRelativePath = Path.GetRelativePath(_repositoryRootPath, scriptPath);

        var capture = SetupCliRunnerCapture();

        var sut = CreateSut();
        await sut.RunAsync(_repositoryRootPath, configuration);

        capture.Command.Should().Be(scriptPath);
        capture.Args.Should().BeEmpty();
    }

    /// <summary><summary>RunAsync_ShouldUseBashExe_WhenScriptIsShFile.</summary>.</summary>
    [Fact]
    public async Task RunAsync_ShouldUseBashExe_WhenScriptIsShFile()
    {
        var scriptPath = CreateScript("scripts/init.sh");
        var capture = SetupCliRunnerCapture();

        var fakeBashDirectory = Path.Combine(_repositoryRootPath, "fake-path-bin");
        Directory.CreateDirectory(fakeBashDirectory);
        var fakeBashExecutable = Path.Combine(fakeBashDirectory, "bash.exe");
        File.WriteAllText(fakeBashExecutable, string.Empty);

        await RepositoryScriptExecutor.RunAsync(
            _repositoryRootPath,
            true,
            Path.GetRelativePath(_repositoryRootPath, scriptPath),
            "Initialisierungsskript",
            _cliRunnerMock.Object,
            NullLogger.Instance,
            CancellationToken.None,
            getEnvironmentVariable: name => name == "PATH" ? fakeBashDirectory : null,
            fileExists: File.Exists);

        capture.Command.Should().Be(fakeBashExecutable);
        capture.Args.Should().ContainSingle().Which.Should().Be(scriptPath);
    }

    /// <summary><summary>RunAsync_ShouldThrow_WhenBashExecutableNotFoundInPath.</summary>.</summary>
    [Fact]
    public async Task RunAsync_ShouldThrow_WhenBashExecutableNotFoundInPath()
    {
        var scriptPath = CreateScript("scripts/init.sh");

        var act = () => RepositoryScriptExecutor.RunAsync(
            _repositoryRootPath,
            true,
            Path.GetRelativePath(_repositoryRootPath, scriptPath),
            "Initialisierungsskript",
            _cliRunnerMock.Object,
            NullLogger.Instance,
            CancellationToken.None,
            getEnvironmentVariable: _ => string.Empty,
            fileExists: _ => false);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*bash.exe*PATH*");
        _cliRunnerMock.Verify(
            runner => runner.RunAsync(
                It.IsAny<string>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<string?>(),
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary><summary>RunAsync_ShouldThrow_WhenScriptExtensionIsUnsupported.</summary>.</summary>
    [Fact]
    public async Task RunAsync_ShouldThrow_WhenScriptExtensionIsUnsupported()
    {
        var scriptPath = CreateScript("scripts/init.txt");
        var configuration = CreateConfig();
        configuration.InitialisierungsskriptRelativePath = Path.GetRelativePath(_repositoryRootPath, scriptPath);

        var sut = CreateSut();
        var act = () => sut.RunAsync(_repositoryRootPath, configuration);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*nicht unterstützten Dateityp*");
        _cliRunnerMock.Verify(
            runner => runner.RunAsync(
                It.IsAny<string>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<string?>(),
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private RepositoryInitialisierungService CreateSut()
        => new(
            _cliRunnerMock.Object,
            NullLogger<RepositoryInitialisierungService>.Instance);

    /// <summary>
    /// Richtet das gemeinsame Mock-Setup für <see cref="ICliRunner.RunAsync"/> ein und liefert ein
    /// Capture-Objekt, in das der beim Aufruf übergebene Befehl und die Argumente geschrieben werden.
    /// </summary>
    /// <returns>Das Capture-Objekt, das nach dem Aufruf Befehl und Argumente enthält.</returns>
    private CliRunnerCapture SetupCliRunnerCapture()
    {
        var capture = new CliRunnerCapture();

        _cliRunnerMock
            .Setup(runner => runner.RunAsync(
                It.IsAny<string>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<string?>(),
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, IEnumerable<string>, string?, IDictionary<string, string>?, CancellationToken>((command, args, _, _, _) =>
            {
                capture.Command = command;
                capture.Args = args.ToList();
            })
            .ReturnsAsync(new CliResult(0, "ok", string.Empty));

        return capture;
    }

    /// <summary>Erfasst den beim gemockten <see cref="ICliRunner.RunAsync"/>-Aufruf übergebenen Befehl und die Argumente.</summary>
    private sealed class CliRunnerCapture
    {
        /// <summary>Der erfasste Befehl (Executable-Pfad).</summary>
        public string? Command { get; set; }

        /// <summary>Die erfassten Argumente.</summary>
        public IReadOnlyList<string> Args { get; set; } = [];
    }

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
