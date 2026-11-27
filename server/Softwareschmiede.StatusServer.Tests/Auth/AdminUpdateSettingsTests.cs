using System.Net;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using msTools.Updater;
using Softwareschmiede.StatusServer.Security;
using Softwareschmiede.StatusServer.Tests.TestSupport;
using Softwareschmiede.StatusServer.Update;

namespace Softwareschmiede.StatusServer.Tests.Auth;

/// <summary>HTTP-Flow-Tests für die Update-Sektion der Admin-Einstellungsseite:
/// Persistieren/Anwenden der Update-Einstellungen, Validierung, Autorisierung
/// und die manuellen Update-Aktionen (PRG-Redirects).</summary>
public sealed class AdminUpdateSettingsTests
{
    /// <summary>Gespeicherte Update-Einstellungen landen in der
    /// <c>ServerSettings</c>-Tabelle und werden sofort auf die laufzeitveränderbaren
    /// <see cref="AutoUpdateOptions"/> angewendet (Kanalwechsel baut die Quelle neu).</summary>
    [Fact]
    public async Task UpdateSettings_Post_PersistsAndAppliesToRuntimeOptions()
    {
        await using var factory = new StatusWebApplicationFactory();
        using var admin = AuthTestClient.Create(factory);
        var seeder = new StatusServerDbSeeder(factory);
        var ct = TestContext.Current.CancellationToken;

        (await admin.RegisterFirstUserAsync("admin", "Geheim-123", ct)).StatusCode.Should().Be(HttpStatusCode.Redirect);

        await admin.RefreshAntiforgeryAsync("/admin/settings", ct);
        var response = await admin.PostFormAsync("/admin/settings/update",
        [
            new KeyValuePair<string, string>("channel", "prerelease"),
            new KeyValuePair<string, string>("mode", "install"),
            new KeyValuePair<string, string>("installTime", "03:30"),
        ], cancellationToken: ct);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().Be("/admin/settings?msg=update-settings-saved");
        (await admin.FollowRedirectAsync(response, ct)).Should().Contain("gespeichert");

        var stored = await seeder.GetUpdateSettingsAsync();
        stored.Channel.Should().Be(StatusServerUpdateChannel.ReleaseCandidate);
        stored.Mode.Should().Be(StatusServerAutomationMode.CheckAndInstall);
        stored.ScheduledInstallTime.Should().Be(new TimeOnly(3, 30));

        var options = factory.Services.GetRequiredService<AutoUpdateOptions>();
        options.Enabled.Should().BeTrue();
        options.AllowPrereleaseUpdates.Should().BeTrue();
        options.EnableAutomaticDownload.Should().BeTrue();
        options.EnableAutomaticInstallation.Should().BeTrue();
        options.ScheduledInstallTime.Should().Be(new TimeOnly(3, 30));
        options.Source.Should().BeOfType<AutoUpdateGithubSource>(
            "der Default-SourceType ist Github — der Kanalwechsel baut die Quelle neu");
        (options.Source as AutoUpdateGithubSource)?.Dispose();
    }

    /// <summary>Unbekannter Kanal, unbekannter Modus und eine fehlende
    /// Installationsuhrzeit bei „Prüfen und installieren" werden mit erklärender
    /// Fehlermeldung abgelehnt — ohne Persistenz.</summary>
    [Fact]
    public async Task UpdateSettings_InvalidValues_RenderErrors()
    {
        await using var factory = new StatusWebApplicationFactory();
        using var admin = AuthTestClient.Create(factory);
        var seeder = new StatusServerDbSeeder(factory);
        var ct = TestContext.Current.CancellationToken;

        (await admin.RegisterFirstUserAsync("admin", "Geheim-123", ct)).StatusCode.Should().Be(HttpStatusCode.Redirect);
        await admin.RefreshAntiforgeryAsync("/admin/settings", ct);

        var badChannel = await admin.PostFormAsync("/admin/settings/update",
        [
            new KeyValuePair<string, string>("channel", "bogus"),
            new KeyValuePair<string, string>("mode", "off"),
        ], cancellationToken: ct);
        badChannel.StatusCode.Should().Be(HttpStatusCode.OK);
        var badChannelHtml = await badChannel.Content.ReadAsStringAsync(ct);
        badChannelHtml.Should().Contain("Unbekannter Release-Kanal");
        admin.ExtractAntiforgery(badChannelHtml, "Fehlerseite Kanal");

        var badMode = await admin.PostFormAsync("/admin/settings/update",
        [
            new KeyValuePair<string, string>("channel", "stable"),
            new KeyValuePair<string, string>("mode", "bogus"),
        ], cancellationToken: ct);
        var badModeHtml = await badMode.Content.ReadAsStringAsync(ct);
        badModeHtml.Should().Contain("Unbekannter Automatisierungsmodus");
        admin.ExtractAntiforgery(badModeHtml, "Fehlerseite Modus");

        var missingTime = await admin.PostFormAsync("/admin/settings/update",
        [
            new KeyValuePair<string, string>("channel", "stable"),
            new KeyValuePair<string, string>("mode", "install"),
        ], cancellationToken: ct);
        (await missingTime.Content.ReadAsStringAsync(ct)).Should().Contain("Installationsuhrzeit");

        // Kein erfolgreicher POST → die Defaults bleiben persistiert.
        var stored = await seeder.GetUpdateSettingsAsync();
        stored.Channel.Should().Be(StatusServerUpdateChannel.Stable);
        stored.Mode.Should().Be(StatusServerAutomationMode.Off);
    }

