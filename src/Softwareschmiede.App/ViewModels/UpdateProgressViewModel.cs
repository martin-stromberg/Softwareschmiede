using Softwareschmiede.Application.Services.Updates;
using System.Windows.Input;

namespace Softwareschmiede.App.ViewModels;

/// <summary>ViewModel für den Fortschrittsdialog der Update-Vorbereitung.</summary>
public sealed class UpdateProgressViewModel : ViewModelBase
{
    private string _phaseText = "Vorbereitung";
    private string _message = "Update wird vorbereitet.";
    private double _percent;
    private bool _isIndeterminate = true;
    private bool _hasError;
    private bool _canClose;
    private bool _canCancel = true;
    private readonly Action? _cancelAction;

    /// <inheritdoc cref="UpdateProgressViewModel"/>
    public UpdateProgressViewModel(Action? cancelAction = null)
    {
        _cancelAction = cancelAction;
        CancelCommand = new RelayCommand(RequestCancel, () => CanCancel);
    }

    /// <summary>Aktuelle Phase als Text.</summary>
    public string PhaseText
    {
        get => _phaseText;
        set => SetProperty(ref _phaseText, value);
    }

    /// <summary>Aktuelle Fortschrittsmeldung.</summary>
    public string Message
    {
        get => _message;
        set => SetProperty(ref _message, value);
    }

    /// <summary>Fortschritt in Prozent.</summary>
    public double Percent
    {
        get => _percent;
        set => SetProperty(ref _percent, value);
    }

    /// <summary>Gibt an, ob der Fortschritt ohne konkreten Prozentwert angezeigt wird.</summary>
    public bool IsIndeterminate
    {
        get => _isIndeterminate;
        set => SetProperty(ref _isIndeterminate, value);
    }

    /// <summary>Gibt an, ob ein Fehler angezeigt wird.</summary>
    public bool HasError
    {
        get => _hasError;
        set => SetProperty(ref _hasError, value);
    }

    /// <summary>Gibt an, ob der Dialog geschlossen werden darf.</summary>
    public bool CanClose
    {
        get => _canClose;
        set => SetProperty(ref _canClose, value);
    }

    /// <summary>Gibt an, ob die Vorbereitung noch abgebrochen werden kann.</summary>
    public bool CanCancel
    {
        get => _canCancel;
        set => SetProperty(ref _canCancel, value, RelayCommand.Refresh);
    }

    /// <summary>Bricht die laufende Vorbereitung ab.</summary>
    public ICommand CancelCommand { get; }

    /// <summary>Übernimmt eine Fortschrittsmeldung aus dem Update-Service.</summary>
    public void Apply(UpdatePreparationProgress progress)
    {
        PhaseText = progress.Phase switch
        {
            UpdatePreparationPhase.Download => "Download",
            UpdatePreparationPhase.Entpacken => "Entpacken",
            UpdatePreparationPhase.UpdateVorbereiten => "Update-Vorbereitung",
            _ => "Vorbereitung"
        };
        Message = progress.Message;
        IsIndeterminate = progress.Percent is null;
        Percent = progress.Percent ?? 0;
    }

    /// <summary>Zeigt einen Fehler an und erlaubt das Schließen des Dialogs.</summary>
    public void SetError(string message)
    {
        HasError = true;
        CanClose = true;
        CanCancel = false;
        Message = message;
        IsIndeterminate = false;
    }

    /// <summary>Bereitet den Dialog auf den Start des externen Update-Skripts und das anschließende Beenden der App vor.</summary>
    public void MarkUpdaterStarting()
    {
        CanCancel = false;
        CanClose = true;
        Message = "Update wird gestartet. Die Anwendung wird beendet.";
        IsIndeterminate = true;
    }

    private void RequestCancel()
    {
        if (!CanCancel)
            return;

        CanCancel = false;
        Message = "Update-Vorbereitung wird abgebrochen.";
        _cancelAction?.Invoke();
    }
}
