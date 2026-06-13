using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;

namespace Softwareschmiede.App.ViewModels;

/// <summary>ViewModel für den Repository-Zuweisungs-Dialog.</summary>
public sealed class RepositoryAssignViewModel : ViewModelBase
{
    private readonly ProjektService _projektService;
    private readonly ILogger<RepositoryAssignViewModel> _logger;
    private GitRepository? _selectedRepository;
    private bool _isLoading;

    /// <summary>Wird ausgelöst wenn der Dialog geschlossen werden soll. Parameter: true = bestätigt, false = abgebrochen.</summary>
    public event EventHandler<bool>? CloseRequested;

    /// <summary>Verfügbare Repositories zur Auswahl.</summary>
    public ObservableCollection<GitRepository> VerfuegbareRepositories { get; } = new();

    /// <summary>Ausgewähltes Repository.</summary>
    public GitRepository? SelectedRepository
    {
        get => _selectedRepository;
        set => SetProperty(ref _selectedRepository, value);
    }

    /// <summary>Gibt an, ob Daten geladen werden.</summary>
    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    /// <summary>Bestätigt die Auswahl und schließt den Dialog.</summary>
    public ICommand BestaetigenCommand { get; }

    /// <summary>Bricht die Auswahl ab und schließt den Dialog.</summary>
    public ICommand AbbrechenCommand { get; }

    /// <inheritdoc cref="RepositoryAssignViewModel"/>
    public RepositoryAssignViewModel(ProjektService projektService, ILogger<RepositoryAssignViewModel> logger)
    {
        _projektService = projektService;
        _logger = logger;

        BestaetigenCommand = new RelayCommand(
            () => CloseRequested?.Invoke(this, true),
            () => _selectedRepository != null);
        AbbrechenCommand = new RelayCommand(() => CloseRequested?.Invoke(this, false));
    }

    /// <summary>Lädt alle bekannten Repositories.</summary>
    public async Task LadenAsync(CancellationToken ct = default)
    {
        IsLoading = true;
        try
        {
            var repositories = await _projektService.GetAllRepositoriesAsync(ct);
            VerfuegbareRepositories.Clear();
            foreach (var repo in repositories)
                VerfuegbareRepositories.Add(repo);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Laden der verfügbaren Repositories.");
        }
        finally
        {
            IsLoading = false;
        }
    }
}
