using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Softwareschmiede.StatusServer.Data;

namespace Softwareschmiede.StatusServer.Security;

/// <summary>Cookie-Authentifizierungs-Events des Status-Servers:
/// setzt die (admin-konfigurierbare) Sitzungsdauer beim Ausstellen und Erneuern des
/// Tickets, erzwingt die Deaktivierung je Request und gleicht geänderte
/// Berechtigungsstufen zeitnah auf bestehende Sessions ab.</summary>
public sealed class StatusServerCookieEvents : CookieAuthenticationEvents
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ServerSettingsService _settings;
    private readonly TimeProvider _timeProvider;

    /// <summary>Erstellt die Events-Instanz.</summary>
    /// <param name="userManager">Der Identity-User-Manager.</param>
    /// <param name="signInManager">Der Identity-SignIn-Manager.</param>
    /// <param name="settings">Der Server-Einstellungs-Service (Sitzungsdauer).</param>
    /// <param name="timeProvider">Die Zeitquelle.</param>
    public StatusServerCookieEvents(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ServerSettingsService settings,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(userManager);
        ArgumentNullException.ThrowIfNull(signInManager);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _userManager = userManager;
        _signInManager = signInManager;
        _settings = settings;
        _timeProvider = timeProvider;
    }

    /// <summary>Setzt beim Ausstellen des Tickets die konfigurierte Sitzungsdauer
    /// als Ablaufzeit (persistentes Cookie).</summary>
    /// <param name="context">Der Sign-In-Kontext.</param>
    /// <returns>Eine abgeschlossene Aufgabe.</returns>
    public override async Task SigningIn(CookieSigningInContext context)
    {
        var minutes = await _settings
            .GetSessionDurationAsync(context.HttpContext.RequestAborted)
            .ConfigureAwait(false);
        context.Properties.IsPersistent = true;
        context.Properties.ExpiresUtc = _timeProvider.GetUtcNow().AddMinutes(minutes);
        await base.SigningIn(context).ConfigureAwait(false);
    }

    /// <summary>Revalidiert die Cookie-Session je Request: Deaktivierungs-Prüfung
    /// (sofortiger Zugriffsverlust), Rollen-Claim-Abgleich (zeitnahe Wirkung von
    /// Stufenänderungen ohne erneute Anmeldung) und Sliding-Refresh mit der
    /// konfigurierten Sitzungsdauer.</summary>
    /// <param name="context">Der Validierungskontext.</param>
    /// <returns>Eine abgeschlossene Aufgabe.</returns>
    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        // Identity-Security-Stamp-Prüfung mitlaufen lassen (Standard-Intervall);
        // lehnt z. B. Sessions mit geändertem Stamp ab (Principal wird dann null).
        await SecurityStampValidator.ValidatePrincipalAsync(context).ConfigureAwait(false);
        if (context.Principal is null)
            return;

        var user = await _userManager.GetUserAsync(context.Principal).ConfigureAwait(false);
        if (user is null || user.IsDeactivated)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme).ConfigureAwait(false);
            return;
        }

        var dbRoles = await _userManager.GetRolesAsync(user).ConfigureAwait(false);
        var roleClaimType = _userManager.Options.ClaimsIdentity.RoleClaimType;
        var claimRoles = context.Principal.FindAll(roleClaimType).Select(c => c.Value).ToList();
        if (!dbRoles.OrderBy(r => r, StringComparer.Ordinal)
                .SequenceEqual(claimRoles.OrderBy(r => r, StringComparer.Ordinal)))
        {
            // Die Stufe wurde inzwischen geändert — Cookie-Principal ohne Re-Login erneuern.
            context.ReplacePrincipal(await _signInManager.CreateUserPrincipalAsync(user).ConfigureAwait(false));
            context.ShouldRenew = true;
        }

        // Sliding-Renewal nur nach Verstreichen der halben Ticket-Laufzeit — ein
        // bedingungsloses ShouldRenew würde jede Antwort neu signieren und
        // SlidingExpiration damit wirkungslos machen. IssuedUtc/ExpiresUtc werden
        // auf die aktuell konfigurierte Dauer gesetzt, damit der Handler beim
        // Renew genau dieses Intervall verlängert.
        var now = _timeProvider.GetUtcNow();
        if (context.Properties.IssuedUtc is { } issuedUtc
            && context.Properties.ExpiresUtc is { } expiresUtc
            && now - issuedUtc >= expiresUtc - now)
        {
            var minutes = await _settings
                .GetSessionDurationAsync(context.HttpContext.RequestAborted)
                .ConfigureAwait(false);
            context.Properties.IssuedUtc = now;
            context.Properties.ExpiresUtc = now.AddMinutes(minutes);
            context.ShouldRenew = true;
        }
    }

    /// <summary>Setup-aware Weiterleitung: ohne vorhandenen Benutzer zur
    /// Registrierung, sonst zur Login-Seite.</summary>
    /// <param name="context">Der Redirect-Kontext.</param>
    /// <returns>Eine abgeschlossene Aufgabe.</returns>
    public override async Task RedirectToLogin(RedirectContext<CookieAuthenticationOptions> context)
    {
        var hasUsers = await _userManager.Users
            .AnyAsync(context.HttpContext.RequestAborted)
            .ConfigureAwait(false);
        if (hasUsers)
            context.Response.Redirect(context.RedirectUri);
        else
            context.Response.Redirect("/register");
    }
}
