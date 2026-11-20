using FluentAssertions;
using Softwareschmiede.Application.Services;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>Tests der Direktivensyntax in <see cref="ScmDelayDirective"/>: frei stehendes
/// Keyword, relative Angaben, Monatstag-Formel und 32-Tage-Deckel.</summary>
public sealed class ScmDelayDirectiveTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 20, 9, 30, 0, TimeSpan.FromHours(1));

    /// <summary>Texte ohne Keyword erzeugen keine Direktive.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("$Softwareschmiede bitte übernehmen")]
    [InlineData("$Softwareschmiede #verzögert4h")] // Keyword klebt am Argument
    [InlineData("$Softwareschmiede '#verzögert 4h'")] // zitiertes Vorkommen
    public void Parse_ReturnsNone_WithoutFreeStandingKeyword(string? body)
    {
        ScmDelayDirective.Parse(body, null, Now).Kind.Should().Be(ScmDelayDirective.ResultKind.None);
    }

    /// <summary>Relative Angaben werden als verstrichene Zeitspanne zum Zielzeitpunkt.</summary>
    [Theory]
    [InlineData("30min", 0, 0, 30)]
    [InlineData("4h", 0, 4, 0)]
    [InlineData("2t", 2, 0, 0)]
    [InlineData("2d", 2, 0, 0)]
    [InlineData("1w", 7, 0, 0)]
    [InlineData("32t", 32, 0, 0)]
    [InlineData("46080min", 32, 0, 0)] // exakt 32 Tage am Maximum
    public void Parse_ReturnsDeferred_AtRelativeSpecification(string specification, int day, int hours, int minutes)
    {
        var result = ScmDelayDirective.Parse($"$Softwareschmiede #verzögert {specification} bitte", null, Now);
        result.Kind.Should().Be(ScmDelayDirective.ResultKind.Deferred);
        result.TargetUtc.Should().Be(Now + new TimeSpan(day, hours, minutes, 0));
        result.Argument.Should().Be(specification);
    }

    /// <summary>Keyword und Einheit werden unabhängig von der Schreibweise erkannt.</summary>
    [Fact]
    public void Parse_IgnoresLargeLowercase()
    {
        var result = ScmDelayDirective.Parse("$Softwareschmiede #VERZÖGERT 4H", null, Now);
        result.Kind.Should().Be(ScmDelayDirective.ResultKind.Deferred);
        result.TargetUtc.Should().Be(Now.AddHours(4));
    }

    /// <summary>Überzogene, nicht positive oder unlesbare Angaben werden verworfen.</summary>
    [Theory]
    [InlineData("#verzögert 33t")]
    [InlineData("#verzögert 5w")]
    [InlineData("#verzögert 0h")]
    [InlineData("#verzögert 50000min")]
    [InlineData("#verzögert 99999999999999d")]
    [InlineData("#verzögert 99999999999999999999h")]
    [InlineData("#verzögert m0")]
    [InlineData("#verzögert m32")]
    [InlineData("#verzögert bald")]
    [InlineData("#verzögert")]
    public void Parse_ReturnsInvalid_AtInvalidSpecification(string directive)
    {
        var result = ScmDelayDirective.Parse($"$Softwareschmiede {directive}", null, Now);
        result.Kind.Should().Be(ScmDelayDirective.ResultKind.Invalid);
        result.Reason.Should().NotBeNullOrWhiteSpace();
    }

    /// <summary>Abschließende Satzzeichen hinter der Angabe werden toleriert.</summary>
    [Fact]
    public void Parse_ToleratesPunctuationBehindTheSpecification()
    {
        var result = ScmDelayDirective.Parse("$Softwareschmiede (#verzögert 4h).", null, Now);
        result.Kind.Should().Be(ScmDelayDirective.ResultKind.Deferred);
        result.TargetUtc.Should().Be(Now.AddHours(4));
    }

    /// <summary>Ein späterer Monatstag im laufenden Monat bleibt im Monat.</summary>
    [Fact]
    public void Parse_Monthday_InRunningMonth()
    {
        // Now: 20.11.2026 — der 25. liegt noch vor uns.
        var result = ScmDelayDirective.Parse("$Softwareschmiede #verzögert m25", null, Now);
        result.Kind.Should().Be(ScmDelayDirective.ResultKind.Deferred);
        result.TargetUtc.Should().Be(new DateTimeOffset(2026, 11, 25, 9, 30, 0, Now.Offset));
    }

    /// <summary>Der Monatstag gilt als erreicht, wenn er dem aktuellen Tag entspricht
    /// oder vor ihm liegt — dann zählt der Folgemonat.</summary>
    [Theory]
    [InlineData("m1", 2026, 12, 1)]   // 1.11. liegt hinter dem 20.11.
    [InlineData("m20", 2026, 12, 20)] // heute erreicht → Folgemonat
    public void Parse_Monthday_InNextmonth(string specification, int year, int month, int tag)
    {
        var result = ScmDelayDirective.Parse($"$Softwareschmiede #verzögert {specification}", null, Now);
        result.Kind.Should().Be(ScmDelayDirective.ResultKind.Deferred);
        result.TargetUtc.Should().Be(new DateTimeOffset(year, month, tag, 9, 30, 0, Now.Offset));
    }

    /// <summary>Über den Jahreswechsel zählt der Januar des Folgejahres.</summary>
    [Fact]
    public void Parse_Monthday_OverYearturn()
    {
        var dez = new DateTimeOffset(2026, 12, 20, 9, 30, 0, Now.Offset);
        var result = ScmDelayDirective.Parse("$Softwareschmiede #verzögert m1", null, dez);
        result.Kind.Should().Be(ScmDelayDirective.ResultKind.Deferred);
        result.TargetUtc.Should().Be(new DateTimeOffset(2027, 1, 1, 9, 30, 0, Now.Offset));
    }

    /// <summary>Ein Monatstag, dessen nächster Termin über 32 Tage entfernt liegt
    /// (z. B. m31 im April), wird verworfen.</summary>
    [Fact]
    public void Parse_Monthday_BeyondOfMaximums()
    {
        var april = new DateTimeOffset(2027, 4, 10, 9, 30, 0, Now.Offset); // nächster 31. = 31.05. (51 Tage)
        var result = ScmDelayDirective.Parse("$Softwareschmiede #verzögert m31", null, april);
        result.Kind.Should().Be(ScmDelayDirective.ResultKind.Invalid);
        // Ebenso: m31 am 20.11. → 31.12. liegt 41 Tage entfernt.
        ScmDelayDirective.Parse("$Softwareschmiede #verzögert m31", null, Now).Kind
            .Should().Be(ScmDelayDirective.ResultKind.Invalid);
    }

    /// <summary>Monate ohne den Monatstag werden übersprungen (m31 im Februar → März).</summary>
    [Fact]
    public void Parse_Monthday_SkipsMonthWithoutTag()
    {
        var februar = new DateTimeOffset(2027, 2, 27, 9, 30, 0, Now.Offset); // 2027 ist kein Schaltjahr
        var result = ScmDelayDirective.Parse("$Softwareschmiede #verzögert m28", null, februar);
        result.Kind.Should().Be(ScmDelayDirective.ResultKind.Deferred);
        result.TargetUtc.Should().Be(new DateTimeOffset(2027, 2, 28, 9, 30, 0, Now.Offset));
    }

    /// <summary>Die erste frei stehende Direktive im Text gewinnt.</summary>
    [Fact]
    public void Parse_TakesFirstDirective()
    {
        var result = ScmDelayDirective.Parse("$Softwareschmiede #verzögert 4h und später #verzögert 2t", null, Now);
        result.Kind.Should().Be(ScmDelayDirective.ResultKind.Deferred);
        result.TargetUtc.Should().Be(Now.AddHours(4));
    }

    /// <summary>Ein konfiguriertes Keyword ersetzt den Standard.</summary>
    [Fact]
    public void Parse_UsesConfiguredKeyword()
    {
        ScmDelayDirective.Parse("$Softwareschmiede #verzögert 4h", "#warte", Now).Kind
            .Should().Be(ScmDelayDirective.ResultKind.None);
        var result = ScmDelayDirective.Parse("$Softwareschmiede #warte 4h", "#warte", Now);
        result.Kind.Should().Be(ScmDelayDirective.ResultKind.Deferred);
        result.TargetUtc.Should().Be(Now.AddHours(4));
    }

    /// <summary>Leere Keyword-Konfiguration fällt auf den Standard zurück.</summary>
    [Fact]
    public void Parse_FallsOnDefaultBack_AtEmptyKeyword()
    {
        var result = ScmDelayDirective.Parse("$Softwareschmiede #verzögert 4h", "   ", Now);
        result.Kind.Should().Be(ScmDelayDirective.ResultKind.Deferred);
    }
}
