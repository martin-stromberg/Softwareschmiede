using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.App.Services.Testing;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Application.Services.Updates;
using Softwareschmiede.Infrastructure.Data;

namespace Softwareschmiede.Tests.E2E;

/// <summary>
/// Besitzt die isolierte Update-E2E-Testumgebung eines einzelnen Testlaufs: eine eindeutige
/// Temp-Wurzel mit simulierter Installation (<c>installed/version.json</c>), eigener
/// SQLite-Datenbank, Szenario- und Konfigurationsdatei, Stable-/RC-ZIP-Paketen mit getrennten
/// Download-URLs und unterscheidbarem Inhalt, dateibasierten Freigabe-Gates sowie dem
/// strukturierten JSONL-Protokoll. Alle App-seitig geschriebenen Update-Artefakte bleiben
/// innerhalb der TestRoot.
/// </summary>
public sealed class UpdateE2EFixture : IDisposable
{
    /// <summary>Standardmäßig simulierte installierte Version.</summary>
    public const string InstalledVersion = "1.2.0";

    /// <summary>Version des simulierten Stable-Releases (neuer als <see cref="InstalledVersion"/>).</summary>
    public const string StableVersion = "1.3.0";

    /// <summary>Version des simulierten Release-Candidates (neuer als <see cref="StableVersion"/>).</summary>
    public const string RcVersion = "1.4.0-rc.1";

    /// <summary>Release-Listendaten-Dateiname relativ zur TestRoot.</summary>
    public const string ReleaseListFileName = "releases.json";

    private static readonly UpdateOptions DefaultOptions = new();

    private static readonly string RepositoryApiUrl =
        $"https://api.github.com/repos/{DefaultOptions.RepositoryOwner}/{DefaultOptions.RepositoryName}";

    /// <summary>Der Release-API-Endpunkt, den <c>GitHubReleaseClient</c> mit den Standard-<see cref="UpdateOptions"/> anfragt.</summary>
    public static readonly string ReleaseApiUrl = $"{RepositoryApiUrl}/releases?per_page=100";

    /// <summary>Feste simulierte Asset-ID des Stable-Pakets in der Release-Liste.</summary>
    public const long StableAssetId = 91001;

    /// <summary>Feste simulierte Asset-ID des RC-Pakets in der Release-Liste.</summary>
    public const long RcAssetId = 91002;

    /// <summary>GitHub-API-Asset-URL (Fallback-Download-Route) des Stable-Pakets.</summary>
    public static readonly string StableAssetApiUrl = AssetApiUrl(StableAssetId);

    /// <summary>GitHub-API-Asset-URL (Fallback-Download-Route) des RC-Pakets.</summary>
    public static readonly string RcAssetApiUrl = AssetApiUrl(RcAssetId);

    /// <summary>Feste simulierte Asset-ID des Stable-Manifests in der Release-Liste.</summary>
    public const long StableManifestAssetId = 91011;

    /// <summary>Feste simulierte Asset-ID des RC-Manifests in der Release-Liste.</summary>
    public const long RcManifestAssetId = 91012;

    /// <summary>GitHub-API-Asset-URL (Fallback-Download-Route) des Stable-Manifests.</summary>
    public static readonly string StableManifestAssetApiUrl = AssetApiUrl(StableManifestAssetId);

    /// <summary>GitHub-API-Asset-URL (Fallback-Download-Route) des RC-Manifests.</summary>
    public static readonly string RcManifestAssetApiUrl = AssetApiUrl(RcManifestAssetId);

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly UpdateE2EScenario _scenario = new();
    private readonly object _scenarioLock = new();
    private bool _disposed;

