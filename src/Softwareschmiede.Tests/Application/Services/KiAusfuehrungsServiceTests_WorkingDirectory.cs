using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Interfaces;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>Tests für die Arbeitsverzeichnis-Auflösung und -Validierung in <see cref="KiAusfuehrungsService"/>.</summary>
public sealed class KiAusfuehrungsServiceTests_WorkingDirectory : IDisposable
{
    private readonly string _tempRoot;
    private readonly KiAusfuehrungsService _sut;

    /// <summary>KiAusfuehrungsServiceTests_WorkingDirectory.</summary>
    public KiAusfuehrungsServiceTests_WorkingDirectory()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "swst-wd-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(_tempRoot);

        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        _sut = new KiAusfuehrungsService(NullLogger<KiAusfuehrungsService>.Instance, NullLoggerFactory.Instance, scopeFactoryMock.Object);
    }

    /// <summary>Dispose.</summary>
    public void Dispose()
    {
        _sut.Dispose();
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    /// <summary>ResolveEffectiveWorkingDirectory kombiniert Repository-Root und relativen Pfad zu einem absoluten Pfad.</summary>
    [Fact]
    public void ResolveEffectiveWorkingDirectory_ShouldCombinePaths()
    {
        var result = WorkingDirectoryResolver.ResolveEffectiveWorkingDirectory(_tempRoot, "backend");

        result.Should().Be(Path.GetFullPath(Path.Combine(_tempRoot, "backend")));
    }

    /// <summary>ResolveEffectiveWorkingDirectory wirft eine Exception bei Path-Traversal-Versuchen.</summary>
    [Fact]
    public void ResolveEffectiveWorkingDirectory_ShouldRejectPathTraversal()
    {
        var act = () => WorkingDirectoryResolver.ResolveEffectiveWorkingDirectory(_tempRoot, "../../../etc");

        act.Should().Throw<InvalidOperationException>();
    }

    /// <summary>ResolveEffectiveWorkingDirectory interpretiert "." als Repository-Root.</summary>
    [Fact]
    public void ResolveEffectiveWorkingDirectory_ShouldAcceptDotAsRoot()
    {
        var result = WorkingDirectoryResolver.ResolveEffectiveWorkingDirectory(_tempRoot, ".");

        result.Should().Be(Path.GetFullPath(_tempRoot));
    }

    /// <summary>
    /// Regressionstest: Ein Root-Verzeichnis darf nicht fälschlich ein Sibling-Verzeichnis mit gemeinsamem
    /// String-Präfix als "innerhalb" akzeptieren (z. B. "task-1" vs. "task-12"), wenn der Vergleich kein
    /// abschließendes Verzeichnistrennzeichen berücksichtigt.
    /// </summary>
    [Fact]
    public void ResolveEffectiveWorkingDirectory_ShouldRejectSiblingDirectoryWithSharedPrefix()
    {
        var parent = Path.Combine(Path.GetTempPath(), "swst-wd-sibling-" + Guid.NewGuid());
        var root = Path.Combine(parent, "task-1");
        var siblingSecret = Path.Combine(parent, "task-12", "secret");
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(siblingSecret);

        try
        {
            var relativePath = Path.Combine("..", "task-12", "secret");

            var act = () => WorkingDirectoryResolver.ResolveEffectiveWorkingDirectory(root, relativePath);

            act.Should().Throw<InvalidOperationException>();
        }
        finally
        {
            Directory.Delete(parent, recursive: true);
        }
    }

    /// <summary>ValidateWorkingDirectory wirft DirectoryNotFoundException, wenn das Verzeichnis nicht existiert.</summary>
    [Fact]
    public void ValidateWorkingDirectory_ShouldThrowWhenNotExists()
    {
        var missingDirectory = Path.Combine(_tempRoot, "does-not-exist");

        var act = () => WorkingDirectoryResolver.ValidateWorkingDirectory(missingDirectory, _tempRoot);

        act.Should().Throw<DirectoryNotFoundException>();
    }

    /// <summary>ValidateWorkingDirectory wirft keine Exception, wenn das Verzeichnis existiert.</summary>
    [Fact]
    public void ValidateWorkingDirectory_ShouldSucceedWhenExists()
    {
        var act = () => WorkingDirectoryResolver.ValidateWorkingDirectory(_tempRoot, _tempRoot);

        act.Should().NotThrow();
    }

    /// <summary>StartCliAsync übergibt dem Plugin das konfigurierte (effektive) Arbeitsverzeichnis, wenn startConfig gesetzt ist.</summary>
    [Fact]
    public async Task StartCliAsync_ShouldUseEffectiveWorkingDirectory()
    {
        var subDirectory = Path.Combine(_tempRoot, "backend");
        Directory.CreateDirectory(subDirectory);
        var startConfig = new RepositoryStartKonfiguration { WorkingDirectoryRelativePath = "backend" };

        string? usedPath = null;
        var pluginMock = new Mock<IKiPlugin>();
        pluginMock.Setup(p => p.StartCliAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback<string, string?, CancellationToken>((path, _, _) => usedPath = path)
            .ReturnsAsync(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c exit 0",
                UseShellExecute = false,
                CreateNoWindow = true,
            });

        await _sut.StartCliAsync(Guid.NewGuid(), pluginMock.Object, _tempRoot, startConfig: startConfig);

        usedPath.Should().Be(Path.GetFullPath(subDirectory));
    }

    /// <summary>StartCliAsync übergibt dem Plugin das Repository-Root, wenn kein startConfig angegeben ist.</summary>
    [Fact]
    public async Task StartCliAsync_ShouldUseRepoRootWhenConfigNull()
    {
        string? usedPath = null;
        var pluginMock = new Mock<IKiPlugin>();
        pluginMock.Setup(p => p.StartCliAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback<string, string?, CancellationToken>((path, _, _) => usedPath = path)
            .ReturnsAsync(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c exit 0",
                UseShellExecute = false,
                CreateNoWindow = true,
            });

        await _sut.StartCliAsync(Guid.NewGuid(), pluginMock.Object, _tempRoot);

        usedPath.Should().Be(_tempRoot);
    }
}
