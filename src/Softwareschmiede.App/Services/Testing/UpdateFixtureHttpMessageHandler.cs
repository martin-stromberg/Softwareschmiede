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
    private static readonly TimeSpan DefaultGateTimeout = TimeSpan.FromSeconds(120);

    private readonly UpdateE2ETestContext _context;

    /// <inheritdoc cref="UpdateFixtureHttpMessageHandler"/>
    /// <param name="context">Der Laufzeitkontext der Update-E2E-Umgebung.</param>
    public UpdateFixtureHttpMessageHandler(UpdateE2ETestContext context)
    {
        _context = context;
    }

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var url = request.RequestUri?.AbsoluteUri ?? string.Empty;

        var scenario = _context.ReadScenario()
            ?? throw new HttpRequestException($"Update-E2E-Szenariodatei ist nicht lesbar: {_context.Configuration.ScenarioFilePath}");

        var e2eResponse = scenario.FindResponse(url);
        var kind = e2eResponse?.Kind ?? Klassifiziere(url);
        _context.Log.Write(
            UpdateE2EEvents.HttpRequest,
            new { url, method = request.Method.Method, art = kind });

        if (e2eResponse is null)
        {
            _context.Log.Write(UpdateE2EEvents.UnknownHttpRequest, new { url });
            throw new HttpRequestException($"Unbekannte Update-E2E-Anfrage ohne konfigurierte Antwort: {url}");
        }

        if (e2eResponse.Error)
            throw new HttpRequestException($"Absichtlicher Update-E2E-Transportfehler für: {url}");

        if (!string.IsNullOrEmpty(e2eResponse.Gate))
        {
            _context.Log.Write(UpdateE2EEvents.HttpRequestBlocked, new { url, gate = e2eResponse.Gate });
            var released = await _context.WaitOnGateAsync(e2eResponse.Gate, cancellationToken, DefaultGateTimeout);
            if (!released)
            {
                if (_context.CheckGate(e2eResponse.Gate) == false)
                    throw new OperationCanceledException(cancellationToken);

                throw new HttpRequestException($"Update-E2E-Gate '{e2eResponse.Gate}' wurde nicht innerhalb von {DefaultGateTimeout.TotalSeconds}s freigegeben: {url}");
            }

            _context.Log.Write(UpdateE2EEvents.HttpRequestReleased, new { url, gate = e2eResponse.Gate });
        }

        var content = LoadResponseContent(e2eResponse);
        var contentType = e2eResponse.ContentType
            ?? (e2eResponse.BodyFile?.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) == true
                ? "application/zip"
                : "application/json");

        Stream stream = new MemoryStream(content, writable: false);
        if (!string.IsNullOrEmpty(e2eResponse.StreamGate))
        {
            var freeBytes = e2eResponse.StreamGateBytes ?? content.Length / 2;
            stream = new GateBlockedStream(stream, freeBytes, e2eResponse.StreamGate, _context);
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

        _context.Log.Write(UpdateE2EEvents.HttpResponse, new { url, status = e2eResponse.Status });
        return response;
    }

    private byte[] LoadResponseContent(UpdateE2EResponse e2eResponse)
    {
        if (e2eResponse.Content is not null)
            return Encoding.UTF8.GetBytes(e2eResponse.Content);

        if (e2eResponse.BodyFile is not null)
        {
            var path = Path.GetFullPath(Path.Combine(_context.Configuration.Testwurzel, e2eResponse.BodyFile));
            var relativ = Path.GetRelativePath(Path.GetFullPath(_context.Configuration.Testwurzel), path);
            if (relativ == ".."
                || relativ.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || relativ.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal)
                || Path.IsPathRooted(relativ))
            {
                throw new HttpRequestException($"Update-E2E-Antwortdatei liegt außerhalb der Testwurzel: {e2eResponse.BodyFile}");
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
    /// Antwortstream, der nach <see cref="_freeBytes"/> gelieferten Bytes blockiert, bis das
    /// benannte Gate freigegeben (<c>.release</c>) oder abgebrochen (<c>.cancel</c>) wird.
    /// </summary>
    private sealed class GateBlockedStream : Stream
    {
        private readonly Stream _inner;
        private readonly long _freeBytes;
        private readonly string _gate;
        private readonly UpdateE2ETestContext _context;
        private long _position;
        private bool _gateErfuellt;

        public GateBlockedStream(Stream inner, long freeBytes, string gate, UpdateE2ETestContext context)
        {
            _inner = inner;
            _freeBytes = freeBytes;
            _gate = gate;
            _context = context;
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
            if (!_gateErfuellt && _position < _freeBytes)
            {
                // Nur bis zur Schwelle liefern, damit echter Teilfortschritt sichtbar wird;
                // der nächste Leseaufruf blockiert dann bis zur Gate-Freigabe.
                var allowed = (int)Math.Min(buffer.Length, _freeBytes - _position);
                var freeRead = await _inner.ReadAsync(buffer[..allowed], cancellationToken);
                _position += freeRead;
                return freeRead;
            }

            await WaitCaseBlockedAsync(cancellationToken);
            var read = await _inner.ReadAsync(buffer, cancellationToken);
            _position += read;
            return read;
        }

        /// <inheritdoc/>
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (!_gateErfuellt && _position < _freeBytes)
            {
                var allowed = (int)Math.Min(count, _freeBytes - _position);
                var freeRead = _inner.Read(buffer, offset, allowed);
                _position += freeRead;
                return freeRead;
            }

            WaitCaseBlockedAsync(CancellationToken.None).GetAwaiter().GetResult();
            var read = _inner.Read(buffer, offset, count);
            _position += read;
            return read;
        }

        private async Task WaitCaseBlockedAsync(CancellationToken ct)
        {
            if (_gateErfuellt)
                return;

            _context.Log.Write(UpdateE2EEvents.HttpRequestBlocked, new { gate = _gate });
            var released = await _context.WaitOnGateAsync(_gate, ct, DefaultGateTimeout);
            if (!released)
            {
                // Echten Aufrufer-Abbruch von Runner-Abbruch (.cancel-Datei) und Timeout trennen:
                // Letztere sind keine Benutzer-Cancellation und müssen als Fehler sichtbar bleiben.
                ct.ThrowIfCancellationRequested();
                var canceled = _context.CheckGate(_gate) == false;
                throw new HttpRequestException(
                    $"Update-E2E-Stream-Gate '{_gate}' wurde {(canceled ? "abgebrochen" : "nicht rechtzeitig freigegeben")}.");
            }

            _gateErfuellt = true;
            _context.Log.Write(UpdateE2EEvents.HttpRequestReleased, new { gate = _gate });
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
