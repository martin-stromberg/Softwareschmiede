using FluentAssertions;
using Softwareschmiede.StatusServer.Security;
using Softwareschmiede.StatusServer.Tests.TestSupport;

namespace Softwareschmiede.StatusServer.Tests.Security;

/// <summary>Service-Tests für <see cref="UserAdminService"/>: der atomare Setup-Guard
/// (SetupCompleted-Markierung) und das transaktionale Rollback bei fehlgeschlagener
/// Rollenzuweisung (kein verwaistes bzw. stufenloses Konto).</summary>
public sealed class UserAdminServiceTests
{
    /// <summary>Steht die Setup-Markierung bereits in der Datenbank (z. B. weil ein
    /// paralleler Setup-Lauf sie geschrieben hat), schlägt die Erst-Registrierung
    /// fehl — auch wenn noch kein Benutzer existiert.</summary>
    [Fact]
    public async Task RegisterFirstAdmin_SetupMarkerSeeded_IsRejected()
    {
        await using var factory = new StatusWebApplicationFactory();
        var seeder = new StatusServerDbSeeder(factory);

        await seeder.AddServerSettingAsync(UserAdminService.SetupCompletedKey, "1");

        var result = await seeder.RegisterFirstAdminAsync("admin", "Geheim-123");

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Contain("bereits");
        (await seeder.CountUsersAsync()).Should().Be(0);
    }

    /// <summary>Scheitert die Rollenzuweisung nach der Benutzeranlage (hier: Rolle
    /// wurde aus der DB entfernt), wird die Transaktion zurückgerollt: Es bleibt
    /// weder ein stufenloser Benutzer noch eine Setup-Markierung zurück — die
    /// Registrierung bleibt geöffnet und ein erneuter Versuch kann gelingen.</summary>
    [Fact]
    public async Task RegisterFirstAdmin_RoleMissing_RollsBackCompletely()
    {
        await using var factory = new StatusWebApplicationFactory();
        var seeder = new StatusServerDbSeeder(factory);
        var ct = TestContext.Current.CancellationToken;

        await seeder.DeleteRoleAsync(StatusRoles.Administration);
        var act = async () => await seeder.RegisterFirstAdminAsync("admin", "Geheim-123");
        await act.Should().ThrowAsync<Exception>();

        // Kein verwaister Benutzer, keine Setup-Markierung → /register bleibt offen.
        (await seeder.CountUsersAsync()).Should().Be(0);
        using var client = AuthTestClient.Create(factory);
        (await client.Http.GetAsync("/register", ct)).StatusCode
            .Should().Be(System.Net.HttpStatusCode.OK);

        // Rolle wiederherstellen → ein erneuter Setup-Versuch gelingt.
        await seeder.EnsureRoleAsync(StatusRoles.Administration);
        var retry = await seeder.RegisterFirstAdminAsync("admin", "Geheim-123");
        retry.Succeeded.Should().BeTrue();
        (await seeder.GetRolesAsync("admin")).Should().Contain(StatusRoles.Administration);
    }

    /// <summary>Scheitert die Rollenzuweisung beim Admin-Anlegen, wird der
    /// Benutzer zurückgerollt — kein stufenloses Konto bleibt zurück.</summary>
    [Fact]
    public async Task CreateUser_RoleMissing_RollsBackUser()
    {
        await using var factory = new StatusWebApplicationFactory();
        var seeder = new StatusServerDbSeeder(factory);

        await seeder.DeleteRoleAsync(StatusRoles.Ueberwacher);
        var act = async () => await seeder.CreateUserAsync("watcher", "Geheim-123", StatusRoles.Ueberwacher);
        await act.Should().ThrowAsync<Exception>();

        (await seeder.CountUsersAsync()).Should().Be(0);
        (await seeder.GetUserIdAsync("watcher")).Should().BeNull();
    }

