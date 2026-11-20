using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

namespace Softwareschmiede.App.Services.Testing;

/// <summary>
/// <see cref="HttpMessageHandler"/> für den dedizierten Update-HttpClient der E2E-Umgebung.
/// Bildet exakte Request-URLs auf konfigurierte HTTP-Status, Header (inkl. <c>Link</c>), JSON- und
/// ZIP-Antworten ab. Die Szenariodatei wird pro Request neu gelesen; Antworten und Streams können
/// über dateibasierte Gates blockieren. Unbekannte Requests scheitern ohne Netzwerkfallback.
/// </summary>
public sealed class UpdateFixtureHttpMessageHandler : HttpMessageHandler
{
    private static readonly TimeSpan StandardGateTimeout = TimeSpan.FromSeconds(120);

    private readonly UpdateE2ETestKontext _kontext;

    /// <inheritdoc cref="UpdateFixtureHttpMessageHandler"/>
    /// <param name="context">Der Laufzeitkontext der Update-E2E-Umgebung.</param>
    public UpdateFixtureHttpMessageHandler(UpdateE2ETestKontext context)
    {
        _kontext = context;
    }

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var url = request.RequestUri?.AbsoluteUri ?? string.Empty;

        var szenario = _kontext.LeseSzenario()
            ?? throw new HttpRequestException($"Update-E2E-Szenariodatei ist nicht lesbar: {_kontext.Configuration.SzenarioDateiPfad}");

        var e2eResponse = szenario.FindeAntwort(url);
        var art = e2eResponse?.Art ?? Klassifiziere(url);
        _kontext.Protokoll.Schreibe(
            UpdateE2EEreignisse.HttpRequest,
            new { url, method = request.Method.Method, art });

        if (e2eResponse is null)
        {
            _kontext.Protokoll.Schreibe(UpdateE2EEreignisse.UnknownHttpRequest, new { url });
            throw new HttpRequestException($"Unbekannte Update-E2E-Anfrage ohne konfigurierte Antwort: {url}");
        }

        if (e2eResponse.Error)
            throw new HttpRequestException($"Absichtlicher Update-E2E-Transportfehler für: {url}");

        if (!string.IsNullOrEmpty(e2eResponse.Gate))
        {
            _kontext.Protokoll.Schreibe(UpdateE2EEreignisse.HttpRequestBlocked, new { url, gate = e2eResponse.Gate });
            var freigegeben = await _kontext.WarteAufGateAsync(e2eResponse.Gate, cancellationToken, StandardGateTimeout);
            if (!freigegeben)
            {
                if (_kontext.PruefeGate(e2eResponse.Gate) == false)
                    throw new OperationCanceledException(cancellationToken);

                throw new HttpRequestException($"Update-E2E-Gate '{e2eResponse.Gate}' wurde nicht innerhalb von {StandardGateTimeout.TotalSeconds}s freigegeben: {url}");
            }

            _kontext.Protokoll.Schreibe(UpdateE2EEreignisse.HttpRequestReleased, new { url, gate = e2eResponse.Gate });
        }

