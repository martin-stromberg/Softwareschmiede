using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Softwareschmiede.App.Services.Testing;

/// <summary>
/// Laufzeitkontext der Update-E2E-Umgebung im App-Prozess. Bündelt die statische Testkonfiguration,
/// das JSONL-Protokoll, das pro Zugriff neu gelesene Szenario, dateibasierte Freigabe-Gates und die
/// Zählung der Updateversuche, damit markierte Settings-Reads der richtigen Lesegrenze zugeordnet
/// werden können.
/// </summary>
public sealed class UpdateE2ETestKontext : IUpdateVersuchProtokoll
{
    /// <summary>Name des Gates für die kontrollierte Settings-Lesefehler-Freigabe.</summary>
    public const string LesefehlerGateName = "settingsReadFailure";

    private int _versuchIndex;
    private string? _aktuellerVersuch;
    private readonly object _ordinalLock = new();
    private int _ordinalVersuchIndex;
    private int _ordinalImVersuch;

    /// <inheritdoc cref="UpdateE2ETestKontext"/>
    /// <param name="konfiguration">Die geladene und validierte Testkonfiguration.</param>
    public UpdateE2ETestKontext(UpdateE2ETestConfiguration konfiguration)
    {
        Konfiguration = konfiguration;
        Protokoll = new UpdateE2EProtokoll(konfiguration.ProtokollDateiPfad, konfiguration.SzenarioId);
        Directory.CreateDirectory(konfiguration.EffektivesGatesVerzeichnis);
    }

    /// <summary>Die statische Testkonfiguration.</summary>
    public UpdateE2ETestConfiguration Konfiguration { get; }

    /// <summary>Der JSONL-Protokollschreiber.</summary>
    public UpdateE2EProtokoll Protokoll { get; }

    /// <summary>Laufende Ordnungszahl des aktuellen Updateversuchs (1-basiert, 0 = kein Versuch aktiv).</summary>
    public int AktuellerVersuchIndex => Volatile.Read(ref _versuchIndex);

    /// <summary>Art des aktuell laufenden Updateversuchs (<c>startup</c>, <c>pruefen</c>, <c>starten</c>) oder <c>null</c>.</summary>
    public string? AktuellerVersuch
    {
        get
        {
            lock (_ordinalLock)
                return _aktuellerVersuch;
        }
    }

    /// <summary>
    /// Liefert die laufende Ordnungszahl eines markierten Settings-Reads innerhalb des aktuellen
    /// Versuchs. Reads ohne aktiven Versuch liefern 0 und verbrauchen keine Ordnungszahl, damit
    /// z. B. Settings-UI-Reads die Grenzzählung nicht verschieben. Der Zähler lebt im Kontext
    /// (Singleton), damit er unabhängig von der Lebensdauer der Interceptor-/DbContext-Instanzen gilt.
    /// </summary>
    /// <returns>Die 1-basierte Ordnungszahl innerhalb des Versuchs, oder 0 außerhalb eines Versuchs.</returns>
    public int NaechsterMarkierterReadOrdinal()
    {
        lock (_ordinalLock)
        {
            if (_aktuellerVersuch is null)
                return 0;

            var index = AktuellerVersuchIndex;
            if (index != _ordinalVersuchIndex)
            {
                _ordinalVersuchIndex = index;
                _ordinalImVersuch = 0;
            }

            return ++_ordinalImVersuch;
        }
    }

    /// <inheritdoc/>
    public void ProtokolliereWindowReady()
        => Protokoll.Schreibe(UpdateE2EEreignisse.WindowReady);

    /// <summary>
    /// Meldet den Beginn eines Updateversuchs und schreibt <see cref="UpdateE2EEreignisse.UpdateAttemptStarted"/>.
    /// Nachfolgende markierte Settings-Reads werden diesem Versuch zugeordnet.
    /// </summary>
    /// <param name="art">Die Versuchsart (<c>startup</c>, <c>pruefen</c>, <c>starten</c>).</param>
    public void BeginneVersuch(string art)
    {
        int index;
        lock (_ordinalLock)
        {
            index = ++_versuchIndex;
            _aktuellerVersuch = art;
        }
        Protokoll.Schreibe(UpdateE2EEreignisse.UpdateAttemptStarted, new { versuch = art, versuchIndex = index });
    }

