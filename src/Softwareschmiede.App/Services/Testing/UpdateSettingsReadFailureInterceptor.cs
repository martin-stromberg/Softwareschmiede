using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Softwareschmiede.Application.Services;

namespace Softwareschmiede.App.Services.Testing;

/// <summary>
/// EF-Core-Interceptor, der ausschließlich mit dem Abfragetag <see cref="AppEinstellungService.UpdateSettingsReadTag"/>
/// markierte Lesebefehle der Update-Einstellungen gezielt mit einer kontrollierten Exception fehlschlagen lässt.
/// Alle anderen Lese- und Schreibbefehle bleiben unberührt.
/// </summary>
/// <remarks>
/// Zwei Steuerungsmodi: Ohne <see cref="UpdateE2ETestKontext"/> (Komponententests) steuern
/// <see cref="ActivateFailure"/>/<see cref="DeactivateFailure"/> den Fehler global. Mit Kontext
/// (E2E-App-Prozess) wird die Szenariodatei pro markiertem Read neu gelesen; der Fehler greift nur
/// auf Reads innerhalb eines laufenden Updateversuchs ab der konfigurierten Lesegrenze
/// (<c>Initial</c>, <c>BeforePreparation</c>, <c>BeforeUpdaterStart</c>) und bleibt aktiv, bis das
/// Szenario ihn ausdrücklich deaktiviert. Optional wartet der betroffene Read begrenzt auf die
/// Freigabe-Datei <c>settingsReadFailure.release</c> bzw. den Abbruch über <c>settingsReadFailure.cancel</c>.
/// </remarks>
public sealed class UpdateSettingsReadFailureInterceptor : DbCommandInterceptor
{
    private volatile bool _failureEnabled;
    private int _settingsReadCount;
    private readonly UpdateE2ETestKontext? _kontext;

    /// <inheritdoc cref="UpdateSettingsReadFailureInterceptor"/>
    public UpdateSettingsReadFailureInterceptor()
    {
    }

    /// <inheritdoc cref="UpdateSettingsReadFailureInterceptor"/>
    /// <param name="kontext">Der Laufzeitkontext der Update-E2E-Umgebung.</param>
    public UpdateSettingsReadFailureInterceptor(UpdateE2ETestKontext kontext)
    {
        _kontext = kontext;
    }

    /// <summary>Wird ausgelöst, sobald ein markierter Update-Settings-Lesebefehl erreicht wird (vor einem etwaigen Fehler).</summary>
    public event EventHandler? SettingsReadReached;

    /// <summary>Anzahl der bisher beobachteten markierten Update-Settings-Lesebefehle.</summary>
    public int SettingsReadCount => _settingsReadCount;

    /// <summary>Gibt an, ob markierte Update-Settings-Lesebefehle aktuell kontrolliert fehlschlagen.</summary>
    public bool FailureEnabled => _failureEnabled;

    /// <summary>Die kontrollierte Exception, die bei aktiviertem Fehler für markierte Lesebefehle geworfen wird.</summary>
    public Exception FailureException { get; init; } = new InvalidOperationException("Kontrollierter Testfehler beim Lesen der Update-Einstellungen.");

    /// <summary>Aktiviert den kontrollierten Lesefehler für markierte Update-Settings-Abfragen.</summary>
    public void ActivateFailure() => _failureEnabled = true;

    /// <summary>Deaktiviert den kontrollierten Lesefehler wieder; markierte Abfragen laufen danach normal.</summary>
    public void DeactivateFailure() => _failureEnabled = false;

