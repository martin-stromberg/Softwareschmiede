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
    Task ExportCliRawAsync(Guid aufgabeId, string zielPfad, CancellationToken ct = default);
}

/// <summary>Implementierung des CLI-Raw-Exports mit persistierter Protokollbasis.</summary>
public sealed class CliRawExportService : ICliRawExportService
{
    private readonly ProtokollService _protokollService;
    private readonly ILogger<CliRawExportService> _logger;

    /// <inheritdoc cref="CliRawExportService"/>
    public CliRawExportService(ProtokollService protokollService, ILogger<CliRawExportService> logger)
    {
        _protokollService = protokollService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task ExportCliRawAsync(Guid aufgabeId, string zielPfad, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(zielPfad);

        var protokolle = await _protokollService.GetByAufgabeAsync(aufgabeId, ct);
        var inhalt = string.Join(
            Environment.NewLine,
            protokolle
                .Where(eintrag => eintrag.Typ == ProtokollTyp.CliOutput)
                .Select(eintrag => eintrag.Inhalt));

        _logger.LogDebug("Exportiere {Anzahl} CLI-Rohausgabezeilen nach {ZielPfad}.", protokolle.Count(e => e.Typ == ProtokollTyp.CliOutput), zielPfad);
        await File.WriteAllTextAsync(zielPfad, inhalt, new UTF8Encoding(false), ct);
    }
}
