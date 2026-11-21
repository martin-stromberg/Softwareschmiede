using System.IO;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.App.Services.Testing;

// Nur der Remote-Transport wird ersetzt: der Produktions-Clone-/Branch-/Workspace-Code führt echtes git aus.
internal sealed class ScmE2EGitRunner(ICliRunner inner, ScmE2ETestConfiguration configuration) : ICliRunner
{
    public Task<CliResult> RunAsync(string command, IEnumerable<string> args, string? workingDirectory,
        IDictionary<string, string>? environmentVariables, CancellationToken ct = default)
    {
        var arguments = args.ToArray();
        if (command != "git") throw new InvalidOperationException("SCM-Fixture erlaubt nur lokale Git-Prozesse.");
        for (var index = 0; index < arguments.Length; index++)
            if (arguments[index] == "https://github.com/scm-owner/scm-e2e")
                arguments[index] = Path.Combine(configuration.TestRoot, "origin");
            else if (arguments[index].Contains("://", StringComparison.Ordinal))
                throw new InvalidOperationException("SCM-Fixture darf keine entfernte Git-Verbindung öffnen.");
        return inner.RunAsync(command, arguments, workingDirectory, null, ct);
    }

    public IAsyncEnumerable<string> StreamAsync(string command, IEnumerable<string> args, string? workingDirectory,
        IDictionary<string, string>? environmentVariables, CancellationToken ct = default)
        => throw new InvalidOperationException("SCM-Fixture verwendet keinen externen Git-Stream.");
}
