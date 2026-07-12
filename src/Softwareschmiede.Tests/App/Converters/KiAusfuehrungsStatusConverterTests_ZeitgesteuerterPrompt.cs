using System.Globalization;
using FluentAssertions;
using Softwareschmiede.App.Converters;
using Softwareschmiede.App.ViewModels;
using Softwareschmiede.Domain.Enums;

namespace Softwareschmiede.Tests.App.Converters;

/// <summary>Tests für KiAusfuehrungsStatusConverter im Zusammenhang mit zeitgesteuerten Prompts.</summary>
public sealed class KiAusfuehrungsStatusConverterTests_ZeitgesteuerterPrompt
{
    private readonly KiAusfuehrungsStatusConverter _sut = new();

    /// <summary>Steht für eine Aufgabe ein zeitgesteuerter Prompt in der Warteschlange (HasScheduledPrompt), muss
    /// die Seitenleiste "Prompt in Wartestellung" statt des allgemeinen "Wartet"-Status anzeigen.</summary>
    [Fact]
    public void Convert_ShouldReturnPromptInWartestellung_WhenHasScheduledPromptIsTrue()
    {
        var item = new AktiveAufgabePanelItem
        {
            Id = Guid.NewGuid(),
            Titel = "Aufgabe mit geplantem Prompt",
            Status = AufgabeStatus.Wartend,
            HasScheduledPrompt = true
        };

        var result = _sut.Convert(item, typeof(string), null!, CultureInfo.InvariantCulture);

        result.Should().Be("⏳ Prompt in Wartestellung");
    }

    /// <summary>HasScheduledPrompt hat Vorrang vor dem sonst aus AktiveRunId/LaufStatus abgeleiteten "▶ Läuft".</summary>
    [Fact]
    public void Convert_ShouldReturnPromptInWartestellung_EvenWhenCliIsRunning()
    {
        var item = new AktiveAufgabePanelItem
        {
            Id = Guid.NewGuid(),
            Titel = "Laufende Aufgabe mit geplantem Prompt",
            Status = AufgabeStatus.Gestartet,
            AktiveRunId = "run-1",
            LastHeartbeatUtc = DateTimeOffset.UtcNow.AddSeconds(-5),
            LaufStatus = AufgabeLaufStatus.Laeuft,
            HasScheduledPrompt = true
        };

        var result = _sut.Convert(item, typeof(string), null!, CultureInfo.InvariantCulture);

        result.Should().Be("⏳ Prompt in Wartestellung");
    }

    /// <summary>Ist kein Prompt geplant (HasScheduledPrompt false), bleibt das bisherige Statusverhalten unverändert.</summary>
    [Fact]
    public void Convert_ShouldReturnWartetString_WhenHasScheduledPromptIsFalse()
    {
        var item = new AktiveAufgabePanelItem
        {
            Id = Guid.NewGuid(),
            Titel = "Aufgabe ohne geplanten Prompt",
            Status = AufgabeStatus.Wartend,
            HasScheduledPrompt = false
        };

        var result = _sut.Convert(item, typeof(string), null!, CultureInfo.InvariantCulture);

        result.Should().Be("⏸ Wartet");
    }
}
