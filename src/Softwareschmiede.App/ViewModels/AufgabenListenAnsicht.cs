using System.ComponentModel;
using System.Windows.Data;

namespace Softwareschmiede.App.ViewModels;

/// <summary>
/// Liefert die Sort- und Gruppierungsdeskriptoren der aktiven Aufgabenliste je
/// <see cref="AufgabenListenModus"/> und wendet sie auf eine <see cref="ListCollectionView"/> an.
/// </summary>
public static class AufgabenListenAnsicht
{
    /// <summary>Gruppenname für Aufgaben ohne bekannte KI-Zuordnung im KI-Modus.</summary>
    public const string NichtAusgefuehrt = "Nicht ausgeführt";

    /// <summary>Gruppenname für Aufgaben ohne Repository-Verknüpfung im Repository-Modus.</summary>
    public const string OhneRepository = "Ohne Repository";

    /// <summary>
    /// Erzeugt die Sortierdeskriptoren für den gegebenen Modus. Die Gruppen-Fallback-Flags werden
    /// aufsteigend sortiert, damit die Fallback-Gruppe ("Nicht ausgeführt"/"Ohne Repository") am
    /// Ende erscheint; <c>Titel</c> und <c>Id</c> dienen als stabile Tie-Breaker bei
    /// Zeitstempel-Gleichstand (identisch zur Datenquelle <c>GetAktiveAufgabenAsync</c>).
    /// </summary>
    /// <param name="modus">Der anzuzeigende Modus.</param>
    /// <returns>Die anzuwendenden Sortierdeskriptoren.</returns>
    public static IReadOnlyList<SortDescription> ErstelleSortierDeskriptoren(AufgabenListenModus modus)
        => modus switch
        {
            AufgabenListenModus.Repository =>
            [
                new SortDescription(nameof(AktiveAufgabePanelItem.RepositoryGruppeIstFallback), ListSortDirection.Ascending),
                new SortDescription(nameof(AktiveAufgabePanelItem.RepositoryGruppenName), ListSortDirection.Ascending),
                new SortDescription(nameof(AktiveAufgabePanelItem.SortierZeitpunkt), ListSortDirection.Descending),
                new SortDescription(nameof(AktiveAufgabePanelItem.Titel), ListSortDirection.Ascending),
                new SortDescription(nameof(AktiveAufgabePanelItem.Id), ListSortDirection.Ascending),
            ],
            AufgabenListenModus.Ki =>
            [
                new SortDescription(nameof(AktiveAufgabePanelItem.KiGruppeIstFallback), ListSortDirection.Ascending),
                new SortDescription(nameof(AktiveAufgabePanelItem.KiGruppenName), ListSortDirection.Ascending),
                new SortDescription(nameof(AktiveAufgabePanelItem.SortierZeitpunkt), ListSortDirection.Descending),
                new SortDescription(nameof(AktiveAufgabePanelItem.Titel), ListSortDirection.Ascending),
                new SortDescription(nameof(AktiveAufgabePanelItem.Id), ListSortDirection.Ascending),
            ],
            _ =>
            [
                new SortDescription(nameof(AktiveAufgabePanelItem.SortierZeitpunkt), ListSortDirection.Descending),
                new SortDescription(nameof(AktiveAufgabePanelItem.Titel), ListSortDirection.Ascending),
                new SortDescription(nameof(AktiveAufgabePanelItem.Id), ListSortDirection.Ascending),
            ]
        };

    /// <summary>
    /// Erzeugt den Gruppierungsdeskriptor für den gegebenen Modus oder <c>null</c> im Modus
    /// <see cref="AufgabenListenModus.Datum"/> (flache Liste ohne Gruppierung).
    /// </summary>
    /// <param name="modus">Der anzuzeigende Modus.</param>
    /// <returns>Der Gruppierungsdeskriptor oder <c>null</c>.</returns>
    public static PropertyGroupDescription? ErstelleGruppenDeskriptor(AufgabenListenModus modus)
        => modus switch
        {
            AufgabenListenModus.Repository => new PropertyGroupDescription(nameof(AktiveAufgabePanelItem.RepositoryGruppenName)),
            AufgabenListenModus.Ki => new PropertyGroupDescription(nameof(AktiveAufgabePanelItem.KiGruppenName)),
            _ => null
        };

    /// <summary>Wendet die zum Modus gehörigen Sort- und Gruppierungsdeskriptoren auf die View an.</summary>
    /// <param name="view">Die zu konfigurierende Collection-View.</param>
    /// <param name="modus">Der anzuzeigende Modus.</param>
    public static void Konfiguriere(ListCollectionView view, AufgabenListenModus modus)
    {
        view.GroupDescriptions.Clear();
        view.SortDescriptions.Clear();

        var gruppenDeskriptor = ErstelleGruppenDeskriptor(modus);
        if (gruppenDeskriptor is not null)
        {
            view.GroupDescriptions.Add(gruppenDeskriptor);
        }

        foreach (var sortierung in ErstelleSortierDeskriptoren(modus))
        {
            view.SortDescriptions.Add(sortierung);
        }
    }
}
