using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Softwareschmiede.StatusServer.Data;
using Softwareschmiede.StatusServer.Pages;
using Softwareschmiede.StatusServer.Security;

namespace Softwareschmiede.StatusServer.Endpoints;

/// <summary>Gemeinsame Sicherheits-Extension-Methoden für die Minimal-API-Endpunkte.</summary>
internal static class EndpointSecurityExtensions
{
    /// <summary>Erzeugt den <see cref="PageUserContext"/> des aktuell angemeldeten
    /// Benutzers (Name, Admin-Stufe, frisches Antiforgery-Token).</summary>
    /// <param name="httpContext">Der HTTP-Kontext.</param>
    /// <param name="userManager">Der Identity-User-Manager.</param>
    /// <param name="antiforgery">Der Antiforgery-Service.</param>
    /// <returns>Der Benutzerkontext für Composer und Navigation.</returns>
    internal static async Task<PageUserContext> BuildUserContextAsync(
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        IAntiforgery antiforgery)
    {
        var user = await userManager.GetUserAsync(httpContext.User).ConfigureAwait(false);
        var roles = user is not null
            ? await userManager.GetRolesAsync(user).ConfigureAwait(false)
            : [];
        var tokens = antiforgery.GetAndStoreTokens(httpContext);
        return new PageUserContext(
            user?.UserName,
            roles.Contains(StatusRoles.Administration),
            tokens.RequestToken!,
            tokens.FormFieldName);
    }

    /// <summary>Hängt einen Endpoint-Filter an, der das Antiforgery-Token der
    /// Anfrage validiert (CSRF-Schutz aller POST-Formularrouten). Bei Ablehnung wird
    /// eine verständliche Fehlerseite mit Rückweg statt einer nackten 400 geliefert.</summary>
    /// <param name="builder">Der Route-Handler-Builder.</param>
    /// <returns>Der Builder für die Verkettung.</returns>
    public static RouteHandlerBuilder RequireAntiforgery(this RouteHandlerBuilder builder)
        => builder.AddEndpointFilter(async (context, next) =>
        {
            var antiforgery = context.HttpContext.RequestServices.GetRequiredService<IAntiforgery>();
            try
            {
                await antiforgery.ValidateRequestAsync(context.HttpContext).ConfigureAwait(false);
            }
            catch (AntiforgeryValidationException)
            {
                return Results.Content(
                    AuthPagesComposer.RenderFormExpired(),
                    PageShell.HtmlContentType,
                    statusCode: StatusCodes.Status400BadRequest);
            }

            return await next(context).ConfigureAwait(false);
        });
}
