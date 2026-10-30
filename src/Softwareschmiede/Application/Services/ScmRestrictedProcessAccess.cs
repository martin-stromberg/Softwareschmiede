using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;

namespace Softwareschmiede.Application.Services;

/// <summary>Reads restrictive capability identities from a same-user process, without changing its token.</summary>
public static class ScmRestrictedProcessAccess
{
    /// <summary>
    /// Carries only already-inheritable dedicated workspace write capabilities into the private
    /// channel. These are candidates, not access evidence: the actual CLI file tool must still
    /// complete a challenge in that channel. Parent process tokens are deliberately irrelevant.
    /// </summary>
    public static IReadOnlyList<string> CaptureWorkspaceCapabilities(string workingDirectory)
    {
        if (!OperatingSystem.IsWindows()) return [];
        return CaptureWindowsWorkspaceCapabilities(workingDirectory);
    }

    [SupportedOSPlatform("windows")]
    private static IReadOnlyList<string> CaptureWindowsWorkspaceCapabilities(string workingDirectory)
    {
        ScmEventExchangeProtocol.EnsureSafePath(workingDirectory);
        var rules = new DirectoryInfo(workingDirectory).GetAccessControl()
            .GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<FileSystemAccessRule>().ToArray();
        // Replacing a deny-bearing inherited DACL could otherwise weaken the effective sandbox.
        if (rules.Any(rule => rule.AccessControlType == AccessControlType.Deny))
            throw new UnauthorizedAccessException("Arbeitsbereich enthält nicht übertragbare Zugriffsverweigerungen.");
        const InheritanceFlags inheritance = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;
        return rules.Where(rule => rule.AccessControlType == AccessControlType.Allow &&
                (rule.InheritanceFlags & inheritance) == inheritance &&
                rule.PropagationFlags == PropagationFlags.None &&
                (rule.FileSystemRights & FileSystemRights.Modify) == FileSystemRights.Modify &&
                IsDedicatedRestriction((SecurityIdentifier)rule.IdentityReference))
            .Select(rule => rule.IdentityReference.Value).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
    }

    /// <summary>General users, groups, well-known principals and broad capabilities are never channel grants.</summary>
    public static void ValidateRestrictingSid(string sid)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Restricted process identities require Windows.");
        var identity = new SecurityIdentifier(sid);
        if (!IsDedicatedRestriction(identity))
            throw new UnauthorizedAccessException("Nur dedizierte beschränkte Prozessidentitäten sind für den Antwortkanal zulässig.");
    }

    [SupportedOSPlatform("windows")]
    private static bool IsDedicatedRestriction(SecurityIdentifier identity)
    {
        // Workspace sandbox capabilities use random account-shaped SIDs. AppContainer capability
        // SIDs (S-1-15-3-...) cannot be passed to CreateRestrictedToken. Never grant a real account,
        // a local-domain principal, or broad helper entries such as Everyone and the logon SID.
        if (!identity.Value.StartsWith("S-1-5-21-", StringComparison.Ordinal) || identity.Value.Split('-').Length != 8)
            return false;
        using var current = WindowsIdentity.GetCurrent();
        if (identity.AccountDomainSid == current.User?.AccountDomainSid)
            return false;
        try
        {
            identity.Translate(typeof(NTAccount));
            return false;
        }
        catch (IdentityNotMappedException) { return true; }
        catch (SystemException ex)
        {
            throw new UnauthorizedAccessException("Beschränkte Prozessidentität konnte nicht sicher geprüft werden.", ex);
        }
    }

    /// <summary>Returns actual restricting SIDs; inability to inspect a token must fail the access contract.</summary>
    public static IReadOnlyList<string> Capture(Process process)
    {
        if (!OperatingSystem.IsWindows()) return [];
        if (!OpenProcessToken(process.Handle, 0x0008, out var token))
            throw new UnauthorizedAccessException("CLI-Prozessrechte konnten nicht gelesen werden.", new Win32Exception());
        using (token)
        {
            using var identity = new WindowsIdentity(token.DangerousGetHandle());
            if (identity.User != WindowsIdentity.GetCurrent().User)
                throw new UnauthorizedAccessException("Antwortkanal unterstützt nur CLI-Prozesse desselben Benutzers.");
            GetTokenInformation(token, 11, IntPtr.Zero, 0, out var size); // TokenRestrictedSids
            if (size <= 0) throw new UnauthorizedAccessException("CLI-Einschränkungen konnten nicht bestimmt werden.");
            var buffer = Marshal.AllocHGlobal(size);
            try
            {
                if (!GetTokenInformation(token, 11, buffer, size, out _))
                    throw new UnauthorizedAccessException("CLI-Einschränkungen konnten nicht gelesen werden.", new Win32Exception());
                var count = Marshal.ReadInt32(buffer);
                if (count is < 0 or > 128) throw new UnauthorizedAccessException("Ungültige CLI-Einschränkungen.");
                var offset = Marshal.OffsetOf<TokenGroups>(nameof(TokenGroups.First)).ToInt32();
                var stride = Marshal.SizeOf<SidAndAttributes>();
                var values = new List<string>();
                for (var index = 0; index < count; index++)
                {
                    var entry = Marshal.PtrToStructure<SidAndAttributes>(IntPtr.Add(buffer, offset + index * stride));
                    var sid = new SecurityIdentifier(entry.Sid);
                    if (IsDedicatedRestriction(sid)) values.Add(sid.Value);
                }
                if (count > 0 && values.Count == 0)
                    throw new UnauthorizedAccessException("CLI-Einschränkungen enthalten keine dedizierte Schreibidentität für den Antwortkanal.");
                return values;
            }
            finally { Marshal.FreeHGlobal(buffer); }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SidAndAttributes { public IntPtr Sid; public uint Attributes; }
    [StructLayout(LayoutKind.Sequential)]
    private struct TokenGroups { public uint Count; public SidAndAttributes First; }
    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenProcessToken(IntPtr process, uint access, out SafeAccessTokenHandle token);
    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetTokenInformation(SafeAccessTokenHandle token, int informationClass, IntPtr buffer, int length, out int needed);
}
