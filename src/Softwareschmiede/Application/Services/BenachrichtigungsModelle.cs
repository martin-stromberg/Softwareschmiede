using Softwareschmiede.Domain.Enums;

namespace Softwareschmiede.Application.Services;

public sealed record BenachrichtigungsEinstellungenDto(
    BenachrichtigungsModus BannerModus,
    BenachrichtigungsModus TonModus);

public sealed record BenachrichtigungsAudioInfoDto(
    bool HatBenutzerdefinierteDatei,
    string? Dateiname,
    string? MimeType,
    int? GroesseBytes);

public sealed record BenachrichtigungsAudioPayload(string MimeType, string Base64Inhalt);
