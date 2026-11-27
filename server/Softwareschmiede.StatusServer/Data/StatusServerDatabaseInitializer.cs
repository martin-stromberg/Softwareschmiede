using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Softwareschmiede.StatusServer.Security;

namespace Softwareschmiede.StatusServer.Data;

/// <summary>Initialisiert beim Server-Start die Datenbank: führt ausstehende
/// Migrationen aus und stellt die beiden Berechtigungsrollen sicher (idempotent).</summary>
internal static class StatusServerDatabaseInitializer
{
    /// <summary>Führt Migrationen aus und legt die Rollen <see cref="StatusRoles.Administration"/>
    /// und <see cref="StatusRoles.Ueberwacher"/> an, sofern sie fehlen.</summary>
    /// <param name="services">Der Service-Provider eines Request-/Start-Scopes.</param>
    /// <param name="cancellationToken">Abbruchtoken.</param>
    /// <returns>Eine abgeschlossene Aufgabe.</returns>
    public static async Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);

        var db = services.GetRequiredService<StatusServerDbContext>();
        await db.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);

        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in StatusRoles.All)
        {
            if (await roleManager.RoleExistsAsync(role).ConfigureAwait(false))
                continue;

            var result = await roleManager.CreateAsync(new IdentityRole(role)).ConfigureAwait(false);
            if (!result.Succeeded)
            {
                var details = string.Join("; ", result.Errors.Select(e => e.Description));
                throw new InvalidOperationException(
                    $"Die Berechtigungsstufe \"{role}\" konnte nicht angelegt werden: {details}");
            }
        }
    }
}
