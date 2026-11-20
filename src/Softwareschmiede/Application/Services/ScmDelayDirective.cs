using System.Text.RegularExpressions;

namespace Softwareschmiede.Application.Services;

/// <summary>Wertet die konfigurierbare Verzögerungs-Direktive in SCM-Ereignistexten aus.
/// Die Direktive wirkt nur im selben Text wie der Trigger und wird ausschließlich bei
/// der Ereignisaufnahme gelesen.</summary>
public static class ScmDelayDirective
{
    /// <summary>Standard-Verzögerungs-Keyword, wenn kein abweichender Wert konfiguriert ist
    /// (Einstellung <see cref="AppSettingService.ScmDelayKeywordKey"/>).</summary>
    public const string DefaultDelayKeyword = "#verzögert";

    /// <summary>Maximal zulässige Verzögerung bis zum Zielzeitpunkt.</summary>
    public static readonly TimeSpan MaxDelay = TimeSpan.FromDays(32);

    private const string QuoteChars = "'\"`´‚‘’„“”«»";

    private static readonly Regex DurationPattern = new(@"^(\d+)(min|h|t|d|w)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex MonthDayPattern = new(@"^m(\d{1,2})$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly char[] TrailingPunctuation = ['.', ',', ';', ':', '!', '?', ')', ']', '}'];

    /// <summary>Ergebnisart der Direktivenauswertung.</summary>
    public enum ResultKind
    {
        /// <summary>Keine frei stehende Direktive im Text.</summary>
        None,
        /// <summary>Gültige Direktive mit Zielzeitpunkt.</summary>
        Deferred,
        /// <summary>Direktive erkannt, Angabe jedoch verworfen.</summary>
        Invalid
    }

    /// <summary>Auswertung einer Direktive: kein Vorkommen, gültiger Zielzeitpunkt oder
    /// verworfene Angabe samt Begründung.</summary>
    public readonly record struct Result(ResultKind Kind, DateTimeOffset TargetUtc, string? Reason, string? Argument)
    {
        /// <summary>Keine Direktive im Text gefunden.</summary>
        public static Result None { get; } = new(ResultKind.None, default, null, null);
        /// <summary>Gültige Direktive: die Zustellung ruht bis <paramref name="targetUtc"/>.</summary>
        public static Result Deferred(DateTimeOffset targetUtc, string argument) => new(ResultKind.Deferred, targetUtc, null, argument);
        /// <summary>Verworfene Direktive samt Begründung und erkanntem Argument.</summary>
        public static Result Invalid(string reason, string? argument) => new(ResultKind.Invalid, default, reason, argument);
    }

    /// <summary>Normalisiert ein konfiguriertes Verzögerungs-Keyword: Leere oder nur aus
    /// Leerraum bestehende Werte fallen auf <see cref="DefaultDelayKeyword"/> zurück.</summary>
    public static string NormalizeDelayKeyword(string? delayKeyword)
        => string.IsNullOrWhiteSpace(delayKeyword) ? DefaultDelayKeyword : delayKeyword.Trim();

    /// <summary>Sucht die erste frei stehende Direktive und bewertet ihre Zeitangabe.
    /// <paramref name="now"/> ist die lokale Ereigniszeit; relative Angaben werden als
    /// verstrichene Zeitspanne, <c>m&lt;N&gt;</c> kalendergenau in der lokalen Zone gelesen.</summary>
    /// <param name="body">Der Ereignistext (Beschreibung oder Kommentar).</param>
    /// <param name="delayKeyword">Das konfigurierte Verzögerungs-Keyword; leere Werte fallen
    /// auf <see cref="DefaultDelayKeyword"/> zurück.</param>
    /// <param name="now">Der lokale Auswertungszeitpunkt.</param>
    public static Result Parse(string? body, string? delayKeyword, DateTimeOffset now)
    {
        if (string.IsNullOrEmpty(body)) return Result.None;
        var keyword = NormalizeDelayKeyword(delayKeyword);
        var index = FreeStandingKeywordIndex(body, keyword, 0);
        if (index < 0) return Result.None;
        var rest = body[(index + keyword.Length)..];
        var argument = FirstToken(rest);
        if (argument is null)
            return Result.Invalid("Die Verzögerungsangabe fehlt.", null);
        var token = argument.TrimEnd(TrailingPunctuation);
        var duration = DurationPattern.Match(token);
        if (duration.Success)
        {
            if (!long.TryParse(duration.Groups[1].Value, out var amount) || amount <= 0)
                return Result.Invalid($"Die Verzögerungsdauer '{token}' muss positiv sein.", token);
            // Die Tagesrechnung vor der TimeSpan-Bildung verhindert Überläufe bei
            // beliebig großen Angaben aus entfernten Texten.
            var days = amount * (duration.Groups[2].Value.ToLowerInvariant() switch
            {
                "min" => 1.0 / 1440,
                "h" => 1.0 / 24,
                "t" or "d" => 1.0,
                _ => 7.0
            });
            if (days > MaxDelay.TotalDays)
                return Result.Invalid($"Die Verzögerung '{token}' überschreitet das Maximum von 32 Tagen.", token);
            return Result.Deferred(now + TimeSpan.FromDays(days), token);
        }
        var monthDay = MonthDayPattern.Match(token);
        if (monthDay.Success)
        {
            var day = int.Parse(monthDay.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            if (day < 1 || day > 31)
                return Result.Invalid($"Der Monatstag '{token}' muss zwischen m1 und m31 liegen.", token);
            if (TryMonthDayTarget(now, day, out var target))
            {
                return target - now > MaxDelay
                    ? Result.Invalid($"Der Zielzeitpunkt für '{token}' liegt mehr als 32 Tage in der Zukunft.", token)
                    : Result.Deferred(target, token);
            }
            return Result.Invalid($"Für '{token}' findet sich kein erreichbarer Monatstag.", token);
        }
        return Result.Invalid($"Die Verzögerungsangabe '{token}' ist ungültig. Erlaubt sind <n>min, <n>h, <n>t/<n>d, <n>w und m<Tag 1-31>.", token);
    }

    /// <summary>Erster zukünftige Zeitpunkt am Tag <paramref name="day"/> eines Monats zur
    /// lokalen Tageszeit von <paramref name="now"/>; ein im laufenden Monat bereits
    /// erreichter oder nicht existierender Tag rückt in den Folgemonat.</summary>
    private static bool TryMonthDayTarget(DateTimeOffset now, int day, out DateTimeOffset target)
    {
        var year = now.Year;
        var month = now.Month;
        for (var i = 0; i < 24; i++)
        {
            if (day <= DateTime.DaysInMonth(year, month) && (i > 0 || day > now.Day))
            {
                target = new DateTimeOffset(year, month, day, now.Hour, now.Minute, now.Second, now.Offset);
                return true;
            }
            if (++month > 12) { month = 1; year++; }
        }
        target = default;
        return false;
    }

    /// <summary>Das erste nichtleere Token hinter dem Keyword; führender Leerraum wird
    /// übersprungen, das Token endet am nächsten Leerraum.</summary>
    private static string? FirstToken(string rest)
    {
        var span = rest.AsSpan().TrimStart();
        if (span.IsEmpty) return null;
        var end = 0;
        while (end < span.Length && !char.IsWhiteSpace(span[end])) end++;
        return (end < span.Length ? span[..end] : span).ToString();
    }

    /// <summary>Position des nächsten frei stehenden Keywords; unmittelbar zitierte Vorkommen
    /// zählen nicht, und hinter dem Keyword muss Leerraum oder Textende stehen.</summary>
    private static int FreeStandingKeywordIndex(string body, string keyword, int startIndex)
    {
        var index = startIndex - 1;
        while ((index = body.IndexOf(keyword, index + 1, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            var before = index > 0 ? body[index - 1] : '\0';
            var afterIndex = index + keyword.Length;
            var after = afterIndex < body.Length ? body[afterIndex] : '\0';
            if (!QuoteChars.Contains(before) && !QuoteChars.Contains(after)
                && (afterIndex >= body.Length || char.IsWhiteSpace(after)))
                return index;
        }
        return -1;
    }
}
