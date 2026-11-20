using FluentAssertions;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Enums;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>Tests für <see cref="ScmDispatchPrioritaet"/> (Issue 410, Schritt 2).</summary>
public sealed class ScmDispatchPrioritaetTests
{
    private static readonly Guid AufgabeA = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid AufgabeB = new("22222222-2222-2222-2222-222222222222");

    private static ScmDispatchPrioritaet.EreignisAuswahl Ereignis(long sequence, Guid? workItemId = null,
        string status = "Wartend", long? nextAttemptUtc = null)
        => new(Guid.NewGuid(), workItemId ?? AufgabeB, sequence, status, nextAttemptUtc);

    private static Dictionary<Guid, ScmDispatchPrioritaet.AufgabenZustand> Zustaende(
        params (Guid Id, ScmDispatchPrioritaet.AufgabenZustand Zustand)[] eintraege)
        => eintraege.ToDictionary(x => x.Id, x => x.Zustand);

    /// <summary>Klasse 1: gesetzte Pausierungszeit (auch abgelaufen) markiert eine Fortsetzung.</summary>
    [Fact]
    public void PrioritaetsKlasse_Pausiert_GibtKlasse1()
    {
        var zustand = new ScmDispatchPrioritaet.AufgabenZustand(WorkItemStatus.Waiting,
            WorkItemExecutionStatus.Active, DateTimeOffset.UtcNow.AddMinutes(-5));

        ScmDispatchPrioritaet.PrioritaetsKlasse(Ereignis(1, AufgabeA), zustand).Should().Be(1);
    }

    /// <summary>Klasse 1: beendeter Lauf einer aktiven Aufgabe (Neu/Gestartet/Wartend).</summary>
    [Theory]
    [InlineData(WorkItemStatus.New)]
    [InlineData(WorkItemStatus.Started)]
    [InlineData(WorkItemStatus.Waiting)]
    public void PrioritaetsKlasse_LaufBeendetAufgabeAktiv_GibtKlasse1(WorkItemStatus status)
    {
        var zustand = new ScmDispatchPrioritaet.AufgabenZustand(status,
            WorkItemExecutionStatus.Completed, null);

        ScmDispatchPrioritaet.PrioritaetsKlasse(Ereignis(1, AufgabeA), zustand).Should().Be(1);
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
        var zustand = new ScmDispatchPrioritaet.AufgabenZustand(status, executionStatus, null);

        ScmDispatchPrioritaet.PrioritaetsKlasse(Ereignis(1, AufgabeA, "Wartend"), zustand).Should().Be(3);
    }

    /// <summary>Auch eine zukünftige Pausierungszeit zählt als Fortsetzungsereignis (Klasse 1).</summary>
    [Fact]
    public void PrioritaetsKlasse_PausiertInZukunft_GibtKlasse1()
    {
        var zustand = new ScmDispatchPrioritaet.AufgabenZustand(WorkItemStatus.Waiting,
            WorkItemExecutionStatus.Active, DateTimeOffset.UtcNow.AddHours(1));

        ScmDispatchPrioritaet.PrioritaetsKlasse(Ereignis(1, AufgabeA), zustand).Should().Be(1);
    }

    /// <summary>Beendete/Archivierte Aufgabe unterdrückt Klasse 1 auch bei gesetzter Pause.</summary>
    [Theory]
    [InlineData(WorkItemStatus.Completed)]
    [InlineData(WorkItemStatus.Archived)]
    public void PrioritaetsKlasse_PausiertAberAufgabeBeendet_NichtKlasse1(WorkItemStatus status)
    {
        var zustand = new ScmDispatchPrioritaet.AufgabenZustand(status,
            WorkItemExecutionStatus.Active, DateTimeOffset.UtcNow.AddMinutes(-5));

        ScmDispatchPrioritaet.PrioritaetsKlasse(Ereignis(1, AufgabeA, "Wartend"), zustand).Should().Be(3);
    }

