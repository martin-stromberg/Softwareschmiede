using FluentAssertions;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Enums;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>Tests für <see cref="ScmDispatchPriority"/> (Issue 410, Schritt 2).</summary>
public sealed class ScmDispatchPrioritaetTests
{
    private static readonly Guid AufgabeA = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid AufgabeB = new("22222222-2222-2222-2222-222222222222");

    private static ScmDispatchPriority.EventSelection Event(long sequence, Guid? workItemId = null,
        string status = "Wartend", long? nextAttemptUtc = null)
        => new(Guid.NewGuid(), workItemId ?? AufgabeB, sequence, status, nextAttemptUtc);

    private static Dictionary<Guid, ScmDispatchPriority.WorkItemState> Zustaende(
        params (Guid Id, ScmDispatchPriority.WorkItemState State)[] entries)
        => entries.ToDictionary(x => x.Id, x => x.State);

    /// <summary>Klasse 1: gesetzte Pausierungszeit (auch abgelaufen) markiert eine Fortsetzung.</summary>
    [Fact]
    public void PrioritaetsKlasse_Pausiert_GibtKlasse1()
    {
        var state = new ScmDispatchPriority.WorkItemState(WorkItemStatus.Waiting,
            WorkItemExecutionStatus.Active, DateTimeOffset.UtcNow.AddMinutes(-5));

        ScmDispatchPriority.PriorityClass(Event(1, AufgabeA), state).Should().Be(1);
    }

    /// <summary>Klasse 1: beendeter Lauf einer aktiven Aufgabe (Neu/Gestartet/Wartend).</summary>
    [Theory]
    [InlineData(WorkItemStatus.New)]
    [InlineData(WorkItemStatus.Started)]
    [InlineData(WorkItemStatus.Waiting)]
    public void PrioritaetsKlasse_LaufBeendetAufgabeAktiv_GibtKlasse1(WorkItemStatus status)
    {
        var state = new ScmDispatchPriority.WorkItemState(status,
            WorkItemExecutionStatus.Completed, null);

        ScmDispatchPriority.PriorityClass(Event(1, AufgabeA), state).Should().Be(1);
    }

    /// <summary>Klasse 1 greift nicht bei beendeter/archivierter Aufgabe oder aktivem Lauf.</summary>
    [Theory]
    [InlineData(WorkItemStatus.Completed, WorkItemExecutionStatus.Completed)]
    [InlineData(WorkItemStatus.Archived, WorkItemExecutionStatus.Completed)]
    [InlineData(WorkItemStatus.Started, WorkItemExecutionStatus.Active)]
    [InlineData(WorkItemStatus.New, WorkItemExecutionStatus.NotStarted)]
    public void PrioritaetsKlasse_KeinFortsetzungsfall_GibtNichtKlasse1(WorkItemStatus status,
        WorkItemExecutionStatus executionStatus)
    {
        var state = new ScmDispatchPriority.WorkItemState(status, executionStatus, null);

        ScmDispatchPriority.PriorityClass(Event(1, AufgabeA, "Wartend"), state).Should().Be(3);
    }

    /// <summary>Auch eine zukünftige Pausierungszeit zählt als Fortsetzungsereignis (Klasse 1).</summary>
    [Fact]
    public void PrioritaetsKlasse_PausiertInZukunft_GibtKlasse1()
    {
        var state = new ScmDispatchPriority.WorkItemState(WorkItemStatus.Waiting,
            WorkItemExecutionStatus.Active, DateTimeOffset.UtcNow.AddHours(1));

        ScmDispatchPriority.PriorityClass(Event(1, AufgabeA), state).Should().Be(1);
    }

    /// <summary>Beendete/Archivierte Aufgabe unterdrückt Klasse 1 auch bei gesetzter Pause.</summary>
    [Theory]
    [InlineData(WorkItemStatus.Completed)]
    [InlineData(WorkItemStatus.Archived)]
    public void PrioritaetsKlasse_PausiertAberAufgabeBeendet_NichtKlasse1(WorkItemStatus status)
    {
        var state = new ScmDispatchPriority.WorkItemState(status,
            WorkItemExecutionStatus.Active, DateTimeOffset.UtcNow.AddMinutes(-5));

        ScmDispatchPriority.PriorityClass(Event(1, AufgabeA, "Wartend"), state).Should().Be(3);
    }

