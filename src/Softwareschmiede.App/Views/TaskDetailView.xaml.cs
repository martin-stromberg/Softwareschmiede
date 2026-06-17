using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Controls;
using Softwareschmiede.App.ViewModels;

namespace Softwareschmiede.App.Views;

/// <summary>Code-behind für TaskDetailView.</summary>
public sealed partial class TaskDetailView : UserControl
{
    private static readonly TimeSpan WindowHandlePollTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan WindowHandlePollInterval = TimeSpan.FromMilliseconds(200);

    private CancellationTokenSource? _pollCts;

    /// <inheritdoc cref="TaskDetailView"/>
    public TaskDetailView()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            if (DataContext is TaskDetailViewModel vm)
            {
                vm.CliProzessGestartet += OnCliProzessGestartet;

                // Gespeichertes HWND prüfen: Nach Navigation-Zurück ist das Fenster noch am Leben,
                // aber process.MainWindowHandle kann es ggf. nicht mehr finden (z.B. conhost-PID,
                // fehlender Titel, o.ä.). Das HWND bleibt durch SetParent unverändert → direkt nutzen.
                var storedHandle = vm.GetCliWindowHandle();
                if (storedHandle != IntPtr.Zero)
                {
                    vm.EmbeddedWindowHandle = storedHandle;
                    return;
                }

                // Fallback: CliProzessGestartet kann bereits gefeuert haben bevor Loaded ausgelöst
                // wurde (Auto-Restart, o.ä.). In diesem Fall via Polling einbetten.
                var runningProcess = vm.GetRunningProcess();
                if (runningProcess != null)
                    OnCliProzessGestartet(runningProcess);
            }
        };
        Unloaded += (_, _) =>
        {
            _pollCts?.Cancel();
            _pollCts?.Dispose();
            _pollCts = null;

            if (DataContext is TaskDetailViewModel vm)
            {
                vm.CliProzessGestartet -= OnCliProzessGestartet;
                vm.Dispose();
            }
        };
    }

    private void OnCliProzessGestartet(System.Diagnostics.Process process)
    {
        _pollCts?.Cancel();
        _pollCts?.Dispose();
        _pollCts = new CancellationTokenSource();
        _ = WaitForWindowHandleAsync(process, _pollCts.Token);
    }

    private async Task WaitForWindowHandleAsync(System.Diagnostics.Process process, CancellationToken ct)
    {
        var capturedViewModel = DataContext as TaskDetailViewModel;
        try
        {
            var deadline = DateTime.UtcNow.Add(WindowHandlePollTimeout);
            while (DateTime.UtcNow < deadline && !process.HasExited && !ct.IsCancellationRequested)
            {
                process.Refresh();
                if (process.MainWindowHandle != IntPtr.Zero)
                    break;
                await Task.Delay(WindowHandlePollInterval, ct);
            }

            if (ct.IsCancellationRequested)
                return;

            var handle = process.MainWindowHandle;
            if (handle != IntPtr.Zero && DataContext is TaskDetailViewModel vm && ReferenceEquals(vm, capturedViewModel))
            {
                // Handle persistent speichern damit Navigation-Zurück + Wiederoeffnen das
                // Fenster direkt einbetten kann ohne erneutes Polling über process.MainWindowHandle.
                capturedViewModel.SetCliWindowHandle(handle);
                vm.EmbeddedWindowHandle = handle;
            }
        }
        catch (OperationCanceledException)
        {
            // Ansicht wurde entladen – kein Einbetten mehr nötig
        }
        catch (Win32Exception ex)
        {
            Debug.WriteLine($"[TaskDetailView] Prozess ist bereits beendet oder nicht zugänglich – kein Einbetten möglich: {ex}");
        }
        catch (InvalidOperationException ex)
        {
            Debug.WriteLine($"[TaskDetailView] Prozess wurde disposed – kein Einbetten möglich: {ex}");
        }
    }
}
