using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;
using System.Text.Json;
using Softwareschmiede.Domain.Abstractions;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Infrastructure.Plugins;

/// <summary>
/// GitHub Plugin – REST-/GraphQL-API-Zugriffe (alle Ereignis- und Lesepfade) laufen prozesslos über
/// <see cref="IGitHubEventApiClient"/>; die gh CLI bleibt für die verbleibenden Mutationen
/// (<c>issue create</c>, <c>pr create</c>, <c>pr merge</c>) und die git CLI für lokale
/// Arbeitskopien-Operationen im Einsatz.
/// Der GitHub-Token wird als GH_TOKEN Umgebungsvariable bzw. Authorization-Header übergeben,
/// niemals als CLI-Argument oder in URLs.
/// </summary>
public sealed partial class GitHubPlugin : GitPluginBase<GitHubPlugin>, IScmEventSource, IScmEventResponder, IScmAgentAccess, IScmNotificationSource
{
    private const string GitHubTokenCredentialKey = "Softwareschmiede.GitHub.Token";
    private const string GitHubAgentTokenCredentialKey = "Softwareschmiede.GitHub.AgentToken";
    private const string RepositoryUrlKey = "RepositoryUrl";
    private const string RepositoryNameKey = "RepositoryName";
    private static readonly Regex EmbeddedTokenPattern = new(@"oauth2:[^@\s]+@", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly string[] LegacyNetrcHosts = ["github.com", "api.github.com"];

    /// <summary>
    /// Exakter Wert, den Altversionen des Plugins als unsicheres <c>core.sshCommand</c> in betreute
    /// Arbeitskopien geschrieben haben. Nur dieser Wert wird bei der Bestandsbereinigung entfernt —
    /// ein vom Anwender bewusst gesetztes eigenes <c>core.sshCommand</c> (auch mit teilweise
    /// identischen Optionen plus eigenen Flags) bleibt unangetastet.
    /// </summary>
    private const string LegacyInsecureSshCommand = "ssh -o StrictHostKeyChecking=no -o UserKnownHostsFile=/dev/null";

    /// <summary>
    /// Name der optionalen Umgebungsvariable, mit der der Pfad der <c>_netrc</c>/<c>.netrc</c>-Datei
    /// für die einmalige Legacy-Bereinigung übersteuert werden kann (Testseam für Kontexte, in denen
    /// der <c>netrcPath</c>-Konstruktorparameter nicht erreichbar ist — z. B. die Plugin-Discovery über
    /// <c>ActivatorUtilities</c>). Leere Werte werden ignoriert; der explizite Konstruktorparameter
    /// hat Vorrang, danach greift diese Variable, zuletzt das Benutzerprofil.
    /// </summary>
    internal const string NetrcPathEnvironmentVariable = "SOFTWARESCHMIEDE_NETRC_PATH";
    private readonly ICliRunner _cliRunner;
    private readonly ICredentialStore _credentialStore;
    private readonly ILogger<GitHubPlugin> _logger;
    private readonly string? _netrcPath;

    /// <inheritdoc/>
    public override string PluginName => "GitHub";

    /// <inheritdoc/>
    public override string PluginPrefix => "Softwareschmiede.GitHub";

    /// <inheritdoc/>
    public override PluginType PluginType => PluginType.SourceCodeManagement;

    /// <inheritdoc/>
    public override string PluginSymbol => "🐙";

    /// <inheritdoc/>
    public override IReadOnlyList<PluginSettingGroup> GetSettingGroups() =>
    [
        new PluginSettingGroup("Authentifizierung",
        [
            new PluginSettingField(
                Key: "Token",
                Label: "Personal Access Token",
                FieldType: PluginSettingFieldType.Secret,
                Placeholder: "ghp_...",
                Description: "GitHub Personal Access Token mit den Berechtigungen repo, read:org und Zugriff auf Code-Scanning-Alerts. Token erstellen: https://github.com/settings/tokens/new",
                IsRequired: true),
            new PluginSettingField(
                Key: "AgentToken",
                Label: "Personal Access Token des separaten Kontos",
                FieldType: PluginSettingFieldType.Secret,
                Placeholder: "ghp_...",
                Description: "GitHub Personal Access Token eines separaten Kontos für den Kontomodus „Separates Konto“ (Scopes: notifications und repo). Erforderlich, sobald der Kontomodus „Separates Konto“ gewählt ist.",
                IsRequired: false)
        ]),
        new PluginSettingGroup("Pull Requests",
        [
            new PluginSettingField(
                Key: "AutoCompletePullRequests",
                Label: "Automatischer PR-Abschluss",
                FieldType: PluginSettingFieldType.Boolean,
                Description: "Pull Requests nach erfolgreichen zugeordneten Actions automatisch abschliessen.",
                DefaultValue: "false"),
            new PluginSettingField(
                Key: "PullRequestCompletionStrategy",
                Label: "Abschlussstrategie",
                FieldType: PluginSettingFieldType.Enum,
                Description: "Strategie fuer den automatischen PR-Abschluss.",
                EnumOptions: Enum.GetNames<PullRequestCompletionStrategy>(),
                DefaultValue: PullRequestCompletionStrategy.Merge.ToString()),
            new PluginSettingField(
                Key: "PullRequestMergeMethod",
                Label: "Merge-Methode",
                FieldType: PluginSettingFieldType.Enum,
                Description: "Merge-Methode fuer direkte PR-Merges.",
                EnumOptions: Enum.GetNames<PullRequestMergeMethod>(),
                DefaultValue: PullRequestMergeMethod.Squash.ToString()),
            new PluginSettingField(
                Key: "AllowProtectedBranchBypass",
                Label: "Protected-Branch-Bypass erlauben",
                FieldType: PluginSettingFieldType.Boolean,
                Description: "Erlaubt Abschlussversuche mit administrativem Bypass, falls GitHub und Token das unterstuetzen.",
                DefaultValue: "false")
        ])
    ];

    /// <inheritdoc/>
    public override IReadOnlyList<PluginSettingField> GetRepositoryLinkFields() =>
    [
        new PluginSettingField(
            Key: RepositoryUrlKey,
            Label: "Repository-URL",
            FieldType: PluginSettingFieldType.Url,
            Placeholder: "https://github.com/owner/repo",
            Description: "Vollständige URL des GitHub-Repositories.",
            IsRequired: true),
        new PluginSettingField(
            Key: RepositoryNameKey,
            Label: "Repository-Name",
            FieldType: PluginSettingFieldType.Text,
            Placeholder: "owner/repo",
            Description: "Repository-ID für API-Aufrufe und Pull-Requests.",
            IsRequired: true)
    ];

    /// <summary>Erstellt eine neue Instanz des <see cref="GitHubPlugin"/>.</summary>
    public GitHubPlugin(ICliRunner cliRunner, ICredentialStore credentialStore, ILogger<GitHubPlugin> logger)
        : this(cliRunner, credentialStore, logger, null)
    {
    }

    /// <summary>Erstellt den Provider mit austauschbarem Ereignis-API-Transport.</summary>
    public GitHubPlugin(ICliRunner cliRunner, ICredentialStore credentialStore, ILogger<GitHubPlugin> logger,
        IGitHubEventApiClient? eventApiClient)
        : this(cliRunner, credentialStore, logger, eventApiClient, null)
    {
    }

    /// <summary>Erstellt den Provider mit austauschbaren Ereignis-API-Transporten beider Konten.</summary>
    /// <param name="netrcPath">Optionaler Pfad-Override der <c>_netrc</c>/<c>.netrc</c>-Datei für die
    /// einmalige Legacy-Bereinigung (Testseam); <see langword="null"/> ist der Produktiv-Default und
    /// löst den Pfad über <see cref="NetrcPathEnvironmentVariable"/> bzw. das Benutzerprofil auf.</param>
    public GitHubPlugin(ICliRunner cliRunner, ICredentialStore credentialStore, ILogger<GitHubPlugin> logger,
        IGitHubEventApiClient? eventApiClient, IGitHubEventApiClient? agentEventApiClient, string? netrcPath = null)
        : base(cliRunner)
    {
        _cliRunner = cliRunner;
        _credentialStore = credentialStore;
        _logger = logger;
        _netrcPath = netrcPath;
        _eventApiClient = eventApiClient ?? new GitHubHttpApiClient(() => _credentialStore.GetCredential(GitHubTokenCredentialKey));
        _agentEventApiClient = agentEventApiClient ?? new GitHubHttpApiClient(() => _credentialStore.GetCredential(GitHubAgentTokenCredentialKey));

        RemoveLegacyNetrcEntries();
    }

    private IDictionary<string, string> GetGhEnvironment()
    {
        var token = _credentialStore.GetCredential(GitHubTokenCredentialKey);
        var env = new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(token))
        {
            env["GH_TOKEN"] = token;
        }
        return env;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Stellt dem Agenten dieselbe gh-/git-Anmeldung zur Verfügung, die der Host für seine
    /// eigenen Aufrufe nutzt (<see cref="GetGhEnvironment"/>): <c>GH_TOKEN</c> für die
    /// <c>gh</c>-CLI, ein <c>http.extraheader</c>-Eintrag für HTTPS-git-Operationen gegen
    /// github.com (z. B. Push nach lokal behobenem Konflikt) sowie
    /// <c>GIT_TERMINAL_PROMPT=0</c>, damit git bei fehlenden Credentials sauber fehlschlägt
    /// statt interaktiv zu blockieren. Die Werte werden ausschließlich als Prozessumgebung
    /// des CLI-Kindprozesses gesetzt — niemals in Prompts, Logs oder Persistenz.
    /// </remarks>
    public Task<IReadOnlyDictionary<string, string?>> GetAgentEnvironmentAsync(ScmKontoModus konto, CancellationToken ct = default)
    {
        var environment = new Dictionary<string, string?>
        {
            // Kein interaktives Credential-Prompt: git schlägt ohne verwertbare Anmeldung fehl,
            // statt die Agenten-Sitzung zu blockieren.
            ["GIT_TERMINAL_PROMPT"] = "0",
        };
        var token = _credentialStore.GetCredential(CredentialKeyFor(konto));
        if (!string.IsNullOrEmpty(token))
        {
            environment["GH_TOKEN"] = token;
            AddGitHubHttpAuthHeader(environment, token);
        }
        return Task.FromResult<IReadOnlyDictionary<string, string?>>(environment);
    }

    /// <summary>
    /// Setzt die HTTPS-git-Authentifizierung für github.com als <c>http.extraheader</c> über die
    /// <c>GIT_CONFIG_*</c>-Prozessumgebung — dasselbe Basic-Authorization-Schema, das
    /// GitHub-Actions-Checkouts verwenden. Der Token bleibt rein prozessbasiert und wird
    /// niemals in URLs, CLI-Argumenten oder Dateien persistiert.
    /// </summary>
    private static void AddGitHubHttpAuthHeader(IDictionary<string, string?> environment, string token)
    {
        var credentials = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("x-access-token:" + token));
        environment["GIT_CONFIG_COUNT"] = "1";
        environment["GIT_CONFIG_KEY_0"] = "http.https://github.com/.extraheader";
        environment["GIT_CONFIG_VALUE_0"] = "AUTHORIZATION: basic " + credentials;
    }