    /// <summary>Klasse 2: „Verzögert"-Ereignisse.</summary>
    [Fact]
    public void PrioritaetsKlasse_Verzoegert_GibtKlasse2()
    {
        var state = new ScmDispatchPriority.WorkItemState(WorkItemStatus.New,
            WorkItemExecutionStatus.NotStarted, null);

        ScmDispatchPriority.PriorityClass(Event(1, AufgabeA, "Verzögert"), state).Should().Be(2);
    }

    /// <summary>Fortsetzung schlägt Verzögerung: Pausierte Aufgabe mit „Verzögert"-Ereignis bleibt Klasse 1.</summary>
    [Fact]
    public void PrioritaetsKlasse_FortsetzungSchlaegtVerzoegerung()
    {
        var state = new ScmDispatchPriority.WorkItemState(WorkItemStatus.Waiting,
            WorkItemExecutionStatus.Active, DateTimeOffset.UtcNow.AddMinutes(-5));

        ScmDispatchPriority.PriorityClass(Event(1, AufgabeA, "Verzögert"), state).Should().Be(1);
    }

    /// <summary>Gesamtreihenfolge: Fortsetzung → Verzögerungen (zeitaufsteigend) → Eingangsreihenfolge.</summary>
    [Fact]
    public void OrderForDispatch_SortiertNachKlassenUndZielzeit()
    {
        var regulär1 = Event(1);
        var verzögertSpät = Event(2, status: "Verzögert", nextAttemptUtc: 2000);
        var regulär2 = Event(3);
        var fortsetzung = Event(4, workItemId: AufgabeA);
        var verzögertFrüh = Event(5, status: "Verzögert", nextAttemptUtc: 1000);
        var items = new List<ScmDispatchPriority.EventSelection>
            { regulär1, verzögertSpät, regulär2, fortsetzung, verzögertFrüh };
        var zustaende = Zustaende(
            (AufgabeA, new ScmDispatchPriority.WorkItemState(WorkItemStatus.Started,
                WorkItemExecutionStatus.Completed, null)),
            (AufgabeB, new ScmDispatchPriority.WorkItemState(WorkItemStatus.New,
                WorkItemExecutionStatus.NotStarted, null)));

        var geordnet = ScmDispatchPriority.OrderForDispatch(items, zustaende);

        geordnet.Should().Equal(fortsetzung, verzögertFrüh, verzögertSpät, regulär1, regulär2);
    }

    /// <summary>Verzögert-Einträge ohne Zielzeitpunkt sortieren ans Ende der Klasse 2.</summary>
    [Fact]
    public void OrderForDispatch_VerzoegertOhneZielzeit_AmKlassenende()
    {
        var ohneZeit = Event(1, status: "Verzögert", nextAttemptUtc: null);
        var mitZeit = Event(9, status: "Verzögert", nextAttemptUtc: 500);

        var geordnet = ScmDispatchPriority.OrderForDispatch([ohneZeit, mitZeit],
            new Dictionary<Guid, ScmDispatchPriority.WorkItemState>());

        geordnet.Should().Equal(mitZeit, ohneZeit);
    }

    /// <summary>Fehlender Aufgabenzustand fällt in Klasse 3 mit Sequence-Reihenfolge.</summary>
    [Fact]
    public void OrderForDispatch_OhneAufgabenzustand_Eingangsreihenfolge()
    {
        var e2 = Event(2, workItemId: Guid.NewGuid());
        var e1 = Event(1, workItemId: Guid.NewGuid());

        var geordnet = ScmDispatchPriority.OrderForDispatch([e2, e1],
            new Dictionary<Guid, ScmDispatchPriority.WorkItemState>());

        geordnet.Should().Equal(e1, e2);
    }
}
