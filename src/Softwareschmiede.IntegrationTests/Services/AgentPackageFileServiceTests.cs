using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Softwareschmiede.Infrastructure.Services;

namespace Softwareschmiede.IntegrationTests.Services;

/// <summary>
/// Integrationstests für <see cref="AgentPackageFileService"/> mit echten Dateisystem-Operationen.
/// Jeder Test nutzt ein eigenes temporäres Verzeichnis, das nach dem Test wieder gelöscht wird.
/// </summary>
public sealed class AgentPackageFileServiceTests : IDisposable
{
    private readonly string _tempBasePath;
    private readonly AgentPackageFileService _sut;

    /// <summary>Initialisiert ein temporäres Verzeichnis und den Service für jeden Test.</summary>
    public AgentPackageFileServiceTests()
    {
        _tempBasePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempBasePath);

        var envMock = new Mock<IHostEnvironment>();
        envMock.Setup(e => e.ContentRootPath).Returns(_tempBasePath);

        _sut = new AgentPackageFileService(
            NullLogger<AgentPackageFileService>.Instance,
            envMock.Object);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Directory.Exists(_tempBasePath))
        {
            Directory.Delete(_tempBasePath, recursive: true);
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Pakete
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Stellt sicher, dass <c>CreatePackageAsync</c> ein Verzeichnis anlegt,
    /// wenn ein gültiger Name übergeben wird.
    /// </summary>
    [Fact]
    public async Task CreatePackageAsync_ShouldCreateDirectory_WhenNameIsValid()
    {
        // Arrange
        const string packageName = "mein-paket";

        // Act
        var result = await _sut.CreatePackageAsync(packageName);

        // Assert
        var expectedPath = Path.Combine(_tempBasePath, "agent-packages", packageName);
        Directory.Exists(expectedPath).Should().BeTrue();
        result.Name.Should().Be(packageName);
    }

    /// <summary>
    /// Stellt sicher, dass <c>CreatePackageAsync</c> eine <see cref="ArgumentException"/> wirft,
    /// wenn der Name ungültige Zeichen enthält.
    /// </summary>
    [Fact]
    public async Task CreatePackageAsync_ShouldThrow_WhenNameContainsInvalidChars()
    {
        // Arrange
        const string invalidName = "invalid/name";

        // Act
        var act = async () => await _sut.CreatePackageAsync(invalidName);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    /// <summary>
    /// Stellt sicher, dass <c>RenamePackageAsync</c> das Paket-Verzeichnis umbenennt,
    /// wenn das Quellpaket existiert.
    /// </summary>
    [Fact]
    public async Task RenamePackageAsync_ShouldRenameDirectory_WhenPackageExists()
    {
        // Arrange
        const string oldName = "altes-paket";
        const string newName = "neues-paket";
        await _sut.CreatePackageAsync(oldName);

        // Act
        await _sut.RenamePackageAsync(oldName, newName);

        // Assert
        var packagesRoot = Path.Combine(_tempBasePath, "agent-packages");
        Directory.Exists(Path.Combine(packagesRoot, oldName)).Should().BeFalse();
        Directory.Exists(Path.Combine(packagesRoot, newName)).Should().BeTrue();
    }

    /// <summary>
    /// Stellt sicher, dass <c>DeletePackageAsync</c> das Paket-Verzeichnis löscht,
    /// wenn das Paket existiert.
    /// </summary>
    [Fact]
    public async Task DeletePackageAsync_ShouldDeleteDirectory_WhenPackageExists()
    {
        // Arrange
        const string packageName = "zu-loeschendes-paket";
        await _sut.CreatePackageAsync(packageName);

        // Act
        await _sut.DeletePackageAsync(packageName);

        // Assert
        var expectedPath = Path.Combine(_tempBasePath, "agent-packages", packageName);
        Directory.Exists(expectedPath).Should().BeFalse();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Verzeichnisse
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Stellt sicher, dass <c>CreateDirectoryAsync</c> ein Unterverzeichnis anlegt,
    /// wenn der Pfad gültig ist.
    /// </summary>
    [Fact]
    public async Task CreateDirectoryAsync_ShouldCreateSubdirectory_WhenPathIsValid()
    {
        // Arrange
        const string packageName = "test-paket";
        const string subDir = "skills";
        await _sut.CreatePackageAsync(packageName);

        // Act
        await _sut.CreateDirectoryAsync(packageName, subDir);

        // Assert
        var expectedPath = Path.Combine(_tempBasePath, "agent-packages", packageName, subDir);
        Directory.Exists(expectedPath).Should().BeTrue();
    }

    /// <summary>
    /// Stellt sicher, dass <c>CreateDirectoryAsync</c> eine <see cref="InvalidOperationException"/> wirft,
    /// wenn ein Path-Traversal-Angriff erkannt wird (z.B. <c>..</c> im Pfad).
    /// </summary>
    [Fact]
    public async Task CreateDirectoryAsync_ShouldThrow_WhenPathTraversalDetected()
    {
        // Arrange
        const string packageName = "test-paket";
        const string traversalPath = "../ausbruch";
        await _sut.CreatePackageAsync(packageName);

        // Act
        var act = async () => await _sut.CreateDirectoryAsync(packageName, traversalPath);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Sicherheitsverletzung*");
    }

    /// <summary>
    /// Stellt sicher, dass <c>RenameDirectoryAsync</c> ein Verzeichnis umbenennt,
    /// wenn das Quellverzeichnis existiert.
    /// </summary>
    [Fact]
    public async Task RenameDirectoryAsync_ShouldRename_WhenDirectoryExists()
    {
        // Arrange
        const string packageName = "test-paket";
        const string oldDir = "altes-verzeichnis";
        const string newDir = "neues-verzeichnis";
        await _sut.CreatePackageAsync(packageName);
        await _sut.CreateDirectoryAsync(packageName, oldDir);

        // Act
        await _sut.RenameDirectoryAsync(packageName, oldDir, newDir);

        // Assert
        var packagesRoot = Path.Combine(_tempBasePath, "agent-packages", packageName);
        Directory.Exists(Path.Combine(packagesRoot, oldDir)).Should().BeFalse();
        Directory.Exists(Path.Combine(packagesRoot, newDir)).Should().BeTrue();
    }

    /// <summary>
    /// Stellt sicher, dass <c>DeleteDirectoryAsync</c> ein Verzeichnis löscht,
    /// wenn es existiert.
    /// </summary>
    [Fact]
    public async Task DeleteDirectoryAsync_ShouldDelete_WhenDirectoryExists()
    {
        // Arrange
        const string packageName = "test-paket";
        const string subDir = "zu-loeschen";
        await _sut.CreatePackageAsync(packageName);
        await _sut.CreateDirectoryAsync(packageName, subDir);

        // Act
        await _sut.DeleteDirectoryAsync(packageName, subDir);

        // Assert
        var expectedPath = Path.Combine(_tempBasePath, "agent-packages", packageName, subDir);
        Directory.Exists(expectedPath).Should().BeFalse();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Dateien
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Stellt sicher, dass <c>CreateEmptyFileAsync</c> eine leere Datei erstellt,
    /// wenn der Pfad gültig ist.
    /// </summary>
    [Fact]
    public async Task CreateEmptyFileAsync_ShouldCreateFile_WhenPathIsValid()
    {
        // Arrange
        const string packageName = "test-paket";
        const string filePath = "agent.md";
        await _sut.CreatePackageAsync(packageName);

        // Act
        await _sut.CreateEmptyFileAsync(packageName, filePath);

        // Assert
        var expectedPath = Path.Combine(_tempBasePath, "agent-packages", packageName, filePath);
        File.Exists(expectedPath).Should().BeTrue();
        (await File.ReadAllTextAsync(expectedPath)).Should().BeEmpty();
    }

    /// <summary>
    /// Stellt sicher, dass <c>WriteFileAsync</c> den Inhalt einer Datei schreibt,
    /// wenn der Pfad gültig ist.
    /// </summary>
    [Fact]
    public async Task WriteFileAsync_ShouldWriteContent_WhenFileExists()
    {
        // Arrange
        const string packageName = "test-paket";
        const string filePath = "config.json";
        const string content = """{"version": "1.0"}""";
        await _sut.CreatePackageAsync(packageName);
        await _sut.CreateEmptyFileAsync(packageName, filePath);

        // Act
        await _sut.WriteFileAsync(packageName, filePath, content);

        // Assert
        var expectedPath = Path.Combine(_tempBasePath, "agent-packages", packageName, filePath);
        var written = await File.ReadAllTextAsync(expectedPath);
        written.Should().Be(content);
    }

    /// <summary>
    /// Stellt sicher, dass <c>ReadFileAsync</c> den Inhalt einer vorhandenen Datei zurückgibt.
    /// </summary>
    [Fact]
    public async Task ReadFileAsync_ShouldReturnContent_WhenFileExists()
    {
        // Arrange
        const string packageName = "test-paket";
        const string filePath = "readme.md";
        const string expectedContent = "# Mein Paket";
        await _sut.CreatePackageAsync(packageName);
        await _sut.WriteFileAsync(packageName, filePath, expectedContent);

        // Act
        var result = await _sut.ReadFileAsync(packageName, filePath);

        // Assert
        result.Should().Be(expectedContent);
    }

    /// <summary>
    /// Stellt sicher, dass <c>UploadFileAsync</c> eine Datei aus einem Stream speichert,
    /// wenn ein gültiger Stream übergeben wird.
    /// </summary>
    [Fact]
    public async Task UploadFileAsync_ShouldSaveFile_WhenStreamProvided()
    {
        // Arrange
        const string packageName = "test-paket";
        const string fileName = "upload.txt";
        const string fileContent = "hochgeladener Inhalt";
        await _sut.CreatePackageAsync(packageName);
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(fileContent));

        // Act
        await _sut.UploadFileAsync(packageName, string.Empty, fileName, stream);

        // Assert
        var expectedPath = Path.Combine(_tempBasePath, "agent-packages", packageName, fileName);
        File.Exists(expectedPath).Should().BeTrue();
        var savedContent = await File.ReadAllTextAsync(expectedPath);
        savedContent.Should().Be(fileContent);
    }

    /// <summary>
    /// Stellt sicher, dass <c>RenameFileAsync</c> eine Datei umbenennt,
    /// wenn die Quelldatei existiert.
    /// </summary>
    [Fact]
    public async Task RenameFileAsync_ShouldRenameFile_WhenFileExists()
    {
        // Arrange
        const string packageName = "test-paket";
        const string oldFile = "alt.md";
        const string newFile = "neu.md";
        await _sut.CreatePackageAsync(packageName);
        await _sut.CreateEmptyFileAsync(packageName, oldFile);

        // Act
        await _sut.RenameFileAsync(packageName, oldFile, newFile);

        // Assert
        var packagePath = Path.Combine(_tempBasePath, "agent-packages", packageName);
        File.Exists(Path.Combine(packagePath, oldFile)).Should().BeFalse();
        File.Exists(Path.Combine(packagePath, newFile)).Should().BeTrue();
    }

    /// <summary>
    /// Stellt sicher, dass <c>DeleteFileAsync</c> eine Datei löscht,
    /// wenn die Datei existiert.
    /// </summary>
    [Fact]
    public async Task DeleteFileAsync_ShouldDeleteFile_WhenFileExists()
    {
        // Arrange
        const string packageName = "test-paket";
        const string filePath = "zuloeschen.md";
        await _sut.CreatePackageAsync(packageName);
        await _sut.CreateEmptyFileAsync(packageName, filePath);

        // Act
        await _sut.DeleteFileAsync(packageName, filePath);

        // Assert
        var expectedPath = Path.Combine(_tempBasePath, "agent-packages", packageName, filePath);
        File.Exists(expectedPath).Should().BeFalse();
    }

    /// <summary>
    /// Stellt sicher, dass <c>WriteFileAsync</c> eine <see cref="InvalidOperationException"/> wirft,
    /// wenn ein Path-Traversal-Angriff im Dateipfad erkannt wird.
    /// </summary>
    [Fact]
    public async Task WriteFileAsync_ShouldThrow_WhenPathTraversalDetected()
    {
        // Arrange
        const string packageName = "test-paket";
        const string traversalPath = "../../geheim.txt";
        await _sut.CreatePackageAsync(packageName);

        // Act
        var act = async () => await _sut.WriteFileAsync(packageName, traversalPath, "inhalt");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Sicherheitsverletzung*");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Dateibaum
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Stellt sicher, dass <c>BuildPackageTreeAsync</c> den Root-Knoten eines Pakets zurückgibt,
    /// wenn das Paket existiert.
    /// </summary>
    [Fact]
    public async Task GetFileTreeAsync_ShouldReturnPackages_WhenPackagesExist()
    {
        // Arrange
        const string packageName = "baum-paket";
        await _sut.CreatePackageAsync(packageName);
        await _sut.CreateEmptyFileAsync(packageName, "agent.md");

        // Act
        var tree = await _sut.BuildPackageTreeAsync(packageName);

        // Assert
        tree.Should().NotBeNull();
        tree.Name.Should().Be(packageName);
        tree.IsDirectory.Should().BeTrue();
        tree.IsPackageRoot.Should().BeTrue();
        tree.Children.Should().ContainSingle(n => n.Name == "agent.md");
    }

    /// <summary>
    /// Stellt sicher, dass <c>BuildPackageTreeAsync</c> eine verschachtelte Struktur zurückgibt,
    /// wenn Unterverzeichnisse und Dateien vorhanden sind.
    /// </summary>
    [Fact]
    public async Task GetFileTreeAsync_ShouldReturnNestedStructure_WhenSubdirectoriesExist()
    {
        // Arrange
        const string packageName = "verschachteltes-paket";
        await _sut.CreatePackageAsync(packageName);
        await _sut.CreateDirectoryAsync(packageName, "skills");
        await _sut.CreateEmptyFileAsync(packageName, "skills/skill1.md");
        await _sut.CreateEmptyFileAsync(packageName, "root.md");

        // Act
        var tree = await _sut.BuildPackageTreeAsync(packageName);

        // Assert
        tree.Should().NotBeNull();
        tree.Children.Should().HaveCount(2);

        var skillsDir = tree.Children.FirstOrDefault(n => n.Name == "skills");
        skillsDir.Should().NotBeNull();
        skillsDir!.IsDirectory.Should().BeTrue();
        skillsDir.Children.Should().ContainSingle(n => n.Name == "skill1.md");

        var rootFile = tree.Children.FirstOrDefault(n => n.Name == "root.md");
        rootFile.Should().NotBeNull();
        rootFile!.IsDirectory.Should().BeFalse();
    }
}
