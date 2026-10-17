using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Application.Services;

/// <summary>
/// Vorbelegung der Workspace-Vertrauensentscheidung der unterstützten CLIs vor dem Prozessstart.
/// Es werden ausschließlich die herstellerseitig dokumentierten, pfadgenauen Mechanismen genutzt
/// (Claude: <c>projects.&lt;dir&gt;.hasTrustDialogAccepted</c> in <c>~/.claude.json</c>;
/// Codex: <c>[projects."&lt;dir&gt;"] trust_level = "trusted"</c> in <c>config.toml</c>).
/// Vorhandene Nutzerentscheidungen und Verzeichnisse mit projektgebundener CLI-Konfiguration
/// (<c>.claude</c>/<c>.codex</c>) bleiben bewusst unberührt, damit Trust keine ungeprüften
/// projektgebundenen Hooks oder Ausführungsregeln aktiviert.
/// </summary>
public static class ScmCliWorkspaceTrust
{
    /// <summary>Legt den Trust-Status für das tatsächliche Arbeitsverzeichnis (und dessen Git-Wurzel) fest.</summary>
    public static void EnsureTrusted(string pluginPrefix, TerminalSessionStartSpec spec)
    {
        var marker = pluginPrefix switch
        {
            "Softwareschmiede.ClaudeCli" => ".claude",
            "Softwareschmiede.Codex" => ".codex",
            _ => null,
        };
        if (marker is null || string.IsNullOrWhiteSpace(spec.WorkingDirectory) ||
            !Path.IsPathFullyQualified(spec.WorkingDirectory)) return;
        var workdir = Path.GetFullPath(spec.WorkingDirectory);
        var root = GitTopLevel(workdir) ?? workdir;
        // Trust aktiviert projektgebundene Konfiguration auf dem gesamten Pfad vom Startverzeichnis
        // bis zur Git-Wurzel; jedes Vorkommen behält bewusst den manuellen Dialog.
        if (HasProjectConfiguration(workdir, root, marker)) return;
        if (pluginPrefix == "Softwareschmiede.ClaudeCli")
        {
            var configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude.json");
            EnsureClaudeTrusted(configPath, workdir);
            if (!string.Equals(root, workdir, StringComparison.OrdinalIgnoreCase)) EnsureClaudeTrusted(configPath, root);
        }
        else
        {
            var configHome = spec.EnvironmentVariables.TryGetValue("CODEX_HOME", out var codexHome) && !string.IsNullOrWhiteSpace(codexHome)
                ? codexHome
                : Environment.GetEnvironmentVariable("CODEX_HOME")
                  ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex");
            var configPath = Path.Combine(configHome, "config.toml");
            EnsureCodexTrusted(configPath, workdir);
            if (!string.Equals(root, workdir, StringComparison.OrdinalIgnoreCase)) EnsureCodexTrusted(configPath, root);
        }
    }

    /// <summary>Prüft <paramref name="marker"/>-Verzeichnisse vom Startpfad bis einschließlich der Wurzel.</summary>
    private static bool HasProjectConfiguration(string workdir, string root, string marker)
    {
        for (var directory = new DirectoryInfo(workdir); directory is not null; directory = directory.Parent)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, marker))) return true;
            if (string.Equals(directory.FullName, root, StringComparison.OrdinalIgnoreCase)) return false;
        }
        return false;
    }

    private static string? GitTopLevel(string workdir)
    {
        try
        {
            var psi = new ProcessStartInfo("git")
            {
                WorkingDirectory = workdir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            psi.ArgumentList.Add("rev-parse");
            psi.ArgumentList.Add("--show-toplevel");
            using var process = Process.Start(psi);
            if (process is null || !process.WaitForExit(5000)) return null;
            var top = process.StandardOutput.ReadToEnd().Trim();
            return process.ExitCode != 0 || string.IsNullOrWhiteSpace(top) ? null : Path.GetFullPath(top);
        }
        catch (Exception ex) when (ex is SystemException) { return null; }
    }

    /// <summary>Setzt <c>projects.&lt;dir&gt;.hasTrustDialogAccepted</c>, ohne andere Felder oder Entscheidungen zu verändern.</summary>
    internal static void EnsureClaudeTrusted(string configPath, string directory)
    {
        // Trust aktiviert projektgebundene .claude-Konfiguration; Verzeichnisse mit eigener
        // Konfiguration behalten bewusst den manuellen Dialog.
        if (Directory.Exists(Path.Combine(directory, ".claude"))) return;
        ScmEventExchangeProtocol.EnsureSafePath(configPath);
        JsonObject root;
        if (File.Exists(configPath))
        {
            try { root = JsonNode.Parse(File.ReadAllText(configPath)) as JsonObject ?? new JsonObject(); }
            catch (JsonException) { return; }
        }
        else root = new JsonObject();
        if (root["projects"] is null) root["projects"] = new JsonObject();
        if (root["projects"] is not JsonObject projects) return;
        var key = Path.GetFullPath(directory);
        var project = projects[key];
        if (project is JsonObject entry)
        {
            // Jede bereits gespeicherte Entscheidung (auch eine Ablehnung) bleibt erhalten.
            if (entry["hasTrustDialogAccepted"] is not null) return;
            entry["hasTrustDialogAccepted"] = true;
        }
        else if (project is null) projects[key] = new JsonObject { ["hasTrustDialogAccepted"] = true };
        else return;
        WriteAtomically(configPath, root.ToJsonString());
    }

    /// <summary>Hängt <c>[projects."&lt;dir&gt;"] trust_level = "trusted"</c> an, falls der Eintrag fehlt.</summary>
    internal static void EnsureCodexTrusted(string configPath, string directory)
    {
        if (Directory.Exists(Path.Combine(directory, ".codex"))) return;
        ScmEventExchangeProtocol.EnsureSafePath(configPath);
        var key = Path.GetFullPath(directory);
        var escaped = key.Replace("\\", "\\\\").Replace("\"", "\\\"");
        var content = File.Exists(configPath) ? File.ReadAllText(configPath) : string.Empty;
        // Ein vorhandener Eintrag — auch eine bewusste "untrusted"-Entscheidung — wird respektiert.
        if (content.Contains($"[projects.\"{escaped}\"]", StringComparison.Ordinal) ||
            content.Contains($"[projects.'{key}']", StringComparison.Ordinal)) return;
        var separator = content.Length == 0 || content.EndsWith('\n') ? string.Empty : "\n";
        Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
        File.AppendAllText(configPath,
            $"{separator}[projects.\"{escaped}\"]\ntrust_level = \"trusted\"\n", new UTF8Encoding(false));
    }

    /// <summary>Verhindert halb geschriebene Benutzerkonfigurationen bei parallelen CLI-Läufen.</summary>
    private static void WriteAtomically(string path, string content)
    {
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        File.WriteAllText(temporary, content, new UTF8Encoding(false));
        File.Move(temporary, path, overwrite: true);
    }
}
