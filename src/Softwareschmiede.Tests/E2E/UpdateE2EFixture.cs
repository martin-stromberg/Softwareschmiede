using System.IO.Compression;
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
/// innerhalb der Testwurzel.
/// </summary>
public sealed class UpdateE2EFixture : IDisposable
{
    /// <summary>Standardmäßig simulierte installierte Version.</summary>
    public const string InstallierteVersion = "1.2.0";

    /// <summary>Version des simulierten Stable-Releases (neuer als <see cref="InstallierteVersion"/>).</summary>
    public const string StableVersion = "1.3.0";

    /// <summary>Version des simulierten Release-Candidates (neuer als <see cref="StableVersion"/>).</summary>
    public const string RcVersion = "1.4.0-rc.1";

    /// <summary>Release-Listendaten-Dateiname relativ zur Testwurzel.</summary>
    public const string ReleaseListDateiName = "releases.json";

    /// <summary>Der Release-API-Endpunkt, den <c>GitHubReleaseClient</c> mit den Standard-<see cref="UpdateOptions"/> anfragt.</summary>
    public static readonly string ReleaseApiUrl =
        $"https://api.github.com/repos/{new UpdateOptions().RepositoryOwner}/{new UpdateOptions().RepositoryName}/releases?per_page=100";

    private static readonly JsonSerializerOptions JsonOptionen = new() { WriteIndented = true };

    private readonly UpdateE2ESzenario _szenario = new();
    private readonly object _szenarioLock = new();
    private bool _disposed;

    /// <inheritdoc cref="UpdateE2EFixture"/>
    public UpdateE2EFixture()
    {
        SzenarioId = $"update-e2e-{Guid.NewGuid():N}";
        Testwurzel = Path.Combine(Path.GetTempPath(), $"softwareschmiede_update_e2e_{Guid.NewGuid():N}");
        InstalliertesVerzeichnis = Path.Combine(Testwurzel, "installed");
        GatesVerzeichnis = Path.Combine(Testwurzel, "gates");
        PaketVerzeichnis = Path.Combine(Testwurzel, "packages");
        SzenarioDateiPfad = Path.Combine(Testwurzel, "scenario.json");
        ProtokollDateiPfad = Path.Combine(Testwurzel, "events.jsonl");
        KonfigurationDateiPfad = Path.Combine(Testwurzel, "testconfig.json");
        DatenbankPfad = Path.Combine(Testwurzel, "test.db");

        Directory.CreateDirectory(InstalliertesVerzeichnis);
        Directory.CreateDirectory(GatesVerzeichnis);
        Directory.CreateDirectory(PaketVerzeichnis);

        SchreibeInstallierteVersion(InstallierteVersion);

        // Deutlich unterscheidbare, eindeutige Download-URLs; niemals echte Release-Quellen.
        StableDownloadUrl = $"https://e2e.invalid/{SzenarioId}/release-{StableVersion}.zip";
        RcDownloadUrl = $"https://e2e.invalid/{SzenarioId}/release-{RcVersion}.zip";
        StableZipPfad = Path.Combine(PaketVerzeichnis, $"release-{StableVersion}.zip");
        RcZipPfad = Path.Combine(PaketVerzeichnis, $"release-{RcVersion}.zip");

        ErstellePaketZip(StableZipPfad, StableVersion, "stable-marker");
        ErstellePaketZip(RcZipPfad, RcVersion, "rc-marker");

        SetzeStandardAntworten();
        SpeichereSzenario();
        SchreibeKonfiguration();
    }

    /// <summary>Eindeutige Szenario-/Prozess-Identität für jeden Protokolleintrag.</summary>
    public string SzenarioId { get; }

    /// <summary>Eindeutiges Temp-Wurzelverzeichnis dieser Fixture.</summary>
    public string Testwurzel { get; }

    /// <summary>Simuliertes Installationsverzeichnis mit der <c>version.json</c>.</summary>
    public string InstalliertesVerzeichnis { get; }

    /// <summary>Verzeichnis der dateibasierten Freigabe-/Abbruch-Gates.</summary>
    public string GatesVerzeichnis { get; }

    /// <summary>Verzeichnis der erzeugten ZIP-Pakete.</summary>
    public string PaketVerzeichnis { get; }

