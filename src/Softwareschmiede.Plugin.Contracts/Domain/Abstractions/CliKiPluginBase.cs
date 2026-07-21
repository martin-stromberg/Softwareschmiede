using System.Diagnostics;
using System.Text;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Domain.Abstractions;

/// <summary>Gemeinsame Basisklasse für CLI-basierte KI-Plugins.</summary>
public abstract class CliKiPluginBase : IKiPlugin
{
    /// <summary>
    /// Provider-spezifischer Bezeichner für Dateinamen.
    /// Beispiele: <c>copilot</c>, <c>claude</c>.
    /// </summary>
    public abstract string ProviderDateiPraefix { get; }

    public abstract string PluginName { get; }
    public abstract string PluginPrefix { get; }
    public abstract PluginType PluginType { get; }
    public abstract IReadOnlyList<PluginSettingGroup> GetSettingGroups();

    /// <summary>Konstruiert ProcessStartInfo für den CLI-Aufruf.</summary>
    /// <param name="localRepoPath">Lokales Arbeitsverzeichnis.</param>
    /// <param name="parameters">Optionale Parameter (z.B. Session-ID).</param>
    protected abstract ProcessStartInfo BuildProcessStartInfo(string localRepoPath, string? parameters);

    /// <inheritdoc/>
    public Task<ProcessStartInfo> StartCliAsync(string localRepoPath, string? parameters = null, CancellationToken ct = default)
    {
        var psi = BuildProcessStartInfo(localRepoPath, parameters);
        return Task.FromResult(psi);
    }

    /// <summary>
    /// Ruft die CLI mit <c>--help</c> auf und gibt den Ausgabetext zurück.
    /// Gibt <c>null</c> zurück bei Timeout, Prozessfehler oder fehlender CLI.
    /// </summary>
    public virtual Task<string?> GetCliHelpTextAsync(CancellationToken ct = default)
        => RunHelpCommandAsync(ProviderDateiPraefix, ct);

