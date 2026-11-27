using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;

namespace Softwareschmiede.StatusServer.Tests.TestSupport;

/// <summary>Baut ein Temp-Verzeichnis als lokale Update-Quelle
/// (<c>AutoUpdateLocalFolderSource</c>): ein gefälsstes Paket und ein
/// <c>statusserver-update.json</c>-Manifest mit echtem SHA-256 (oder einem
/// bewusst falschen Hash für Hash-Mismatch-Tests).</summary>
public sealed class TempUpdateSource : IDisposable
{
    /// <summary>Der Dateiname des Server-Manifests (deckt sich mit
    /// <c>StatusServerUpdateOptions.ManifestAssetName</c>).</summary>
    public const string ManifestFileName = "statusserver-update.json";

    /// <summary>Erstellt das Quellverzeichnis.</summary>
    public TempUpdateSource()
    {
        DirectoryPath = Path.Combine(Path.GetTempPath(), $"statusserver-update-src-{Guid.NewGuid():N}");
        Directory.CreateDirectory(DirectoryPath);
    }

    /// <summary>Pfad des Quellverzeichnisses (Wert für <c>Update:LocalFolderPath</c>).</summary>
    public string DirectoryPath { get; }

    /// <summary>Schreibt ein gefälsstes Paket in das Quellverzeichnis — ein echtes
    /// ZIP-Archiv, weil der Paket-Validator der Bibliothek neben Größe und SHA-256
    /// auch die ZIP-Integrität prüft.</summary>
    /// <param name="fileName">Der Paket-Dateiname (z. B. <c>statusserver-win-x64.zip</c>).</param>
    /// <returns>Der volle Pfad der geschriebenen Datei.</returns>
    public string WritePackage(string fileName)
    {
        var path = Path.Combine(DirectoryPath, fileName);
        using (var stream = File.Create(path))
        using (var zip = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Create))
        {
            var entry = zip.CreateEntry("Softwareschmiede.StatusServer.exe");
            using var writer = new StreamWriter(entry.Open());
            writer.Write("gefälschtes Server-Binary für Update-Tests");
        }

        return path;
    }

    /// <summary>Schreibt das Update-Manifest (<see cref="ManifestFileName"/>) für das
    /// aktuell laufende Plattform/RID-Paar mit dem realen SHA-256 des Pakets. Die
    /// lokale Quelle deserialisiert das Manifest direkt als
    /// <c>AutoUpdateReleaseInfo</c> — das Schema heißt hier <c>packages</c> mit
    /// <c>fileName</c>/<c>uri</c> (nicht das <c>assets</c>/<c>assetUrl</c>-Rezepte-
    /// Format, das nur die GitHub-Quelle parst); die lokale Quelle ignoriert den
    /// <c>uri</c>-Wert ohnehin und löst <c>fileName</c> im Quellverzeichnis auf.</summary>
    /// <param name="version">Die zu meldende Versionsnummer.</param>
    /// <param name="assetFileName">Der Paket-Dateiname (muss per
    /// <see cref="WritePackage"/> angelegt worden sein).</param>
    /// <param name="sha256Override">Optionaler falscher Hash (Hash-Mismatch-Szenario).</param>
    public void WriteManifest(string version, string assetFileName, string? sha256Override = null)
    {
        var packagePath = Path.Combine(DirectoryPath, assetFileName);
        var sha256 = sha256Override
            ?? Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(packagePath))).ToLowerInvariant();
        var sizeBytes = new FileInfo(packagePath).Length;
        var platform = OperatingSystem.IsWindows() ? "windows" : "linux";

        var manifest = new
        {
            version,
            releaseNotes = $"Test-Release {version}",
            publishedAt = DateTimeOffset.UtcNow,
            packages = new[]
            {
                new
                {
                    version,
                    platform,
                    runtimeIdentifier = RuntimeInformation.RuntimeIdentifier,
                    fileName = assetFileName,
                    uri = new Uri(packagePath).AbsoluteUri,
                    sha256,
                    sizeBytes,
                },
            },
        };
        File.WriteAllText(Path.Combine(DirectoryPath, ManifestFileName), JsonSerializer.Serialize(manifest));
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        try
        {
            Directory.Delete(DirectoryPath, recursive: true);
        }
        catch (IOException)
        {
            // Temp-Rest ist unkritisch und wird vom OS aufgeräumt.
        }
    }
}
