using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Application.Services;

/// <summary>Ein nachweisbarer Zugriff, eine ausdrückliche Verweigerung oder nicht unterstützte Konfiguration.</summary>
public enum ScmExchangeAccessStatus
{
    /// <summary>Der ausgewertete Startvertrag erlaubt den Arbeitsbereichskanal.</summary>
    Supported,
    /// <summary>Eine bekannte Regel verweigert Dateizugriff.</summary>
    Denied,
    /// <summary>Die effektiven Rechte sind mit diesem Adapter nicht sicher bestimmbar.</summary>
    Unknown,
}

/// <summary>Rungebundene Zugriffsbeschreibung ohne Umgebungsvariablen, Tokens oder Konfigurationsinhalte.</summary>
public sealed record ScmExchangeAccess(ScmExchangeAccessStatus Status, string Reason, string WorkingDirectory,
    Guid CliRunId, string ConfigurationFingerprint)
{
    /// <summary>Existing dedicated workspace capabilities verified by a file-tool channel probe, never arbitrary users or groups.</summary>
    public IReadOnlyList<string> RestrictedWriteSids { get; init; } = [];
}

/// <summary>Konfigurationsquelle; null markiert eine beim Start nicht vorhandene Datei.</summary>
public sealed record ScmCliConfigurationFile(string Path, string? Content);

