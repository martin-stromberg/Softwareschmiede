using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Infrastructure.Plugins;

/// <summary>Credentialfreier, austauschbarer GitHub-Ereignistransport.</summary>
public interface IGitHubEventApiClient
{
    /// <summary>Führt exakt eine API-Seite oder Mutation aus; Bodies bleiben JSON-Daten.</summary>
    Task<GitHubEventApiResponse> SendAsync(GitHubEventApiRequest request, CancellationToken ct = default);
}

/// <summary>Einzelner API-Aufruf am vom Provider validierten Host.</summary>
public sealed record GitHubEventApiRequest(string Host, string Method, string Endpoint, string? JsonBody = null);

/// <summary>Unveränderte JSON-Antwort einschließlich der für Pagination und Backoff nötigen Header.</summary>
public sealed record GitHubEventApiResponse(int StatusCode, string Body, IReadOnlyDictionary<string, string>? Headers = null);

/// <summary>Produktiver Transport über gh und den vorhandenen Credential-Store.</summary>
public sealed class GhEventApiClient(ICliRunner cliRunner, Func<IDictionary<string, string>> environmentFactory)
    : IGitHubEventApiClient
{
    /// <inheritdoc />
    public async Task<GitHubEventApiResponse> SendAsync(GitHubEventApiRequest request, CancellationToken ct = default)
    {
        var args = new List<string> { "api", "--hostname", request.Host, "--method", request.Method, "--include",
            "--header", "Accept: application/vnd.github+json", "--header", "X-GitHub-Api-Version: 2022-11-28", request.Endpoint };
        string? inputPath = null;
        try
        {
            if (request.JsonBody is not null)
            {
                inputPath = Path.Combine(Path.GetTempPath(), $"softwareschmiede-gh-{Guid.NewGuid():N}.json");
                await using (var stream = new FileStream(inputPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    var bytes = Encoding.UTF8.GetBytes(request.JsonBody);
                    await stream.WriteAsync(bytes, ct);
                }
                args.AddRange(["--input", inputPath]);
            }

            var environment = environmentFactory();
            if (!request.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)
                && environment.TryGetValue("GH_TOKEN", out var token))
                environment["GH_ENTERPRISE_TOKEN"] = token;

            var result = await cliRunner.RunAsync("gh", args, null, environment, ct);
            var normalized = result.StdOut.Replace("\r\n", "\n", StringComparison.Ordinal);
            var separator = normalized.IndexOf("\n\n", StringComparison.Ordinal);
            var headerBlock = separator >= 0 ? normalized[..separator] : string.Empty;
            var status = Regex.Match(headerBlock, @"^HTTP/\S+\s+(\d{3})", RegexOptions.CultureInvariant);
            if (!status.Success)
            {
                if (result.IsSuccess) return new(200, result.StdOut);
                // Never include stdout/stderr: gh can echo credentials or remote bodies there.
                var errorStatus = Regex.Match(result.StdErr, @"HTTP\s+(\d{3})", RegexOptions.CultureInvariant);
                if (errorStatus.Success)
                    return new(int.Parse(errorStatus.Groups[1].Value, CultureInfo.InvariantCulture), "{}");
                throw new ScmProviderException(ScmProviderFailureKind.Temporary,
                    "GitHub-Transport nicht erreichbar.", outcomeUncertain: request.Method != "GET" && request.Endpoint != "graphql");
            }

            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var line in headerBlock.Split('\n').Skip(1))
            {
                var colon = line.IndexOf(':');
                if (colon > 0) headers[line[..colon].Trim()] = line[(colon + 1)..].Trim();
            }
            return new(int.Parse(status.Groups[1].Value, CultureInfo.InvariantCulture), normalized[(separator + 2)..], headers);
        }
        catch (ScmProviderException) { throw; }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            throw new ScmProviderException(ScmProviderFailureKind.Temporary,
                "GitHub-Transport konnte nicht ausgeführt werden.", outcomeUncertain: request.Method != "GET");
        }
        finally
        {
            if (inputPath is not null) File.Delete(inputPath);
        }
    }
}
