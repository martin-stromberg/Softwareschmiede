using Softwareschmiede.Application.Services.Updates;

namespace Softwareschmiede.App.Services;

/// <summary>Beendet die WPF-Anwendung geordnet.</summary>
public sealed class WpfApplicationShutdownService : IApplicationShutdownService
{
    /// <inheritdoc/>
    public void Shutdown()
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
            System.Windows.Application.Current.Shutdown());
    }
}
