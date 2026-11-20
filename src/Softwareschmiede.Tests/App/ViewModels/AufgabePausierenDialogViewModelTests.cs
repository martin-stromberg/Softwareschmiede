using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Softwareschmiede.App.ViewModels;

namespace Softwareschmiede.Tests.App.ViewModels;

/// <summary>Unit-Tests für <see cref="PauseWorkItemDialogViewModel"/> (Issue 151).</summary>
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
        var sut = new PauseWorkItemDialogViewModel(timeProvider);

        var prefill = timeProvider.GetLocalNow().AddMinutes(1);
        sut.PausedDate.Should().Be(prefill.Date);
        sut.PausedHour.Should().Be(prefill.Hour);
        sut.PausedMinute.Should().Be(prefill.Minute);
        sut.ValidationError.Should().BeNull("die Vorbelegung muss unmittelbar gültig sein — kein roter Fehler beim Öffnen");
        sut.CanConfirm.Should().BeTrue();
    }

    /// <summary>Ein Zeitpunkt in der Vergangenheit ist ungültig und blockiert die Übernahme.</summary>
    [Fact]
    public void KannBestaetigen_ShouldBeFalse_WhenZeitpunktInVergangenheit()
    {
        var timeProvider = new FakeTimeProvider(ReferenzZeit);
        var sut = new PauseWorkItemDialogViewModel(timeProvider);

        var gestern = timeProvider.GetLocalNow().AddDays(-1);
        sut.PausedDate = gestern.Date;
        sut.PausedHour = gestern.Hour;
        sut.PausedMinute = gestern.Minute;

        sut.CanConfirm.Should().BeFalse();
        sut.ValidationError.Should().Be("Der Zeitpunkt muss in der Zukunft liegen.");
    }

    /// <summary>Ein gültiger zukünftiger Zeitpunkt erlaubt die Übernahme.</summary>
    [Fact]
    public void KannBestaetigen_ShouldBeTrue_WhenZeitpunktInZukunft()
    {
        var timeProvider = new FakeTimeProvider(ReferenzZeit);
        var sut = new PauseWorkItemDialogViewModel(timeProvider);

        var morgen = timeProvider.GetLocalNow().AddDays(1);
        sut.PausedDate = morgen.Date;
        sut.PausedHour = morgen.Hour;
        sut.PausedMinute = morgen.Minute;

        sut.CanConfirm.Should().BeTrue();
        sut.ValidationError.Should().BeNull();
    }

    /// <summary>Ungültige Stunden-/Minutenwerte werden als Validierungsfehler gemeldet.</summary>
    [Theory]
    [InlineData(-1, 30)]
    [InlineData(24, 30)]
    [InlineData(12, -1)]
    [InlineData(12, 60)]
    public void ValidierungsFehler_ShouldBeSet_WhenStundeOderMinuteUngueltig(int hour, int minute)
    {
        var timeProvider = new FakeTimeProvider(ReferenzZeit);
        var sut = new PauseWorkItemDialogViewModel(timeProvider);
        sut.PausedDate = timeProvider.GetLocalNow().AddDays(1).Date;

        sut.PausedHour = hour;
        sut.PausedMinute = minute;

        sut.ValidationError.Should().NotBeNull();
        sut.CanConfirm.Should().BeFalse();
    }

    /// <summary>BestaetigenCommand liefert den gewählten Zeitpunkt als UTC im Ergebnis und fordert das Schließen an.</summary>
    [Fact]
    public void BestaetigenCommand_ShouldProduceErgebnis_AndRequestClose()
    {
        var timeProvider = new FakeTimeProvider(ReferenzZeit);
        timeProvider.SetLocalTimeZone(TimeZoneInfo.Local);
        var sut = new PauseWorkItemDialogViewModel(timeProvider);
        var target = timeProvider.GetLocalNow().AddDays(1);
        sut.PausedDate = target.Date;
        sut.PausedHour = target.Hour;
        sut.PausedMinute = target.Minute;
        var closed = new List<bool>();
        sut.CloseRequested += (_, result) => closed.Add(result);

        sut.ConfirmCommand.Execute(null);

        // Das ViewModel interpretiert die Eingabe in der LocalTimeZone des TimeProviders und rechnet
        // nach UTC um; dank SetLocalTimeZone(TimeZoneInfo.Local) entspricht das der Maschinen-Zone —
        // Erwartung daher über DateTimeKind.Local → UTC berechnen.
        var erwartetUtc = new DateTimeOffset(
            new DateTime(target.Year, target.Month, target.Day, target.Hour, target.Minute, 0, DateTimeKind.Local)).ToUniversalTime();

        sut.Result.Should().NotBeNull();
        sut.Result!.Aufheben.Should().BeFalse();
        sut.Result.PausedUntilUtc.Should().BeCloseTo(erwartetUtc, TimeSpan.FromMinutes(1));
        closed.Should().Equal(true);
    }

    /// <summary>AufhebenCommand ist nur bei aktiver Pause verfügbar und liefert das Aufheben-Ergebnis.</summary>
    [Fact]
    public void AufhebenCommand_ShouldProduceAufhebenErgebnis_OnlyWhenPausiert()
    {
        var timeProvider = new FakeTimeProvider(ReferenzZeit);
        var sut = new PauseWorkItemDialogViewModel(timeProvider);

        sut.AufhebenCommand.CanExecute(null).Should().BeFalse("ohne aktive Pause ist Aufheben deaktiviert");

        var pausedUntil = timeProvider.GetUtcNow().AddHours(2);
        sut.Initialize(pausedUntil);

        sut.IsCurrentlyPaused.Should().BeTrue();
        sut.CurrentPauseDisplay.Should().NotBeNull();
        sut.AufhebenCommand.CanExecute(null).Should().BeTrue();

        var closed = new List<bool>();
        sut.CloseRequested += (_, result) => closed.Add(result);
        sut.AufhebenCommand.Execute(null);

        sut.Result.Should().NotBeNull();
        sut.Result!.Aufheben.Should().BeTrue();
        sut.Result.PausedUntilUtc.Should().BeNull();
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
        var sut = new PauseWorkItemDialogViewModel(timeProvider);

        sut.Initialize(timeProvider.GetUtcNow().AddMinutes(-5));

        sut.CurrentPauseDisplay.Should().BeNull("eine abgelaufene Pause ist keine aktive Pause");
        sut.IsCurrentlyPaused.Should().BeFalse();

        var prefill = timeProvider.GetLocalNow().AddMinutes(1);
        sut.PausedDate.Should().Be(prefill.Date);
        sut.PausedHour.Should().Be(prefill.Hour);
        sut.PausedMinute.Should().Be(prefill.Minute);
        sut.ValidationError.Should().BeNull();
        sut.CanConfirm.Should().BeTrue();
    }

    /// <summary>Initialize mit bestehender Pause befüllt die Eingabefelder mit dem lokalen Pause-Ende.</summary>
    [Fact]
    public void Initialize_ShouldPrefillFields_WhenPauseVorhanden()
    {
        var timeProvider = new FakeTimeProvider(ReferenzZeit);
        timeProvider.SetLocalTimeZone(TimeZoneInfo.Local);
        var sut = new PauseWorkItemDialogViewModel(timeProvider);
        var pausedUntil = timeProvider.GetUtcNow().AddHours(5);
        var local = pausedUntil.ToLocalTime();

        sut.Initialize(pausedUntil);

        sut.PausedDate.Should().Be(local.Date);
        sut.PausedHour.Should().Be(local.Hour);
        sut.PausedMinute.Should().Be(local.Minute);
    }

    /// <summary>AbbrechenCommand fordert das Schließen ohne Ergebnis an.</summary>
    [Fact]
    public void AbbrechenCommand_ShouldRequestClose_WithoutErgebnis()
    {
        var sut = new PauseWorkItemDialogViewModel(new FakeTimeProvider(ReferenzZeit));
        var closed = new List<bool>();
        sut.CloseRequested += (_, result) => closed.Add(result);

        sut.CancelCommand.Execute(null);

        sut.Result.Should().BeNull();
        closed.Should().Equal(false);
    }
}