    /// <inheritdoc cref="UpdateE2EFixture"/>
    public UpdateE2EFixture()
    {
        ScenarioId = $"update-e2e-{Guid.NewGuid():N}";
        TestRoot = Path.Combine(Path.GetTempPath(), $"softwareschmiede_update_e2e_{Guid.NewGuid():N}");
        InstalledDirectory = Path.Combine(TestRoot, "installed");
        GatesDirectory = Path.Combine(TestRoot, "gates");
        PackageDirectory = Path.Combine(TestRoot, "packages");
        ScenarioFilePath = Path.Combine(TestRoot, "scenario.json");
        LogFilePath = Path.Combine(TestRoot, "events.jsonl");
        ConfigurationFilePath = Path.Combine(TestRoot, "testconfig.json");
        DatabasePath = Path.Combine(TestRoot, "test.db");

        Directory.CreateDirectory(InstalledDirectory);
        Directory.CreateDirectory(GatesDirectory);
        Directory.CreateDirectory(PackageDirectory);

        WriteInstalledVersion(InstalledVersion);

        // Deutlich unterscheidbare, eindeutige Download-URLs; niemals echte Release-Quellen.
        StableDownloadUrl = $"https://e2e.invalid/{ScenarioId}/release-{StableVersion}.zip";
        RcDownloadUrl = $"https://e2e.invalid/{ScenarioId}/release-{RcVersion}.zip";
        StableManifestDownloadUrl = $"https://e2e.invalid/{ScenarioId}/update-{StableVersion}.json";
        RcManifestDownloadUrl = $"https://e2e.invalid/{ScenarioId}/update-{RcVersion}.json";
        StableZipPath = Path.Combine(PackageDirectory, $"release-{StableVersion}.zip");
        RcZipPath = Path.Combine(PackageDirectory, $"release-{RcVersion}.zip");

        CreatePackageZip(StableZipPath, StableVersion, "stable-marker");
        CreatePackageZip(RcZipPath, RcVersion, "rc-marker");

        SetDefaultResponses();
        SaveScenario();
        WriteConfiguration();
    }

    /// <summary>Eindeutige Szenario-/Prozess-Identität für jeden Protokolleintrag.</summary>
    public string ScenarioId { get; }

    /// <summary>Eindeutiges Temp-Wurzelverzeichnis dieser Fixture.</summary>
    public string TestRoot { get; }

    /// <summary>Simuliertes Installationsverzeichnis mit der <c>version.json</c>.</summary>
    public string InstalledDirectory { get; }

    /// <summary>Verzeichnis der dateibasierten Freigabe-/Abbruch-Gates.</summary>
    public string GatesDirectory { get; }

    /// <summary>Verzeichnis der erzeugten ZIP-Pakete.</summary>
    public string PackageDirectory { get; }

    /// <summary>Pfad der pro Request/Lesebefehl neu gelesenen Szenariodatei.</summary>
    public string ScenarioFilePath { get; }

    /// <summary>Pfad der strukturierten JSONL-Protokolldatei.</summary>
    public string LogFilePath { get; }

    /// <summary>Pfad der Testkonfigurations-JSON (Wert von <c>SOFTWARESCHMIEDE_UPDATE_TEST_CONFIG</c>).</summary>
    public string ConfigurationFilePath { get; }

    /// <summary>Pfad der fixture-eigenen SQLite-Testdatenbank.</summary>
    public string DatabasePath { get; }

    /// <summary>Download-URL des Stable-Pakets.</summary>
    public string StableDownloadUrl { get; }

    /// <summary>Download-URL des RC-Pakets.</summary>
    public string RcDownloadUrl { get; }

    /// <summary>Download-URL des Stable-Manifests (<c>update.json</c>).</summary>
    public string StableManifestDownloadUrl { get; }

    /// <summary>Download-URL des RC-Manifests (<c>update.json</c>).</summary>
    public string RcManifestDownloadUrl { get; }

    /// <summary>Pfad der Stable-ZIP-Datei.</summary>
    public string StableZipPath { get; }

    /// <summary>Pfad der RC-ZIP-Datei.</summary>
    public string RcZipPath { get; }

    /// <summary>
    /// Liefert die prozessbezogenen Umgebungsvariablen für den App-Start:
    /// <c>SOFTWARESCHMIEDE_UPDATE_TEST_CONFIG</c> und <c>SOFTWARESCHMIEDE_TEST_DB_PATH</c>.
    /// </summary>
    /// <returns>Die Umgebungsvariablen für <c>WpfTestBase.LaunchApp</c>.</returns>
    public IReadOnlyDictionary<string, string?> CreateStartEnvironment()
        => new Dictionary<string, string?>
        {
            [UpdateE2ETestConfiguration.EnvironmentVariable] = ConfigurationFilePath,
            [UpdateE2ETestConfiguration.TestDatabaseEnvironmentVariable] = DatabasePath
        };

