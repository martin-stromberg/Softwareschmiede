using System.Net;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using msTools.Updater;
using Softwareschmiede.StatusServer.Tests.TestSupport;
using Softwareschmiede.StatusServer.Update;

namespace Softwareschmiede.StatusServer.Tests.E2E;

/// <summary>E2E-Nachweise des Update-Flows über die echte
/// <c>msTools.Updater</c>-Pipeline: lokale Update-Quelle
/// (<c>AutoUpdateLocalFolderSource</c>) → Manifest → Download inkl. SHA-256 →
/// <c>ReadyToInstall</c>. Die eigentliche Installation (Skript-Start, Dienst-
/// Stopp/Neustart) ist nicht automatisierbar und bleibt manuelle Abnahme.</summary>
public sealed class StatusServerUpdateFlowTests
{
    /// <summary>Happy Path: Die lokale Quelle meldet 9.9.9 gegenüber der
    /// installierten 1.0.0 → der manuelle Check-POST lädt das Paket → der
    /// Snapshot steht auf <c>ReadyToInstall</c> und die Admin-Seite zeigt die
    /// verfügbare Version.</summary>
    [Fact]
    public async Task LocalFolderSource_CheckAndDownload_ReachesReadyToInstall()
    {
        using var source = new TempUpdateSource();
        var packageName = "statusserver-win-x64.zip";
        source.WritePackage(packageName);
        source.WriteManifest("9.9.9", packageName);

        await using var factory = new UpdateTestFactory(source.DirectoryPath);
        using var admin = AuthTestClient.Create(factory);
        var ct = TestContext.Current.CancellationToken;
        (await admin.RegisterFirstUserAsync("admin", "Geheim-123", ct))
            .StatusCode.Should().Be(HttpStatusCode.Redirect);

        await admin.RefreshAntiforgeryAsync("/admin/settings", ct);
        var response = await admin.PostFormAsync("/admin/settings/update/check", cancellationToken: ct);
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().Be("/admin/settings?msg=update-check-done");

        var snapshot = await factory.Services.GetRequiredService<IAutoUpdateOrchestrator>()
            .GetStatusAsync(ct);
        snapshot.State.Should().Be(AutoUpdateState.ReadyToInstall);
        snapshot.AvailableVersion.Should().Be("9.9.9");
        snapshot.InstalledVersion.Should().Be("1.0.0");

        var page = await admin.FollowRedirectAsync(response, ct);
        page.Should().Contain("9.9.9");
        page.Should().Contain("Bereit zur Installation");
    }

    /// <summary>Fehlerpfad: Der SHA-256 im Manifest passt nicht zum Paket — der
    /// Download schlägt fehl, der Check-POST leitet mit
    /// <c>err=update-check-failed</c> um und der Snapshot trägt den Fehler.</summary>
    [Fact]
    public async Task LocalFolderSource_HashMismatch_CheckFails()
    {
        using var source = new TempUpdateSource();
        var packageName = "statusserver-win-x64.zip";
        source.WritePackage(packageName);
        source.WriteManifest("9.9.9", packageName, sha256Override: new string('0', 64));

        await using var factory = new UpdateTestFactory(source.DirectoryPath);
        using var admin = AuthTestClient.Create(factory);
        var ct = TestContext.Current.CancellationToken;
        (await admin.RegisterFirstUserAsync("admin", "Geheim-123", ct))
            .StatusCode.Should().Be(HttpStatusCode.Redirect);

        await admin.RefreshAntiforgeryAsync("/admin/settings", ct);
        var response = await admin.PostFormAsync("/admin/settings/update/check", cancellationToken: ct);
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().Be("/admin/settings?err=update-check-failed");

        var snapshot = await factory.Services.GetRequiredService<IAutoUpdateOrchestrator>()
            .GetStatusAsync(ct);
        snapshot.State.Should().Be(AutoUpdateState.Failed);
        snapshot.LastError.Should().NotBeNullOrEmpty();
    }

    /// <summary>Neustart-Persistenz: Über die Admin-UI gespeicherte
    /// Update-Einstellungen werden beim nächsten Start aus der
    /// <c>ServerSettings</c>-Tabelle gelesen und auf die Laufzeitoptionen
    /// angewendet (<see cref="AutoUpdateApplicationExtensions"/>-Verdrahtung).</summary>
    [Fact]
    public async Task Restart_SameDatabase_UpdateSettingsApplyOnStartup()
    {
        using var source = new TempUpdateSource();
        var dir = Path.Combine(Path.GetTempPath(), $"statusserver-e2e-update-{Guid.NewGuid():N}");
        var dbPath = Path.Combine(dir, "statusserver.db");
        try
        {
            await using (var factory = new UpdateTestFactory(source.DirectoryPath)
            {
                DatabasePathOverride = dbPath,
            })
            {
                var ct = TestContext.Current.CancellationToken;
                using var admin = AuthTestClient.Create(factory);
                (await admin.RegisterFirstUserAsync("admin", "Geheim-123", ct))
                    .StatusCode.Should().Be(HttpStatusCode.Redirect);

                await admin.RefreshAntiforgeryAsync("/admin/settings", ct);
                var response = await admin.PostFormAsync("/admin/settings/update",
                [
                    new KeyValuePair<string, string>("channel", "prerelease"),
                    new KeyValuePair<string, string>("mode", "install"),
                    new KeyValuePair<string, string>("installTime", "04:30"),
                ], cancellationToken: ct);
                response.StatusCode.Should().Be(HttpStatusCode.Redirect);
            }

            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

            await using (var factory = new UpdateTestFactory(source.DirectoryPath)
            {
                DatabasePathOverride = dbPath,
            })
            {
                // Der Server muss gestartet sein, damit ApplyPersistedUpdateSettingsAsync
                // gelaufen ist — ein beliebiger Request erzeugt den Host.
                _ = factory.Server;

                var options = factory.Services.GetRequiredService<AutoUpdateOptions>();
                options.Enabled.Should().BeTrue();
                options.AllowPrereleaseUpdates.Should().BeTrue();
                options.EnableAutomaticDownload.Should().BeTrue();
                options.EnableAutomaticInstallation.Should().BeTrue();
                options.ScheduledInstallTime.Should().Be(new TimeOnly(4, 30));
                options.Source.Should().BeOfType<AutoUpdateLocalFolderSource>();
            }
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            try { Directory.Delete(dir, recursive: true); } catch (IOException) { }
        }
    }
}
