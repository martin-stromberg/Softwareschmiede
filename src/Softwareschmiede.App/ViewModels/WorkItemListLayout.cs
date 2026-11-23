using System.ComponentModel;
using System.Windows.Data;

namespace Softwareschmiede.App.ViewModels;

/// <summary>
/// Liefert die Sort- und Gruppierungsdeskriptoren der aktiven Aufgabenliste je
/// <see cref="WorkItemListMode"/> und wendet sie auf eine <see cref="ListCollectionView"/> an.
/// </summary>
public static class WorkItemListLayout
{
    /// <summary>Gruppenname für Aufgaben ohne bekannte KI-Zuordnung im KI-Modus.</summary>
    public const string NotExecuted = "Nicht ausgeführt";

    /// <summary>Gruppenname für Aufgaben ohne Repository-Verknüpfung im Repository-Modus.</summary>
    public const string WithoutRepository = "Ohne Repository";

    /// <summary>Gruppenname für Aufgaben ohne bestimmbare Auftragsart im Auftragsart-Modus.</summary>
    public const string WithoutKind = "Ohne Auftragsart";

    /// <summary>Deutscher Anzeigename einer <see cref="Domain.Enums.WorkItemKind"/> — dient als
    /// Gruppenname im Auftragsart-Modus und als sichtbare Typ-Markierung auf der Kachel.</summary>
    public static string KindGroupName(Domain.Enums.WorkItemKind kind)
        => kind switch
        {
            Domain.Enums.WorkItemKind.Manual => "Manuell",
            Domain.Enums.WorkItemKind.Instruction => "Anweisung",
            Domain.Enums.WorkItemKind.IssuePreparation => "Issue-Aufbereitung",
            Domain.Enums.WorkItemKind.PullRequest => "Pull Request",
            Domain.Enums.WorkItemKind.Alert => "Alert",
            _ => WithoutKind
        };

    /// <summary>Unicode-Glyph einer <see cref="Domain.Enums.WorkItemKind"/> für das Auftragsart-Badge
    /// auf der Kachel — <c>null</c> bedeutet „kein Symbol/kein Badge" (<c>Unspecified</c> und
    /// unbekannte Werte).</summary>
    public static string? KindSymbol(Domain.Enums.WorkItemKind kind)
        => kind switch
        {
            Domain.Enums.WorkItemKind.Manual => "✋",
            Domain.Enums.WorkItemKind.Instruction => "📣",
            Domain.Enums.WorkItemKind.IssuePreparation => "🔍",
            Domain.Enums.WorkItemKind.PullRequest => "🔀",
            Domain.Enums.WorkItemKind.Alert => "⚠",
            _ => null
        };

    /// <summary>
    /// Erzeugt die Sortierdeskriptoren für den gegebenen Modus. Die Gruppen-Fallback-Flags werden
    /// aufsteigend sortiert, damit die Fallback-Gruppe ("Nicht ausgeführt"/"Ohne Repository") am
    /// Ende erscheint; <c>Titel</c> und <c>Id</c> dienen als stabile Tie-Breaker bei
    /// Zeitstempel-Gleichstand (identisch zur Datenquelle <c>GetAktiveAufgabenAsync</c>).
    /// </summary>
    /// <param name="mode">Der anzuzeigende Modus.</param>
    /// <returns>Die anzuwendenden Sortierdeskriptoren.</returns>
    public static IReadOnlyList<SortDescription> CreateSortDescriptors(WorkItemListMode mode)
        => mode switch
        {
            WorkItemListMode.Repository =>
            [
                new SortDescription(nameof(ActiveWorkItemPanelItem.RepositoryGroupIsFallback), ListSortDirection.Ascending),
                new SortDescription(nameof(ActiveWorkItemPanelItem.RepositoryGroupName), ListSortDirection.Ascending),
                new SortDescription(nameof(ActiveWorkItemPanelItem.SortTimestamp), ListSortDirection.Descending),
                new SortDescription(nameof(ActiveWorkItemPanelItem.Title), ListSortDirection.Ascending),
                new SortDescription(nameof(ActiveWorkItemPanelItem.Id), ListSortDirection.Ascending),
            ],
            WorkItemListMode.Ai =>
            [
                new SortDescription(nameof(ActiveWorkItemPanelItem.AiGroupIsFallback), ListSortDirection.Ascending),
                new SortDescription(nameof(ActiveWorkItemPanelItem.AiGroupName), ListSortDirection.Ascending),
                new SortDescription(nameof(ActiveWorkItemPanelItem.SortTimestamp), ListSortDirection.Descending),
                new SortDescription(nameof(ActiveWorkItemPanelItem.Title), ListSortDirection.Ascending),
                new SortDescription(nameof(ActiveWorkItemPanelItem.Id), ListSortDirection.Ascending),
            ],
            WorkItemListMode.Kind =>
            [
                new SortDescription(nameof(ActiveWorkItemPanelItem.KindSortOrder), ListSortDirection.Ascending),
                new SortDescription(nameof(ActiveWorkItemPanelItem.SortTimestamp), ListSortDirection.Descending),
                new SortDescription(nameof(ActiveWorkItemPanelItem.Title), ListSortDirection.Ascending),
                new SortDescription(nameof(ActiveWorkItemPanelItem.Id), ListSortDirection.Ascending),
            ],
            _ =>
            [
                new SortDescription(nameof(ActiveWorkItemPanelItem.SortTimestamp), ListSortDirection.Descending),
                new SortDescription(nameof(ActiveWorkItemPanelItem.Title), ListSortDirection.Ascending),
                new SortDescription(nameof(ActiveWorkItemPanelItem.Id), ListSortDirection.Ascending),
            ]
        };

    /// <summary>
    /// Erzeugt den Gruppierungsdeskriptor für den gegebenen Modus oder <c>null</c> im Modus
    /// <see cref="WorkItemListMode.Date"/> (flache Liste ohne Gruppierung).
    /// </summary>
    /// <param name="mode">Der anzuzeigende Modus.</param>
    /// <returns>Der Gruppierungsdeskriptor oder <c>null</c>.</returns>
    public static PropertyGroupDescription? CreateGroupDescriptor(WorkItemListMode mode)
        => mode switch
        {
            WorkItemListMode.Repository => new PropertyGroupDescription(nameof(ActiveWorkItemPanelItem.RepositoryGroupName)),
            WorkItemListMode.Ai => new PropertyGroupDescription(nameof(ActiveWorkItemPanelItem.AiGroupName)),
            WorkItemListMode.Kind => new PropertyGroupDescription(nameof(ActiveWorkItemPanelItem.KindGroupName)),
            _ => null
        };

    /// <summary>Wendet die zum Modus gehörigen Sort- und Gruppierungsdeskriptoren auf die View an.</summary>
    /// <param name="view">Die zu konfigurierende Collection-View.</param>
    /// <param name="mode">Der anzuzeigende Modus.</param>
    public static void Configure(ListCollectionView view, WorkItemListMode mode)
    {
        view.GroupDescriptions.Clear();
        view.SortDescriptions.Clear();

        var groupDescriptor = CreateGroupDescriptor(mode);
        if (groupDescriptor is not null)
        {
            view.GroupDescriptions.Add(groupDescriptor);
        }

        foreach (var sortOrder in CreateSortDescriptors(mode))
        {
            view.SortDescriptions.Add(sortOrder);
        }
    }
}
