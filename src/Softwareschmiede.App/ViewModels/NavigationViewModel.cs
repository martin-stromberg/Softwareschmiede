using System.Windows.Input;

namespace Softwareschmiede.App.ViewModels;

/// <summary>Verwaltung des Navigationsmenü-State (einklappbar/expandiert).</summary>
public sealed class NavigationViewModel : ViewModelBase
{
    private bool _isExpanded = true;

    /// <summary>Gibt an, ob das Navigationsmenü aufgeklappt ist.</summary>
    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    /// <summary>Gibt die Breite des Navigationsbereichs zurück (expandiert oder eingeklappt).</summary>
    public double NavigationWidth => _isExpanded ? 220 : 48;

    /// <summary>Klappt das Menü ein oder aus.</summary>
    public ICommand ToggleCommand { get; }

    /// <inheritdoc cref="NavigationViewModel"/>
    public NavigationViewModel()
    {
        ToggleCommand = new RelayCommand(Toggle);
    }

    private void Toggle()
    {
        IsExpanded = !IsExpanded;
        OnPropertyChanged(nameof(NavigationWidth));
    }
}