    /// <summary>Pfad der pro Request/Lesebefehl neu gelesenen Szenariodatei.</summary>
    public string SzenarioDateiPfad { get; }

    /// <summary>Pfad der strukturierten JSONL-Protokolldatei.</summary>
    public string ProtokollDateiPfad { get; }

    /// <summary>Pfad der Testkonfigurations-JSON (Wert von <c>SOFTWARESCHMIEDE_UPDATE_TEST_CONFIG</c>).</summary>
    public string KonfigurationDateiPfad { get; }

    /// <summary>Pfad der fixture-eigenen SQLite-Testdatenbank.</summary>
    public string DatenbankPfad { get; }

    /// <summary>Download-URL des Stable-Pakets.</summary>
    public string StableDownloadUrl { get; }

    /// <summary>Download-URL des RC-Pakets.</summary>
    public string RcDownloadUrl { get; }

    /// <summary>Pfad der Stable-ZIP-Datei.</summary>
    public string StableZipPfad { get; }

    /// <summary>Pfad der RC-ZIP-Datei.</summary>
    public string RcZipPfad { get; }

    /// <summary>
    /// Liefert die prozessbezogenen Umgebungsvariablen für den App-Start:
    /// <c>SOFTWARESCHMIEDE_UPDATE_TEST_CONFIG</c> und <c>SOFTWARESCHMIEDE_TEST_DB_PATH</c>.
    /// </summary>
    /// <returns>Die Umgebungsvariablen für <c>WpfTestBase.LaunchApp</c>.</returns>
    public IReadOnlyDictionary<string, string?> ErzeugeStartUmgebung()
        => new Dictionary<string, string?>
        {
            [UpdateE2ETestConfiguration.UmgebungsVariable] = KonfigurationDateiPfad,
            [UpdateE2ETestConfiguration.TestDatenbankUmgebungsVariable] = DatenbankPfad
        };

    /// <summary>
    /// Setzt die Standard-Antworten: Der Release-Endpunkt liefert die Release-Liste mit Stable- und
    /// RC-Release (sofortige Antwort), beide Paket-URLs liefern die zugehörigen ZIP-Dateien.
    /// </summary>
    /// <param name="mitRcRelease">Bei <c>false</c> enthält die Release-Liste nur das Stable-Release.</param>
    public void SetzeStandardAntworten(bool mitRcRelease = true)
    {
        _szenario.Antworten.Clear();
        _szenario.Antworten.Add(new UpdateE2EAntwort
        {
            Url = ReleaseApiUrl,
            Status = 200,
            Inhalt = ErzeugeReleaseListenJson(mitRcRelease)
        });
        _szenario.Antworten.Add(new UpdateE2EAntwort
        {
            Url = StableDownloadUrl,
            Status = 200,
            BodyDatei = RelativZurWurzel(StableZipPfad)
        });
        _szenario.Antworten.Add(new UpdateE2EAntwort
        {
            Url = RcDownloadUrl,
            Status = 200,
            BodyDatei = RelativZurWurzel(RcZipPfad)
        });
    }

    /// <summary>Mutiert das Szenario thread-sicher und schreibt die Szenariodatei anschließend neu.</summary>
    /// <param name="mutation">Die Änderung am Szenario.</param>
    public void AktualisiereSzenario(Action<UpdateE2ESzenario> mutation)
    {
        lock (_szenarioLock)
        {
            mutation(_szenario);
            SpeichereSzenario();
        }
    }

    /// <summary>Schreibt das aktuelle Szenario in die Szenariodatei.</summary>
    public void SpeichereSzenario()
    {
        lock (_szenarioLock)
        {
            File.WriteAllText(SzenarioDateiPfad, JsonSerializer.Serialize(_szenario, JsonOptionen));
        }
    }

