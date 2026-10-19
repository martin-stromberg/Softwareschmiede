using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Softwareschmiede.App.ViewModels;
using Softwareschmiede.Application.Services;

namespace Softwareschmiede.App.Services;

/// <summary>
/// Persistiert den Anzeigemodus der aktiven Aufgabenliste je Ansicht (Seitenleiste, Dashboard)
/// über <see cref="AppEinstellungService"/> in eigenen DI-Scopes (Muster <see cref="DarkModeService"/>).
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
        => LadeModusAsync(AppEinstellungService.AktiveAufgabenListenModusKey, ct);

    /// <summary>Lädt den gespeicherten Anzeigemodus der Dashboard-Aufgabenliste.</summary>
    /// <param name="ct">Abbruchtoken.</param>
    /// <returns>Den gespeicherten Modus oder <see cref="AufgabenListenModus.Datum"/>, wenn kein gültiger Wert gespeichert ist.</returns>
    public Task<AufgabenListenModus> LadeDashboardModusAsync(CancellationToken ct = default)
        => LadeModusAsync(AppEinstellungService.AktiveAufgabenListenModusDashboardKey, ct);

    /// <summary>Speichert den Anzeigemodus der Seitenleisten-Aufgabenliste.</summary>
    /// <param name="modus">Der zu speichernde Modus.</param>
    /// <param name="ct">Abbruchtoken.</param>
    public Task SpeichereMenueModusAsync(AufgabenListenModus modus, CancellationToken ct = default)
        => SpeichereModusAsync(AppEinstellungService.AktiveAufgabenListenModusKey, modus, ct);

    /// <summary>Speichert den Anzeigemodus der Dashboard-Aufgabenliste.</summary>
    /// <param name="modus">Der zu speichernde Modus.</param>
    /// <param name="ct">Abbruchtoken.</param>
    public Task SpeichereDashboardModusAsync(AufgabenListenModus modus, CancellationToken ct = default)
        => SpeichereModusAsync(AppEinstellungService.AktiveAufgabenListenModusDashboardKey, modus, ct);

    private async Task<AufgabenListenModus> LadeModusAsync(string schluessel, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var einstellungService = scope.ServiceProvider.GetRequiredService<AppEinstellungService>();
        var wert = await einstellungService.GetSettingAsync(schluessel, ct);

        if (Enum.TryParse<AufgabenListenModus>(wert, ignoreCase: true, out var modus)
            && Enum.IsDefined(modus))
        {
            return modus;
        }

        return AufgabenListenModus.Datum;
    }

    private async Task SpeichereModusAsync(string schluessel, AufgabenListenModus modus, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var einstellungService = scope.ServiceProvider.GetRequiredService<AppEinstellungService>();
        await einstellungService.SetSettingAsync(schluessel, modus.ToString(), ct);
        _logger.LogDebug("Aufgabenlisten-Modus '{Modus}' unter '{Schluessel}' gespeichert.", modus, schluessel);
    }
}
