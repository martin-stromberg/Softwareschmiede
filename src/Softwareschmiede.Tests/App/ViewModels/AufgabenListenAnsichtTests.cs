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
        var sortierungen = WorkItemListLayout.CreateSortDescriptors(WorkItemListMode.Date);
        var grouping = WorkItemListLayout.CreateGroupDescriptor(WorkItemListMode.Date);

        sortierungen[0].Should().Be(new SortDescription(nameof(ActiveWorkItemPanelItem.SortTimestamp), ListSortDirection.Descending));
        grouping.Should().BeNull();
    }

    /// <summary>Konfiguriere im Modus Repository gruppiert nach RepositoryGruppenName mit Fallback-Gruppe am Ende.</summary>
    [Fact]
    public void Konfiguriere_ShouldApplyGroupingAndSorting_WhenModusIstRepository()
    {
        var sortierungen = WorkItemListLayout.CreateSortDescriptors(WorkItemListMode.Repository);
        var grouping = WorkItemListLayout.CreateGroupDescriptor(WorkItemListMode.Repository);

        grouping.Should().NotBeNull();
        grouping!.PropertyName.Should().Be(nameof(ActiveWorkItemPanelItem.RepositoryGroupName));
        sortierungen[0].Should().Be(new SortDescription(nameof(ActiveWorkItemPanelItem.RepositoryGroupIsFallback), ListSortDirection.Ascending));
        sortierungen[1].Should().Be(new SortDescription(nameof(ActiveWorkItemPanelItem.RepositoryGroupName), ListSortDirection.Ascending));
        sortierungen[2].Should().Be(new SortDescription(nameof(ActiveWorkItemPanelItem.SortTimestamp), ListSortDirection.Descending));
    }

    /// <summary>Konfiguriere im Modus Ki gruppiert nach KiGruppenName mit Fallback-Gruppe am Ende.</summary>
    [Fact]
    public void Konfiguriere_ShouldApplyGroupingAndSorting_WhenModusIstKi()
    {
        var sortierungen = WorkItemListLayout.CreateSortDescriptors(WorkItemListMode.Ai);
        var grouping = WorkItemListLayout.CreateGroupDescriptor(WorkItemListMode.Ai);

        grouping.Should().NotBeNull();
        grouping!.PropertyName.Should().Be(nameof(ActiveWorkItemPanelItem.AiGroupName));
        sortierungen[0].Should().Be(new SortDescription(nameof(ActiveWorkItemPanelItem.AiGroupIsFallback), ListSortDirection.Ascending));
        sortierungen[1].Should().Be(new SortDescription(nameof(ActiveWorkItemPanelItem.AiGroupName), ListSortDirection.Ascending));
        sortierungen[2].Should().Be(new SortDescription(nameof(ActiveWorkItemPanelItem.SortTimestamp), ListSortDirection.Descending));
    }

    /// <summary>Die Fallback-Gruppe erscheint im gruppierten Modus als letzte Gruppe.</summary>
    [Fact]
    public void Konfiguriere_ShouldPlaceFallbackGroupLast_WhenGrouped()
    {
        var items = new List<ActiveWorkItemPanelItem>
        {
            new() { Id = Guid.NewGuid(), Title = "C", RepositoryGroupName = WorkItemListLayout.WithoutRepository, RepositoryGroupIsFallback = true, SortTimestamp = DateTimeOffset.UtcNow },
            new() { Id = Guid.NewGuid(), Title = "B", RepositoryGroupName = "zeta-repo", SortTimestamp = DateTimeOffset.UtcNow.AddHours(-1) },
            new() { Id = Guid.NewGuid(), Title = "A", RepositoryGroupName = "alpha-repo", SortTimestamp = DateTimeOffset.UtcNow.AddHours(-2) }
        };
        var view = new System.Windows.Data.ListCollectionView(items);

        WorkItemListLayout.Configure(view, WorkItemListMode.Repository);

        var groups = view.Groups!.Cast<System.Windows.Data.CollectionViewGroup>().Select(g => (string)g.Name!).ToArray();
        groups.Should().Equal("alpha-repo", "zeta-repo", WorkItemListLayout.WithoutRepository);
    }

    /// <summary>Innerhalb einer Gruppe wird absteigend nach SortierZeitpunkt sortiert.</summary>
    [Fact]
    public void Konfiguriere_ShouldSortWithinGroupBySortierZeitpunktDesc()
    {
        var aelteste = DateTimeOffset.UtcNow.AddHours(-3);
        var items = new List<ActiveWorkItemPanelItem>
        {
            new() { Id = Guid.NewGuid(), Title = "Alt", AiGroupName = "KI Simulator", SortTimestamp = aelteste },
            new() { Id = Guid.NewGuid(), Title = "Neu", AiGroupName = "KI Simulator", SortTimestamp = DateTimeOffset.UtcNow },
            new() { Id = Guid.NewGuid(), Title = "Mitte", AiGroupName = "KI Simulator", SortTimestamp = aelteste.AddHours(1) }
        };
        var view = new System.Windows.Data.ListCollectionView(items);

        WorkItemListLayout.Configure(view, WorkItemListMode.Ai);

        var title = view.Cast<ActiveWorkItemPanelItem>().Select(i => i.Title).ToArray();
        title.Should().Equal("Neu", "Mitte", "Alt");
    }
}
