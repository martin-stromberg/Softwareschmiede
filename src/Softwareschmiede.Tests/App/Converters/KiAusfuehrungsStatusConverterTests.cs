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
        var workItem = new WorkItem
        {
            Id = Guid.NewGuid(),
            Title = "Laufende Aufgabe",
            Status = WorkItemStatus.Started,
            ExecutionStatus = WorkItemExecutionStatus.Active,
            ActiveRunId = "run-1",
            LastHeartbeatUtc = DateTimeOffset.UtcNow.AddMinutes(-1)
        };

        var result = _sut.Convert(workItem, typeof(string), null!, CultureInfo.InvariantCulture);

        result.Should().Be("▶ Läuft");
    }

    /// <summary>Convert gibt "⏸ Wartet" zurück, wenn Status Wartend ist.</summary>
    [Fact]
    public void Convert_ShouldReturnWartetString_WhenStatusIstWartend()
    {
        var workItem = new WorkItem
        {
            Id = Guid.NewGuid(),
            Title = "Wartende Aufgabe",
            Status = WorkItemStatus.Waiting,
            ExecutionStatus = WorkItemExecutionStatus.Active
        };

        var result = _sut.Convert(workItem, typeof(string), null!, CultureInfo.InvariantCulture);

        result.Should().Be("⏸ Wartet");
    }

    /// <summary>
    /// Regressionstest (Issue 108, Folgefehler des Rückwegs Läuft → Wartet): Auch während ein CLI-Prozess
    /// noch läuft (<see cref="WorkItem.ActiveRunId"/> gesetzt, Heartbeat aktuell) muss die Kachel auf
    /// "⏸ Wartet" umschalten, sobald der persistierte Laufzeit-Substatus <see cref="WorkItemRunStatus.WaitingForInput"/>
    /// meldet, dass die CLI auf Benutzereingabe wartet. Vor dem Fix konnte der Converter diesen Zustand
    /// strukturell nicht erkennen, weil ihm der Substatus nie zur Verfügung stand — er zeigte fälschlich
    /// dauerhaft "▶ Läuft", solange der Prozess lebte.
    /// </summary>
    [Fact]
    public void Convert_ShouldReturnWartetString_WhenAktiveRunIdPresentAndLaufStatusIstWartetAufEingabe()
    {
        var workItem = new WorkItem
        {
            Id = Guid.NewGuid(),
            Title = "Laufende, aber wartende Aufgabe",
            Status = WorkItemStatus.Started,
            ExecutionStatus = WorkItemExecutionStatus.Active,
            ActiveRunId = "run-3",
            LastHeartbeatUtc = DateTimeOffset.UtcNow.AddSeconds(-5),
            RunStatus = WorkItemRunStatus.WaitingForInput
        };

        var result = _sut.Convert(workItem, typeof(string), null!, CultureInfo.InvariantCulture);

        result.Should().Be("⏸ Wartet");
    }

    /// <summary>
    /// Ist der Laufzeit-Substatus explizit <see cref="WorkItemRunStatus.Running"/> (oder null, z. B. beim
    /// klassischen Start ohne ConPTY-Sitzung), muss weiterhin "▶ Läuft" angezeigt werden — die Erweiterung
    /// um den Substatus darf das bisherige Verhalten für den Normalfall nicht verändern.
    /// </summary>
    [Fact]
    public void Convert_ShouldReturnLaeuftString_WhenAktiveRunIdPresentAndLaufStatusIstLaeuftOderNull()
    {
        var aufgabeMitLaeuft = new WorkItem
        {
            Id = Guid.NewGuid(),
            Title = "Laufende Aufgabe (explizit Laeuft)",
            Status = WorkItemStatus.Started,
            ExecutionStatus = WorkItemExecutionStatus.Active,
            ActiveRunId = "run-4",
            LastHeartbeatUtc = DateTimeOffset.UtcNow.AddSeconds(-5),
            RunStatus = WorkItemRunStatus.Running
        };
        var aufgabeOhneLaufStatus = new WorkItem
        {
            Id = Guid.NewGuid(),
            Title = "Laufende Aufgabe (kein Substatus bekannt)",
            Status = WorkItemStatus.Started,
            ExecutionStatus = WorkItemExecutionStatus.Active,
            ActiveRunId = "run-5",
            LastHeartbeatUtc = DateTimeOffset.UtcNow.AddSeconds(-5),
            RunStatus = null
        };

        _sut.Convert(aufgabeMitLaeuft, typeof(string), null!, CultureInfo.InvariantCulture).Should().Be("▶ Läuft");
        _sut.Convert(aufgabeOhneLaufStatus, typeof(string), null!, CultureInfo.InvariantCulture).Should().Be("▶ Läuft");
    }

    /// <summary>Convert gibt "✓ Bereit" zurück, wenn keine aktive Ausführung erkennbar ist.</summary>
    [Fact]
    public void Convert_ShouldReturnBereitString_WhenNoActiveRunOrHeartbeatExpired()
    {
        var workItem = new WorkItem
        {
            Id = Guid.NewGuid(),
            Title = "Aufgabe ohne aktiven Lauf",
            Status = WorkItemStatus.Started,
            ExecutionStatus = WorkItemExecutionStatus.Active,
            ActiveRunId = "run-2",
            LastHeartbeatUtc = DateTimeOffset.UtcNow.AddMinutes(-10)
        };

        var result = _sut.Convert(workItem, typeof(string), null!, CultureInfo.InvariantCulture);

        result.Should().Be("✓ Bereit");
    }

    /// <summary>Convert unterstützt das Sidebar-Panel-Item mit denselben Statusdaten wie Aufgabe.</summary>
    [Fact]
    public void Convert_ShouldReturnLaeuftString_WhenPanelItemHasActiveRunAndHeartbeatRecent()
    {
        var item = new AktiveAufgabePanelItem
        {
            Id = Guid.NewGuid(),
            Title = "Sidebar-Aufgabe",
            Status = WorkItemStatus.Started,
            ExecutionStatus = WorkItemExecutionStatus.Active,
            ActiveRunId = "run-panel",
            LastHeartbeatUtc = DateTimeOffset.UtcNow.AddSeconds(-10),
            RunStatus = WorkItemRunStatus.Running
        };

        var result = _sut.Convert(item, typeof(string), null!, CultureInfo.InvariantCulture);

        result.Should().Be("▶ Läuft");
    }

    /// <summary>Convert berücksichtigt beim Sidebar-Panel-Item den persistierten Wartestatus einer aktiven CLI-Ausführung.</summary>
    [Fact]
    public void Convert_ShouldReturnWartetString_WhenPanelItemHasActiveRunAndLaufStatusIstWartetAufEingabe()
    {
        var item = new AktiveAufgabePanelItem
        {
            Id = Guid.NewGuid(),
            Title = "Wartende Sidebar-Aufgabe",
            Status = WorkItemStatus.Started,
            ExecutionStatus = WorkItemExecutionStatus.Active,
            ActiveRunId = "run-panel-wartet",
            LastHeartbeatUtc = DateTimeOffset.UtcNow.AddSeconds(-10),
            RunStatus = WorkItemRunStatus.WaitingForInput
        };

        var result = _sut.Convert(item, typeof(string), null!, CultureInfo.InvariantCulture);

        result.Should().Be("⏸ Wartet");
    }

    /// <summary>
    /// Live-Zweig (Issue 342): Zeigt <see cref="AktiveAufgabePanelItem.LiveLaufStatus"/> einen laufenden
    /// Prozess an, zeigt die Kachel "▶ Läuft" — selbst ohne persistierte Laufdaten (AktiveRunId null)
    /// und selbst wenn der persistierte Ausführungsstatus noch Beendet lautet.
    /// </summary>
    [Fact]
    public void Convert_ShouldReturnLaeuftString_WhenPanelItemHasLiveLaufStatusOhneLaufdaten()
    {
        var item = new AktiveAufgabePanelItem
        {
            Id = Guid.NewGuid(),
            Title = "Aufgabe mit lebendem Prozess ohne Laufdaten",
            Status = WorkItemStatus.Started,
            ExecutionStatus = WorkItemExecutionStatus.Completed,
            ActiveRunId = null,
            LastHeartbeatUtc = null,
            LiveLaufStatus = WorkItemRunStatus.Running
        };

        var result = _sut.Convert(item, typeof(string), null!, CultureInfo.InvariantCulture);

        result.Should().Be("▶ Läuft");
    }

    /// <summary>Live-Zweig (Issue 342): LiveLaufStatus WartetAufEingabe zeigt "⏸ Wartet".</summary>
    [Fact]
    public void Convert_ShouldReturnWartetString_WhenPanelItemHasLiveLaufStatusWartetAufEingabe()
    {
        var item = new AktiveAufgabePanelItem
        {
            Id = Guid.NewGuid(),
            Title = "Aufgabe mit wartender Live-CLI",
            Status = WorkItemStatus.Started,
            ExecutionStatus = WorkItemExecutionStatus.Completed,
            LiveLaufStatus = WorkItemRunStatus.WaitingForInput
        };

        var result = _sut.Convert(item, typeof(string), null!, CultureInfo.InvariantCulture);

        result.Should().Be("⏸ Wartet");
    }

    /// <summary>
    /// Der Live-Zweig wertet nur aufwärts (Laeuft/Wartet): Pausiert und PromptInWartestellung dürfen
    /// nicht durch einen Live-Status überschrieben werden.
    /// </summary>
    [Fact]
    public void Convert_ShouldKeepPausiertUndWartestellung_VorLiveLaufStatus()
    {
        var pausiert = new AktiveAufgabePanelItem
        {
            Id = Guid.NewGuid(),
            Title = "Pausierte Aufgabe mit Live-Prozess",
            Status = WorkItemStatus.Started,
            ExecutionStatus = WorkItemExecutionStatus.Active,
            LiveLaufStatus = WorkItemRunStatus.Running,
            PausedUntilUtc = DateTimeOffset.UtcNow.AddMinutes(30)
        };
        var wartestellung = new AktiveAufgabePanelItem
        {
            Id = Guid.NewGuid(),
            Title = "Aufgabe mit Prompt-Wartestellung",
            Status = WorkItemStatus.Started,
            ExecutionStatus = WorkItemExecutionStatus.Active,
            LiveLaufStatus = WorkItemRunStatus.Running,
            HasScheduledPrompt = true
        };

        _sut.Convert(pausiert, typeof(string), null!, CultureInfo.InvariantCulture)
            .Should().BeOfType<string>().Which.Should().StartWith("⏸ Pausiert (noch ");
        _sut.Convert(wartestellung, typeof(string), null!, CultureInfo.InvariantCulture)
            .Should().Be("⏳ Prompt in Wartestellung");
    }

    /// <summary>
    /// Live-Zweig (Issue 342): <see cref="AktiveAufgabePanelItem.LiveLaufStatus"/> == null darf die
    /// bisherige persistenzbasierte Bewertung nicht verändern — keine erzwungene Beendigung.
    /// </summary>
    [Fact]
    public void Convert_ShouldReturnBereitString_WhenPanelItemLiveLaufStatusNullUndBeendet()
    {
        var item = new AktiveAufgabePanelItem
        {
            Id = Guid.NewGuid(),
            Title = "Beendete Aufgabe ohne Live-Prozess",
            Status = WorkItemStatus.Started,
            ExecutionStatus = WorkItemExecutionStatus.Completed,
            LiveLaufStatus = null
        };

        var result = _sut.Convert(item, typeof(string), null!, CultureInfo.InvariantCulture);

        result.Should().Be("✓ Bereit");
    }

    /// <summary>Convert ignoriert den alten Gesamtstatus Wartend, wenn die KI-Ausführung beendet ist.</summary>
    [Fact]
    public void Convert_ShouldReturnBereitString_WhenWartendAufgabeHasBeendeteAusfuehrung()
    {
        var workItem = new WorkItem
        {
            Id = Guid.NewGuid(),
            Title = "Gestoppte wartende Aufgabe",
            Status = WorkItemStatus.Waiting,
            ExecutionStatus = WorkItemExecutionStatus.Completed
        };

        var result = _sut.Convert(workItem, typeof(string), null!, CultureInfo.InvariantCulture);

        result.Should().Be("✓ Bereit");
    }

    /// <summary>Convert berücksichtigt den beendeten Ausführungsstatus auch bei Sidebar-Panel-Items.</summary>
    [Fact]
    public void Convert_ShouldReturnBereitString_WhenPanelItemWartendHasBeendeteAusfuehrung()
    {
        var item = new AktiveAufgabePanelItem
        {
            Id = Guid.NewGuid(),
            Title = "Gestoppte Sidebar-Aufgabe",
            Status = WorkItemStatus.Waiting,
            ExecutionStatus = WorkItemExecutionStatus.Completed
        };

        var result = _sut.Convert(item, typeof(string), null!, CultureInfo.InvariantCulture);

        result.Should().Be("✓ Bereit");
    }

    /// <summary>Convert zeigt bei zukünftigem PausiertBisUtc den Pausiert-Countdown — vor allen übrigen Statusanzeigen (Issue 151).</summary>
    [Fact]
    public void Convert_ShouldReturnPausiertCountdown_WhenPausiertBisUtcInFuture()
    {
        var workItem = new WorkItem
        {
            Id = Guid.NewGuid(),
            Title = "Pausierte Aufgabe",
            Status = WorkItemStatus.Started,
            ExecutionStatus = WorkItemExecutionStatus.Active,
            ActiveRunId = "run-pausiert",
            LastHeartbeatUtc = DateTimeOffset.UtcNow.AddSeconds(-5),
            RunStatus = WorkItemRunStatus.Running,
            PausedUntilUtc = DateTimeOffset.UtcNow.AddMinutes(90)
        };

        var result = _sut.Convert(workItem, typeof(string), null!, CultureInfo.InvariantCulture);

        result.Should().BeOfType<string>().Which.Should().StartWith("⏸ Pausiert (noch ");
    }

    /// <summary>Convert zeigt den Pausiert-Countdown auch für Sidebar-Panel-Items (Issue 151).</summary>
    [Fact]
    public void Convert_ShouldReturnPausiertCountdown_WhenPanelItemPausiert()
    {
        var item = new AktiveAufgabePanelItem
        {
            Id = Guid.NewGuid(),
            Title = "Pausierte Sidebar-Aufgabe",
            Status = WorkItemStatus.Started,
            ExecutionStatus = WorkItemExecutionStatus.Active,
            ActiveRunId = "run-panel-pausiert",
            LastHeartbeatUtc = DateTimeOffset.UtcNow.AddSeconds(-5),
            PausedUntilUtc = DateTimeOffset.UtcNow.AddHours(3)
        };

        var result = _sut.Convert(item, typeof(string), null!, CultureInfo.InvariantCulture);

        result.Should().BeOfType<string>().Which.Should().StartWith("⏸ Pausiert (noch ");
    }

    /// <summary>Convert zeigt bei Pausen >= 24 Stunden das Tag-Format d.hh:mm:ss (Issue 151).</summary>
    [Fact]
    public void Convert_ShouldReturnPausiertCountdownMitTagen_WhenPauseUeber24Stunden()
    {
        var workItem = new WorkItem
        {
            Id = Guid.NewGuid(),
            Title = "Lange pausierte Aufgabe",
            Status = WorkItemStatus.Started,
            ExecutionStatus = WorkItemExecutionStatus.Active,
            ActiveRunId = "run-lang",
            LastHeartbeatUtc = DateTimeOffset.UtcNow.AddSeconds(-5),
            PausedUntilUtc = DateTimeOffset.UtcNow.AddDays(2)
        };

        var result = _sut.Convert(workItem, typeof(string), null!, CultureInfo.InvariantCulture);

        result.Should().BeOfType<string>().Which.Should().MatchRegex(@"^⏸ Pausiert \(noch \d+\.\d{2}:\d{2}:\d{2}\)$");
    }

    /// <summary>Convert ignoriert einen abgelaufenen PausiertBisUtc-Wert und zeigt den normalen Status (Issue 151).</summary>
    [Fact]
    public void Convert_ShouldReturnNormalStatus_WhenPausiertBisUtcAbgelaufen()
    {
        var workItem = new WorkItem
        {
            Id = Guid.NewGuid(),
            Title = "Aufgabe mit abgelaufener Pause",
            Status = WorkItemStatus.Started,
            ExecutionStatus = WorkItemExecutionStatus.Active,
            ActiveRunId = "run-abgelaufen",
            LastHeartbeatUtc = DateTimeOffset.UtcNow.AddSeconds(-5),
            PausedUntilUtc = DateTimeOffset.UtcNow.AddMinutes(-1)
        };

        var result = _sut.Convert(workItem, typeof(string), null!, CultureInfo.InvariantCulture);

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
        var act = () => _sut.ConvertBack("▶ Läuft", typeof(WorkItem), null!, CultureInfo.InvariantCulture);

        act.Should().Throw<NotSupportedException>();
    }
}
