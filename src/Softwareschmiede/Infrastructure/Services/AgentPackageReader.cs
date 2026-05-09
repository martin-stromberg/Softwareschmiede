using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Infrastructure.Services;

/// <summary>Liest Agentenpakete aus dem Dateisystem.</summary>
public sealed class AgentPackageReader : IAgentPackageService
{
    private readonly ILogger<AgentPackageReader> _logger;
    private readonly string _packagesBasePath;

    /// <summary>Erstellt eine neue Instanz des <see cref="AgentPackageReader"/>.</summary>
    public AgentPackageReader(ILogger<AgentPackageReader> logger, IWebHostEnvironment env)
    {
        _logger = logger;
        _packagesBasePath = Path.Combine(env.ContentRootPath, "agent-packages");
        EnsurePackagesDirectoryExists();
    }

    private void EnsurePackagesDirectoryExists()
    {
        if (!Directory.Exists(_packagesBasePath))
        {
            Directory.CreateDirectory(_packagesBasePath);
            _logger.LogInformation("Agentenpaket-Verzeichnis erstellt: {Path}", _packagesBasePath);
        }
    }

    /// <inheritdoc/>
    public Task<IEnumerable<AgentPackageInfo>> GetPackagesAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Lade Agentenpakete aus {Path}", _packagesBasePath);

        var packages = Directory.GetDirectories(_packagesBasePath)
            .Select(dir => ReadPackage(dir))
            .ToList();

        _logger.LogInformation("{Count} Agentenpakete gefunden.", packages.Count);
        return Task.FromResult<IEnumerable<AgentPackageInfo>>(packages);
    }

    /// <inheritdoc/>
    public Task<AgentPackageInfo?> GetPackageAsync(string name, CancellationToken ct = default)
    {
        var packagePath = Path.Combine(_packagesBasePath, name);
        if (!Directory.Exists(packagePath))
            return Task.FromResult<AgentPackageInfo?>(null);

        return Task.FromResult<AgentPackageInfo?>(ReadPackage(packagePath));
    }

    private AgentPackageInfo ReadPackage(string packagePath)
    {
        var name = Path.GetFileName(packagePath);
        var files = Directory.GetFiles(packagePath, "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(packagePath, f))
            .ToList();

        // Agenten aus .agent.md Dateien ermitteln (GitHub Copilot verwendet .agent.md Dateien)
        var agents = Directory.GetFiles(packagePath, "*.agent.md", SearchOption.AllDirectories)
            .Select(f => new AgentInfo(
                Name: Path.GetFileNameWithoutExtension(f).Replace(".agent", string.Empty),
                Beschreibung: ReadFirstLine(f),
                DateiPfad: f
            ))
            .ToList();

        return new AgentPackageInfo(name, packagePath, agents, files);
    }

    private static string? ReadFirstLine(string filePath)
    {
        try { return File.ReadLines(filePath).FirstOrDefault(); }
        catch { return null; }
    }
}
