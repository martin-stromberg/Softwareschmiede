using System.Text.RegularExpressions;
using System.Text.Json;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Infrastructure.Plugins;

public sealed partial class GitHubPlugin
{
    private const int MaximumCaptionChars = 1000;

    /// <inheritdoc />
    public async Task<ScmResponseResult?> ReconcileResponseAsync(ScmResponseRequest request, CancellationToken ct = default)
    {
        ValidateResponse(request);
        if (request.Kind == ScmResponseKind.IssueBodyUpdate)
            return await ReconcileIssueBodyAsync(request, ct);
        if (request.Kind == ScmResponseKind.Reaction && request.ContentKind == ScmContentKind.PullRequestReview)
            return await ReconcileReviewReactionAsync(request, ct);
        var repository = request.Reference.Repository;
        var client = ClientFor(repository.Account);
        var endpoint = request.Kind switch
        {
            ScmResponseKind.Comment => $"{RepositoryEndpoint(repository)}/issues/{request.Reference.Number}/comments?per_page=100",
            ScmResponseKind.PullRequest => $"{RepositoryEndpoint(repository)}/pulls?state=all&per_page=100",
            ScmResponseKind.Reaction => ReactionEndpoint(request) + "?per_page=100",
            _ => throw Permanent("Unbekannte GitHub-Antwortaktion.")
        };
        // Alle Seiten lesen, bevor Abwesenheit gemeldet wird; eine fehlgeschlagene spätere Seite darf keinen zweiten POST
        var candidates = await RestPagesAsync(client, repository.Host, endpoint, ct);
        foreach (var candidate in candidates)
        {
            var user = Object(candidate, "user");
            if (!IsViewer(repository, String(user, "node_id"), String(user, "login"))) continue;
            var matches = request.Kind == ScmResponseKind.Reaction
                ? String(candidate, "content") == request.Reaction
                : (String(candidate, "body") ?? string.Empty).Contains(request.Marker, StringComparison.Ordinal);
            if (matches) return new(Required(candidate, "id"), SafeResponseUrl(request, candidate), true);
        }
        return null;
    }

    /// <inheritdoc />
    public async Task<ScmResponseResult> ExecuteResponseAsync(ScmResponseRequest request, CancellationToken ct = default)
    {
        ValidateResponse(request);
        var repository = request.Reference.Repository;
        var client = ClientFor(repository.Account);
        // Ein umbenanntes/ersetztes Repository oder geänderte Anmeldedaten dürfen eine persistierte Antwort nicht umleiten.
        var current = await GetEventRepositoryAsync(new(Guid.NewGuid(), PluginPrefix,
            $"https://{repository.Host}/{repository.Owner}/{repository.Name}", repository.Account), ct);
        if (current.RepositoryId != repository.RepositoryId || current.ViewerId != repository.ViewerId)
            throw new ScmProviderException(ScmProviderFailureKind.Permission, "Repository- oder Benutzeridentität hat sich geändert.");
        if (!current.CanWrite) throw new ScmProviderException(ScmProviderFailureKind.Permission, "GitHub-Schreibberechtigung fehlt.");
        var existing = await ReconcileResponseAsync(request, ct);
        if (existing is not null) return existing;
        JsonElement result;
        if (request.Kind == ScmResponseKind.Reaction && request.ContentKind == ScmContentKind.PullRequestReview)
        {
            var data = await GraphAsync(client, repository.Host,
                "mutation($subject:ID!,$content:ReactionContent!){addReaction(input:{subjectId:$subject,content:$content}){reaction{id}}}",
                new { subject = request.RemoteNodeId, content = GraphReaction(request.Reaction!) }, true, ct);
            result = Object(Object(data, "addReaction"), "reaction");
        }
        else
        {
            var composed = request.Kind is ScmResponseKind.Comment or ScmResponseKind.PullRequest or ScmResponseKind.IssueBodyUpdate
                ? await ComposeBodyAsync(request, client, repository, current, ct)
                : null;
            var (method, endpoint, body) = request.Kind switch
            {
                ScmResponseKind.Comment => ("POST", $"{RepositoryEndpoint(repository)}/issues/{request.Reference.Number}/comments",
                    (object)new { body = composed }),
                // Die Beschreibung wird vollständig ersetzt (Titel ausdrücklich unberührt) und
                // trägt denselben Aktions-Marker wie Kommentare — damit findet der Reconcile-Pfad
                // eine angewendete Änderung sicher wieder, statt erneut zu schreiben.
                ScmResponseKind.IssueBodyUpdate => ("PATCH", $"{RepositoryEndpoint(repository)}/issues/{request.Reference.Number}",
                    (object)new { body = composed }),
                ScmResponseKind.Reaction => ("POST", ReactionEndpoint(request), (object)new { content = request.Reaction }),
                ScmResponseKind.PullRequest => ("POST", $"{RepositoryEndpoint(repository)}/pulls",
                    (object)new { title = request.Title, body = composed, head = request.HeadBranch, @base = request.BaseBranch }),
                _ => throw Permanent("Unbekannte GitHub-Antwortaktion.")
            };
            result = await EventApiAsync(client, repository.Host, method, endpoint, body, true, ct);
        }
        var id = String(result, "id");
        if (string.IsNullOrWhiteSpace(id)) throw new ScmProviderException(ScmProviderFailureKind.Temporary,
            "GitHub-Schreibantwort enthält keine bestätigte Aktions-ID.", outcomeUncertain: true);
        return new(id, SafeResponseUrl(request, result));
    }