    /// <summary>
    /// Aktiviert den kontrollierten Settings-Lesefehler für die gewählte Lesegrenze.
    /// </summary>
    /// <param name="grenze"><c>Initial</c>, <c>BeforePreparation</c> oder <c>BeforeUpdaterStart</c>.</param>
    /// <param name="versuch">Versuchsauswahl (<c>any</c>, <c>startup</c>, <c>manuell</c>, <c>pruefen</c>, <c>starten</c>).</param>
    /// <param name="aufFreigabeWarten">Bei <c>true</c> blockiert der betroffene Read bis zur Gate-Freigabe.</param>
    /// <param name="wartezeitSekunden">Maximale Gate-Wartezeit in Sekunden.</param>
    public void AktiviereLesefehler(
        string grenze,
        string versuch = "any",
        bool aufFreigabeWarten = true,
        int wartezeitSekunden = 30)
    {
        AktualisiereSzenario(s => s.Lesefehler = new UpdateE2ELesefehler
        {
            Aktiv = true,
            Versuch = versuch,
            Grenze = grenze,
            AufFreigabeWarten = aufFreigabeWarten,
            WartezeitSekunden = wartezeitSekunden
        });
    }

    /// <summary>
    /// Deaktiviert den kontrollierten Settings-Lesefehler ausdrücklich und protokolliert
    /// <see cref="UpdateE2EEreignisse.UpdateSettingsReadFailureDisabled"/> als Runner-Ereignis.
    /// </summary>
    public void DeaktiviereLesefehler()
    {
        AktualisiereSzenario(s => s.Lesefehler = new UpdateE2ELesefehler { Aktiv = false });
        UpdateE2EProtokoll.Anfuegen(
            ProtokollDateiPfad, SzenarioId, UpdateE2EEreignisse.UpdateSettingsReadFailureDisabled);
    }

    /// <summary>Schreibt die Freigabe-Datei <c>{gate}.release</c> für ein blockiertes Gate.</summary>
    /// <param name="gate">Der Gate-Name.</param>
    public void OeffneGate(string gate)
        => File.WriteAllText(Path.Combine(GatesVerzeichnis, $"{gate}.release"), "release");

    /// <summary>Schreibt die Abbruch-Datei <c>{gate}.cancel</c> für ein blockiertes Gate.</summary>
    /// <param name="gate">Der Gate-Name.</param>
    public void BrecheGateAb(string gate)
        => File.WriteAllText(Path.Combine(GatesVerzeichnis, $"{gate}.cancel"), "cancel");

    /// <summary>Entfernt beide Gate-Dateien eines Gates wieder.</summary>
    /// <param name="gate">Der Gate-Name.</param>
    public void SetzeGateZurueck(string gate)
    {
        var release = Path.Combine(GatesVerzeichnis, $"{gate}.release");
        var cancel = Path.Combine(GatesVerzeichnis, $"{gate}.cancel");
        if (File.Exists(release))
            File.Delete(release);
        if (File.Exists(cancel))
            File.Delete(cancel);
    }

    /// <summary>
    /// Erstellt die Fixture-Datenbank (EF-Migration) und persistiert die Update-Einstellungen
    /// darin. Für Vorbedingungen vor dem ersten App-Start.
    /// </summary>
    /// <param name="modus">Der zu speichernde Update-Modus.</param>
    /// <param name="includePrereleases">Die zu speichernde Prerelease-Auswahl.</param>
    public async Task SetzeUpdateEinstellungenAsync(UpdateMode modus, bool includePrereleases)
    {
        var options = new DbContextOptionsBuilder<SoftwareschmiededDbContext>()
            .UseSqlite($"Data Source={DatenbankPfad}")
            .Options;

        await using var db = new SoftwareschmiededDbContext(options);
        await db.Database.MigrateAsync();
        var service = new AppEinstellungService(db, NullLogger<AppEinstellungService>.Instance);
        await service.SetUpdateSettingsAsync(new UpdateSettings(modus, includePrereleases));
    }

    /// <summary>
    /// Persistiert Update-Einstellungen in der bereits bestehenden Fixture-Datenbank
    /// (für Änderungen zwischen zwei App-Läufen bei erhaltener Datenbank).
    /// </summary>
    /// <param name="modus">Der zu speichernde Update-Modus.</param>
    /// <param name="includePrereleases">Die zu speichernde Prerelease-Auswahl.</param>
    public async Task AktualisiereUpdateEinstellungenAsync(UpdateMode modus, bool includePrereleases)
    {
        var options = new DbContextOptionsBuilder<SoftwareschmiededDbContext>()
            .UseSqlite($"Data Source={DatenbankPfad}")
            .Options;

        await using var db = new SoftwareschmiededDbContext(options);
        var service = new AppEinstellungService(db, NullLogger<AppEinstellungService>.Instance);
        await service.SetUpdateSettingsAsync(new UpdateSettings(modus, includePrereleases));
    }