    /// <summary>
    /// Meldet den Abschluss eines Updateversuchs nach Gate-/Busy-Freigabe und schreibt
    /// <see cref="UpdateE2EEreignisse.UpdateAttemptCompleted"/>; beim Startversuch zusätzlich
    /// <see cref="UpdateE2EEreignisse.StartupUpdateCompleted"/>.
    /// </summary>
    /// <param name="art">Die Versuchsart.</param>
    /// <param name="updateVerfuegbar">Ob nach dem Versuch ein Updateangebot sichtbar ist.</param>
    /// <param name="hinweis">Der aktuell angezeigte Update-Hinweis, falls vorhanden.</param>
    public void BeendeVersuch(string art, bool updateVerfuegbar, string? hinweis)
    {
        int index;
        lock (_ordinalLock)
        {
            index = _versuchIndex;
            _aktuellerVersuch = null;
        }
        Protokoll.Schreibe(
            UpdateE2EEreignisse.UpdateAttemptCompleted,
            new { versuch = art, versuchIndex = index, updateVerfuegbar, hinweis });
        if (art == "startup")
        {
            Protokoll.Schreibe(
                UpdateE2EEreignisse.StartupUpdateCompleted,
                new { updateVerfuegbar, hinweis });
        }
    }

    /// <summary>
    /// Liest die Szenariodatei neu ein. Bei fehlender/unlesbarer Datei wird <c>null</c> geliefert und
    /// ein <see cref="UpdateE2EEreignisse.ScenarioFileError"/>-Ereignis protokolliert.
    /// </summary>
    /// <returns>Das aktuell geltende Szenario oder <c>null</c>.</returns>
    public UpdateE2ESzenario? LeseSzenario()
    {
        try
        {
            var json = File.ReadAllText(Konfiguration.SzenarioDateiPfad);
            return JsonSerializer.Deserialize<UpdateE2ESzenario>(json) ?? new UpdateE2ESzenario();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            Protokoll.Schreibe(UpdateE2EEreignisse.ScenarioFileError, new { error = ex.Message });
            return null;
        }
    }

    /// <summary>
    /// Wartet asynchron auf die Freigabe- oder Abbruch-Datei eines Gates.
    /// </summary>
    /// <param name="gate">Der Gate-Name.</param>
    /// <param name="ct">Abbruchtoken des Aufrufers.</param>
    /// <param name="timeout">Maximale Wartezeit.</param>
    /// <returns><c>true</c> bei Freigabe (<c>{gate}.release</c>), <c>false</c> bei Abbruch (<c>{gate}.cancel</c>) oder Timeout.</returns>
    public async Task<bool> WarteAufGateAsync(string gate, CancellationToken ct, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            var ergebnis = PruefeGate(gate);
            if (ergebnis is not null)
                return ergebnis.Value;

            // ConfigureAwait(false): Die synchrone Variante blockiert denselben Ablauf über
            // GetAwaiter().GetResult() - ohne Kontextbefreiung könnte ein Aufrufer mit
            // SynchronizationContext (z. B. UI-Thread) hier deadlocks auslösen.
            await Task.Delay(100, ct).ConfigureAwait(false);
        }

        return false;
    }

    /// <summary>Synchrone Variante von <see cref="WarteAufGateAsync"/> für synchron aufgerufene Interceptoren.</summary>
    /// <param name="gate">Der Gate-Name.</param>
    /// <param name="ct">Abbruchtoken des Aufrufers.</param>
    /// <param name="timeout">Maximale Wartezeit.</param>
    /// <returns><c>true</c> bei Freigabe, <c>false</c> bei Abbruch oder Timeout.</returns>
    public bool WarteAufGate(string gate, CancellationToken ct, TimeSpan timeout)
    {
        try
        {
            return WarteAufGateAsync(gate, ct, timeout).ConfigureAwait(false).GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    /// <summary>Liefert <c>true</c> bei vorhandener Freigabe-Datei, <c>false</c> bei Abbruch-Datei, sonst <c>null</c>.</summary>
    /// <param name="gate">Der Gate-Name.</param>
    /// <returns>Das Gate-Ergebnis oder <c>null</c>, wenn noch keine Datei existiert.</returns>
    public bool? PruefeGate(string gate)
    {
        var verzeichnis = Konfiguration.EffektivesGatesVerzeichnis;
        if (File.Exists(Path.Combine(verzeichnis, $"{gate}.release")))
            return true;
        if (File.Exists(Path.Combine(verzeichnis, $"{gate}.cancel")))
            return false;
        return null;
    }
}
