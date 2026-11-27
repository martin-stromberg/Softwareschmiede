using FluentAssertions;
using Softwareschmiede.StatusServer.Data;
using Softwareschmiede.StatusServer.Security;
using Softwareschmiede.StatusServer.Tests.TestSupport;

namespace Softwareschmiede.StatusServer.Tests.Security;

/// <summary>Tests für <see cref="RelayTokenValidator"/> (datenbankbasierte Token-Prüfung).</summary>
public sealed class RelayTokenValidatorTests
{
    /// <summary>Ein bekanntes, nicht widerrufenes Token wird akzeptiert.</summary>
    [Fact]
    public async Task IsValid_KnownToken_ReturnsTrue()
    {
        var ct = TestContext.Current.CancellationToken;
        using var db = new StatusServerTestDb();
        db.Context.RelayTokens.Add(new RelayToken
        {
            Label = "Instanz",
            TokenHash = RelayTokenService.HashToken("token-a"),
            CreatedUtc = DateTimeOffset.UtcNow,
        });
        await db.Context.SaveChangesAsync(ct);

        var sut = new RelayTokenValidator(db.Context);
        (await sut.IsValidAsync("token-a", ct)).Should().BeTrue();
    }

    /// <summary>Unbekannte, leere oder <see langword="null"/>-Tokens werden abgelehnt.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("unbekannt")]
    public async Task IsValid_UnknownOrEmptyToken_ReturnsFalse(string? token)
    {
        var ct = TestContext.Current.CancellationToken;
        using var db = new StatusServerTestDb();
        db.Context.RelayTokens.Add(new RelayToken
        {
            Label = "Instanz",
            TokenHash = RelayTokenService.HashToken("token-a"),
            CreatedUtc = DateTimeOffset.UtcNow,
        });
        await db.Context.SaveChangesAsync(ct);

        var sut = new RelayTokenValidator(db.Context);
        (await sut.IsValidAsync(token, ct)).Should().BeFalse();
    }

    /// <summary>Ohne Token-Datensätze wird jeder Token abgelehnt.</summary>
    [Fact]
    public async Task IsValid_EmptyTokenList_RejectsAll()
    {
        using var db = new StatusServerTestDb();
        var sut = new RelayTokenValidator(db.Context);
        (await sut.IsValidAsync("irgendwas", TestContext.Current.CancellationToken)).Should().BeFalse();
    }

    /// <summary>Ein widerrufenes Token wird trotz korrektem Klartext abgelehnt.</summary>
    [Fact]
    public async Task IsValid_RevokedToken_ReturnsFalse()
    {
        var ct = TestContext.Current.CancellationToken;
        using var db = new StatusServerTestDb();
        db.Context.RelayTokens.Add(new RelayToken
        {
            Label = "Instanz",
            TokenHash = RelayTokenService.HashToken("token-a"),
            CreatedUtc = DateTimeOffset.UtcNow,
            RevokedUtc = DateTimeOffset.UtcNow,
        });
        await db.Context.SaveChangesAsync(ct);

        var sut = new RelayTokenValidator(db.Context);
        (await sut.IsValidAsync("token-a", ct)).Should().BeFalse();
    }
}
