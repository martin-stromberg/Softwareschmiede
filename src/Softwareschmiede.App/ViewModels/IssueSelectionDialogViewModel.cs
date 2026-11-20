using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.App.ViewModels;

/// <summary>ViewModel für den Issue-Auswahl-Dialog.</summary>
public sealed class IssueSelectionDialogViewModel : ViewModelBase
{
    private readonly IGitPlugin _gitPlugin;
    private readonly ILogger<IssueSelectionDialogViewModel> _logger;

    private Issue? _selectedIssue;
    private bool _isLoading;

    /// <summary>Wird ausgelöst wenn der Dialog geschlossen werden soll. Parameter: true = bestätigt, false = abgebrochen.</summary>
    public event EventHandler<bool>? CloseRequested;

    /// <summary>Verfügbare Issues zur Auswahl.</summary>
    public ObservableCollection<Issue> AvailableIssues { get; } = new();

    /// <summary>Das vom Benutzer gewählte Issue.</summary>
    public Issue? SelectedIssue
    {
        get => _selectedIssue;
        set
        {
            SetProperty(ref _selectedIssue, value);
            OnPropertyChanged(nameof(CanConfirm));
        }
    }

    /// <summary>Gibt an, ob Issues geladen werden.</summary>
    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    /// <summary>CanExecute für BestaetigenCommand: ein Issue muss gewählt sein.</summary>
    public bool CanConfirm => _selectedIssue != null;

    /// <summary>Bestätigt die Auswahl und schließt den Dialog.</summary>
    public ICommand ConfirmCommand { get; }

    /// <summary>Bricht die Auswahl ab und schließt den Dialog.</summary>
    public ICommand CancelCommand { get; }

    /// <inheritdoc cref="IssueSelectionDialogViewModel"/>
    public IssueSelectionDialogViewModel(IGitPlugin gitPlugin, ILogger<IssueSelectionDialogViewModel> logger)
    {
        _gitPlugin = gitPlugin;
        _logger = logger;

        ConfirmCommand = new RelayCommand(
            () => CloseRequested?.Invoke(this, true),
            () => CanConfirm);
        CancelCommand = new RelayCommand(() => CloseRequested?.Invoke(this, false));
    }

    /// <summary>Lädt Issues für das angegebene Repository.</summary>
    public async Task LoadAsync(string repositoryId, CancellationToken ct = default)
    {
        IsLoading = true;
        AvailableIssues.Clear();

        try
        {
            var issues = await _gitPlugin.GetIssuesAsync(repositoryId, ct);
            foreach (var issue in issues)
                AvailableIssues.Add(issue);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Laden der Issues für Repository {RepositoryId}.", repositoryId);
        }
        finally
        {
            IsLoading = false;
        }
    }
}
