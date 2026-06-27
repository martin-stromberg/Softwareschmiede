using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Infrastructure.Data;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>BenachrichtigungsEinstellungenServiceTests.</summary>
public sealed class BenachrichtigungsEinstellungenServiceTests
{
    private static SoftwareschmiededDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<SoftwareschmiededDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SoftwareschmiededDbContext(options);
    }

    /// <summary><summary>SaveAsync_ShouldPersistModesPerUser.</summary>.</summary>
    [Fact]
    /// <summary>SaveAsync_ShouldPersistModesPerUser.</summary>
    public async Task SaveAsync_ShouldPersistModesPerUser()
    {
        await using var db = CreateDb();
        var sut = new BenachrichtigungsEinstellungenService(db);

        await sut.SaveAsync(
            "user-a",
            new BenachrichtigungsEinstellungenDto(BenachrichtigungsModus.Deaktiviert, BenachrichtigungsModus.Ton));

        var loaded = await sut.GetAsync("user-a");
        loaded.BannerModus.Should().Be(BenachrichtigungsModus.Deaktiviert);
        loaded.TonModus.Should().Be(BenachrichtigungsModus.Ton);
    }

    /// <summary><summary>GetAsync_ShouldReturnDefaults_WhenNoSettingsExist.</summary>.</summary>
    [Fact]
    /// <summary>GetAsync_ShouldReturnDefaults_WhenNoSettingsExist.</summary>
    public async Task GetAsync_ShouldReturnDefaults_WhenNoSettingsExist()
    {
        await using var db = CreateDb();
        var sut = new BenachrichtigungsEinstellungenService(db);

        var loaded = await sut.GetAsync("unknown-user");

        loaded.BannerModus.Should().Be(BenachrichtigungsModus.Banner);
        loaded.TonModus.Should().Be(BenachrichtigungsModus.Deaktiviert);
    }

    /// <summary><summary>SaveAsync_ShouldUpsertExistingUserSettings.</summary>.</summary>
    [Fact]
    /// <summary>SaveAsync_ShouldUpsertExistingUserSettings.</summary>
    public async Task SaveAsync_ShouldUpsertExistingUserSettings()
    {
        await using var db = CreateDb();
        var sut = new BenachrichtigungsEinstellungenService(db);

        await sut.SaveAsync("user-a", new BenachrichtigungsEinstellungenDto(BenachrichtigungsModus.Deaktiviert, BenachrichtigungsModus.Ton));
        await sut.SaveAsync("user-a", new BenachrichtigungsEinstellungenDto(BenachrichtigungsModus.Banner, BenachrichtigungsModus.Deaktiviert));

        db.BenachrichtigungsEinstellungen.Should().ContainSingle(e => e.BenutzerId == "user-a");
        var loaded = await sut.GetAsync("user-a");
        loaded.BannerModus.Should().Be(BenachrichtigungsModus.Banner);
        loaded.TonModus.Should().Be(BenachrichtigungsModus.Deaktiviert);
    }

    /// <summary><summary>UploadAudioAsync_ShouldRejectOversizedFiles.</summary>.</summary>
    [Fact]
    /// <summary>UploadAudioAsync_ShouldRejectOversizedFiles.</summary>
    public async Task UploadAudioAsync_ShouldRejectOversizedFiles()
    {
        await using var db = CreateDb();
        var sut = new BenachrichtigungsEinstellungenService(db);
        var payload = new byte[BenachrichtigungsEinstellungenService.MaxAudioDateigroesseBytes + 1];

        var action = () => sut.UploadAudioAsync("user-a", "test.mp3", "audio/mpeg", payload);

        await action.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*10 MB*");
    }

    /// <summary><summary>UploadAudioAsync_ShouldRejectEmptyPayload.</summary>.</summary>
    [Fact]
    /// <summary>UploadAudioAsync_ShouldRejectEmptyPayload.</summary>
    public async Task UploadAudioAsync_ShouldRejectEmptyPayload()
    {
        await using var db = CreateDb();
        var sut = new BenachrichtigungsEinstellungenService(db);

        var action = () => sut.UploadAudioAsync("user-a", "test.mp3", "audio/mpeg", []);

        await action.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*leer*");
    }

    /// <summary><summary>UploadAudioAsync_ShouldRejectInvalidExtension.</summary>.</summary>
    [Fact]
    /// <summary>UploadAudioAsync_ShouldRejectInvalidExtension.</summary>
    public async Task UploadAudioAsync_ShouldRejectInvalidExtension()
    {
        await using var db = CreateDb();
        var sut = new BenachrichtigungsEinstellungenService(db);

        var action = () => sut.UploadAudioAsync("user-a", "test.txt", "audio/mpeg", [0x49, 0x44, 0x33]);

        await action.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Audioformat*");
    }

    /// <summary><summary>UploadAudioAsync_ShouldRejectInvalidMimeType.</summary>.</summary>
    [Fact]
    /// <summary>UploadAudioAsync_ShouldRejectInvalidMimeType.</summary>
    public async Task UploadAudioAsync_ShouldRejectInvalidMimeType()
    {
        await using var db = CreateDb();
        var sut = new BenachrichtigungsEinstellungenService(db);

        var action = () => sut.UploadAudioAsync("user-a", "test.mp3", "text/plain", [0x49, 0x44, 0x33]);

        await action.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*MIME-Typ*");
    }

    /// <summary><summary>UploadAudioAsync_ShouldRejectWhenSignatureDoesNotMatchExtension.</summary>.</summary>
    [Fact]
    /// <summary>UploadAudioAsync_ShouldRejectWhenSignatureDoesNotMatchExtension.</summary>
    public async Task UploadAudioAsync_ShouldRejectWhenSignatureDoesNotMatchExtension()
    {
        await using var db = CreateDb();
        var sut = new BenachrichtigungsEinstellungenService(db);
        var wavBytes = new byte[] { 0x52, 0x49, 0x46, 0x46, 0x00, 0x00, 0x00, 0x00, 0x57, 0x41, 0x56, 0x45 };

        var action = () => sut.UploadAudioAsync("user-a", "test.mp3", "audio/mpeg", wavBytes);

        await action.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Signatur*");
    }

    /// <summary><summary>UploadAudioAsync_ShouldStoreAndResolveAudioPayload.</summary>.</summary>
    [Fact]
    /// <summary>UploadAudioAsync_ShouldStoreAndResolveAudioPayload.</summary>
    public async Task UploadAudioAsync_ShouldStoreAndResolveAudioPayload()
    {
        await using var db = CreateDb();
        var sut = new BenachrichtigungsEinstellungenService(db);
        var mp3Bytes = new byte[] { 0x49, 0x44, 0x33, 0x04, 0x00, 0x00 };

        await sut.UploadAudioAsync("user-a", "alert.mp3", "audio/mpeg", mp3Bytes);

        var payload = await sut.GetAudioPayloadAsync("user-a");
        payload.Should().NotBeNull();
        payload!.MimeType.Should().Be("audio/mpeg");
        payload.Base64Inhalt.Should().Be(Convert.ToBase64String(mp3Bytes));
    }

    /// <summary><summary>UploadAudioAsync_ShouldSanitizeFilenameAndInferMimeType_WhenMimeIsMissing.</summary>.</summary>
    [Fact]
    /// <summary>UploadAudioAsync_ShouldSanitizeFilenameAndInferMimeType_WhenMimeIsMissing.</summary>
    public async Task UploadAudioAsync_ShouldSanitizeFilenameAndInferMimeType_WhenMimeIsMissing()
    {
        await using var db = CreateDb();
        var sut = new BenachrichtigungsEinstellungenService(db);
        var wavBytes = new byte[] { 0x52, 0x49, 0x46, 0x46, 0x00, 0x00, 0x00, 0x00, 0x57, 0x41, 0x56, 0x45 };
        var veryLongName = $"..\\evil\\{new string('a', 260)}.wav";

        await sut.UploadAudioAsync("user-a", veryLongName, null, wavBytes);

        var info = await sut.GetAudioInfoAsync("user-a");
        info.HatBenutzerdefinierteDatei.Should().BeTrue();
        info.Dateiname.Should().NotContain("\\");
        info.Dateiname.Should().NotContain("/");
        info.Dateiname.Should().StartWith("a");
        info.Dateiname.Should().HaveLength(240);
        info.MimeType.Should().Be("audio/wav");
    }

    /// <summary><summary>RemoveAudioAsync_ShouldDoNothing_WhenNoAudioExists.</summary>.</summary>
    [Fact]
    /// <summary>RemoveAudioAsync_ShouldDoNothing_WhenNoAudioExists.</summary>
    public async Task RemoveAudioAsync_ShouldDoNothing_WhenNoAudioExists()
    {
        await using var db = CreateDb();
        var sut = new BenachrichtigungsEinstellungenService(db);

        var act = () => sut.RemoveAudioAsync("missing-user");

        await act.Should().NotThrowAsync();
    }

    /// <summary><summary>RemoveAudioAsync_ShouldDeleteStoredAudio.</summary>.</summary>
    [Fact]
    /// <summary>RemoveAudioAsync_ShouldDeleteStoredAudio.</summary>
    public async Task RemoveAudioAsync_ShouldDeleteStoredAudio()
    {
        await using var db = CreateDb();
        var sut = new BenachrichtigungsEinstellungenService(db);
        var oggBytes = new byte[] { 0x4F, 0x67, 0x67, 0x53, 0x00 };
        await sut.UploadAudioAsync("user-a", "alarm.ogg", "audio/ogg", oggBytes);

        await sut.RemoveAudioAsync("user-a");

        var info = await sut.GetAudioInfoAsync("user-a");
        var payload = await sut.GetAudioPayloadAsync("user-a");
        info.HatBenutzerdefinierteDatei.Should().BeFalse();
        payload.Should().BeNull();
    }

    /// <summary><summary>GetAudioPayloadAsync_ShouldReturnNull_WhenStoredAudioContentIsEmpty.</summary>.</summary>
    [Fact]
    /// <summary>GetAudioPayloadAsync_ShouldReturnNull_WhenStoredAudioContentIsEmpty.</summary>
    public async Task GetAudioPayloadAsync_ShouldReturnNull_WhenStoredAudioContentIsEmpty()
    {
        await using var db = CreateDb();
        db.BenachrichtigungsAudioDateien.Add(new BenachrichtigungsAudioDatei
        {
            Id = Guid.NewGuid(),
            BenutzerId = "user-a",
            OriginalDateiname = "empty.mp3",
            MimeType = "audio/mpeg",
            GroesseBytes = 0,
            Inhalt = [],
            HochgeladenAm = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var sut = new BenachrichtigungsEinstellungenService(db);
        var payload = await sut.GetAudioPayloadAsync("user-a");

        payload.Should().BeNull();
    }
}
