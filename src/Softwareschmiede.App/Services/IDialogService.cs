using Softwareschmiede.App.ViewModels;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.App.Services;

/// <summary>Abstrahiert UI-Dialoge für das MVVM-Muster.</summary>
public interface IDialogService
{
    /// <summary>Zeigt eine Bestätigungsabfrage an und gibt zurück, ob der Benutzer bestätigt hat.</summary>
    bool BestaetigenDialog(string nachricht, string titel);

    /// <summary>Öffnet den Repository-Zuweisungs-Dialog und gibt zurück, ob der Benutzer bestätigt hat.</summary>
    bool RepositoryZuweisenDialog(RepositoryAssignViewModel viewModel);

    /// <summary>Öffnet den Dialog zur Bearbeitung des Arbeitsverzeichnisses und gibt zurück, ob der Benutzer bestätigt hat.</summary>
    bool ArbeitsverzeichnisBearbeitenDialog(ArbeitsverzeichnisBearbeitenViewModel viewModel);

    /// <summary>Zeigt den Plugin-Auswahl-Dialog und gibt das Ergebnis der Benutzerauswahl zurück.</summary>
    Task<PluginSelectionResult> ShowPluginSelectionDialogAsync(
        IEnumerable<string> availablePlugins,
        string? currentSelection,
        CancellationToken ct = default);

    /// <summary>Zeigt den Issue-Auswahl-Dialog und gibt das gewählte Issue zurück, oder null wenn abgebrochen.</summary>
    Task<Issue?> ShowIssueSelectionDialogAsync(
        IssueSelectionDialogViewModel viewModel,
        CancellationToken ct = default);

    /// <summary>Zeigt den Issue-Anlage-Dialog und gibt das angelegte Issue zurück, oder null wenn abgebrochen.</summary>
    Task<Issue?> ShowIssueCreateDialogAsync(
        IssueCreateDialogViewModel viewModel,
        CancellationToken ct = default);

    /// <summary>Zeigt den Solution-Auswahl-Dialog und gibt den gewählten Solution-Pfad zurück, oder null wenn abgebrochen.</summary>
    Task<string?> ShowSolutionSelectionDialogAsync(
        IReadOnlyList<string> solutionPfade,
        CancellationToken ct = default);
}
