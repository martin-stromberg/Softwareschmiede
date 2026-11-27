using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Softwareschmiede.StatusServer.Data;
using Softwareschmiede.StatusServer.Pages;
using Softwareschmiede.StatusServer.Security;

namespace Softwareschmiede.StatusServer.Endpoints;

/// <summary>Registriert die Authentifizierungs-Endpunkte:
/// Erst-Registrierung, Login/Logout, Profil mit Passwortänderung, Zugriffsverweigerung.</summary>
public static class AuthEndpoints
{
    /// <summary>Mappt die Authentifizierungs-Endpunkte.</summary>
    /// <param name="app">Der Endpoint-Route-Builder.</param>
    /// <returns>Der Builder für die Verkettung.</returns>
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet("/register", GetRegister).AllowAnonymous();
        app.MapPost("/register", PostRegister).AllowAnonymous().RequireAntiforgery();
        app.MapGet("/login", GetLogin).AllowAnonymous();
        app.MapPost("/login", PostLogin).AllowAnonymous().RequireAntiforgery();
        app.MapPost("/logout", PostLogout)
            .RequireAuthorization(StatusRoles.PageAccessPolicy)
            .RequireAntiforgery();
        app.MapGet("/profile", GetProfile).RequireAuthorization(StatusRoles.PageAccessPolicy);
        app.MapPost("/profile/password", PostProfilePassword)
            .RequireAuthorization(StatusRoles.PageAccessPolicy)
            .RequireAntiforgery();
        app.MapGet("/access-denied", GetAccessDenied).AllowAnonymous();

