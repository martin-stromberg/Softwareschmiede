using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Softwareschmiede.Domain.ValueObjects;

/// <summary>Art der verknüpften SCM-Referenz.</summary>
public enum ScmReferenceKind { Issue, PullRequest }

/// <summary>Getrennte ID-Namensräume der beobachteten Texte.</summary>
public enum ScmContentKind { Description, IssueComment, ReviewComment, PullRequestReview }

/// <summary>Vom Provider geladene Repository- und Authentifizierungsdaten, ohne Credentials.</summary>
public sealed record ScmEventRepository(string ProviderPrefix, string Host, string RepositoryId,
    string Owner, string Name, string? OwnerId, string ViewerId, string ViewerLogin, bool CanWrite);

/// <summary>Validierter Ursprung einer Beobachtung oder Antwort.</summary>
public sealed record ScmEventReference(ScmEventRepository Repository, ScmReferenceKind Kind, int Number);

/// <summary>Eine separat checkpointbare Quelle. SinceUtc enthält bereits die Überlappung des Aufrufers.</summary>
public sealed record ScmEventReadRequest(ScmEventReference Reference, ScmContentKind ContentKind,
    DateTimeOffset? SinceUtc, DateTimeOffset WatermarkUtc);

/// <summary>Vollständig gelesene Quelle, die atomar mit ihrem Checkpoint gespeichert werden kann.</summary>
public sealed record ScmEventBatch(ScmEventReadRequest Request, IReadOnlyList<ScmEventObservation> Observations);

/// <summary>Text und Identität einer gemeinsamen Remoteversion.</summary>
public sealed record ScmEventObservation(ScmEventReference Reference, ScmContentKind ContentKind,
    string RemoteNodeId, string? RemoteCommentId, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt,
    DateTimeOffset? LastEditedAt, string? AuthorId, string? AuthorLogin, string? EditorId, string? EditorLogin,
    string Body, string Url, DateTimeOffset ObservedAt)
{
    /// <summary>SHA-256 über den exakten UTF-8-Text.</summary>
    public string BodyHash => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Body)));

    /// <summary>Revision der beobachteten Textversion; Metadatenänderungen zählen nicht.</summary>
    public DateTimeOffset Revision => LastEditedAt ?? CreatedAt;

    /// <summary>Stabiler Schlüssel eines Texts, unabhängig von Inhalt und Version.</summary>
    public string ContentKey => string.Join('/', new[] { Reference.Repository.ProviderPrefix.ToLowerInvariant(),
        Reference.Repository.Host.ToLowerInvariant(), Reference.Repository.RepositoryId,
        Reference.Kind.ToString(), Reference.Number.ToString(CultureInfo.InvariantCulture),
        ContentKind.ToString(), RemoteNodeId }.Select(Uri.EscapeDataString));

    /// <summary>Stabiler Deduplizierungsschlüssel einer beobachteten Version.</summary>
    public string EventKey => $"{ContentKey}/{Revision.ToUniversalTime():O}/{BodyHash}";
}

/// <summary>Vom Antwortkanal erlaubte Remoteaktion.</summary>
public enum ScmResponseKind { Comment, Reaction, PullRequest }

/// <summary>Antwort mit ausschließlich vom Host vorgegebenem Ursprung und deterministischer Aktions-ID.</summary>
public sealed record ScmResponseRequest(ScmEventReference Reference, string ActionId, ScmResponseKind Kind,
    string? Body = null, string? Reaction = null, ScmContentKind? ContentKind = null,
    string? RemoteCommentId = null, string? RemoteNodeId = null, string? Title = null,
    string? HeadBranch = null, string? BaseBranch = null)
{
    /// <summary>Wiedererkennungsmarker ohne ausführbaren Trigger.</summary>
    public string Marker => $"<!-- softwareschmiede:action:{ActionId} -->";
}

/// <summary>Bestätigte oder wiedergefundene Remoteaktion.</summary>
public sealed record ScmResponseResult(string RemoteId, string? Url = null, bool AlreadyExisted = false);

/// <summary>Fehlerklasse für gezielte Sperren und zeitgesteuerte Wiederholungen.</summary>
public enum ScmProviderFailureKind { Authentication, Permission, RateLimit, Temporary, Permanent }

/// <summary>Inhaltsarme, credentialfreie Fehlerbeschreibung des Providers.</summary>
public sealed class ScmProviderException : Exception
{
    /// <summary>Erstellt einen typisierten Providerfehler.</summary>
    public ScmProviderException(ScmProviderFailureKind failureKind, string message,
        DateTimeOffset? retryAfterUtc = null, bool outcomeUncertain = false) : base(message)
    {
        FailureKind = failureKind;
        RetryAfterUtc = retryAfterUtc;
        OutcomeUncertain = outcomeUncertain;
    }

    /// <summary>Fehlerkategorie.</summary>
    public ScmProviderFailureKind FailureKind { get; }
    /// <summary>Frühester sinnvoller Wiederholungszeitpunkt.</summary>
    public DateTimeOffset? RetryAfterUtc { get; }
    /// <summary>Eine Remoteänderung kann trotz Fehler stattgefunden haben.</summary>
    public bool OutcomeUncertain { get; }
}