    /// <summary>Die Update-Endpunkte erfordern die Admin-Stufe und das
    /// Antiforgery-Token: anonym → /login, Überwacher → /access-denied,
    /// Admin ohne Token → 400-Seite.</summary>
    [Fact]
    public async Task UpdateSettings_RequiresAdminRoleAndAntiforgery()
    {
        await using var factory = new StatusWebApplicationFactory();
        using var admin = AuthTestClient.Create(factory);
        var seeder = new StatusServerDbSeeder(factory);
        var ct = TestContext.Current.CancellationToken;

        (await admin.RegisterFirstUserAsync("admin", "Geheim-123", ct)).StatusCode.Should().Be(HttpStatusCode.Redirect);
        (await seeder.CreateUserAsync("watcher", "Geheim-123", StatusRoles.Ueberwacher)).Succeeded.Should().BeTrue();

        using var anonymous = AuthTestClient.Create(factory);
        var noAuth = await anonymous.Http.PostAsync("/admin/settings/update",
            new FormUrlEncodedContent([]), ct);
        noAuth.StatusCode.Should().Be(HttpStatusCode.Redirect);
        noAuth.Headers.Location!.OriginalString.Should().Contain("/login");

        using var watcher = AuthTestClient.Create(factory);
        (await watcher.LoginAsync("watcher", "Geheim-123", ct)).StatusCode.Should().Be(HttpStatusCode.Redirect);
        var denied = await watcher.Http.PostAsync("/admin/settings/update",
            new FormUrlEncodedContent([]), ct);
        denied.StatusCode.Should().Be(HttpStatusCode.Redirect);
        denied.Headers.Location!.OriginalString.Should().Contain("/access-denied");

        await admin.RefreshAntiforgeryAsync("/admin/settings", ct);
        var missingToken = await admin.PostFormAsync("/admin/settings/update",
            [new KeyValuePair<string, string>("channel", "stable")],
            includeAntiforgery: false, cancellationToken: ct);
        missingToken.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>Ein erfolgreicher manueller Check leitet mit
    /// <c>msg=update-check-done</c> um; ein Manifest mit falschem SHA-256
    /// (Download-Verifikation schlägt fehl) mit <c>err=update-check-failed</c>.
    /// Beide Pfade kommen ohne GitHub aus (<see cref="UpdateTestFactory"/> mit
    /// lokalem Quellverzeichnis).</summary>
    [Fact]
    public async Task UpdateCheck_Post_RedirectsPerOutcome()
    {
        using var source = new TempUpdateSource();
        var packageName = "statusserver-win-x64.zip";
        source.WritePackage(packageName);
        source.WriteManifest("9.9.9", packageName);

        await using var factory = new UpdateTestFactory(source.DirectoryPath);
        using var admin = AuthTestClient.Create(factory);
        var ct = TestContext.Current.CancellationToken;
        (await admin.RegisterFirstUserAsync("admin", "Geheim-123", ct)).StatusCode.Should().Be(HttpStatusCode.Redirect);

        await admin.RefreshAntiforgeryAsync("/admin/settings", ct);
        var ok = await admin.PostFormAsync("/admin/settings/update/check", cancellationToken: ct);
        ok.StatusCode.Should().Be(HttpStatusCode.Redirect);
        ok.Headers.Location!.OriginalString.Should().Be("/admin/settings?msg=update-check-done");

        // Hash-Mismatch → Download schlägt fehl → err-Redirect.
        using var badSource = new TempUpdateSource();
        badSource.WritePackage(packageName);
        badSource.WriteManifest("9.9.9", packageName, sha256Override: new string('0', 64));

        await using var failingFactory = new UpdateTestFactory(badSource.DirectoryPath);
        using var failingAdmin = AuthTestClient.Create(failingFactory);
        (await failingAdmin.RegisterFirstUserAsync("admin", "Geheim-123", ct))
            .StatusCode.Should().Be(HttpStatusCode.Redirect);

        await failingAdmin.RefreshAntiforgeryAsync("/admin/settings", ct);
        var failed = await failingAdmin.PostFormAsync("/admin/settings/update/check", cancellationToken: ct);
        failed.StatusCode.Should().Be(HttpStatusCode.Redirect);
        failed.Headers.Location!.OriginalString.Should().Be("/admin/settings?err=update-check-failed");
    }

    /// <summary>Der Install-POST ohne Downtime-Bestätigung rendert die Fehlermeldung
    /// direkt (kein Redirect) und startet nichts.</summary>
    [Fact]
    public async Task UpdateInstall_WithoutDowntimeConfirmation_RendersError()
    {
        using var source = new TempUpdateSource();
        await using var factory = new UpdateTestFactory(source.DirectoryPath);
        using var admin = AuthTestClient.Create(factory);
        var ct = TestContext.Current.CancellationToken;
        (await admin.RegisterFirstUserAsync("admin", "Geheim-123", ct)).StatusCode.Should().Be(HttpStatusCode.Redirect);

        await admin.RefreshAntiforgeryAsync("/admin/settings", ct);
        var response = await admin.PostFormAsync("/admin/settings/update/install", cancellationToken: ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(ct)).Should().Contain("Downtime");
    }

    /// <summary>Der Install-POST mit bestätigter Downtime, aber ohne bereit
    /// liegendes Paket (<c>InstallAsync</c> meldet <c>Failed</c>), leitet mit
    /// <c>err=update-install-failed</c> um.</summary>
    [Fact]
    public async Task UpdateInstall_NoReadyPackage_RedirectsWithError()
    {
        using var source = new TempUpdateSource();
        await using var factory = new UpdateTestFactory(source.DirectoryPath);
        using var admin = AuthTestClient.Create(factory);
        var ct = TestContext.Current.CancellationToken;
        (await admin.RegisterFirstUserAsync("admin", "Geheim-123", ct)).StatusCode.Should().Be(HttpStatusCode.Redirect);

        await admin.RefreshAntiforgeryAsync("/admin/settings", ct);
        var response = await admin.PostFormAsync("/admin/settings/update/install",
            [new KeyValuePair<string, string>("confirmDowntime", "true")], cancellationToken: ct);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().Be("/admin/settings?err=update-install-failed");
    }

    /// <summary>Ein vom <c>BeforeInstall</c>-Abonnenten abgebrochener
    /// Installationsanlauf (Outcome <c>Canceled</c> — das Skript startet nicht,
    /// der Server läuft weiter) leitet per Post/Redirect/Get mit
    /// <c>msg=update-install-not-started</c> um — nicht auf die statische
    /// „Der Server startet neu"-Seite.</summary>
    [Fact]
    public async Task UpdateInstall_CanceledInstall_RedirectsNotStarted()
    {
        using var source = new TempUpdateSource();
        var packageName = "statusserver-win-x64.zip";
        source.WritePackage(packageName);
        source.WriteManifest("9.9.9", packageName);

        await using var factory = new UpdateTestFactory(source.DirectoryPath);
        using var admin = AuthTestClient.Create(factory);
        var ct = TestContext.Current.CancellationToken;
        (await admin.RegisterFirstUserAsync("admin", "Geheim-123", ct)).StatusCode.Should().Be(HttpStatusCode.Redirect);

        // Paket bis ReadyToInstall bringen — erst dann erreicht InstallAsync
        // das kürzende BeforeInstall-Ereignis.
        await admin.RefreshAntiforgeryAsync("/admin/settings", ct);
        var check = await admin.PostFormAsync("/admin/settings/update/check", cancellationToken: ct);
        check.Headers.Location!.OriginalString.Should().Be("/admin/settings?msg=update-check-done");

        var events = factory.Services.GetRequiredService<IAutoUpdateEventAggregator>();
        events.BeforeInstall += (_, args) => args.Cancel = true;

        var response = await admin.PostFormAsync("/admin/settings/update/install",
            [new KeyValuePair<string, string>("confirmDowntime", "true")], cancellationToken: ct);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().Be("/admin/settings?msg=update-install-not-started");
        (await admin.FollowRedirectAsync(response, ct)).Should().Contain("nicht gestartet");
    }

    /// <summary>Der Lock-Reset-POST ohne vorhandenen Lock leitet mit
    /// <c>err=update-lock-reset-failed</c> um.</summary>
    [Fact]
    public async Task UpdateLockReset_NoLock_RedirectsWithError()
    {
        using var source = new TempUpdateSource();
        await using var factory = new UpdateTestFactory(source.DirectoryPath);
        using var admin = AuthTestClient.Create(factory);
        var ct = TestContext.Current.CancellationToken;
        (await admin.RegisterFirstUserAsync("admin", "Geheim-123", ct)).StatusCode.Should().Be(HttpStatusCode.Redirect);

        await admin.RefreshAntiforgeryAsync("/admin/settings", ct);
        var response = await admin.PostFormAsync("/admin/settings/update/lock-reset", cancellationToken: ct);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().Be("/admin/settings?err=update-lock-reset-failed");
    }
}
