using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace Softwareschmiede.IntegrationTests.Services;

/// <summary>Starts only a genuinely write-restricted same-user test process; never falls back to normal execution.</summary>
[SupportedOSPlatform("windows")]
internal static class ScmRestrictedWindowsProcess
{
    internal static Process Start(string workspace, string command, string capabilitySid)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("The mandatory restricted process evidence requires Windows.");
        if (!OpenProcessToken(Process.GetCurrentProcess().Handle, 0x0002 | 0x0008 | 0x0001 | 0x0080, out var original))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        using (original)
        {
            // Logon/Everyone are required by Windows process initialization, but neither is
            // granted access to the private workspace/channel or the neighbouring app-data.
            var sids = new[] { new SecurityIdentifier(capabilitySid), GetLogonSid(original), new SecurityIdentifier(WellKnownSidType.WorldSid, null) };
            var buffers = new List<IntPtr>();
            try
            {
                var restrictions = sids.Select(sid =>
                {
                    var bytes = new byte[sid.BinaryLength];
                    sid.GetBinaryForm(bytes, 0);
                    var buffer = Marshal.AllocHGlobal(bytes.Length);
                    buffers.Add(buffer);
                    Marshal.Copy(bytes, 0, buffer, bytes.Length);
                    return new SidAndAttributes { Sid = buffer };
                }).ToArray();
                if (!CreateRestrictedToken(original, 0x1 | 0x8, 0, IntPtr.Zero, 0, IntPtr.Zero, (uint)restrictions.Length, restrictions, out var restricted))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                using (restricted)
                {
                    SetDefaultDacl(restricted, sids[1]);
                    var executable = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe");
                    var arguments = new StringBuilder('"' + executable + "\" -NoLogo -NoProfile -NonInteractive -EncodedCommand " + Convert.ToBase64String(Encoding.Unicode.GetBytes(command)));
                    var startup = new StartupInfo { Size = Marshal.SizeOf<StartupInfo>() };
                    var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                    var environment = Marshal.StringToHGlobalUni($"PATH={windows}\\System32\0SystemRoot={windows}\0TEMP={workspace}\0TMP={workspace}\0WINDIR={windows}\0\0");
                    try
                    {
                        if (!CreateProcessAsUser(restricted, executable, arguments, IntPtr.Zero, IntPtr.Zero, false, 0x08000400,
                                environment, workspace, ref startup, out var information))
                            throw new Win32Exception(Marshal.GetLastWin32Error(), "Restricted test process could not be started; no unrestricted fallback is allowed.");
                        try
                        {
                            var process = Process.GetProcessById(information.ProcessId);
                            _ = process.Handle; // Retain an owning handle even when the child exits immediately.
                            return process;
                        }
                        finally
                        {
                            CloseHandle(information.Thread);
                            CloseHandle(information.Process);
                        }
                    }
                    finally { Marshal.FreeHGlobal(environment); }
                }
            }
            finally { foreach (var buffer in buffers) Marshal.FreeHGlobal(buffer); }
        }
    }

    private static void SetDefaultDacl(SafeAccessTokenHandle token, SecurityIdentifier logonSid)
    {
        // The child must be able to use its own kernel objects. Scope that access to the
        // creating logon session; do not grant broad filesystem access to the user SID.
        var acl = new RawAcl(2, 2);
        acl.InsertAce(0, new CommonAce(AceFlags.None, AceQualifier.AccessAllowed, 0x10000000, logonSid, false, null));
        acl.InsertAce(1, new CommonAce(AceFlags.None, AceQualifier.AccessAllowed, 0x00020000,
            new SecurityIdentifier("S-1-3-4"), false, null));
        var bytes = new byte[acl.BinaryLength];
        acl.GetBinaryForm(bytes, 0);
        var aclBuffer = Marshal.AllocHGlobal(bytes.Length);
        var info = Marshal.AllocHGlobal(IntPtr.Size);
        try
        {
            Marshal.Copy(bytes, 0, aclBuffer, bytes.Length);
            Marshal.WriteIntPtr(info, aclBuffer);
            if (!SetTokenInformation(token, 6, info, IntPtr.Size)) // TokenDefaultDacl
                throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        finally { Marshal.FreeHGlobal(info); Marshal.FreeHGlobal(aclBuffer); }
    }

    private static SecurityIdentifier GetLogonSid(SafeAccessTokenHandle token)
    {
        GetTokenInformation(token, 2, IntPtr.Zero, 0, out var size); // TokenGroups
        if (size <= 0) throw new Win32Exception(Marshal.GetLastWin32Error());
        var buffer = Marshal.AllocHGlobal(size);
        try
        {
            if (!GetTokenInformation(token, 2, buffer, size, out _))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            var count = Marshal.ReadInt32(buffer);
            var offset = Marshal.OffsetOf<TokenGroups>(nameof(TokenGroups.First)).ToInt32();
            for (var index = 0; index < count; index++)
            {
                var group = Marshal.PtrToStructure<SidAndAttributes>(IntPtr.Add(buffer, offset + index * Marshal.SizeOf<SidAndAttributes>()));
                if ((group.Attributes & 0xC0000000) == 0xC0000000) return new SecurityIdentifier(group.Sid);
            }
            throw new InvalidOperationException("The restricted test token requires an actual Windows logon identity.");
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SidAndAttributes { public IntPtr Sid; public uint Attributes; }
    [StructLayout(LayoutKind.Sequential)]
    private struct TokenGroups { public uint Count; public SidAndAttributes First; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct StartupInfo
    {
        public int Size;
        public string? Reserved, Desktop, Title;
        public uint X, Y, XSize, YSize, XCountChars, YCountChars, FillAttribute, Flags;
        public ushort ShowWindow, Reserved2Size;
        public IntPtr Reserved2, Input, Output, Error;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInformation { public IntPtr Process, Thread; public int ProcessId, ThreadId; }
    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenProcessToken(IntPtr process, uint access, out SafeAccessTokenHandle token);
    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetTokenInformation(SafeAccessTokenHandle token, int informationClass, IntPtr buffer, int length, out int needed);
    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetTokenInformation(SafeAccessTokenHandle token, int informationClass, IntPtr buffer, int length);
    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateRestrictedToken(SafeAccessTokenHandle original, uint flags, uint disableCount, IntPtr disable,
        uint deletePrivilegeCount, IntPtr deletePrivileges, uint restrictCount, [In] SidAndAttributes[] restrictions, out SafeAccessTokenHandle token);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateProcessAsUser(SafeAccessTokenHandle token, string application, StringBuilder arguments,
        IntPtr processAttributes, IntPtr threadAttributes, bool inheritHandles, uint flags, IntPtr environment, string workingDirectory,
        ref StartupInfo startup, out ProcessInformation information);
    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);
}
