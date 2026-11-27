using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Softwareschmiede.StatusServer.Data;
using Softwareschmiede.StatusServer.Security;

namespace Softwareschmiede.StatusServer.Tests.TestSupport;

/// <summary>Scope-basierter Datenbankzugriff für Tests: Benutzer anlegen, Relay-Tokens
/// seeden, Deaktivierung setzen und Lockout-/Einstellungszustände prüfen.</summary>
public sealed class StatusServerDbSeeder
{
    private readonly StatusWebApplicationFactory _factory;

    /// <summary>Erstellt den Seeder.</summary>
    /// <param name="factory">Die Server-Factory (liefert den Service-Provider).</param>
    public StatusServerDbSeeder(StatusWebApplicationFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _factory = factory;
    }

    /// <summary>Legt einen Benutzer mit der angegebenen Stufe an.</summary>
    /// <param name="userName">Der Benutzername.</param>
    /// <param name="password">Das Passwort.</param>
    /// <param name="role">Die Stufe (<see cref="StatusRoles.Administration"/> oder <see cref="StatusRoles.Ueberwacher"/>).</param>
    /// <returns>Das Service-Ergebnis.</returns>
    public Task<UserAdminResult> CreateUserAsync(string userName, string password, string role)
        => WithScopeAsync(sp => sp.GetRequiredService<UserAdminService>()
            .CreateUserAsync(userName, password, role));

    /// <summary>Ruft die Erst-Registrierung des Services direkt auf
    /// (für Setup-Guard-/Rollback-Tests ohne HTTP-Umweg).</summary>
    /// <param name="userName">Der Benutzername.</param>
    /// <param name="password">Das Passwort.</param>
    /// <returns>Das Service-Ergebnis.</returns>
    public Task<UserAdminResult> RegisterFirstAdminAsync(string userName, string password)
        => WithScopeAsync(sp => sp.GetRequiredService<UserAdminService>()
            .RegisterFirstAdminAsync(userName, password));

    /// <summary>Löscht eine Rolle direkt aus der Datenbank — simuliert den
    /// Fehlerpfad, bei dem die Rollenzuweisung nach der Benutzeranlage scheitert.</summary>
    /// <param name="roleName">Der Rollenname.</param>
    /// <returns>Eine abgeschlossene Aufgabe.</returns>
    public Task DeleteRoleAsync(string roleName)
        => WithScopeAsync(async sp =>
        {
            var roleManager = sp.GetRequiredService<RoleManager<IdentityRole>>();
            var role = await roleManager.FindByNameAsync(roleName).ConfigureAwait(false);
            if (role is not null)
                await roleManager.DeleteAsync(role).ConfigureAwait(false);
        });

    /// <summary>Stellt eine Rolle (wieder) her, falls sie fehlt.</summary>
    /// <param name="roleName">Der Rollenname.</param>
    /// <returns>Eine abgeschlossene Aufgabe.</returns>
    public Task EnsureRoleAsync(string roleName)
        => WithScopeAsync(async sp =>
        {
            var roleManager = sp.GetRequiredService<RoleManager<IdentityRole>>();
            if (!await roleManager.RoleExistsAsync(roleName).ConfigureAwait(false))
                await roleManager.CreateAsync(new IdentityRole(roleName)).ConfigureAwait(false);
        });

