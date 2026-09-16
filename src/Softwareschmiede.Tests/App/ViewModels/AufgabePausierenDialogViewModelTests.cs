using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Softwareschmiede.App.ViewModels;

namespace Softwareschmiede.Tests.App.ViewModels;

/// <summary>Unit-Tests für <see cref="AufgabePausierenDialogViewModel"/> (Issue 151).</summary>
public sealed class AufgabePausierenDialogViewModelTests
{
    private static readonly DateTimeOffset ReferenzZeit = new(2026, 7, 12, 10, 0, 0, TimeSpan.Zero);

    /// <summary>Der Dialog initialisiert Datum/Stunde/Minute mit dem aktuellen Zeitpunkt — aufgrund der
    /// Minuten-Granularität der Eingabefelder als nächste volle Minute, damit die Vorbelegung gültig ist.</summary>
    [Fact]
    public void Ctor_ShouldDefaultToCurrentLocalTime()
    {
        // LocalTimeZone = Machine-Zone, damit GetLocalNow() zur Interpretation der Eingabefelder
        // (LocalTimeZone des TimeProviders in BerechnePausiertBisUtc) konsistent ist.
        var timeProvider = new FakeTimeProvider(ReferenzZeit);
        timeProvider.SetLocalTimeZone(TimeZoneInfo.Local);
        var sut = new AufgabePausierenDialogViewModel(timeProvider);

        var vorbelegung = timeProvider.GetLocalNow().AddMinutes(1);
        sut.PausiertDatum.Should().Be(vorbelegung.Date);
        sut.PausiertStunde.Should().Be(vorbelegung.Hour);
        sut.PausiertMinute.Should().Be(vorbelegung.Minute);
        sut.ValidierungsFehler.Should().BeNull("die Vorbelegung muss unmittelbar gültig sein — kein roter Fehler beim Öffnen");
        sut.KannBestaetigen.Should().BeTrue();
    }

    /// <summary>Ein Zeitpunkt in der Vergangenheit ist ungültig und blockiert die Übernahme.</summary>
    [Fact]
    public void KannBestaetigen_ShouldBeFalse_WhenZeitpunktInVergangenheit()
    {
        var timeProvider = new FakeTimeProvider(ReferenzZeit);
        var sut = new AufgabePausierenDialogViewModel(timeProvider);

        var gestern = timeProvider.GetLocalNow().AddDays(-1);
        sut.PausiertDatum = gestern.Date;
        sut.PausiertStunde = gestern.Hour;
        sut.PausiertMinute = gestern.Minute;

        sut.KannBestaetigen.Should().BeFalse();
        sut.ValidierungsFehler.Should().Be("Der Zeitpunkt muss in der Zukunft liegen.");
    }

    /// <summary>Ein gültiger zukünftiger Zeitpunkt erlaubt die Übernahme.</summary>
    [Fact]
    public void KannBestaetigen_ShouldBeTrue_WhenZeitpunktInZukunft()
    {
        var timeProvider = new FakeTimeProvider(ReferenzZeit);
        var sut = new AufgabePausierenDialogViewModel(timeProvider);

        var morgen = timeProvider.GetLocalNow().AddDays(1);
        sut.PausiertDatum = morgen.Date;
        sut.PausiertStunde = morgen.Hour;
        sut.PausiertMinute = morgen.Minute;

        sut.KannBestaetigen.Should().BeTrue();
        sut.ValidierungsFehler.Should().BeNull();
    }

    /// <summary>Ungültige Stunden-/Minutenwerte werden als Validierungsfehler gemeldet.</summary>
    [Theory]
    [InlineData(-1, 30)]
    [InlineData(24, 30)]
    [InlineData(12, -1)]
    [InlineData(12, 60)]
    public void ValidierungsFehler_ShouldBeSet_WhenStundeOderMinuteUngueltig(int stunde, int minute)
    {
        var timeProvider = new FakeTimeProvider(ReferenzZeit);
        var sut = new AufgabePausierenDialogViewModel(timeProvider);
        sut.PausiertDatum = timeProvider.GetLocalNow().AddDays(1).Date;

        sut.PausiertStunde = stunde;
        sut.PausiertMinute = minute;

        sut.ValidierungsFehler.Should().NotBeNull();
        sut.KannBestaetigen.Should().BeFalse();
    }