    /// <inheritdoc/>
    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result)
    {
        if (PruefeMarkiertenRead(command) is { } fehlerfall)
            WarteUndWirf(fehlerfall, CancellationToken.None);

        return result;
    }

    /// <inheritdoc/>
    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        // Die Gate-Wartezeit muss asynchron ablaufen: Markierte Reads laufen im Update-Flow auf dem
        // UI-Thread, ein synchrones Blockieren würde die Oberfläche (und damit FlaUI-Zugriffe wie
        // GetMainWindow) für die gesamte Gate-Dauer einfrieren.
        if (PruefeMarkiertenRead(command) is { } fehlerfall)
            await WarteUndWirfAsync(fehlerfall, cancellationToken);

        return result;
    }

    /// <summary>Ein erkannter, anzuwendender Lesefehler-Fall inklusive des Read-Zeitpunkts im Versuch.</summary>
    private sealed record Fehlerfall(UpdateE2ELesefehler Lesefehler, string? Versuch, int VersuchIndex, int Ordinal);

    /// <summary>
    /// Prüft einen Lesebefehl gegen den Abfragetag und die Szenariokonfiguration. Liefert den
    /// anzuwendenden Fehlerfall oder <c>null</c>, wenn der Read ungehindert laufen darf.
    /// Protokolliert jeden markierten Read als <see cref="UpdateE2EEreignisse.UpdateSettingsReadReached"/>.
    /// </summary>
    private Fehlerfall? PruefeMarkiertenRead(DbCommand command)
    {
        if (!command.CommandText.Contains(AppEinstellungService.UpdateSettingsReadTag, StringComparison.Ordinal))
            return null;

        Interlocked.Increment(ref _settingsReadCount);
        SettingsReadReached?.Invoke(this, EventArgs.Empty);

        if (_kontext is null)
        {
            if (_failureEnabled)
                throw FailureException;
            return null;
        }

        // Ordinalzählung und Versuchszuordnung liegen im Singleton-Kontext, damit sie
        // unabhängig davon gelten, ob derselbe Interceptor oder eine neue Instanz je
        // Scoped-DbContext läuft - instanzbezogene Zähler könnten Reads verscopen.
        var ordinal = _kontext.NaechsterMarkierterReadOrdinal();
        var versuch = _kontext.AktuellerVersuch;
        var versuchIndex = _kontext.AktuellerVersuchIndex;
        var lesefehler = _kontext.LeseSzenario()?.Lesefehler;

        _kontext.Protokoll.Schreibe(
            UpdateE2EEreignisse.UpdateSettingsReadReached,
            new
            {
                versuch,
                versuchIndex,
                ordinal,
                grenze = lesefehler?.Grenze,
                fehlerAktiv = lesefehler?.Aktiv == true || _failureEnabled
            });

        if (_failureEnabled)
            throw FailureException;

        if (lesefehler?.Aktiv != true
            || versuch is null
            || !lesefehler.TrifftVersuch(versuch)
            || ordinal < lesefehler.GrenzOrdinal())
        {
            return null;
        }

        return new Fehlerfall(lesefehler, versuch, versuchIndex, ordinal);
    }

    private void WarteUndWirf(Fehlerfall fehlerfall, CancellationToken cancellationToken)
    {
        var grund = "sofort";
        if (fehlerfall.Lesefehler.AufFreigabeWarten)
        {
            var freigegeben = _kontext!.WarteAufGate(
                UpdateE2ETestKontext.LesefehlerGateName, cancellationToken, WarteTimeout(fehlerfall));
            grund = ErmittleWarteGrund(freigegeben);
        }

        ProtokolliereFehlerUndWirf(fehlerfall, grund);
    }

    private async Task WarteUndWirfAsync(Fehlerfall fehlerfall, CancellationToken cancellationToken)
    {
        var grund = "sofort";
        if (fehlerfall.Lesefehler.AufFreigabeWarten)
        {
            bool freigegeben;
            try
            {
                freigegeben = await _kontext!.WarteAufGateAsync(
                    UpdateE2ETestKontext.LesefehlerGateName, cancellationToken, WarteTimeout(fehlerfall));
            }
            catch (OperationCanceledException)
            {
                freigegeben = false;
            }
            grund = ErmittleWarteGrund(freigegeben);
        }

        ProtokolliereFehlerUndWirf(fehlerfall, grund);
    }

    /// <summary>Die konfigurierte Wartezeit des Lesefehlers, mindestens eine Sekunde.</summary>
    private static TimeSpan WarteTimeout(Fehlerfall fehlerfall)
        => TimeSpan.FromSeconds(Math.Max(1, fehlerfall.Lesefehler.WartezeitSekunden));

    /// <summary>
    /// WarteAufGate liefert bei Abbruch und Timeout beide <c>false</c> - die Abbruch-Datei
    /// entscheidet, ob der Aufrufer das Gate explizit abgebrochen hat.
    /// </summary>
    private string ErmittleWarteGrund(bool freigegeben)
        => freigegeben
            ? "freigegeben"
            : _kontext!.PruefeGate(UpdateE2ETestKontext.LesefehlerGateName) == false
                ? "abgebrochen"
                : "timeout";

    private void ProtokolliereFehlerUndWirf(Fehlerfall fehlerfall, string grund)
    {
        _kontext!.Protokoll.Schreibe(
            UpdateE2EEreignisse.UpdateSettingsReadFailed,
            new
            {
                versuch = fehlerfall.Versuch,
                versuchIndex = fehlerfall.VersuchIndex,
                ordinal = fehlerfall.Ordinal,
                grenze = fehlerfall.Lesefehler.Grenze,
                grund
            });

        throw FailureException;
    }
}