    /// <summary>Liest alle bisher geschriebenen JSONL-Protokolleinträge.</summary>
    /// <returns>Die Protokolleinträge in Dateireihenfolge.</returns>
    public IReadOnlyList<UpdateE2EProtokollEintrag> LeseProtokoll()
    {
        if (!File.Exists(ProtokollDateiPfad))
            return [];

        var eintraege = new List<UpdateE2EProtokollEintrag>();
        string inhalt;
        using (var stream = new FileStream(ProtokollDateiPfad, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        using (var reader = new StreamReader(stream))
        {
            inhalt = reader.ReadToEnd();
        }

        foreach (var zeile in inhalt.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var eintrag = JsonSerializer.Deserialize<UpdateE2EProtokollEintrag>(zeile.Trim());
            if (eintrag is not null)
                eintraege.Add(eintrag);
        }

        return eintraege;
    }

    /// <summary>
    /// Wartet begrenzt, bis mindestens <paramref name="mindestens"/> Protokolleinträge mit dem
    /// Ereignisnamen (und optionalem Filter) vorliegen, und liefert die Treffer.
    /// </summary>
    /// <param name="ereignis">Der Ereignisname.</param>
    /// <param name="timeout">Maximale Wartezeit.</param>
    /// <param name="mindestens">Mindestanzahl der Treffer.</param>
    /// <param name="filter">Optionaler Filter auf den Nutzdaten-Eintrag.</param>
    /// <returns>Die gefundenen Einträge.</returns>
    /// <exception cref="TimeoutException">Die Ereignisse erschienen nicht rechtzeitig.</exception>
    public async Task<IReadOnlyList<UpdateE2EProtokollEintrag>> WarteAufEreignisAsync(
        string ereignis,
        TimeSpan timeout,
        int mindestens = 1,
        Func<UpdateE2EProtokollEintrag, bool>? filter = null)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var treffer = LeseProtokoll()
                .Where(e => e.Ereignis == ereignis && (filter is null || filter(e)))
                .ToList();
            if (treffer.Count >= mindestens)
                return treffer;

            await Task.Delay(100);
        }

        var protokoll = LeseProtokoll();
        var gleichnamig = protokoll
            .Where(e => e.Ereignis == ereignis)
            .Select(e => e.Daten is null ? "(ohne Daten)" : JsonSerializer.Serialize(e.Daten));
        throw new TimeoutException(
            $"Protokollereignis '{ereignis}' (mindestens {mindestens}) erschien nicht innerhalb von {timeout.TotalSeconds}s. " +
            $"Bisherige Ereignisse: {string.Join(", ", protokoll.Select(e => e.Ereignis).Distinct())}. " +
            $"Vorhandene '{ereignis}'-Daten: {string.Join(" | ", gleichnamig)}");
    }

    /// <summary>Liefert die Anzahl der Protokolleinträge eines Ereignisnamens.</summary>
    /// <param name="ereignis">Der Ereignisname.</param>
    /// <returns>Die Anzahl der Einträge.</returns>
    public int ZaehleEreignis(string ereignis)
        => LeseProtokoll().Count(e => e.Ereignis == ereignis);

    /// <summary>Liefert den <c>data</c>-Wert eines Protokolleintrags als <see cref="JsonElement"/>.</summary>
    /// <param name="eintrag">Der Protokolleintrag.</param>
    /// <returns>Das Nutzdaten-Element oder <c>null</c>.</returns>
    public static JsonElement? Daten(UpdateE2EProtokollEintrag eintrag)
        => eintrag.Daten as JsonElement? ?? (eintrag.Daten is null ? null : JsonSerializer.SerializeToElement(eintrag.Daten));

