using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Domain.Interfaces;

/// <summary>Git-Provider Plugin Interface.</summary>
public interface IGitPlugin : IPlugin
{
    /// <summary>
    /// Liefert die Felder für die projektbezogene Repository-Verknüpfung.
    /// </summary>
    /// <remarks>
    /// Diese Felder steuern die UI für "Repository verknüpfen" (Label, Placeholder, Pflichtfelder).
    /// </remarks>
    IReadOnlyList<PluginSettingField> GetRepositoryLinkFields() => [];

    /// <summary>Ruft Issues aus dem Repository ab.</summary>
    /// <param name="repositoryId">Repository-Identifier (z.B. "owner/repo").</param>
    /// <param name="ct">Cancellation Token.</param>
    Task<IEnumerable<Issue>> GetIssuesAsync(string repositoryId, CancellationToken ct = default);

    /// <summary>Klont ein Repository in das Zielverzeichnis.</summary>
    /// <param name="repositoryUrl">URL des Repositories.</param>
    /// <param name="targetPath">Zielpfad für den Klon.</param>
    /// <param name="ct">Cancellation Token.</param>
    Task CloneRepositoryAsync(string repositoryUrl, string targetPath, CancellationToken ct = default);

    /// <summary>Legt einen neuen Branch im lokalen Klon an.</summary>
    /// <param name="localPath">Lokaler Pfad des geklonten Repositories.</param>
    /// <param name="branchName">Name des neuen Branches.</param>
    /// <param name="ct">Cancellation Token.</param>
    Task CreateBranchAsync(string localPath, string branchName, CancellationToken ct = default);

    /// <summary>Pusht den Branch auf den Remote.</summary>
    /// <param name="localPath">Lokaler Pfad des geklonten Repositories.</param>
    /// <param name="branchName">Name des zu pushenden Branches.</param>
    /// <param name="ct">Cancellation Token.</param>
    Task PushBranchAsync(string localPath, string branchName, CancellationToken ct = default);

    /// <summary>Holt Änderungen vom Remote.</summary>
    /// <param name="localPath">Lokaler Pfad des geklonten Repositories.</param>
    /// <param name="ct">Cancellation Token.</param>
    Task PullAsync(string localPath, CancellationToken ct = default);

    /// <summary>Erstellt einen Pull Request.</summary>
    /// <param name="repositoryId">Repository-Identifier (z.B. "owner/repo").</param>
    /// <param name="branchName">Name des Quell-Branches.</param>
    /// <param name="title">Titel des Pull Requests.</param>
    /// <param name="body">Beschreibung des Pull Requests.</param>
    /// <param name="ct">Cancellation Token.</param>
    Task<PullRequest> CreatePullRequestAsync(string repositoryId, string branchName, string title, string body, CancellationToken ct = default);

    /// <summary>Führt einen Commit durch.</summary>
    /// <param name="localPath">Lokaler Pfad des geklonten Repositories.</param>
    /// <param name="message">Commit-Nachricht.</param>
    /// <param name="ct">Cancellation Token.</param>
    Task CommitAsync(string localPath, string message, CancellationToken ct = default);

    /// <summary>Setzt Commits zurück.</summary>
    /// <param name="localPath">Lokaler Pfad des geklonten Repositories.</param>
    /// <param name="resetType">Art des Resets (z.B. "hard", "soft", "mixed").</param>
    /// <param name="targetRef">Ziel-Referenz für den Reset.</param>
    /// <param name="ct">Cancellation Token.</param>
    Task ResetAsync(string localPath, string resetType, string? targetRef, CancellationToken ct = default);

    /// <summary>Prüft ob das Plugin verfügbar ist (CLI installiert, Token gültig).</summary>
    /// <param name="ct">Cancellation Token.</param>
    Task<bool> CheckHealthAsync(CancellationToken ct = default);

    /// <summary>Listet alle Remote-Branches eines Repositories auf (ohne Klon).</summary>
    /// <param name="repositoryUrl">URL des Repositories.</param>
    /// <param name="ct">Cancellation Token.</param>
    /// <returns>Liste der Branch-Namen (ohne "origin/"-Präfix).</returns>
    Task<IEnumerable<string>> GetRemoteBranchesAsync(string repositoryUrl, CancellationToken ct = default);

    /// <summary>Ermittelt den Standard-Branch eines Repositories (z.B. "main" oder "master").</summary>
    /// <param name="repositoryUrl">URL des Repositories.</param>
    /// <param name="ct">Cancellation Token.</param>
    /// <returns>Name des Standard-Branches.</returns>
    Task<string> GetDefaultBranchAsync(string repositoryUrl, CancellationToken ct = default);

    /// <summary>Wechselt zu einem vorhandenen Remote-Branch (erstellt lokalen Tracking-Branch).</summary>
    /// <param name="localPath">Lokaler Pfad des geklonten Repositories.</param>
    /// <param name="branchName">Name des Remote-Branches (ohne "origin/"-Präfix).</param>
    /// <param name="ct">Cancellation Token.</param>
    Task CheckoutRemoteBranchAsync(string localPath, string branchName, CancellationToken ct = default);

    /// <summary>Liefert die verfügbaren Git-Aktionen für die UI.</summary>
    /// <param name="localPath">Optionaler lokaler Arbeitsverzeichnispfad der Aufgabe.</param>
    /// <param name="ct">Cancellation Token.</param>
    Task<GitActionCapabilities> GetGitActionCapabilitiesAsync(string? localPath = null, CancellationToken ct = default)
        => Task.FromResult(new GitActionCapabilities(
            RepositoryKind.RemoteGit,
            IsWorkingDirectoryCopy: false,
            CanPush: true,
            CanPull: true,
            CanCreatePullRequest: true,
            CanMergeToSource: false));

    /// <summary>Übernimmt lokale Änderungen vom Arbeitsverzeichnis ins Quellverzeichnis.</summary>
    /// <param name="localPath">Lokaler Pfad des Arbeitsverzeichnisses.</param>
    /// <param name="ct">Cancellation Token.</param>
    Task MergeToSourceAsync(string localPath, CancellationToken ct = default)
        => throw new NotSupportedException($"'{nameof(MergeToSourceAsync)}' wird von Plugin '{PluginPrefix}' nicht unterstützt.");

    /// <summary>Liefert die für dieses Plugin verfügbaren Repositories aus der externen Quelle.</summary>
    /// <param name="ct">Cancellation Token.</param>
    Task<IEnumerable<AvailableRepository>> GetAvailableRepositoriesAsync(CancellationToken ct = default);
}
