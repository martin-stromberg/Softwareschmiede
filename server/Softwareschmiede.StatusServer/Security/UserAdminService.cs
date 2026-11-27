using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Softwareschmiede.StatusServer.Data;

namespace Softwareschmiede.StatusServer.Security;

/// <summary>Transaction-Script für die Benutzerverwaltung des Status-Servers:
/// Erst-Registrierung, Anlegen, Stufenänderung, Deaktivieren/Aktivieren, Löschen,
/// Sperrung zurücksetzen — inklusive Schutz des letzten aktiven Administrators.</summary>
public sealed class UserAdminService
{
    /// <summary>Schlüssel der Setup-Markierung in der <c>ServerSettings</c>-Tabelle.
    /// Der primärschlüssel-geschützte Eintrag wird in derselben Transaktion wie der
    /// Erst-Admin geschrieben — ein zweiter, paralleler Setup-Versuch scheitert an
    /// der Constraint-Verletzung (atomarer TOCTOU-Guard).</summary>
    public const string SetupCompletedKey = "SetupCompleted";

    /// <summary>Prozessweiter Schalter, der die Letzter-Admin-Prüfung
    /// (<see cref="IsLastActiveAdminAsync"/>) und die zugehörige Mutation serialisiert.
    /// Der Status-Server läuft single-instanzig — ein In-Process-Lock genügt, um den
    /// Check-then-act-TOCTOU zweier paralleler Admin-Requests auszuschließen.</summary>
    private static readonly SemaphoreSlim s_lastAdminLock = new(1, 1);

    /// <summary>Meldung für den Verlierer eines <c>ConcurrencyStamp</c>-Konflikts:
    /// Zwei parallele Mutationen am selben Benutzer (z. B. Löschen in zwei
    /// Admin-Tabs oder Freischalten parallel zum Deaktivieren) — der erste
    /// Schreibvorgang gewinnt, der zweite landet auf einer
    /// <see cref="DbUpdateConcurrencyException"/>, die als kontrollierter
    /// Fehlschlag statt HTTP 500 zurückgegeben wird. Greift doppelt: Der
    /// Fehlercode <c>ConcurrencyFailure</c>, in den Identity die Exception an
    /// den meisten Pfaden bereits wrappt, wird in <see cref="DescribeErrors"/>
    /// auf diese Meldung übersetzt; entkommt die Exception dennoch (z. B. über
    /// künftige Pfade), fangen die Mutationsmethoden sie direkt ab.</summary>
    private const string ConcurrencyConflictError =
        "Der Benutzer wurde zwischenzeitlich geändert — bitte erneut versuchen.";

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly StatusServerDbContext _db;
    private readonly TimeProvider _timeProvider;

    /// <summary>Erstellt den Service.</summary>
    /// <param name="userManager">Der Identity-User-Manager.</param>
    /// <param name="db">Der Datenbankkontext (geteilt mit dem Identity-UserStore).</param>
    /// <param name="timeProvider">Die Zeitquelle.</param>
    public UserAdminService(
        UserManager<ApplicationUser> userManager,
        StatusServerDbContext db,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(userManager);
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _userManager = userManager;
        _db = db;
        _timeProvider = timeProvider;
    }

    /// <summary>Legt den allerersten Benutzer mit Stufe <see cref="StatusRoles.Administration"/> an.
    /// Schlägt fehl, sobald bereits ein Benutzer existiert (Setup-Guard). Benutzer,
    /// Rollenzuweisung und die <see cref="SetupCompletedKey"/>-Markierung werden in
    /// einer Transaktion geschrieben: Zwei parallele Registrierungen können daher
    /// nicht beide erfolgreich sein, und ein Fehlschlag hinterlässt kein Konto.</summary>
    /// <param name="userName">Der Benutzername.</param>
    /// <param name="password">Das Passwort.</param>
    /// <param name="cancellationToken">Abbruchtoken.</param>
    /// <returns>Das Ergebnis inkl. des erzeugten Benutzers bei Erfolg.</returns>
    public async Task<UserAdminResult> RegisterFirstAdminAsync(
        string userName, string password, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userName);
        ArgumentNullException.ThrowIfNull(password);

