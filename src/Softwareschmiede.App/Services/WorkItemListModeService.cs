using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Softwareschmiede.App.ViewModels;
using Softwareschmiede.Application.Services;

namespace Softwareschmiede.App.Services;

/// <summary>
/// Persistiert den Anzeigemodus der aktiven Aufgabenliste je Ansicht (Seitenleiste, Dashboard)
/// über <see cref="AppSettingService"/> in eigenen DI-Scopes (Muster <see cref="DarkModeService"/>).
/// </summary>
public sealed class WorkItemListModeService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WorkItemListModeService> _logger;

    /// <inheritdoc cref="WorkItemListModeService"/>
    public WorkItemListModeService(IServiceScopeFactory scopeFactory, ILogger<WorkItemListModeService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>Lädt den gespeicherten Anzeigemodus der Seitenleisten-Aufgabenliste.</summary>
    /// <param name="ct">Abbruchtoken.</param>
    /// <returns>Den gespeicherten Modus oder <see cref="WorkItemListMode.Date"/>, wenn kein gültiger Wert gespeichert ist.</returns>
    public Task<WorkItemListMode> LoadMenuModeAsync(CancellationToken ct = default)
        => LoadModeAsync(AppSettingService.ActiveWorkItemsListModeKey, ct);

    /// <summary>Lädt den gespeicherten Anzeigemodus der Dashboard-Aufgabenliste.</summary>
    /// <param name="ct">Abbruchtoken.</param>
    /// <returns>Den gespeicherten Modus oder <see cref="WorkItemListMode.Date"/>, wenn kein gültiger Wert gespeichert ist.</returns>
    public Task<WorkItemListMode> LoadDashboardModeAsync(CancellationToken ct = default)
        => LoadModeAsync(AppSettingService.ActiveWorkItemsListModeDashboardKey, ct);

    /// <summary>Speichert den Anzeigemodus der Seitenleisten-Aufgabenliste.</summary>
    /// <param name="mode">Der zu speichernde Modus.</param>
    /// <param name="ct">Abbruchtoken.</param>
    public Task SaveMenuModeAsync(WorkItemListMode mode, CancellationToken ct = default)
        => SaveModeAsync(AppSettingService.ActiveWorkItemsListModeKey, mode, ct);

    /// <summary>Speichert den Anzeigemodus der Dashboard-Aufgabenliste.</summary>
    /// <param name="mode">Der zu speichernde Modus.</param>
    /// <param name="ct">Abbruchtoken.</param>
    public Task SaveDashboardModeAsync(WorkItemListMode mode, CancellationToken ct = default)
        => SaveModeAsync(AppSettingService.ActiveWorkItemsListModeDashboardKey, mode, ct);

    private async Task<WorkItemListMode> LoadModeAsync(string key, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var settingService = scope.ServiceProvider.GetRequiredService<AppSettingService>();
        var value = await settingService.GetSettingAsync(key, ct);

        if (Enum.TryParse<WorkItemListMode>(value, ignoreCase: true, out var mode)
            && Enum.IsDefined(mode))
        {
            return mode;
        }

        // Gespeicherte Altbestände (deutsche Membernamen vor der Codesprachen-Umstellung).
        return value switch
        {
            "Ki" => WorkItemListMode.Ai,
            "Datum" => WorkItemListMode.Date,
            _ => WorkItemListMode.Date
        };
    }

    private async Task SaveModeAsync(string key, WorkItemListMode mode, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var settingService = scope.ServiceProvider.GetRequiredService<AppSettingService>();
        await settingService.SetSettingAsync(key, ToPersistedString(mode), ct);
        _logger.LogDebug("Aufgabenlisten-Modus '{Modus}' unter '{Schluessel}' gespeichert.", mode, key);
    }

    // Persistierte Werte bleiben die bisherigen deutschen Membernamen, damit Altbestände
    // und ältere Programmversionen dieselbe Einstellung weiter lesen können.
    private static string ToPersistedString(WorkItemListMode mode)
        => mode switch
        {
            WorkItemListMode.Ai => "Ki",
            WorkItemListMode.Date => "Datum",
            _ => mode.ToString()
        };
}
