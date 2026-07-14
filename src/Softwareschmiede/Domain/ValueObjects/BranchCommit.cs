using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Softwareschmiede.Domain.ValueObjects;

/// <summary>Repräsentiert einen Branch-Commit im Repository-Explorer.</summary>
public sealed class BranchCommit : INotifyPropertyChanged
{
    private bool _isLoadingFiles;
    private string? _errorMessage;

    /// <inheritdoc/>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Vollständiger SHA-Hash des Commits.</summary>
    public string Sha { get; init; } = string.Empty;

    /// <summary>Abgekürzter SHA-Hash (7 Zeichen).</summary>
    public string ShortSha { get; init; } = string.Empty;

    /// <summary>Commit-Nachricht (erste Zeile).</summary>
    public string Subject { get; init; } = string.Empty;

    /// <summary>Gibt an, ob der Commit-Knoten im Tree-View aufgeklappt ist.</summary>
    public bool IsExpanded { get; set; }

    /// <summary>Gibt an, ob die Kind-Elemente (geänderte Dateien) bereits geladen wurden.</summary>
    public bool ChildrenLoaded { get; set; }

    /// <summary>Gibt an, ob die Dateien gerade geladen werden.</summary>
    public bool IsLoadingFiles
    {
        get => _isLoadingFiles;
        set
        {
            if (_isLoadingFiles == value)
                return;

            _isLoadingFiles = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Fehlermeldung beim Laden der Dateien, falls aufgetreten.</summary>
    public string? ErrorMessage
    {
        get => _errorMessage;
        set
        {
            if (_errorMessage == value)
                return;

            _errorMessage = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Liste der im Commit geänderten Dateien.</summary>
    public ObservableCollection<WorkspaceFileNode> Files { get; set; } = [];

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