        // UX-Vorfilter — die eigentliche Sicherung ist die PK-Markierung in der Transaktion.
        if (await _userManager.Users.AnyAsync(cancellationToken).ConfigureAwait(false))
            return UserAdminResult.Failed("Es existiert bereits ein Benutzerkonto.");

        await using var transaction = await _db.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);
        try
        {
            _db.ServerSettings.Add(new ServerSetting { Key = SetupCompletedKey, Value = "1" });
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException)
        {
            // Ein paralleler Setup-Lauf hat die Markierung bereits geschrieben.
            return UserAdminResult.Failed("Es existiert bereits ein Benutzerkonto.");
        }

        var user = new ApplicationUser { UserName = userName };
        var created = await _userManager.CreateAsync(user, password).ConfigureAwait(false);
        if (!created.Succeeded)
            return UserAdminResult.Failed(DescribeErrors(created));

        var roleAdded = await _userManager.AddToRoleAsync(user, StatusRoles.Administration).ConfigureAwait(false);
        if (!roleAdded.Succeeded)
            return UserAdminResult.Failed(DescribeErrors(roleAdded));

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return UserAdminResult.Ok(user);
    }

    /// <summary>Legt einen neuen Benutzer mit der angegebenen Berechtigungsstufe an (Admin-Aktion).</summary>
    /// <param name="userName">Der Benutzername.</param>
    /// <param name="password">Das Passwort.</param>
    /// <param name="role">Die Stufe (<see cref="StatusRoles.Administration"/> oder <see cref="StatusRoles.Ueberwacher"/>).</param>
    /// <param name="cancellationToken">Abbruchtoken.</param>
    /// <returns>Das Ergebnis.</returns>
    public async Task<UserAdminResult> CreateUserAsync(
        string userName, string password, string role, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userName);
        ArgumentNullException.ThrowIfNull(password);
        if (!StatusRoles.All.Contains(role))
            return UserAdminResult.Failed("Unbekannte Berechtigungsstufe.");

        await using var transaction = await _db.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        var user = new ApplicationUser { UserName = userName };
        var created = await _userManager.CreateAsync(user, password).ConfigureAwait(false);
        if (!created.Succeeded)
            return UserAdminResult.Failed(DescribeErrors(created));

        var roleAdded = await _userManager.AddToRoleAsync(user, role).ConfigureAwait(false);
        if (!roleAdded.Succeeded)
            return UserAdminResult.Failed(DescribeErrors(roleAdded));

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return UserAdminResult.Ok(user);
    }

    /// <summary>Ändert die Berechtigungsstufe eines Benutzers. Aktualisiert den
    /// Security-Stamp, damit die neue Stufe zeitnah auf bestehende Sessions wirkt.</summary>
    /// <param name="userId">Die Id des Benutzers.</param>
    /// <param name="role">Die Zielstufe (<see cref="StatusRoles.Administration"/> oder <see cref="StatusRoles.Ueberwacher"/>).</param>
    /// <param name="cancellationToken">Abbruchtoken.</param>
    /// <returns>Das Ergebnis.</returns>
    public async Task<UserAdminResult> SetRoleAsync(
        string userId, string role, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);
        if (!StatusRoles.All.Contains(role))
            return UserAdminResult.Failed("Unbekannte Berechtigungsstufe.");

        var user = await _userManager.FindByIdAsync(userId).ConfigureAwait(false);
        if (user is null)
            return UserAdminResult.NotFound("Der Benutzer wurde nicht gefunden.");

        var currentRoles = await _userManager.GetRolesAsync(user).ConfigureAwait(false);
        if (currentRoles.Count == 1 && currentRoles[0] == role)
            return UserAdminResult.Failed("Der Benutzer hat diese Stufe bereits.");

        // Herabstufung kann den letzten aktiven Admin betreffen — Prüfung und
        // Mutation unter dem prozessweiten Lock serialisieren (Check-then-act).
        var needsAdminGuard = role != StatusRoles.Administration;
        if (needsAdminGuard)
            await s_lastAdminLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (needsAdminGuard && await IsLastActiveAdminAsync(user).ConfigureAwait(false))
                return UserAdminResult.Failed("Der letzte aktive Administrator kann nicht herabgestuft werden.");

            // Entfernen + Hinzufügen atomar — sonst bliebe bei einem Fehlschlag ein
            // Benutzer ohne Stufe zurück.
            await using var transaction = await _db.Database
                .BeginTransactionAsync(cancellationToken)
                .ConfigureAwait(false);

            var removed = await _userManager.RemoveFromRolesAsync(user, currentRoles).ConfigureAwait(false);
            if (!removed.Succeeded)
                return UserAdminResult.Failed(DescribeErrors(removed));

            var added = await _userManager.AddToRoleAsync(user, role).ConfigureAwait(false);
            if (!added.Succeeded)
                return UserAdminResult.Failed(DescribeErrors(added));

            var stamped = await _userManager.UpdateSecurityStampAsync(user).ConfigureAwait(false);
            if (!stamped.Succeeded)
                return UserAdminResult.Failed(DescribeErrors(stamped));

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return UserAdminResult.Ok(user);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Verlierer eines parallelen Schreibzugriffs auf denselben Benutzer.
            return UserAdminResult.Failed(ConcurrencyConflictError);
        }
        finally
        {
            if (needsAdminGuard)
                s_lastAdminLock.Release();
        }
    }

    /// <summary>Aktiviert oder deaktiviert ein Konto. Beim Deaktivieren wird der
    /// Security-Stamp aktualisiert, damit bestehende Sessions sofort sterben.</summary>
    /// <param name="userId">Die Id des Benutzers.</param>
    /// <param name="deactivated"><see langword="true"/> zum Deaktivieren, <see langword="false"/> zum Aktivieren.</param>
    /// <param name="cancellationToken">Abbruchtoken.</param>
    /// <returns>Das Ergebnis.</returns>
    public async Task<UserAdminResult> SetDeactivatedAsync(
        string userId, bool deactivated, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);
        var user = await _userManager.FindByIdAsync(userId).ConfigureAwait(false);
        if (user is null)
            return UserAdminResult.NotFound("Der Benutzer wurde nicht gefunden.");

        // Deaktivieren kann den letzten aktiven Admin betreffen — Prüfung und
        // Mutation unter dem prozessweiten Lock serialisieren (Check-then-act).
        var needsAdminGuard = deactivated && !user.IsDeactivated;
        if (needsAdminGuard)
            await s_lastAdminLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (needsAdminGuard && await IsLastActiveAdminAsync(user).ConfigureAwait(false))
                return UserAdminResult.Failed("Der letzte aktive Administrator kann nicht deaktiviert werden.");

            user.IsDeactivated = deactivated;
            var updated = await _userManager.UpdateAsync(user).ConfigureAwait(false);
            if (!updated.Succeeded)
                return UserAdminResult.Failed(DescribeErrors(updated));

            if (deactivated)
            {
                var stamped = await _userManager.UpdateSecurityStampAsync(user).ConfigureAwait(false);
                if (!stamped.Succeeded)
                    return UserAdminResult.Failed(DescribeErrors(stamped));
            }

            return UserAdminResult.Ok(user);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Verlierer eines parallelen Schreibzugriffs auf denselben Benutzer.
            return UserAdminResult.Failed(ConcurrencyConflictError);
        }
        finally
        {
            if (needsAdminGuard)
                s_lastAdminLock.Release();
        }
    }

    /// <summary>Löscht ein Benutzerkonto.</summary>
    /// <param name="userId">Die Id des Benutzers.</param>
    /// <param name="cancellationToken">Abbruchtoken.</param>
    /// <returns>Das Ergebnis.</returns>
    public async Task<UserAdminResult> DeleteUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);
        var user = await _userManager.FindByIdAsync(userId).ConfigureAwait(false);
        if (user is null)
            return UserAdminResult.NotFound("Der Benutzer wurde nicht gefunden.");

        // Löschen kann den letzten aktiven Admin betreffen — Prüfung und
        // Mutation unter dem prozessweiten Lock serialisieren (Check-then-act).
        await s_lastAdminLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (await IsLastActiveAdminAsync(user).ConfigureAwait(false))
                return UserAdminResult.Failed("Der letzte aktive Administrator kann nicht gelöscht werden.");

            var deleted = await _userManager.DeleteAsync(user).ConfigureAwait(false);
            if (!deleted.Succeeded)
                return UserAdminResult.Failed(DescribeErrors(deleted));

            return UserAdminResult.Ok(user);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Verlierer eines parallelen Schreibzugriffs auf denselben Benutzer.
            return UserAdminResult.Failed(ConcurrencyConflictError);
        }
        finally
        {
            s_lastAdminLock.Release();
        }
    }

    /// <summary>Setzt eine Sperrung (Lockout) zurück: Lockout-Ende aufheben und
    /// Fehlversuchszähler auf 0.</summary>
    /// <param name="userId">Die Id des Benutzers.</param>
    /// <param name="cancellationToken">Abbruchtoken.</param>
    /// <returns>Das Ergebnis.</returns>
    public async Task<UserAdminResult> ResetLockoutAsync(string userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);
        var user = await _userManager.FindByIdAsync(userId).ConfigureAwait(false);
        if (user is null)
            return UserAdminResult.NotFound("Der Benutzer wurde nicht gefunden.");

        try
        {
            var unlocked = await _userManager.SetLockoutEndDateAsync(user, null).ConfigureAwait(false);
            if (!unlocked.Succeeded)
                return UserAdminResult.Failed(DescribeErrors(unlocked));

            var reset = await _userManager.ResetAccessFailedCountAsync(user).ConfigureAwait(false);
            if (!reset.Succeeded)
                return UserAdminResult.Failed(DescribeErrors(reset));

            return UserAdminResult.Ok(user);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Verlierer eines parallelen Schreibzugriffs auf denselben Benutzer.
            return UserAdminResult.Failed(ConcurrencyConflictError);
        }
    }

    /// <summary>Listet alle Benutzer mit Stufe und Status für die Verwaltungsseite.</summary>
    /// <param name="cancellationToken">Abbruchtoken.</param>
    /// <returns>Die Benutzerliste (alphabetisch sortiert).</returns>
    public async Task<IReadOnlyList<UserAdminListItem>> ListUsersAsync(CancellationToken cancellationToken = default)
    {
        var users = await _userManager.Users
            .OrderBy(u => u.NormalizedUserName)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var activeAdminCount = (await _userManager.GetUsersInRoleAsync(StatusRoles.Administration).ConfigureAwait(false))
            .Count(u => !u.IsDeactivated);
        var now = _timeProvider.GetUtcNow();

        var items = new List<UserAdminListItem>(users.Count);
        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user).ConfigureAwait(false);
            var role = StatusRoles.All.FirstOrDefault(roles.Contains) ?? roles.FirstOrDefault() ?? "–";
            var isActiveAdmin = role == StatusRoles.Administration && !user.IsDeactivated;
            items.Add(new UserAdminListItem(
                user.Id,
                user.UserName ?? string.Empty,
                role,
                user.IsDeactivated,
                user.LockoutEnd,
                user.LockoutEnd is { } end && end > now,
                isActiveAdmin && activeAdminCount == 1));
        }

        return items;
    }

    /// <summary>Prüft, ob <paramref name="user"/> der einzige aktive Benutzer mit
    /// Rolle <see cref="StatusRoles.Administration"/> ist.</summary>
    private async Task<bool> IsLastActiveAdminAsync(ApplicationUser user)
    {
        var admins = await _userManager.GetUsersInRoleAsync(StatusRoles.Administration).ConfigureAwait(false);
        var activeAdmins = admins.Where(u => !u.IsDeactivated).ToList();
        return activeAdmins.Count == 1 && activeAdmins[0].Id == user.Id;
    }

    /// <summary>Übersetzt <see cref="IdentityResult.Errors"/> in eine verständliche deutsche Meldung.</summary>
    /// <param name="result">Das Identity-Ergebnis.</param>
    /// <returns>Die übersetzte, zusammengefügte Fehlermeldung.</returns>
    internal static string DescribeErrors(IdentityResult result)
    {
        var translated = result.Errors
            .Select(e => e.Code switch
            {
                "DuplicateUserName" => "Der Benutzername ist bereits vergeben.",
                "InvalidUserName" => "Der Benutzername enthält unzulässige Zeichen " +
                    $"— erlaubt sind {UserNamePolicy.AllowedCharactersDescription} (keine Umlaute, keine Leerzeichen).",
                "PasswordTooShort" => $"Das Passwort ist zu kurz (mindestens {PasswordPolicy.MinLength} Zeichen).",
                "PasswordRequiresNonAlphanumeric" => "Das Passwort benötigt mindestens ein Sonderzeichen.",
                "PasswordRequiresDigit" => "Das Passwort benötigt mindestens eine Ziffer.",
                "PasswordRequiresLower" => "Das Passwort benötigt mindestens einen Kleinbuchstaben.",
                "PasswordRequiresUpper" => "Das Passwort benötigt mindestens einen Großbuchstaben.",
                "PasswordMismatch" => "Das aktuelle Passwort ist falsch.",
                "ConcurrencyFailure" => ConcurrencyConflictError,
                _ => e.Description,
            });
        return string.Join(" ", translated);
    }
}

