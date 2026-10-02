using System.Text;

namespace Softwareschmiede.App.Services;

/// <summary>Formatiert Roh-CLI-Chunks für die Quell-Ansicht des Konsolentestfensters:
/// UTF-8-dekodiert, Steuerzeichen werden sichtbar gemacht (ESC → ␛, CR → \r, LF → \n,
/// TAB → \t, übrige Steuerzeichen → \xNN).</summary>
public static class CliChunkQuelltextFormatter
{
    /// <summary>Formatiert einen Roh-Chunk als lesbaren Quelltext.</summary>
    /// <param name="data">Der zu formatierende Roh-Chunk.</param>
    /// <returns>Den Quelltext mit sichtbar gemachten Steuersequenzen.</returns>
    public static string Formatiere(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
            return string.Empty;

        var text = Encoding.UTF8.GetString(data);
        var sb = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            switch (c)
            {
                case '\x1B':
                    sb.Append('␛');
                    break;
                case '\r':
                    sb.Append("\\r");
                    break;
                case '\n':
                    sb.Append("\\n");
                    break;
                case '\t':
                    sb.Append("\\t");
                    break;
                default:
                    if (c < ' ' || c == '\x7F')
                        sb.Append($"\\x{(int)c:X2}");
                    else
                        sb.Append(c);
                    break;
            }
        }

        return sb.ToString();
    }
}
