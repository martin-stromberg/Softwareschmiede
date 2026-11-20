using System.Net;
using System.Net.Http;
using System.Text.Json;
using Softwareschmiede.App.Services.Testing;

namespace Softwareschmiede.Tests.App.Services.Testing;

/// <summary>
/// Unit-Tests für den kontrollierten Update-HTTP-Handler der E2E-Fixture: exakte URL-Abbildung,
/// Header/Status, fehlender Netzwerkfallback, absichtliche Transportfehler, Antwort- und
/// Stream-Gates sowie die Testwurzel-Isolation der Antwortdateien.
/// </summary>
public sealed class UpdateFixtureHttpMessageHandlerTests : IDisposable
{
    private const string ReleaseUrl = "https://api.github.test/repos/o/r/releases?per_page=10&page=1";

    private readonly string _wurzel;
    private readonly UpdateE2ETestKontext _kontext;
    private readonly HttpClient _client;

    /// <summary>Richtet eine frische Temp-Testwurzel mit Szenario-, Protokoll- und Gate-Pfaden ein.</summary>
    public UpdateFixtureHttpMessageHandlerTests()
    {
        _wurzel = Path.Combine(Path.GetTempPath(), $"swm-update-handler-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_wurzel);
        _kontext = new UpdateE2ETestKontext(new UpdateE2ETestConfiguration
        {
            Testwurzel = _wurzel,
            SzenarioDateiPfad = Path.Combine(_wurzel, "scenario.json"),
            ProtokollDateiPfad = Path.Combine(_wurzel, "protocol.jsonl"),
            SzenarioId = "handler-tests",
            GatesVerzeichnis = Path.Combine(_wurzel, "gates"),
        });
        _client = new HttpClient(new UpdateFixtureHttpMessageHandler(_kontext));
        SchreibeSzenario(new UpdateE2ESzenario());
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _client.Dispose();
        // Best-effort: Temp-Dateien können noch von einem noch nicht freigegebenen
        // Handle gehalten oder gesperrt sein (IOException/UnauthorizedAccessException).
        try { Directory.Delete(_wurzel, recursive: true); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    /// <summary>Eine exakt passende URL liefert Status, Header (Link) und Inline-Body der Konfiguration.</summary>
    [Fact]
    public async Task SendAsync_LiefertKonfigurierteAntwortMitStatusUndHeadern()
    {
        SchreibeSzenario(new UpdateE2ESzenario
        {
            Answer =
            [
                new UpdateE2EAntwort
                {
                    Url = ReleaseUrl,
                    Status = 200,
                    Content = "[{\"tag_name\":\"v1.3.0\"}]",
                    Header = new Dictionary<string, string>
                    {
                        ["Link"] = "<https://api.github.test/page2>; rel=\"next\"",
                    },
                },
            ],
        });

        using var response = await _client.GetAsync(ReleaseUrl, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("[{\"tag_name\":\"v1.3.0\"}]", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("<https://api.github.test/page2>; rel=\"next\"",
            response.Headers.GetValues("Link").Single());
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        AssertProtokollEnthaelt(UpdateE2EEreignisse.HttpRequest);
        AssertProtokollEnthaelt(UpdateE2EEreignisse.HttpResponse);
    }

    /// <summary>Eine unbekannte URL scheitert mit HttpRequestException und protokolliert UnknownHttpRequest.</summary>
    [Fact]
    public async Task SendAsync_UnbekannteUrl_ScheitertOhneNetzwerkfallback()
    {
        var ex = await Assert.ThrowsAsync<HttpRequestException>(
            () => _client.GetAsync("https://api.github.test/andere/ressource", TestContext.Current.CancellationToken));

        Assert.Contains("Unbekannte Update-E2E-Anfrage", ex.Message);
        AssertProtokollEnthaelt(UpdateE2EEreignisse.UnknownHttpRequest);
    }

    /// <summary>Eine GitHub-API-Asset-URL (<c>.../releases/assets/{id}</c>) wird als <c>asset</c>-Abruf protokolliert, nicht als Release-Abruf.</summary>
    [Fact]
    public async Task SendAsync_KlassifiziertApiAssetUrlAlsAsset()
    {
        var assetApiUrl = "https://api.github.com/repos/o/r/releases/assets/91001";
        SchreibeSzenario(new UpdateE2ESzenario
        {
            Answer = [new UpdateE2EAntwort { Url = assetApiUrl, Content = "ok" }],
        });

        using var response = await _client.GetAsync(assetApiUrl, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var lines = File.ReadAllLines(_kontext.Configuration.ProtokollDateiPfad);
        Assert.Contains(lines, z => z.Contains($"\"evt\":\"{UpdateE2EEreignisse.HttpRequest}\"", StringComparison.Ordinal)
            && z.Contains("\"art\":\"asset\"", StringComparison.Ordinal));
    }

    /// <summary>Eine <c>*.json</c>-Download-URL (Update-Manifest) wird als <c>manifest</c>-Abruf protokolliert.</summary>
    [Fact]
    public async Task SendAsync_KlassifiziertManifestUrlAlsManifest()
    {
        var manifestUrl = "https://e2e.invalid/szenario/update-1.3.0.json";
        SchreibeSzenario(new UpdateE2ESzenario
        {
            Answer = [new UpdateE2EAntwort { Url = manifestUrl, Content = "{}" }],
        });

        using var response = await _client.GetAsync(manifestUrl, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var lines = File.ReadAllLines(_kontext.Configuration.ProtokollDateiPfad);
        Assert.Contains(lines, z => z.Contains($"\"evt\":\"{UpdateE2EEreignisse.HttpRequest}\"", StringComparison.Ordinal)
            && z.Contains("\"art\":\"manifest\"", StringComparison.Ordinal));
    }

    /// <summary>Ein explizites <c>art</c> der Antwort überschreibt die URL-Klassifizierung (Manifest-API-Fallback-URL).</summary>
    [Fact]
    public async Task SendAsync_AntwortArt_UeberschreibtUrlKlassifizierung()
    {
        var manifestApiUrl = "https://api.github.com/repos/o/r/releases/assets/91011";
        SchreibeSzenario(new UpdateE2ESzenario
        {
            Answer = [new UpdateE2EAntwort { Url = manifestApiUrl, Content = "{}", Art = "manifest" }],
        });

        using var response = await _client.GetAsync(manifestApiUrl, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var lines = File.ReadAllLines(_kontext.Configuration.ProtokollDateiPfad);
        Assert.Contains(lines, z => z.Contains($"\"evt\":\"{UpdateE2EEreignisse.HttpRequest}\"", StringComparison.Ordinal)
            && z.Contains("\"art\":\"manifest\"", StringComparison.Ordinal));
    }

    /// <summary>Eine per <c>fault</c> markierte Antwort wirft den kontrollierten Transportfehler.</summary>
    [Fact]
    public async Task SendAsync_Transportfehler_WirftKontrollierteAusnahme()
    {
        SchreibeSzenario(new UpdateE2ESzenario
        {
            Answer = [new UpdateE2EAntwort { Url = ReleaseUrl, Error = true }],
        });

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => _client.GetAsync(ReleaseUrl, TestContext.Current.CancellationToken));
        Assert.Contains("Transportfehler", ex.Message);
    }

    /// <summary>Ein Antwort-Gate blockiert die Antwort bis zur <c>.release</c>-Datei.</summary>
    [Fact]
    public async Task SendAsync_AntwortGate_BlockiertBisZurFreigabe()
    {
        SchreibeSzenario(new UpdateE2ESzenario
        {
            Answer = [new UpdateE2EAntwort { Url = ReleaseUrl, Content = "ok", Gate = "releaseApi" }],
        });

        var request = _client.GetAsync(ReleaseUrl, TestContext.Current.CancellationToken);
        await Task.Delay(400, TestContext.Current.CancellationToken);
        Assert.False(request.IsCompleted, "Die Anfrage lieferte ohne Gate-Freigabe eine Antwort.");
        AssertProtokollEnthaelt(UpdateE2EEreignisse.HttpRequestBlocked);

        File.WriteAllText(Path.Combine(_wurzel, "gates", "releaseApi.release"), string.Empty);
        using var response = await request;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertProtokollEnthaelt(UpdateE2EEreignisse.HttpRequestReleased);
    }

    /// <summary>Ein Stream-Gate liefert die freien Bytes und blockiert erst beim nächsten Read.</summary>
    [Fact]
    public async Task SendAsync_StreamGate_LiefertTeilbytesUndBlockiertBeimNaechstenLesen()
    {
        var content = new string('x', 1000);
        SchreibeSzenario(new UpdateE2ESzenario
        {
            Answer =
            [
                new UpdateE2EAntwort
                {
                    Url = ReleaseUrl,
                    Content = content,
                    StreamGate = "download",
                    StreamGateBytes = 400,
                },
            ],
        });

        await using var stream = await _client.GetStreamAsync(ReleaseUrl, TestContext.Current.CancellationToken);
        var buffer = new byte[1000];
        var gelesen = 0;

        // Erste Reads liefern genau die freien Bytes.
        while (gelesen < 400)
        {
            var n = await stream.ReadAsync(buffer.AsMemory(gelesen), TestContext.Current.CancellationToken);
            Assert.True(n > 0);
            gelesen += n;
        }

        // Der nächste Read blockiert bis zur Gate-Freigabe.
        var blockierterRead = stream.ReadAsync(buffer.AsMemory(gelesen), TestContext.Current.CancellationToken).AsTask();
        await Task.Delay(400, TestContext.Current.CancellationToken);
        Assert.False(blockierterRead.IsCompleted, "Der Stream lieferte ohne Gate-Freigabe weitere Bytes.");

        File.WriteAllText(Path.Combine(_wurzel, "gates", "download.release"), string.Empty);
        var restGelesen = gelesen + await blockierterRead;
        while (restGelesen < 1000)
            restGelesen += await stream.ReadAsync(buffer.AsMemory(restGelesen), TestContext.Current.CancellationToken);

        Assert.Equal(content, System.Text.Encoding.UTF8.GetString(buffer));
    }

    /// <summary>Eine Antwortdatei außerhalb der Testwurzel wird mit HttpRequestException abgelehnt.</summary>
    [Fact]
    public async Task SendAsync_AntwortdateiAusserhalbDerTestwurzel_WirdAbgelehnt()
    {
        var ausserhalb = Path.Combine(Path.GetTempPath(), $"ausserhalb-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(ausserhalb, "{}", TestContext.Current.CancellationToken);
        try
        {
            SchreibeSzenario(new UpdateE2ESzenario
            {
                Answer =
                [
                    new UpdateE2EAntwort
                    {
                        Url = ReleaseUrl,
                        BodyDatei = Path.Combine("..", Path.GetFileName(ausserhalb)),
                    },
                ],
            });

            var ex = await Assert.ThrowsAsync<HttpRequestException>(() => _client.GetAsync(ReleaseUrl, TestContext.Current.CancellationToken));
            Assert.Contains("außerhalb der Testwurzel", ex.Message);
        }
        finally
        {
            File.Delete(ausserhalb);
        }
    }

    /// <summary>Eine Antwortdatei innerhalb der Testwurzel wird geladen (ZIP-Content-Type aus Endung).</summary>
    [Fact]
    public async Task SendAsync_AntwortdateiInnerhalbDerWurzel_WirdGeladen()
    {
        var inhaltPfad = Path.Combine(_wurzel, "payloads", "release.zip");
        Directory.CreateDirectory(Path.GetDirectoryName(inhaltPfad)!);
        await File.WriteAllBytesAsync(inhaltPfad, [0x50, 0x4B, 0x03, 0x04], TestContext.Current.CancellationToken);
        SchreibeSzenario(new UpdateE2ESzenario
        {
            Answer = [new UpdateE2EAntwort { Url = ReleaseUrl, BodyDatei = "payloads/release.zip" }],
        });

        using var response = await _client.GetAsync(ReleaseUrl, TestContext.Current.CancellationToken);

        Assert.Equal("application/zip", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal([0x50, 0x4B, 0x03, 0x04], await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
    }

    private void SchreibeSzenario(UpdateE2ESzenario szenario)
        => File.WriteAllText(
            _kontext.Configuration.SzenarioDateiPfad,
            JsonSerializer.Serialize(szenario));

    private void AssertProtokollEnthaelt(string evt)
    {
        var lines = File.ReadAllLines(_kontext.Configuration.ProtokollDateiPfad);
        Assert.Contains(lines, z => z.Contains($"\"evt\":\"{evt}\"", StringComparison.Ordinal));
    }
}
