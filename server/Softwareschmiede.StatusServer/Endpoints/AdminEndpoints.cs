using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Softwareschmiede.StatusServer.Data;
using Softwareschmiede.StatusServer.Pages;
using Softwareschmiede.StatusServer.Security;

namespace Softwareschmiede.StatusServer.Endpoints;

/// <summary>Registriert die Admin-Verwaltungs-Endpunkte (Policy <c>AdminOnly</c>):
/// Benutzerverwaltung, Relay-Tokens und Server-Einstellungen.
/// Alle POST-Handler folgen dem Post/Redirect/Get-Muster: Bei Erfolg wird auf die
/// jeweilige GET-Seite umgeleitet und die Meldung als fester <c>?msg=</c>-Schlüssel
/// übergeben (ein Browser-Refresh sendet dann kein Formular erneut). „Nicht
/// gefunden"-Fehlschläge — das Zielobjekt der Mutation existiert ohnehin nicht
/// mehr — werden ebenfalls per Redirect als <c>?err=</c>-Schlüssel transportiert;
/// übrige fachliche Fehlschläge rendern die Seite direkt mit der Fehlermeldung,
/// damit der Fehler im Kontext erscheint (bewusster Tradeoff: F5 auf einer
/// Fehlerseite sendet die Anfrage erneut, erzeugt aber kein Duplikat).
/// Beide Query-Parameter werden über <see cref="StatusMessages"/> auf feste
/// deutsche Texte aufgelöst — ein präparierter Link kann keinen Freitext als
/// Statusmeldung rendern.
/// Ausnahme/Sonderfall Token-Anlage: Der Klartext wird über den einmaligen
/// <see cref="RelayTokenPlaintextStore"/> an <c>GET /admin/tokens?created=…</c>
/// übergeben — so entsteht auch hier bei einem Refresh kein zweites Token.</summary>
public static class AdminEndpoints
{
    /// <summary>Mappt die Admin-Endpunkte.</summary>
    /// <param name="app">Der Endpoint-Route-Builder.</param>
    /// <returns>Der Builder für die Verkettung.</returns>
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet("/admin/users", GetUsers).RequireAuthorization(StatusRoles.AdminOnlyPolicy);
        app.MapPost("/admin/users", PostUser).RequireAuthorization(StatusRoles.AdminOnlyPolicy).RequireAntiforgery();
        app.MapPost("/admin/users/{id}/role", PostUserRole).RequireAuthorization(StatusRoles.AdminOnlyPolicy).RequireAntiforgery();
        app.MapPost("/admin/users/{id}/deactivate", PostUserDeactivate).RequireAuthorization(StatusRoles.AdminOnlyPolicy).RequireAntiforgery();
        app.MapPost("/admin/users/{id}/activate", PostUserActivate).RequireAuthorization(StatusRoles.AdminOnlyPolicy).RequireAntiforgery();
        app.MapGet("/admin/users/{id}/delete", GetUserDeleteConfirm).RequireAuthorization(StatusRoles.AdminOnlyPolicy);
        app.MapPost("/admin/users/{id}/delete", PostUserDelete).RequireAuthorization(StatusRoles.AdminOnlyPolicy).RequireAntiforgery();
        app.MapPost("/admin/users/{id}/unlock", PostUserUnlock).RequireAuthorization(StatusRoles.AdminOnlyPolicy).RequireAntiforgery();

        app.MapGet("/admin/tokens", GetTokens).RequireAuthorization(StatusRoles.AdminOnlyPolicy);
        app.MapPost("/admin/tokens", PostToken).RequireAuthorization(StatusRoles.AdminOnlyPolicy).RequireAntiforgery();
        app.MapGet("/admin/tokens/{id:long}/revoke", GetTokenRevokeConfirm).RequireAuthorization(StatusRoles.AdminOnlyPolicy);
        app.MapPost("/admin/tokens/{id:long}/revoke", PostTokenRevoke).RequireAuthorization(StatusRoles.AdminOnlyPolicy).RequireAntiforgery();

        app.MapGet("/admin/settings", GetSettings).RequireAuthorization(StatusRoles.AdminOnlyPolicy);
        app.MapPost("/admin/settings/session-duration", PostSessionDuration).RequireAuthorization(StatusRoles.AdminOnlyPolicy).RequireAntiforgery();

