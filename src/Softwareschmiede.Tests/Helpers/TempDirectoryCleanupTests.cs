using System.Diagnostics;
using FluentAssertions;
using Softwareschmiede.TestSupport;
using Softwareschmiede.Tests.Infrastructure.Testing;

namespace Softwareschmiede.Tests.Helpers;

/// <summary>Deterministische Datei-I/O-Tests des best-effort Temp-Verzeichnis-Cleanups.</summary>
public sealed class TempDirectoryCleanupTests
{
    /// <summary>Ein Verzeichnis mit Read-only-Datei wird mit Attribut-Normalisierung vollstaendig entfernt.</summary>
    [Fact]
    public void DeleteBestEffort_ExistingDirectory_DeletesIncludingReadOnlyFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), "tempdir-cleanup-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var readOnlyFile = Path.Combine(root, "readonly.txt");
        File.WriteAllText(readOnlyFile, "x");
        File.SetAttributes(readOnlyFile, FileAttributes.ReadOnly);

        TempDirectoryCleanup.DeleteBestEffort(root, normalizeAttributes: true);

        Directory.Exists(root).Should().BeFalse();
    }

    /// <summary>Eine exklusiv geoeffnete Datei laesst alle Loeschversuche scheitern — die Methode
    /// kehrt trotzdem ohne Exception zurueck, damit Cleanup nie einen Assert ueberdeckt.</summary>
    [Fact]
    public void DeleteBestEffort_LockedDirectory_DoesNotThrow()
    {
        var root = Path.Combine(Path.GetTempPath(), "tempdir-cleanup-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var lockedFile = Path.Combine(root, "locked.txt");
        File.WriteAllText(lockedFile, "x");
        var stream = new FileStream(lockedFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        try
        {
            var act = () => TempDirectoryCleanup.DeleteBestEffort(root);

            act.Should().NotThrow("ein Cleanup-Fehler darf nie den eigentlichen Assert ueberdecken");
        }
        finally
        {
            stream.Dispose();
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>Ein nicht vorhandener Pfad wird toleriert.</summary>
    [Fact]
    public void DeleteBestEffort_MissingDirectory_DoesNotThrow()
    {
        var root = Path.Combine(Path.GetTempPath(), "tempdir-cleanup-missing-" + Guid.NewGuid().ToString("N"));

        var act = () => TempDirectoryCleanup.DeleteBestEffort(root);

        act.Should().NotThrow();
    }

    /// <summary>Das Kill-Sicherheitsnetz beendet registrierte Prozesse vor dem Loeschen;
    /// ein bereits disposed Prozessobjekt wird dabei toleriert (Muster: cmd/ping-Platzhalter
    /// wie im ScmDispatchFixture).</summary>
    [OsInterfaceFact]
    public void DeleteBestEffort_ProcessesToKill_KillsProcessAndDeletesDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), "tempdir-cleanup-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "x.txt"), "x");
        var process = Process.Start(new ProcessStartInfo("cmd.exe", "/c ping -n 300 127.0.0.1 > nul")
        {
            UseShellExecute = false,
            CreateNoWindow = true
        })!;
        var disposedProcess = Process.Start(new ProcessStartInfo("cmd.exe", "/c exit 0")
        {
            UseShellExecute = false,
            CreateNoWindow = true
        })!;
        disposedProcess.Dispose();
        try
        {
            TempDirectoryCleanup.DeleteBestEffort(root, processesToKill: [process, disposedProcess]);

            process.HasExited.Should().BeTrue("registrierte Prozesse werden vor dem Loeschen beendet");
            Directory.Exists(root).Should().BeFalse();
        }
        finally
        {
            if (!process.HasExited) { process.Kill(entireProcessTree: true); }
            process.Dispose();
            if (Directory.Exists(root)) { Directory.Delete(root, recursive: true); }
        }
    }
}
