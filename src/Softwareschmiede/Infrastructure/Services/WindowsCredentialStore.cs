using System.Runtime.InteropServices;
using System.Text;
using Softwareschmiede.Domain.Interfaces;

namespace Softwareschmiede.Infrastructure.Services;

/// <summary>Windows Credential Store Implementierung via Windows Credential Manager API.</summary>
public sealed class WindowsCredentialStore : ICredentialStore
{
    private const int CredTypeGeneric = 1;
    private const int CredPersistLocalMachine = 2;

    // P/Invoke Deklarationen für Windows Credential API
    [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredRead(string target, int type, int flags, out IntPtr credential);

    [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredWrite([In] ref NativeCredential credential, uint flags);

    [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredDelete(string target, int type, int flags);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern void CredFree(IntPtr buffer);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NativeCredential
    {
        public uint Flags;
        public int Type;
        public string TargetName;
        public string Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public string TargetAlias;
        public string UserName;
    }

    /// <inheritdoc/>
    public string? GetCredential(string target)
    {
        if (!CredRead(target, CredTypeGeneric, 0, out var credPtr)) return null;
        try
        {
            var cred = Marshal.PtrToStructure<NativeCredential>(credPtr);
            if (cred.CredentialBlobSize == 0) return string.Empty;
            return Marshal.PtrToStringUni(cred.CredentialBlob, (int)cred.CredentialBlobSize / 2);
        }
        finally
        {
            CredFree(credPtr);
        }
    }

    /// <inheritdoc/>
    public void SetCredential(string target, string value)
    {
        var blob = Encoding.Unicode.GetBytes(value);
        var blobHandle = GCHandle.Alloc(blob, GCHandleType.Pinned);
        try
        {
            var cred = new NativeCredential
            {
                Type = CredTypeGeneric,
                TargetName = target,
                CredentialBlobSize = (uint)blob.Length,
                CredentialBlob = blobHandle.AddrOfPinnedObject(),
                Persist = CredPersistLocalMachine,
                UserName = Environment.UserName
            };
            if (!CredWrite(ref cred, 0))
                throw new InvalidOperationException(
                    $"CredWrite für '{target}' fehlgeschlagen: {Marshal.GetLastWin32Error()}");
        }
        finally
        {
            blobHandle.Free();
        }
    }

    /// <inheritdoc/>
    public void DeleteCredential(string target)
    {
        CredDelete(target, CredTypeGeneric, 0);
    }
}
