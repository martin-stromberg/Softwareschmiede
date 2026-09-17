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
    /// <param name="kontext">Der Laufzeitkontext der Update-E2E-Umgebung.</param>
    public UpdateFixtureHttpMessageHandler(UpdateE2ETestKontext kontext)
    {
        _kontext = kontext;
    }

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var url = request.RequestUri?.AbsoluteUri ?? string.Empty;
        var art = Klassifiziere(url);
        _kontext.Protokoll.Schreibe(
            UpdateE2EEreignisse.HttpRequest,
            new { url, method = request.Method.Method, art });

        var szenario = _kontext.LeseSzenario()
            ?? throw new HttpRequestException($"Update-E2E-Szenariodatei ist nicht lesbar: {_kontext.Konfiguration.SzenarioDateiPfad}");

        var antwort = szenario.FindeAntwort(url);
        if (antwort is null)
        {
            _kontext.Protokoll.Schreibe(UpdateE2EEreignisse.UnknownHttpRequest, new { url });
            throw new HttpRequestException($"Unbekannte Update-E2E-Anfrage ohne konfigurierte Antwort: {url}");
        }

        if (antwort.Fehler)
            throw new HttpRequestException($"Absichtlicher Update-E2E-Transportfehler für: {url}");

        if (!string.IsNullOrEmpty(antwort.Gate))
        {
            _kontext.Protokoll.Schreibe(UpdateE2EEreignisse.HttpRequestBlocked, new { url, gate = antwort.Gate });
            var freigegeben = await _kontext.WarteAufGateAsync(antwort.Gate, cancellationToken, StandardGateTimeout);
            if (!freigegeben)
            {
                if (_kontext.PruefeGate(antwort.Gate) == false)
                    throw new OperationCanceledException(cancellationToken);

                throw new HttpRequestException($"Update-E2E-Gate '{antwort.Gate}' wurde nicht innerhalb von {StandardGateTimeout.TotalSeconds}s freigegeben: {url}");
            }

            _kontext.Protokoll.Schreibe(UpdateE2EEreignisse.HttpRequestReleased, new { url, gate = antwort.Gate });
        }

        var inhalt = LadeAntwortInhalt(antwort);
        var contentType = antwort.ContentType
            ?? (antwort.BodyDatei?.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) == true
                ? "application/zip"
                : "application/json");

        Stream stream = new MemoryStream(inhalt, writable: false);
        if (!string.IsNullOrEmpty(antwort.StreamGate))
        {
            var freieBytes = antwort.StreamGateBytes ?? inhalt.Length / 2;
            stream = new GateBlockierterStream(stream, freieBytes, antwort.StreamGate, _kontext);
        }

        var response = new HttpResponseMessage((HttpStatusCode)antwort.Status)
        {
            RequestMessage = request,
            Content = new StreamContent(stream)
        };
        response.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        response.Content.Headers.ContentLength = inhalt.Length;

        if (antwort.Header is not null)
        {
            foreach (var (name, wert) in antwort.Header)
            {
                if (!response.Headers.TryAddWithoutValidation(name, wert))
                    response.Content.Headers.TryAddWithoutValidation(name, wert);
            }
        }

        _kontext.Protokoll.Schreibe(UpdateE2EEreignisse.HttpResponse, new { url, status = antwort.Status });
        return response;
    }

    private byte[] LadeAntwortInhalt(UpdateE2EAntwort antwort)
    {
        if (antwort.Inhalt is not null)
            return Encoding.UTF8.GetBytes(antwort.Inhalt);

        if (antwort.BodyDatei is not null)
        {
            var pfad = Path.GetFullPath(Path.Combine(_kontext.Konfiguration.Testwurzel, antwort.BodyDatei));
            var relativ = Path.GetRelativePath(Path.GetFullPath(_kontext.Konfiguration.Testwurzel), pfad);
            if (relativ == ".."
                || relativ.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || relativ.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal)
                || Path.IsPathRooted(relativ))
            {
                throw new HttpRequestException($"Update-E2E-Antwortdatei liegt außerhalb der Testwurzel: {antwort.BodyDatei}");
            }

            return File.ReadAllBytes(pfad);
        }

        return [];
    }

    private static string Klassifiziere(string url)
    {
        if (url.Contains("/releases", StringComparison.OrdinalIgnoreCase))
            return "release";
        if (url.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            return "asset";
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

        public GateBlockierterStream(Stream inner, long freieBytes, string gate, UpdateE2ETestKontext kontext)
        {
            _inner = inner;
            _freieBytes = freieBytes;
            _gate = gate;
            _kontext = kontext;
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
                throw new OperationCanceledException(ct);

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
