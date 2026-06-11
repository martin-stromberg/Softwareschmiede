using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

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

        if (_embeddedHandle != IntPtr.Zero)
            EmbedWindow(_embeddedHandle);

        return new HandleRef(this, _hostHandle);
    }

    /// <inheritdoc/>
    protected override void DestroyWindowCore(HandleRef hwnd)
    {
        if (_embeddedHandle != IntPtr.Zero)
        {
            NativeMethods.SetParent(_embeddedHandle, IntPtr.Zero);
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
        style = (style | WS_CHILD) & ~0x00C00000; // Entfernt WS_CAPTION und WS_THICKFRAME
        NativeMethods.SetWindowLong(handle, GWL_STYLE, style);

        NativeMethods.SetParent(handle, _hostHandle);
        ResizeEmbeddedWindow();
    }

    private void ResizeEmbeddedWindow()
    {
        if (_embeddedHandle == IntPtr.Zero || _hostHandle == IntPtr.Zero)
            return;

        var width = (int)ActualWidth;
        var height = (int)ActualHeight;

        NativeMethods.SetWindowPos(
            _embeddedHandle,
            IntPtr.Zero,
            0, 0,
            width, height,
            0x0040 | 0x0010); // SWP_SHOWWINDOW | SWP_NOACTIVATE
    }

    private static class NativeMethods
    {
        [DllImport("user32.dll")]
        public static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

        [DllImport("user32.dll")]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        public static extern bool DestroyWindow(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
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
