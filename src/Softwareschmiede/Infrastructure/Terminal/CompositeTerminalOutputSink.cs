namespace Softwareschmiede.Infrastructure.Terminal;

/// <summary>Fächert eine <see cref="ITerminalOutputSink"/> auf mehrere innere Senken auf
/// (z. B. Protokoll-Writer + Rohbyte-Recorder). <see cref="ITerminalDiagnosticsSink.OnDiagnosticsChunk"/>
/// wird nur an innere Senken weitergereicht, die <see cref="ITerminalDiagnosticsSink"/> implementieren,
/// damit Diagnose-Marker nicht in byte-exakte Mitschnitte gelangen.</summary>
public sealed class CompositeTerminalOutputSink : ITerminalOutputSink, ITerminalDiagnosticsSink
{
    private readonly IReadOnlyList<ITerminalOutputSink> _inner;
    private readonly IReadOnlyList<ITerminalDiagnosticsSink> _diagnoseSinks;

    /// <summary>Erstellt eine Composite-Senke über die übergebenen inneren Senken.</summary>
    /// <param name="inner">Die inneren Senken in Aufrufreihenfolge.</param>
    public CompositeTerminalOutputSink(params ITerminalOutputSink[] inner)
    {
        _inner = inner;
        _diagnoseSinks = inner.OfType<ITerminalDiagnosticsSink>().ToArray();
    }

    /// <inheritdoc/>
    public void OnOutputChunk(ReadOnlySpan<byte> bytes)
    {
        foreach (var sink in _inner)
            sink.OnOutputChunk(bytes);
    }

    /// <inheritdoc/>
    public void OnDiagnosticsChunk(ReadOnlySpan<byte> bytes)
    {
        foreach (var sink in _diagnoseSinks)
            sink.OnDiagnosticsChunk(bytes);
    }

    /// <inheritdoc/>
    public void Complete()
    {
        foreach (var sink in _inner)
            sink.Complete();
    }

    /// <inheritdoc/>
    public async Task CompleteAsync(TimeSpan timeout, CancellationToken ct = default)
    {
        foreach (var sink in _inner)
            await sink.CompleteAsync(timeout, ct).ConfigureAwait(false);
    }
}
