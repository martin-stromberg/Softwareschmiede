using Microsoft.AspNetCore.Identity;

namespace Softwareschmiede.StatusServer.Data;

/// <summary>Benutzerkonto des Status-Servers (ASP.NET Core Identity).</summary>
public sealed class ApplicationUser : IdentityUser
{
    /// <summary>Deaktivierte Konten können sich nicht anmelden und verlieren
    /// bestehende Sitzungen sofort (Prüfung in den Cookie-Events je Request).</summary>
    public bool IsDeactivated { get; set; }

    /// <summary>Die Anzeige-Einstellung (Light/Dark/Automatisch) der
    /// server-gerenderten Web-UI — gilt ab dem nächsten Seitenaufruf.</summary>
    public ThemePreference Theme { get; set; } = ThemePreference.Auto;
}
