using System.IO;
using System.Text;
using Microsoft.Extensions.Logging;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Enums;

namespace Softwareschmiede.App.Services;

/// <summary>Exportiert die protokollierte CLI-Rohausgabe als .raw-Datei.</summary>
public interface ICliRawExportService
{
    /// <summary>Schreibt die CLI-Rohausgabe einer Aufgabe in die angegebene .raw-Datei.</summary>
    Task ExportCliRawAsync(Guid workItemId, string targetPath, CancellationToken ct = default);
}

/// <summary>Implementierung des CLI-Raw-Exports mit persistierter Protokollbasis.</summary>
public sealed class CliRawExportService : ICliRawExportService
{
    private readonly LogService _logService;
    private readonly ILogger<CliRawExportService> _logger;

    /// <inheritdoc cref="CliRawExportService"/>
    public CliRawExportService(LogService logService, ILogger<CliRawExportService> logger)
    {
        _logService = logService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task ExportCliRawAsync(Guid workItemId, string targetPath, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);

        var log = await _logService.GetByWorkItemAsync(workItemId, ct);
        var content = string.Join(
            Environment.NewLine,
            log
                .Where(entry => entry.Type == LogEntryType.CliOutput)
                .Select(entry => entry.Content));

        _logger.LogDebug("Exportiere {Anzahl} CLI-Rohausgabezeilen nach {ZielPfad}.", log.Count(e => e.Type == LogEntryType.CliOutput), targetPath);
        await File.WriteAllTextAsync(targetPath, content, new UTF8Encoding(false), ct);
    }
}