    /// <summary>Klasse 2: „Verzögert"-Ereignisse.</summary>
    [Fact]
    public void PrioritaetsKlasse_Verzoegert_GibtKlasse2()
    {
        var zustand = new ScmDispatchPrioritaet.AufgabenZustand(WorkItemStatus.New,
            WorkItemExecutionStatus.NotStarted, null);

        ScmDispatchPrioritaet.PrioritaetsKlasse(Ereignis(1, AufgabeA, "Verzögert"), zustand).Should().Be(2);
    }

    /// <summary>Fortsetzung schlägt Verzögerung: Pausierte Aufgabe mit „Verzögert"-Ereignis bleibt Klasse 1.</summary>
    [Fact]
    public void PrioritaetsKlasse_FortsetzungSchlaegtVerzoegerung()
    {
        var zustand = new ScmDispatchPrioritaet.AufgabenZustand(WorkItemStatus.Waiting,
            WorkItemExecutionStatus.Active, DateTimeOffset.UtcNow.AddMinutes(-5));

        ScmDispatchPrioritaet.PrioritaetsKlasse(Ereignis(1, AufgabeA, "Verzögert"), zustand).Should().Be(1);
    }

    /// <summary>Gesamtreihenfolge: Fortsetzung → Verzögerungen (zeitaufsteigend) → Eingangsreihenfolge.</summary>
    [Fact]
    public void OrderForDispatch_SortiertNachKlassenUndZielzeit()
    {
        var regulär1 = Ereignis(1);
        var verzögertSpät = Ereignis(2, status: "Verzögert", nextAttemptUtc: 2000);
        var regulär2 = Ereignis(3);
        var fortsetzung = Ereignis(4, workItemId: AufgabeA);
        var verzögertFrüh = Ereignis(5, status: "Verzögert", nextAttemptUtc: 1000);
        var items = new List<ScmDispatchPrioritaet.EreignisAuswahl>
            { regulär1, verzögertSpät, regulär2, fortsetzung, verzögertFrüh };
        var zustaende = Zustaende(
            (AufgabeA, new ScmDispatchPrioritaet.AufgabenZustand(WorkItemStatus.Started,
                WorkItemExecutionStatus.Completed, null)),
            (AufgabeB, new ScmDispatchPrioritaet.AufgabenZustand(WorkItemStatus.New,
                WorkItemExecutionStatus.NotStarted, null)));

        var geordnet = ScmDispatchPrioritaet.OrderForDispatch(items, zustaende);

        geordnet.Should().Equal(fortsetzung, verzögertFrüh, verzögertSpät, regulär1, regulär2);
    }

    /// <summary>Verzögert-Einträge ohne Zielzeitpunkt sortieren ans Ende der Klasse 2.</summary>
    [Fact]
    public void OrderForDispatch_VerzoegertOhneZielzeit_AmKlassenende()
    {
        var ohneZeit = Ereignis(1, status: "Verzögert", nextAttemptUtc: null);
        var mitZeit = Ereignis(9, status: "Verzögert", nextAttemptUtc: 500);

        var geordnet = ScmDispatchPrioritaet.OrderForDispatch([ohneZeit, mitZeit],
            new Dictionary<Guid, ScmDispatchPrioritaet.AufgabenZustand>());

        geordnet.Should().Equal(mitZeit, ohneZeit);
    }

    /// <summary>Fehlender Aufgabenzustand fällt in Klasse 3 mit Sequence-Reihenfolge.</summary>
    [Fact]
    public void OrderForDispatch_OhneAufgabenzustand_Eingangsreihenfolge()
    {
        var e2 = Ereignis(2, workItemId: Guid.NewGuid());
        var e1 = Ereignis(1, workItemId: Guid.NewGuid());

        var geordnet = ScmDispatchPrioritaet.OrderForDispatch([e2, e1],
            new Dictionary<Guid, ScmDispatchPrioritaet.AufgabenZustand>());

        geordnet.Should().Equal(e1, e2);
    }
}
