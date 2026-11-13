using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Softwareschmiede.Application.Services;

namespace Softwareschmiede.App.Services;

/// <summary>
/// Persistiert die Auto-Shutdown-Nebenwerte (Erzwingen-Flag und Toleranzzeit) über den scoped
/// <see cref="AppEinstellungService"/> in eigenen DI-Scopes (Muster <see cref="AufgabenListenModusService"/>).
/// Der Aktivierungszustand des Auto-Shutdowns wird bewusst nicht persistiert.
/// </summary>
public sealed class AutoShutdownEinstellungenService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AutoShutdownEinstellungenService> _logger;

    /// <inheritdoc cref="AutoShutdownEinstellungenService"/>
    public AutoShutdownEinstellungenService(IServiceScopeFactory scopeFactory, ILogger<AutoShutdownEinstellungenService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>Lädt die gespeicherten Auto-Shutdown-Nebenwerte (Erzwingen und Toleranzzeit).</summary>
    /// <param name="ct">Abbruchtoken.</param>
    /// <returns>Erzwingen-Flag und Toleranzzeit in Minuten mit den Service-Defaults, wenn nichts gespeichert ist.</returns>
    public async Task<(bool Erzwingen, int ToleranzMinuten)> LadeAsync(CancellationToken ct = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var einstellungService = scope.ServiceProvider.GetRequiredService<AppEinstellungService>();
        var erzwingen = await einstellungService.GetAutoShutdownErzwingenAsync(ct);
        var toleranz = await einstellungService.GetAutoShutdownToleranzMinutenAsync(ct);
        return (erzwingen, toleranz);
    }

    /// <summary>Speichert den Schalter "Herunterfahren erzwingen".</summary>
    /// <param name="erzwingen">Der zu speichernde Schalter.</param>
    /// <param name="ct">Abbruchtoken.</param>
    public async Task SpeichereErzwingenAsync(bool erzwingen, CancellationToken ct = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var einstellungService = scope.ServiceProvider.GetRequiredService<AppEinstellungService>();
        await einstellungService.SetAutoShutdownErzwingenAsync(erzwingen, ct);
        _logger.LogDebug("Auto-Shutdown-Erzwingen '{Erzwingen}' gespeichert.", erzwingen);
    }

    /// <summary>Speichert die Toleranzzeit (Minuten) des Auto-Shutdown-Countdowns.</summary>
    /// <param name="toleranzMinuten">Die zu speichernde Toleranzzeit in Minuten.</param>
    /// <param name="ct">Abbruchtoken.</param>
    public async Task SpeichereToleranzMinutenAsync(int toleranzMinuten, CancellationToken ct = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var einstellungService = scope.ServiceProvider.GetRequiredService<AppEinstellungService>();
        await einstellungService.SetAutoShutdownToleranzMinutenAsync(toleranzMinuten, ct);
        _logger.LogDebug("Auto-Shutdown-Toleranz '{ToleranzMinuten}' Minuten gespeichert.", toleranzMinuten);
    }
}
