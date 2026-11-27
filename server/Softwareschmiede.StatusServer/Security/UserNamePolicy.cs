namespace Softwareschmiede.StatusServer.Security;

/// <summary>Kommunikation des erlaubten Benutzername-Zeichensatzes. Es gilt der
/// unveränderte Identity-Default von <c>UserOptions.AllowedUserNameCharacters</c>
/// (keine Umlaute, keine Leerzeichen) — diese Konstanten beschreiben ihn nur für
/// Hinweistexte, HTML-<c>pattern</c>/<c>title</c>-Attribute und Fehlermeldungen.</summary>
internal static class UserNamePolicy
{
    /// <summary>Lesbare Beschreibung des erlaubten Zeichensatzes (Identity-Default).</summary>
    internal const string AllowedCharactersDescription = "a–z, A–Z, 0–9 sowie -._@+";

    /// <summary>HTML-<c>pattern</c>-Attribut für die clientseitige Vorabprüfung
    /// (spiegelt den Identity-Default-Zeichensatz, ersetzt keine Server-Validierung).</summary>
    internal const string AllowedCharactersPattern = "[a-zA-Z0-9._@+-]+";

    /// <summary>Hinweistext unter den Benutzername-Feldern der Formulare.</summary>
    internal const string FieldHint = "Erlaubte Zeichen: a–z, A–Z, 0–9 sowie -._@+ (keine Umlaute, keine Leerzeichen).";
}
