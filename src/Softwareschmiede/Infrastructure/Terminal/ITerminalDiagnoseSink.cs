namespace Softwareschmiede.Infrastructure.Terminal;

/// <summary>Zusätzlicher Routing-Kanal einer <see cref="ITerminalOutputSink"/> für
/// <c>[Terminal-Diagnose]</c>-Markerzeilen: Senken, die echte CLI-Rohbytes aufzeichnen
/// (z. B. <see cref="CliOutputRecorder"/>), implementieren dieses Interface bewusst nicht, damit die
/// artefaktischen Markerzeilen nicht in den byte-exakten Mitschnitt gelangen. Senken ohne das
/// Interface erhalten Marker weiter über <see cref="ITerminalOutputSink.OnOutputChunk"/>.</summary>
public interface ITerminalDiagnoseSink
{
    /// <summary>Meldet eine Diagnose-Markerzeile (keine echte CLI-Ausgabe).</summary>
    /// <param name="bytes">Die UTF-8-kodierte Markerzeile.</param>
    void OnDiagnoseChunk(ReadOnlySpan<byte> bytes);
}