    private IDictionary<string, string> GetGitEnvironment(string? token = null)
    {
        token ??= _credentialStore.GetCredential(GitHubTokenCredentialKey);
        var env = new Dictionary<string, string>();

        // Disable terminal prompts completely
        env["GIT_TERMINAL_PROMPT"] = "0";

        if (!string.IsNullOrEmpty(token))
        {
            // Programm-Token für git-Authentifizierung bereitstellen, ohne ihn in URLs einzubetten
            env["GH_TOKEN"] = token;

            // HTTPS-Auth gegen github.com rein prozessbasiert über http.extraheader —
            // es wird kein Token mehr nach _netrc/.netrc persistiert.
            // Expliziter Cast: env trägt nur nicht-leere Werte, der Helfer arbeitet auf
            // dem nullability-toleranten IDictionary<string, string?>.
            AddGitHubHttpAuthHeader((IDictionary<string, string?>)env, token);
        }

        return env;
    }

    /// <summary>
    /// Ermittelt den Pfad der <c>_netrc</c>/<c>.netrc</c>-Datei: expliziter
    /// <c>netrcPath</c>-Konstruktorparameter, sonst die Umgebungsvariable
    /// <see cref="NetrcPathEnvironmentVariable"/> (leere Werte werden ignoriert), sonst das
    /// Benutzerprofil. Die Umgebungsvariable ist die Testseam für Instanziierungen, bei denen der
    /// Konstruktorparameter nicht erreichbar ist (Plugin-Discovery via <c>ActivatorUtilities</c>).
    /// </summary>
    private string ResolveNetrcPath()
    {
        if (_netrcPath is not null)
        {
            return _netrcPath;
        }

        var environmentOverride = Environment.GetEnvironmentVariable(NetrcPathEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(environmentOverride))
        {
            return environmentOverride;
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            OperatingSystem.IsWindows() ? "_netrc" : ".netrc");
    }

    /// <summary>
    /// Entfernt einmalig pro Plugin-Instanziierung die von Altversionen geschriebenen
    /// <c>machine github.com</c>/<c>machine api.github.com</c>-Stanzas aus einer existierenden
    /// <c>_netrc</c>. Nur Fremd-Host-Einträge (z. B. des BitBucket-Plugins oder manuell
    /// gepflegte Stanzas anderer Hosts) bleiben unverändert — manuell gepflegte
    /// GitHub-Stanzas werden ebenfalls entfernt, da sie von plugin-geschriebenen nicht
    /// unterscheidbar sind. Fehler werden nur geloggt und brechen den Ablauf nie ab.
    /// </summary>
    /// <remarks>
    /// Der Read-Modify-Write läuft unter exklusivem Dateizugriff (<see cref="FileShare.None"/>):
    /// Ein gleichzeitiger Fremd-Lese-/Schreibzugriff (z. B. <c>BitBucketPlugin.UpdateNetrcEntry</c>,
    /// das die Datei bei jedem BitBucket-git-Aufruf ebenfalls nicht-atomar verändert) kann
    /// innerhalb dieses Fensters nicht mehr interleaven — er schlägt mit einer
    /// <see cref="IOException"/> fehl, statt eine veraltete Version zurückzuschreiben.
    /// Verbleibende Einschränkung: Der Exklusivzugriff gilt nur für das eigene
    /// Read-Modify-Write-Fenster; ein Fremd-Writer kann die Datei vor dem Öffnen oder
    /// zwischen dem Schließen des Handles und dem <see cref="File.Delete(string)"/> erneut
    /// verändern — vollständige Atomizität bzw. ein prozessübergreifender Lock besteht nicht.
    /// </remarks>
    private void RemoveLegacyNetrcEntries()
    {
        try
        {
            var netrcPath = ResolveNetrcPath();
            if (!File.Exists(netrcPath) && !Directory.Exists(netrcPath))
            {
                return;
            }

            // Erst nur lesend prüfen: Eine _netrc ohne GitHub-Stanzas bleibt auch dann
            // warnfrei unverändert, wenn sie schreibgeschützt ist.
            var cleanupRequired = false;
            using (var previewStream = new FileStream(netrcPath, FileMode.Open, FileAccess.Read, FileShare.None))
            using (var reader = new StreamReader(previewStream))
            {
                cleanupRequired = RemoveNetrcStanzas(reader.ReadToEnd(), LegacyNetrcHosts) is not null;
            }

            if (!cleanupRequired)
            {
                return;
            }

            var deleteFile = false;
            using (var stream = new FileStream(netrcPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                string content;
                System.Text.Encoding encoding;
                using (var reader = new StreamReader(stream, detectEncodingFromByteOrderMarks: true, leaveOpen: true))
                {
                    content = reader.ReadToEnd();
                    encoding = reader.CurrentEncoding;
                }

                var remaining = RemoveNetrcStanzas(content, LegacyNetrcHosts);
                if (remaining is null)
                {
                    return;
                }

                if (string.IsNullOrWhiteSpace(remaining))
                {
                    // Löschen erst nach dem Schließen: das eigene Handle ist ohne
                    // FileShare.Delete geöffnet, File.Delete würde an der eigenen Sperre scheitern.
                    deleteFile = true;
                }
                else
                {
                    stream.SetLength(0);
                    stream.Position = 0;
                    using (var writer = new StreamWriter(stream, encoding, bufferSize: 1024, leaveOpen: true))
                    {
                        writer.Write(remaining);
                    }
                }
            }

            if (deleteFile)
            {
                File.Delete(netrcPath);
                _logger.LogInformation("_netrc nach Entfernen der GitHub-Legacy-Einträge gelöscht: {Path}", netrcPath);
            }
            else
            {
                _logger.LogInformation("GitHub-Legacy-Einträge aus _netrc entfernt: {Path}", netrcPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "_netrc konnte nicht von GitHub-Legacy-Einträgen bereinigt werden");
        }
    }

    /// <summary>
    /// Entfernt aus <paramref name="content"/> alle Stanzas, deren <c>machine</c>-Zeile auf einen der
    /// übergebenen Hosts trifft — jeweils die komplette <c>machine</c>-Zeile plus alle Folgezeilen
    /// bis zur nächsten <c>machine</c>- oder <c>default</c>-Zeile. Die Erkennung tokenisiert jede
    /// Zeile (beliebige Whitespace-Trenner, case-insensitiv): <c>machine\tgithub.com</c>,
    /// Mehrfach-Leerzeichen und Inline-Tokens wie <c>machine github.com login … password …</c>
    /// werden erkannt und die ganze Zeile entfernt; <c>default</c> beendet eine laufende Stanza
    /// als Blockgrenze. Liefert <see langword="null"/>, wenn kein Eintrag betroffen war, sonst
    /// den bereinigten Inhalt mit unverändertem Rest (inklusive finaler Zeilenschaltung).
    /// </summary>
    private static string? RemoveNetrcStanzas(string content, IReadOnlyCollection<string> hosts)
    {
        var lines = content.Split('\n');
        var result = new List<string>(lines.Length);
        var removed = false;
        var skipping = false;

        foreach (var line in lines)
        {
            var tokens = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length > 0
                && tokens[0].Equals("machine", StringComparison.OrdinalIgnoreCase))
            {
                skipping = tokens.Length > 1
                    && hosts.Any(host => tokens[1].Equals(host, StringComparison.OrdinalIgnoreCase));
                removed |= skipping;
            }
            else if (tokens.Length > 0
                && tokens[0].Equals("default", StringComparison.OrdinalIgnoreCase))
            {
                skipping = false;
            }

            if (!skipping)
            {
                result.Add(line);
            }
        }

        // Die finale Zeilenschaltung des Restinhalts erhalten: endete die Datei innerhalb
        // einer entfernten Stanza, ging das abschließende leere Split-Element verloren.
        if (removed && skipping && content.EndsWith('\n'))
        {
            result.Add(string.Empty);
        }

        return removed ? string.Join('\n', result) : null;
    }

    private static bool IsHttpsRepositoryUrl(string repositoryUrl)
        => Uri.TryCreate(repositoryUrl, UriKind.Absolute, out var uri)
           && string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);

