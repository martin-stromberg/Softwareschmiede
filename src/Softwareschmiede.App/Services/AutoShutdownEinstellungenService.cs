using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Softwareschmiede.Application.Services;

namespace Softwareschmiede.App.Services;

/// <summary>
/// Persistiert die Auto-Shutdown-Nebenwerte (Erzwingen-Flag und Toleranzzeit) über den scoped
/// <see cref="AppSettingService"/> in eigenen DI-Scopes (Muster <see cref="AufgabenListenModusService"/>).
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
    public async Task<(bool Erzwingen, int ToleranzMinuten)> LoadAsync(CancellationToken ct = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var settingService = scope.ServiceProvider.GetRequiredService<AppSettingService>();
        var force = await settingService.GetAutoShutdownErzwingenAsync(ct);
        var tolerance = await settingService.GetAutoShutdownToleranceMinutesAsync(ct);
        return (force, tolerance);
    }

    /// <summary>Speichert den Schalter "Herunterfahren erzwingen".</summary>
    /// <param name="force">Der zu speichernde Schalter.</param>
    /// <param name="ct">Abbruchtoken.</param>
    public async Task SpeichereErzwingenAsync(bool force, CancellationToken ct = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var settingService = scope.ServiceProvider.GetRequiredService<AppSettingService>();
        await settingService.SetAutoShutdownErzwingenAsync(force, ct);
        _logger.LogDebug("Auto-Shutdown-Erzwingen '{Erzwingen}' gespeichert.", force);
    }

    /// <summary>Speichert die Toleranzzeit (Minuten) des Auto-Shutdown-Countdowns.</summary>
    /// <param name="toleranceMinutes">Die zu speichernde Toleranzzeit in Minuten.</param>
    /// <param name="ct">Abbruchtoken.</param>
    public async Task SpeichereToleranzMinutenAsync(int toleranceMinutes, CancellationToken ct = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var settingService = scope.ServiceProvider.GetRequiredService<AppSettingService>();
        await settingService.SetAutoShutdownToleranceMinutesAsync(toleranceMinutes, ct);
        _logger.LogDebug("Auto-Shutdown-Toleranz '{ToleranzMinuten}' Minuten gespeichert.", toleranceMinutes);
    }
}
