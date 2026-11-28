using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using msTools.Updater;

namespace Softwareschmiede.StatusServer.Tests;

/// <summary>Tests für <see cref="StatusWebApplicationFactory"/> — das eigene
/// Temp-Verzeichnis (Datenbank-Default und Update-Workspace) muss beim Dispose
/// auch dann aufgeräumt werden, wenn die Datenbank per
/// <see cref="StatusWebApplicationFactory.DatabasePathOverride"/> außerhalb liegt.</summary>
public sealed class StatusWebApplicationFactoryTests
{
    /// <summary>Bei gesetztem <see cref="StatusWebApplicationFactory.DatabasePathOverride"/>
    /// liegt die Datenbank außerhalb des Factory-Verzeichnisses (Aufräumen obliegt
    /// dem Test) — der Update-Workspace (<c>Update:WorkingDirectory</c>) liegt
    /// aber weiterhin darin und muss beim Dispose entfernt werden.</summary>
    [Fact]
    public async Task DisposeAsync_WithDatabasePathOverride_RemovesOwnedDirectory()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"statusserver-factory-{Guid.NewGuid():N}");
        var dbPath = Path.Combine(dir, "statusserver.db");
        string ownedDirectory;
        try
        {
            var factory = new StatusWebApplicationFactory { DatabasePathOverride = dbPath };
            try
            {
                // Host starten, damit die Update-Konfiguration gebunden ist.
                _ = factory.Services;
                var downloadPath = factory.Services.GetRequiredService<AutoUpdateOptions>().DownloadPath;
                downloadPath.Should().NotBeNullOrEmpty();
                ownedDirectory = Path.GetDirectoryName(downloadPath)!;

                // Workspace simuliert belegen (Lock-/Statusdateien des Updaters).
                Directory.CreateDirectory(downloadPath);
                await File.WriteAllTextAsync(
                    Path.Combine(downloadPath, "status.json"), "{}",
                    TestContext.Current.CancellationToken);
            }
            finally
            {
                await factory.DisposeAsync();
            }

            Directory.Exists(ownedDirectory).Should().BeFalse(
                "das eigene Temp-Verzeichnis der Factory wird auch mit DatabasePathOverride aufgeräumt");
            File.Exists(dbPath).Should().BeTrue(
                "die Override-Datenbank gehört dem Test und bleibt erhalten");
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            try { Directory.Delete(dir, recursive: true); } catch (IOException) { }
        }
    }
}
