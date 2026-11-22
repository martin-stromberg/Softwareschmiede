namespace Softwareschmiede.Tests.Helpers;

/// <summary>Synthetische Token-Werte für die Format-/Längenprüfung der GitHub-Credential-Kette
/// (Issue #453). Ein echtes <c>ghs_</c>-Installationstoken erfordert eine installierte GitHub
/// App und ist im Testkontext nicht beschaffbar — die internen Schichten werden daher mit
/// einem synthetischen Token im dokumentierten Format verifiziert.</summary>
public static class TestTokens
{
    /// <summary>Synthetisches ~520-stelliges GitHub-App-Installationstoken: <c>ghs_</c>-Präfix,
    /// JWT-artige Struktur mit zwei Punkten, Zeichensatz <c>[A-Za-z0-9.\-_]</c> — deckt sich mit
    /// der Changelog-Regex <c>ghs_[A-Za-z0-9.\-_]{36,}</c>.</summary>
    public static string GhsInstallationToken { get; } = BuildGhsInstallationToken();

    /// <summary>Credential-Wert oberhalb der <c>CredWriteW</c>-Hartgrenze
    /// <c>CRED_MAX_CREDENTIAL_BLOB_SIZE</c> (2560 Byte ≙ 1280 UTF-16-Zeichen) für die
    /// Grenzprobe am <c>WindowsCredentialStore</c>.</summary>
    public static string OverlongCredentialValue { get; } = new string('a', 1400);

    private static string BuildGhsInstallationToken()
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_";
        static string Segment(int length, int offset) =>
            new(Enumerable.Range(offset, length).Select(i => alphabet[i % alphabet.Length]).ToArray());
        return "ghs_" + Segment(36, 0) + "." + Segment(300, 7) + "." + Segment(178, 29);
    }
}
