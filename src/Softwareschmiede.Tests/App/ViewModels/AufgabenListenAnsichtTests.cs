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
        var gruppierung = AufgabenListenAnsicht.ErstelleGruppenDeskriptor(AufgabenListenModus.Datum);

        sortierungen[0].Should().Be(new SortDescription(nameof(AktiveAufgabePanelItem.SortierZeitpunkt), ListSortDirection.Descending));
        gruppierung.Should().BeNull();
    }

    /// <summary>Konfiguriere im Modus Repository gruppiert nach RepositoryGruppenName mit Fallback-Gruppe am Ende.</summary>
    [Fact]
    public void Konfiguriere_ShouldApplyGroupingAndSorting_WhenModusIstRepository()
    {
        var sortierungen = AufgabenListenAnsicht.ErstelleSortierDeskriptoren(AufgabenListenModus.Repository);
        var gruppierung = AufgabenListenAnsicht.ErstelleGruppenDeskriptor(AufgabenListenModus.Repository);

        gruppierung.Should().NotBeNull();
        gruppierung!.PropertyName.Should().Be(nameof(AktiveAufgabePanelItem.RepositoryGruppenName));
        sortierungen[0].Should().Be(new SortDescription(nameof(AktiveAufgabePanelItem.RepositoryGruppeIstFallback), ListSortDirection.Ascending));
        sortierungen[1].Should().Be(new SortDescription(nameof(AktiveAufgabePanelItem.RepositoryGruppenName), ListSortDirection.Ascending));
        sortierungen[2].Should().Be(new SortDescription(nameof(AktiveAufgabePanelItem.SortierZeitpunkt), ListSortDirection.Descending));
    }

    /// <summary>Konfiguriere im Modus Ki gruppiert nach KiGruppenName mit Fallback-Gruppe am Ende.</summary>
    [Fact]
    public void Konfiguriere_ShouldApplyGroupingAndSorting_WhenModusIstKi()
    {
        var sortierungen = AufgabenListenAnsicht.ErstelleSortierDeskriptoren(AufgabenListenModus.Ki);
        var gruppierung = AufgabenListenAnsicht.ErstelleGruppenDeskriptor(AufgabenListenModus.Ki);

        gruppierung.Should().NotBeNull();
        gruppierung!.PropertyName.Should().Be(nameof(AktiveAufgabePanelItem.KiGruppenName));
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
            new() { Id = Guid.NewGuid(), Titel = "C", RepositoryGruppenName = AufgabenListenAnsicht.OhneRepository, RepositoryGruppeIstFallback = true, SortierZeitpunkt = DateTimeOffset.UtcNow },
            new() { Id = Guid.NewGuid(), Titel = "B", RepositoryGruppenName = "zeta-repo", SortierZeitpunkt = DateTimeOffset.UtcNow.AddHours(-1) },
            new() { Id = Guid.NewGuid(), Titel = "A", RepositoryGruppenName = "alpha-repo", SortierZeitpunkt = DateTimeOffset.UtcNow.AddHours(-2) }
        };
        var view = new System.Windows.Data.ListCollectionView(items);

        AufgabenListenAnsicht.Konfiguriere(view, AufgabenListenModus.Repository);

        var gruppen = view.Groups!.Cast<System.Windows.Data.CollectionViewGroup>().Select(g => (string)g.Name!).ToArray();
        gruppen.Should().Equal("alpha-repo", "zeta-repo", AufgabenListenAnsicht.OhneRepository);
    }

    /// <summary>Innerhalb einer Gruppe wird absteigend nach SortierZeitpunkt sortiert.</summary>
    [Fact]
    public void Konfiguriere_ShouldSortWithinGroupBySortierZeitpunktDesc()
    {
        var aelteste = DateTimeOffset.UtcNow.AddHours(-3);
        var items = new List<AktiveAufgabePanelItem>
        {
            new() { Id = Guid.NewGuid(), Titel = "Alt", KiGruppenName = "KI Simulator", SortierZeitpunkt = aelteste },
            new() { Id = Guid.NewGuid(), Titel = "Neu", KiGruppenName = "KI Simulator", SortierZeitpunkt = DateTimeOffset.UtcNow },
            new() { Id = Guid.NewGuid(), Titel = "Mitte", KiGruppenName = "KI Simulator", SortierZeitpunkt = aelteste.AddHours(1) }
        };
        var view = new System.Windows.Data.ListCollectionView(items);

        AufgabenListenAnsicht.Konfiguriere(view, AufgabenListenModus.Ki);

        var titel = view.Cast<AktiveAufgabePanelItem>().Select(i => i.Titel).ToArray();
        titel.Should().Equal("Neu", "Mitte", "Alt");
    }
}
