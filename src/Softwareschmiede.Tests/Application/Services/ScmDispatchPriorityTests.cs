using FluentAssertions;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Enums;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>Tests für <see cref="ScmDispatchPriority"/> (Issue 410, Schritt 2).</summary>
public sealed class ScmDispatchPriorityTests
{
    private static readonly Guid WorkItemA = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid WorkItemB = new("22222222-2222-2222-2222-222222222222");

    private static ScmDispatchPriority.EventSelection Event(long sequence, Guid? workItemId = null,
        string status = "Wartend", long? nextAttemptUtc = null, string? purpose = null)
        => new(Guid.NewGuid(), workItemId ?? WorkItemB, sequence, status, nextAttemptUtc, purpose);

    private static Dictionary<Guid, ScmDispatchPriority.WorkItemState> States(
        params (Guid Id, ScmDispatchPriority.WorkItemState State)[] entries)
        => entries.ToDictionary(x => x.Id, x => x.State);

    /// <summary>Klasse 1: gesetzte Pausierungszeit (auch abgelaufen) markiert eine Fortsetzung.</summary>
    [Fact]
    public void PriorityClass_Paused_ReturnsClass1()
    {
        var state = new ScmDispatchPriority.WorkItemState(WorkItemStatus.Waiting,
            WorkItemExecutionStatus.Active, DateTimeOffset.UtcNow.AddMinutes(-5));

        ScmDispatchPriority.PriorityClass(Event(1, WorkItemA), state).Should().Be(1);
    }

    /// <summary>Klasse 1: beendeter Lauf einer aktiven Aufgabe (Neu/Gestartet/Wartend).</summary>
    [Theory]
    [InlineData(WorkItemStatus.New)]
    [InlineData(WorkItemStatus.Started)]
    [InlineData(WorkItemStatus.Waiting)]
    public void PriorityClass_RunCompletedWorkItemActive_ReturnsClass1(WorkItemStatus status)
    {
        var state = new ScmDispatchPriority.WorkItemState(status,
            WorkItemExecutionStatus.Completed, null);

        ScmDispatchPriority.PriorityClass(Event(1, WorkItemA), state).Should().Be(1);
    }

    /// <summary>Klasse 1 greift nicht bei beendeter/archivierter Aufgabe oder aktivem Lauf.</summary>
    [Theory]
    [InlineData(WorkItemStatus.Completed, WorkItemExecutionStatus.Completed)]
    [InlineData(WorkItemStatus.Archived, WorkItemExecutionStatus.Completed)]
    [InlineData(WorkItemStatus.Started, WorkItemExecutionStatus.Active)]
    [InlineData(WorkItemStatus.New, WorkItemExecutionStatus.NotStarted)]
    public void PriorityClass_NoContinuationcase_ReturnsNotClass1(WorkItemStatus status,
        WorkItemExecutionStatus executionStatus)
    {
        var state = new ScmDispatchPriority.WorkItemState(status, executionStatus, null);

        ScmDispatchPriority.PriorityClass(Event(1, WorkItemA, "Wartend"), state).Should().Be(3);
    }

    /// <summary>Auch eine zukünftige Pausierungszeit zählt als Fortsetzungsereignis (Klasse 1).</summary>
    [Fact]
    public void PriorityClass_PausedInFuture_ReturnsClass1()
    {
        var state = new ScmDispatchPriority.WorkItemState(WorkItemStatus.Waiting,
            WorkItemExecutionStatus.Active, DateTimeOffset.UtcNow.AddHours(1));

        ScmDispatchPriority.PriorityClass(Event(1, WorkItemA), state).Should().Be(1);
    }

    /// <summary>Beendete/Archivierte Aufgabe unterdrückt Klasse 1 auch bei gesetzter Pause.</summary>
    [Theory]
    [InlineData(WorkItemStatus.Completed)]
    [InlineData(WorkItemStatus.Archived)]
    public void PriorityClass_PausedButWorkItemCompleted_NotClass1(WorkItemStatus status)
    {
        var state = new ScmDispatchPriority.WorkItemState(status,
            WorkItemExecutionStatus.Active, DateTimeOffset.UtcNow.AddMinutes(-5));

        ScmDispatchPriority.PriorityClass(Event(1, WorkItemA, "Wartend"), state).Should().Be(3);
    }

    /// <summary>Klasse 2: „Verzögert"-Ereignisse.</summary>
    [Fact]
    public void PriorityClass_Delayed_ReturnsClass2()
    {
        var state = new ScmDispatchPriority.WorkItemState(WorkItemStatus.New,
            WorkItemExecutionStatus.NotStarted, null);

        ScmDispatchPriority.PriorityClass(Event(1, WorkItemA, "Verzögert"), state).Should().Be(2);
    }

    /// <summary>Fortsetzung schlägt Verzögerung: Pausierte Aufgabe mit „Verzögert"-Ereignis bleibt Klasse 1.</summary>
    [Fact]
    public void PriorityClass_ContinuationStrikesDelay()
    {
        var state = new ScmDispatchPriority.WorkItemState(WorkItemStatus.Waiting,
            WorkItemExecutionStatus.Active, DateTimeOffset.UtcNow.AddMinutes(-5));

        ScmDispatchPriority.PriorityClass(Event(1, WorkItemA, "Verzögert"), state).Should().Be(1);
    }

