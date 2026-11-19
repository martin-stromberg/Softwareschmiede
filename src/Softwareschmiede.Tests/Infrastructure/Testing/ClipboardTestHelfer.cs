using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Softwareschmiede.Tests.Infrastructure.Testing;

/// <summary>
/// Robuste Zwischenablage-Zugriffe für Tests mit großzügigem Retry-Budget und
/// Halter-Diagnose. <see cref="ClipboardTestLock"/> serialisiert nur Zugriffe innerhalb
/// des Testhosts — externe Halter (Clipboard-Manager, <c>rdpclip.exe</c> bei RDP-Sessions,
/// Windows-Zwischenablageverlauf, Sync-Tools oder die von E2E-Tests gestartete App)
/// können <c>OpenClipboard</c> weiterhin über längere Zeitfenster belegen. Fünf Versuche
/// à 50 ms reichen dafür nicht; bei Erschöpfung benennt die Fehlermeldung über
/// <c>GetOpenClipboardWindow</c>/<c>GetClipboardOwner</c> den haltenden Prozess.
/// Alle Methoden müssen auf einem STA-Thread laufen (WPF-Clipboard ist STA-gebunden).
/// </summary>
public static class ClipboardTestHelfer
{
    /// <summary>Setzt Text in die Zwischenablage und bestätigt per Read-back die stabile
    /// Vorbedingung. Wiederholt bei <see cref="COMException"/> bis <paramref name="maxAttempts"/>.</summary>
    public static void SetTextMitRetry(string text, int maxAttempts = 25)
    {
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                System.Windows.Clipboard.SetText(text);
                if (System.Windows.Clipboard.ContainsText()
                    && System.Windows.Clipboard.GetText() == text)
                    return;
            }
            catch (COMException) when (attempt < maxAttempts)
            {
            }

            if (attempt < maxAttempts)
                Thread.Sleep(200);
        }

        throw new InvalidOperationException(
            $"Die Zwischenablage konnte nach {maxAttempts} Versuchen nicht stabil mit dem erwarteten Text " +
            $"vorbereitet werden. Aktueller Halter: {BeschreibeHalter()}.");
    }

    /// <summary>Liest den Zwischenablage-Text mit Retry bei <see cref="COMException"/>.</summary>
    public static string GetTextMitRetry(int maxAttempts = 15)
    {
        COMException? letzterFehler = null;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                return System.Windows.Clipboard.ContainsText()
                    ? System.Windows.Clipboard.GetText()
                    : string.Empty;
            }
            catch (COMException ex) when (attempt < maxAttempts)
            {
                letzterFehler = ex;
                Thread.Sleep(200);
            }
        }

        throw new InvalidOperationException(
            $"Die Zwischenablage konnte nach {maxAttempts} Versuchen nicht gelesen werden. " +
            $"Aktueller Halter: {BeschreibeHalter()}.", letzterFehler);
    }

    /// <summary>Leert die Zwischenablage mit Retry bei <see cref="COMException"/>.</summary>
    public static void LeerenMitRetry(int maxAttempts = 25)
    {
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                System.Windows.Clipboard.Clear();
                return;
            }
            catch (COMException) when (attempt < maxAttempts)
            {
                Thread.Sleep(200);
            }
        }

        throw new InvalidOperationException(
            $"Die Zwischenablage konnte nach {maxAttempts} Versuchen nicht geleert werden. " +
            $"Aktueller Halter: {BeschreibeHalter()}.");
    }

    /// <summary>Bestmögliche Diagnose, welcher Prozess die Zwischenablage gerade offen hält
    /// (<c>GetOpenClipboardWindow</c>) bzw. zuletzt befüllt hat (<c>GetClipboardOwner</c>).
    /// Erscheint in Fehlermeldungen, damit externe Halter erkennbar werden.</summary>
    /// <returns>Prozessname und PID des Halters bzw. Besitzers, oder „unbekannt".</returns>
    public static string BeschreibeHalter()
    {
        var offen = GetOpenClipboardWindow();
        var besitzer = GetClipboardOwner();
        var teile = new List<string>();
        if (offen != IntPtr.Zero)
            teile.Add($"offen gehalten von {ProzessName(offen)}");
        if (besitzer != IntPtr.Zero && besitzer != offen)
            teile.Add($"Inhalt zuletzt von {ProzessName(besitzer)} gesetzt");
        return teile.Count > 0
            ? string.Join("; ", teile)
            : "kein offener Halter/Besitzer ermittelbar (zwischen den Versuchen freigegeben)";
    }

    private static string ProzessName(IntPtr hWnd)
    {
        GetWindowThreadProcessId(hWnd, out var pid);
        if (pid == 0)
            return $"Fenster 0x{hWnd:X}";
        try
        {
            return $"{Process.GetProcessById((int)pid).ProcessName} (PID {pid})";
        }
        catch
        {
            return $"PID {pid}";
        }
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetOpenClipboardWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr GetClipboardOwner();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
}
