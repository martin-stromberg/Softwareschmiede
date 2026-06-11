using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.JSInterop;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;

namespace Softwareschmiede.Components.Layout;

public partial class MainLayout : IDisposable
{
    [Inject] private IRunningAutomationStatusSource RunningAutomationStatusSource { get; set; } = null!;
    [Inject] private IAutoShutdownOrchestrator AutoShutdownOrchestrator { get; set; } = null!;
    [Inject] private KiAufgabenBenachrichtigungsHub BenachrichtigungsHub { get; set; } = null!;
    [Inject] private BenachrichtigungsEinstellungenService BenachrichtigungsEinstellungen { get; set; } = null!;
    [Inject] private BenachrichtigungsAuditService BenachrichtigungsAudit { get; set; } = null!;
    [Inject] private IBenutzerkontextService Benutzerkontext { get; set; } = null!;
    [Inject] private NavigationManager NavigationManager { get; set; } = null!;
    [Inject] private IJSRuntime JsRuntime { get; set; } = null!;
    [Inject] private ILogger<MainLayout> Logger { get; set; } = null!;

    private int _runningAutomationCount;
    private bool _autoShutdownEnabled;
    private readonly List<UiToast> _toasts = [];
    private readonly HashSet<string> _dispatchDedupe = new(StringComparer.Ordinal);
    private IDisposable? _notificationSubscription;

    protected override void OnInitialized()
    {
        _runningAutomationCount = RunningAutomationStatusSource.GetRunningCount();
        RunningAutomationStatusSource.RunningCountChanged += RunningCountChanged;
        AutoShutdownOrchestrator.SetEnabled(_autoShutdownEnabled);
        _notificationSubscription = BenachrichtigungsHub.Subscribe(HandleKiAufgabenAbschlussAsync);
        NavigationManager.LocationChanged += OnLocationChanged;
    }

    private void RunningCountChanged(int previousCount, int currentCount)
    {
        _runningAutomationCount = currentCount;
        _ = InvokeAsync(StateHasChanged);
    }

    private void AutoShutdownChanged(ChangeEventArgs changeEventArgs)
    {
        _autoShutdownEnabled = changeEventArgs.Value switch
        {
            bool checkedValue => checkedValue,
            string stringValue when bool.TryParse(stringValue, out var parsedValue) => parsedValue,
            _ => false
        };
        AutoShutdownOrchestrator.SetEnabled(_autoShutdownEnabled);
    }

    private void OnLocationChanged(object? _, LocationChangedEventArgs __)
    {
    }

    private async Task HandleKiAufgabenAbschlussAsync(KiAufgabenAbschlussEreignis ereignis)
    {
        await InvokeAsync(async () =>
        {
            var benutzerId = Benutzerkontext.GetBenutzerId();
            var einstellungen = await BenachrichtigungsEinstellungen.GetAsync(benutzerId);
            var istAufgabenseite = IstAktiveAufgabenseite(ereignis.AufgabeId);

            await VerarbeiteToastAsync(ereignis, benutzerId, einstellungen.BannerModus, istAufgabenseite);
            await VerarbeiteTonAsync(ereignis, benutzerId, einstellungen.TonModus, istAufgabenseite);

            StateHasChanged();
        });
    }

    private async Task VerarbeiteToastAsync(
        KiAufgabenAbschlussEreignis ereignis,
        string benutzerId,
        BenachrichtigungsModus modus,
        bool istAufgabenseite)
    {
        var key = $"{ereignis.EreignisId}:toast";
        if (!_dispatchDedupe.Add(key))
        {
            return;
        }

        if (!IstKanalAktiv(modus, istAufgabenseite))
        {
            await BenachrichtigungsAudit.LogAsync(
                ereignis.EreignisId,
                ereignis.AufgabeId,
                benutzerId,
                BenachrichtigungsKanal.Banner,
                modus,
                BenachrichtigungsEntscheidung.Unterdrueckt,
                BestimmeUnterdrueckungsgrund(modus, istAufgabenseite));
            return;
        }

        var statusText = ereignis.AbschlussStatus == AufgabeStatus.Beendet ? "abgeschlossen" : "mit Fehler beendet";
        AddToast("KI-Aufgabe", $"{ereignis.Aufgabentitel} wurde {statusText}.", "toast-info");

        await BenachrichtigungsAudit.LogAsync(
            ereignis.EreignisId,
            ereignis.AufgabeId,
            benutzerId,
            BenachrichtigungsKanal.Banner,
            modus,
            BenachrichtigungsEntscheidung.Gesendet,
            "ToastAngezeigt");
    }

