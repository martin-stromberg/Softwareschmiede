using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Softwareschmiede.App.ViewModels;

/// <summary>ViewModel für den Plugin-Auswahl-Dialog.</summary>
public sealed class PluginSelectionDialogViewModel : ViewModelBase
{
    private string? _selectedPluginPrefix;
    private bool _saveAsProjectDefault;

    /// <summary>Wird ausgelöst wenn der Dialog geschlossen werden soll. Parameter: true = bestätigt, false = abgebrochen.</summary>
    public event EventHandler<bool>? CloseRequested;

    /// <summary>Verfügbare Plugin-Prefixe zur Auswahl.</summary>
    public ObservableCollection<string> VerfuegbarePlugins { get; } = new();

    /// <summary>Vom Benutzer gewähltes Plugin-Prefix.</summary>
    public string? SelectedPluginPrefix
    {
        get => _selectedPluginPrefix;
        set
        {
            SetProperty(ref _selectedPluginPrefix, value);
            OnPropertyChanged(nameof(KannBestaetigen));
        }
    }

    /// <summary>Checkbox-Zustand: Plugin als Projekt-Standard speichern.</summary>
    public bool SaveAsProjectDefault
    {
        get => _saveAsProjectDefault;
        set => SetProperty(ref _saveAsProjectDefault, value);
    }

    /// <summary>CanExecute für BestaetigenCommand: ein Plugin muss gewählt sein.</summary>
    public bool KannBestaetigen => !string.IsNullOrEmpty(_selectedPluginPrefix);

    /// <summary>Bestätigt die Auswahl und schließt den Dialog.</summary>
    public ICommand BestaetigenCommand { get; }

    /// <summary>Bricht die Auswahl ab und schließt den Dialog.</summary>
    public ICommand AbbrechenCommand { get; }

    /// <inheritdoc cref="PluginSelectionDialogViewModel"/>
    public PluginSelectionDialogViewModel(IEnumerable<string> availablePlugins, string? currentSelection)
    {
        foreach (var plugin in availablePlugins)
            VerfuegbarePlugins.Add(plugin);

        SelectedPluginPrefix = !string.IsNullOrEmpty(currentSelection) && VerfuegbarePlugins.Contains(currentSelection)
            ? currentSelection
            : VerfuegbarePlugins.FirstOrDefault();

        BestaetigenCommand = new RelayCommand(
            () => CloseRequested?.Invoke(this, true),
            () => KannBestaetigen);
        AbbrechenCommand = new RelayCommand(() => CloseRequested?.Invoke(this, false));
    }
}
