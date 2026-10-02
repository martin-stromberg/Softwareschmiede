using Microsoft.Extensions.Logging;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Infrastructure.Terminal;

namespace Softwareschmiede.App.Services;

/// <summary>Exportiert den Rohbyte-Mitschnitt einer Terminal-Session als .clireplay-Datei.</summary>
public interface ICliReplayExportService
{
    /// <summary>Schreibt die aufgezeichnete CLI-Ausgabe einer Aufgabe in die angegebene .clireplay-Datei.</summary>
    /// <param name="aufgabeId">ID der Aufgabe.</param>
    /// <param name="zielPfad">Der Ziel-Dateipfad.</param>
    /// <param name="ct">Abbruch-Token.</param>
    /// <exception cref="InvalidOperationException">Es liegt keine Aufzeichnung für die Aufgabe vor.</exception>
    Task ExportCliReplayAsync(Guid aufgabeId, string zielPfad, CancellationToken ct = default);

    /// <summary>Prüft, ob für die Aufgabe ein Mitschnitt vorliegt (Vorab-Prüfung vor dem Speicherdialog).</summary>
    /// <param name="aufgabeId">ID der Aufgabe.</param>
    /// <returns><c>true</c>, wenn eine Aufzeichnung exportiert werden kann.</returns>
    bool HatAufzeichnung(Guid aufgabeId);
}

/// <summary>Implementierung des CLI-Replay-Exports über den im <see cref="KiAusfuehrungsService"/> gehaltenen Mitschnitt.</summary>
public sealed class CliReplayExportService : ICliReplayExportService
{
    private readonly KiAusfuehrungsService _kiAusfuehrungsService;
    private readonly CliReplayAufzeichnungStore _store;
    private readonly ILogger<CliReplayExportService> _logger;

    /// <inheritdoc cref="CliReplayExportService"/>
    public CliReplayExportService(
        KiAusfuehrungsService kiAusfuehrungsService,
        CliReplayAufzeichnungStore store,
        ILogger<CliReplayExportService> logger)
    {
        _kiAusfuehrungsService = kiAusfuehrungsService;
        _store = store;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task ExportCliReplayAsync(Guid aufgabeId, string zielPfad, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(zielPfad);

        var aufzeichnung = _kiAusfuehrungsService.GetCliAufzeichnung(aufgabeId)
            ?? throw new InvalidOperationException("Für diese Aufgabe liegt keine Aufzeichnung vor.");

        _logger.LogDebug(
            "Exportiere CLI-Aufzeichnung von Aufgabe {AufgabeId} ({ChunkCount} Chunks) nach {ZielPfad}.",
            aufgabeId, aufzeichnung.Chunks.Count, zielPfad);
        await _store.SpeichernAsync(zielPfad, aufzeichnung, ct);
    }

    /// <inheritdoc/>
    public bool HatAufzeichnung(Guid aufgabeId)
        => _kiAusfuehrungsService.GetCliAufzeichnung(aufgabeId) is not null;
}