    /// <summary>
    /// Erzeugt eine Release-Paket-ZIP mit Root-Einträgen <c>Softwareschmiede.exe</c> (unterscheidbarer
    /// Markerinhalt) und <c>version.json</c> passend zu <paramref name="version"/>.
    /// </summary>
    /// <param name="zipPfad">Zielpfad der ZIP-Datei.</param>
    /// <param name="version">Die Paketversion.</param>
    /// <param name="marker">Unterscheidbarer Inhaltsmarker der Paket-Exe.</param>
    public static void ErstellePaketZip(string zipPfad, string version, string marker)
    {
        if (File.Exists(zipPfad))
            File.Delete(zipPfad);

        var executableName = new UpdateOptions().ExecutableName;
        using var archiv = ZipFile.Open(zipPfad, ZipArchiveMode.Create);

        var exe = archiv.CreateEntry(executableName);
        using (var writer = new StreamWriter(exe.Open()))
            writer.Write($"E2E-Paket {version} ({marker})");

        var versionJson = archiv.CreateEntry("version.json");
        using (var writer = new StreamWriter(versionJson.Open()))
            writer.Write($$"""{"version":"{{version}}","tagName":"v{{version}}"}""");
    }

    /// <summary>Liest den Markerinhalt der Paket-Exe aus einem entpackten Verzeichnis.</summary>
    /// <param name="verzeichnis">Das entpackte Paketverzeichnis.</param>
    /// <returns>Der Inhalt der Paket-Exe oder <c>null</c>.</returns>
    public static string? LesePaketExeInhalt(string verzeichnis)
    {
        var pfad = Path.Combine(verzeichnis, new UpdateOptions().ExecutableName);
        return File.Exists(pfad) ? File.ReadAllText(pfad) : null;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        // Fehlersteuerung sicher zurücksetzen, auch wenn der Test mittendrin abbricht.
        try { DeaktiviereLesefehler(); } catch { /* Testwurzel evtl. schon entfernt. */ }

        // Diagnose-Fluchtventil: Mit SOFTWARESCHMIEDE_UPDATE_E2E_KEEP=1 bleibt die Testwurzel
        // (Szenario, Gates, JSONL-Protokoll) zur Fehleranalyse erhalten.
        if (Environment.GetEnvironmentVariable("SOFTWARESCHMIEDE_UPDATE_E2E_KEEP") == "1")
            return;

        for (var versuch = 0; versuch < 5; versuch++)
        {
            try
            {
                if (Directory.Exists(Testwurzel))
                    Directory.Delete(Testwurzel, recursive: true);
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

    private void SchreibeInstallierteVersion(string version)
    {
        File.WriteAllText(
            Path.Combine(InstalliertesVerzeichnis, "version.json"),
            $$"""{"version":"{{version}}","tagName":"v{{version}}"}""");
    }

    private void SchreibeKonfiguration()
    {
        var konfiguration = new UpdateE2ETestConfiguration
        {
            Testwurzel = Testwurzel,
            SzenarioDateiPfad = SzenarioDateiPfad,
            ProtokollDateiPfad = ProtokollDateiPfad,
            SzenarioId = SzenarioId,
            GatesVerzeichnis = GatesVerzeichnis
        };

        File.WriteAllText(KonfigurationDateiPfad, JsonSerializer.Serialize(konfiguration, JsonOptionen));
    }

    private string ErzeugeReleaseListenJson(bool mitRcRelease)
    {
        var releases = new List<object>
        {
            new
            {
                tag_name = $"v{StableVersion}",
                prerelease = false,
                draft = false,
                published_at = DateTimeOffset.UtcNow.AddDays(-7),
                assets = new[]
                {
                    new { name = new UpdateOptions().AssetName, browser_download_url = StableDownloadUrl }
                }
            }
        };

        if (mitRcRelease)
        {
            releases.Insert(0, new
            {
                tag_name = $"v{RcVersion}",
                prerelease = true,
                draft = false,
                published_at = DateTimeOffset.UtcNow,
                assets = new[]
                {
                    new { name = new UpdateOptions().AssetName, browser_download_url = RcDownloadUrl }
                }
            });
        }

        return JsonSerializer.Serialize(releases);
    }

    private string RelativZurWurzel(string pfad)
        => Path.GetRelativePath(Testwurzel, pfad);
}