    private static bool IsAuthenticationFailure(string error)
    {
        var normalizedError = error.ToLowerInvariant();
        return normalizedError.Contains("terminal prompts disabled", StringComparison.Ordinal)
               || normalizedError.Contains("could not read username", StringComparison.Ordinal)
               || normalizedError.Contains("authentication failed", StringComparison.Ordinal)
               || normalizedError.Contains("invalid username or password", StringComparison.Ordinal)
               || normalizedError.Contains("support for password authentication was removed", StringComparison.Ordinal)
               || normalizedError.Contains("403", StringComparison.Ordinal)
               || normalizedError.Contains("access denied", StringComparison.Ordinal)
               || normalizedError.Contains("insufficient", StringComparison.Ordinal);
    }

    private string SanitizeSensitiveOutput(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return "unbekannter Fehler";
        }

        var sanitizedMessage = message;

        foreach (var credentialKey in new[] { GitHubTokenCredentialKey, GitHubAgentTokenCredentialKey })
        {
            var token = _credentialStore.GetCredential(credentialKey);
            if (!string.IsNullOrWhiteSpace(token))
            {
                sanitizedMessage = sanitizedMessage.Replace(token, "***", StringComparison.Ordinal);
            }
        }

        sanitizedMessage = EmbeddedTokenPattern.Replace(sanitizedMessage, "oauth2:***@");

