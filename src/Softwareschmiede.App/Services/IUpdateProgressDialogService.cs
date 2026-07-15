using Softwareschmiede.App.ViewModels;

namespace Softwareschmiede.App.Services;

/// <summary>Öffnet und schließt den Fortschrittsdialog für Update-Vorbereitungen.</summary>
public interface IUpdateProgressDialogService
{
    /// <summary>Zeigt den Fortschrittsdialog nicht blockierend an.</summary>
    void Show(UpdateProgressViewModel viewModel);

    /// <summary>Schließt einen zuvor geöffneten Fortschrittsdialog.</summary>
    void Close(UpdateProgressViewModel viewModel);
}