    /// <summary>Zwei parallele Deaktivierungen der beiden letzten aktiven Admins:
    /// Der prozessweite Lock serialisiert Letzter-Admin-Prüfung und Mutation —
    /// genau ein Request scheitert, ein aktiver Admin bleibt (kein unverwaltbares System).</summary>
    [Fact]
    public async Task SetDeactivated_ParallelOnLastTwoAdmins_ExactlyOneFails()
    {
        await using var factory = new StatusWebApplicationFactory();
        var seeder = new StatusServerDbSeeder(factory);

        (await seeder.CreateUserAsync("admin1", "Geheim-123", StatusRoles.Administration)).Succeeded.Should().BeTrue();
        (await seeder.CreateUserAsync("admin2", "Geheim-123", StatusRoles.Administration)).Succeeded.Should().BeTrue();

        var results = await Task.WhenAll(
            seeder.SetDeactivatedAsync("admin1", true),
            seeder.SetDeactivatedAsync("admin2", true));

        results.Count(r => r.Succeeded).Should().Be(1);
        results.Count(r => !r.Succeeded && r.Error!.Contains("letzte aktive Administrator")).Should().Be(1);
    }

    /// <summary>Entsprechendes Parallel-Szenario beim Löschen — die gleiche
    /// Serialisierung schützt auch den Löschpfad.</summary>
    [Fact]
    public async Task DeleteUser_ParallelOnLastTwoAdmins_ExactlyOneFails()
    {
        await using var factory = new StatusWebApplicationFactory();
        var seeder = new StatusServerDbSeeder(factory);

        (await seeder.CreateUserAsync("admin1", "Geheim-123", StatusRoles.Administration)).Succeeded.Should().BeTrue();
        (await seeder.CreateUserAsync("admin2", "Geheim-123", StatusRoles.Administration)).Succeeded.Should().BeTrue();

        var results = await Task.WhenAll(
            seeder.DeleteUserAsync("admin1"),
            seeder.DeleteUserAsync("admin2"));

        results.Count(r => r.Succeeded).Should().Be(1);
        results.Count(r => !r.Succeeded && r.Error!.Contains("letzte aktive Administrator")).Should().Be(1);
        (await seeder.CountUsersAsync()).Should().Be(1);
    }

    /// <summary>Eine parallele Änderung am selben Benutzer (ConcurrencyStamp-Konflikt,
    /// hier deterministisch über eine veraltete Entität erzeugt) wird an allen
    /// Mutationspfaden als kontrolliertes <see cref="UserAdminResult"/> mit
    /// verständlichem Grund zurückgegeben — nicht als unbehandelte
    /// <c>DbUpdateConcurrencyException</c> (HTTP 500).</summary>
    [Fact]
    public async Task Mutations_ConcurrentChangeOnSameUser_ReturnFailed()
    {
        await using var factory = new StatusWebApplicationFactory();
        var seeder = new StatusServerDbSeeder(factory);

        (await seeder.CreateUserAsync("admin", "Geheim-123", StatusRoles.Administration)).Succeeded.Should().BeTrue();
        (await seeder.CreateUserAsync("watcher", "Geheim-123", StatusRoles.Ueberwacher)).Succeeded.Should().BeTrue();

        foreach (var mutate in new Func<UserAdminService, string, Task<UserAdminResult>>[]
        {
            (s, id) => s.SetDeactivatedAsync(id, true),
            (s, id) => s.ResetLockoutAsync(id),
            (s, id) => s.SetRoleAsync(id, StatusRoles.Administration),
            (s, id) => s.DeleteUserAsync(id),
        })
        {
            var result = await seeder.MutateWithConcurrentChangeAsync("watcher", mutate);
            result.Succeeded.Should().BeFalse();
            result.Error.Should().Contain("zwischenzeitlich geändert");
        }

        // Der Benutzer selbst bleibt unverändert bestehen (der erste Schreibvorgang
        // der simulierten Konkurrenz gewann — hier nur der Stamp-Bump).
        (await seeder.GetUserIdAsync("watcher")).Should().NotBeNull();
    }
}