    private async Task VerarbeiteTonAsync(
        KiAufgabenAbschlussEreignis ereignis,
        string benutzerId,
        BenachrichtigungsModus modus,
        bool istAufgabenseite)
    {
        var key = $"{ereignis.EreignisId}:ton";
        if (!_dispatchDedupe.Add(key))
        {
            return;
        }

        if (!IstKanalAktiv(modus, istAufgabenseite))
        {
            await BenachrichtigungsAudit.LogAsync(
                ereignis.EreignisId,
                ereignis.AufgabeId,
                benutzerId,
                BenachrichtigungsKanal.Ton,
                modus,
                BenachrichtigungsEntscheidung.Unterdrueckt,
                BestimmeUnterdrueckungsgrund(modus, istAufgabenseite));
            return;
        }

        try
        {
            var audio = await BenachrichtigungsEinstellungen.GetAudioPayloadAsync(benutzerId);
            var result = await JsRuntime.InvokeAsync<string>(
                "softwareschmiedeNotifications.playAlert",
                audio?.Base64Inhalt,
                audio?.MimeType);

            if (string.Equals(result, "deferred", StringComparison.OrdinalIgnoreCase))
            {
                AddToast("Hinweiston", "Audio wurde durch Browserrichtlinien verzögert und wird nach Interaktion erneut versucht.", "toast-warning");
                await BenachrichtigungsAudit.LogAsync(
                    ereignis.EreignisId,
                    ereignis.AufgabeId,
                    benutzerId,
                    BenachrichtigungsKanal.Ton,
                    modus,
                    BenachrichtigungsEntscheidung.Zurueckgestellt,
                    "AutoplayBlockiert");
                return;
            }

            if (string.Equals(result, "failed", StringComparison.OrdinalIgnoreCase))
            {
                AddToast("Hinweiston", "Ton konnte nicht abgespielt werden. Die Anwendung bleibt weiterhin nutzbar.", "toast-error");
                await BenachrichtigungsAudit.LogAsync(
                    ereignis.EreignisId,
                    ereignis.AufgabeId,
                    benutzerId,
                    BenachrichtigungsKanal.Ton,
                    modus,
                    BenachrichtigungsEntscheidung.Fehlgeschlagen,
                    "AudioPlaybackFehler");
                return;
            }

            var grund = audio is null ? "StandardtonFallback" : "BenutzerdefinierterTon";
            await BenachrichtigungsAudit.LogAsync(
                ereignis.EreignisId,
                ereignis.AufgabeId,
                benutzerId,
                BenachrichtigungsKanal.Ton,
                modus,
                BenachrichtigungsEntscheidung.Gesendet,
                grund);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Ton-Benachrichtigung fehlgeschlagen.");
            AddToast("Hinweiston", "Ton konnte nicht abgespielt werden. Die Anwendung bleibt weiterhin nutzbar.", "toast-error");
            await BenachrichtigungsAudit.LogAsync(
                ereignis.EreignisId,
                ereignis.AufgabeId,
                benutzerId,
                BenachrichtigungsKanal.Ton,
                modus,
                BenachrichtigungsEntscheidung.Fehlgeschlagen,
                "JsInteropFehler");
        }
    }

    private bool IstAktiveAufgabenseite(Guid aufgabeId)
    {
        var absoluteUri = NavigationManager.ToAbsoluteUri(NavigationManager.Uri);
        var relativePath = NavigationManager.ToBaseRelativePath(absoluteUri.AbsolutePath).TrimStart('/');
        var teile = relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (teile.Length < 2 || !string.Equals(teile[0], "aufgaben", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return Guid.TryParse(teile[1], out var aktiveAufgabeId) && aktiveAufgabeId == aufgabeId;
    }

    private static bool IstKanalAktiv(BenachrichtigungsModus modus, bool istAufgabenseite)
    {
        return modus switch
        {
            BenachrichtigungsModus.Deaktiviert => false,
            BenachrichtigungsModus.Banner => true,
            BenachrichtigungsModus.Ton => true,
            _ => false
        };
    }

    private static string BestimmeUnterdrueckungsgrund(BenachrichtigungsModus modus, bool istAufgabenseite)
    {
        if (modus == BenachrichtigungsModus.Deaktiviert)
        {
            return "KanalDeaktiviert";
        }

        return "Unterdrueckt";
    }

    private void AddToast(string titel, string nachricht, string cssClass)
    {
        var toast = new UiToast(Guid.NewGuid(), titel, nachricht, cssClass);
        _toasts.Add(toast);
        _ = RemoveToastDelayedAsync(toast.Id, TimeSpan.FromSeconds(5));
    }

    private async Task RemoveToastDelayedAsync(Guid toastId, TimeSpan delay)
    {
        await Task.Delay(delay);
        await InvokeAsync(() =>
        {
            EntferneToast(toastId);
            StateHasChanged();
        });
    }

    private void EntferneToast(Guid id)
    {
        _toasts.RemoveAll(t => t.Id == id);
    }

    public void Dispose()
    {
        RunningAutomationStatusSource.RunningCountChanged -= RunningCountChanged;
        NavigationManager.LocationChanged -= OnLocationChanged;
        _notificationSubscription?.Dispose();
    }

    private sealed record UiToast(Guid Id, string Titel, string Nachricht, string CssClass);
}