        return app;
    }

    private static async Task<IResult> GetRegister(
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        IAntiforgery antiforgery)
    {
        if (await userManager.Users.AnyAsync(httpContext.RequestAborted).ConfigureAwait(false))
            return Results.Redirect($"{PageShell.BasePath}/");

        var tokens = antiforgery.GetAndStoreTokens(httpContext);
        return Results.Content(
            AuthPagesComposer.RenderRegister(tokens.FormFieldName, tokens.RequestToken!),
            PageShell.HtmlContentType);
    }

    private static async Task<IResult> PostRegister(
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        UserAdminService userAdmin,
        IAntiforgery antiforgery)
    {
        if (await userManager.Users.AnyAsync(httpContext.RequestAborted).ConfigureAwait(false))
            return Results.Redirect($"{PageShell.BasePath}/");

        var form = await httpContext.Request.ReadFormAsync(httpContext.RequestAborted).ConfigureAwait(false);
        var userName = form["username"].ToString().Trim();
        var password = form["password"].ToString();
        var confirmPassword = form["confirmPassword"].ToString();

        string? error;
        if (userName.Length == 0)
            error = "Der Benutzername darf nicht leer sein.";
        else if (password.Length < PasswordPolicy.MinLength)
            error = $"Das Passwort muss mindestens {PasswordPolicy.MinLength} Zeichen lang sein.";
        else if (password != confirmPassword)
            error = "Die Passwörter stimmen nicht überein.";
        else
        {
            var result = await userAdmin
                .RegisterFirstAdminAsync(userName, password, httpContext.RequestAborted)
                .ConfigureAwait(false);
            if (result.Succeeded && result.User is not null)
            {
                await signInManager.SignInAsync(result.User, isPersistent: true).ConfigureAwait(false);
                return Results.Redirect($"{PageShell.BasePath}/");
            }

            error = result.Error;
        }

        var tokens = antiforgery.GetAndStoreTokens(httpContext);
        return Results.Content(
            AuthPagesComposer.RenderRegister(tokens.FormFieldName, tokens.RequestToken!, error),
            PageShell.HtmlContentType);
    }

    private static Task<IResult> GetLogin(HttpContext httpContext, IAntiforgery antiforgery)
    {
        if (httpContext.User.Identity?.IsAuthenticated == true)
            return Task.FromResult<IResult>(Results.Redirect($"{PageShell.BasePath}/"));

        var tokens = antiforgery.GetAndStoreTokens(httpContext);
        return Task.FromResult<IResult>(Results.Content(
            AuthPagesComposer.RenderLogin(tokens.FormFieldName, tokens.RequestToken!),
            PageShell.HtmlContentType));
    }

    private static async Task<IResult> PostLogin(
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IOptions<IdentityOptions> identityOptions,
        IAntiforgery antiforgery)
    {
        var form = await httpContext.Request.ReadFormAsync(httpContext.RequestAborted).ConfigureAwait(false);
        var userName = form["username"].ToString();
        var password = form["password"].ToString();

        string error;
        var signIn = await signInManager
            .PasswordSignInAsync(userName, password, isPersistent: true, lockoutOnFailure: true)
            .ConfigureAwait(false);
        if (signIn.Succeeded)
        {
            var user = await userManager.FindByNameAsync(userName).ConfigureAwait(false);
            if (user?.IsDeactivated == true)
            {
                await signInManager.SignOutAsync().ConfigureAwait(false);
                error = "Dieses Konto ist deaktiviert.";
            }
            else
            {
                return Results.Redirect($"{PageShell.BasePath}/");
            }
        }
        else if (signIn.IsLockedOut)
        {
            var duration = identityOptions.Value.Lockout.DefaultLockoutTimeSpan;
            error = $"Das Konto ist gesperrt. Die Sperrung endet automatisch nach {FormatLockoutDuration(duration)} " +
                "oder kann durch die Administration aufgehoben werden.";
        }
        else
        {
            error = "Benutzername oder Passwort ist falsch.";
        }

        var tokens = antiforgery.GetAndStoreTokens(httpContext);
        return Results.Content(
            AuthPagesComposer.RenderLogin(tokens.FormFieldName, tokens.RequestToken!, error),
            PageShell.HtmlContentType);
    }

    private static async Task<IResult> PostLogout(SignInManager<ApplicationUser> signInManager)
    {
        await signInManager.SignOutAsync().ConfigureAwait(false);
        return Results.Redirect($"{PageShell.BasePath}/login");
    }

    private static async Task<IResult> GetProfile(
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        IAntiforgery antiforgery,
        string? msg)
    {
        var user = await EndpointSecurityExtensions
            .BuildUserContextAsync(httpContext, userManager, antiforgery)
            .ConfigureAwait(false);
        return Results.Content(
            AuthPagesComposer.RenderProfile(user, StatusMessages.Resolve(msg)),
            PageShell.HtmlContentType);
    }

    private static async Task<IResult> PostProfilePassword(
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IAntiforgery antiforgery)
    {
        var user = await userManager.GetUserAsync(httpContext.User).ConfigureAwait(false);
        if (user is null)
            return Results.Redirect($"{PageShell.BasePath}/login");

        var form = await httpContext.Request.ReadFormAsync(httpContext.RequestAborted).ConfigureAwait(false);
        var currentPassword = form["currentPassword"].ToString();
        var newPassword = form["newPassword"].ToString();
        var confirmPassword = form["confirmPassword"].ToString();

        string message;
        var isError = false;
        if (newPassword.Length < PasswordPolicy.MinLength)
        {
            message = $"Das neue Passwort muss mindestens {PasswordPolicy.MinLength} Zeichen lang sein.";
            isError = true;
        }
        else if (newPassword != confirmPassword)
        {
            message = "Die neuen Passwörter stimmen nicht überein.";
            isError = true;
        }
        else
        {
            var result = await userManager
                .ChangePasswordAsync(user, currentPassword, newPassword)
                .ConfigureAwait(false);
            if (result.Succeeded)
            {
                await signInManager.RefreshSignInAsync(user).ConfigureAwait(false);
                // Post/Redirect/Get — F5 sendet die Passwortänderung nicht erneut.
                return Results.Redirect($"{PageShell.BasePath}/profile?msg={StatusMessages.PasswordChanged}");
            }
            else
            {
                message = UserAdminService.DescribeErrors(result);
                isError = true;
            }
        }

        var userContext = await EndpointSecurityExtensions
            .BuildUserContextAsync(httpContext, userManager, antiforgery)
            .ConfigureAwait(false);
        return Results.Content(AuthPagesComposer.RenderProfile(userContext, message, isError), PageShell.HtmlContentType);
    }

    private static async Task<IResult> GetAccessDenied(
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        IAntiforgery antiforgery)
    {
        PageUserContext? user = null;
        if (httpContext.User.Identity?.IsAuthenticated == true)
        {
            user = await EndpointSecurityExtensions
                .BuildUserContextAsync(httpContext, userManager, antiforgery)
                .ConfigureAwait(false);
        }

        return Results.Content(AuthPagesComposer.RenderAccessDenied(user), PageShell.HtmlContentType);
    }

    /// <summary>Formatiert die Sperrdauer (<c>IdentityOptions.Lockout.DefaultLockoutTimeSpan</c>)
    /// für die Nutzermeldung — volle Stunden als Stunden, sonst als Minuten.</summary>
    private static string FormatLockoutDuration(TimeSpan duration)
    {
        if (duration.TotalMinutes % 60 == 0)
            return duration.TotalHours == 1 ? "einer Stunde" : $"{(int)duration.TotalHours} Stunden";
        return duration.TotalMinutes == 1 ? "einer Minute" : $"{(int)duration.TotalMinutes} Minuten";
    }
}
