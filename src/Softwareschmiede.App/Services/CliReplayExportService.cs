using Microsoft.Extensions.Logging;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Infrastructure.Terminal;

namespace Softwareschmiede.App.Services;

/// <summary>Exportiert den Rohbyte-Mitschnitt einer Terminal-Session als .clireplay-Datei.</summary>
public interface ICliReplayExportService
{
    /// <summary>Schreibt die aufgezeichnete CLI-Ausgabe einer Aufgabe in die angegebene .clireplay-Datei.</summary>
    /// <param name="workItemId">ID der Aufgabe.</param>
    /// <param name="targetPath">Der Ziel-Dateipfad.</param>
    /// <param name="ct">Abbruch-Token.</param>
    /// <exception cref="InvalidOperationException">Es liegt keine Aufzeichnung für die Aufgabe vor.</exception>
    Task ExportCliReplayAsync(Guid workItemId, string targetPath, CancellationToken ct = default);

    /// <summary>Prüft, ob für die Aufgabe ein Mitschnitt vorliegt (Vorab-Prüfung vor dem Speicherdialog).</summary>
    /// <param name="workItemId">ID der Aufgabe.</param>
    /// <returns><c>true</c>, wenn eine Aufzeichnung exportiert werden kann.</returns>
    bool HatAufzeichnung(Guid workItemId);
}

/// <summary>Implementierung des CLI-Replay-Exports über den im <see cref="AiExecutionService"/> gehaltenen Mitschnitt.</summary>
public sealed class CliReplayExportService : ICliReplayExportService
{
    private readonly AiExecutionService _kiAusfuehrungsService;
    private readonly CliReplayRecordingStore _store;
    private readonly ILogger<CliReplayExportService> _logger;

    /// <inheritdoc cref="CliReplayExportService"/>
    public CliReplayExportService(
        AiExecutionService kiAusfuehrungsService,
        CliReplayRecordingStore store,
        ILogger<CliReplayExportService> logger)
    {
        _kiAusfuehrungsService = kiAusfuehrungsService;
        _store = store;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task ExportCliReplayAsync(Guid workItemId, string targetPath, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);

        var recording = _kiAusfuehrungsService.GetCliRecording(workItemId)
            ?? throw new InvalidOperationException("Für diese Aufgabe liegt keine Aufzeichnung vor.");

        _logger.LogDebug(
            "Exportiere CLI-Aufzeichnung von Aufgabe {AufgabeId} ({ChunkCount} Chunks) nach {ZielPfad}.",
            workItemId, recording.Chunks.Count, targetPath);
        await _store.SaveAsync(targetPath, recording, ct);
    }

    /// <inheritdoc/>
    public bool HatAufzeichnung(Guid workItemId)
        => _kiAusfuehrungsService.GetCliRecording(workItemId) is not null;
}
