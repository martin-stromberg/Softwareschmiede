using System.Runtime.InteropServices;
using System.Text;

namespace Softwareschmiede.Tests.E2E;

/// <summary>
/// Win32-basierte Aufzählung von Top-Level-Fenstern eines Prozesses. Dient als Ersatz für
/// UI-Automation-Desktop-Traversierungen (<c>Automation.GetDesktop().FindAllDescendants(...)</c>):
/// Jede Desktop-Enumeration prüft sämtliche Top-Level-Fenster aller Prozesse auf ihre
/// Control-View-Zugehörigkeit; ein einzelner nicht antwortender Fremd-Provider (beobachtet:
/// ein verwaistes Fenster hinter 'Program Manager' in der Z-Order) blockiert dabei jede
/// Enumeration für die volle UIA-Provider-Timeout-Zeit (~80s) — pro Aufruf. Die reine
/// Win32-Enumeration (EnumWindows + GetWindowThreadProcessId) fragt keine UIA-Provider ab,
/// ist prozessgenau und braucht Mikrosekunden statt Sekunden.
/// </summary>
internal static class ProcessWindowEnumeration
{
    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc cb, IntPtr lParam);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr hwnd, StringBuilder sb, int max);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);
    [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr hwnd);

    /// <summary>Alle Top-Level-Window-Handles des angegebenen Prozesses (inkl. unsichtbarer).</summary>
    internal static IReadOnlyList<IntPtr> TopLevelWindowsOfProcess(int processId)
    {
        var result = new List<IntPtr>();
        EnumWindows((hwnd, _) =>
        {
            GetWindowThreadProcessId(hwnd, out var pid);
            if (pid == processId)
                result.Add(hwnd);
            return true;
        }, IntPtr.Zero);
        return result;
    }

    /// <summary>Handle + Titel aller Top-Level-Fenster des Prozesses.</summary>
    internal static IReadOnlyList<(IntPtr Hwnd, string Title)> TopLevelWindowsAndTitles(int processId)
    {
        var result = new List<(IntPtr, string)>();
        EnumWindows((hwnd, _) =>
        {
            GetWindowThreadProcessId(hwnd, out var pid);
            if (pid == processId)
                result.Add((hwnd, WindowText(hwnd)));
            return true;
        }, IntPtr.Zero);
        return result;
    }

    /// <summary>Erstes Top-Level-Fenster (beliebiger Prozess) mit exakt diesem Titel.</summary>
    internal static IntPtr FindTopLevelWindowByTitle(string title)
    {
        var found = IntPtr.Zero;
        EnumWindows((hwnd, _) =>
        {
            if (WindowText(hwnd) != title)
                return true;
            found = hwnd;
            return false;
        }, IntPtr.Zero);
        return found;
    }

    internal static bool IsAliveWindow(IntPtr hwnd) => hwnd != IntPtr.Zero && IsWindow(hwnd);

    private static string WindowText(IntPtr hwnd)
    {
        var sb = new StringBuilder(512);
        GetWindowText(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }
}
