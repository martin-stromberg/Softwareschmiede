using System.ComponentModel;
using FluentAssertions;
using Softwareschmiede.App.ViewModels;

namespace Softwareschmiede.Tests.App.ViewModels;

/// <summary>Tests für die Sort-/Gruppierungsdeskriptoren von AufgabenListenAnsicht.</summary>
public sealed class AufgabenListenAnsichtTests
{
    /// <summary>Konfiguriere im Modus Datum sortiert nur nach SortierZeitpunkt absteigend, ohne Gruppierung.</summary>
    [Fact]
    public void Konfiguriere_ShouldApplyGroupingAndSorting_WhenModusIstDatum()
    {
        var sortierungen = AufgabenListenAnsicht.ErstelleSortierDeskriptoren(AufgabenListenModus.Datum);
        var grouping = AufgabenListenAnsicht.ErstelleGruppenDeskriptor(AufgabenListenModus.Datum);

        sortierungen[0].Should().Be(new SortDescription(nameof(AktiveAufgabePanelItem.SortierZeitpunkt), ListSortDirection.Descending));
        grouping.Should().BeNull();
    }

    /// <summary>Konfiguriere im Modus Repository gruppiert nach RepositoryGruppenName mit Fallback-Gruppe am Ende.</summary>
    [Fact]
    public void Konfiguriere_ShouldApplyGroupingAndSorting_WhenModusIstRepository()
    {
        var sortierungen = AufgabenListenAnsicht.ErstelleSortierDeskriptoren(AufgabenListenModus.Repository);
        var grouping = AufgabenListenAnsicht.ErstelleGruppenDeskriptor(AufgabenListenModus.Repository);

        grouping.Should().NotBeNull();
        grouping!.PropertyName.Should().Be(nameof(AktiveAufgabePanelItem.RepositoryGruppenName));
        sortierungen[0].Should().Be(new SortDescription(nameof(AktiveAufgabePanelItem.RepositoryGruppeIstFallback), ListSortDirection.Ascending));
        sortierungen[1].Should().Be(new SortDescription(nameof(AktiveAufgabePanelItem.RepositoryGruppenName), ListSortDirection.Ascending));
        sortierungen[2].Should().Be(new SortDescription(nameof(AktiveAufgabePanelItem.SortierZeitpunkt), ListSortDirection.Descending));
    }

    /// <summary>Konfiguriere im Modus Ki gruppiert nach KiGruppenName mit Fallback-Gruppe am Ende.</summary>
    [Fact]
    public void Konfiguriere_ShouldApplyGroupingAndSorting_WhenModusIstKi()
    {
        var sortierungen = AufgabenListenAnsicht.ErstelleSortierDeskriptoren(AufgabenListenModus.Ki);
        var grouping = AufgabenListenAnsicht.ErstelleGruppenDeskriptor(AufgabenListenModus.Ki);

        grouping.Should().NotBeNull();
        grouping!.PropertyName.Should().Be(nameof(AktiveAufgabePanelItem.KiGruppenName));
        sortierungen[0].Should().Be(new SortDescription(nameof(AktiveAufgabePanelItem.KiGruppeIstFallback), ListSortDirection.Ascending));
        sortierungen[1].Should().Be(new SortDescription(nameof(AktiveAufgabePanelItem.KiGruppenName), ListSortDirection.Ascending));
        sortierungen[2].Should().Be(new SortDescription(nameof(AktiveAufgabePanelItem.SortierZeitpunkt), ListSortDirection.Descending));
    }

    /// <summary>Die Fallback-Gruppe erscheint im gruppierten Modus als letzte Gruppe.</summary>
    [Fact]
    public void Konfiguriere_ShouldPlaceFallbackGroupLast_WhenGrouped()
    {
        var items = new List<AktiveAufgabePanelItem>
        {
            new() { Id = Guid.NewGuid(), Title = "C", RepositoryGruppenName = AufgabenListenAnsicht.OhneRepository, RepositoryGruppeIstFallback = true, SortierZeitpunkt = DateTimeOffset.UtcNow },
            new() { Id = Guid.NewGuid(), Title = "B", RepositoryGruppenName = "zeta-repo", SortierZeitpunkt = DateTimeOffset.UtcNow.AddHours(-1) },
            new() { Id = Guid.NewGuid(), Title = "A", RepositoryGruppenName = "alpha-repo", SortierZeitpunkt = DateTimeOffset.UtcNow.AddHours(-2) }
        };
        var view = new System.Windows.Data.ListCollectionView(items);

        AufgabenListenAnsicht.Konfiguriere(view, AufgabenListenModus.Repository);

        var groups = view.Groups!.Cast<System.Windows.Data.CollectionViewGroup>().Select(g => (string)g.Name!).ToArray();
        groups.Should().Equal("alpha-repo", "zeta-repo", AufgabenListenAnsicht.OhneRepository);
    }

    /// <summary>Innerhalb einer Gruppe wird absteigend nach SortierZeitpunkt sortiert.</summary>
    [Fact]
    public void Konfiguriere_ShouldSortWithinGroupBySortierZeitpunktDesc()
    {
        var aelteste = DateTimeOffset.UtcNow.AddHours(-3);
        var items = new List<AktiveAufgabePanelItem>
        {
            new() { Id = Guid.NewGuid(), Title = "Alt", KiGruppenName = "KI Simulator", SortierZeitpunkt = aelteste },
            new() { Id = Guid.NewGuid(), Title = "Neu", KiGruppenName = "KI Simulator", SortierZeitpunkt = DateTimeOffset.UtcNow },
            new() { Id = Guid.NewGuid(), Title = "Mitte", KiGruppenName = "KI Simulator", SortierZeitpunkt = aelteste.AddHours(1) }
        };
        var view = new System.Windows.Data.ListCollectionView(items);

        AufgabenListenAnsicht.Konfiguriere(view, AufgabenListenModus.Ki);

        var title = view.Cast<AktiveAufgabePanelItem>().Select(i => i.Title).ToArray();
        title.Should().Equal("Neu", "Mitte", "Alt");
    }
}
