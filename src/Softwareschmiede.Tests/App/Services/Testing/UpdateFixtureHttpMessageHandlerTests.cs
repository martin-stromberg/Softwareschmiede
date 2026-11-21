using System.Net;
using System.Net.Http;
using System.Text.Json;
using Softwareschmiede.App.Services.Testing;

namespace Softwareschmiede.Tests.App.Services.Testing;

/// <summary>
/// Unit-Tests für den kontrollierten Update-HTTP-Handler der E2E-Fixture: exakte URL-Abbildung,
/// Header/Status, fehlender Netzwerkfallback, absichtliche Transportfehler, Antwort- und
/// Stream-Gates sowie die TestRoot-Isolation der Antwortdateien.
/// </summary>
public sealed class UpdateFixtureHttpMessageHandlerTests : IDisposable
{
    private const string ReleaseUrl = "https://api.github.test/repos/o/r/releases?per_page=10&page=1";

    private readonly string _root;
    private readonly UpdateE2ETestContext _context;
    private readonly HttpClient _client;

    /// <summary>Richtet eine frische Temp-TestRoot mit Szenario-, Protokoll- und Gate-Pfaden ein.</summary>
    public UpdateFixtureHttpMessageHandlerTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"swm-update-handler-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
        _context = new UpdateE2ETestContext(new UpdateE2ETestConfiguration
        {
            TestRoot = _root,
            ScenarioFilePath = Path.Combine(_root, "scenario.json"),
            LogFilePath = Path.Combine(_root, "protocol.jsonl"),
            ScenarioId = "handler-tests",
            GatesDirectory = Path.Combine(_root, "gates"),
        });
        _client = new HttpClient(new UpdateFixtureHttpMessageHandler(_context));
        WriteScenario(new UpdateE2EScenario());
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _client.Dispose();
        // Best-effort: Temp-Dateien können noch von einem noch nicht freigegebenen
        // Handle gehalten oder gesperrt sein (IOException/UnauthorizedAccessException).
        try { Directory.Delete(_root, recursive: true); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    /// <summary>Eine exakt passende URL liefert Status, Header (Link) und Inline-Body der Konfiguration.</summary>
    [Fact]
    public async Task SendAsync_ReturnsConfiguredResponseWithStatusAndHeaders()
    {
        WriteScenario(new UpdateE2EScenario
        {
            Answer =
            [
                new UpdateE2EResponse
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
        AssertLogContains(UpdateE2EEvents.HttpRequest);
        AssertLogContains(UpdateE2EEvents.HttpResponse);
    }

    /// <summary>Eine unbekannte URL scheitert mit HttpRequestException und protokolliert UnknownHttpRequest.</summary>
    [Fact]
    public async Task SendAsync_UnknownUrl_FailsWithoutNetworkfallback()
    {
        var ex = await Assert.ThrowsAsync<HttpRequestException>(
            () => _client.GetAsync("https://api.github.test/andere/ressource", TestContext.Current.CancellationToken));

        Assert.Contains("Unbekannte Update-E2E-Anfrage", ex.Message);
        AssertLogContains(UpdateE2EEvents.UnknownHttpRequest);
    }

    /// <summary>Eine GitHub-API-Asset-URL (<c>.../releases/assets/{id}</c>) wird als <c>asset</c>-Abruf protokolliert, nicht als Release-Abruf.</summary>
    [Fact]
    public async Task SendAsync_ClassifiesApiAssetUrlAsAsset()
    {
        var assetApiUrl = "https://api.github.com/repos/o/r/releases/assets/91001";
        WriteScenario(new UpdateE2EScenario
        {
            Answer = [new UpdateE2EResponse { Url = assetApiUrl, Content = "ok" }],
        });

        using var response = await _client.GetAsync(assetApiUrl, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var lines = File.ReadAllLines(_context.Configuration.LogFilePath);
        Assert.Contains(lines, z => z.Contains($"\"evt\":\"{UpdateE2EEvents.HttpRequest}\"", StringComparison.Ordinal)
            && z.Contains("\"art\":\"asset\"", StringComparison.Ordinal));
    }

    /// <summary>Eine <c>*.json</c>-Download-URL (Update-Manifest) wird als <c>manifest</c>-Abruf protokolliert.</summary>
    [Fact]
    public async Task SendAsync_ClassifiesManifestUrlAsManifest()
    {
        var manifestUrl = "https://e2e.invalid/szenario/update-1.3.0.json";
        WriteScenario(new UpdateE2EScenario
        {
            Answer = [new UpdateE2EResponse { Url = manifestUrl, Content = "{}" }],
        });

        using var response = await _client.GetAsync(manifestUrl, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var lines = File.ReadAllLines(_context.Configuration.LogFilePath);
        Assert.Contains(lines, z => z.Contains($"\"evt\":\"{UpdateE2EEvents.HttpRequest}\"", StringComparison.Ordinal)
            && z.Contains("\"art\":\"manifest\"", StringComparison.Ordinal));
    }

    /// <summary>Ein explizites <c>art</c> der Antwort überschreibt die URL-Klassifizierung (Manifest-API-Fallback-URL).</summary>
    [Fact]
    public async Task SendAsync_ResponseKind_OverwritesUrlClassification()
    {
        var manifestApiUrl = "https://api.github.com/repos/o/r/releases/assets/91011";
        WriteScenario(new UpdateE2EScenario
        {
            Answer = [new UpdateE2EResponse { Url = manifestApiUrl, Content = "{}", Kind = "manifest" }],
        });

        using var response = await _client.GetAsync(manifestApiUrl, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var lines = File.ReadAllLines(_context.Configuration.LogFilePath);
        Assert.Contains(lines, z => z.Contains($"\"evt\":\"{UpdateE2EEvents.HttpRequest}\"", StringComparison.Ordinal)
            && z.Contains("\"art\":\"manifest\"", StringComparison.Ordinal));
    }

    /// <summary>Eine per <c>fault</c> markierte Antwort wirft den kontrollierten Transportfehler.</summary>
    [Fact]
    public async Task SendAsync_Transporterror_ThrowsControlledException()
    {
        WriteScenario(new UpdateE2EScenario
        {
            Answer = [new UpdateE2EResponse { Url = ReleaseUrl, Error = true }],
        });

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => _client.GetAsync(ReleaseUrl, TestContext.Current.CancellationToken));
        Assert.Contains("Transportfehler", ex.Message);
    }

    /// <summary>Ein Antwort-Gate blockiert die Antwort bis zur <c>.release</c>-Datei.</summary>
    [Fact]
    public async Task SendAsync_ResponseGate_BlocksUntilToRelease()
    {
        WriteScenario(new UpdateE2EScenario
        {
            Answer = [new UpdateE2EResponse { Url = ReleaseUrl, Content = "ok", Gate = "releaseApi" }],
        });

        var request = _client.GetAsync(ReleaseUrl, TestContext.Current.CancellationToken);
        await Task.Delay(400, TestContext.Current.CancellationToken);
        Assert.False(request.IsCompleted, "Die Anfrage lieferte ohne Gate-Freigabe eine Antwort.");
        AssertLogContains(UpdateE2EEvents.HttpRequestBlocked);

        File.WriteAllText(Path.Combine(_root, "gates", "releaseApi.release"), string.Empty);
        using var response = await request;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertLogContains(UpdateE2EEvents.HttpRequestReleased);
    }

    /// <summary>Ein Stream-Gate liefert die freien Bytes und blockiert erst beim nächsten Read.</summary>
    [Fact]
    public async Task SendAsync_StreamGate_ReadReturnsPartialbytesAndBlocksAtNext()
    {
        var content = new string('x', 1000);
        WriteScenario(new UpdateE2EScenario
        {
            Answer =
            [
                new UpdateE2EResponse
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
        var read = 0;

        // Erste Reads liefern genau die freien Bytes.
        while (read < 400)
        {
            var n = await stream.ReadAsync(buffer.AsMemory(read), TestContext.Current.CancellationToken);
            Assert.True(n > 0);
            read += n;
        }

        // Der nächste Read blockiert bis zur Gate-Freigabe.
        var blockedRead = stream.ReadAsync(buffer.AsMemory(read), TestContext.Current.CancellationToken).AsTask();
        await Task.Delay(400, TestContext.Current.CancellationToken);
        Assert.False(blockedRead.IsCompleted, "Der Stream lieferte ohne Gate-Freigabe weitere Bytes.");

        File.WriteAllText(Path.Combine(_root, "gates", "download.release"), string.Empty);
        var remainderRead = read + await blockedRead;
        while (remainderRead < 1000)
            remainderRead += await stream.ReadAsync(buffer.AsMemory(remainderRead), TestContext.Current.CancellationToken);

        Assert.Equal(content, System.Text.Encoding.UTF8.GetString(buffer));
    }

    /// <summary>Eine Antwortdatei außerhalb der TestRoot wird mit HttpRequestException abgelehnt.</summary>
    [Fact]
    public async Task SendAsync_ResponsefileOutsideTheTestroot_WillRejected()
    {
        var outside = Path.Combine(Path.GetTempPath(), $"ausserhalb-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(outside, "{}", TestContext.Current.CancellationToken);
        try
        {
            WriteScenario(new UpdateE2EScenario
            {
                Answer =
                [
                    new UpdateE2EResponse
                    {
                        Url = ReleaseUrl,
                        BodyFile = Path.Combine("..", Path.GetFileName(outside)),
                    },
                ],
            });

            var ex = await Assert.ThrowsAsync<HttpRequestException>(() => _client.GetAsync(ReleaseUrl, TestContext.Current.CancellationToken));
            Assert.Contains("außerhalb der TestRoot", ex.Message);
        }
        finally
        {
            File.Delete(outside);
        }
    }

    /// <summary>Eine Antwortdatei innerhalb der TestRoot wird geladen (ZIP-Content-Type aus Endung).</summary>
    [Fact]
    public async Task SendAsync_ResponsefileWithinTheRoot_WillLoaded()
    {
        var contentPath = Path.Combine(_root, "payloads", "release.zip");
        Directory.CreateDirectory(Path.GetDirectoryName(contentPath)!);
        await File.WriteAllBytesAsync(contentPath, [0x50, 0x4B, 0x03, 0x04], TestContext.Current.CancellationToken);
        WriteScenario(new UpdateE2EScenario
        {
            Answer = [new UpdateE2EResponse { Url = ReleaseUrl, BodyFile = "payloads/release.zip" }],
        });

        using var response = await _client.GetAsync(ReleaseUrl, TestContext.Current.CancellationToken);

        Assert.Equal("application/zip", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal([0x50, 0x4B, 0x03, 0x04], await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
    }

    private void WriteScenario(UpdateE2EScenario scenario)
        => File.WriteAllText(
            _context.Configuration.ScenarioFilePath,
            JsonSerializer.Serialize(scenario));

    private void AssertLogContains(string evt)
    {
        var lines = File.ReadAllLines(_context.Configuration.LogFilePath);
        Assert.Contains(lines, z => z.Contains($"\"evt\":\"{evt}\"", StringComparison.Ordinal));
    }
}