    /// <summary>
    /// Setzt die Standard-Antworten: Der Release-Endpunkt liefert die Release-Liste mit Stable- und
    /// RC-Release (sofortige Antwort), beide Paket-URLs liefern die zugehörigen ZIP-Dateien.
    /// </summary>
    /// <param name="withRcRelease">Bei <c>false</c> enthält die Release-Liste nur das Stable-Release.</param>
    public void SetDefaultResponses(bool withRcRelease = true)
    {
        _scenario.Answer.Clear();
        _scenario.Answer.Add(new UpdateE2EResponse
        {
            Url = ReleaseApiUrl,
            Status = 200,
            Content = CreateReleaseListsJson(withRcRelease)
        });
        foreach (var (url, zipPath) in new[]
        {
            (StableDownloadUrl, StableZipPath),
            (RcDownloadUrl, RcZipPath),
            (StableAssetApiUrl, StableZipPath),
            (RcAssetApiUrl, RcZipPath)
        })
        {
            _scenario.Answer.Add(new UpdateE2EResponse
            {
                Url = url,
                Status = 200,
                BodyFile = RelativeToRoot(zipPath)
            });
        }

        foreach (var (manifestUrl, version, downloadUrl, zipPath) in new[]
        {
            (StableManifestDownloadUrl, StableVersion, StableDownloadUrl, StableZipPath),
            (RcManifestDownloadUrl, RcVersion, RcDownloadUrl, RcZipPath),
            (StableManifestAssetApiUrl, StableVersion, StableDownloadUrl, StableZipPath),
            (RcManifestAssetApiUrl, RcVersion, RcDownloadUrl, RcZipPath)
        })
        {
            _scenario.Answer.Add(new UpdateE2EResponse
            {
                Url = manifestUrl,
                Status = 200,
                Content = CreateManifestJson(version, downloadUrl, zipPath),
                Kind = "manifest"
            });
        }
    }

    /// <summary>Mutiert das Szenario thread-sicher und schreibt die Szenariodatei anschließend neu.</summary>
    /// <param name="mutation">Die Änderung am Szenario.</param>
    public void UpdateScenario(Action<UpdateE2EScenario> mutation)
    {
        lock (_scenarioLock)
        {
            mutation(_scenario);
            SaveScenario();
        }
    }

    /// <summary>Schreibt das aktuelle Szenario in die Szenariodatei.</summary>
    public void SaveScenario()
    {
        lock (_scenarioLock)
        {
            File.WriteAllText(ScenarioFilePath, JsonSerializer.Serialize(_scenario, JsonOptions));
        }
    }

    /// <summary>
    /// Aktiviert den kontrollierten Settings-Lesefehler für die gewählte Lesegrenze.
    /// </summary>
    /// <param name="limit"><c>Initial</c>, <c>BeforePreparation</c> oder <c>BeforeUpdaterStart</c>.</param>
    /// <param name="attempt">Versuchsauswahl (<c>any</c>, <c>startup</c>, <c>manuell</c>, <c>pruefen</c>, <c>starten</c>).</param>
    /// <param name="onReleaseWait">Bei <c>true</c> blockiert der betroffene Read bis zur Gate-Freigabe.</param>
    /// <param name="waittimeSeconds">Maximale Gate-Wartezeit in Sekunden.</param>
    public void ActivateReadfailure(
        string limit,
        string attempt = "any",
        bool onReleaseWait = true,
        int waittimeSeconds = 30)
    {
        UpdateScenario(s => s.ReadFailure = new UpdateE2EReadFailure
        {
            Active = true,
            Attempt = attempt,
            Limit = limit,
            WaitForRelease = onReleaseWait,
            WaitTimeoutSeconds = waittimeSeconds
        });
    }

