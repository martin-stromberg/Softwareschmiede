using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Softwareschmiede.App.ViewModels;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;

namespace Softwareschmiede.App.Services;

/// <summary>Orchestriert den Ablauf "Autonome Aufgabe initialisieren": öffnet den Initialisierungsdialog, lädt die aktualisierte Aufgabe und zeigt die Detail-Ansicht an.</summary>
public sealed class AutonomAufgabeStartService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IDialogService _dialogService;
    private readonly AufgabeService _aufgabeService;
    private readonly ILogger<AutonomAufgabeStartService> _logger;

    /// <inheritdoc cref="AutonomAufgabeStartService"/>
    public AutonomAufgabeStartService(
        IServiceProvider serviceProvider,
        IDialogService dialogService,
        AufgabeService aufgabeService,
        ILogger<AutonomAufgabeStartService> logger)
    {
        _serviceProvider = serviceProvider;
        _dialogService = dialogService;
        _aufgabeService = aufgabeService;
        _logger = logger;
    }

    /// <summary>Zeigt den Initialisierungsdialog für <paramref name="aufgabe"/> an und öffnet bei Erfolg die Detail-Ansicht. Gibt <see langword="null"/> zurück, wenn der Dialog abgebrochen wurde.</summary>
    public async Task<AutonomAufgabeStartResult?> StarteAsync(Aufgabe aufgabe, CancellationToken ct)
    {
        var aktuelleAufgabe = aufgabe;
        try
        {
            // Aktuellsten Stand laden statt des ggf. veralteten, im ViewModel gecachten aufgabe-Parameters zu
            // verwenden (LokalerKlonPfad wird erst beim Starten der Aufgabe gesetzt und könnte sich seit dem
            // letzten Laden dieser Ansicht geändert haben).
            aktuelleAufgabe = await _aufgabeService.GetDetailAsync(aufgabe.Id, ct) ?? aufgabe;

            var dialogVm = _serviceProvider.GetRequiredService<AutonomAufgabeInitialisierungsDialogViewModel>();
            dialogVm.Initialize(aktuelleAufgabe);
            await dialogVm.LadeAsync(ct);

            var konfiguration = await _dialogService.ShowAutonomAufgabeInitialisierungsDialogAsync(dialogVm, ct);
            if (konfiguration is null)
            {
                return null;
            }

            aktuelleAufgabe = await _aufgabeService.GetDetailAsync(aufgabe.Id, ct) ?? aktuelleAufgabe;

            var detailVm = new AutonomAufgabeDetailViewModel(
                aktuelleAufgabe,
                konfiguration,
                _serviceProvider.GetRequiredService<ProjektleiterAgentService>(),
                _serviceProvider.GetRequiredService<SessionManagementService>(),
                _serviceProvider.GetRequiredService<ILogger<AutonomAufgabeDetailViewModel>>());
            return new AutonomAufgabeStartResult(aktuelleAufgabe, null, detailVm);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Autonome Aufgabe {AufgabeId} konnte nicht initialisiert oder angezeigt werden.", aufgabe.Id);
            return new AutonomAufgabeStartResult(
                aktuelleAufgabe,
                $"Autonome Aufgabe konnte nicht initialisiert oder angezeigt werden: {ex.Message}",
                null);
        }
    }
}
