using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;

namespace Softwareschmiede.App.Services.Testing;

/// <summary>Ereignisnamen des strukturierten Update-E2E-Protokolls (JSONL).</summary>
public static class UpdateE2EEvents
{
    /// <summary>Die allgemeine Datenbankinitialisierung (Migration) ist abgeschlossen.</summary>
    public const string DatabaseInitializationCompleted = "DatabaseInitializationCompleted";

    /// <summary>Das Hauptfenster wurde gerendert und ist bereit.</summary>
    public const string WindowReady = "WindowReady";

    /// <summary>Der einmalige Start-Updateversuch ist abgeschlossen (nach Gate-/Busy-Freigabe).</summary>
    public const string StartupUpdateCompleted = "StartupUpdateCompleted";

    /// <summary>Ein Updateversuch wurde begonnen (Daten: versuch, versuchIndex).</summary>
    public const string UpdateAttemptStarted = "UpdateAttemptStarted";

    /// <summary>Ein Updateversuch ist abgeschlossen (nach Gate-/Busy-Freigabe).</summary>
    public const string UpdateAttemptCompleted = "UpdateAttemptCompleted";

    /// <summary>Eine Update-Prüfung wurde gestartet.</summary>
    public const string UpdateCheckStarted = "UpdateCheckStarted";

    /// <summary>Eine Update-Prüfung wurde abgeschlossen (Daten: status, version, isPrerelease).</summary>
    public const string UpdateCheckCompleted = "UpdateCheckCompleted";

    /// <summary>HTTP-Anfrage der Update-Pipeline (Daten: url, method, art).</summary>
    public const string HttpRequest = "HttpRequest";

    /// <summary>HTTP-Anfrage wartet auf ein Gate (Daten: url, gate).</summary>
    public const string HttpRequestBlocked = "HttpRequestBlocked";

    /// <summary>Blockierte HTTP-Anfrage wurde freigegeben (Daten: url, gate).</summary>
    public const string HttpRequestReleased = "HttpRequestReleased";

    /// <summary>HTTP-Anfrage ohne konfigurierte Antwort — scheitert ohne Netzwerkfallback (Daten: url).</summary>
    public const string UnknownHttpRequest = "UnknownHttpRequest";

    /// <summary>HTTP-Antwort wurde geliefert (Daten: url, status).</summary>
    public const string HttpResponse = "HttpResponse";

    /// <summary>Eine Vorbereitungsphase wurde gemeldet (Daten: phase, percent, message).</summary>
    public const string PreparationPhase = "PreparationPhase";

    /// <summary>Die Update-Vorbereitung wurde abgeschlossen (Daten: scriptPath, extractedDirectory).</summary>
    public const string PreparationCompleted = "PreparationCompleted";

    /// <summary>Die Update-Vorbereitung ist fehlgeschlagen (Daten: error).</summary>
    public const string PreparationFailed = "PreparationFailed";

    /// <summary>Der Updater-Start wurde versucht (Daten: scriptPath).</summary>
    public const string UpdateStartAttempt = "UpdateStartAttempt";

    /// <summary>Der Updater-Start wurde erfolgreich an den Launcher übergeben.</summary>
    public const string UpdateStartSucceeded = "UpdateStartSucceeded";

    /// <summary>Der Updater-Start ist fehlgeschlagen (Daten: error).</summary>
    public const string UpdateStartFailed = "UpdateStartFailed";

    /// <summary>Der Prozess-Launcher hat einen Start aufgezeichnet (Daten: fileName, arguments, workingDirectory, runElevated, result).</summary>
    public const string UpdateProcessStartRecorded = "UpdateProcessStartRecorded";

    /// <summary>Der Shutdown-Service wurde aufgerufen; die Test-App bleibt offen.</summary>
    public const string ShutdownRequested = "ShutdownRequested";

    /// <summary>Die CLI-Sicherheitsprüfung wurde ausgeführt (Daten: riskyTaskCount).</summary>
    public const string CliSafetyChecked = "CliSafetyChecked";

    /// <summary>Ein markierter Update--Einstellungen-Read wurde erreicht (Daten: versuch, ordinal, grenze).</summary>
    public const string UpdateSettingsReadReached = "UpdateSettingsReadReached";

    /// <summary>Ein markierter Update--Einstellungen-Read ist kontrolliert fehlgeschlagen (Daten: versuch, ordinal, grund).</summary>
    public const string UpdateSettingsReadFailed = "UpdateSettingsReadFailed";

    /// <summary>Der Runner hat den Lesefehler-Auslöser deaktiviert.</summary>
    public const string UpdateSettingsReadFailureDisabled = "UpdateSettingsReadFailureDisabled";

    /// <summary>Die Szenariodatei konnte nicht gelesen/geparst werden (Daten: error).</summary>
    public const string ScenarioFileError = "ScenarioFileError";
}

