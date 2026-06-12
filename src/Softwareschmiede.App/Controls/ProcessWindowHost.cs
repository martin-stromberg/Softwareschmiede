using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Extensions.Logging;

namespace Softwareschmiede.App.Controls;

/// <summary>
/// WPF-Host-Control, das ein externes Prozessfenster via Win32 <c>SetParent</c> einbettet.
/// Das eingebettete Fenster füllt den gesamten verfügbaren Platz aus.
/// </summary>
public sealed class ProcessWindowHost : HwndHost
{
    private const int WS_CHILD = 0x40000000;
    private const int WS_VISIBLE = 0x10000000;
    private const int GWL_STYLE = -16;

    private IntPtr _embeddedHandle = IntPtr.Zero;
    private IntPtr _hostHandle = IntPtr.Zero;

    private ILogger? _logger;

    /// <summary>Setzt den Logger für Win32-Fehlermeldungen.</summary>
    public void SetLogger(ILogger logger) => _logger = logger;

    /// <summary>Dependency Property für das einzubettende Fenster-Handle.</summary>
    public static readonly DependencyProperty EmbeddedHandleProperty =
        DependencyProperty.Register(
            nameof(EmbeddedHandle),
            typeof(IntPtr),
            typeof(ProcessWindowHost),
            new PropertyMetadata(IntPtr.Zero, OnEmbeddedHandleChanged));

    /// <summary>Das einzubettende Fenster-Handle.</summary>
    public IntPtr EmbeddedHandle
    {
        get => (IntPtr)GetValue(EmbeddedHandleProperty);
        set => SetValue(EmbeddedHandleProperty, value);
    }

    private static void OnEmbeddedHandleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ProcessWindowHost host)
            host.EmbedWindow((IntPtr)e.NewValue);
    }

    /// <inheritdoc/>
    protected override HandleRef BuildWindowCore(HandleRef hwndParent)
    {
        _hostHandle = NativeMethods.CreateWindowEx(
            0,
            "static",
            string.Empty,
            WS_CHILD | WS_VISIBLE,
            0, 0,
            (int)ActualWidth, (int)ActualHeight,
            hwndParent.Handle,
            IntPtr.Zero,
            IntPtr.Zero,
            IntPtr.Zero);

        if (_hostHandle == IntPtr.Zero)
            throw new InvalidOperationException(
                $"CreateWindowEx ist fehlgeschlagen (Win32-Fehler: {Marshal.GetLastWin32Error()}).");

        if (_embeddedHandle != IntPtr.Zero)
            EmbedWindow(_embeddedHandle);

        return new HandleRef(this, _hostHandle);
    }

    /// <inheritdoc/>
    protected override void DestroyWindowCore(HandleRef hwnd)
    {
        if (_embeddedHandle != IntPtr.Zero)
        {
            var result = NativeMethods.SetParent(_embeddedHandle, IntPtr.Zero);
            if (result == IntPtr.Zero)
            {
                _logger?.LogWarning(
                    "SetParent beim Trennen fehlgeschlagen (Win32-Fehler: {ErrorCode}).",
                    Marshal.GetLastWin32Error());
            }

            _embeddedHandle = IntPtr.Zero;
        }

        NativeMethods.DestroyWindow(hwnd.Handle);
        _hostHandle = IntPtr.Zero;
    }

    /// <inheritdoc/>
    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        ResizeEmbeddedWindow();
    }

    private void EmbedWindow(IntPtr handle)
    {
        _embeddedHandle = handle;

        if (_hostHandle == IntPtr.Zero || handle == IntPtr.Zero)
            return;

        var style = NativeMethods.GetWindowLong(handle, GWL_STYLE);
        if (style == 0)
        {
            _logger?.LogWarning(
                "GetWindowLong fehlgeschlagen (Win32-Fehler: {ErrorCode}).",
                Marshal.GetLastWin32Error());
        }

        style = (style | WS_CHILD) & ~0x00C00000; // Entfernt WS_CAPTION und WS_THICKFRAME
        var setLongResult = NativeMethods.SetWindowLong(handle, GWL_STYLE, style);
        if (setLongResult == 0)
        {
            var errorCode = Marshal.GetLastWin32Error();
            if (errorCode != 0)
            {
                _logger?.LogWarning(
                    "SetWindowLong fehlgeschlagen (Win32-Fehler: {ErrorCode}).",
                    errorCode);
            }
        }

        var parentResult = NativeMethods.SetParent(handle, _hostHandle);
        if (parentResult == IntPtr.Zero)
        {
            _logger?.LogWarning(
                "SetParent fehlgeschlagen (Win32-Fehler: {ErrorCode}). Fallback auf AlwaysOnTop.",
                Marshal.GetLastWin32Error());
            SetAlwaysOnTopFallback(handle);
            return;
        }

        ResizeEmbeddedWindow();
    }

    private static void SetAlwaysOnTopFallback(IntPtr handle)
    {
        var hwndTopmost = new IntPtr(-1); // HWND_TOPMOST
        NativeMethods.SetWindowPos(handle, hwndTopmost, 0, 0, 800, 600, 0x0002 | 0x0001); // SWP_NOMOVE | SWP_NOSIZE ignoriert
    }

    private void ResizeEmbeddedWindow()
    {
        if (_hostHandle == IntPtr.Zero)
            return;

        var width = Math.Max(1, (int)ActualWidth);
        var height = Math.Max(1, (int)ActualHeight);

        NativeMethods.SetWindowPos(
            _hostHandle,
            IntPtr.Zero,
            0, 0,
            width, height,
            0x0010 | 0x0002); // SWP_NOACTIVATE | SWP_NOMOVE

        if (_embeddedHandle == IntPtr.Zero)
            return;

        var result = NativeMethods.SetWindowPos(
            _embeddedHandle,
            IntPtr.Zero,
            0, 0,
            width, height,
            0x0010); // SWP_NOACTIVATE

        if (!result)
        {
            _logger?.LogWarning(
                "SetWindowPos fehlgeschlagen (Win32-Fehler: {ErrorCode}).",
                Marshal.GetLastWin32Error());
        }
    }

    private static class NativeMethods
    {
        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool DestroyWindow(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr CreateWindowEx(
            int dwExStyle,
            string lpClassName,
            string lpWindowName,
            int dwStyle,
            int x, int y,
            int nWidth, int nHeight,
            IntPtr hWndParent,
            IntPtr hMenu,
            IntPtr hInstance,
            IntPtr lpParam);
    }
}