        return app;
    }

    private static async Task<IResult> GetUsers(
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        UserAdminService userAdmin,
        IAntiforgery antiforgery,
        string? msg,
        string? err)
    {
        var (message, isError) = StatusMessages.Resolve(msg, err);
        return await RenderUsersAsync(httpContext, userManager, userAdmin, antiforgery, message, isError).ConfigureAwait(false);
    }

    private static async Task<IResult> PostUser(
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        UserAdminService userAdmin,
        IAntiforgery antiforgery)
    {
        var form = await httpContext.Request.ReadFormAsync(httpContext.RequestAborted).ConfigureAwait(false);
        var userName = form["username"].ToString().Trim();
        var password = form["password"].ToString();
        var role = form["role"].ToString();

        if (userName.Length == 0)
            return await RenderUsersAsync(httpContext, userManager, userAdmin, antiforgery, "Der Benutzername darf nicht leer sein.", isError: true).ConfigureAwait(false);
        if (password.Length < PasswordPolicy.MinLength)
            return await RenderUsersAsync(httpContext, userManager, userAdmin, antiforgery, $"Das Passwort muss mindestens {PasswordPolicy.MinLength} Zeichen lang sein.", isError: true).ConfigureAwait(false);
        if (!StatusRoles.All.Contains(role))
            return await RenderUsersAsync(httpContext, userManager, userAdmin, antiforgery, "Unbekannte Berechtigungsstufe.", isError: true).ConfigureAwait(false);

        var result = await userAdmin.CreateUserAsync(userName, password, role, httpContext.RequestAborted).ConfigureAwait(false);
        return result.Succeeded
            ? RedirectWithMessage("/admin/users", StatusMessages.UserCreated)
            : await RenderUsersAsync(httpContext, userManager, userAdmin, antiforgery, result.Error, isError: true).ConfigureAwait(false);
    }

    private static async Task<IResult> PostUserRole(
        string id,
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        UserAdminService userAdmin,
        IAntiforgery antiforgery)
    {
        var form = await httpContext.Request.ReadFormAsync(httpContext.RequestAborted).ConfigureAwait(false);
        var role = form["role"].ToString();
        if (!StatusRoles.All.Contains(role))
            return await RenderUsersAsync(httpContext, userManager, userAdmin, antiforgery, "Unbekannte Berechtigungsstufe.", isError: true).ConfigureAwait(false);

        var result = await userAdmin.SetRoleAsync(id, role, httpContext.RequestAborted).ConfigureAwait(false);
        if (result.Succeeded)
            return RedirectWithMessage("/admin/users", StatusMessages.RoleChanged);
        return result.IsNotFound
            ? RedirectWithError("/admin/users", StatusMessages.UserNotFound)
            : await RenderUsersAsync(httpContext, userManager, userAdmin, antiforgery, result.Error, isError: true).ConfigureAwait(false);
    }

    private static async Task<IResult> PostUserDeactivate(
        string id,
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        UserAdminService userAdmin,
        IAntiforgery antiforgery)
        => await RunUserActionAsync(httpContext, userManager, userAdmin, antiforgery,
            userAdmin.SetDeactivatedAsync(id, deactivated: true, httpContext.RequestAborted),
            StatusMessages.UserDeactivated).ConfigureAwait(false);

    private static async Task<IResult> PostUserActivate(
        string id,
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        UserAdminService userAdmin,
        IAntiforgery antiforgery)
        => await RunUserActionAsync(httpContext, userManager, userAdmin, antiforgery,
            userAdmin.SetDeactivatedAsync(id, deactivated: false, httpContext.RequestAborted),
            StatusMessages.UserActivated).ConfigureAwait(false);

    private static async Task<IResult> GetUserDeleteConfirm(
        string id,
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        UserAdminService userAdmin,
        IAntiforgery antiforgery)
    {
        var target = await userManager.FindByIdAsync(id).ConfigureAwait(false);
        if (target is null)
        {
            return await RenderUsersAsync(httpContext, userManager, userAdmin, antiforgery,
                "Der Benutzer wurde nicht gefunden.", isError: true).ConfigureAwait(false);
        }

        var user = await EndpointSecurityExtensions
            .BuildUserContextAsync(httpContext, userManager, antiforgery)
            .ConfigureAwait(false);
        return Results.Content(
            AdminPagesComposer.RenderUserDeleteConfirm(user, id, target.UserName ?? string.Empty),
            PageShell.HtmlContentType);
    }

    private static async Task<IResult> PostUserDelete(
        string id,
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        UserAdminService userAdmin,
        IAntiforgery antiforgery)
        => await RunUserActionAsync(httpContext, userManager, userAdmin, antiforgery,
            userAdmin.DeleteUserAsync(id, httpContext.RequestAborted),
            StatusMessages.UserDeleted).ConfigureAwait(false);

    private static async Task<IResult> PostUserUnlock(
        string id,
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        UserAdminService userAdmin,
        IAntiforgery antiforgery)
        => await RunUserActionAsync(httpContext, userManager, userAdmin, antiforgery,
            userAdmin.ResetLockoutAsync(id, httpContext.RequestAborted),
            StatusMessages.LockoutReset).ConfigureAwait(false);

    private static async Task<IResult> GetTokens(
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        RelayTokenService tokenService,
        RelayTokenPlaintextStore plaintextStore,
        IAntiforgery antiforgery,
        string? msg,
        string? err,
        Guid? created)
    {
        // Einmal-Anzeige: Der Klartext eines frisch angelegten Tokens liegt nur im
        // Single-Use-Store — Take() liefert ihn genau einmal; ein Refresh des
        // ?created=-Links zeigt danach einen Hinweis statt des Klartexts.
        string? plaintext = null;
        var (message, isError) = StatusMessages.Resolve(msg, err);
        if (created is { } key)
        {
            plaintext = plaintextStore.Take(key);
            if (plaintext is null && message is null)
            {
                message = "Der Klartext des Tokens wurde bereits angezeigt oder ist nicht mehr abrufbar " +
                    "— das Token selbst ist angelegt und in der Liste sichtbar.";
            }
        }

        return await RenderTokensAsync(httpContext, userManager, tokenService, antiforgery,
            message, isError, createdToken: plaintext).ConfigureAwait(false);
    }

    private static async Task<IResult> PostToken(
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        RelayTokenService tokenService,
        RelayTokenPlaintextStore plaintextStore,
        IAntiforgery antiforgery)
    {
        var form = await httpContext.Request.ReadFormAsync(httpContext.RequestAborted).ConfigureAwait(false);
        var label = form["label"].ToString().Trim();
        if (label.Length is 0 or > RelayTokenService.MaxLabelLength)
        {
            return await RenderTokensAsync(httpContext, userManager, tokenService, antiforgery,
                $"Das Label muss zwischen 1 und {RelayTokenService.MaxLabelLength} Zeichen lang sein.",
                isError: true).ConfigureAwait(false);
        }

        var created = await tokenService.CreateAsync(label, httpContext.RequestAborted).ConfigureAwait(false);
        var key = plaintextStore.Store(created.Plaintext);
        return Results.Redirect($"/admin/tokens?created={key}");
    }

    private static async Task<IResult> GetTokenRevokeConfirm(
        long id,
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        RelayTokenService tokenService,
        IAntiforgery antiforgery)
    {
        var token = await tokenService.FindAsync(id, httpContext.RequestAborted).ConfigureAwait(false);
        if (token is null || token.RevokedUtc is not null)
        {
            return await RenderTokensAsync(httpContext, userManager, tokenService, antiforgery,
                token is null ? "Das Token wurde nicht gefunden." : "Das Token ist bereits widerrufen.",
                isError: true).ConfigureAwait(false);
        }

        var user = await EndpointSecurityExtensions
            .BuildUserContextAsync(httpContext, userManager, antiforgery)
            .ConfigureAwait(false);
        return Results.Content(
            AdminPagesComposer.RenderTokenRevokeConfirm(user, token),
            PageShell.HtmlContentType);
    }

    private static async Task<IResult> PostTokenRevoke(
        long id,
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        RelayTokenService tokenService,
        IAntiforgery antiforgery)
    {
        var revoked = await tokenService.RevokeAsync(id, httpContext.RequestAborted).ConfigureAwait(false);
        return revoked
            ? RedirectWithMessage("/admin/tokens", StatusMessages.TokenRevoked)
            : RedirectWithError("/admin/tokens", StatusMessages.TokenNotFound);
    }

    private static async Task<IResult> GetSettings(
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        ServerSettingsService settings,
        IAntiforgery antiforgery,
        string? msg)
        => await RenderSettingsAsync(httpContext, userManager, settings, antiforgery,
            StatusMessages.Resolve(msg)).ConfigureAwait(false);

    private static async Task<IResult> PostSessionDuration(
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        ServerSettingsService settings,
        IAntiforgery antiforgery)
    {
        var form = await httpContext.Request.ReadFormAsync(httpContext.RequestAborted).ConfigureAwait(false);
        if (!int.TryParse(form["minutes"], out var minutes)
            || minutes is < ServerSettingsService.MinSessionDurationMinutes
                or > ServerSettingsService.MaxSessionDurationMinutes)
        {
            return await RenderSettingsAsync(httpContext, userManager, settings, antiforgery,
                $"Die Sitzungsdauer muss eine ganze Zahl zwischen {ServerSettingsService.MinSessionDurationMinutes} " +
                $"und {ServerSettingsService.MaxSessionDurationMinutes} Minuten sein.", isError: true).ConfigureAwait(false);
        }

        await settings.SetSessionDurationAsync(minutes, httpContext.RequestAborted).ConfigureAwait(false);
        return RedirectWithMessage("/admin/settings", StatusMessages.SettingsSaved);
    }

    /// <summary>Baut die Post/Redirect/Get-Umleitung auf eine GET-Seite mit der
    /// Statusmeldung als festem <c>msg</c>-Schlüssel (die Zielseite löst ihn über
    /// <see cref="StatusMessages"/> auf und rendert den Text HTML-escaped).</summary>
    private static IResult RedirectWithMessage(string path, string messageKey)
        => Results.Redirect($"{path}?msg={messageKey}");

    /// <summary>Baut die Post/Redirect/Get-Umleitung auf eine GET-Seite mit der
    /// Fehlermeldung als festem <c>err</c>-Schlüssel.</summary>
    private static IResult RedirectWithError(string path, string errorKey)
        => Results.Redirect($"{path}?err={errorKey}");

    private static async Task<IResult> RunUserActionAsync(
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        UserAdminService userAdmin,
        IAntiforgery antiforgery,
        Task<UserAdminResult> action,
        string successKey)
    {
        var result = await action.ConfigureAwait(false);
        if (result.Succeeded)
            return RedirectWithMessage("/admin/users", successKey);
        return result.IsNotFound
            ? RedirectWithError("/admin/users", StatusMessages.UserNotFound)
            : await RenderUsersAsync(httpContext, userManager, userAdmin, antiforgery,
                result.Error, isError: true).ConfigureAwait(false);
    }

    private static async Task<IResult> RenderUsersAsync(
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        UserAdminService userAdmin,
        IAntiforgery antiforgery,
        string? message = null,
        bool isError = false)
    {
        var user = await EndpointSecurityExtensions
            .BuildUserContextAsync(httpContext, userManager, antiforgery)
            .ConfigureAwait(false);
        var users = await userAdmin.ListUsersAsync(httpContext.RequestAborted).ConfigureAwait(false);
        return Results.Content(AdminPagesComposer.RenderUserList(user, users, message, isError), PageShell.HtmlContentType);
    }

    private static async Task<IResult> RenderTokensAsync(
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        RelayTokenService tokenService,
        IAntiforgery antiforgery,
        string? message = null,
        bool isError = false,
        string? createdToken = null)
    {
        var user = await EndpointSecurityExtensions
            .BuildUserContextAsync(httpContext, userManager, antiforgery)
            .ConfigureAwait(false);
        var tokens = await tokenService.ListAsync(httpContext.RequestAborted).ConfigureAwait(false);
        return Results.Content(
            AdminPagesComposer.RenderTokenList(user, tokens, message, isError, createdToken),
            PageShell.HtmlContentType);
    }

    private static async Task<IResult> RenderSettingsAsync(
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        ServerSettingsService settings,
        IAntiforgery antiforgery,
        string? message = null,
        bool isError = false)
    {
        var user = await EndpointSecurityExtensions
            .BuildUserContextAsync(httpContext, userManager, antiforgery)
            .ConfigureAwait(false);
        var minutes = await settings.GetSessionDurationAsync(httpContext.RequestAborted).ConfigureAwait(false);
        return Results.Content(AdminPagesComposer.RenderSettings(user, minutes, message, isError), PageShell.HtmlContentType);
    }
}
