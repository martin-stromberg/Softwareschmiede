using System.Globalization;
using FluentAssertions;
using Softwareschmiede.App.Converters;
using Softwareschmiede.App.ViewModels;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;

namespace Softwareschmiede.Tests.App.Converters;

/// <summary>Tests für KiAusfuehrungsStatusConverter.</summary>
public sealed class KiAusfuehrungsStatusConverterTests
{
    private readonly KiAusfuehrungsStatusConverter _sut = new();

    /// <summary>Convert gibt "▶ Läuft" zurück, wenn AktiveRunId gesetzt und der Heartbeat aktuell ist.</summary>
    [Fact]
    public void Convert_ShouldReturnLaeuftString_WhenAktiveRunIdPresentAndHeartbeatRecent()
    {
        var aufgabe = new Aufgabe
        {
            Id = Guid.NewGuid(),
            Titel = "Laufende Aufgabe",
            Status = AufgabeStatus.Gestartet,
            AktiveRunId = "run-1",
            LastHeartbeatUtc = DateTimeOffset.UtcNow.AddMinutes(-1)
        };

        var result = _sut.Convert(aufgabe, typeof(string), null!, CultureInfo.InvariantCulture);

        result.Should().Be("▶ Läuft");
    }

    /// <summary>Convert gibt "⏸ Wartet" zurück, wenn Status Wartend ist.</summary>
    [Fact]
    public void Convert_ShouldReturnWartetString_WhenStatusIstWartend()
    {
        var aufgabe = new Aufgabe
        {
            Id = Guid.NewGuid(),
            Titel = "Wartende Aufgabe",
            Status = AufgabeStatus.Wartend
        };

        var result = _sut.Convert(aufgabe, typeof(string), null!, CultureInfo.InvariantCulture);

        result.Should().Be("⏸ Wartet");
    }

    /// <summary>
    /// Regressionstest (Issue 108, Folgefehler des Rückwegs Läuft → Wartet): Auch während ein CLI-Prozess
    /// noch läuft (<see cref="Aufgabe.AktiveRunId"/> gesetzt, Heartbeat aktuell) muss die Kachel auf
    /// "⏸ Wartet" umschalten, sobald der persistierte Laufzeit-Substatus <see cref="AufgabeLaufStatus.WartetAufEingabe"/>
    /// meldet, dass die CLI auf Benutzereingabe wartet. Vor dem Fix konnte der Converter diesen Zustand
    /// strukturell nicht erkennen, weil ihm der Substatus nie zur Verfügung stand — er zeigte fälschlich
    /// dauerhaft "▶ Läuft", solange der Prozess lebte.
    /// </summary>
    [Fact]
    public void Convert_ShouldReturnWartetString_WhenAktiveRunIdPresentAndLaufStatusIstWartetAufEingabe()
    {
        var aufgabe = new Aufgabe
        {
            Id = Guid.NewGuid(),
            Titel = "Laufende, aber wartende Aufgabe",
            Status = AufgabeStatus.Gestartet,
            AktiveRunId = "run-3",
            LastHeartbeatUtc = DateTimeOffset.UtcNow.AddSeconds(-5),
            LaufStatus = AufgabeLaufStatus.WartetAufEingabe
        };

        var result = _sut.Convert(aufgabe, typeof(string), null!, CultureInfo.InvariantCulture);

        result.Should().Be("⏸ Wartet");
    }

    /// <summary>
    /// Ist der Laufzeit-Substatus explizit <see cref="AufgabeLaufStatus.Laeuft"/> (oder null, z. B. beim
    /// klassischen Start ohne ConPTY-Sitzung), muss weiterhin "▶ Läuft" angezeigt werden — die Erweiterung
    /// um den Substatus darf das bisherige Verhalten für den Normalfall nicht verändern.
    /// </summary>
    [Fact]
    public void Convert_ShouldReturnLaeuftString_WhenAktiveRunIdPresentAndLaufStatusIstLaeuftOderNull()
    {
        var aufgabeMitLaeuft = new Aufgabe
        {
            Id = Guid.NewGuid(),
            Titel = "Laufende Aufgabe (explizit Laeuft)",
            Status = AufgabeStatus.Gestartet,
            AktiveRunId = "run-4",
            LastHeartbeatUtc = DateTimeOffset.UtcNow.AddSeconds(-5),
            LaufStatus = AufgabeLaufStatus.Laeuft
        };
        var aufgabeOhneLaufStatus = new Aufgabe
        {
            Id = Guid.NewGuid(),
            Titel = "Laufende Aufgabe (kein Substatus bekannt)",
            Status = AufgabeStatus.Gestartet,
            AktiveRunId = "run-5",
            LastHeartbeatUtc = DateTimeOffset.UtcNow.AddSeconds(-5),
            LaufStatus = null
        };

        _sut.Convert(aufgabeMitLaeuft, typeof(string), null!, CultureInfo.InvariantCulture).Should().Be("▶ Läuft");
        _sut.Convert(aufgabeOhneLaufStatus, typeof(string), null!, CultureInfo.InvariantCulture).Should().Be("▶ Läuft");
    }

    /// <summary>Convert gibt "✓ Bereit" zurück, wenn keine aktive Ausführung erkennbar ist.</summary>
    [Fact]
    public void Convert_ShouldReturnBereitString_WhenNoActiveRunOrHeartbeatExpired()
    {
        var aufgabe = new Aufgabe
        {
            Id = Guid.NewGuid(),
            Titel = "Aufgabe ohne aktiven Lauf",
            Status = AufgabeStatus.Gestartet,
            AktiveRunId = "run-2",
            LastHeartbeatUtc = DateTimeOffset.UtcNow.AddMinutes(-10)
        };

        var result = _sut.Convert(aufgabe, typeof(string), null!, CultureInfo.InvariantCulture);

        result.Should().Be("✓ Bereit");
    }

    /// <summary>Convert unterstützt das Sidebar-Panel-Item mit denselben Statusdaten wie Aufgabe.</summary>
    [Fact]
    public void Convert_ShouldReturnLaeuftString_WhenPanelItemHasActiveRunAndHeartbeatRecent()
    {
        var item = new AktiveAufgabePanelItem
        {
            Id = Guid.NewGuid(),
            Titel = "Sidebar-Aufgabe",
            Status = AufgabeStatus.Gestartet,
            AktiveRunId = "run-panel",
            LastHeartbeatUtc = DateTimeOffset.UtcNow.AddSeconds(-10),
            LaufStatus = AufgabeLaufStatus.Laeuft
        };

        var result = _sut.Convert(item, typeof(string), null!, CultureInfo.InvariantCulture);

        result.Should().Be("▶ Läuft");
    }

    /// <summary>Convert gibt einen leeren String zurück, wenn kein unterstütztes Objekt übergeben wird.</summary>
    [Fact]
    public void Convert_ShouldReturnEmptyString_WhenValueIsNotAufgabe()
    {
        var result = _sut.Convert("invalid", typeof(string), null!, CultureInfo.InvariantCulture);

        result.Should().Be(string.Empty);
    }

    /// <summary>ConvertBack wirft NotSupportedException.</summary>
    [Fact]
    public void ConvertBack_ShouldThrowNotSupportedException()
    {
        var act = () => _sut.ConvertBack("▶ Läuft", typeof(Aufgabe), null!, CultureInfo.InvariantCulture);

        act.Should().Throw<NotSupportedException>();
    }
}
