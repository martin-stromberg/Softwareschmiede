using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Softwareschmiede.App.ViewModels;
using Softwareschmiede.Application.Services;

namespace Softwareschmiede.App.Services;

/// <summary>
/// Persistiert den Anzeigemodus der aktiven Aufgabenliste je Ansicht (Seitenleiste, Dashboard)
/// über <see cref="AppSettingService"/> in eigenen DI-Scopes (Muster <see cref="DarkModeService"/>).
/// </summary>
public sealed class AufgabenListenModusService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AufgabenListenModusService> _logger;

    /// <inheritdoc cref="AufgabenListenModusService"/>
    public AufgabenListenModusService(IServiceScopeFactory scopeFactory, ILogger<AufgabenListenModusService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>Lädt den gespeicherten Anzeigemodus der Seitenleisten-Aufgabenliste.</summary>
    /// <param name="ct">Abbruchtoken.</param>
    /// <returns>Den gespeicherten Modus oder <see cref="AufgabenListenModus.Datum"/>, wenn kein gültiger Wert gespeichert ist.</returns>
    public Task<AufgabenListenModus> LadeMenueModusAsync(CancellationToken ct = default)
        => LadeModusAsync(AppSettingService.ActiveWorkItemsListModeKey, ct);

    /// <summary>Lädt den gespeicherten Anzeigemodus der Dashboard-Aufgabenliste.</summary>
    /// <param name="ct">Abbruchtoken.</param>
    /// <returns>Den gespeicherten Modus oder <see cref="AufgabenListenModus.Datum"/>, wenn kein gültiger Wert gespeichert ist.</returns>
    public Task<AufgabenListenModus> LadeDashboardModusAsync(CancellationToken ct = default)
        => LadeModusAsync(AppSettingService.ActiveWorkItemsListModeDashboardKey, ct);

    /// <summary>Speichert den Anzeigemodus der Seitenleisten-Aufgabenliste.</summary>
    /// <param name="mode">Der zu speichernde Modus.</param>
    /// <param name="ct">Abbruchtoken.</param>
    public Task SpeichereMenueModusAsync(AufgabenListenModus mode, CancellationToken ct = default)
        => SpeichereModusAsync(AppSettingService.ActiveWorkItemsListModeKey, mode, ct);

    /// <summary>Speichert den Anzeigemodus der Dashboard-Aufgabenliste.</summary>
    /// <param name="mode">Der zu speichernde Modus.</param>
    /// <param name="ct">Abbruchtoken.</param>
    public Task SpeichereDashboardModusAsync(AufgabenListenModus mode, CancellationToken ct = default)
        => SpeichereModusAsync(AppSettingService.ActiveWorkItemsListModeDashboardKey, mode, ct);

    private async Task<AufgabenListenModus> LadeModusAsync(string key, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var settingService = scope.ServiceProvider.GetRequiredService<AppSettingService>();
        var value = await settingService.GetSettingAsync(key, ct);

        if (Enum.TryParse<AufgabenListenModus>(value, ignoreCase: true, out var mode)
            && Enum.IsDefined(mode))
        {
            return mode;
        }

        return AufgabenListenModus.Datum;
    }

    private async Task SpeichereModusAsync(string key, AufgabenListenModus mode, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var settingService = scope.ServiceProvider.GetRequiredService<AppSettingService>();
        await settingService.SetSettingAsync(key, mode.ToString(), ct);
        _logger.LogDebug("Aufgabenlisten-Modus '{Modus}' unter '{Schluessel}' gespeichert.", mode, key);
    }
}
