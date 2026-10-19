using System.Globalization;
using FluentAssertions;
using Softwareschmiede.App.Converters;
using Softwareschmiede.App.ViewModels;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;

namespace Softwareschmiede.Tests.App.Converters;

/// <summary>Tests für KiAusfuehrungsStatusSymbolConverter (nur Glyph, kein Text/Countdown).</summary>
public sealed class KiAusfuehrungsStatusSymbolConverterTests
{
    private readonly KiAusfuehrungsStatusSymbolConverter _sut = new();

    /// <summary>Convert gibt "▶" zurück, wenn AktiveRunId gesetzt und der Heartbeat aktuell ist.</summary>
    [Fact]
    public void Convert_ShouldReturnSymbolOnly_WhenLaeuft()
    {
        var aufgabe = new Aufgabe
        {
            Id = Guid.NewGuid(),
            Titel = "Laufende Aufgabe",
            Status = AufgabeStatus.Gestartet,
            AusfuehrungsStatus = AufgabeAusfuehrungsStatus.Aktiv,
            AktiveRunId = "run-1",
            LastHeartbeatUtc = DateTimeOffset.UtcNow.AddMinutes(-1)
        };

        var result = _sut.Convert(aufgabe, typeof(string), null!, CultureInfo.InvariantCulture);

        result.Should().Be("▶");
    }

    /// <summary>Convert gibt "⏳" zurück, wenn ein zeitgesteuerter Prompt in der Warteschlange steht.</summary>
    [Fact]
    public void Convert_ShouldReturnSymbolOnly_WhenScheduledPrompt()
    {
        var item = new AktiveAufgabePanelItem
        {
            Id = Guid.NewGuid(),
            Titel = "Prompt-Aufgabe",
            Status = AufgabeStatus.Gestartet,
            AusfuehrungsStatus = AufgabeAusfuehrungsStatus.Aktiv,
            AktiveRunId = "run-prompt",
            LastHeartbeatUtc = DateTimeOffset.UtcNow.AddSeconds(-5),
            HasScheduledPrompt = true
        };

        var result = _sut.Convert(item, typeof(string), null!, CultureInfo.InvariantCulture);

        result.Should().Be("⏳");
    }

    /// <summary>Convert gibt "⏸" ohne Countdown-Text zurück, wenn die Aufgabe pausiert ist.</summary>
    [Fact]
    public void Convert_ShouldReturnSymbolOnly_WhenPausiert()
    {
        var aufgabe = new Aufgabe
        {
            Id = Guid.NewGuid(),
            Titel = "Pausierte Aufgabe",
            Status = AufgabeStatus.Gestartet,
            AusfuehrungsStatus = AufgabeAusfuehrungsStatus.Aktiv,
            AktiveRunId = "run-pausiert",
            LastHeartbeatUtc = DateTimeOffset.UtcNow.AddSeconds(-5),
            PausiertBisUtc = DateTimeOffset.UtcNow.AddMinutes(90)
        };

        var result = _sut.Convert(aufgabe, typeof(string), null!, CultureInfo.InvariantCulture);

        result.Should().Be("⏸");
    }

    /// <summary>Convert gibt "⏸" zurück, wenn der Laufzeit-Substatus WartetAufEingabe meldet.</summary>
    [Fact]
    public void Convert_ShouldReturnSymbolOnly_WhenWartetAufEingabe()
    {
        var aufgabe = new Aufgabe
        {
            Id = Guid.NewGuid(),
            Titel = "Wartende Aufgabe",
            Status = AufgabeStatus.Gestartet,
            AusfuehrungsStatus = AufgabeAusfuehrungsStatus.Aktiv,
            AktiveRunId = "run-3",
            LastHeartbeatUtc = DateTimeOffset.UtcNow.AddSeconds(-5),
            LaufStatus = AufgabeLaufStatus.WartetAufEingabe
        };

        var result = _sut.Convert(aufgabe, typeof(string), null!, CultureInfo.InvariantCulture);

        result.Should().Be("⏸");
    }

    /// <summary>Convert gibt "⏸" zurück, wenn der Status Wartend ist (kein aktiver Lauf).</summary>
    [Fact]
    public void Convert_ShouldReturnSymbolOnly_WhenStatusIstWartend()
    {
        var aufgabe = new Aufgabe
        {
            Id = Guid.NewGuid(),
            Titel = "Wartende Aufgabe",
            Status = AufgabeStatus.Wartend,
            AusfuehrungsStatus = AufgabeAusfuehrungsStatus.Aktiv
        };

        var result = _sut.Convert(aufgabe, typeof(string), null!, CultureInfo.InvariantCulture);

        result.Should().Be("⏸");
    }

    /// <summary>Convert gibt "✓" zurück, wenn keine aktive Ausführung erkennbar ist.</summary>
    [Fact]
    public void Convert_ShouldReturnSymbolOnly_WhenBereit()
    {
        var aufgabe = new Aufgabe
        {
            Id = Guid.NewGuid(),
            Titel = "Aufgabe ohne aktiven Lauf",
            Status = AufgabeStatus.Gestartet,
            AusfuehrungsStatus = AufgabeAusfuehrungsStatus.Aktiv,
            AktiveRunId = "run-2",
            LastHeartbeatUtc = DateTimeOffset.UtcNow.AddMinutes(-10)
        };

        var result = _sut.Convert(aufgabe, typeof(string), null!, CultureInfo.InvariantCulture);

        result.Should().Be("✓");
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
        var act = () => _sut.ConvertBack("▶", typeof(Aufgabe), null!, CultureInfo.InvariantCulture);

        act.Should().Throw<NotSupportedException>();
    }
}
