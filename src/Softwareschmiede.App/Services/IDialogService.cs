using Softwareschmiede.App.ViewModels;

namespace Softwareschmiede.App.Services;

/// <summary>Abstrahiert UI-Dialoge für das MVVM-Muster.</summary>
public interface IDialogService
{
    /// <summary>Zeigt eine Bestätigungsabfrage an und gibt zurück, ob der Benutzer bestätigt hat.</summary>
    bool BestaetigenDialog(string nachricht, string titel);

    /// <summary>Öffnet den Repository-Zuweisungs-Dialog und gibt zurück, ob der Benutzer bestätigt hat.</summary>
    bool RepositoryZuweisenDialog(RepositoryAssignViewModel viewModel);

    /// <summary>Zeigt den Plugin-Auswahl-Dialog und gibt das Ergebnis der Benutzerauswahl zurück.</summary>
    Task<PluginSelectionResult> ShowPluginSelectionDialogAsync(
        IEnumerable<string> availablePlugins,
        string? currentSelection,
        CancellationToken ct = default);
}
