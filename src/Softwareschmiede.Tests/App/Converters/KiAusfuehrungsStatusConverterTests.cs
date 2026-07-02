using System.Globalization;
using FluentAssertions;
using Softwareschmiede.App.Converters;
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

    /// <summary>Convert gibt einen leeren String zurück, wenn kein Aufgabe-Objekt übergeben wird.</summary>
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
