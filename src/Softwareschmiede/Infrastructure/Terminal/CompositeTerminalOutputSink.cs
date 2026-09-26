namespace Softwareschmiede.Infrastructure.Terminal;

/// <summary>Fächert eine <see cref="ITerminalOutputSink"/> auf mehrere innere Senken auf
/// (z. B. Protokoll-Writer + Rohbyte-Recorder). <see cref="ITerminalDiagnoseSink.OnDiagnoseChunk"/>
/// wird nur an innere Senken weitergereicht, die <see cref="ITerminalDiagnoseSink"/> implementieren,
/// damit Diagnose-Marker nicht in byte-exakte Mitschnitte gelangen.</summary>
public sealed class CompositeTerminalOutputSink : ITerminalOutputSink, ITerminalDiagnoseSink
{
    private readonly IReadOnlyList<ITerminalOutputSink> _inner;
    private readonly IReadOnlyList<ITerminalDiagnoseSink> _diagnoseSinks;

    /// <summary>Erstellt eine Composite-Senke über die übergebenen inneren Senken.</summary>
    /// <param name="inner">Die inneren Senken in Aufrufreihenfolge.</param>
    public CompositeTerminalOutputSink(params ITerminalOutputSink[] inner)
    {
        _inner = inner;
        _diagnoseSinks = inner.OfType<ITerminalDiagnoseSink>().ToArray();
    }

    /// <inheritdoc/>
    public void OnOutputChunk(ReadOnlySpan<byte> bytes)
    {
        foreach (var sink in _inner)
            sink.OnOutputChunk(bytes);
    }

    /// <inheritdoc/>
    public void OnDiagnoseChunk(ReadOnlySpan<byte> bytes)
    {
        foreach (var sink in _diagnoseSinks)
            sink.OnDiagnoseChunk(bytes);
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
