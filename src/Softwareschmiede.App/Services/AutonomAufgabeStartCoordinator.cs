using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Softwareschmiede.App.ViewModels;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;

namespace Softwareschmiede.App.Services;

/// <summary>Orchestriert den Ablauf "Autonome Aufgabe initialisieren": öffnet den Initialisierungsdialog, lädt die aktualisierte Aufgabe und zeigt die Detail-Ansicht an.</summary>
public sealed class AutonomAufgabeStartCoordinator
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IDialogService _dialogService;
    private readonly AufgabeService _aufgabeService;
    private readonly ILogger<AutonomAufgabeStartCoordinator> _logger;

    /// <inheritdoc cref="AutonomAufgabeStartCoordinator"/>
    public AutonomAufgabeStartCoordinator(
        IServiceProvider serviceProvider,
        IDialogService dialogService,
        AufgabeService aufgabeService,
        ILogger<AutonomAufgabeStartCoordinator> logger)
    {
        _serviceProvider = serviceProvider;
        _dialogService = dialogService;
        _aufgabeService = aufgabeService;
        _logger = logger;
    }

    /// <summary>Zeigt den Initialisierungsdialog für <paramref name="aufgabe"/> an und öffnet bei Erfolg die Detail-Ansicht. Gibt <see langword="null"/> zurück, wenn der Dialog abgebrochen wurde.</summary>
    public async Task<AutonomAufgabeStartErgebnis?> StarteAsync(Guid aufgabeId, Aufgabe aufgabe, CancellationToken ct)
    {
        // Aktuellsten Stand laden statt des ggf. veralteten, im ViewModel gecachten aufgabe-Parameters zu
        // verwenden (LokalerKlonPfad wird erst beim Starten der Aufgabe gesetzt und könnte sich seit dem
        // letzten Laden dieser Ansicht geändert haben).
        var aktuelleAufgabe = await _aufgabeService.GetDetailAsync(aufgabeId, ct) ?? aufgabe;

        var dialogVm = _serviceProvider.GetRequiredService<AutonomAufgabeInitialisierungsDialogViewModel>();
        dialogVm.Initialize(aktuelleAufgabe);

        var konfiguration = await _dialogService.ShowAutonomAufgabeInitialisierungsDialogAsync(dialogVm, ct);
        if (konfiguration is null)
        {
            return null;
        }

        var aktualisierteAufgabe = await _aufgabeService.GetDetailAsync(aufgabeId, ct);

        try
        {
            var detailVm = _serviceProvider.GetRequiredService<AutonomAufgabeDetailViewModel>();
            detailVm.Initialize(aktualisierteAufgabe ?? aufgabe, konfiguration);
            await _dialogService.ShowAutonomAufgabeDetailAsync(detailVm, ct);
            return new AutonomAufgabeStartErgebnis(aktualisierteAufgabe, null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Detail-Ansicht der Autonomen Aufgabe {AufgabeId} konnte nicht angezeigt werden.", aufgabeId);
            return new AutonomAufgabeStartErgebnis(
                aktualisierteAufgabe,
                $"Detail-Ansicht der Autonomen Aufgabe konnte nicht angezeigt werden: {ex.Message}");
        }
    }
}
