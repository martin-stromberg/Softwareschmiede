using System.Security.Cryptography;
using System.Text;

namespace Softwareschmiede.StatusServer.Security;

/// <summary>Konstantzeit-Vergleich für Zugangsdaten (Relay-Tokens, Basic-Auth).</summary>
public static class SecretComparer
{
    /// <summary>Vergleicht zwei Geheimwerte konstantzeit. Beide Seiten werden zuvor gehasht,
    /// damit der Vergleich unabhängig von der Eingabelänge ist (kein Längen-Leak durch
    /// frühes Abbruchkriterium) und der Konstantzeit-Vergleich der BCL
    /// (<c>CryptographicOperations.FixedTimeEquals</c>) genutzt werden kann.</summary>
    /// <param name="left">Der bereitgestellte Wert.</param>
    /// <param name="right">Der erwartete Wert.</param>
    /// <returns><see langword="true"/>, wenn beide Werte identisch sind.</returns>
    public static bool FixedTimeEquals(string? left, string? right)
    {
        if (left is null || right is null)
            return left == right;

        return CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(left)),
            SHA256.HashData(Encoding.UTF8.GetBytes(right)));
    }
}