    /// <summary>
    /// Führt <paramref name="fileName"/> mit <c>--help</c> aus und gibt die Ausgabe zurück.
    /// </summary>
    protected static async Task<string?> RunHelpCommandAsync(string fileName, CancellationToken ct, TimeSpan? helpTimeout = null)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(helpTimeout ?? TimeSpan.FromSeconds(10));

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = "--help",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                }
            };

            process.Start();

            var stdoutTask = process.StandardOutput.ReadToEndAsync(cts.Token);
            var stderrTask = process.StandardError.ReadToEndAsync(cts.Token);

            try
            {
                await process.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                try { process.Kill(entireProcessTree: true); } catch { /* ignorieren */ }
                try { await stdoutTask.ConfigureAwait(false); } catch { /* ignorieren */ }
                try { await stderrTask.ConfigureAwait(false); } catch { /* ignorieren */ }
                return null;
            }

            var stdout = await stdoutTask;
            var stderr = await stderrTask;
            return string.IsNullOrWhiteSpace(stdout)
                ? (string.IsNullOrWhiteSpace(stderr) ? null : stderr)
                : stdout;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Gibt den konfigurierten Executable-Pfad aus dem Credential Store zurück,
    /// oder <paramref name="defaultExecutable"/> als Fallback.
    /// </summary>
    protected static string ResolveExecutablePath(ICredentialStore credentialStore, string pluginPrefix, string defaultExecutable)
    {
        var configuredPath = credentialStore.GetCredential($"{pluginPrefix}.ExecutablePath");
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return configuredPath.Trim().Trim('"');
        }
        return defaultExecutable;
    }

    /// <summary>
    /// Liest den gespeicherten <c>CommandLineParameters</c>-Wert aus dem Credential Store
    /// und hängt ihn an <paramref name="psi"/>.<see cref="ProcessStartInfo.Arguments"/> an.
    /// </summary>
    protected static void AppendCommandLineParameters(ProcessStartInfo psi, ICredentialStore credentialStore, string pluginPrefix)
    {
        var commandLineParameters = credentialStore.GetCredential($"{pluginPrefix}.CommandLineParameters");
        if (!string.IsNullOrWhiteSpace(commandLineParameters))
        {
            psi.Arguments = string.IsNullOrWhiteSpace(psi.Arguments)
                ? commandLineParameters
                : $"{psi.Arguments} {commandLineParameters}";
        }
    }

    /// <summary>
    /// Startet <paramref name="executablePath"/> mit <c>--version</c> und prüft ob ExitCode 0.
    /// </summary>
    protected static async Task<bool> CheckHealthWithVersionCommandAsync(string executablePath, CancellationToken ct)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = executablePath,
                    Arguments = "--version",
                    UseShellExecute = false,
                    RedirectStandardOutput = false,
                    RedirectStandardError = false,
                    CreateNoWindow = true,
                }
            };

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(10));
            process.Start();
            try
            {
                await process.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                try { process.Kill(entireProcessTree: true); } catch { /* ignorieren */ }
            }
            return process.ExitCode == 0;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <inheritdoc/>
    public virtual string GetProcessWindowTitle(Guid aufgabeId) => string.Empty;

    /// <inheritdoc/>
    public abstract bool SupportsSessionContinuation();

    /// <inheritdoc/>
    public abstract Task<bool> CheckHealthAsync(CancellationToken ct = default);

    /// <summary>Erstellt den Prompt für eine einmalige Issue-Template-Ausfüllung.</summary>
    protected static string BuildIssueTemplateFillPrompt(string templateBody, string? originalRequirement)
        => $"""
           Fülle das folgende GitHub-/Issue-Template anhand der Originalanforderung aus.
           Gib ausschließlich den fertigen Issue-Body zurück. Keine Einleitung, keine Markdown-Codefences, keine Erklärungen.
           Erhalte sinnvolle Überschriften und Platzhalter, wenn sie ohne weitere Fachinformation nicht sicher ausgefüllt werden können.

           Template:
           {templateBody}

           Originalanforderung:
           {originalRequirement ?? string.Empty}
           """;

    /// <summary>Führt einen CLI-basierten Einmal-Prompt aus und gibt stdout zurück.</summary>
    protected static async Task<string> RunOneShotTextGenerationAsync(
        ProcessStartInfo psi,
        string? standardInput,
        CancellationToken ct)
    {
        psi.UseShellExecute = false;
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;
        psi.RedirectStandardInput = standardInput is not null;
        psi.StandardOutputEncoding = Encoding.UTF8;
        psi.StandardErrorEncoding = Encoding.UTF8;
        psi.StandardInputEncoding = Encoding.UTF8;
        psi.CreateNoWindow = true;

        if (!psi.EnvironmentVariables.ContainsKey("PATH"))
        {
            psi.EnvironmentVariables["PATH"] = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        }

        using var process = new Process { StartInfo = psi };
        process.Start();

        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);

        if (standardInput is not null)
        {
            await process.StandardInput.WriteAsync(standardInput.AsMemory(), ct).ConfigureAwait(false);
            process.StandardInput.Close();
        }

        try
        {
            await process.WaitForExitAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { /* Best effort. */ }
            throw;
        }

        var stdout = await stdoutTask.ConfigureAwait(false);
        var stderr = await stderrTask.ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            var details = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
            throw new InvalidOperationException($"KI-CLI wurde mit ExitCode {process.ExitCode} beendet: {details.Trim()}");
        }

        var result = stdout.Trim();
        if (string.IsNullOrWhiteSpace(result))
        {
            throw new InvalidOperationException("KI-CLI hat keinen Text zurückgegeben.");
        }

        return result;
    }

    /// <summary>
    /// Liest alle Agenten aus dem angegebenen Unterverzeichnis eines Agentenpakets.
    /// </summary>
    protected static IReadOnlyList<AgentInfo> DiscoverAgents(string agentPackagePath, string relativeAgentDirectory)
    {
        if (string.IsNullOrWhiteSpace(agentPackagePath))
        {
            return [];
        }

        var agentDirectory = Path.Combine(agentPackagePath, relativeAgentDirectory);
        if (!Directory.Exists(agentDirectory))
        {
            return [];
        }

        return Directory.GetFiles(agentDirectory, "*.md", SearchOption.AllDirectories)
            .Select(filePath => new AgentInfo(
                Path.GetFileNameWithoutExtension(filePath).Replace(".agent", string.Empty, StringComparison.OrdinalIgnoreCase),
                ReadAgentDescription(filePath),
                filePath))
            .OrderBy(agent => agent.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string? ReadAgentDescription(string agentFilePath)
    {
        try
        {
            var lines = File.ReadLines(agentFilePath).Take(10).ToList();
            var descLine = lines.FirstOrDefault(line => line.TrimStart().StartsWith("description:", StringComparison.OrdinalIgnoreCase));
            if (descLine is not null)
            {
                return descLine.Split(':', 2).ElementAtOrDefault(1)?.Trim().Trim('"');
            }

            return lines.FirstOrDefault(line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith("---", StringComparison.Ordinal));
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }
}