    /// <summary>Liest das Issue-Objekt selbst: Der Aktions-Marker im Remote-Body beweist die
    /// bereits angewendete Beschreibungsänderung — ein Viewer-Vergleich entfällt, weil der
    /// Issue-Autor nicht das Ereignis-Konto sein muss.</summary>
    private async Task<ScmResponseResult?> ReconcileIssueBodyAsync(ScmResponseRequest request, CancellationToken ct)
    {
        var repository = request.Reference.Repository;
        var client = ClientFor(repository.Account);
        var issue = await EventApiAsync(client, repository.Host, "GET",
            $"{RepositoryEndpoint(repository)}/issues/{request.Reference.Number}", null, false, ct);
        if (issue.ValueKind != JsonValueKind.Object) throw Permanent("Unvollständige GitHub-Antwort.");
        return (String(issue, "body") ?? string.Empty).Contains(request.Marker, StringComparison.Ordinal)
            ? new(Required(issue, "id"), SafeResponseUrl(request, issue), true)
            : null;
    }

    private async Task<ScmResponseResult?> ReconcileReviewReactionAsync(ScmResponseRequest request, CancellationToken ct)
    {
        var repository = request.Reference.Repository;
        var client = ClientFor(repository.Account);
        string? cursor = null;
        var visited = new HashSet<string>(StringComparer.Ordinal);
        ScmResponseResult? found = null;
        do
        {
            var data = await GraphAsync(client, repository.Host,
                "query($id:ID!,$cursor:String){node(id:$id){... on PullRequestReview{pullRequest{number repository{id}} reactions(first:100,after:$cursor){nodes{id content user{id login}} pageInfo{hasNextPage endCursor}}}}}",
                new { id = request.RemoteNodeId, cursor }, false, ct);
            var node = Object(data, "node");
            var pullRequest = Object(node, "pullRequest");
            if (String(Object(pullRequest, "repository"), "id") != repository.RepositoryId
                || String(pullRequest, "number") != request.Reference.Number.ToString(System.Globalization.CultureInfo.InvariantCulture))
                throw Permanent("Review-Reaktion gehört nicht zum gespeicherten Ursprung.");
            var connection = Object(node, "reactions");
            foreach (var reaction in RequiredArray(connection, "nodes"))
            {
                var user = Object(reaction, "user");
                if (String(reaction, "content") == GraphReaction(request.Reaction!)
                    && IsViewer(repository, String(user, "id"), String(user, "login")))
                    found = new(Required(reaction, "id"), null, true);
            }
            cursor = NextCursor(connection, visited);
        } while (cursor is not null);
        return found;
    }

