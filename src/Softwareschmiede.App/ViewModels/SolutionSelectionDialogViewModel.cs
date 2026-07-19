using System.Windows.Input;

namespace Softwareschmiede.App.ViewModels;

/// <summary>ViewModel für den Solution-Auswahl-Dialog bei mehreren gefundenen <c>*.sln</c>-Dateien.</summary>
public sealed class SolutionSelectionDialogViewModel : ViewModelBase
{
    private string? _selectedSolution;

    /// <summary>Wird ausgelöst wenn der Dialog geschlossen werden soll. Parameter: true = bestätigt, false = abgebrochen.</summary>
    public event EventHandler<bool>? CloseRequested;

    /// <summary>Die zur Auswahl stehenden Solution-Pfade.</summary>
    public IReadOnlyList<string> Solutions { get; }

    /// <summary>Der vom Benutzer gewählte Solution-Pfad.</summary>
    public string? SelectedSolution
    {
        get => _selectedSolution;
        set
        {
            SetProperty(ref _selectedSolution, value);
            OnPropertyChanged(nameof(KannBestaetigen));
        }
    }

    /// <summary>CanExecute für BestaetigenCommand: eine Solution muss gewählt sein.</summary>
    public bool KannBestaetigen => _selectedSolution != null;

    /// <summary>Bestätigt die Auswahl und schließt den Dialog.</summary>
    public ICommand BestaetigenCommand { get; }

    /// <summary>Bricht die Auswahl ab und schließt den Dialog.</summary>
    public ICommand AbbrechenCommand { get; }

    /// <inheritdoc cref="SolutionSelectionDialogViewModel"/>
    public SolutionSelectionDialogViewModel(IReadOnlyList<string> solutions)
    {
        Solutions = solutions;

        BestaetigenCommand = new RelayCommand(
            () => CloseRequested?.Invoke(this, true),
            () => KannBestaetigen);
        AbbrechenCommand = new RelayCommand(() => CloseRequested?.Invoke(this, false));
    }
}