    /// <summary>
    /// Deaktiviert den kontrollierten Settings-Lesefehler ausdrücklich und protokolliert
    /// <see cref="UpdateE2EEvents.UpdateSettingsReadFailureDisabled"/> als Runner-Ereignis.
    /// </summary>
    public void DeactivateReadfailure()
    {
        UpdateScenario(s => s.ReadFailure = new UpdateE2EReadFailure { Active = false });
        UpdateE2ELog.Append(
            LogFilePath, ScenarioId, UpdateE2EEvents.UpdateSettingsReadFailureDisabled);
    }

    /// <summary>Schreibt die Freigabe-Datei <c>{gate}.release</c> für ein blockiertes Gate.</summary>
    /// <param name="gate">Der Gate-Name.</param>
    public void OpenGate(string gate)
        => File.WriteAllText(Path.Combine(GatesDirectory, $"{gate}.release"), "release");

    /// <summary>Schreibt die Abbruch-Datei <c>{gate}.cancel</c> für ein blockiertes Gate.</summary>
    /// <param name="gate">Der Gate-Name.</param>
    public void CancelGateFrom(string gate)
        => File.WriteAllText(Path.Combine(GatesDirectory, $"{gate}.cancel"), "cancel");

    /// <summary>Entfernt beide Gate-Dateien eines Gates wieder.</summary>
    /// <param name="gate">Der Gate-Name.</param>
    public void SetGateBack(string gate)
    {
        var release = Path.Combine(GatesDirectory, $"{gate}.release");
        var cancel = Path.Combine(GatesDirectory, $"{gate}.cancel");
        if (File.Exists(release))
            File.Delete(release);
        if (File.Exists(cancel))
            File.Delete(cancel);
    }

    /// <summary>
    /// Erstellt die Fixture-Datenbank (EF-Migration) und persistiert die Update--Einstellungen
    /// darin. Für Vorbedingungen vor dem ersten App-Start.
    /// </summary>
    /// <param name="mode">Der zu speichernde Update-Modus.</param>
    /// <param name="updatePath">Der zu speichernde Update-Pfad.</param>
    public async Task SetUpdateSettingsAsync(UpdateMode mode, string updatePath)
        => await WriteUpdateSettingsAsync(mode, updatePath, migrate: true);

    /// <summary>
    /// Persistiert Update--Einstellungen in der bereits bestehenden Fixture-Datenbank
    /// (für Änderungen zwischen zwei App-Läufen bei erhaltener Datenbank).
    /// </summary>
    /// <param name="mode">Der zu speichernde Update-Modus.</param>
    /// <param name="updatePath">Der zu speichernde Update-Pfad.</param>
    public async Task UpdateUpdateSettingsAsync(UpdateMode mode, string updatePath)
        => await WriteUpdateSettingsAsync(mode, updatePath, migrate: false);

    /// <summary>
    /// Persistiert die Update--Einstellungen über einen eigenen DbContext auf der
    /// Fixture-Datenbank; migriert das Schema nur bei <paramref name="migrate"/>.
    /// </summary>
    /// <param name="mode">Der zu speichernde Update-Modus.</param>
    /// <param name="updatePath">Der zu speichernde Update-Pfad.</param>
    /// <param name="migrate">Bei <c>true</c> wird die Fixture-Datenbank zuvor per EF-Migration erstellt.</param>
    private async Task WriteUpdateSettingsAsync(UpdateMode mode, string updatePath, bool migrate)
    {
        var options = new DbContextOptionsBuilder<SoftwareschmiedeDbContext>()
            .UseSqlite($"Data Source={DatabasePath}")
            .Options;

        await using var db = new SoftwareschmiedeDbContext(options);
        if (migrate)
            await db.Database.MigrateAsync();
        var service = new AppSettingService(db, NullLogger<AppSettingService>.Instance);
        await service.SetUpdateSettingsAsync(new UpdateSettings(mode, updatePath));
    }

