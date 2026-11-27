namespace Softwareschmiede.StatusServer.Security;

/// <summary>Rollen- und Policy-Konstanten der Status-Server-Benutzerverwaltung.</summary>
public static class StatusRoles
{
    /// <summary>Rolle mit Verwaltungsrechten (Benutzer, Tokens, Einstellungen).</summary>
    public const string Administration = "Administration";

    /// <summary>Rolle mit ausschließlich lesendem Zugriff auf die Status-Seite.</summary>
    public const string Ueberwacher = "Ueberwacher";

    /// <summary>Alle bekannten Rollen (Reihenfolge: höchste Stufe zuerst).</summary>
    public static readonly IReadOnlyList<string> All = [Administration, Ueberwacher];

    /// <summary>Authorization-Policy für den Lesezugriff auf <c>/</c> und <c>/status.json</c> (beide Rollen).</summary>
    public const string PageAccessPolicy = "PageAccess";

    /// <summary>Authorization-Policy für die Admin-Endpunkte (nur <see cref="Administration"/>).</summary>
    public const string AdminOnlyPolicy = "AdminOnly";

    /// <summary>Bildet den anwenderseitigen Anzeigenamen einer Stufe
    /// (die internen Schlüssel sind ASCII-Umschriften, z. B. <c>Ueberwacher</c> → „Überwacher").</summary>
    /// <param name="role">Der Rollen-Schlüssel.</param>
    /// <returns>Der deutsche Anzeigename; unbekannte Schlüssel werden unverändert zurückgegeben.</returns>
    public static string DisplayName(string role) => role switch
    {
        Administration => "Administration",
        Ueberwacher => "Überwacher",
        _ => role,
    };
}
