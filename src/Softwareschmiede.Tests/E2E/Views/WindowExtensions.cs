using FlaUI.Core.AutomationElements;
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
        w => new AutonomAufgabeDetailDialogView(w),
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
        foreach (var factory in DialogFactories)
        {
            var dialog = factory(window);
            if (dialog.IsVisible)
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
