namespace Softwareschmiede.StatusServer.Security;

/// <summary>Zentrale Passwort-Regeln des Status-Servers. <see cref="MinLength"/> ist die
/// führende Stelle für die Mindestlänge: <c>IdentityOptions.Password.RequiredLength</c>,
/// die Vorprüfungen der Endpunkte, Fehlertexte und HTML-<c>minlength</c>-Attribute
/// beziehen sich alle auf diesen Wert.</summary>
internal static class PasswordPolicy
{
    /// <summary>Mindestlänge von Passwörtern.</summary>
    internal const int MinLength = 8;
}
