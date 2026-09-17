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
        try { Directory.Delete(_wurzel, recursive: true); } catch (IOException) { }
    }

    /// <summary>Eine exakt passende URL liefert Status, Header (Link) und Inline-Body der Konfiguration.</summary>
    [Fact]
    public async Task SendAsync_LiefertKonfigurierteAntwortMitStatusUndHeadern()
    {
        SchreibeSzenario(new UpdateE2ESzenario
        {
            Antworten =
            [
                new UpdateE2EAntwort
                {
                    Url = ReleaseUrl,
                    Status = 200,
                    Inhalt = "[{\"tag_name\":\"v1.3.0\"}]",
                    Header = new Dictionary<string, string>
                    {
                        ["Link"] = "<https://api.github.test/page2>; rel=\"next\"",
                    },
                },
            ],
        });

        using var antwort = await _client.GetAsync(ReleaseUrl);

        Assert.Equal(HttpStatusCode.OK, antwort.StatusCode);
        Assert.Equal("[{\"tag_name\":\"v1.3.0\"}]", await antwort.Content.ReadAsStringAsync());
        Assert.Equal("<https://api.github.test/page2>; rel=\"next\"",
            antwort.Headers.GetValues("Link").Single());
        Assert.Equal("application/json", antwort.Content.Headers.ContentType?.MediaType);
        AssertProtokollEnthaelt(UpdateE2EEreignisse.HttpRequest);
        AssertProtokollEnthaelt(UpdateE2EEreignisse.HttpResponse);
    }

    /// <summary>Eine unbekannte URL scheitert mit HttpRequestException und protokolliert UnknownHttpRequest.</summary>
    [Fact]
    public async Task SendAsync_UnbekannteUrl_ScheitertOhneNetzwerkfallback()
    {
        var ex = await Assert.ThrowsAsync<HttpRequestException>(
            () => _client.GetAsync("https://api.github.test/andere/ressource"));

        Assert.Contains("Unbekannte Update-E2E-Anfrage", ex.Message);
        AssertProtokollEnthaelt(UpdateE2EEreignisse.UnknownHttpRequest);
    }

    /// <summary>Eine per <c>fault</c> markierte Antwort wirft den kontrollierten Transportfehler.</summary>
    [Fact]
    public async Task SendAsync_Transportfehler_WirftKontrollierteAusnahme()
    {
        SchreibeSzenario(new UpdateE2ESzenario
        {
            Antworten = [new UpdateE2EAntwort { Url = ReleaseUrl, Fehler = true }],
        });

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => _client.GetAsync(ReleaseUrl));
        Assert.Contains("Transportfehler", ex.Message);
    }

    /// <summary>Ein Antwort-Gate blockiert die Antwort bis zur <c>.release</c>-Datei.</summary>
    [Fact]
    public async Task SendAsync_AntwortGate_BlockiertBisZurFreigabe()
    {
        SchreibeSzenario(new UpdateE2ESzenario
        {
            Antworten = [new UpdateE2EAntwort { Url = ReleaseUrl, Inhalt = "ok", Gate = "releaseApi" }],
        });

        var anfrage = _client.GetAsync(ReleaseUrl);
        await Task.Delay(400);
        Assert.False(anfrage.IsCompleted, "Die Anfrage lieferte ohne Gate-Freigabe eine Antwort.");
        AssertProtokollEnthaelt(UpdateE2EEreignisse.HttpRequestBlocked);

        File.WriteAllText(Path.Combine(_wurzel, "gates", "releaseApi.release"), string.Empty);
        using var antwort = await anfrage;

        Assert.Equal(HttpStatusCode.OK, antwort.StatusCode);
        AssertProtokollEnthaelt(UpdateE2EEreignisse.HttpRequestReleased);
    }

    /// <summary>Ein Stream-Gate liefert die freien Bytes und blockiert erst beim nächsten Read.</summary>
    [Fact]
    public async Task SendAsync_StreamGate_LiefertTeilbytesUndBlockiertBeimNaechstenLesen()
    {
        var inhalt = new string('x', 1000);
        SchreibeSzenario(new UpdateE2ESzenario
        {
            Antworten =
            [
                new UpdateE2EAntwort
                {
                    Url = ReleaseUrl,
                    Inhalt = inhalt,
                    StreamGate = "download",
                    StreamGateBytes = 400,
                },
            ],
        });

        await using var stream = await _client.GetStreamAsync(ReleaseUrl);
        var puffer = new byte[1000];
        var gelesen = 0;

        // Erste Reads liefern genau die freien Bytes.
        while (gelesen < 400)
        {
            var n = await stream.ReadAsync(puffer.AsMemory(gelesen));
            Assert.True(n > 0);
            gelesen += n;
        }

        // Der nächste Read blockiert bis zur Gate-Freigabe.
        var blockierterRead = stream.ReadAsync(puffer.AsMemory(gelesen)).AsTask();
        await Task.Delay(400);
        Assert.False(blockierterRead.IsCompleted, "Der Stream lieferte ohne Gate-Freigabe weitere Bytes.");

        File.WriteAllText(Path.Combine(_wurzel, "gates", "download.release"), string.Empty);
        var restGelesen = gelesen + await blockierterRead;
        while (restGelesen < 1000)
            restGelesen += await stream.ReadAsync(puffer.AsMemory(restGelesen));

        Assert.Equal(inhalt, System.Text.Encoding.UTF8.GetString(puffer));
    }

    /// <summary>Eine Antwortdatei außerhalb der Testwurzel wird mit HttpRequestException abgelehnt.</summary>
    [Fact]
    public async Task SendAsync_AntwortdateiAusserhalbDerTestwurzel_WirdAbgelehnt()
    {
        var ausserhalb = Path.Combine(Path.GetTempPath(), $"ausserhalb-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(ausserhalb, "{}");
        try
        {
            SchreibeSzenario(new UpdateE2ESzenario
            {
                Antworten =
                [
                    new UpdateE2EAntwort
                    {
                        Url = ReleaseUrl,
                        BodyDatei = Path.Combine("..", Path.GetFileName(ausserhalb)),
                    },
                ],
            });

            var ex = await Assert.ThrowsAsync<HttpRequestException>(() => _client.GetAsync(ReleaseUrl));
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
        await File.WriteAllBytesAsync(inhaltPfad, [0x50, 0x4B, 0x03, 0x04]);
        SchreibeSzenario(new UpdateE2ESzenario
        {
            Antworten = [new UpdateE2EAntwort { Url = ReleaseUrl, BodyDatei = "payloads/release.zip" }],
        });

        using var antwort = await _client.GetAsync(ReleaseUrl);

        Assert.Equal("application/zip", antwort.Content.Headers.ContentType?.MediaType);
        Assert.Equal([0x50, 0x4B, 0x03, 0x04], await antwort.Content.ReadAsByteArrayAsync());
    }

    private void SchreibeSzenario(UpdateE2ESzenario szenario)
        => File.WriteAllText(
            _kontext.Konfiguration.SzenarioDateiPfad,
            JsonSerializer.Serialize(szenario));

    private void AssertProtokollEnthaelt(string ereignis)
    {
        var zeilen = File.ReadAllLines(_kontext.Konfiguration.ProtokollDateiPfad);
        Assert.Contains(zeilen, z => z.Contains($"\"event\":\"{ereignis}\"", StringComparison.Ordinal));
    }
}
