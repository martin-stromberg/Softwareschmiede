using System.IO.Compression;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Softwareschmiede.Application.Services.Updates;

namespace Softwareschmiede.Infrastructure.Services.Updates;

/// <summary>Bereitet ein Update-Paket durch Download, Entpacken und Basisvalidierung vor.</summary>
public sealed class UpdatePackageService : IUpdatePackageService
{
    private readonly HttpClient _httpClient;
    private readonly IUpdateScriptService _scriptService;
    private readonly UpdateOptions _options;
    private readonly ILogger<UpdatePackageService> _logger;
    private readonly string _baseDirectory;

    /// <inheritdoc cref="UpdatePackageService"/>
    public UpdatePackageService(
        HttpClient httpClient,
        IUpdateScriptService scriptService,
        IOptions<UpdateOptions> options,
        ILogger<UpdatePackageService> logger)
        : this(httpClient, scriptService, options, logger, AppContext.BaseDirectory)
    {
    }

    /// <summary>Erstellt den Service mit explizitem Basispfad für Tests.</summary>
    public UpdatePackageService(
        HttpClient httpClient,
        IUpdateScriptService scriptService,
        IOptions<UpdateOptions> options,
        ILogger<UpdatePackageService> logger,
        string baseDirectory)
    {
        _httpClient = httpClient;
        _scriptService = scriptService;
        _options = options.Value;
        _logger = logger;
        _baseDirectory = Path.GetFullPath(baseDirectory);
    }

    /// <inheritdoc/>
    public async Task<UpdatePreparationResult> PreparePackageAsync(
        UpdateInfo update,
        IProgress<UpdatePreparationProgress>? progress,
        CancellationToken ct = default)
    {
        var updateRoot = EnsureInsideBase(Path.Combine(_baseDirectory, _options.UpdateDirectoryName));
        var downloadDirectory = EnsureInsideBase(Path.Combine(updateRoot, "download"));
        var extractRoot = EnsureInsideBase(Path.Combine(updateRoot, "extracted"));
        var extractedDirectory = EnsureInsideBase(Path.Combine(extractRoot, SanitizePathSegment(update.Version)));
        var partialZipPath = EnsureInsideBase(Path.Combine(downloadDirectory, $"{_options.AssetName}.download"));
        var zipPath = EnsureInsideBase(Path.Combine(downloadDirectory, _options.AssetName));
        var logPath = EnsureInsideBase(Path.Combine(updateRoot, "update.log"));

        Directory.CreateDirectory(downloadDirectory);
        Directory.CreateDirectory(extractRoot);
        DeleteFileIfExists(partialZipPath, updateRoot);
        DeleteFileIfExists(zipPath, updateRoot);
        DeleteDirectoryIfExists(extractedDirectory, updateRoot);
        Directory.CreateDirectory(extractedDirectory);

        try
        {
            progress?.Report(new UpdatePreparationProgress(UpdatePreparationPhase.Download, 0, "Update wird heruntergeladen."));
            await DownloadAsync(update.DownloadUrl, partialZipPath, progress, ct);
            File.Move(partialZipPath, zipPath, overwrite: true);

            if (new FileInfo(zipPath).Length == 0)
                throw new InvalidDataException("Das heruntergeladene Update-Paket ist leer.");

            progress?.Report(new UpdatePreparationProgress(UpdatePreparationPhase.Entpacken, null, "Update wird entpackt."));
            ZipFile.ExtractToDirectory(zipPath, extractedDirectory, overwriteFiles: true);
            ValidateExtractedPackage(extractedDirectory);

            progress?.Report(new UpdatePreparationProgress(UpdatePreparationPhase.UpdateVorbereiten, null, "Update-Skript wird vorbereitet."));
            var scriptPath = await _scriptService.CreateScriptAsync(_baseDirectory, extractedDirectory, _options.ExecutableName, logPath, ct);
            var requiresElevation = !CanWriteToBaseDirectory();

            return new UpdatePreparationResult(zipPath, extractedDirectory, scriptPath, logPath, requiresElevation);
        }
        catch
        {
            DeleteFileIfExists(partialZipPath, updateRoot);
            DeleteFileIfExists(zipPath, updateRoot);
            DeleteDirectoryIfExists(extractedDirectory, updateRoot);
            throw;
        }
    }

    private async Task DownloadAsync(
        Uri downloadUrl,
        string partialZipPath,
        IProgress<UpdatePreparationProgress>? progress,
        CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, downloadUrl);
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength;
        await using var remoteStream = await response.Content.ReadAsStreamAsync(ct);
        await using var fileStream = new FileStream(partialZipPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);

        var buffer = new byte[81920];
        long downloaded = 0;
        int read;
        while ((read = await remoteStream.ReadAsync(buffer.AsMemory(0, buffer.Length), ct)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, read), ct);
            downloaded += read;
            if (total is > 0)
            {
                var percent = Math.Min(100, downloaded * 100d / total.Value);
                progress?.Report(new UpdatePreparationProgress(UpdatePreparationPhase.Download, percent, "Update wird heruntergeladen."));
            }
        }
    }

    private void ValidateExtractedPackage(string extractedDirectory)
    {
        var exePath = Path.Combine(extractedDirectory, _options.ExecutableName);
        var versionPath = Path.Combine(extractedDirectory, "version.json");
        if (!File.Exists(exePath))
            throw new InvalidDataException($"Das Update-Paket enthält {_options.ExecutableName} nicht im Root-Verzeichnis.");

        if (!File.Exists(versionPath))
            throw new InvalidDataException("Das Update-Paket enthält version.json nicht im Root-Verzeichnis.");
    }

    private bool CanWriteToBaseDirectory()
    {
        var probePath = Path.Combine(_baseDirectory, $".update-write-test-{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(probePath, "test");
            File.Delete(probePath);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogInformation(ex, "Programmverzeichnis erfordert vermutlich erhöhte Rechte für den Dateiaustausch.");
            return false;
        }
    }

    private string EnsureInsideBase(string path)
        => EnsureInsideDirectory(path, _baseDirectory, "Programmverzeichnis", allowRoot: true);

    private static string EnsureInsideDirectory(string path, string directory, string description, bool allowRoot)
    {
        var fullPath = Path.GetFullPath(path);
        var fullDirectory = Path.GetFullPath(directory);
        var relativePath = Path.GetRelativePath(fullDirectory, fullPath);

        var isRoot = relativePath == ".";
        var isOutside = relativePath == ".."
            || relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            || relativePath.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal)
            || Path.IsPathRooted(relativePath);

        if (isOutside || (isRoot && !allowRoot))
            throw new InvalidOperationException($"Update-Pfad liegt außerhalb des {description}: {fullPath}");

        return fullPath;
    }

    private static string SanitizePathSegment(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = value.Select(c => invalid.Contains(c) ? '_' : c).ToArray();
        return new string(chars);
    }

    private static void DeleteFileIfExists(string path, string updateRoot)
    {
        EnsureInsideDirectory(path, updateRoot, "Update-Verzeichnisses", allowRoot: false);
        if (File.Exists(path))
            File.Delete(path);
    }

    private static void DeleteDirectoryIfExists(string path, string updateRoot)
    {
        EnsureInsideDirectory(path, updateRoot, "Update-Verzeichnisses", allowRoot: false);
        if (Directory.Exists(path))
            Directory.Delete(path, recursive: true);
    }
}
