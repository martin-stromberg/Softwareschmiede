using System.Text;
using FluentAssertions;
using Softwareschmiede.Infrastructure.Services;
using Softwareschmiede.Tests.Helpers;
using Softwareschmiede.Tests.Infrastructure.Testing;

namespace Softwareschmiede.Tests.Infrastructure.Services;

/// <summary>Roundtrip-Tests gegen den echten Windows Credential Store (<c>CredWriteW</c>/<c>CredReadW</c>)
/// unter einem testeigenen Schlüssel — belegt die Längenverträglichkeit von ~520-stelligen
/// ghs_-Installationstokens und das Fehlerbild oberhalb der Hartgrenze
/// <c>CRED_MAX_CREDENTIAL_BLOB_SIZE</c> (2560 Byte ≙ 1280 UTF-16-Zeichen).</summary>
public sealed class WindowsCredentialStoreTests
{
    private const string TestTarget = "Softwareschmiede.Tests.GhsTokenProbe";

    /// <summary>Ein ~520-stelliges ghs_-Token (~1040 Byte UTF-16, deutlich unter 2560 Byte)
    /// übersteht den CredWriteW/CredReadW-Roundtrip exakt.</summary>
    [OsInterfaceFact]
    public void SetCredential_GhsInstallationToken_RoundtripsExact()
    {
        var sut = new WindowsCredentialStore();
        try
        {
            Encoding.Unicode.GetByteCount(TestTokens.GhsInstallationToken)
                .Should().BeLessThan(2560, "das Token muss unter der CredWriteW-Hartgrenze bleiben");

            sut.SetCredential(TestTarget, TestTokens.GhsInstallationToken);

            sut.GetCredential(TestTarget).Should().Be(TestTokens.GhsInstallationToken);
        }
        finally
        {
            sut.DeleteCredential(TestTarget);
        }
    }

    /// <summary>Ein Wert oberhalb der CredWriteW-Hartgrenze (>1280 UTF-16-Zeichen) schlägt mit
    /// <see cref="InvalidOperationException"/> samt Win32-Fehlercode fehl — dokumentiert die
    /// einzige Längen-Hartgrenze der Token-Kette und ihr Fehlerbild.</summary>
    [OsInterfaceFact]
    public void SetCredential_OverlongValue_ThrowsInvalidOperation()
    {
        var sut = new WindowsCredentialStore();
        try
        {
            Encoding.Unicode.GetByteCount(TestTokens.OverlongCredentialValue)
                .Should().BeGreaterThan(2560, "der Prüfwert muss die CredWriteW-Hartgrenze überschreiten");

            var act = () => sut.SetCredential(TestTarget, TestTokens.OverlongCredentialValue);

            act.Should().Throw<InvalidOperationException>().WithMessage("*CredWrite*");
        }
        finally
        {
            sut.DeleteCredential(TestTarget);
        }
    }
}
