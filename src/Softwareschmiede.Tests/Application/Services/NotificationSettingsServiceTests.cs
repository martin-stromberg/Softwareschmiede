using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Infrastructure.Data;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>BenachrichtigungsEinstellungenServiceTests.</summary>
public sealed class NotificationSettingsServiceTests
{
    private static SoftwareschmiedeDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<SoftwareschmiedeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SoftwareschmiedeDbContext(options);
    }

    /// <summary>SaveAsync_ShouldPersistModesPerUser.</summary>
    [Fact]
    public async Task SaveAsync_ShouldPersistModesPerUser()
    {
        await using var db = CreateDb();
        var sut = new NotificationSettingsService(db);

        await sut.SaveAsync("user-a", new NotificationSettingsDto(NotificationMode.Disabled, NotificationMode.Sound), TestContext.Current.CancellationToken);

        var loaded = await sut.GetAsync("user-a", TestContext.Current.CancellationToken);
        loaded.BannerMode.Should().Be(NotificationMode.Disabled);
        loaded.SoundMode.Should().Be(NotificationMode.Sound);
    }

    /// <summary>GetAsync_ShouldReturnDefaults_WhenNoSettingsExist.</summary>
    [Fact]
    public async Task GetAsync_ShouldReturnDefaults_WhenNoSettingsExist()
    {
        await using var db = CreateDb();
        var sut = new NotificationSettingsService(db);

        var loaded = await sut.GetAsync("unknown-user", TestContext.Current.CancellationToken);

        loaded.BannerMode.Should().Be(NotificationMode.Banner);
        loaded.SoundMode.Should().Be(NotificationMode.Disabled);
    }

    /// <summary>SaveAsync_ShouldUpsertExistingUserSettings.</summary>
    [Fact]
    public async Task SaveAsync_ShouldUpsertExistingUserSettings()
    {
        await using var db = CreateDb();
        var sut = new NotificationSettingsService(db);

        await sut.SaveAsync("user-a", new NotificationSettingsDto(NotificationMode.Disabled, NotificationMode.Sound), TestContext.Current.CancellationToken);
        await sut.SaveAsync("user-a", new NotificationSettingsDto(NotificationMode.Banner, NotificationMode.Disabled), TestContext.Current.CancellationToken);

        db.NotificationSettings.Should().ContainSingle(e => e.UserId == "user-a");
        var loaded = await sut.GetAsync("user-a", TestContext.Current.CancellationToken);
        loaded.BannerMode.Should().Be(NotificationMode.Banner);
        loaded.SoundMode.Should().Be(NotificationMode.Disabled);
    }

    /// <summary>UploadAudioAsync_ShouldRejectOversizedFiles.</summary>
    [Fact]
    public async Task UploadAudioAsync_ShouldRejectOversizedFiles()
    {
        await using var db = CreateDb();
        var sut = new NotificationSettingsService(db);
        var payload = new byte[NotificationSettingsService.MaxAudioFileSizeBytes + 1];

        var action = () => sut.UploadAudioAsync("user-a", "test.mp3", "audio/mpeg", payload);

        await action.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*10 MB*");
    }

    /// <summary>UploadAudioAsync_ShouldRejectEmptyPayload.</summary>
    [Fact]
    public async Task UploadAudioAsync_ShouldRejectEmptyPayload()
    {
        await using var db = CreateDb();
        var sut = new NotificationSettingsService(db);

        var action = () => sut.UploadAudioAsync("user-a", "test.mp3", "audio/mpeg", []);

        await action.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*leer*");
    }

    /// <summary>UploadAudioAsync_ShouldRejectInvalidExtension.</summary>
    [Fact]
    public async Task UploadAudioAsync_ShouldRejectInvalidExtension()
    {
        await using var db = CreateDb();
        var sut = new NotificationSettingsService(db);

        var action = () => sut.UploadAudioAsync("user-a", "test.txt", "audio/mpeg", [0x49, 0x44, 0x33]);

        await action.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Audioformat*");
    }

    /// <summary>UploadAudioAsync_ShouldRejectInvalidMimeType.</summary>
    [Fact]
    public async Task UploadAudioAsync_ShouldRejectInvalidMimeType()
    {
        await using var db = CreateDb();
        var sut = new NotificationSettingsService(db);

        var action = () => sut.UploadAudioAsync("user-a", "test.mp3", "text/plain", [0x49, 0x44, 0x33]);

        await action.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*MIME-Typ*");
    }

    /// <summary>UploadAudioAsync_ShouldRejectWhenSignatureDoesNotMatchExtension.</summary>
    [Fact]
    public async Task UploadAudioAsync_ShouldRejectWhenSignatureDoesNotMatchExtension()
    {
        await using var db = CreateDb();
        var sut = new NotificationSettingsService(db);
        var wavBytes = new byte[] { 0x52, 0x49, 0x46, 0x46, 0x00, 0x00, 0x00, 0x00, 0x57, 0x41, 0x56, 0x45 };

        var action = () => sut.UploadAudioAsync("user-a", "test.mp3", "audio/mpeg", wavBytes);

        await action.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Signatur*");
    }

    /// <summary>UploadAudioAsync_ShouldStoreAndResolveAudioPayload.</summary>
    [Fact]
    public async Task UploadAudioAsync_ShouldStoreAndResolveAudioPayload()
    {
        await using var db = CreateDb();
        var sut = new NotificationSettingsService(db);
        var mp3Bytes = new byte[] { 0x49, 0x44, 0x33, 0x04, 0x00, 0x00 };

        await sut.UploadAudioAsync("user-a", "alert.mp3", "audio/mpeg", mp3Bytes, TestContext.Current.CancellationToken);

        var payload = await sut.GetAudioPayloadAsync("user-a", TestContext.Current.CancellationToken);
        payload.Should().NotBeNull();
        payload!.MimeType.Should().Be("audio/mpeg");
        payload.Base64Content.Should().Be(Convert.ToBase64String(mp3Bytes));
    }

    /// <summary>UploadAudioAsync_ShouldSanitizeFilenameAndInferMimeType_WhenMimeIsMissing.</summary>
    [Fact]
    public async Task UploadAudioAsync_ShouldSanitizeFilenameAndInferMimeType_WhenMimeIsMissing()
    {
        await using var db = CreateDb();
        var sut = new NotificationSettingsService(db);
        var wavBytes = new byte[] { 0x52, 0x49, 0x46, 0x46, 0x00, 0x00, 0x00, 0x00, 0x57, 0x41, 0x56, 0x45 };
        var veryLongName = $"..\\evil\\{new string('a', 260)}.wav";

        await sut.UploadAudioAsync("user-a", veryLongName, null, wavBytes, TestContext.Current.CancellationToken);

        var info = await sut.GetAudioInfoAsync("user-a", TestContext.Current.CancellationToken);
        info.HasCustomFile.Should().BeTrue();
        info.FileName.Should().NotContain("\\");
        info.FileName.Should().NotContain("/");
        info.FileName.Should().StartWith("a");
        info.FileName.Should().HaveLength(240);
        info.MimeType.Should().Be("audio/wav");
    }

    /// <summary>RemoveAudioAsync_ShouldDoNothing_WhenNoAudioExists.</summary>
    [Fact]
    public async Task RemoveAudioAsync_ShouldDoNothing_WhenNoAudioExists()
    {
        await using var db = CreateDb();
        var sut = new NotificationSettingsService(db);

        var act = () => sut.RemoveAudioAsync("missing-user");

        await act.Should().NotThrowAsync();
    }

    /// <summary>RemoveAudioAsync_ShouldDeleteStoredAudio.</summary>
    [Fact]
    public async Task RemoveAudioAsync_ShouldDeleteStoredAudio()
    {
        await using var db = CreateDb();
        var sut = new NotificationSettingsService(db);
        var oggBytes = new byte[] { 0x4F, 0x67, 0x67, 0x53, 0x00 };
        await sut.UploadAudioAsync("user-a", "alarm.ogg", "audio/ogg", oggBytes, TestContext.Current.CancellationToken);

        await sut.RemoveAudioAsync("user-a", TestContext.Current.CancellationToken);

        var info = await sut.GetAudioInfoAsync("user-a", TestContext.Current.CancellationToken);
        var payload = await sut.GetAudioPayloadAsync("user-a", TestContext.Current.CancellationToken);
        info.HasCustomFile.Should().BeFalse();
        payload.Should().BeNull();
    }

    /// <summary>GetAudioPayloadAsync_ShouldReturnNull_WhenStoredAudioContentIsEmpty.</summary>
    [Fact]
    public async Task GetAudioPayloadAsync_ShouldReturnNull_WhenStoredAudioContentIsEmpty()
    {
        await using var db = CreateDb();
        db.NotificationAudioFiles.Add(new NotificationAudioFile
        {
            Id = Guid.NewGuid(),
            UserId = "user-a",
            OriginalFileName = "empty.mp3",
            MimeType = "audio/mpeg",
            SizeBytes = 0,
            Content = [],
            UploadedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var sut = new NotificationSettingsService(db);
        var payload = await sut.GetAudioPayloadAsync("user-a", TestContext.Current.CancellationToken);

        payload.Should().BeNull();
    }
}
