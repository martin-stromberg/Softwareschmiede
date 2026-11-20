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
public sealed class UpdateE2ETestContext : IUpdateAttemptLog
{
    /// <summary>Name des Gates für die kontrollierte Settings-Lesefehler-Freigabe.</summary>
    public const string ReadFailureGateName = "settingsReadFailure";

    private int _attemptIndex;
    private string? _currentAttempt;
    private readonly object _ordinalLock = new();
    private int _ordinalAttemptIndex;
    private int _ordinalInAttempt;

    /// <inheritdoc cref="UpdateE2ETestContext"/>
    /// <param name="configuration">Die geladene und validierte Testkonfiguration.</param>
    public UpdateE2ETestContext(UpdateE2ETestConfiguration configuration)
    {
        Configuration = configuration;
        Log = new UpdateE2ELog(configuration.LogFilePath, configuration.ScenarioId);
        Directory.CreateDirectory(configuration.EffectiveGatesDirectory);
    }

    /// <summary>Die statische Testkonfiguration.</summary>
    public UpdateE2ETestConfiguration Configuration { get; }

    /// <summary>Der JSONL-Protokollschreiber.</summary>
    public UpdateE2ELog Log { get; }

    /// <summary>Laufende Ordnungszahl des aktuellen Updateversuchs (1-basiert, 0 = kein Versuch aktiv).</summary>
    public int CurrentAttemptIndex => Volatile.Read(ref _attemptIndex);

    /// <summary>Art des aktuell laufenden Updateversuchs (<c>startup</c>, <c>pruefen</c>, <c>starten</c>) oder <c>null</c>.</summary>
    public string? CurrentAttempt
    {
        get
        {
            lock (_ordinalLock)
                return _currentAttempt;
        }
    }

    /// <summary>
    /// Liefert die laufende Ordnungszahl eines markierten Settings-Reads innerhalb des aktuellen
    /// Versuchs. Reads ohne aktiven Versuch liefern 0 und verbrauchen keine Ordnungszahl, damit
    /// z. B. Settings-UI-Reads die Grenzzählung nicht verschieben. Der Zähler lebt im Kontext
    /// (Singleton), damit er unabhängig von der Lebensdauer der Interceptor-/DbContext-Instanzen gilt.
    /// </summary>
    /// <returns>Die 1-basierte Ordnungszahl innerhalb des Versuchs, oder 0 außerhalb eines Versuchs.</returns>
    public int NextMarkedReadOrdinal()
    {
        lock (_ordinalLock)
        {
            if (_currentAttempt is null)
                return 0;

            var index = CurrentAttemptIndex;
            if (index != _ordinalAttemptIndex)
            {
                _ordinalAttemptIndex = index;
                _ordinalInAttempt = 0;
            }

            return ++_ordinalInAttempt;
        }
    }

    /// <inheritdoc/>
    public void LogWindowReady()
        => Log.Write(UpdateE2EEvents.WindowReady);

    /// <summary>
    /// Meldet den Beginn eines Updateversuchs und schreibt <see cref="UpdateE2EEvents.UpdateAttemptStarted"/>.
    /// Nachfolgende markierte Settings-Reads werden diesem Versuch zugeordnet.
    /// </summary>
    /// <param name="kind">Die Versuchsart (<c>startup</c>, <c>pruefen</c>, <c>starten</c>).</param>
    public void BeginAttempt(string kind)
    {
        int index;
        lock (_ordinalLock)
        {
            index = ++_attemptIndex;
            _currentAttempt = kind;
        }
        Log.Write(UpdateE2EEvents.UpdateAttemptStarted, new { versuch = kind, versuchIndex = index });
    }

    /// <summary>
    /// Meldet den Abschluss eines Updateversuchs nach Gate-/Busy-Freigabe und schreibt
    /// <see cref="UpdateE2EEvents.UpdateAttemptCompleted"/>; beim Startversuch zusätzlich
    /// <see cref="UpdateE2EEvents.StartupUpdateCompleted"/>.
    /// </summary>
    /// <param name="kind">Die Versuchsart.</param>
    /// <param name="updateAvailable">Ob nach dem Versuch ein Updateangebot sichtbar ist.</param>
    /// <param name="hint">Der aktuell angezeigte Update-Hinweis, falls vorhanden.</param>
    public void EndAttempt(string kind, bool updateAvailable, string? hint)
    {
        int index;
        lock (_ordinalLock)
        {
            index = _attemptIndex;
            _currentAttempt = null;
        }
        Log.Write(
            UpdateE2EEvents.UpdateAttemptCompleted,
            new { versuch = kind, versuchIndex = index, updateVerfuegbar = updateAvailable, hinweis = hint });
        if (kind == "startup")
        {
            Log.Write(
                UpdateE2EEvents.StartupUpdateCompleted,
                new { updateVerfuegbar = updateAvailable, hinweis = hint });
        }
    }

    /// <summary>
    /// Liest die Szenariodatei neu ein. Bei fehlender/unlesbarer Datei wird <c>null</c> geliefert und
    /// ein <see cref="UpdateE2EEvents.ScenarioFileError"/>-Ereignis protokolliert.
    /// </summary>
    /// <returns>Das aktuell geltende Szenario oder <c>null</c>.</returns>
    public UpdateE2EScenario? ReadScenario()
    {
        try
        {
            var json = File.ReadAllText(Configuration.ScenarioFilePath);
            return JsonSerializer.Deserialize<UpdateE2EScenario>(json) ?? new UpdateE2EScenario();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            Log.Write(UpdateE2EEvents.ScenarioFileError, new { error = ex.Message });
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
    public async Task<bool> WaitOnGateAsync(string gate, CancellationToken ct, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            var result = CheckGate(gate);
            if (result is not null)
                return result.Value;

            // ConfigureAwait(false): Die synchrone Variante blockiert denselben Ablauf über
            // GetAwaiter().GetResult() - ohne Kontextbefreiung könnte ein Aufrufer mit
            // SynchronizationContext (z. B. UI-Thread) hier deadlocks auslösen.
            await Task.Delay(100, ct).ConfigureAwait(false);
        }

        return false;
    }

    /// <summary>Synchrone Variante von <see cref="WaitOnGateAsync"/> für synchron aufgerufene Interceptoren.</summary>
    /// <param name="gate">Der Gate-Name.</param>
    /// <param name="ct">Abbruchtoken des Aufrufers.</param>
    /// <param name="timeout">Maximale Wartezeit.</param>
    /// <returns><c>true</c> bei Freigabe, <c>false</c> bei Abbruch oder Timeout.</returns>
    public bool WaitOnGate(string gate, CancellationToken ct, TimeSpan timeout)
    {
        try
        {
            return WaitOnGateAsync(gate, ct, timeout).ConfigureAwait(false).GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    /// <summary>Liefert <c>true</c> bei vorhandener Freigabe-Datei, <c>false</c> bei Abbruch-Datei, sonst <c>null</c>.</summary>
    /// <param name="gate">Der Gate-Name.</param>
    /// <returns>Das Gate-Ergebnis oder <c>null</c>, wenn noch keine Datei existiert.</returns>
    public bool? CheckGate(string gate)
    {
        var directory = Configuration.EffectiveGatesDirectory;
        if (File.Exists(Path.Combine(directory, $"{gate}.release")))
            return true;
        if (File.Exists(Path.Combine(directory, $"{gate}.cancel")))
            return false;
        return null;
    }
}
