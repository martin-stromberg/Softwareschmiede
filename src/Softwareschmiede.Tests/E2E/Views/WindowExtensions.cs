using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using Softwareschmiede.Tests.E2E.Views.Dialogs;

namespace Softwareschmiede.Tests.E2E.Views;

/// <summary>Erweiterungsmethoden für <see cref="Window"/>, die das aktuell sichtbare View-Pattern-Objekt erkennen.</summary>
public static class WindowExtensions
{
    private static readonly Func<Window, DialogView>[] DialogFactories =
    [
        w => new RepositoryAssignDialogView(w),
        w => new PluginSelectionDialogView(w),
        w => new IssueSelectionDialogView(w),
        w => new IssueCreateDialogView(w),
        w => new AutonomAufgabeInitialisierungsDialogView(w),
        w => new ArbeitsverzeichnisBearbeitenDialogView(w),
        w => new OpenTodosDialogView(w),
        w => new HelpTextDialogView(w),
        w => new SolutionSelectionDialogView(w),
        w => new UpdateProgressDialogView(w),
        w => new DeleteConfirmationDialogView(w),
    ];

    /// <summary>
    /// Erkennt anhand charakteristischer UI-Marker die aktuell aktive Ansicht des Hauptfensters und gibt
    /// die passende <see cref="BaseWindowView"/>-Subklasse-Instanz zurück. Prüfreihenfolge: modale Dialoge
    /// (eigenes Fenster, höchste Priorität), Fehlerbanner, verschachtelte Aufgaben-Unteransichten
    /// (Datei-Explorer, To-Dos), Aufgabendetail, Projektdetail, Projektliste, Einstellungen, autonome
    /// Aufgabendetailansicht, zuletzt Fallback auf das Dashboard.
    /// </summary>
    /// <param name="window">Das Hauptfenster der Anwendung.</param>
    /// <returns>Die erkannte View-Instanz.</returns>
    /// <exception cref="InvalidOperationException">Wird geworfen, wenn keine bekannte Ansicht erkannt werden konnte.</exception>
    public static BaseWindowView CurrentView(this Window window)
    {
        // Statt pro bekanntem Dialogtyp eine eigene, vollständige Desktop-Teilbaum-Suche über
        // DialogView.IsVisible auszulösen (12 Suchen, jede davon ein voller FindFirstDescendant-Durchlauf
        // über die komplette Automation-Baumstruktur aller offenen Fenster auf dem Desktop - spürbar
        // langsam), wird die Desktop-Suche einmal zentral ausgeführt: alle aktuell offenen Top-Level-Fenster
        // werden in einem einzigen Durchlauf ermittelt, die Dialogerkennung vergleicht dagegen nur noch
        // client-seitig (siehe DialogView.MatchesOpenWindow).
        var openTopLevelWindowTitles = GetOpenTopLevelWindowTitles(window);

        foreach (var factory in DialogFactories)
        {
            var dialog = factory(window);
            if (dialog.MatchesOpenWindow(openTopLevelWindowTitles))
                return dialog;
        }

        var errorView = new ErrorView(window);
        if (errorView.IsVisible)
            return errorView;

        BaseWindowView[] mainViewCandidates =
        [
            new FileExplorerView(window),
            new TodoListView(window),
            new AutonomAufgabeDetailView(window),
            new TaskDetailView(window),
            new ProjectDetailView(window),
            new ProjectListView(window),
            new SettingsView(window),
        ];

        foreach (var view in mainViewCandidates)
        {
            if (view.IsVisible)
                return view;
        }

        var dashboardView = new DashboardView(window);
        if (dashboardView.IsVisible)
            return dashboardView;

        throw BuildUnrecognizedViewException(window);
    }

    /// <summary>
    /// Ermittelt die Titel aller aktuell offenen Top-Level-Fenster (Control-Type <c>Window</c>) auf dem
    /// Desktop in einem einzigen <c>FindAllDescendants</c>-Durchlauf, statt für jeden bekannten Dialogtyp
    /// separat zu suchen (siehe <see cref="CurrentView"/>).
    /// </summary>
    /// <param name="window">Das Hauptfenster der Anwendung.</param>
    /// <returns>Die Titel aller offenen Top-Level-Fenster.</returns>
    private static HashSet<string> GetOpenTopLevelWindowTitles(Window window)
    {
        var desktop = window.Automation.GetDesktop();
        return desktop
            .FindAllDescendants(cf => cf.ByControlType(ControlType.Window))
            .Where(w =>
            {
                try
                {
                    _ = w.Name;
                    return true;
                }
                catch (FlaUI.Core.Exceptions.PropertyNotSupportedException)
                {
                    return false;
                }
            })
            .Select(w => w.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.Ordinal);
    }

    private static InvalidOperationException BuildUnrecognizedViewException(Window window)
    {
        var expectedMarkers = string.Join(", ",
        [
            "ProjektName", "AufgabeNeu", "EditTitel", "Neu", "Plugins",
            "FileExplorerBaum", "TodosList", "AutonomAufgabeDetailTabs", "FehlerMeldung",
            "Dashboard-Titeltext", "Dialogtitel (z. B. 'Repository zuweisen')"
        ]);

        var visibleNames = window
            .FindAllChildren()
            .Select(e => e.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct()
            .ToArray();

        return new InvalidOperationException(
            $"CurrentView() konnte keine bekannte Ansicht erkennen. Erwartete Marker (mindestens einer je View): " +
            $"{expectedMarkers}. Aktuell sichtbare Top-Level-Elemente im Hauptfenster: {string.Join(", ", visibleNames)}.");
    }
}
