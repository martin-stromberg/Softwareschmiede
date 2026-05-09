using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Infrastructure.Services;

/// <summary>Service zur Verwaltung von Agentenpaketen, Verzeichnissen und Dateien im Dateisystem.</summary>
public sealed class AgentPackageFileService : IAgentPackageFileService
{
    private readonly ILogger<AgentPackageFileService> _logger;
    private readonly string _packagesBasePath;

    /// <summary>Erstellt eine neue Instanz des <see cref="AgentPackageFileService"/>.</summary>
    /// <param name="logger">Logger-Instanz.</param>
    /// <param name="env">Web-Host-Umgebung für den Basispfad.</param>
    public AgentPackageFileService(ILogger<AgentPackageFileService> logger, IWebHostEnvironment env)
    {
        _logger = logger;
        _packagesBasePath = Path.GetFullPath(Path.Combine(env.ContentRootPath, "agent-packages"));
        if (!Directory.Exists(_packagesBasePath))
        {
            Directory.CreateDirectory(_packagesBasePath);
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Pakete
    // ──────────────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public Task<AgentPackageInfo> CreatePackageAsync(string name, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(name);
        ValidateName(name);

        _logger.LogInformation("Erstelle Agentenpaket '{Name}'", name);

        var packagePath = GetPackageBasePath(name);
        if (Directory.Exists(packagePath))
        {
            throw new InvalidOperationException($"Ein Paket mit dem Namen '{name}' existiert bereits.");
        }

        Directory.CreateDirectory(packagePath);

        var info = new AgentPackageInfo(name, packagePath, Array.Empty<AgentInfo>(), Array.Empty<string>());
        return Task.FromResult(info);
    }

    /// <inheritdoc/>
    public Task RenamePackageAsync(string oldName, string newName, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(oldName);
        ArgumentNullException.ThrowIfNull(newName);
        ValidateName(oldName, nameof(oldName));
        ValidateName(newName, nameof(newName));

        _logger.LogInformation("Benenne Agentenpaket '{OldName}' in '{NewName}' um", oldName, newName);

        var oldPath = GetPackageBasePath(oldName);
        var newPath = GetPackageBasePath(newName);

        if (!Directory.Exists(oldPath))
        {
            throw new InvalidOperationException($"Das Paket '{oldName}' wurde nicht gefunden.");
        }
        if (Directory.Exists(newPath))
        {
            throw new InvalidOperationException($"Ein Paket mit dem Namen '{newName}' existiert bereits.");
        }

        Directory.Move(oldPath, newPath);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task DeletePackageAsync(string name, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(name);
        ValidateName(name);

        _logger.LogInformation("Lösche Agentenpaket '{Name}'", name);

        var packagePath = GetPackageBasePath(name);
        if (!Directory.Exists(packagePath))
        {
            throw new InvalidOperationException($"Das Paket '{name}' wurde nicht gefunden.");
        }

        Directory.Delete(packagePath, recursive: true);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<FileTreeNode> BuildPackageTreeAsync(string packageName, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(packageName);

        _logger.LogInformation("Erstelle Dateibaum für Paket '{PackageName}'", packageName);

        var packagePath = GetPackageBasePath(packageName);
        if (!Directory.Exists(packagePath))
        {
            throw new InvalidOperationException($"Das Paket '{packageName}' wurde nicht gefunden.");
        }

        var rootNode = BuildNode(packagePath, string.Empty, packageName);
        return Task.FromResult(rootNode);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Verzeichnisse
    // ──────────────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public Task CreateDirectoryAsync(string packageName, string relativePath, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(packageName);
        ArgumentNullException.ThrowIfNull(relativePath);

        _logger.LogInformation("Erstelle Verzeichnis '{RelativePath}' in Paket '{PackageName}'", relativePath, packageName);

        var fullPath = ResolveSafePath(packageName, relativePath);
        if (Directory.Exists(fullPath))
        {
            throw new InvalidOperationException($"Das Verzeichnis '{relativePath}' existiert bereits.");
        }

        Directory.CreateDirectory(fullPath);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task RenameDirectoryAsync(string packageName, string relativeOldPath, string newName, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(packageName);
        ArgumentNullException.ThrowIfNull(relativeOldPath);
        ArgumentNullException.ThrowIfNull(newName);
        ValidateName(newName, nameof(newName));

        _logger.LogInformation("Benenne Verzeichnis '{OldPath}' in '{NewName}' um (Paket: {PackageName})", relativeOldPath, newName, packageName);

        var oldFullPath = ResolveSafePath(packageName, relativeOldPath);
        if (!Directory.Exists(oldFullPath))
        {
            throw new InvalidOperationException($"Das Verzeichnis '{relativeOldPath}' wurde nicht gefunden.");
        }

        var parentDir = Path.GetDirectoryName(oldFullPath) ?? GetPackageBasePath(packageName);
        var newFullPath = Path.Combine(parentDir, newName);
        var safeNewPath = Path.GetFullPath(newFullPath);
        var baseWithSep = GetPackageBasePath(packageName).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!safeNewPath.StartsWith(baseWithSep, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Sicherheitsverletzung: Zielpfad außerhalb des Paket-Verzeichnisses.");
        }

        if (Directory.Exists(safeNewPath))
        {
            throw new InvalidOperationException($"Ein Verzeichnis mit dem Namen '{newName}' existiert bereits.");
        }

        Directory.Move(oldFullPath, safeNewPath);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task DeleteDirectoryAsync(string packageName, string relativePath, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(packageName);
        ArgumentNullException.ThrowIfNull(relativePath);

        _logger.LogInformation("Lösche Verzeichnis '{RelativePath}' in Paket '{PackageName}'", relativePath, packageName);

        var fullPath = ResolveSafePath(packageName, relativePath);
        if (!Directory.Exists(fullPath))
        {
            throw new InvalidOperationException($"Das Verzeichnis '{relativePath}' wurde nicht gefunden.");
        }

        Directory.Delete(fullPath, recursive: true);
        return Task.CompletedTask;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Dateien
    // ──────────────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<string> ReadFileAsync(string packageName, string relativeFilePath, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(packageName);
        ArgumentNullException.ThrowIfNull(relativeFilePath);

        _logger.LogInformation("Lese Datei '{RelativePath}' aus Paket '{PackageName}'", relativeFilePath, packageName);

        var fullPath = ResolveSafePath(packageName, relativeFilePath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"Die Datei '{relativeFilePath}' wurde nicht gefunden.", fullPath);
        }

        return await File.ReadAllTextAsync(fullPath, ct);
    }

    /// <inheritdoc/>
    public async Task WriteFileAsync(string packageName, string relativeFilePath, string content, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(packageName);
        ArgumentNullException.ThrowIfNull(relativeFilePath);
        ArgumentNullException.ThrowIfNull(content);

        _logger.LogInformation("Schreibe Datei '{RelativePath}' in Paket '{PackageName}'", relativeFilePath, packageName);

        var fullPath = ResolveSafePath(packageName, relativeFilePath);
        var directory = Path.GetDirectoryName(fullPath);
        if (directory is not null && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(fullPath, content, ct);
    }

    /// <inheritdoc/>
    public async Task CreateEmptyFileAsync(string packageName, string relativeFilePath, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(packageName);
        ArgumentNullException.ThrowIfNull(relativeFilePath);

        _logger.LogInformation("Erstelle leere Datei '{RelativePath}' in Paket '{PackageName}'", relativeFilePath, packageName);

        var fullPath = ResolveSafePath(packageName, relativeFilePath);
        if (File.Exists(fullPath))
        {
            throw new InvalidOperationException($"Die Datei '{relativeFilePath}' existiert bereits.");
        }

        var directory = Path.GetDirectoryName(fullPath);
        if (directory is not null && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(fullPath, string.Empty, ct);
    }

    /// <inheritdoc/>
    public async Task UploadFileAsync(string packageName, string relativeDirectory, string fileName, Stream content, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(packageName);
        ArgumentNullException.ThrowIfNull(relativeDirectory);
        ArgumentNullException.ThrowIfNull(fileName);
        ArgumentNullException.ThrowIfNull(content);
        ValidateName(fileName, nameof(fileName));

        _logger.LogInformation("Lade Datei '{FileName}' in Paket '{PackageName}' hoch (Verzeichnis: '{Directory}')", fileName, packageName, relativeDirectory);

        var dirPath = relativeDirectory.Length > 0
            ? ResolveSafePath(packageName, relativeDirectory)
            : GetPackageBasePath(packageName);

        if (!Directory.Exists(dirPath))
        {
            Directory.CreateDirectory(dirPath);
        }

        var relativeFilePath = relativeDirectory.Length > 0
            ? $"{relativeDirectory}/{fileName}"
            : fileName;

        var fullPath = ResolveSafePath(packageName, relativeFilePath);

        await using var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await content.CopyToAsync(fileStream, ct);
    }

    /// <inheritdoc/>
    public Task RenameFileAsync(string packageName, string relativeOldPath, string newName, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(packageName);
        ArgumentNullException.ThrowIfNull(relativeOldPath);
        ArgumentNullException.ThrowIfNull(newName);
        ValidateName(newName, nameof(newName));

        _logger.LogInformation("Benenne Datei '{OldPath}' in '{NewName}' um (Paket: {PackageName})", relativeOldPath, newName, packageName);

        var oldFullPath = ResolveSafePath(packageName, relativeOldPath);
        if (!File.Exists(oldFullPath))
        {
            throw new FileNotFoundException($"Die Datei '{relativeOldPath}' wurde nicht gefunden.", oldFullPath);
        }

        var parentDir = Path.GetDirectoryName(oldFullPath) ?? GetPackageBasePath(packageName);
        var newFullPath = Path.GetFullPath(Path.Combine(parentDir, newName));
        var baseWithSep = GetPackageBasePath(packageName).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!newFullPath.StartsWith(baseWithSep, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Sicherheitsverletzung: Zielpfad außerhalb des Paket-Verzeichnisses.");
        }

        if (File.Exists(newFullPath))
        {
            throw new InvalidOperationException($"Eine Datei mit dem Namen '{newName}' existiert bereits.");
        }

        File.Move(oldFullPath, newFullPath);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task DeleteFileAsync(string packageName, string relativeFilePath, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(packageName);
        ArgumentNullException.ThrowIfNull(relativeFilePath);

        _logger.LogInformation("Lösche Datei '{RelativePath}' aus Paket '{PackageName}'", relativeFilePath, packageName);

        var fullPath = ResolveSafePath(packageName, relativeFilePath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"Die Datei '{relativeFilePath}' wurde nicht gefunden.", fullPath);
        }

        File.Delete(fullPath);
        return Task.CompletedTask;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Hilfsmethoden
    // ──────────────────────────────────────────────────────────────────────────

    private string GetPackageBasePath(string packageName)
        => Path.GetFullPath(Path.Combine(_packagesBasePath, packageName));

    private string ResolveSafePath(string packageName, string relativePath)
    {
        var basePath = GetPackageBasePath(packageName);

        if (string.IsNullOrEmpty(relativePath))
        {
            return basePath;
        }

        // Normalize path separators
        var normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.GetFullPath(Path.Combine(basePath, normalized));

        // Ensure the resolved path is within the package directory
        var baseWithSep = basePath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(baseWithSep, StringComparison.OrdinalIgnoreCase) && fullPath != basePath)
        {
            throw new InvalidOperationException($"Sicherheitsverletzung: Pfad außerhalb des Paket-Verzeichnisses: {relativePath}");
        }

        return fullPath;
    }

    private static void ValidateName(string name, string paramName = "name")
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name darf nicht leer sein.", paramName);
        }

        var invalidChars = Path.GetInvalidFileNameChars();
        var invalidFound = name.Where(c => invalidChars.Contains(c)).ToArray();
        if (invalidFound.Length > 0)
        {
            throw new ArgumentException($"Name enthält ungültige Zeichen: {string.Join(", ", invalidFound)}", paramName);
        }

        // Prevent path traversal via name
        if (name.Contains('/') || name.Contains('\\') || name == ".." || name == ".")
        {
            throw new ArgumentException($"Name enthält ungültige Pfadzeichen: {name}", paramName);
        }
    }

    private FileTreeNode BuildNode(string absolutePath, string relativePath, string packageName)
    {
        var name = relativePath.Length == 0 ? packageName : Path.GetFileName(absolutePath);
        var isDirectory = Directory.Exists(absolutePath);

        var node = new FileTreeNode
        {
            Name = name,
            RelativePath = relativePath,
            PackageName = packageName,
            IsDirectory = isDirectory
        };

        if (!isDirectory)
        {
            return node;
        }

        var entries = Directory.GetFileSystemEntries(absolutePath);
        var children = new List<FileTreeNode>();

        foreach (var entry in entries)
        {
            var childName = Path.GetFileName(entry);
            var childRelPath = relativePath.Length > 0 ? $"{relativePath}/{childName}" : childName;
            var childNode = BuildNode(entry, childRelPath, packageName);
            children.Add(childNode);
        }

        // Verzeichnisse zuerst, dann Dateien, jeweils alphabetisch
        node.Children.AddRange(children
            .OrderByDescending(c => c.IsDirectory)
            .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase));

        return node;
    }
}