    private void ValidateResponse(ScmResponseRequest request)
    {
        ValidateReference(request.Reference);
        if (!Regex.IsMatch(request.ActionId, @"\A[A-Za-z0-9:_-]{1,200}\z", RegexOptions.CultureInvariant))
            throw Permanent("Ungültige Antwortaktions-ID.");
        if (request.Attachments is { Count: > 0 }
            && (request.Kind is not (ScmResponseKind.Comment or ScmResponseKind.PullRequest)
                || request.Attachments.Any(x => x is null || string.IsNullOrWhiteSpace(x.Path)
                    || string.IsNullOrWhiteSpace(x.Name) || string.IsNullOrWhiteSpace(x.ContentType))))
            throw Permanent("Unbekannte oder unvollständige Antwortaktion.");
        switch (request.Kind)
        {
            case ScmResponseKind.Comment:
                if (string.IsNullOrWhiteSpace(request.Body)) throw Permanent("Antworttext fehlt.");
                break;
            case ScmResponseKind.Reaction:
                _ = GraphReaction(request.Reaction ?? string.Empty);
                if (request.ContentKind == ScmContentKind.PullRequestReview)
                {
                    if (request.Reference.Kind != ScmReferenceKind.PullRequest || string.IsNullOrWhiteSpace(request.RemoteNodeId))
                        throw Permanent("Review-Ursprung fehlt.");
                }
                else _ = ReactionEndpoint(request);
                break;
            case ScmResponseKind.IssueBodyUpdate:
                if (request.Reference.Kind != ScmReferenceKind.Issue)
                    throw Permanent("Issue-Beschreibungen benötigen eine Issue-Referenz.");
                if (string.IsNullOrWhiteSpace(request.Body)) throw Permanent("Neue Issue-Beschreibung fehlt.");
                break;
            case ScmResponseKind.PullRequest:
                if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.HeadBranch) || string.IsNullOrWhiteSpace(request.BaseBranch))
                    throw Permanent("Titel und Branches des Pull Requests fehlen.");
                // Branches sind JSON-Daten; nur ein Branch dieses Repositories kann ausgewählt werden.
                if (request.HeadBranch.Contains(':') || request.BaseBranch.Contains(':')
                    || request.HeadBranch.Any(char.IsControl) || request.BaseBranch.Any(char.IsControl))
                    throw Permanent("Pull-Request-Branches müssen zum gespeicherten Repository gehören.");
                break;
            default: throw Permanent("Unbekannte GitHub-Antwortaktion.");
        }
    }

    private static string ResponseBody(ScmResponseRequest request) => $"{request.Body ?? string.Empty}\n\n{request.Marker}";

    /// <summary>Lädt hostseitig gesammelte Nachweis-Bilder hoch und bettet sie als Markdown ein.
    /// Invariante: Bis zum Kommentar-POST verlässt der Upload-Teilschritt ausschließlich mit
    /// Erfolg, einer Übersprungen-Hinweiszeile, einer <see cref="ScmProviderException"/> ohne
    /// OutcomeUncertain oder einem Abbruch — ein ungewisser oder ungetypter Fehler würde die
    /// Outbox-Aktion reconcile-only („Uncertain") stehen lassen, ohne dass je ein Kommentar
    /// abgeglichen werden könnte.</summary>
    private async Task<string> ComposeBodyAsync(ScmResponseRequest request, IGitHubEventApiClient client,
        ScmEventRepository repository, ScmEventRepository current, CancellationToken ct)
    {
        if (request.Attachments is not { Count: > 0 } attachments) return ResponseBody(request);
        // Den undokumentierten uploads.github.com-Endpoint gibt es nur auf github.com: Auf
        // anderen Hosts (z. B. GHES) degradiert der Nachweis zu Hinweiszeilen im Kommentar,
        // statt die ganze Aktion permanent scheitern zu lassen.
        var uploadsSupported = repository.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase);
        string? repositoryId = null;
        if (uploadsSupported)
        {
            repositoryId = current.NumericRepositoryId;
            if (string.IsNullOrEmpty(repositoryId))
            {
                try
                {
                    repositoryId = String(await EventApiAsync(client, repository.Host, "GET",
                        RepositoryEndpoint(repository), null, false, ct), "id");
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (ScmProviderException) { throw; }
                catch (Exception)
                {
                    // Invariante wie beim Upload: kein ungetypter Fehler zwischen Marker-Reconcile
                    // und Kommentar-POST — die Outbox-Aktion dürfte sonst reconcile-only
                    // („Uncertain") verbleiben, ohne dass je ein Kommentar abgeglichen würde.
                    throw new ScmProviderException(ScmProviderFailureKind.Temporary, "Repository-Kennung konnte nicht ermittelt werden.");
                }
            }
            if (string.IsNullOrEmpty(repositoryId)) throw Permanent("Numerische Repository-Kennung fehlt.");
        }
        var embeds = new List<string>();
        var skipped = new List<string>();
        foreach (var attachment in attachments)
        {
            ct.ThrowIfCancellationRequested();
            if (!uploadsSupported)
            { skipped.Add($"*Übersprungen: {attachment.Name} (Upload wird für diesen Host nicht unterstützt)*"); continue; }
            byte[]? data;
            try
            {
                // Vertikale Verteidigung (TOCTOU): Die Pfadsicherheit wird unmittelbar vor dem
                // Lesen erneut geprüft — der Agent könnte seit dem Einsammeln eine Junction
                // auf einen fremden Pfad gelegt haben. Fehler werden zum Übersprungen-Hinweis.
                EnsureUploadPathSafe(attachment.Path);
                var info = new FileInfo(attachment.Path);
                data = info.Exists && info.Length is > 0 and <= GitHubHttpApiClient.MaxUploadBytes
                    ? await File.ReadAllBytesAsync(attachment.Path, ct)
                    : null;
                if (data is null || data.Length != info.Length)
                { skipped.Add($"*Übersprungen: {attachment.Name} (Datei nicht lesbar)*"); continue; }
                // Vertikale Verteidigung (TOCTOU): Der Inhalt darf seit dem hostseitigen
                // Einsammeln nicht gegen ein Nicht-Bild ausgetauscht worden sein — die
                // Signatur wird am tatsächlich gesendeten Puffer erneut geprüft.
                if (!HasImageSignature(attachment.ContentType, data))
                { skipped.Add($"*Übersprungen: {attachment.Name} (Datei-Signatur passt nicht zum Bildformat)*"); continue; }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception)
            {
                skipped.Add($"*Übersprungen: {attachment.Name} (Datei nicht lesbar)*");
                continue;
            }
            try
            {
                var response = await SendUploadApiAsync(client,
                    new(repository.Host, repositoryId!, attachment.Name, attachment.ContentType, data), ct);
                var url = String(ParseJson(response.Body), "url");
                if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https"
                    || !uri.IsDefaultPort || !string.IsNullOrEmpty(uri.UserInfo)
                    || !uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase))
                    throw Permanent("GitHub-Attachment-Antwort enthält keine verwertbare Adresse.");
                embeds.Add($"![{attachment.Name}]({url})");
                if (attachment.Caption is { } caption && SanitizeCaption(caption) is { Length: > 0 } clean)
                    embeds.Add($"*{clean}*");
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (ScmProviderException) { throw; }
            catch (Exception)
            {
                throw new ScmProviderException(ScmProviderFailureKind.Temporary, "Upload-Antwort nicht auswertbar.");
            }
        }
        // Die Überschrift folgt dem tatsächlichen Ergebnis, nicht nur der eingesammelten Menge:
        // Sind alle Dateien erst im Upload-Schritt übersprungen worden (z. B. GHES, verschwundene
        // oder zwischenzeitlich ausgetauschte Dateien), verspricht der Kommentar keine Nachweise.
        var body = request.Body ?? string.Empty;
        const string attachmentsTitle = "Screenshot-Nachweise des abgeschlossenen Laufs.";
        if (embeds.Count == 0 && body.StartsWith(attachmentsTitle, StringComparison.Ordinal))
            body = "Die Screenshot-Nachweise des abgeschlossenen Laufs konnten nicht hochgeladen werden."
                + body[attachmentsTitle.Length..];
        return $"{body}\n\n{string.Join("\n", embeds.Concat(skipped))}\n\n{request.Marker}";
    }

    /// <summary>Upload-Variante von <see cref="SendEventApiAsync"/> mit derselben Status-Matrix,
    /// aber bewusst ohne OutcomeUncertain — der Upload-Schritt ist bewusst wiederholbar.</summary>
    private async Task<GitHubEventApiResponse> SendUploadApiAsync(IGitHubEventApiClient client,
        GitHubAssetUploadRequest request, CancellationToken ct)
    {
        GitHubEventApiResponse response;
        try { response = await client.UploadAsync(request, ct); }
        catch (ScmProviderException ex) when (ex.OutcomeUncertain)
        { throw new ScmProviderException(ex.FailureKind, ex.Message, ex.RetryAfterUtc); }
        catch (ScmProviderException) { throw; }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex is IOException or HttpRequestException or TimeoutException)
        { throw new ScmProviderException(ScmProviderFailureKind.Temporary, "GitHub-Verbindung unterbrochen."); }
        catch (Exception)
        {
            // Restfehler einer Transportimplementierung dürfen nie ungetypt durchschlagen —
            // dieselbe Sending-Fenster-Invariante wie in ComposeBodyAsync.
            throw new ScmProviderException(ScmProviderFailureKind.Temporary, "Attachment-Upload konnte nicht ausgeführt werden.");
        }
        return EvaluateResponse(response, $"HTTP {response.StatusCode}, Attachment-Upload", uncertain: false);
    }

    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] JpegSignature = [0xFF, 0xD8, 0xFF];

    /// <summary>Magische Bytes der zugelassenen Bildtypen — dieselbe Prüffamilie wie beim
    /// hostseitigen Einsammeln, hier am finalen Upload-Puffer wiederholt.</summary>
    private static bool HasImageSignature(string contentType, ReadOnlySpan<byte> head) => contentType switch
    {
        "image/png" => head.StartsWith(PngSignature),
        "image/jpeg" => head.StartsWith(JpegSignature),
        "image/gif" => head.StartsWith("GIF87a"u8) || head.StartsWith("GIF89a"u8),
        "image/webp" => head.Length >= 12 && head.StartsWith("RIFF"u8) && head[8..12].SequenceEqual("WEBP"u8),
        _ => false
    };

    /// <summary>Verweigert Reparse-Points an allen existierenden Pfadbestandteilen —
    /// lokale Variante der Einsammel-Prüfung (das Austauschprotokoll steht im Plugin
    /// nicht zur Verfügung).</summary>
    private static void EnsureUploadPathSafe(string path)
    {
        for (var current = Path.GetFullPath(path); !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
            if (Path.Exists(current) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Symlinks und Reparse-Points sind für den Upload nicht zulässig.");
    }

    private static string SanitizeCaption(string caption)
    {
        // Der agentenkontrollierte Text wird auf eine restriktive Zeichen-Whitelist
        // reduziert: Unicode-Buchstaben/-Ziffern und harmlose Satzzeichen bleiben
        // erhalten, alles andere wird zu einem Leerzeichen. Damit können weder
        // Steuerzeichen und Unicode-Zeilenumbrüche (z. B. U+2028) noch HTML-/SCM-
        // Kommentarstrukturen (<!-Junctions inklusive), Markdown-Elemente
        // ([…](…), `, *, _, \), Mentions (@) oder andere Marker den öffentlichen
        // Kommentar verändern — der Text selbst bleibt sichtbar und lesbar.
        var mapped = new string(caption.Select(c => IsCaptionChar(c) ? c : ' ').ToArray());
        var clean = string.Join(' ', mapped.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return clean.Length > MaximumCaptionChars ? clean[..MaximumCaptionChars] : clean;
    }

    private static bool IsCaptionChar(char c) => char.IsLetterOrDigit(c)
        || c is ' ' or '.' or ',' or ';' or ':' or '!' or '?' or '-' or '–' or '—' or '…'
            or '+' or '=' or '/' or '\'' or '‘' or '’' or '‚' or '"' or '„' or '“' or '”' or '«' or '»';

    private static string ReactionEndpoint(ScmResponseRequest request)
    {
        var prefix = RepositoryEndpoint(request.Reference.Repository);
        if (request.ContentKind == ScmContentKind.Description)
            return $"{prefix}/issues/{request.Reference.Number}/reactions";
        if (request.ContentKind is not (ScmContentKind.IssueComment or ScmContentKind.ReviewComment)
            || !long.TryParse(request.RemoteCommentId, out var id) || id <= 0)
            throw Permanent("Kommentar-Ursprung für die Reaktion fehlt.");
        if (request.ContentKind == ScmContentKind.ReviewComment && request.Reference.Kind != ScmReferenceKind.PullRequest)
            throw Permanent("Review-Kommentar benötigt eine Pull-Request-Referenz.");
        return $"{prefix}/{(request.ContentKind == ScmContentKind.ReviewComment ? "pulls" : "issues")}/comments/{id}/reactions";
    }

    private static string GraphReaction(string content) => content switch
    {
        "+1" => "THUMBS_UP",
        "-1" => "THUMBS_DOWN",
        "laugh" => "LAUGH",
        "confused" => "CONFUSED",
        "heart" => "HEART",
        "hooray" => "HOORAY",
        "rocket" => "ROCKET",
        "eyes" => "EYES",
        _ => throw Permanent("Unbekannte GitHub-Reaktion.")
    };

    private static string? SafeResponseUrl(ScmResponseRequest request, JsonElement result)
    {
        var url = String(result, "html_url");
        if (url is null) return null;
        var repo = request.Reference.Repository;
        return Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == "https" && uri.IsDefaultPort
            && uri.Host.Equals(repo.Host, StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(uri.UserInfo)
            && uri.AbsolutePath.StartsWith($"/{repo.Owner}/{repo.Name}/", StringComparison.OrdinalIgnoreCase) ? url : null;
    }
}
