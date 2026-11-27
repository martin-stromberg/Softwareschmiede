using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Softwareschmiede.StatusServer.Security;
using Softwareschmiede.StatusServer.Tests.TestSupport;

namespace Softwareschmiede.StatusServer.Tests.Security;

/// <summary>Tests für <see cref="RelayTokenService"/> (Erzeugung, Hash-Speicherung, Widerruf).</summary>
public sealed class RelayTokenServiceTests
{
    /// <summary>Ein neues Token wird ausschließlich als SHA-256-Hash persistiert;
    /// der Klartext ist hoch entropisch (32 Byte, Base64Url) und taucht nicht in der DB auf.</summary>
    [Fact]
    public async Task Create_StoresHash_PlaintextNotPersisted()
    {
        var ct = TestContext.Current.CancellationToken;
        using var db = new StatusServerTestDb();
        var time = new TestTimeProvider();
        var sut = new RelayTokenService(db.Context, time);

        var created = await sut.CreateAsync("Instanz-A", ct);

        created.Plaintext.Should().NotBeNullOrEmpty();
        created.Plaintext.Should().MatchRegex("^[A-Za-z0-9_-]{43}$");
        created.Token.TokenHash.Should().Be(RelayTokenService.HashToken(created.Plaintext));
        created.Token.TokenHash.Should().NotBe(created.Plaintext);
        created.Token.CreatedUtc.Should().Be(time.GetUtcNow());

        var persisted = await db.Context.RelayTokens.SingleAsync(ct);
        persisted.TokenHash.Should().Be(created.Token.TokenHash);
        persisted.Label.Should().Be("Instanz-A");
    }

    /// <summary>Labels außerhalb der erlaubten Länge (leer oder &gt; 200 Zeichen)
    /// werden mit einer ArgumentException abgelehnt.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Create_EmptyLabel_Throws(string label)
    {
        using var db = new StatusServerTestDb();
        var sut = new RelayTokenService(db.Context, new TestTimeProvider());
        var act = () => sut.CreateAsync(label, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    /// <summary>Ein Label mit mehr als 200 Zeichen wird abgelehnt.</summary>
    [Fact]
    public async Task Create_TooLongLabel_Throws()
    {
        using var db = new StatusServerTestDb();
        var sut = new RelayTokenService(db.Context, new TestTimeProvider());
        var act = () => sut.CreateAsync(
            new string('x', RelayTokenService.MaxLabelLength + 1),
            TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    /// <summary>Widerruf setzt den Zeitstempel (idempotent bei erneutem Aufruf);
    /// unbekannte Ids liefern <see langword="false"/>.</summary>
    [Fact]
    public async Task Revoke_SetsTimestamp_UnknownIdReturnsFalse()
    {
        var ct = TestContext.Current.CancellationToken;
        using var db = new StatusServerTestDb();
        var time = new TestTimeProvider();
        var sut = new RelayTokenService(db.Context, time);
        var created = await sut.CreateAsync("Instanz-A", ct);

        (await sut.RevokeAsync(created.Token.Id, ct)).Should().BeTrue();
        created.Token.RevokedUtc.Should().Be(time.GetUtcNow());

        // Erneuter Widerruf bleibt erfolgreich und verändert nichts.
        time.Advance(TimeSpan.FromHours(1));
        (await sut.RevokeAsync(created.Token.Id, ct)).Should().BeTrue();
        created.Token.RevokedUtc.Should().NotBe(time.GetUtcNow());

        (await sut.RevokeAsync(999_999, ct)).Should().BeFalse();
    }

    /// <summary>Die Liste liefert alle Tokens neueste zuerst ohne Klartext.</summary>
    [Fact]
    public async Task List_ReturnsNewestFirst()
    {
        var ct = TestContext.Current.CancellationToken;
        using var db = new StatusServerTestDb();
        var time = new TestTimeProvider();
        var sut = new RelayTokenService(db.Context, time);
        var first = await sut.CreateAsync("alt", ct);
        time.Advance(TimeSpan.FromMinutes(5));
        var second = await sut.CreateAsync("neu", ct);

        var list = await sut.ListAsync(ct);
        list.Should().HaveCount(2);
        list[0].Id.Should().Be(second.Token.Id);
        list[1].Id.Should().Be(first.Token.Id);
    }
}
