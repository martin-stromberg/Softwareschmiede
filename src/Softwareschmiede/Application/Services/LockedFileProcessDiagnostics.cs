using System.Runtime.InteropServices;
using System.Text;

namespace Softwareschmiede.Application.Services;

/// <summary>Best-effort-Diagnose gesperrter Dateien/Verzeichnisse über die Windows Restart
/// Manager-API (<c>rstrtmgr.dll</c>): liefert eine lesbare Liste der Prozesse, die die
/// angegebenen Pfade offen halten — oder <see langword="null"/>, wenn keine Diagnose
/// möglich ist (Nicht-Windows, API-Fehler, keine Sperre). Wirft niemals.</summary>
internal static class LockedFileProcessDiagnostics
{
    private const int MaximumResources = 256;

    /// <summary>Nennt die Prozesse, die <paramref name="path"/> oder Dateien darunter
    /// offen halten (z. B. <c>"node.exe (PID 123), dotnet.exe (PID 45)"</c>).</summary>
    public static string? DescribeLockingProcesses(string path)
    {
        try
        {
            if (!OperatingSystem.IsWindows() || !Path.Exists(path)) return null;
            var resources = CollectResources(path);
            if (resources.Length == 0) return null;
            var holders = QueryLockingProcesses(resources);
            return holders.Count == 0 ? null : string.Join(", ", holders);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Sammelt (gedeckelt) die zu diagnostizierenden Dateien. Der Verzeichnispfad
    /// selbst wird bewusst nicht registriert: Der Restart Manager akzeptiert nur
    /// Datei-Ressourcen und schlägt sonst komplett fehl (ERROR_ACCESS_DENIED).</summary>
    private static string[] CollectResources(string path)
    {
        var resources = new List<string>();
        if (File.Exists(path)) resources.Add(path);
        if (Directory.Exists(path))
            try
            {
                foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                {
                    resources.Add(file);
                    if (resources.Count >= MaximumResources) break;
                }
            }
            catch { }
        return resources.ToArray();
    }

    private static List<string> QueryLockingProcesses(string[] resources)
    {
        var key = new StringBuilder(Guid.NewGuid().ToString("N"), 32);
        if (RmStartSession(out var session, 0, key) != 0) return [];
        try
        {
            if (RmRegisterResources(session, (uint)resources.Length, resources, 0, IntPtr.Zero, 0, null) != 0)
                return [];
            var count = 0u;
            var needed = 0u;
            var reason = 0u;
            // Erster Aufruf ohne Puffer liefert die benötigte Eintragszahl.
            var result = RmGetList(session, out needed, ref count, null, ref reason);
            if (needed == 0) return [];
            var info = new RmProcessInfo[needed];
            count = needed; // nProcInfo ist in/out: Puffergröße des zweiten Aufrufs.
            result = RmGetList(session, out needed, ref count, info, ref reason);
            if (result != 0) return [];
            var holders = new List<string>();
            for (var i = 0u; i < count && i < info.Length; i++)
            {
                var name = info[i].ApplicationName;
                if (string.IsNullOrWhiteSpace(name))
                    try { name = System.Diagnostics.Process.GetProcessById((int)info[i].Process.ProcessId).ProcessName; }
                    catch { name = null; }
                holders.Add($"{name ?? "unbekannt"} (PID {info[i].Process.ProcessId})");
            }
            return holders;
        }
        finally { RmEndSession(session); }
    }

    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
    private static extern int RmStartSession(out uint sessionHandle, int sessionFlags, StringBuilder sessionKey);

    [DllImport("rstrtmgr.dll")]
    private static extern int RmEndSession(uint sessionHandle);

    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
    private static extern int RmRegisterResources(uint sessionHandle, uint nFiles, string[] fileNames,
        uint nApplications, IntPtr applications, uint nServices, string[]? serviceNames);

    [DllImport("rstrtmgr.dll")]
    private static extern int RmGetList(uint sessionHandle, out uint procInfoNeeded,
        ref uint procInfo, [In, Out] RmProcessInfo[]? affected, ref uint rebootReason);

    [StructLayout(LayoutKind.Sequential)]
    private struct RmUniqueProcess
    {
        public int ProcessId;
        public System.Runtime.InteropServices.ComTypes.FILETIME ProcessStartTime;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct RmProcessInfo
    {
        public RmUniqueProcess Process;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string ApplicationName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string ServiceShortName;
        public int ApplicationType;
        public uint AppStatus;
        public uint TSSessionId;
        [MarshalAs(UnmanagedType.Bool)] public bool Restartable;
    }
}