        var content = LadeAntwortInhalt(e2eResponse);
        var contentType = e2eResponse.ContentType
            ?? (e2eResponse.BodyDatei?.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) == true
                ? "application/zip"
                : "application/json");

        Stream stream = new MemoryStream(content, writable: false);
        if (!string.IsNullOrEmpty(e2eResponse.StreamGate))
        {
            var freieBytes = e2eResponse.StreamGateBytes ?? content.Length / 2;
            stream = new GateBlockierterStream(stream, freieBytes, e2eResponse.StreamGate, _kontext);
        }

        var response = new HttpResponseMessage((HttpStatusCode)e2eResponse.Status)
        {
            RequestMessage = request,
            Content = new StreamContent(stream)
        };
        response.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        response.Content.Headers.ContentLength = content.Length;

        if (e2eResponse.Header is not null)
        {
            foreach (var (name, value) in e2eResponse.Header)
            {
                if (!response.Headers.TryAddWithoutValidation(name, value))
                    response.Content.Headers.TryAddWithoutValidation(name, value);
            }
        }

        _kontext.Protokoll.Schreibe(UpdateE2EEreignisse.HttpResponse, new { url, status = e2eResponse.Status });
        return response;
    }

    private byte[] LadeAntwortInhalt(UpdateE2EAntwort e2eResponse)
    {
        if (e2eResponse.Content is not null)
            return Encoding.UTF8.GetBytes(e2eResponse.Content);

        if (e2eResponse.BodyDatei is not null)
        {
            var path = Path.GetFullPath(Path.Combine(_kontext.Configuration.Testwurzel, e2eResponse.BodyDatei));
            var relativ = Path.GetRelativePath(Path.GetFullPath(_kontext.Configuration.Testwurzel), path);
            if (relativ == ".."
                || relativ.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || relativ.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal)
                || Path.IsPathRooted(relativ))
            {
                throw new HttpRequestException($"Update-E2E-Antwortdatei liegt außerhalb der Testwurzel: {e2eResponse.BodyDatei}");
            }

            return File.ReadAllBytes(path);
        }

        return [];
    }

    private static string Klassifiziere(string url)
    {
        if (url.Contains("/releases/assets/", StringComparison.OrdinalIgnoreCase))
            return "asset";
        if (url.Contains("/releases", StringComparison.OrdinalIgnoreCase))
            return "release";
        if (url.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            return "asset";
        if (url.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            return "manifest";
        return "sonstige";
    }

    /// <summary>
    /// Antwortstream, der nach <see cref="_freieBytes"/> gelieferten Bytes blockiert, bis das
    /// benannte Gate freigegeben (<c>.release</c>) oder abgebrochen (<c>.cancel</c>) wird.
    /// </summary>
    private sealed class GateBlockierterStream : Stream
    {
        private readonly Stream _inner;
        private readonly long _freieBytes;
        private readonly string _gate;
        private readonly UpdateE2ETestKontext _kontext;
        private long _position;
        private bool _gateErfuellt;

        public GateBlockierterStream(Stream inner, long freieBytes, string gate, UpdateE2ETestKontext context)
        {
            _inner = inner;
            _freieBytes = freieBytes;
            _gate = gate;
            _kontext = context;
        }

        /// <inheritdoc/>
        public override bool CanRead => true;

        /// <inheritdoc/>
        public override bool CanSeek => false;

        /// <inheritdoc/>
        public override bool CanWrite => false;

        /// <inheritdoc/>
        public override long Length => _inner.Length;

        /// <inheritdoc/>
        public override long Position
        {
            get => _position;
            set => throw new NotSupportedException();
        }

        /// <inheritdoc/>
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (!_gateErfuellt && _position < _freieBytes)
            {
                // Nur bis zur Schwelle liefern, damit echter Teilfortschritt sichtbar wird;
                // der nächste Leseaufruf blockiert dann bis zur Gate-Freigabe.
                var erlaubt = (int)Math.Min(buffer.Length, _freieBytes - _position);
                var freiGelesen = await _inner.ReadAsync(buffer[..erlaubt], cancellationToken);
                _position += freiGelesen;
                return freiGelesen;
            }

            await WarteFallsBlockiertAsync(cancellationToken);
            var gelesen = await _inner.ReadAsync(buffer, cancellationToken);
            _position += gelesen;
            return gelesen;
        }

        /// <inheritdoc/>
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (!_gateErfuellt && _position < _freieBytes)
            {
                var erlaubt = (int)Math.Min(count, _freieBytes - _position);
                var freiGelesen = _inner.Read(buffer, offset, erlaubt);
                _position += freiGelesen;
                return freiGelesen;
            }

            WarteFallsBlockiertAsync(CancellationToken.None).GetAwaiter().GetResult();
            var gelesen = _inner.Read(buffer, offset, count);
            _position += gelesen;
            return gelesen;
        }

        private async Task WarteFallsBlockiertAsync(CancellationToken ct)
        {
            if (_gateErfuellt)
                return;

            _kontext.Protokoll.Schreibe(UpdateE2EEreignisse.HttpRequestBlocked, new { gate = _gate });
            var freigegeben = await _kontext.WarteAufGateAsync(_gate, ct, StandardGateTimeout);
            if (!freigegeben)
            {
                // Echten Aufrufer-Abbruch von Runner-Abbruch (.cancel-Datei) und Timeout trennen:
                // Letztere sind keine Benutzer-Cancellation und müssen als Fehler sichtbar bleiben.
                ct.ThrowIfCancellationRequested();
                var abgebrochen = _kontext.PruefeGate(_gate) == false;
                throw new HttpRequestException(
                    $"Update-E2E-Stream-Gate '{_gate}' wurde {(abgebrochen ? "abgebrochen" : "nicht rechtzeitig freigegeben")}.");
            }

            _gateErfuellt = true;
            _kontext.Protokoll.Schreibe(UpdateE2EEreignisse.HttpRequestReleased, new { gate = _gate });
        }

        /// <inheritdoc/>
        public override void Flush() => _inner.Flush();

        /// <inheritdoc/>
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        /// <inheritdoc/>
        public override void SetLength(long value) => throw new NotSupportedException();

        /// <inheritdoc/>
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _inner.Dispose();
            base.Dispose(disposing);
        }
    }
}