/// <summary>
/// Strukturierter JSONL-Protokollschreiber der Update-E2E-Umgebung. Jede Zeile enthält eine pro
/// Quelle monotone Sequenznummer, die Szenario-ID und die Prozess-ID. Der Test-Runner kann über
/// <see cref="Anfuegen"/> eigene Steuerereignisse (z. B. <see cref="UpdateE2EEvents.UpdateSettingsReadFailureDisabled"/>)
/// in dieselbe Datei schreiben.
/// </summary>
public sealed class UpdateE2ELog
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly object _lock = new();
    private readonly string _filePath;
    private readonly string _scenarioId;
    private readonly string _source;
    private long _seq;

    /// <inheritdoc cref="UpdateE2ELog"/>
    /// <param name="filePath">Pfad der JSONL-Protokolldatei.</param>
    /// <param name="scenarioId">Szenario-ID für jeden Eintrag.</param>
    /// <param name="source">Quellkennung der Einträge (z. B. <c>app</c> oder <c>runner</c>).</param>
    public UpdateE2ELog(string filePath, string scenarioId, string source = "app")
    {
        _filePath = filePath;
        _scenarioId = scenarioId;
        _source = source;
        _seq = CountExistingEntries(filePath, source);
    }

    /// <summary>Schreibt ein Protokollereignis als JSONL-Zeile.</summary>
    /// <param name="evt">Der Ereignisname (siehe <see cref="UpdateE2EEvents"/>).</param>
    /// <param name="data">Optionale strukturierte Nutzdaten.</param>
    public void Write(string evt, object? data = null)
    {
        lock (_lock)
        {
            var entry = new UpdateE2ELogEntry
            {
                Seq = ++_seq,
                ProcessId = Environment.ProcessId,
                ScenarioId = _scenarioId,
                Event = evt,
                Data = data,
                Utc = DateTimeOffset.UtcNow,
                Source = _source
            };

            AppendAllTextWithRetry(
                _filePath,
                JsonSerializer.Serialize(entry, SerializerOptions) + Environment.NewLine);
        }
    }

    /// <summary>
    /// Hängt ein Ereignis fremder Quelle (z. B. des Test-Runners) an die Protokolldatei an.
    /// Die Sequenznummer wird aus der letzten Zeile derselben Quelle fortgesetzt.
    /// </summary>
    /// <param name="filePath">Pfad der JSONL-Protokolldatei.</param>
    /// <param name="scenarioId">Szenario-ID des Eintrags.</param>
    /// <param name="evt">Der Ereignisname.</param>
    /// <param name="data">Optionale strukturierte Nutzdaten.</param>
    /// <param name="source">Quellkennung des Eintrags.</param>
    public static void Anfuegen(string filePath, string scenarioId, string evt, object? data = null, string source = "runner")
    {
        var seq = CountExistingEntries(filePath, source) + 1;
        var entry = new UpdateE2ELogEntry
        {
            Seq = seq,
            ProcessId = Environment.ProcessId,
            ScenarioId = scenarioId,
            Event = evt,
            Data = data,
            Utc = DateTimeOffset.UtcNow,
            Source = source
        };

        AppendAllTextWithRetry(
            filePath,
            JsonSerializer.Serialize(entry, SerializerOptions) + Environment.NewLine);
    }

    // App- und Runner-Prozess schreiben in dieselbe Datei; ein kurzer Retry fängt die
    // übliche Sharing-Verletzung bei zeitgleichem Append ab.
    private static void AppendAllTextWithRetry(string filePath, string line)
    {
        const int attempt = 5;
        for (var i = 1; ; i++)
        {
            try
            {
                File.AppendAllText(filePath, line);
                return;
            }
            catch (IOException) when (i < attempt)
            {
                Thread.Sleep(50 * i);
            }
        }
    }

    private static long CountExistingEntries(string filePath, string source)
    {
        if (!File.Exists(filePath))
            return 0;

        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);
            var lastSeq = 0L;
            while (reader.ReadLine() is { } line)
            {
                if (line.Contains($"\"quelle\":\"{source}\"", StringComparison.Ordinal)
                    && line.Contains("\"seq\":", StringComparison.Ordinal))
                {
                    var seqStart = line.IndexOf("\"seq\":", StringComparison.Ordinal) + 6;
                    var seqEnd = seqStart;
                    while (seqEnd < line.Length && char.IsDigit(line[seqEnd]))
                        seqEnd++;
                    if (long.TryParse(line[seqStart..seqEnd], out var seq))
                        lastSeq = Math.Max(lastSeq, seq);
                }
            }

            return lastSeq;
        }
        catch (IOException)
        {
            return 0;
        }
    }
}

/// <summary>Ein strukturierter Eintrag des Update-E2E-JSONL-Protokolls.</summary>
public sealed class UpdateE2ELogEntry
{
    /// <summary>Monotone Sequenznummer innerhalb der Quelle.</summary>
    [JsonPropertyName("seq")]
    public long Seq { get; set; }

    /// <summary>ID des schreibenden Prozesses.</summary>
    [JsonPropertyName("pid")]
    public int ProcessId { get; set; }

    /// <summary>Szenario-ID aus der Testkonfiguration.</summary>
    [JsonPropertyName("scenario")]
    public string ScenarioId { get; set; } = string.Empty;

    /// <summary>Ereignisname (siehe <see cref="UpdateE2EEvents"/>).</summary>
    [JsonPropertyName("evt")]
    public string Event { get; set; } = string.Empty;

    /// <summary>Optionale strukturierte Nutzdaten.</summary>
    [JsonPropertyName("data")]
    public object? Data { get; set; }

    /// <summary>UTC-Zeitpunkt des Ereignisses.</summary>
    [JsonPropertyName("utc")]
    public DateTimeOffset Utc { get; set; }

    /// <summary>Quellkennung (<c>app</c> oder <c>runner</c>).</summary>
    [JsonPropertyName("quelle")]
    public string Source { get; set; } = "app";
}
