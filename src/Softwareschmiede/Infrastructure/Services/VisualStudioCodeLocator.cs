using Softwareschmiede.Application.Services;

namespace Softwareschmiede.Infrastructure.Services;

/// <summary>Findet Visual Studio Code über PATH oder typische Windows-Installationspfade.</summary>
public sealed class VisualStudioCodeLocator : IVisualStudioCodeLocator
{
    private readonly Func<string, string?> _getEnvironmentVariable;
    private readonly Func<string, bool> _fileExists;

    /// <inheritdoc cref="VisualStudioCodeLocator"/>
    public VisualStudioCodeLocator()
        : this(Environment.GetEnvironmentVariable, File.Exists)
    {
    }

    /// <inheritdoc cref="VisualStudioCodeLocator"/>
    public VisualStudioCodeLocator(
        Func<string, string?> getEnvironmentVariable,
        Func<string, bool> fileExists)
    {
        _getEnvironmentVariable = getEnvironmentVariable;
        _fileExists = fileExists;
    }

    /// <inheritdoc/>
    public VisualStudioCodeAvailability Locate()
    {
        foreach (var candidate in EnumeratePathCandidates())
        {
            if (_fileExists(candidate))
                return new VisualStudioCodeAvailability(true, candidate);
        }

        foreach (var candidate in EnumerateKnownInstallCandidates())
        {
            if (_fileExists(candidate))
                return new VisualStudioCodeAvailability(true, candidate);
        }

        return VisualStudioCodeAvailability.NotAvailable;
    }

    private IEnumerable<string> EnumeratePathCandidates()
    {
        var path = _getEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
            yield break;

        foreach (var entry in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            yield return Path.Combine(entry, "code.cmd");
            yield return Path.Combine(entry, "code");
        }
    }

    private IEnumerable<string> EnumerateKnownInstallCandidates()
    {
        var localAppData = _getEnvironmentVariable("LOCALAPPDATA");
        if (!string.IsNullOrWhiteSpace(localAppData))
        {
            yield return Path.Combine(localAppData, "Programs", "Microsoft VS Code", "bin", "code.cmd");
            yield return Path.Combine(localAppData, "Programs", "Microsoft VS Code", "Code.exe");
        }

        foreach (var root in new[]
        {
            _getEnvironmentVariable("ProgramFiles"),
            _getEnvironmentVariable("ProgramFiles(x86)")
        })
        {
            if (string.IsNullOrWhiteSpace(root))
                continue;

            yield return Path.Combine(root, "Microsoft VS Code", "bin", "code.cmd");
            yield return Path.Combine(root, "Microsoft VS Code", "Code.exe");
        }
    }
}
