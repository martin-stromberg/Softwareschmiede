using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace Softwareschmiede.Infrastructure.Services;

/// <summary>
/// Win32 SetParent API-Wrapper für die Einbettung von CLI-Fenstern in WPF-Controls.
/// Fallback: AlwaysOnTop-Fenster neben der App, falls SetParent scheitert.
/// </summary>
public sealed class ProcessWindowEmbedder
{
    private readonly ILogger<ProcessWindowEmbedder> _logger;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int x, int y,
        int cx, int cy,
        uint uFlags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    private static readonly IntPtr HwndTopmost = new(-1);
    private const uint SwpNosize = 0x0001;
    private const uint SwpNomove = 0x0002;
    private const uint SwpShowwindow = 0x0040;
    private const int GwlStyle = -16;
    private const int WsChild = 0x40000000;
    private const int WsPopup = unchecked((int)0x80000000);

    /// <summary>Erstellt eine neue Instanz des <see cref="ProcessWindowEmbedder"/>.</summary>
    public ProcessWindowEmbedder(ILogger<ProcessWindowEmbedder> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Wartet auf das Fenster-Handle des Prozesses und bettet es in das angegebene Parent-Handle ein.
    /// Gibt <c>true</c> zurück wenn Einbettung erfolgreich, <c>false</c> wenn Fallback (AlwaysOnTop) verwendet.
    /// </summary>
    public async Task<bool> EmbedProcessWindowAsync(
        Process process,
        IntPtr parentWindowHandle,
        CancellationToken ct = default)
    {
        var childHandle = await WaitForMainWindowHandleAsync(process, ct);
        if (childHandle == IntPtr.Zero)
        {
            _logger.LogWarning(
                "Kein Fenster-Handle für Prozess {Pid} ermittelt – SetParent nicht möglich.",
                process.Id);
            return false;
        }

        try
        {
            var style = GetWindowLong(childHandle, GwlStyle);
            style = (style & ~WsPopup) | WsChild;
            SetWindowLong(childHandle, GwlStyle, style);

            var result = SetParent(childHandle, parentWindowHandle);
            if (result == IntPtr.Zero)
            {
                var error = Marshal.GetLastWin32Error();
                _logger.LogWarning(
                    "SetParent für Prozess {Pid} fehlgeschlagen (Win32-Fehler: {Error}). Aktiviere AlwaysOnTop-Fallback.",
                    process.Id,
                    error);
                ActivateAlwaysOnTopFallback(childHandle);
                return false;
            }

            _logger.LogInformation("CLI-Fenster für Prozess {Pid} erfolgreich eingebettet.", process.Id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler bei Fenster-Einbettung für Prozess {Pid}. Aktiviere AlwaysOnTop-Fallback.", process.Id);
            ActivateAlwaysOnTopFallback(childHandle);
            return false;
        }
    }

    private void ActivateAlwaysOnTopFallback(IntPtr hWnd)
    {
        try
        {
            SetWindowPos(hWnd, HwndTopmost, 0, 0, 0, 0, SwpNomove | SwpNosize | SwpShowwindow);
            _logger.LogInformation("AlwaysOnTop-Fallback für Fenster {Handle} aktiviert.", hWnd);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AlwaysOnTop-Fallback für Fenster {Handle} fehlgeschlagen.", hWnd);
        }
    }

    private static async Task<IntPtr> WaitForMainWindowHandleAsync(Process process, CancellationToken ct)
    {
        const int maxWaitMs = 10_000;
        const int pollIntervalMs = 200;
        var elapsed = 0;

        while (elapsed < maxWaitMs && !ct.IsCancellationRequested)
        {
            try
            {
                process.Refresh();
                if (process.MainWindowHandle != IntPtr.Zero)
                {
                    return process.MainWindowHandle;
                }
            }
            catch (Exception)
            {
                return IntPtr.Zero;
            }

            await Task.Delay(pollIntervalMs, ct);
            elapsed += pollIntervalMs;
        }

        return IntPtr.Zero;
    }
}
