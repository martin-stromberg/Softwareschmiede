using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using static Softwareschmiede.Infrastructure.Terminal.PseudoConsoleNativeMethods;

namespace Softwareschmiede.Infrastructure.Terminal;

/// <summary>Startet einen Win32-Prozess mit einer zugewiesenen Pseudo Console.</summary>
internal static class PseudoConsoleProcessStarter
{
    /// <summary>Startet einen Prozess mit der angegebenen Pseudo Console.</summary>
    /// <param name="psi">Startinformationen für den Prozess.</param>
    /// <param name="pc">Die Pseudo Console, der der Prozess zugewiesen werden soll.</param>
    /// <returns>Ein <see cref="ProcessStartResult"/> mit Win32-Prozess-Handle und Prozess-ID.</returns>
    internal static ProcessStartResult Start(ProcessStartInfo psi, PseudoConsole pc)
    {
        var commandLine = BuildCommandLine(psi.FileName, psi.Arguments);
        var environmentBlock = BuildEnvironmentBlock(psi);

        var attributeList = IntPtr.Zero;
        var environmentPtr = IntPtr.Zero;

        try
        {
            var size = IntPtr.Zero;
            InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref size);

            attributeList = Marshal.AllocHGlobal(size);

            if (!InitializeProcThreadAttributeList(attributeList, 1, 0, ref size))
                throw new InvalidOperationException($"InitializeProcThreadAttributeList fehlgeschlagen: {Marshal.GetLastWin32Error()}");

            var hpcon = pc.Handle;
            var hpconSize = new IntPtr(IntPtr.Size);
            var hpconPtr = Marshal.AllocHGlobal(IntPtr.Size);
            try
            {
                Marshal.WriteIntPtr(hpconPtr, hpcon);

                if (!UpdateProcThreadAttribute(
                    attributeList,
                    0,
                    new IntPtr((long)PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE),
                    hpconPtr,
                    hpconSize,
                    IntPtr.Zero,
                    IntPtr.Zero))
                {
                    throw new InvalidOperationException($"UpdateProcThreadAttribute fehlgeschlagen: {Marshal.GetLastWin32Error()}");
                }

                var startupInfoEx = new STARTUPINFOEX
                {
                    StartupInfo = new STARTUPINFO
                    {
                        cb = Marshal.SizeOf<STARTUPINFOEX>(),
                    },
                    lpAttributeList = attributeList,
                };

                if (environmentBlock != null)
                    environmentPtr = BuildEnvironmentPtr(environmentBlock);

                var creationFlags = EXTENDED_STARTUPINFO_PRESENT | CREATE_UNICODE_ENVIRONMENT;

                if (!CreateProcess(
                    null,
                    commandLine,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    false,
                    creationFlags,
                    environmentPtr,
                    string.IsNullOrEmpty(psi.WorkingDirectory) ? null : psi.WorkingDirectory,
                    ref startupInfoEx,
                    out var pi))
                {
                    throw new InvalidOperationException($"CreateProcess fehlgeschlagen: {Marshal.GetLastWin32Error()}");
                }

                CloseHandle(pi.hThread);
                return new ProcessStartResult(pi.hProcess, pi.dwProcessId);
            }
            finally
            {
                Marshal.FreeHGlobal(hpconPtr);
            }
        }
        finally
        {
            if (attributeList != IntPtr.Zero)
            {
                DeleteProcThreadAttributeList(attributeList);
                Marshal.FreeHGlobal(attributeList);
            }

            if (environmentPtr != IntPtr.Zero)
                Marshal.FreeHGlobal(environmentPtr);
        }
    }

    private static string BuildCommandLine(string fileName, string arguments)
    {
        if (string.IsNullOrEmpty(arguments))
            return fileName.Contains(' ') ? $"\"{fileName}\"" : fileName;

        var sb = new StringBuilder();
        if (fileName.Contains(' '))
            sb.Append('"').Append(fileName).Append('"');
        else
            sb.Append(fileName);

        sb.Append(' ').Append(arguments);
        return sb.ToString();
    }

    private static Dictionary<string, string>? BuildEnvironmentBlock(ProcessStartInfo psi)
    {
        if (psi.EnvironmentVariables.Count == 0)
            return null;

        var env = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (System.Collections.DictionaryEntry entry in System.Environment.GetEnvironmentVariables())
        {
            if (entry.Key is string key && entry.Value is string val)
                env[key] = val;
        }

        foreach (string key in psi.EnvironmentVariables.Keys)
        {
            var val = psi.EnvironmentVariables[key];
            if (val != null)
                env[key] = val;
        }

        return env;
    }

    private static IntPtr BuildEnvironmentPtr(Dictionary<string, string> env)
    {
        var sb = new StringBuilder();
        foreach (var kv in env)
        {
            sb.Append(kv.Key).Append('=').Append(kv.Value).Append('\0');
        }
        sb.Append('\0');

        var bytes = Encoding.Unicode.GetBytes(sb.ToString());
        var ptr = Marshal.AllocHGlobal(bytes.Length);
        Marshal.Copy(bytes, 0, ptr, bytes.Length);
        return ptr;
    }
}

/// <summary>Ergebnis eines Win32-Prozessstarts via <see cref="PseudoConsoleProcessStarter"/>.</summary>
internal readonly struct ProcessStartResult
{
    /// <summary>Win32-Prozess-Handle.</summary>
    internal IntPtr ProcessHandle { get; }

    /// <summary>Prozess-ID.</summary>
    internal int Pid { get; }

    /// <summary>Erstellt ein neues <see cref="ProcessStartResult"/>.</summary>
    /// <param name="processHandle">Win32-Prozess-Handle.</param>
    /// <param name="pid">Prozess-ID.</param>
    internal ProcessStartResult(IntPtr processHandle, int pid)
    {
        ProcessHandle = processHandle;
        Pid = pid;
    }
}
