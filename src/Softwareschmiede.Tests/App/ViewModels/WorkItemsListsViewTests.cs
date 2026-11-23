using System.ComponentModel;
using FluentAssertions;
using Softwareschmiede.App.ViewModels;
using Softwareschmiede.Domain.Enums;

namespace Softwareschmiede.Tests.App.ViewModels;

/// <summary>Tests für die Sort-/Gruppierungsdeskriptoren von AufgabenListenAnsicht.</summary>
public sealed class WorkItemsListsViewTests
{
    /// <summary>Konfiguriere im Modus Datum sortiert nur nach SortierZeitpunkt absteigend, ohne Gruppierung.</summary>
    [Fact]
    public void Configure_ShouldApplyGroupingAndSorting_WhenModeIsDate()
    {
        var sort = WorkItemListLayout.CreateSortDescriptors(WorkItemListMode.Date);
        var grouping = WorkItemListLayout.CreateGroupDescriptor(WorkItemListMode.Date);

        sort[0].Should().Be(new SortDescription(nameof(ActiveWorkItemPanelItem.SortTimestamp), ListSortDirection.Descending));
        grouping.Should().BeNull();
    }

    /// <summary>Konfiguriere im Modus Repository gruppiert nach RepositoryGruppenName mit Fallback-Gruppe am Ende.</summary>
    [Fact]
    public void Configure_ShouldApplyGroupingAndSorting_WhenModeIsRepository()
    {
        var sort = WorkItemListLayout.CreateSortDescriptors(WorkItemListMode.Repository);
        var grouping = WorkItemListLayout.CreateGroupDescriptor(WorkItemListMode.Repository);

        grouping.Should().NotBeNull();
        grouping!.PropertyName.Should().Be(nameof(ActiveWorkItemPanelItem.RepositoryGroupName));
        sort[0].Should().Be(new SortDescription(nameof(ActiveWorkItemPanelItem.RepositoryGroupIsFallback), ListSortDirection.Ascending));
        sort[1].Should().Be(new SortDescription(nameof(ActiveWorkItemPanelItem.RepositoryGroupName), ListSortDirection.Ascending));
        sort[2].Should().Be(new SortDescription(nameof(ActiveWorkItemPanelItem.SortTimestamp), ListSortDirection.Descending));
    }

    /// <summary>Konfiguriere im Modus Ki gruppiert nach KiGruppenName mit Fallback-Gruppe am Ende.</summary>
    [Fact]
    public void Configure_ShouldApplyGroupingAndSorting_WhenModeIsAi()
    {
        var sort = WorkItemListLayout.CreateSortDescriptors(WorkItemListMode.Ai);
        var grouping = WorkItemListLayout.CreateGroupDescriptor(WorkItemListMode.Ai);

        grouping.Should().NotBeNull();
        grouping!.PropertyName.Should().Be(nameof(ActiveWorkItemPanelItem.AiGroupName));
        sort[0].Should().Be(new SortDescription(nameof(ActiveWorkItemPanelItem.AiGroupIsFallback), ListSortDirection.Ascending));
        sort[1].Should().Be(new SortDescription(nameof(ActiveWorkItemPanelItem.AiGroupName), ListSortDirection.Ascending));
        sort[2].Should().Be(new SortDescription(nameof(ActiveWorkItemPanelItem.SortTimestamp), ListSortDirection.Descending));
    }

    /// <summary>Konfiguriere im Modus Art gruppiert nach Auftragsart mit Fallback-Gruppe am Ende.</summary>
    [Fact]
    public void Configure_ShouldApplyGroupingAndSorting_WhenModeIsKind()
    {
        var sort = WorkItemListLayout.CreateSortDescriptors(WorkItemListMode.Kind);
        var grouping = WorkItemListLayout.CreateGroupDescriptor(WorkItemListMode.Kind);

        grouping.Should().NotBeNull();
        grouping!.PropertyName.Should().Be(nameof(ActiveWorkItemPanelItem.KindGroupName));
        sort[0].Should().Be(new SortDescription(nameof(ActiveWorkItemPanelItem.KindSortOrder), ListSortDirection.Ascending));
    }

    /// <summary>Die Fallback-Gruppe erscheint im gruppierten Modus als letzte Gruppe.</summary>
    [Fact]
    public void Configure_ShouldPlaceFallbackGroupLast_WhenGrouped()
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
    public void Configure_ShouldSortWithinGroupBySortTimestampDesc()
    {
        var oldest = DateTimeOffset.UtcNow.AddHours(-3);
        var items = new List<ActiveWorkItemPanelItem>
        {
            new() { Id = Guid.NewGuid(), Title = "Alt", AiGroupName = "KI Simulator", SortTimestamp = oldest },
            new() { Id = Guid.NewGuid(), Title = "Neu", AiGroupName = "KI Simulator", SortTimestamp = DateTimeOffset.UtcNow },
            new() { Id = Guid.NewGuid(), Title = "Mitte", AiGroupName = "KI Simulator", SortTimestamp = oldest.AddHours(1) }
        };
        var view = new System.Windows.Data.ListCollectionView(items);

        WorkItemListLayout.Configure(view, WorkItemListMode.Ai);

        var title = view.Cast<ActiveWorkItemPanelItem>().Select(i => i.Title).ToArray();
        title.Should().Equal("Neu", "Mitte", "Alt");
    }

    /// <summary>Das Symbol-Mapping liefert je sichtbarer Auftragsart den dokumentierten Glyph.</summary>
    [Theory]
    [InlineData(WorkItemKind.Manual, "✋")]
    [InlineData(WorkItemKind.Instruction, "📣")]
    [InlineData(WorkItemKind.IssuePreparation, "🔍")]
    [InlineData(WorkItemKind.PullRequest, "🔀")]
    [InlineData(WorkItemKind.Alert, "⚠")]
    public void KindSymbol_ShouldReturnExpectedGlyph_ForEachVisibleKind(WorkItemKind kind, string expected)
    {
        WorkItemListLayout.KindSymbol(kind).Should().Be(expected);
    }

    /// <summary>Nicht darstellbare bzw. unbekannte Werte liefern null — Grundlage der Badge-Ausblendung.</summary>
    [Theory]
    [InlineData(WorkItemKind.Unspecified)]
    [InlineData((WorkItemKind)999)]
    public void KindSymbol_ShouldReturnNull_ForUnspecifiedAndUndefined(WorkItemKind kind)
    {
        WorkItemListLayout.KindSymbol(kind).Should().BeNull();
    }

    /// <summary>Alle definierten Werte außer Unspecified liefern nicht-leere, paarweise verschiedene Glyphen.</summary>
    [Fact]
    public void KindSymbol_ShouldBeDefinedAndDistinct_ForAllVisibleKinds()
    {
        var symbols = Enum.GetValues<WorkItemKind>()
            .Where(k => k != WorkItemKind.Unspecified)
            .Select(WorkItemListLayout.KindSymbol)
            .ToArray();

        symbols.Should().OnlyContain(s => !string.IsNullOrEmpty(s),
            "jede sichtbare Auftragsart muss ein Symbol liefern");
        symbols.Should().OnlyHaveUniqueItems(
            "die Symbole der Auftragsarten müssen paarweise unterscheidbar sein");
    }
}
