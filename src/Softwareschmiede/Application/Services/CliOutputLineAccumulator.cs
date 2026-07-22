using System.Text;

namespace Softwareschmiede.Application.Services;

/// <summary>Dekodiert Terminal-Output chunkuebergreifend und segmentiert ihn in Protokollzeilen.</summary>
public sealed class CliOutputLineAccumulator
{
    private readonly Decoder _decoder = Encoding.UTF8.GetDecoder();
    private readonly StringBuilder _currentLine = new();
    private bool _skipNextLf;

    /// <summary>Verarbeitet einen Byte-Chunk und gibt dabei abgeschlossene Zeilen zurueck.</summary>
    /// <param name="bytes">UTF-8-kodierte Terminalausgabe.</param>
    /// <returns>Abgeschlossene Ausgabezeilen in Stream-Reihenfolge.</returns>
    public IReadOnlyList<string> Append(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty)
            return Array.Empty<string>();

        var charCount = _decoder.GetCharCount(bytes, flush: false);
        if (charCount == 0)
            return Array.Empty<string>();

        var chars = new char[charCount];
        _decoder.GetChars(bytes, chars, flush: false);
        return AppendText(chars);
    }

    /// <summary>Schliesst die Dekodierung ab und liefert eine vorhandene Restzeile.</summary>
    /// <returns>Die letzte unvollstaendige Zeile oder keine Zeile, wenn kein Inhalt aussteht.</returns>
    public IReadOnlyList<string> Flush()
    {
        var charCount = _decoder.GetCharCount(ReadOnlySpan<byte>.Empty, flush: true);
        var lines = new List<string>();

        if (charCount > 0)
        {
            var chars = new char[charCount];
            _decoder.GetChars(ReadOnlySpan<byte>.Empty, chars, flush: true);
            lines.AddRange(AppendText(chars));
        }

        if (_currentLine.Length > 0)
        {
            lines.Add(_currentLine.ToString());
            _currentLine.Clear();
        }

        _skipNextLf = false;
        return lines;
    }

    private IReadOnlyList<string> AppendText(ReadOnlySpan<char> text)
    {
        var lines = new List<string>();

        foreach (var c in text)
        {
            if (_skipNextLf)
            {
                _skipNextLf = false;
                if (c == '\n')
                    continue;
            }

            if (c == '\r')
            {
                lines.Add(_currentLine.ToString());
                _currentLine.Clear();
                _skipNextLf = true;
                continue;
            }

            if (c == '\n')
            {
                lines.Add(_currentLine.ToString());
                _currentLine.Clear();
                continue;
            }

            _currentLine.Append(c);
        }

        return lines;
    }
}