    /// <summary>Schreibt einen ServerSettings-Eintrag direkt (z. B. die
    /// <see cref="UserAdminService.SetupCompletedKey"/>-Setup-Markierung).</summary>
    /// <param name="key">Der Schlüssel.</param>
    /// <param name="value">Der Wert.</param>
    /// <returns>Eine abgeschlossene Aufgabe.</returns>
    public Task AddServerSettingAsync(string key, string value)
        => WithScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<StatusServerDbContext>();
            db.ServerSettings.Add(new ServerSetting { Key = key, Value = value });
            await db.SaveChangesAsync().ConfigureAwait(false);
        });

    /// <summary>Erzeugt ein Relay-Token (Klartext im Ergebnis enthalten).</summary>
    /// <param name="label">Das Label.</param>
    /// <returns>Datensatz und Klartext.</returns>
    public Task<CreatedRelayToken> SeedRelayTokenAsync(string label)
        => WithScopeAsync(sp => sp.GetRequiredService<RelayTokenService>().CreateAsync(label));

    /// <summary>Setzt oder löst die Deaktivierung eines Benutzers.</summary>
    /// <param name="userName">Der Benutzername.</param>
    /// <param name="deactivated">Der Zielzustand.</param>
    /// <returns>Das Service-Ergebnis.</returns>
    public Task<UserAdminResult> SetDeactivatedAsync(string userName, bool deactivated)
        => WithScopeAsync(async sp =>
        {
            var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByNameAsync(userName).ConfigureAwait(false);
            if (user is null)
                return UserAdminResult.NotFound("Der Benutzer wurde nicht gefunden.");
            return await sp.GetRequiredService<UserAdminService>()
                .SetDeactivatedAsync(user.Id, deactivated)
                .ConfigureAwait(false);
        });

    /// <summary>Löscht ein Benutzerkonto über den produktiven Service
    /// (z. B. für parallele Letzter-Admin-Tests).</summary>
    /// <param name="userName">Der Benutzername.</param>
    /// <returns>Das Service-Ergebnis.</returns>
    public Task<UserAdminResult> DeleteUserAsync(string userName)
        => WithScopeAsync(async sp =>
        {
            var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByNameAsync(userName).ConfigureAwait(false);
            if (user is null)
                return UserAdminResult.NotFound("Der Benutzer wurde nicht gefunden.");
            return await sp.GetRequiredService<UserAdminService>()
                .DeleteUserAsync(user.Id)
                .ConfigureAwait(false);
        });

    /// <summary>Simuliert eine parallele Mutation am selben Benutzer: lädt den Benutzer
    /// in einen Scope (mit dessen <c>ConcurrencyStamp</c> getrackt), ändert den
    /// Datensatz über einen zweiten Scope und ruft dann die übergebene
    /// Service-Mutation im ersten Scope auf — der Schreibzugriff der veralteten
    /// Entität trifft deterministisch auf den Concurrency-Konflikt.</summary>
    /// <param name="userName">Der Benutzername (muss existieren).</param>
    /// <param name="mutate">Die auszuführende Mutation (Service + Benutzer-Id).</param>
    /// <returns>Das Service-Ergebnis der veralteten Mutation.</returns>
    public async Task<UserAdminResult> MutateWithConcurrentChangeAsync(
        string userName, Func<UserAdminService, string, Task<UserAdminResult>> mutate)
    {
        ArgumentNullException.ThrowIfNull(mutate);
        await using var scope = _factory.Services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var user = await sp.GetRequiredService<UserManager<ApplicationUser>>()
            .FindByNameAsync(userName)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Benutzer '{userName}' nicht gefunden.");

        // Parallele Änderung desselben Datensatzes: Der im ersten Scope getrackte
        // ConcurrencyStamp ist danach veraltet — die Mutation schlägt mit dem
        // Concurrency-Konflikt fehl, den der Service abfängt.
        await BumpConcurrencyStampAsync(user.Id).ConfigureAwait(false);
        return await mutate(sp.GetRequiredService<UserAdminService>(), user.Id).ConfigureAwait(false);
    }

    /// <summary>Zählt die Relay-Token-Datensätze.</summary>
    /// <returns>Die Anzahl der Tokens.</returns>
    public Task<int> CountRelayTokensAsync()
        => WithScopeAsync(sp => sp.GetRequiredService<StatusServerDbContext>().RelayTokens.CountAsync());

    /// <summary>Liefert das Sperr-Ende (LockoutEnd) eines Benutzers.</summary>
    /// <param name="userName">Der Benutzername.</param>
    /// <returns>Das Lockout-Ende (UTC) oder <see langword="null"/>.</returns>
    public Task<DateTimeOffset?> GetLockoutEndAsync(string userName)
        => WithScopeAsync(async sp =>
            (await sp.GetRequiredService<UserManager<ApplicationUser>>()
                .FindByNameAsync(userName)
                .ConfigureAwait(false))?.LockoutEnd);

    /// <summary>Liefert die Id eines Benutzers (für die Admin-Route-Parameter).</summary>
    /// <param name="userName">Der Benutzername.</param>
    /// <returns>Die Benutzer-Id oder <see langword="null"/>.</returns>
    public Task<string?> GetUserIdAsync(string userName)
        => WithScopeAsync(async sp =>
            (await sp.GetRequiredService<UserManager<ApplicationUser>>()
                .FindByNameAsync(userName)
                .ConfigureAwait(false))?.Id);

    /// <summary>Liefert die Rollen eines Benutzers.</summary>
    /// <param name="userName">Der Benutzername.</param>
    /// <returns>Die Rollenliste.</returns>
    public Task<IList<string>> GetRolesAsync(string userName)
        => WithScopeAsync(async sp =>
        {
            var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByNameAsync(userName).ConfigureAwait(false);
            return user is null ? [] : await userManager.GetRolesAsync(user).ConfigureAwait(false);
        });

    /// <summary>Setzt die admin-konfigurierbare Sitzungsdauer direkt.</summary>
    /// <param name="minutes">Die Dauer in Minuten.</param>
    /// <returns>Eine abgeschlossene Aufgabe.</returns>
    public Task SetSessionDurationAsync(int minutes)
        => WithScopeAsync(sp => sp.GetRequiredService<ServerSettingsService>().SetSessionDurationAsync(minutes));

    /// <summary>Liest die aktuell konfigurierte Sitzungsdauer.</summary>
    /// <returns>Die Sitzungsdauer in Minuten.</returns>
    public Task<int> GetSessionDurationAsync()
        => WithScopeAsync(sp => sp.GetRequiredService<ServerSettingsService>().GetSessionDurationAsync());

    /// <summary>Zählt die Benutzerkonten.</summary>
    /// <returns>Die Anzahl der Benutzer.</returns>
    public Task<int> CountUsersAsync()
        => WithScopeAsync(sp => sp.GetRequiredService<UserManager<ApplicationUser>>().Users.CountAsync());

    /// <summary>Liefert den Relay-Token-Datensatz zu einem Label.</summary>
    /// <param name="label">Das Label.</param>
    /// <returns>Der Datensatz oder <see langword="null"/>.</returns>
    public Task<RelayToken?> GetRelayTokenAsync(string label)
        => WithScopeAsync(sp => sp.GetRequiredService<StatusServerDbContext>().RelayTokens
            .FirstOrDefaultAsync(t => t.Label == label));

    /// <summary>Widerruft ein Relay-Token über den produktiven Service.</summary>
    /// <param name="id">Die Token-Id.</param>
    /// <returns><see langword="true"/>, wenn das Token gefunden wurde.</returns>
    public Task<bool> RevokeRelayTokenAsync(long id)
        => WithScopeAsync(sp => sp.GetRequiredService<RelayTokenService>().RevokeAsync(id));

    /// <summary>Prüft einen Token-Klartext gegen den produktiven <see cref="RelayTokenValidator"/>.</summary>
    /// <param name="token">Der Klartext.</param>
    /// <returns><see langword="true"/>, wenn das Token aktuell gültig ist.</returns>
    public Task<bool> IsRelayTokenValidAsync(string token)
        => WithScopeAsync(sp => sp.GetRequiredService<RelayTokenValidator>().IsValidAsync(token));

    /// <summary>Schreibt einen neuen <c>ConcurrencyStamp</c> direkt auf den
    /// Benutzer-Datensatz — macht die in einem anderen Scope getrackte Entität
    /// veraltet (simulierter paralleler Schreibzugriff).</summary>
    /// <param name="userId">Die Benutzer-Id.</param>
    /// <returns>Eine abgeschlossene Aufgabe.</returns>
    private Task BumpConcurrencyStampAsync(string userId)
        => WithScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<StatusServerDbContext>();
            var user = await db.Users.FirstAsync(u => u.Id == userId).ConfigureAwait(false);
            user.ConcurrencyStamp = Guid.NewGuid().ToString("N");
            await db.SaveChangesAsync().ConfigureAwait(false);
        });

    private async Task<T> WithScopeAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider).ConfigureAwait(false);
    }

    private Task WithScopeAsync(Func<IServiceProvider, Task> action)
        => WithScopeAsync<object?>(async sp =>
        {
            await action(sp).ConfigureAwait(false);
            return null;
        });
}