    /// <summary>BestaetigenCommand liefert den gewählten Zeitpunkt als UTC im Ergebnis und fordert das Schließen an.</summary>
    [Fact]
    public void BestaetigenCommand_ShouldProduceErgebnis_AndRequestClose()
    {
        var timeProvider = new FakeTimeProvider(ReferenzZeit);
        timeProvider.SetLocalTimeZone(TimeZoneInfo.Local);
        var sut = new AufgabePausierenDialogViewModel(timeProvider);
        var ziel = timeProvider.GetLocalNow().AddDays(1);
        sut.PausiertDatum = ziel.Date;
        sut.PausiertStunde = ziel.Hour;
        sut.PausiertMinute = ziel.Minute;
        var closed = new List<bool>();
        sut.CloseRequested += (_, result) => closed.Add(result);

        sut.BestaetigenCommand.Execute(null);

        // Das ViewModel interpretiert die Eingabe in der LocalTimeZone des TimeProviders und rechnet
        // nach UTC um; dank SetLocalTimeZone(TimeZoneInfo.Local) entspricht das der Maschinen-Zone —
        // Erwartung daher über DateTimeKind.Local → UTC berechnen.
        var erwartetUtc = new DateTimeOffset(
            new DateTime(ziel.Year, ziel.Month, ziel.Day, ziel.Hour, ziel.Minute, 0, DateTimeKind.Local)).ToUniversalTime();

        sut.Ergebnis.Should().NotBeNull();
        sut.Ergebnis!.Aufheben.Should().BeFalse();
        sut.Ergebnis.PausiertBisUtc.Should().BeCloseTo(erwartetUtc, TimeSpan.FromMinutes(1));
        closed.Should().Equal(true);
    }

    /// <summary>AufhebenCommand ist nur bei aktiver Pause verfügbar und liefert das Aufheben-Ergebnis.</summary>
    [Fact]
    public void AufhebenCommand_ShouldProduceAufhebenErgebnis_OnlyWhenPausiert()
    {
        var timeProvider = new FakeTimeProvider(ReferenzZeit);
        var sut = new AufgabePausierenDialogViewModel(timeProvider);

        sut.AufhebenCommand.CanExecute(null).Should().BeFalse("ohne aktive Pause ist Aufheben deaktiviert");

        var pausiertBis = timeProvider.GetUtcNow().AddHours(2);
        sut.Initialize(pausiertBis);

        sut.IstAktuellPausiert.Should().BeTrue();
        sut.AktuellePauseAnzeige.Should().NotBeNull();
        sut.AufhebenCommand.CanExecute(null).Should().BeTrue();

        var closed = new List<bool>();
        sut.CloseRequested += (_, result) => closed.Add(result);
        sut.AufhebenCommand.Execute(null);

        sut.Ergebnis.Should().NotBeNull();
        sut.Ergebnis!.Aufheben.Should().BeTrue();
        sut.Ergebnis.PausiertBisUtc.Should().BeNull();
        closed.Should().Equal(true);
    }

    /// <summary>Eine bereits abgelaufene Pause wird nicht als „Aktuell pausiert" angezeigt und die
    /// Eingabefelder werden mit der Standardvorbelegung (aktueller Zeitpunkt) statt dem vergangenen
    /// Zeitstempel befüllt — der Dialog öffnet sich ohne sofortigen Validierungsfehler.</summary>
    [Fact]
    public void AktuellePauseAnzeige_ShouldBeNull_WhenPauseAbgelaufen()
    {
        var timeProvider = new FakeTimeProvider(ReferenzZeit);
        timeProvider.SetLocalTimeZone(TimeZoneInfo.Local);
        var sut = new AufgabePausierenDialogViewModel(timeProvider);

        sut.Initialize(timeProvider.GetUtcNow().AddMinutes(-5));

        sut.AktuellePauseAnzeige.Should().BeNull("eine abgelaufene Pause ist keine aktive Pause");
        sut.IstAktuellPausiert.Should().BeFalse();

        var vorbelegung = timeProvider.GetLocalNow().AddMinutes(1);
        sut.PausiertDatum.Should().Be(vorbelegung.Date);
        sut.PausiertStunde.Should().Be(vorbelegung.Hour);
        sut.PausiertMinute.Should().Be(vorbelegung.Minute);
        sut.ValidierungsFehler.Should().BeNull();
        sut.KannBestaetigen.Should().BeTrue();
    }

    /// <summary>Initialize mit bestehender Pause befüllt die Eingabefelder mit dem lokalen Pause-Ende.</summary>
    [Fact]
    public void Initialize_ShouldPrefillFields_WhenPauseVorhanden()
    {
        var timeProvider = new FakeTimeProvider(ReferenzZeit);
        timeProvider.SetLocalTimeZone(TimeZoneInfo.Local);
        var sut = new AufgabePausierenDialogViewModel(timeProvider);
        var pausiertBis = timeProvider.GetUtcNow().AddHours(5);
        var lokal = pausiertBis.ToLocalTime();

        sut.Initialize(pausiertBis);

        sut.PausiertDatum.Should().Be(lokal.Date);
        sut.PausiertStunde.Should().Be(lokal.Hour);
        sut.PausiertMinute.Should().Be(lokal.Minute);
    }

    /// <summary>AbbrechenCommand fordert das Schließen ohne Ergebnis an.</summary>
    [Fact]
    public void AbbrechenCommand_ShouldRequestClose_WithoutErgebnis()
    {
        var sut = new AufgabePausierenDialogViewModel(new FakeTimeProvider(ReferenzZeit));
        var closed = new List<bool>();
        sut.CloseRequested += (_, result) => closed.Add(result);

        sut.AbbrechenCommand.Execute(null);

        sut.Ergebnis.Should().BeNull();
        closed.Should().Equal(false);
    }
}