/// <summary>Ergebnis einer Verwaltungsoperation von <see cref="UserAdminService"/>.</summary>
/// <param name="Succeeded">Ob die Operation erfolgreich war.</param>
/// <param name="Error">Der Ablehnungs-/Fehlergrund bei <c>Succeeded == false</c>.</param>
/// <param name="User">Der betroffene Benutzer bei Erfolg (falls relevant, sonst <see langword="null"/>).</param>
/// <param name="IsNotFound">Ob der Fehlschlag ein „Zielobjekt nicht gefunden" ist —
/// die Endpunkte leiten diesen Fall per <c>?err=</c>-Redirect auf die Liste um
/// (Post/Redirect/Get), statt die Seite direkt mit Fehlermeldung zu rendern.</param>
public sealed record UserAdminResult(
    bool Succeeded,
    string? Error,
    ApplicationUser? User = null,
    bool IsNotFound = false)
{
    /// <summary>Erfolgsergebnis.</summary>
    /// <param name="user">Der betroffene Benutzer (optional).</param>
    /// <returns>Das Ergebnis.</returns>
    public static UserAdminResult Ok(ApplicationUser? user = null) => new(true, null, user);

    /// <summary>Fehlerergebnis mit verständlichem Grund.</summary>
    /// <param name="error">Der Grund.</param>
    /// <returns>Das Ergebnis.</returns>
    public static UserAdminResult Failed(string error) => new(false, error);

    /// <summary>Fehlerergebnis „Zielobjekt nicht gefunden" (<see cref="IsNotFound"/>).</summary>
    /// <param name="error">Der Grund.</param>
    /// <returns>Das Ergebnis.</returns>
    public static UserAdminResult NotFound(string error) => new(false, error, IsNotFound: true);
}

/// <summary>Listeneintrag der Benutzerverwaltung (Sicht eines Benutzers für die Admin-Seite).</summary>
/// <param name="Id">Die Benutzer-Id.</param>
/// <param name="UserName">Der Benutzername.</param>
/// <param name="Role">Die Berechtigungsstufe.</param>
/// <param name="IsDeactivated">Ob das Konto deaktiviert ist.</param>
/// <param name="LockoutEnd">Das Ende der Sperrung (UTC), falls gesperrt.</param>
/// <param name="IsLockedOut">Ob die Sperrung aktuell wirkt (LockoutEnd in der Zukunft).</param>
/// <param name="IsLastActiveAdmin">Ob dies der einzige aktive Administrator ist (Schutz-Flag für die UI).</param>
public sealed record UserAdminListItem(
    string Id,
    string UserName,
    string Role,
    bool IsDeactivated,
    DateTimeOffset? LockoutEnd,
    bool IsLockedOut,
    bool IsLastActiveAdmin);