    /// <summary>Liest alle bisher geschriebenen JSONL-Protokolleinträge.</summary>
    /// <returns>Die Protokolleinträge in Dateireihenfolge.</returns>
    public IReadOnlyList<UpdateE2ELogEntry> ReadLog()
    {
        if (!File.Exists(LogFilePath))
            return [];

        var entries = new List<UpdateE2ELogEntry>();
        string content;
        using (var stream = new FileStream(LogFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        using (var reader = new StreamReader(stream))
        {
            content = reader.ReadToEnd();
        }

        foreach (var line in content.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var entry = JsonSerializer.Deserialize<UpdateE2ELogEntry>(line.Trim());
            if (entry is not null)
                entries.Add(entry);
        }

        return entries;
    }

    /// <summary>
    /// Wartet begrenzt, bis mindestens <paramref name="mindestens"/> Protokolleinträge mit dem
    /// Ereignisnamen (und optionalem Filter) vorliegen, und liefert die Treffer.
    /// </summary>
    /// <param name="evt">Der Ereignisname.</param>
    /// <param name="timeout">Maximale Wartezeit.</param>
    /// <param name="mindestens">Mindestanzahl der Treffer.</param>
    /// <param name="filter">Optionaler Filter auf den Nutzdaten-Eintrag.</param>
    /// <returns>Die gefundenen Einträge.</returns>
    /// <exception cref="TimeoutException">Die Ereignisse erschienen nicht rechtzeitig.</exception>
    public async Task<IReadOnlyList<UpdateE2ELogEntry>> WaitOnEventAsync(
        string evt,
        TimeSpan timeout,
        int mindestens = 1,
        Func<UpdateE2ELogEntry, bool>? filter = null)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var match = ReadLog()
                .Where(e => e.Event == evt && (filter is null || filter(e)))
                .ToList();
            if (match.Count >= mindestens)
                return match;

            await Task.Delay(100);
        }

        var log = ReadLog();
        var gleichnamig = log
            .Where(e => e.Event == evt)
            .Select(e => e.Data is null ? "(ohne Daten)" : JsonSerializer.Serialize(e.Data));
        throw new TimeoutException(
            $"Protokollereignis '{evt}' (mindestens {mindestens}) erschien nicht innerhalb von {timeout.TotalSeconds}s. " +
            $"Bisherige Ereignisse: {string.Join(", ", log.Select(e => e.Event).Distinct())}. " +
            $"Vorhandene '{evt}'-Daten: {string.Join(" | ", gleichnamig)}");
    }

    /// <summary>Liefert die Anzahl der Protokolleinträge eines Ereignisnamens.</summary>
    /// <param name="evt">Der Ereignisname.</param>
    /// <returns>Die Anzahl der Einträge.</returns>
    public int CountEvent(string evt)
        => ReadLog().Count(e => e.Event == evt);

    /// <summary>Liefert den <c>data</c>-Wert eines Protokolleintrags als <see cref="JsonElement"/>.</summary>
    /// <param name="entry">Der Protokolleintrag.</param>
    /// <returns>Das Nutzdaten-Element oder <c>null</c>.</returns>
    public static JsonElement? Data(UpdateE2ELogEntry entry)
        => entry.Data as JsonElement? ?? (entry.Data is null ? null : JsonSerializer.SerializeToElement(entry.Data));

    /// <summary>GitHub-API-Asset-URL (Fallback-Download-Route) zu einer Asset-ID.</summary>
    /// <param name="assetId">Die Asset-ID.</param>
    /// <returns>Die Asset-API-URL.</returns>
    public static string AssetApiUrl(long assetId) => $"{RepositoryApiUrl}/releases/assets/{assetId}";

    /// <summary>Erzeugt das JSON-Objekt eines Release-Assets für die simulierte Release-Liste.</summary>
    /// <param name="apiUrl">Die GitHub-API-Asset-URL (<c>url</c>-Feld).</param>
    /// <param name="assetId">Die Asset-ID.</param>
    /// <param name="downloadUrl">Die Browser-Download-URL.</param>
    /// <param name="assetName">Der Asset-Name; Standard ist <see cref="UpdateOptions.AssetName"/>.</param>
    /// <returns>Das anonyme Asset-Objekt.</returns>
    public static object CreateAssetJson(string apiUrl, long assetId, string downloadUrl, string? assetName = null)
        => new
        {
            name = assetName ?? DefaultOptions.AssetName,
            url = apiUrl,
            id = assetId,
            browser_download_url = downloadUrl
        };

