using System.Text;

namespace Softwareschmiede.Infrastructure.Terminal;

/// <summary>Serialisiert und deserialisiert <see cref="CliOutputRecording"/>-Instanzen im
/// <c>.clireplay</c>-Binärformat: Header (Magic, Version, Metadaten) gefolgt von
/// <c>[Int64 OffsetTicks][Int32 Length][Bytes]</c>-Records bis zum Stream-Ende.</summary>
public sealed class CliReplayRecordingStore
{
    private static readonly byte[] Magic = "SWCLRPLY"u8.ToArray();
    private const int SupportedVersion = 1;

    /// <summary>Schreibt die Aufzeichnung in den angegebenen Stream.</summary>
    /// <param name="stream">Der Zielstream (muss schreibbar sein).</param>
    /// <param name="recording">Die zu serialisierende Aufzeichnung.</param>
    /// <param name="ct">Abbruch-Token.</param>
    public async Task SaveAsync(Stream stream, CliOutputRecording recording, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(recording);

        // Serialisierung in einen Puffer, der eigentliche Stream-Zugriff bleibt async
        // (synchrone 8-MB-Schreibvorgänge würden den aufrufenden UI-Thread blockieren).
        using var buffer = new MemoryStream();
        var pluginNameBytes = Encoding.UTF8.GetBytes(recording.PluginName ?? string.Empty);

        using (var writer = new BinaryWriter(buffer, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(Magic);
            writer.Write(SupportedVersion);
            writer.Write(recording.WorkItemId.ToByteArray());
            writer.Write(recording.StartUtc.UtcTicks);
            writer.Write(recording.EndUtc?.UtcTicks ?? 0L);
            writer.Write(recording.Cols);
            writer.Write(recording.Rows);
            writer.Write(recording.IsComplete);
            writer.Write(pluginNameBytes.Length);
            writer.Write(pluginNameBytes);

            foreach (var chunk in recording.Chunks)
            {
                ct.ThrowIfCancellationRequested();
                writer.Write(chunk.Offset.Ticks);
                writer.Write(chunk.Data.Length);
                writer.Write(chunk.Data);
            }
        }

        buffer.Position = 0;
        await buffer.CopyToAsync(stream, ct).ConfigureAwait(false);
        await stream.FlushAsync(ct).ConfigureAwait(false);
    }

    /// <summary>Liest eine Aufzeichnung aus dem angegebenen Stream.</summary>
    /// <param name="stream">Der Quellstream (muss lesbar sein).</param>
    /// <param name="ct">Abbruch-Token.</param>
    /// <returns>Die deserialisierte Aufzeichnung.</returns>
    /// <exception cref="InvalidDataException">Das Format ist ungültig (Magic, Version, Header oder Record-Längen).</exception>
    public async Task<CliOutputRecording> LoadAsync(Stream stream, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        // Gesamten Inhalt async einlesen; das Parsing läuft auf dem gepufferten (seekbaren)
        // Stream, sodass Längenangaben stets gegen die Restlänge geprüft werden können.
        using var buffered = new MemoryStream();
        await stream.CopyToAsync(buffered, ct).ConfigureAwait(false);
        buffered.Position = 0;
        return Read(buffered, ct);
    }

    /// <summary>Schreibt die Aufzeichnung in die angegebene Datei.</summary>
    /// <param name="path">Der Ziel-Dateipfad.</param>
    /// <param name="recording">Die zu serialisierende Aufzeichnung.</param>
    /// <param name="ct">Abbruch-Token.</param>
    public async Task SaveAsync(string path, CliOutputRecording recording, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        await using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        await SaveAsync(stream, recording, ct).ConfigureAwait(false);
    }

    /// <summary>Liest eine Aufzeichnung aus der angegebenen Datei.</summary>
    /// <param name="path">Der Quell-Dateipfad.</param>
    /// <param name="ct">Abbruch-Token.</param>
    /// <returns>Die deserialisierte Aufzeichnung.</returns>
    /// <exception cref="InvalidDataException">Das Format ist ungültig.</exception>
    public async Task<CliOutputRecording> LoadAsync(string path, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        return await LoadAsync(stream, ct).ConfigureAwait(false);
    }

    private static CliOutputRecording Read(Stream stream, CancellationToken ct)
    {
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

        var magic = reader.ReadBytes(Magic.Length);
        if (magic.Length != Magic.Length || !magic.SequenceEqual(Magic))
            throw new InvalidDataException("Die Datei ist keine CLI-Aufzeichnung (ungültiges Magic).");

        int version;
        Guid workItemId;
        long startTicks;
        long endTicks;
        int cols;
        int rows;
        bool isComplete;
        int pluginNameLength;
        try
        {
            version = reader.ReadInt32();
            var guidBytes = reader.ReadBytes(16);
            if (guidBytes.Length != 16)
                throw new EndOfStreamException();
            workItemId = new Guid(guidBytes);
            startTicks = reader.ReadInt64();
            endTicks = reader.ReadInt64();
            cols = reader.ReadInt32();
            rows = reader.ReadInt32();
            isComplete = reader.ReadBoolean();
            pluginNameLength = reader.ReadInt32();
        }
        catch (EndOfStreamException ex)
        {
            throw new InvalidDataException("Die Aufzeichnung endet unerwartet im Header.", ex);
        }

        if (version != SupportedVersion)
            throw new InvalidDataException($"Nicht unterstützte Aufzeichnungs-Version {version} (unterstützt: {SupportedVersion}).");
        if (cols <= 0 || rows <= 0)
            throw new InvalidDataException($"Ungültige Terminal-Geometrie im Aufzeichnungs-Header ({cols}x{rows}).");
        if (pluginNameLength < 0 || pluginNameLength > stream.Length - stream.Position)
            throw new InvalidDataException("Ungültige PluginName-Länge im Aufzeichnungs-Header.");

        var pluginNameBytes = reader.ReadBytes(pluginNameLength);
        if (pluginNameBytes.Length != pluginNameLength)
            throw new InvalidDataException("Die Aufzeichnung endet unerwartet im PluginName-Header-Feld.");

        var chunks = new List<CliOutputChunkRecord>();
        while (true)
        {
            ct.ThrowIfCancellationRequested();

            // ReadBytes liefert bei Stream-Ende weniger Bytes statt zu werfen: 0 = reguläres
            // Dateiende zwischen zwei Records, 1..11 = abgebrochener Record-Header (korrupt).
            var headerBytes = reader.ReadBytes(sizeof(long) + sizeof(int));
            if (headerBytes.Length == 0)
                break;
            if (headerBytes.Length != sizeof(long) + sizeof(int))
                throw new InvalidDataException("Die Aufzeichnung endet unerwartet mitten in einem Chunk-Record-Header.");

            var offsetTicks = BitConverter.ToInt64(headerBytes, 0);
            var length = BitConverter.ToInt32(headerBytes, sizeof(long));
            if (length < 0 || length > stream.Length - stream.Position)
                throw new InvalidDataException($"Ungültige Chunk-Länge {length} im Aufzeichnungs-Record (übersteigt die Restlänge des Streams).");

            var data = reader.ReadBytes(length);
            if (data.Length != length)
                throw new InvalidDataException("Die Aufzeichnung endet unerwartet mitten in einem Chunk-Record.");

            chunks.Add(new CliOutputChunkRecord(TimeSpan.FromTicks(offsetTicks), data));
        }

        return new CliOutputRecording
        {
            WorkItemId = workItemId,
            PluginName = Encoding.UTF8.GetString(pluginNameBytes),
            StartUtc = new DateTimeOffset(startTicks, TimeSpan.Zero),
            EndUtc = endTicks == 0 ? null : new DateTimeOffset(endTicks, TimeSpan.Zero),
            Cols = cols,
            Rows = rows,
            IsComplete = isComplete,
            Chunks = chunks,
        };
    }
}