    /// <summary>Gesamtreihenfolge: Fortsetzung → Verzögerungen (zeitaufsteigend) → Eingangsreihenfolge.</summary>
    [Fact]
    public void OrderForDispatch_SortsAfterClassesAndTargettime()
    {
        var regulär1 = Event(1);
        var verzögertSpät = Event(2, status: "Verzögert", nextAttemptUtc: 2000);
        var regulär2 = Event(3);
        var continuation = Event(4, workItemId: WorkItemA);
        var verzögertFrüh = Event(5, status: "Verzögert", nextAttemptUtc: 1000);
        var items = new List<ScmDispatchPriority.EventSelection>
            { regulär1, verzögertSpät, regulär2, continuation, verzögertFrüh };
        var states = States(
            (WorkItemA, new ScmDispatchPriority.WorkItemState(WorkItemStatus.Started,
                WorkItemExecutionStatus.Completed, null)),
            (WorkItemB, new ScmDispatchPriority.WorkItemState(WorkItemStatus.New,
                WorkItemExecutionStatus.NotStarted, null)));

        var geordnet = ScmDispatchPriority.OrderForDispatch(items, states);

        geordnet.Should().Equal(continuation, verzögertFrüh, verzögertSpät, regulär1, regulär2);
    }

    /// <summary>Verzögert-Einträge ohne Zielzeitpunkt sortieren ans Ende der Klasse 2.</summary>
    [Fact]
    public void OrderForDispatch_DelayedWithoutTargettime_AtClassend()
    {
        var withoutTime = Event(1, status: "Verzögert", nextAttemptUtc: null);
        var withTime = Event(9, status: "Verzögert", nextAttemptUtc: 500);

        var geordnet = ScmDispatchPriority.OrderForDispatch([withoutTime, withTime],
            new Dictionary<Guid, ScmDispatchPriority.WorkItemState>());

        geordnet.Should().Equal(withTime, withoutTime);
    }

    /// <summary>Fehlender Aufgabenzustand fällt in Klasse 3 mit Sequence-Reihenfolge.</summary>
    [Fact]
    public void OrderForDispatch_WithoutWorkitemstate_Inputorder()
    {
        var e2 = Event(2, workItemId: Guid.NewGuid());
        var e1 = Event(1, workItemId: Guid.NewGuid());

        var geordnet = ScmDispatchPriority.OrderForDispatch([e2, e1],
            new Dictionary<Guid, ScmDispatchPriority.WorkItemState>());

        geordnet.Should().Equal(e1, e2);
    }

    /// <summary>Purpose-Tiebreak: Das zweck-markierte Aufbereitungsereignis erhält innerhalb
    /// derselben Prioritätsklasse den Vortritt — unabhängig von der Eingangsreihenfolge.</summary>
    [Fact]
    public void OrderForDispatch_PurposeEventPrecedesSameClassEvents()
    {
        var regulär = Event(1);
        var preparation = Event(2, purpose: ScmIssuePreparationService.Purpose);

        var geordnet = ScmDispatchPriority.OrderForDispatch([regulär, preparation],
            new Dictionary<Guid, ScmDispatchPriority.WorkItemState>());

        geordnet.Should().Equal(preparation, regulär);
    }

    /// <summary>Purpose-Tiebreak in Klasse 2: Bei gleichem Zielzeitpunkt gewinnt das
    /// Aufbereitungsereignis.</summary>
    [Fact]
    public void OrderForDispatch_PurposeEvent_DelayedSameTargetTime_PurposeWins()
    {
        var regulär = Event(1, status: "Verzögert", nextAttemptUtc: 1000);
        var preparation = Event(2, status: "Verzögert", nextAttemptUtc: 1000,
            purpose: ScmIssuePreparationService.Purpose);

        ScmDispatchPriority.OrderForDispatch([regulär, preparation],
                new Dictionary<Guid, ScmDispatchPriority.WorkItemState>())
            .Should().Equal(preparation, regulär);
    }

    /// <summary>Die Zielzeit sortiert vor dem Purpose: Ein früher fälliges reguläres
    /// Ereignis bleibt vor dem Aufbereitungsereignis.</summary>
    [Fact]
    public void OrderForDispatch_PurposeEvent_DelayedEarlierTargetTime_RegularWins()
    {
        var preparation = Event(2, status: "Verzögert", nextAttemptUtc: 1000,
            purpose: ScmIssuePreparationService.Purpose);
        var regulärFrüher = Event(3, status: "Verzögert", nextAttemptUtc: 500);

        ScmDispatchPriority.OrderForDispatch([preparation, regulärFrüher],
                new Dictionary<Guid, ScmDispatchPriority.WorkItemState>())
            .Should().Equal(regulärFrüher, preparation);
    }

    /// <summary>Das Purpose-Ereignis überspringt keine höherpriorisierte Klasse: Fortsetzungen
    /// (Klasse 1) und früher fällige Verzögerungen (Klasse 2) behalten den Vortritt.</summary>
    [Fact]
    public void OrderForDispatch_PurposeEvent_DoesNotJumpEarlierClasses()
    {
        var continuation = Event(1, workItemId: WorkItemA);
        var verzögert = Event(2, status: "Verzögert", nextAttemptUtc: 1000);
        var preparation = Event(3, purpose: ScmIssuePreparationService.Purpose);
        var states = States(
            (WorkItemA, new ScmDispatchPriority.WorkItemState(WorkItemStatus.Started,
                WorkItemExecutionStatus.Completed, null)),
            (WorkItemB, new ScmDispatchPriority.WorkItemState(WorkItemStatus.New,
                WorkItemExecutionStatus.NotStarted, null)));

        var geordnet = ScmDispatchPriority.OrderForDispatch([preparation, verzögert, continuation], states);

        geordnet.Should().Equal(continuation, verzögert, preparation);
    }
}