        return sanitizedMessage.Trim();
    }

    private static bool IsNoCommitsBetweenFailure(string error)
    {
        var normalizedError = error.ToLowerInvariant();
        return normalizedError.Contains("no commits between", StringComparison.Ordinal)
               || (normalizedError.Contains("head sha", StringComparison.Ordinal)
                   && normalizedError.Contains("blank", StringComparison.Ordinal)
                   && normalizedError.Contains("base sha", StringComparison.Ordinal));
    }

    private async Task NormalizeRemoteUrlAsync(string localPath, CancellationToken ct = default)
    {
        try
        {
            // Get current remote URL
            var getUrlResult = await _cliRunner.RunAsync(
                "git",
                ["config", "remote.origin.url"],
                localPath,
                null,
                ct);

            if (getUrlResult?.IsSuccess != true)
            {
                _logger.LogWarning("Konnte remote.origin.url nicht abrufen: {Error}", getUrlResult?.StdErr);
                return;
            }

            var currentUrl = getUrlResult.StdOut.Trim();

            // Legacy-Repositories können noch einen eingebetteten Token aus früheren Klones enthalten.
            // Diese werden entfernt, damit keine Klartext-Tokens in der Remote-URL verbleiben.
            var normalizedUrl = EmbeddedTokenPattern.Replace(currentUrl, string.Empty);

            // SSH-Remote-URLs aus Altversionen werden auf HTTPS normalisiert — der
            // plugin-geführte git-Transport läuft ausschließlich über HTTPS.
            normalizedUrl = ToHttpsRepositoryUrl(normalizedUrl) ?? normalizedUrl;

            if (string.Equals(normalizedUrl, currentUrl, StringComparison.Ordinal))
            {
                return;
            }

            var setUrlResult = await _cliRunner.RunAsync(
                "git",
                ["remote", "set-url", "origin", normalizedUrl],
                localPath,
                null,
                ct);

            if (setUrlResult?.IsSuccess != true)
            {
                _logger.LogWarning("Konnte remote.origin.url nicht normalisieren: {Error}", setUrlResult?.StdErr);
            }
            else
            {
                _logger.LogInformation("Remote origin URL normalisiert (HTTPS, ohne eingebettetes Token).");
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "remote.origin.url konnte nicht normalisiert werden.");
        }
    }

    /// <summary>
    /// Entfernt das von Altversionen des Plugins in betreute Arbeitskopien geschriebene,
    /// unsichere <c>core.sshCommand</c> per <c>git config --unset</c>. Wertbasiert: nur der
    /// exakt plugin-geschriebene Wert (<see cref="LegacyInsecureSshCommand"/>) wird entfernt —
    /// ein vom Anwender bewusst gesetztes eigenes <c>core.sshCommand</c>, auch wenn es die
    /// Option <c>StrictHostKeyChecking=no</c> neben weiteren eigenen Flags enthält, bleibt
    /// unverändert. Fehler werden nur geloggt und brechen die umgebende git-Operation nie ab.
    /// </summary>
    private async Task RemoveInsecureSshCommandAsync(string localPath, CancellationToken ct = default)
    {
        try
        {
            var getResult = await _cliRunner.RunAsync(
                "git",
                ["config", "--get", "core.sshCommand"],
                localPath,
                null,
                ct);

            if (getResult?.IsSuccess != true
                || !string.Equals(getResult.StdOut.Trim(), LegacyInsecureSshCommand, StringComparison.Ordinal))
            {
                return;
            }

            var unsetResult = await _cliRunner.RunAsync(
                "git",
                ["config", "--unset", "core.sshCommand"],
                localPath,
                null,
                ct);

            if (unsetResult?.IsSuccess == true)
            {
                _logger.LogInformation("Unsicheren core.sshCommand-Eintrag aus der Arbeitskopie entfernt.");
            }
            else
            {
                _logger.LogWarning("core.sshCommand konnte nicht entfernt werden: {Error}", unsetResult?.StdErr);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "core.sshCommand konnte nicht geprüft oder entfernt werden.");
        }
    }

    private async Task ConfigureGitCredentialsAsync(string localPath, string repositoryUrl, CancellationToken ct = default)
    {
        var token = _credentialStore.GetCredential(GitHubTokenCredentialKey);
        if (string.IsNullOrEmpty(token))
        {
            _logger.LogWarning("Kein GitHub-Token verfügbar für Git-Konfiguration.");
            return;
        }

        var gitEnvironment = GetGitEnvironment(token);

        // Configure user name and email for commits
        var userNameResult = await _cliRunner.RunAsync(
            "git",
            ["config", "user.name", "Softwareschmiede Bot"],
            localPath,
            gitEnvironment,
            ct);

        if (!userNameResult.IsSuccess)
        {
            _logger.LogWarning("Git user.name Konfiguration fehlgeschlagen: {Error}", userNameResult.StdErr);
        }

        var userEmailResult = await _cliRunner.RunAsync(
            "git",
            ["config", "user.email", "bot@softwareschmiede.local"],
            localPath,
            gitEnvironment,
            ct);

        if (!userEmailResult.IsSuccess)
        {
            _logger.LogWarning("Git user.email Konfiguration fehlgeschlagen: {Error}", userEmailResult.StdErr);
        }

        // Remote-URL bleibt unauthentifiziert – der Token wird ausschließlich über GH_TOKEN
        // (GetGitEnvironment()) bzw. http.extraheader bereitgestellt, niemals in der URL eingebettet.
        if (repositoryUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            var setUrlResult = await _cliRunner.RunAsync(
                "git",
                ["remote", "set-url", "origin", repositoryUrl],
                localPath,
                gitEnvironment,
                ct);

            if (!setUrlResult.IsSuccess)
            {
                _logger.LogWarning("Git remote URL Konfiguration fehlgeschlagen: {Error}", setUrlResult.StdErr);
            }
            else
            {
                _logger.LogInformation("Git remote origin URL aktualisiert (ohne eingebettetes Token).");
            }
        }
    }

    /// <inheritdoc/>
    public override async Task<IEnumerable<Issue>> GetIssuesAsync(string repositoryId, CancellationToken ct = default)
    {
        var normalizedRepositoryId = NormalizeRepositoryId(repositoryId);
        if (string.IsNullOrWhiteSpace(normalizedRepositoryId))
        {
            return [];
        }

        _logger.LogInformation("Rufe Issues für Repository {RepositoryId} ab.", normalizedRepositoryId);

        try
        {
            var (host, owner, name) = ParseEventRepository(normalizedRepositoryId);
            var items = await RestPagesAsync(_eventApiClient, host,
                $"repos/{owner}/{name}/issues?state=open&per_page=100", ct);
            return ParseIssues(items);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            _logger.LogError("GitHub-Issues konnten für {RepositoryId} nicht geladen werden: {Error}",
                normalizedRepositoryId, SanitizeSensitiveOutput(ex.Message));
            return [];
        }
    }

    /// <inheritdoc/>
    public override async Task<IEnumerable<ScmAlert>> GetAlertsAsync(string repositoryId, CancellationToken ct = default)
    {
        var normalizedRepositoryId = NormalizeRepositoryId(repositoryId);
        if (string.IsNullOrWhiteSpace(normalizedRepositoryId))
        {
            return [];
        }

        _logger.LogInformation("Rufe Code-Scanning-Alerts für Repository {RepositoryId} ab.", normalizedRepositoryId);

        try
        {
            var (host, owner, name) = ParseEventRepository(normalizedRepositoryId);
            var items = await RestPagesAsync(_eventApiClient, host,
                $"repos/{owner}/{name}/code-scanning/alerts?state=open&per_page=100", ct);
            return ParseCodeScanningAlerts(items, normalizedRepositoryId);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (ScmProviderException ex) when (ex.FailureKind == ScmProviderFailureKind.Permanent)
        {
            _logger.LogInformation(
                "Code-Scanning-Alerts für Repository {RepositoryId} nicht verfügbar: {Error}",
                normalizedRepositoryId, ex.Message);
            return [];
        }
        catch (ScmProviderException ex) when (ex.FailureKind is ScmProviderFailureKind.Authentication or ScmProviderFailureKind.Permission)
        {
            _logger.LogWarning(
                "Code-Scanning-Alerts für Repository {RepositoryId} konnten wegen fehlender Rechte nicht geladen werden: {Error}",
                normalizedRepositoryId, ex.Message);
            return [];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                "Code-Scanning-Alerts für Repository {RepositoryId} fehlgeschlagen: {Error}",
                normalizedRepositoryId, SanitizeSensitiveOutput(ex.Message));
            return [];
        }
    }

    /// <inheritdoc/>
    public override Task<bool> CanCreateIssueAsync(string repositoryId, CancellationToken ct = default)
        => Task.FromResult(!string.IsNullOrWhiteSpace(NormalizeRepositoryId(repositoryId)));

    /// <inheritdoc/>
    public override async Task<IssueCreateResult> CreateIssueAsync(
        string repositoryId,
        IssueCreateRequest request,
        CancellationToken ct = default)
    {
        var normalizedRepositoryId = NormalizeRepositoryId(repositoryId);
        if (string.IsNullOrWhiteSpace(normalizedRepositoryId))
        {
            return IssueCreateResult.Failed("Repository-ID fehlt.");
        }

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return IssueCreateResult.Failed("Der Issue-Titel darf nicht leer sein.");
        }

        var result = await _cliRunner.RunAsync(
            "gh",
            [
                "issue", "create",
                "--repo", normalizedRepositoryId,
                "--title", request.Title.Trim(),
                "--body", request.Body ?? string.Empty
            ],
            null,
            GetGhEnvironment(),
            ct);

        if (!result.IsSuccess)
        {
            var sanitizedError = SanitizeSensitiveOutput(result.StdErr);
            _logger.LogError("gh issue create fehlgeschlagen für {RepositoryId}: {StdErr}", normalizedRepositoryId, sanitizedError);
            return IssueCreateResult.Failed($"GitHub-Issue konnte nicht erstellt werden: {sanitizedError}");
        }

        var issueUrl = result.StdOut.Trim();
        if (!TryParseIssueNumber(issueUrl, out var issueNumber))
        {
            return IssueCreateResult.Failed($"GitHub-Issue wurde erstellt, die Antwort konnte aber nicht ausgewertet werden: {issueUrl}");
        }

        return IssueCreateResult.Success(new Issue(
            issueNumber,
            request.Title.Trim(),
            request.Body,
            [],
            null,
            issueUrl));
    }

    /// <inheritdoc/>
    public override async Task<IssueTemplateLoadResult> GetIssueTemplatesAsync(string repositoryId, CancellationToken ct = default)
    {
        var normalizedRepositoryId = NormalizeRepositoryId(repositoryId);
        if (string.IsNullOrWhiteSpace(normalizedRepositoryId))
        {
            return IssueTemplateLoadResult.Success([]);
        }

        GitHubEventApiResponse listResult;
        (string Host, string Owner, string Name) eventRepository;
        try
        {
            eventRepository = ParseEventRepository(normalizedRepositoryId);
            listResult = await _eventApiClient.SendAsync(new(eventRepository.Host, "GET",
                $"repos/{eventRepository.Owner}/{eventRepository.Name}/contents/.github/ISSUE_TEMPLATE"), ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            return IssueTemplateLoadResult.Failed($"GitHub-Issue-Templates konnten nicht geladen werden: {SanitizeSensitiveOutput(ex.Message)}");
        }

        if (listResult.StatusCode == 404)
        {
            return IssueTemplateLoadResult.Success([]);
        }

        if (listResult.StatusCode is < 200 or >= 300)
        {
            return IssueTemplateLoadResult.Failed($"GitHub-Issue-Templates konnten nicht geladen werden (HTTP {listResult.StatusCode}).");
        }

        try
        {
            using var doc = JsonDocument.Parse(listResult.Body);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return IssueTemplateLoadResult.Success([]);
            }

            var templates = new List<IssueTemplate>();
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                var type = item.TryGetProperty("type", out var typeEl) ? typeEl.GetString() : null;
                var name = item.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : null;
                var path = item.TryGetProperty("path", out var pathEl) ? pathEl.GetString() : null;

                if (!string.Equals(type, "file", StringComparison.OrdinalIgnoreCase)
                    || string.IsNullOrWhiteSpace(name)
                    || string.IsNullOrWhiteSpace(path)
                    || !IsSupportedTemplateFile(name))
                {
                    continue;
                }

                var content = await LoadTemplateContentAsync(eventRepository, path, ct);
                if (!string.IsNullOrWhiteSpace(content))
                {
                    templates.Add(new IssueTemplate(name, content));
                }
            }

            return IssueTemplateLoadResult.Success(templates);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Auswerten der GitHub-Issue-Templates für {RepositoryId}.", repositoryId);
            return IssueTemplateLoadResult.Failed("GitHub-Issue-Templates konnten nicht ausgewertet werden.");
        }
    }

    private async Task<string?> LoadTemplateContentAsync((string Host, string Owner, string Name) repository, string path, CancellationToken ct)
    {
        GitHubEventApiResponse result;
        try
        {
            var escapedPath = string.Join('/', path.Split('/').Select(Uri.EscapeDataString));
            result = await _eventApiClient.SendAsync(new(repository.Host, "GET", $"repos/{repository.Owner}/{repository.Name}/contents/{escapedPath}"), ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            _logger.LogWarning("GitHub-Issue-Template {TemplatePath} konnte nicht geladen werden: {Error}", path, SanitizeSensitiveOutput(ex.Message));
            return null;
        }

        if (result.StatusCode is < 200 or >= 300)
        {
            _logger.LogWarning("GitHub-Issue-Template {TemplatePath} konnte nicht geladen werden (HTTP {StatusCode}).", path, result.StatusCode);
            return null;
        }

        using var doc = JsonDocument.Parse(result.Body);
        if (!doc.RootElement.TryGetProperty("content", out var contentEl))
        {
            return null;
        }

        var encoded = contentEl.GetString();
        if (string.IsNullOrWhiteSpace(encoded))
        {
            return null;
        }

        return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(encoded.Replace("\n", string.Empty, StringComparison.Ordinal)));
    }

    private static bool TryParseIssueNumber(string issueUrl, out int issueNumber)
    {
        issueNumber = 0;
        var lastSlashIndex = issueUrl.LastIndexOf('/');
        return lastSlashIndex > 0
               && lastSlashIndex < issueUrl.Length - 1
               && int.TryParse(issueUrl[(lastSlashIndex + 1)..], out issueNumber);
    }

    private static bool IsSupportedTemplateFile(string name)
        => name.EndsWith(".md", StringComparison.OrdinalIgnoreCase);

    private static string? NormalizeRepositoryId(string repositoryId)
    {
        if (string.IsNullOrWhiteSpace(repositoryId))
        {
            return null;
        }

        var extracted = TryExtractRepositoryId(repositoryId);
        return extracted ?? repositoryId.Trim().Trim('/');
    }

    private static IEnumerable<ScmAlert> ParseCodeScanningAlerts(IEnumerable<JsonElement> elements, string repositoryId)
    {
        var alerts = new List<ScmAlert>();
        foreach (var element in elements)
        {
            var number = GetInt32OrDefault(element, "number");
            if (number <= 0)
            {
                continue;
            }

            var rule = TryGetObject(element, "rule");
            var tool = TryGetObject(element, "tool");
            var location = TryGetObject(TryGetObject(element, "most_recent_instance"), "location");
            var message = TryGetObject(element, "most_recent_instance");

            var ruleId = GetStringOrNull(rule, "id");
            var ruleName = GetStringOrNull(rule, "name");
            var description = GetStringOrNull(rule, "description");
            var messageText = GetStringOrNull(TryGetObject(message, "message"), "text");
            var title = FirstNonWhiteSpace(ruleName, ruleId, messageText, $"Code scanning alert #{number}")!;
            var severity = FirstNonWhiteSpace(
                GetStringOrNull(rule, "security_severity_level"),
                GetStringOrNull(rule, "severity"));

            alerts.Add(new ScmAlert(
                AlertNumber: number,
                SourceKey: $"github:code-scanning:{repositoryId}:{number}",
                AlertType: ScmAlertType.CodeScanning,
                Title: title,
                Description: FirstNonWhiteSpace(messageText, description),
                AlertUrl: GetStringOrNull(element, "html_url"),
                Severity: severity,
                State: GetStringOrNull(element, "state"),
                ToolName: GetStringOrNull(tool, "name"),
                RuleId: ruleId,
                RuleName: ruleName,
                FilePath: GetStringOrNull(location, "path"),
                StartLine: GetNullableInt32(location, "start_line")));
        }

        return alerts;
    }

    private static JsonElement? TryGetObject(JsonElement? element, string propertyName)
    {
        if (element is not { ValueKind: JsonValueKind.Object } value
            || !value.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return property;
    }

    private static string? GetStringOrNull(JsonElement? element, string propertyName)
    {
        if (element is not { ValueKind: JsonValueKind.Object } value
            || !value.TryGetProperty(propertyName, out var property)
            || property.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        return property.GetString();
    }

    private static int GetInt32OrDefault(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var property) && property.TryGetInt32(out var value) ? value : 0;

    private static bool GetBoolOrDefault(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.True;

    private static int? GetNullableInt32(JsonElement? element, string propertyName)
        => element is { ValueKind: JsonValueKind.Object } value
           && value.TryGetProperty(propertyName, out var property)
           && property.TryGetInt32(out var intValue)
            ? intValue
            : null;

    private static string? FirstNonWhiteSpace(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static IEnumerable<Issue> ParseIssues(IEnumerable<JsonElement> elements)
    {
        var issues = new List<Issue>();
        foreach (var element in elements)
        {
            // Der Issues-Endpunkt listet auch Pull Requests; diese sind keine Issues.
            if (element.TryGetProperty("pull_request", out var prEl) && prEl.ValueKind == JsonValueKind.Object)
            {
                continue;
            }
            var number = element.GetProperty("number").GetInt32();
            var title = element.GetProperty("title").GetString() ?? string.Empty;
            var body = element.TryGetProperty("body", out var bodyEl) ? bodyEl.GetString() : null;
            var labels = element.TryGetProperty("labels", out var labelsEl)
                ? labelsEl.EnumerateArray()
                    .Select(l => l.TryGetProperty("name", out var n) ? n.GetString() ?? string.Empty : string.Empty)
                    .ToList()
                : new List<string>();
            var milestone = element.TryGetProperty("milestone", out var msEl) && msEl.ValueKind == JsonValueKind.Object
                ? msEl.TryGetProperty("title", out var msTitle) ? msTitle.GetString() : null
                : null;
            var url = element.TryGetProperty("html_url", out var urlEl) ? urlEl.GetString() : null;
            issues.Add(new Issue(number, title, body, labels, milestone, url));
        }
        return issues;
    }

    /// <inheritdoc/>
    public override async Task<IEnumerable<PullRequest>> GetOpenPullRequestsAsync(string repositoryId, CancellationToken ct = default)
    {
        var normalizedRepositoryId = NormalizeRepositoryId(repositoryId);
        if (string.IsNullOrWhiteSpace(normalizedRepositoryId))
        {
            return [];
        }

        if (!PullRequestRepositoryId.TryNormalize(PullRequestProvider.GitHub, normalizedRepositoryId, out normalizedRepositoryId))
        {
            _logger.LogWarning("Pull Requests koennen nicht geladen werden: Repository-ID {RepositoryId} ist ungueltig.", repositoryId);
            return [];
        }

        _logger.LogInformation("Rufe offene Pull Requests fuer Repository {RepositoryId} ab.", normalizedRepositoryId);

        try
        {
            var (host, owner, name) = ParseEventRepository(normalizedRepositoryId);
            var items = await RestPagesAsync(_eventApiClient, host,
                $"repos/{owner}/{name}/pulls?state=open&per_page=100", ct);
            return ParseOpenPullRequests(items, normalizedRepositoryId);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            _logger.LogWarning("Offene Pull Requests fuer {RepositoryId} fehlgeschlagen: {Error}", normalizedRepositoryId, SanitizeSensitiveOutput(ex.Message));
            return [];
        }
    }

    private static IEnumerable<PullRequest> ParseOpenPullRequests(IEnumerable<JsonElement> elements, string repositoryId)
    {
        var list = new List<PullRequest>();
        foreach (var element in elements)
        {
            AddOpenPullRequest(list, element, repositoryId);
        }

        return list;
    }

    private static void AddOpenPullRequest(List<PullRequest> list, JsonElement element, string repositoryId)
    {
        var state = GetStringOrNull(element, "state");
        if (!string.IsNullOrWhiteSpace(state) && !string.Equals(state, "open", StringComparison.OrdinalIgnoreCase))
            return;

        if (ParsePullRequest(element, repositoryId) is { } pullRequest)
            list.Add(pullRequest);
    }

    /// <summary>Bildet ein Pull-Request-Element auf das <see cref="PullRequest"/>-Objekt ab;
    /// <c>null</c> bei fehlender oder ungültiger Nummer.</summary>
    private static PullRequest? ParsePullRequest(JsonElement element, string repositoryId)
    {
        var number = GetInt32OrDefault(element, "number");
        if (number <= 0)
            return null;

        var title = GetStringOrNull(element, "title") ?? string.Empty;
        var body = GetStringOrNull(element, "body");
        var url = GetStringOrNull(element, "html_url") ?? GetStringOrNull(element, "url") ?? string.Empty;
        var head = element.TryGetProperty("head", out var headElement) && headElement.ValueKind == JsonValueKind.Object
            ? headElement
            : (JsonElement?)null;
        var @base = element.TryGetProperty("base", out var baseElement) && baseElement.ValueKind == JsonValueKind.Object
            ? baseElement
            : (JsonElement?)null;

        var sourceBranch = head is null ? string.Empty : GetStringOrNull(head.Value, "ref") ?? string.Empty;
        var sourceSha = head is null ? null : GetStringOrNull(head.Value, "sha");
        var sourceRepositoryId = repositoryId;
        string? sourceRepositoryUrl = null;
        if (head is not null
            && head.Value.TryGetProperty("repo", out var headRepo)
            && headRepo.ValueKind == JsonValueKind.Object)
        {
            var fullName = GetStringOrNull(headRepo, "full_name");
            if (!string.IsNullOrWhiteSpace(fullName))
                sourceRepositoryId = PullRequestRepositoryId.Normalize(PullRequestProvider.GitHub, fullName);
            sourceRepositoryUrl = GetStringOrNull(headRepo, "clone_url") ?? GetStringOrNull(headRepo, "ssh_url");
        }

        var targetBranch = @base is null ? null : GetStringOrNull(@base.Value, "ref");

        return new PullRequest(
            number,
            title,
            url,
            sourceBranch,
            PullRequestProvider.GitHub,
            repositoryId,
            GetStringOrNull(element, "node_id") ?? GetStringOrNull(element, "id"),
            targetBranch,
            sourceSha,
            sourceRepositoryId,
            sourceRepositoryUrl,
            $"refs/heads/{sourceBranch}",
            body);
    }

    /// <inheritdoc/>
    public override async Task CloneRepositoryAsync(string repositoryUrl, string targetPath, CancellationToken ct = default)
    {
        _logger.LogInformation("Klone Repository {Url} nach {TargetPath}.", repositoryUrl, targetPath);

        // Nur HTTPS-Transport: SSH-URLs werden in ihre HTTPS-Form umgewandelt,
        // andere Schemata werden vor jedem CLI-Aufruf abgelehnt.
        repositoryUrl = ToHttpsRepositoryUrl(repositoryUrl)
            ?? throw new InvalidOperationException(
                "git clone abgebrochen: Es werden nur HTTPS-Repository-URLs verwendet; SSH-URLs werden automatisch konvertiert.");

        var token = _credentialStore.GetCredential(GitHubTokenCredentialKey);

        if (IsHttpsRepositoryUrl(repositoryUrl) && string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException(
                "git clone abgebrochen: GitHub-Token fehlt. Bitte in den Plugin-Einstellungen einen gültigen Personal Access Token konfigurieren (Scope: repo, ggf. read:org).");
        }

        // Clone mit unauthentifizierter URL – der Token wird ausschließlich über GH_TOKEN
        // bzw. http.extraheader (GetGitEnvironment()) bereitgestellt, niemals in der URL eingebettet.
        var result = await _cliRunner.RunAsync(
            "git",
            ["clone", repositoryUrl, targetPath],
            null,
            GetGitEnvironment(token),
            ct);

        if (!result.IsSuccess)
        {
            var sanitizedError = SanitizeSensitiveOutput(result.StdErr);
            if (IsAuthenticationFailure(result.StdErr))
            {
                throw new InvalidOperationException(
                    $"git clone fehlgeschlagen: Authentifizierung fehlgeschlagen. Bitte GitHub-Token prüfen/neu setzen und Scopes (repo, ggf. read:org) verifizieren. Details: {sanitizedError}");
            }

            throw new InvalidOperationException($"git clone fehlgeschlagen: {sanitizedError}");
        }

        // Configure git credentials for the cloned repository
        await ConfigureGitCredentialsAsync(targetPath, repositoryUrl, ct);
    }

    /// <inheritdoc/>
    public override async Task PushBranchAsync(string localPath, string branchName, CancellationToken ct = default)
    {
        _logger.LogInformation("Pushe Branch {BranchName} in {LocalPath}.", branchName, localPath);

        // Bestands-Arbeitskopien hygienisch bereinigen: Remote-URL auf HTTPS normalisieren
        // und das von Altversionen geschriebene unsichere core.sshCommand entfernen.
        await NormalizeRemoteUrlAsync(localPath, ct);
        await RemoveInsecureSshCommandAsync(localPath, ct);

        var token = _credentialStore.GetCredential(GitHubTokenCredentialKey);

        var result = await _cliRunner.RunAsync(
            "git",
            ["push", "--set-upstream", "origin", branchName],
            localPath,
            GetGitEnvironment(token),
            ct);

        if (!result.IsSuccess)
        {
            var sanitizedError = SanitizeSensitiveOutput(result.StdErr);
            throw new InvalidOperationException($"git push fehlgeschlagen: {sanitizedError}");
        }
    }

    /// <inheritdoc/>
    public override async Task PullAsync(string localPath, CancellationToken ct = default)
    {
        _logger.LogInformation("Führe git pull in {LocalPath} durch.", localPath);

        // Bestands-Arbeitskopien hygienisch bereinigen: Remote-URL auf HTTPS normalisieren
        // und das von Altversionen geschriebene unsichere core.sshCommand entfernen.
        await NormalizeRemoteUrlAsync(localPath, ct);
        await RemoveInsecureSshCommandAsync(localPath, ct);

        var token = _credentialStore.GetCredential(GitHubTokenCredentialKey);

        var result = await _cliRunner.RunAsync(
            "git",
            ["pull"],
            localPath,
            GetGitEnvironment(token),
            ct);

        if (!result.IsSuccess)
        {
            var sanitizedError = SanitizeSensitiveOutput(result.StdErr);
            throw new InvalidOperationException($"git pull fehlgeschlagen: {sanitizedError}");
        }
    }

    /// <inheritdoc/>
    public override async Task<PullRequest> CreatePullRequestAsync(
        string repositoryId,
        string branchName,
        string? baseBranch,
        string title,
        string body,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Erstelle Pull Request für Branch {BranchName} in Repository {RepositoryId}.",
            branchName, repositoryId);

        // Use --fill flag to auto-fill from commits, or provide explicit title/body
        var args = new List<string>
        {
            "pr", "create",
            "--repo", repositoryId,
            "--head", branchName,
            "--title", title,
            "--body", body
        };

        if (!string.IsNullOrEmpty(baseBranch))
        {
            args.AddRange(["--base", baseBranch]);
        }

        var result = await _cliRunner.RunAsync(
            "gh",
            args.ToArray(),
            null,
            GetGhEnvironment(),
            ct);

        if (!result.IsSuccess)
        {
            var sanitizedError = SanitizeSensitiveOutput(result.StdErr);
            _logger.LogError("gh pr create fehlgeschlagen: {StdErr}", sanitizedError);

            if (IsNoCommitsBetweenFailure(result.StdErr))
            {
                throw new InvalidOperationException(
                    $"gh pr create fehlgeschlagen: Der Branch '{branchName}' enthält keine Commits gegenüber dem Zielbranch. Bitte stelle sicher, dass Änderungen committet wurden. Details: {sanitizedError}");
            }

            throw new InvalidOperationException($"gh pr create fehlgeschlagen: {sanitizedError}");
        }

        // Parse the output text: "https://github.com/owner/repo/pull/123"
        var prUrl = result.StdOut.Trim();

        // Extract PR number from URL
        var lastSlashIndex = prUrl.LastIndexOf('/');
        if (lastSlashIndex > 0 && int.TryParse(prUrl[(lastSlashIndex + 1)..], out var prNumber))
        {
            try
            {
                var status = await GetPullRequestStatusAsync(repositoryId, prNumber, ct);
                return new PullRequest(
                    status.Nummer,
                    status.Titel,
                    status.Url,
                    status.SourceBranch,
                    status.Provider,
                    status.RepositoryId,
                    status.ProviderPullRequestId,
                    status.TargetBranch,
                    status.HeadSha);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Pull-Request-Metadaten konnten nach Erstellung nicht nachgeladen werden.");
                return new PullRequest(prNumber, title, prUrl, branchName, PullRequestProvider.GitHub, repositoryId);
            }
        }

        // Fallback: if we can't parse, throw error
        throw new InvalidOperationException($"PR created but could not parse response: {result.StdOut}");
    }

    /// <inheritdoc/>
    public override async Task<PullRequestStatusInfo> GetPullRequestStatusAsync(
        string repositoryId,
        int pullRequestNumber,
        CancellationToken ct = default)
    {
        var normalizedRepositoryId = NormalizeRepositoryId(repositoryId)
            ?? throw new InvalidOperationException("Repository-ID fehlt.");

        try
        {
            var (host, owner, name) = ParseEventRepository(normalizedRepositoryId);
            var data = await GraphAsync(_eventApiClient, host,
                "query($owner:String!,$name:String!,$number:Int!){repository(owner:$owner,name:$name){pullRequest(number:$number){id number title url state headRefName baseRefName headRefOid mergeCommit{oid} mergeStateStatus isDraft reviewDecision commits(last:1){nodes{commit{statusCheckRollup{state}}}}}}}",
                new Dictionary<string, object?> { ["owner"] = owner, ["name"] = name, ["number"] = pullRequestNumber }, false, ct);
            var pullRequest = Object(Object(data, "repository"), "pullRequest");
            if (pullRequest.ValueKind != JsonValueKind.Object)
                throw new InvalidOperationException($"Pull Request #{pullRequestNumber} wurde in {normalizedRepositoryId} nicht gefunden.");
            return ParsePullRequestStatus(pullRequest.GetRawText(), normalizedRepositoryId);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (InvalidOperationException) { throw; }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Pull-Request-Status konnte nicht geladen werden: {SanitizeSensitiveOutput(ex.Message)}");
        }
    }

    /// <inheritdoc/>
    public override async Task<IReadOnlyList<PullRequestWorkflowRunInfo>> GetPullRequestWorkflowRunsAsync(
        string repositoryId,
        int pullRequestNumber,
        string? headSha = null,
        string? mergeCommitSha = null,
        CancellationToken ct = default)
    {
        var normalizedRepositoryId = NormalizeRepositoryId(repositoryId)
            ?? throw new InvalidOperationException("Repository-ID fehlt.");

        var runs = new List<PullRequestWorkflowRunInfo>();
        if (!string.IsNullOrWhiteSpace(headSha))
        {
            var elements = await RunWorkflowListAsync(normalizedRepositoryId, headSha, ct);
            runs.AddRange(ParseWorkflowRuns(elements, headSha, null));
        }

        if (!string.IsNullOrWhiteSpace(mergeCommitSha)
            && !string.Equals(mergeCommitSha, headSha, StringComparison.OrdinalIgnoreCase))
        {
            var elements = await RunWorkflowListAsync(normalizedRepositoryId, mergeCommitSha, ct);
            runs.AddRange(ParseWorkflowRuns(elements, null, mergeCommitSha));
        }

        if (runs.Count == 0 && string.IsNullOrWhiteSpace(headSha) && string.IsNullOrWhiteSpace(mergeCommitSha))
        {
            var elements = await RunWorkflowListAsync(normalizedRepositoryId, null, ct);
            runs.AddRange(ParseWorkflowRuns(elements, headSha, mergeCommitSha));
        }

        return runs
            .GroupBy(r => r.ProviderRunId, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();
    }

    /// <inheritdoc/>
    public override async Task<PullRequestCompletionResult> CompletePullRequestAsync(
        string repositoryId,
        int pullRequestNumber,
        PullRequestCompletionOptions options,
        CancellationToken ct = default)
    {
        var normalizedRepositoryId = NormalizeRepositoryId(repositoryId);
        if (string.IsNullOrWhiteSpace(normalizedRepositoryId))
        {
            return PullRequestCompletionResult.Failed("Repository-ID fehlt.");
        }

        var args = new List<string>();
        if (options.Strategy == PullRequestCompletionStrategy.ApprovalOnly)
        {
            args.AddRange(["pr", "review", pullRequestNumber.ToString(), "--repo", normalizedRepositoryId, "--approve"]);
        }
        else
        {
            args.AddRange(["pr", "merge", pullRequestNumber.ToString(), "--repo", normalizedRepositoryId]);
            args.Add(options.MergeMethod switch
            {
                PullRequestMergeMethod.Merge => "--merge",
                PullRequestMergeMethod.Rebase => "--rebase",
                _ => "--squash"
            });

            if (options.Strategy == PullRequestCompletionStrategy.AutoMerge)
            {
                args.Add("--auto");
            }

            if (options.AllowProtectedBranchBypass)
            {
                args.Add("--admin");
            }
        }

        var result = await _cliRunner.RunAsync("gh", args.ToArray(), null, GetGhEnvironment(), ct);
        if (!result.IsSuccess)
        {
            var sanitizedError = SanitizeSensitiveOutput(result.StdErr);
            return IsAuthenticationFailure(result.StdErr) || IsBranchProtectionFailure(result.StdErr)
                ? PullRequestCompletionResult.BlockedResult(sanitizedError)
                : PullRequestCompletionResult.Failed(sanitizedError);
        }

        if (options.Strategy == PullRequestCompletionStrategy.ApprovalOnly)
        {
            return PullRequestCompletionResult.Approved(result.StdOut.Trim());
        }

        var status = await GetPullRequestStatusAsync(normalizedRepositoryId, pullRequestNumber, ct);
        if (status.Status != PullRequestStatus.Merged)
        {
            return PullRequestCompletionResult.WaitingForMerge(result.StdOut.Trim());
        }

        return PullRequestCompletionResult.Completed(status.MergeCommitSha, result.StdOut.Trim());
    }

    /// <inheritdoc/>
    public override async Task<PullRequestCompletionResult> MarkPullRequestReadyAsync(
        string repositoryId,
        int pullRequestNumber,
        CancellationToken ct = default)
    {
        var normalizedRepositoryId = NormalizeRepositoryId(repositoryId);
        if (string.IsNullOrWhiteSpace(normalizedRepositoryId))
        {
            return PullRequestCompletionResult.Failed("Repository-ID fehlt.");
        }

        var result = await _cliRunner.RunAsync("gh",
            ["pr", "ready", pullRequestNumber.ToString(), "--repo", normalizedRepositoryId],
            null, GetGhEnvironment(), ct);
        if (!result.IsSuccess)
        {
            var sanitizedError = SanitizeSensitiveOutput(result.StdErr);
            return IsAuthenticationFailure(result.StdErr) || IsBranchProtectionFailure(result.StdErr)
                ? PullRequestCompletionResult.BlockedResult(sanitizedError)
                : PullRequestCompletionResult.Failed(sanitizedError);
        }

        return PullRequestCompletionResult.Approved(result.StdOut.Trim());
    }

    private async Task<IReadOnlyList<JsonElement>> RunWorkflowListAsync(string repositoryId, string? commitSha, CancellationToken ct)
    {
        var (host, owner, name) = ParseEventRepository(repositoryId);
        var endpoint = $"repos/{owner}/{name}/actions/runs";
        endpoint += string.IsNullOrWhiteSpace(commitSha)
            ? "?per_page=100"
            : $"?head_sha={Uri.EscapeDataString(commitSha)}&per_page=100";
        try
        {
            return await RestObjectPagesAsync(_eventApiClient, host, endpoint, "workflow_runs", ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"GitHub-Workflow-Runs konnten nicht geladen werden: {SanitizeSensitiveOutput(ex.Message)}");
        }
    }

    private static PullRequestStatusInfo ParsePullRequestStatus(string json, string repositoryId)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var state = GetStringOrNull(root, "state");
        var mergeStateStatus = GetStringOrNull(root, "mergeStateStatus");
        var reviewDecision = root.TryGetProperty("reviewDecision", out var reviewDecisionElement)
            ? MapReviewDecision(reviewDecisionElement.ValueKind == JsonValueKind.String
                ? reviewDecisionElement.GetString() : null)
            : PullRequestReviewDecision.Unknown;
        var checksState = LiesCheckRollupState(root) ?? PullRequestChecksState.Unknown;
        var mergeCommitSha = root.TryGetProperty("mergeCommit", out var mergeCommit)
                             && mergeCommit.ValueKind == JsonValueKind.Object
            ? GetStringOrNull(mergeCommit, "oid")
            : null;

        var status = string.Equals(state, "MERGED", StringComparison.OrdinalIgnoreCase)
                     || (string.Equals(state, "CLOSED", StringComparison.OrdinalIgnoreCase)
                         && !string.IsNullOrWhiteSpace(mergeCommitSha))
            ? PullRequestStatus.Merged
            : string.Equals(state, "OPEN", StringComparison.OrdinalIgnoreCase)
                ? PullRequestStatus.Open
                : string.Equals(state, "CLOSED", StringComparison.OrdinalIgnoreCase)
                    ? PullRequestStatus.Closed
                    : PullRequestStatus.Unknown;

        return new PullRequestStatusInfo(
            PullRequestProvider.GitHub,
            repositoryId,
            GetInt32OrDefault(root, "number"),
            GetStringOrNull(root, "id"),
            GetStringOrNull(root, "url") ?? string.Empty,
            GetStringOrNull(root, "title") ?? string.Empty,
            GetStringOrNull(root, "headRefName") ?? string.Empty,
            GetStringOrNull(root, "baseRefName") ?? string.Empty,
            GetStringOrNull(root, "headRefOid"),
            mergeCommitSha,
            status,
            MapMergeStatus(mergeStateStatus, status),
            DateTimeOffset.UtcNow,
            GetBoolOrDefault(root, "isDraft"),
            reviewDecision,
            checksState);
    }

    /// <summary>Liest den Rollup-Zustand der Statuschecks des Head-Commits aus. Fehlt das
    /// commits-Feld ganz, gilt der Zustand als unbekannt (<see langword="null"/>); ein
    /// fehlender Rollup bedeutet „keine Checks vorhanden“.</summary>
    private static PullRequestChecksState? LiesCheckRollupState(JsonElement root)
    {
        if (!root.TryGetProperty("commits", out var commits)
            || commits.ValueKind != JsonValueKind.Object
            || !commits.TryGetProperty("nodes", out var nodes)
            || nodes.ValueKind != JsonValueKind.Array)
            return null;
        if (nodes.GetArrayLength() == 0
            || !nodes[0].TryGetProperty("commit", out var commit)
            || commit.ValueKind != JsonValueKind.Object
            || !commit.TryGetProperty("statusCheckRollup", out var rollup)
            || rollup.ValueKind != JsonValueKind.Object)
            return PullRequestChecksState.None;
        return MapChecksState(GetStringOrNull(rollup, "state"));
    }

    private static IReadOnlyList<PullRequestWorkflowRunInfo> ParseWorkflowRuns(
        IEnumerable<JsonElement> elements,
        string? headSha,
        string? mergeCommitSha)
    {
        var runs = new List<PullRequestWorkflowRunInfo>();
        foreach (var element in elements)
        {
            var runHeadSha = GetStringOrNull(element, "head_sha");
            var isPreMerge = !string.IsNullOrWhiteSpace(headSha)
                             && string.Equals(runHeadSha, headSha, StringComparison.OrdinalIgnoreCase);
            var isPostMerge = !string.IsNullOrWhiteSpace(mergeCommitSha)
                              && string.Equals(runHeadSha, mergeCommitSha, StringComparison.OrdinalIgnoreCase);

            if (!isPreMerge && !isPostMerge)
            {
                continue;
            }

            var providerRunId = element.TryGetProperty("id", out var databaseId)
                ? databaseId.ValueKind == JsonValueKind.Number
                    ? databaseId.GetInt64().ToString()
                    : databaseId.GetString() ?? string.Empty
                : string.Empty;

            runs.Add(new PullRequestWorkflowRunInfo(
                providerRunId,
                ResolveWorkflowRunDisplayName(element),
                GetStringOrNull(element, "html_url"),
                runHeadSha,
                GetStringOrNull(element, "head_branch"),
                MapWorkflowRunStatus(GetStringOrNull(element, "status")),
                MapWorkflowRunConclusion(GetStringOrNull(element, "conclusion")),
                TryGetDateTimeOffset(element, "created_at"),
                TryGetDateTimeOffset(element, "updated_at"),
                isPostMerge));
        }

        return runs;
    }

    private static string ResolveWorkflowRunDisplayName(JsonElement element)
    {
        var name = GetStringOrNull(element, "name");
        var displayTitle = GetStringOrNull(element, "display_title");
        if (!string.IsNullOrWhiteSpace(displayTitle))
        {
            return !string.IsNullOrWhiteSpace(name)
                   && !string.Equals(name, displayTitle, StringComparison.OrdinalIgnoreCase)
                ? $"{name}: {displayTitle}"
                : displayTitle;
        }

        return name ?? "Workflow";
    }

    private static PullRequestMergeStatus MapMergeStatus(string? mergeStateStatus, PullRequestStatus status)
    {
        if (status == PullRequestStatus.Merged)
        {
            return PullRequestMergeStatus.Merged;
        }

        return mergeStateStatus?.ToUpperInvariant() switch
        {
            "CLEAN" or "HAS_HOOKS" or "UNSTABLE" => PullRequestMergeStatus.Mergeable,
            "DIRTY" => PullRequestMergeStatus.Conflicting,
            "BLOCKED" or "DRAFT" or "BEHIND" => PullRequestMergeStatus.Blocked,
            _ => PullRequestMergeStatus.Unknown
        };
    }

    private static PullRequestReviewDecision MapReviewDecision(string? reviewDecision)
        => reviewDecision?.ToUpperInvariant() switch
        {
            "REVIEW_REQUIRED" => PullRequestReviewDecision.ReviewRequired,
            "CHANGES_REQUESTED" => PullRequestReviewDecision.ChangesRequested,
            "APPROVED" => PullRequestReviewDecision.Approved,
            null => PullRequestReviewDecision.None,
            _ => PullRequestReviewDecision.Unknown
        };

    private static PullRequestChecksState MapChecksState(string? rollupState)
        => rollupState?.ToUpperInvariant() switch
        {
            "SUCCESS" => PullRequestChecksState.Success,
            "PENDING" or "EXPECTED" => PullRequestChecksState.Pending,
            "FAILURE" or "ERROR" => PullRequestChecksState.Failure,
            _ => PullRequestChecksState.Unknown
        };

    private static WorkflowRunStatus MapWorkflowRunStatus(string? status)
        => status?.ToLowerInvariant() switch
        {
            "queued" or "waiting" or "requested" or "pending" => WorkflowRunStatus.Queued,
            "in_progress" => WorkflowRunStatus.InProgress,
            "completed" => WorkflowRunStatus.Completed,
            _ => WorkflowRunStatus.Unknown
        };

    private static WorkflowRunConclusion MapWorkflowRunConclusion(string? conclusion)
        => conclusion?.ToLowerInvariant() switch
        {
            "success" => WorkflowRunConclusion.Success,
            "failure" => WorkflowRunConclusion.Failure,
            "cancelled" => WorkflowRunConclusion.Cancelled,
            "skipped" => WorkflowRunConclusion.Skipped,
            "timed_out" => WorkflowRunConclusion.TimedOut,
            "action_required" => WorkflowRunConclusion.ActionRequired,
            _ => WorkflowRunConclusion.Unknown
        };

    private static DateTimeOffset? TryGetDateTimeOffset(JsonElement element, string propertyName)
    {
        var value = GetStringOrNull(element, propertyName);
        return DateTimeOffset.TryParse(value, out var parsed) ? parsed : null;
    }

    private static bool IsBranchProtectionFailure(string error)
    {
        var normalizedError = error.ToLowerInvariant();
        return normalizedError.Contains("protected branch", StringComparison.Ordinal)
               || normalizedError.Contains("required status check", StringComparison.Ordinal)
               || normalizedError.Contains("review required", StringComparison.Ordinal)
               || normalizedError.Contains("bypass", StringComparison.Ordinal)
               || normalizedError.Contains("cannot approve your own pull request", StringComparison.Ordinal);
    }

    /// <inheritdoc/>
    public override async Task<bool> CheckHealthAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Prüfe GitHub-Plugin-Health.");
        try
        {
            var response = await _eventApiClient.SendAsync(new("github.com", "GET", "user"), ct);
            return response.StatusCode is >= 200 and < 300;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception)
        {
            return false;
        }
    }

    /// <inheritdoc/>
    public override async Task<IEnumerable<string>> GetRemoteBranchesAsync(string repositoryUrl, CancellationToken ct = default)
    {
        _logger.LogInformation("Rufe Remote-Branches für {RepositoryUrl} ab.", repositoryUrl);

        var remoteUrl = ToHttpsRepositoryUrl(repositoryUrl) ?? repositoryUrl;

        var result = await _cliRunner.RunAsync(
            "git",
            ["ls-remote", "--heads", remoteUrl],
            null,
            GetGitEnvironment(),
            ct);

        if (!result.IsSuccess)
        {
            _logger.LogWarning("git ls-remote fehlgeschlagen für {RepositoryUrl}: {StdErr}", repositoryUrl, result.StdErr);
            return [];
        }

        // Ausgabe: "<hash>\trefs/heads/<branchname>"
        return result.StdOut
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line =>
            {
                var parts = line.Split('\t', 2);
                return parts.Length == 2 ? parts[1].Replace("refs/heads/", string.Empty).Trim() : null;
            })
            .Where(name => !string.IsNullOrEmpty(name))
            .Select(name => name!)
            .OrderBy(name => name)
            .ToList();
    }

    /// <inheritdoc/>
    public override async Task<IEnumerable<AvailableRepository>> GetAvailableRepositoriesAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Lade verfügbare GitHub-Repositories.");
        try
        {
            var items = await RestPagesAsync(_eventApiClient, "github.com", "user/repos?affiliation=owner&per_page=100", ct);
            return items
                .Select(e => new AvailableRepository(
                    GetStringOrNull(e, "name") ?? string.Empty,
                    TryGetDateTimeOffset(e, "updated_at")?.UtcDateTime
                        ?? TryGetDateTimeOffset(e, "created_at")?.UtcDateTime ?? DateTime.MinValue,
                    GetStringOrNull(e, "full_name") ?? string.Empty,
                    GetStringOrNull(e, "html_url") ?? string.Empty))
                .ToList();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            _logger.LogWarning("Verfügbare GitHub-Repositories konnten nicht geladen werden: {Error}", SanitizeSensitiveOutput(ex.Message));
            return [];
        }
    }

    /// <inheritdoc/>
    public override async Task<string> GetDefaultBranchAsync(string repositoryUrl, CancellationToken ct = default)
    {
        _logger.LogInformation("Ermittle Standard-Branch für {RepositoryUrl}.", repositoryUrl);

        var remoteUrl = ToHttpsRepositoryUrl(repositoryUrl) ?? repositoryUrl;

        var result = await _cliRunner.RunAsync(
            "git",
            ["ls-remote", "--symref", remoteUrl, "HEAD"],
            null,
            GetGitEnvironment(),
            ct);

        if (result.IsSuccess)
        {
            // Erste Zeile: "ref: refs/heads/main\tHEAD"
            var firstLine = result.StdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
            if (firstLine.StartsWith("ref: refs/heads/", StringComparison.Ordinal))
            {
                var branch = firstLine.Replace("ref: refs/heads/", string.Empty).Split('\t')[0].Trim();
                if (!string.IsNullOrEmpty(branch))
                {
                    return branch;
                }
            }
        }

        _logger.LogWarning("Standard-Branch konnte nicht ermittelt werden, Fallback auf 'main'.");
        return "main";
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Übergibt dem <c>git fetch</c>/<c>checkout</c> des PR-Checkouts die git-Umgebung aus
    /// <see cref="GetGitEnvironment"/> — nötig seit dem Wegfall der <c>_netrc</c>-Persistenz,
    /// damit der Checkout auch gegen private Repositories authentifiziert läuft. Zuvor wird die
    /// betreute Arbeitskopie hygienisch bereinigt (SSH-<c>remote.origin.url</c> → HTTPS,
    /// plugin-geschriebenes unsicheres <c>core.sshCommand</c> → entfernt) und eine SSH-
    /// <c>SourceRepositoryUrl</c> (Fork-Fall) in ihre HTTPS-Form umgewandelt.
    /// </remarks>
    public override async Task CheckoutPullRequestSourceAsync(
        string localPath,
        PullRequestCheckoutSpec checkoutSpec,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(checkoutSpec);

        await NormalizeRemoteUrlAsync(localPath, ct);
        await RemoveInsecureSshCommandAsync(localPath, ct);

        if (!string.IsNullOrWhiteSpace(checkoutSpec.SourceRepositoryUrl))
        {
            checkoutSpec = checkoutSpec with
            {
                SourceRepositoryUrl = ToHttpsRepositoryUrl(checkoutSpec.SourceRepositoryUrl)
                    ?? checkoutSpec.SourceRepositoryUrl
            };
        }

        await CheckoutPullRequestSourceCoreAsync(localPath, checkoutSpec, GetGitEnvironment(), ct);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Ruft die Verzeichnisstruktur des Standard-Branches rein remote über die GitHub Git-Trees-API ab
    /// (<c>gh api repos/{owner}/{repo}/git/trees/{branch}?recursive=1</c>) — ein lokaler Klon ist dafür nicht
    /// erforderlich. Damit ist eine Unterverzeichnis-Auswahl bereits vor dem Klon möglich (Hauptanwendungsfall
    /// der Arbeitsverzeichnis-Auswahl).
    /// </remarks>
    public override async Task<IEnumerable<RepositoryDirectoryEntry>> GetRepositoryStructureAsync(
        string repositoryUrl,
        int maxDepth = 2,
        CancellationToken ct = default,
        string? branchName = null)
    {
        var result = await GetRepositoryStructureLoadResultAsync(repositoryUrl, maxDepth, ct, branchName).ConfigureAwait(false);
        return result.Status == RepositoryStructureLoadStatus.Success ? result.Entries : [];
    }

    /// <inheritdoc/>
    public override async Task<RepositoryStructureLoadResult> GetRepositoryStructureLoadResultAsync(
        string repositoryUrl,
        int maxDepth = 2,
        CancellationToken ct = default,
        string? branchName = null)
    {
        var repositoryId = TryExtractRepositoryId(repositoryUrl);
        if (repositoryId is null)
        {
            _logger.LogWarning(
                "Verzeichnisstruktur konnte nicht ermittelt werden: Repository-ID konnte nicht aus '{RepositoryUrl}' extrahiert werden.",
                repositoryUrl);
            return RepositoryStructureLoadResult.Failed("Repository-ID konnte nicht aus der URL ermittelt werden.");
        }

        var branch = string.IsNullOrWhiteSpace(branchName) ? await GetDefaultBranchAsync(repositoryUrl, ct) : branchName;

        try
        {
            var (host, owner, name) = ParseEventRepository(repositoryId);
            // Git-Refs dürfen '#' und '%' enthalten; '/' bleibt als Segmenttrenner erhalten.
            var escapedBranch = string.Join('/', branch.Split('/').Select(Uri.EscapeDataString));
            var tree = await EventApiAsync(_eventApiClient, host, "GET",
                $"repos/{owner}/{name}/git/trees/{escapedBranch}?recursive=1", null, false, ct);
            return ParseRepositoryTreeLoadResult(tree.GetRawText(), maxDepth, repositoryId);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            _logger.LogWarning(
                "GitHub git/trees fehlgeschlagen für {RepositoryId} (Branch {Branch}): {Error}",
                repositoryId,
                branch,
                SanitizeSensitiveOutput(ex.Message));
            return RepositoryStructureLoadResult.Failed(SanitizeSensitiveOutput(ex.Message));
        }
    }

    private RepositoryStructureLoadResult ParseRepositoryTreeLoadResult(string json, int maxDepth, string repositoryId)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("truncated", out var truncatedEl) &&
                truncatedEl.ValueKind == JsonValueKind.True)
            {
                _logger.LogWarning(
                    "GitHub Git-Trees-API-Antwort für {RepositoryId} ist abgeschnitten (truncated=true) — bei sehr großen Repositories ist die ermittelte Verzeichnisstruktur ggf. unvollständig.",
                    repositoryId);
            }

            if (!doc.RootElement.TryGetProperty("tree", out var treeEl) || treeEl.ValueKind != JsonValueKind.Array)
            {
                return RepositoryStructureLoadResult.Failed("GitHub-Antwort enthält keine gültige tree-Liste.");
            }

            var entries = treeEl.EnumerateArray()
                .Select(entry => new
                {
                    Type = entry.TryGetProperty("type", out var typeEl) ? typeEl.GetString() : null,
                    Path = entry.TryGetProperty("path", out var pathEl) ? pathEl.GetString() : null
                })
                .Where(entry => (entry.Type == "tree" || entry.Type == "blob") && !string.IsNullOrEmpty(entry.Path))
                .Where(entry => entry.Path!.Count(c => c == '/') + 1 <= maxDepth)
                .Select(entry => new RepositoryDirectoryEntry(entry.Path!, entry.Type == "tree"))
                .ToList();

            return RepositoryStructureLoadResult.Success(entries);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Parsen der GitHub-Verzeichnisstruktur für {RepositoryId}.", repositoryId);
            return RepositoryStructureLoadResult.Failed(ex.Message);
        }
    }

    /// <summary>
    /// Extrahiert die Repository-ID (<c>owner/repo</c>) aus einer GitHub-Repository-URL. Unterstützt HTTPS-
    /// (<c>https://github.com/owner/repo(.git)?</c>) und SSH-URLs (<c>git@github.com:owner/repo(.git)?</c>).
    /// Liefert <c>null</c> statt zu werfen, wenn die URL nicht geparst werden kann.
    /// </summary>
    /// <param name="repositoryUrl">Die zu parsende GitHub-Repository-URL (HTTPS oder SSH).</param>
    /// <returns>Die Repository-ID im Format <c>owner/repo</c>, oder <c>null</c> wenn die URL nicht erkannt wurde.</returns>
    private static string? TryExtractRepositoryId(string repositoryUrl)
    {
        if (string.IsNullOrWhiteSpace(repositoryUrl))
        {
            return null;
        }

        var url = repositoryUrl.EndsWith(".git", StringComparison.OrdinalIgnoreCase)
            ? repositoryUrl[..^4]
            : repositoryUrl;

        if (!url.Contains("://", StringComparison.Ordinal))
        {
            // SCP-/SSH-Format: git@github.com:owner/repo
            var colonIndex = url.IndexOf(':');
            if (colonIndex < 0 || colonIndex >= url.Length - 1)
            {
                return null;
            }

            var repositoryPath = url[(colonIndex + 1)..];
            var slashIndex = repositoryPath.IndexOf('/');
            if (slashIndex <= 0 || slashIndex >= repositoryPath.Length - 1)
            {
                return null;
            }

            return $"{repositoryPath[..slashIndex]}/{repositoryPath[(slashIndex + 1)..]}";
        }

        // HTTPS-Format: https://github.com/owner/repo[.git][/][?query][#fragment]. Uri.AbsolutePath
        // normalisiert Trailing-Slashes weg und ignoriert Query-String/Fragment, statt sie fälschlich
        // in "owner"/"repo" einzumischen (siehe TryExtractRepositoryId in BitbucketPlugin für dasselbe Muster).
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return null;
        }

        var segments = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2)
        {
            return null;
        }

        var owner = segments[0];
        var repo = segments[1].EndsWith(".git", StringComparison.OrdinalIgnoreCase)
            ? segments[1][..^4]
            : segments[1];

        return string.IsNullOrEmpty(owner) || string.IsNullOrEmpty(repo) ? null : $"{owner}/{repo}";
    }
}