    /// <summary>Erzeugt das JSON-Objekt des Manifest-Assets (<c>update.json</c>) für die simulierte Release-Liste.</summary>
    /// <param name="apiUrl">Die GitHub-API-Asset-URL (<c>url</c>-Feld).</param>
    /// <param name="assetId">Die Asset-ID.</param>
    /// <param name="downloadUrl">Die Browser-Download-URL.</param>
    /// <returns>Das anonyme Asset-Objekt.</returns>
    public static object CreateManifestAssetJson(string apiUrl, long assetId, string downloadUrl)
        => CreateAssetJson(apiUrl, assetId, downloadUrl, DefaultOptions.ManifestAssetName);

    /// <summary>
    /// Erzeugt das Update-Manifest (<c>update.json</c>) im Workflow-Schema mit dem tatsächlichen
    /// SHA-256 und der Größe der Paket-ZIP unter <paramref name="zipPath"/>.
    /// </summary>
    /// <param name="version">Die Release-Version.</param>
    /// <param name="downloadUrl">Die Browser-Download-URL des Paket-Assets (<c>assetUrl</c>-Feld).</param>
    /// <param name="zipPath">Pfad der zu verifizierenden Paket-ZIP.</param>
    /// <returns>Das Manifest als JSON-Text.</returns>
    public static string CreateManifestJson(string version, string downloadUrl, string zipPath)
        => CreateManifestJson(version, downloadUrl, File.ReadAllBytes(zipPath));

    /// <summary>
    /// Erzeugt das Update-Manifest (<c>update.json</c>) im Workflow-Schema mit dem tatsächlichen
    /// SHA-256 und der Größe der Paket-Bytes in <paramref name="zipBytes"/>.
    /// </summary>
    /// <param name="version">Die Release-Version.</param>
    /// <param name="downloadUrl">Die Browser-Download-URL des Paket-Assets (<c>assetUrl</c>-Feld).</param>
    /// <param name="zipBytes">Bytes der zu verifizierenden Paket-ZIP.</param>
    /// <param name="sha256Override">Optionaler abweichender SHA-256-Wert (für Mismatch-Szenarien).</param>
    /// <returns>Das Manifest als JSON-Text.</returns>
    public static string CreateManifestJson(string version, string downloadUrl, byte[] zipBytes, string? sha256Override = null)
    {
        var sha256 = sha256Override ?? Convert.ToHexString(SHA256.HashData(zipBytes)).ToLowerInvariant();
        return JsonSerializer.Serialize(new
        {
            version,
            releaseNotes = $"Softwareschmiede release v{version}",
            publishedAt = DateTimeOffset.UtcNow.ToString("O"),
            assets = new[]
            {
                new
                {
                    platform = "windows",
                    runtimeIdentifier = "win-x64",
                    assetName = DefaultOptions.AssetName,
                    assetUrl = downloadUrl,
                    sha256,
                    sizeBytes = (long)zipBytes.Length
                }
            }
        });
    }

    /// <summary>Erzeugt das JSON-Objekt eines Releases für die simulierte Release-Liste.</summary>
    /// <param name="version">Die Release-Version (Tag wird als <c>v{version}</c> gesetzt).</param>
    /// <param name="prerelease">Ob das Release als Vorabversion markiert ist.</param>
    /// <param name="publishedAt">Der Veröffentlichungszeitpunkt (<c>published_at</c>-Feld).</param>
    /// <param name="assetJson">Das Asset-Objekt des Releases (aus <see cref="CreateAssetJson"/>).</param>
    /// <param name="manifestAssetJson">Optionales Manifest-Asset-Objekt des Releases (aus <see cref="CreateManifestAssetJson"/>).</param>
    /// <returns>Das anonyme Release-Objekt.</returns>
    public static object CreateReleaseJson(string version, bool prerelease, DateTimeOffset publishedAt, object assetJson, object? manifestAssetJson = null)
        => new
        {
            tag_name = $"v{version}",
            prerelease,
            draft = false,
            published_at = publishedAt,
            assets = manifestAssetJson is null ? new[] { assetJson } : new[] { assetJson, manifestAssetJson }
        };