/// <summary>
/// Konservativer Auswerter unterstützter lokaler CLI-Konfigurationen. Unbekannte Parameter,
/// Profile, Managed-Regeln und Dateitool-Overrides werden nie als Freigabe interpretiert.
/// </summary>
public static class ScmCliAccessEvaluator
{
    /// <summary>Erfasst bekannte Benutzer-, Projekt- und Managed-Konfigurationen vor dem Prozessstart.</summary>
    public static IReadOnlyList<ScmCliConfigurationFile> Capture(string prefix, TerminalSessionStartSpec spec)
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var userDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string? EnvironmentValue(string key) => spec.EnvironmentVariables.TryGetValue(key, out var value) ? value : Environment.GetEnvironmentVariable(key);
        if (prefix == "Softwareschmiede.Codex")
        {
            var configHome = EnvironmentValue("CODEX_HOME") ?? Path.Combine(userDirectory, ".codex");
            foreach (var name in new[] { "config.toml", "requirements.toml", "managed_config.toml" })
                paths.Add(Path.Combine(configHome, name));
            var managed = OperatingSystem.IsWindows() ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "OpenAI", "Codex") : "/etc/codex";
            foreach (var name in new[] { "config.toml", "requirements.toml", "managed_config.toml" }) paths.Add(Path.Combine(managed, name));
            for (var directory = new DirectoryInfo(Path.GetFullPath(spec.WorkingDirectory)); directory is not null; directory = directory.Parent)
                paths.Add(Path.Combine(directory.FullName, ".codex", "config.toml"));
        }
        else if (prefix == "Softwareschmiede.ClaudeCli")
        {
            var configHome = EnvironmentValue("CLAUDE_CONFIG_DIR") ?? Path.Combine(userDirectory, ".claude");
            paths.Add(Path.Combine(configHome, "settings.json"));
            paths.Add(Path.Combine(spec.WorkingDirectory, ".claude", "settings.json"));
            paths.Add(Path.Combine(spec.WorkingDirectory, ".claude", "settings.local.json"));
            var managed = OperatingSystem.IsWindows() ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "ClaudeCode") : "/etc/claude-code";
            paths.Add(Path.Combine(managed, "managed-settings.json"));
            paths.Add(Path.Combine(configHome, "managed-settings.json"));
        }
        return paths.Select(path => new ScmCliConfigurationFile(Path.GetFullPath(path), ReadConfiguration(path))).ToArray();
    }

    /// <summary>Erneute Prüfung erkennt auch neu angelegte Konfigurationen nach dem Start.</summary>
    public static bool IsUnchanged(IReadOnlyList<ScmCliConfigurationFile> files)
    {
        try { return files.All(file => ReadConfiguration(file.Path) == file.Content); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
    }

    /// <summary>Prüft Fixture oder erfasste Startdaten; verändert weder Parameter noch Benutzerregeln.</summary>
    public static ScmExchangeAccess Evaluate(string prefix, TerminalSessionStartSpec spec, Guid runId,
        IReadOnlyList<ScmCliConfigurationFile> files)
    {
        if (string.IsNullOrWhiteSpace(spec.WorkingDirectory) || !Path.IsPathFullyQualified(spec.WorkingDirectory))
            return new(ScmExchangeAccessStatus.Unknown, "Das tatsächliche CLI-Arbeitsverzeichnis ist nicht bekannt.", string.Empty, runId, string.Empty);
        var workdir = Path.GetFullPath(spec.WorkingDirectory);
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            prefix + "\n" + workdir + "\n" + spec.Arguments + "\n" + string.Join("\n", files.Select(file => file.Path + "\n" + file.Content)))));
        ScmExchangeAccess Result(ScmExchangeAccessStatus status, string reason) => new(status, reason, workdir, runId, fingerprint);
        try
        {
            ScmEventExchangeProtocol.EnsureSafePath(workdir);
            if (prefix == "Softwareschmiede.KiSimulator")
            {
                if (spec.EnvironmentVariables.TryGetValue("SOFTWARESCHMIEDE_SCM_SIMULATOR_READONLY", out var readOnly) && readOnly == "1")
                    return Result(ScmExchangeAccessStatus.Denied, "Simulator ist schreibgeschützt gestartet; regulärer Neustart erforderlich.");
                return spec.EnvironmentVariables.TryGetValue("SOFTWARESCHMIEDE_SCM_SIMULATOR", out var enabled) && enabled == "1"
                    ? Result(ScmExchangeAccessStatus.Supported, "Deterministischer JSON-Simulator im gestarteten Arbeitsbereich.")
                    : Result(ScmExchangeAccessStatus.Unknown, "Diese Simulator-Sitzung ist eine Shell und kein KI-Eingabekanal.");
            }
            if (prefix is not ("Softwareschmiede.Codex" or "Softwareschmiede.ClaudeCli"))
                return Result(ScmExchangeAccessStatus.Unknown, "Für diese CLI ist kein Antwortkanal-Adapter verfügbar.");
            var tokens = Tokenize(spec.Arguments);
            string? mode = null;
            foreach (var file in files.Where(file => file.Content is not null))
            {
                if (Path.GetFileName(file.Path).Contains("managed", StringComparison.OrdinalIgnoreCase) ||
                    Path.GetFileName(file.Path) == "requirements.toml")
                    return Result(ScmExchangeAccessStatus.Unknown, "Managed-Berechtigungen benötigen einen unterstützten effektiven Startvertrag.");
                if (prefix == "Softwareschmiede.Codex")
                {
                    string section = "";
                    var seenKeys = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var line in file.Content!.Split('\n').Select(line => line.Trim()))
                    {
                        if (line.Length == 0 || line.StartsWith('#')) continue;
                        if (line.StartsWith('['))
                        {
                            section = line;
                            if (section is not ("[sandbox_workspace_write]" or "[features]") &&
                                !section.StartsWith("[projects.", StringComparison.Ordinal))
                                return Result(ScmExchangeAccessStatus.Unknown, "Codex-Profil oder Konfigurationsabschnitt ist nicht auflösbar.");
                            continue;
                        }
                        var match = Regex.Match(line, "^([a-z_]+)\\s*=\\s*(\"[^\"]*\"|true|false)\\s*(?:#.*)?$", RegexOptions.CultureInvariant);
                        if (!match.Success) return Result(ScmExchangeAccessStatus.Unknown, "Codex-Konfiguration enthält nicht unterstützte Vorgaben.");
                        var key = match.Groups[1].Value;
                        var value = match.Groups[2].Value.Trim('"');
                        if (!seenKeys.Add(section + "/" + key))
                            return Result(ScmExchangeAccessStatus.Unknown, "Codex-Konfiguration enthält doppelte Vorgaben.");
                        if (section == "[features]")
                        {
                            if (key is "shell_tool" or "apply_patch_freeform")
                            {
                                if (value == "false") return Result(ScmExchangeAccessStatus.Denied, "Codex-Dateitools sind deaktiviert.");
                            }
                            else return Result(ScmExchangeAccessStatus.Unknown, "Unbekannte Codex-Toolkonfiguration.");
                        }
                        else if (section == "[sandbox_workspace_write]")
                        {
                            if (key is not ("network_access" or "exclude_tmpdir_env_var" or "exclude_slash_tmp"))
                                return Result(ScmExchangeAccessStatus.Unknown, "Unbekannte Codex-Schreibwurzel oder Sandbox-Regel.");
                        }
                        else if (section.StartsWith("[projects.", StringComparison.Ordinal))
                        {
                            // Per-directory trust decisions never change the effective sandbox rights.
                            if (key != "trust_level" || value is not ("trusted" or "untrusted"))
                                return Result(ScmExchangeAccessStatus.Unknown, "Codex-Projektvertrauen ist nicht sicher auflösbar.");
                        }
                        else if (key == "sandbox_mode")
                        {
                            if (value == "read-only") return Result(ScmExchangeAccessStatus.Denied, "Codex ist schreibgeschützt gestartet.");
                            mode = value;
                        }
                        else if (key is not ("model" or "model_reasoning_effort" or "approval_policy"))
                            return Result(ScmExchangeAccessStatus.Unknown, "Codex-Konfigurationsvorgabe ist nicht unterstützt.");
                    }
                }
                else
                {
                    using var document = JsonDocument.Parse(file.Content!);
                    foreach (var field in document.RootElement.EnumerateObject())
                    {
                        if (field.Name is "model" or "$schema") continue;
                        if (field.Name != "permissions")
                            return Result(ScmExchangeAccessStatus.Unknown, "Claude-Konfiguration enthält nicht auflösbare Tools, Hooks oder Vorgaben.");
                        foreach (var rule in field.Value.EnumerateObject())
                        {
                            if (rule.Name == "defaultMode") mode = rule.Value.GetString();
                            else if (rule.Name is "deny" or "ask")
                            {
                                if (rule.Value.GetArrayLength() != 0)
                                    return Result(ScmExchangeAccessStatus.Denied, "Claude-Dateitoolzugriff ist durch Deny-/Ask-Regeln eingeschränkt.");
                            }
                            else if (rule.Name == "allow") { /* acceptEdits still required; allow never cancels deny. */ }
                            else return Result(ScmExchangeAccessStatus.Unknown, "Unbekannte Claude-Berechtigungsvorgabe.");
                        }
                    }
                }
            }
            for (var index = 0; index < tokens.Count; index++)
            {
                var key = tokens[index];
                if (key is "--model" or "-m")
                {
                    if (++index >= tokens.Count) return Result(ScmExchangeAccessStatus.Unknown, "CLI-Modellparameter ist unvollständig.");
                    continue;
                }
                if (prefix == "Softwareschmiede.Codex" && key is "--sandbox" or "-s" or "--ask-for-approval" or "-a")
                {
                    if (++index >= tokens.Count) return Result(ScmExchangeAccessStatus.Unknown, "Codex-Parameter ist unvollständig.");
                    if (key is "--sandbox" or "-s")
                    {
                        if (tokens[index] == "read-only") return Result(ScmExchangeAccessStatus.Denied, "Codex ist schreibgeschützt gestartet.");
                        mode = tokens[index];
                    }
                    continue;
                }
                if (prefix == "Softwareschmiede.ClaudeCli" && key == "--permission-mode")
                {
                    if (++index >= tokens.Count) return Result(ScmExchangeAccessStatus.Unknown, "Claude-Permission-Modus fehlt.");
                    mode = tokens[index];
                    continue;
                }
                return Result(ScmExchangeAccessStatus.Unknown, "Freie CLI-Parameter, Profile, Arbeitsverzeichnis- oder Tool-Overrides sind nicht sicher auflösbar.");
            }
            var supported = prefix == "Softwareschmiede.Codex" ? mode == "workspace-write" : mode == "acceptEdits";
            return supported
                ? Result(ScmExchangeAccessStatus.Supported, "Erfasste CLI-Konfiguration erlaubt Read/Write im tatsächlichen Arbeitsverzeichnis.")
                : Result(ScmExchangeAccessStatus.Unknown, "Keine unterstützte automatische Dateifreigabe (Codex workspace-write / Claude acceptEdits) erfasst.");
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        {
            return Result(ScmExchangeAccessStatus.Unknown, "CLI-Konfiguration oder Arbeitsverzeichnis konnte nicht sicher geprüft werden.");
        }
    }

    private static string? ReadConfiguration(string path)
    {
        ScmEventExchangeProtocol.EnsureSafePath(path);
        if (!File.Exists(path)) return null;
        if (new FileInfo(path).Length > 1024 * 1024) throw new IOException("CLI-Konfiguration überschreitet das Größenlimit.");
        return File.ReadAllText(path);
    }

    private static List<string> Tokenize(string arguments)
    {
        var tokens = new List<string>();
        var current = new StringBuilder();
        char quote = '\0';
        foreach (var c in arguments)
        {
            if (c is '\r' or '\n' or '\0') throw new ArgumentException("Steuerzeichen in CLI-Parametern.");
            if (c is '"' or '\'')
            {
                if (quote == '\0') quote = c;
                else if (quote == c) quote = '\0';
                else current.Append(c);
            }
            else if (char.IsWhiteSpace(c) && quote == '\0')
            {
                if (current.Length > 0) { tokens.Add(current.ToString()); current.Clear(); }
            }
            else current.Append(c);
        }
        if (quote != '\0') throw new ArgumentException("Unvollständige CLI-Parameter.");
        if (current.Length > 0) tokens.Add(current.ToString());
        return tokens;
    }
}
