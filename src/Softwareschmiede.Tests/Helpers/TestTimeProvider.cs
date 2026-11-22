using System.Text.Json;

namespace Softwareschmiede.TestSupport;

/// <summary>Deterministische Zeitquelle für Tests: Die aktuelle Zeit ist über <see cref="Now"/>
/// direkt setzbar und lässt sich über <see cref="Advance"/> ohne echtes Warten vorstellen —
/// damit lassen sich Wartezeiten und Timeout-Pfade zeitgesteuerter Dienste prüfen.</summary>
internal sealed class TestTimeProvider : TimeProvider
{
    private DateTimeOffset _now = DateTimeOffset.UtcNow;

    /// <summary>Aktuelle Zeit der Testquelle; direkt setzbar für Zeitvorgaben ohne <see cref="Advance"/>.</summary>
    internal DateTimeOffset Now
    {
        get => _now;
        set => _now = value;
    }

    public override DateTimeOffset GetUtcNow() => _now;

    /// <summary>Stellt die Testquelle um <paramref name="span"/> vor.</summary>
    internal void Advance(TimeSpan span) => _now += span;
}

/// <summary>Gemeinsame JSON-Optionen (Web-Defaults) für Test-Hilfsmethoden, z. B. zum Schreiben
/// von SCM-Kanaldateien wie <c>accepted.json</c>/<c>complete.json</c>.</summary>
internal static class TestJson
{
    internal static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}