    /// <summary>
    /// Erzeugt eine Release-Paket-ZIP mit Root-Einträgen <c>Softwareschmiede.exe</c> (unterscheidbarer
    /// Markerinhalt) und <c>version.json</c> passend zu <paramref name="version"/>.
    /// </summary>
    /// <param name="zipPath">Zielpfad der ZIP-Datei.</param>
    /// <param name="version">Die Paketversion.</param>
    /// <param name="marker">Unterscheidbarer Inhaltsmarker der Paket-Exe.</param>
    public static void CreatePackageZip(string zipPath, string version, string marker)
    {
        if (File.Exists(zipPath))
            File.Delete(zipPath);

        var executableName = DefaultOptions.ExecutableName;
        using var archiv = ZipFile.Open(zipPath, ZipArchiveMode.Create);

        var exe = archiv.CreateEntry(executableName);
        using (var writer = new StreamWriter(exe.Open()))
            writer.Write($"E2E-Paket {version} ({marker})");

        var versionJson = archiv.CreateEntry("version.json");
        using (var writer = new StreamWriter(versionJson.Open()))
            writer.Write($$"""{"version":"{{version}}","tagName":"v{{version}}"}""");
    }

    /// <summary>Liest den Markerinhalt der Paket-Exe aus einem entpackten Verzeichnis.</summary>
    /// <param name="directory">Das entpackte Paketverzeichnis.</param>
    /// <returns>Der Inhalt der Paket-Exe oder <c>null</c>.</returns>
    public static string? ReadPackageExeContent(string directory)
    {
        var path = Path.Combine(directory, DefaultOptions.ExecutableName);
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        // Fehlersteuerung sicher zurücksetzen, auch wenn der Test mittendrin abbricht.
        try { DeactivateReadfailure(); } catch { /* TestRoot evtl. schon entfernt. */ }

        // Diagnose-Fluchtventil: Mit SOFTWARESCHMIEDE_UPDATE_E2E_KEEP=1 bleibt die TestRoot
        // (Szenario, Gates, JSONL-Protokoll) zur Fehleranalyse erhalten.
        if (Environment.GetEnvironmentVariable("SOFTWARESCHMIEDE_UPDATE_E2E_KEEP") == "1")
            return;

        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                if (Directory.Exists(TestRoot))
                    Directory.Delete(TestRoot, recursive: true);
                return;
            }
            catch (IOException)
            {
                Thread.Sleep(200);
            }
            catch (UnauthorizedAccessException)
            {
                Thread.Sleep(200);
            }
        }
    }

    private void WriteInstalledVersion(string version)
    {
        File.WriteAllText(
            Path.Combine(InstalledDirectory, "version.json"),
            $$"""{"version":"{{version}}","tagName":"v{{version}}"}""");
    }

    private void WriteConfiguration()
    {
        var configuration = new UpdateE2ETestConfiguration
        {
            TestRoot = TestRoot,
            ScenarioFilePath = ScenarioFilePath,
            LogFilePath = LogFilePath,
            ScenarioId = ScenarioId,
            GatesDirectory = GatesDirectory
        };

        File.WriteAllText(ConfigurationFilePath, JsonSerializer.Serialize(configuration, JsonOptions));
    }

    private string CreateReleaseListsJson(bool withRcRelease)
    {
        var releases = new List<object>
        {
            CreateReleaseJson(
                StableVersion,
                prerelease: false,
                DateTimeOffset.UtcNow.AddDays(-7),
                CreateAssetJson(StableAssetApiUrl, StableAssetId, StableDownloadUrl),
                CreateManifestAssetJson(StableManifestAssetApiUrl, StableManifestAssetId, StableManifestDownloadUrl))
        };

        if (withRcRelease)
        {
            releases.Insert(0, CreateReleaseJson(
                RcVersion,
                prerelease: true,
                DateTimeOffset.UtcNow,
                CreateAssetJson(RcAssetApiUrl, RcAssetId, RcDownloadUrl),
                CreateManifestAssetJson(RcManifestAssetApiUrl, RcManifestAssetId, RcManifestDownloadUrl)));
        }

        return JsonSerializer.Serialize(releases);
    }

    private string RelativeToRoot(string path)
        => Path.GetRelativePath(TestRoot, path);
}
