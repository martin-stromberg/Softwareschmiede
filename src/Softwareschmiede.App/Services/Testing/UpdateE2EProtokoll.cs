using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Softwareschmiede.App.Services.Testing;

/// <summary>Ereignisnamen des strukturierten Update-E2E-Protokolls (JSONL).</summary>
public static class UpdateE2EEreignisse
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

    /// <summary>Ein markierter Update-Settings-Read wurde erreicht (Daten: versuch, ordinal, grenze).</summary>
    public const string UpdateSettingsReadReached = "UpdateSettingsReadReached";

    /// <summary>Ein markierter Update-Settings-Read ist kontrolliert fehlgeschlagen (Daten: versuch, ordinal, grund).</summary>
    public const string UpdateSettingsReadFailed = "UpdateSettingsReadFailed";

    /// <summary>Der Runner hat den Lesefehler-Auslöser deaktiviert.</summary>
    public const string UpdateSettingsReadFailureDisabled = "UpdateSettingsReadFailureDisabled";

    /// <summary>Die Szenariodatei konnte nicht gelesen/geparst werden (Daten: error).</summary>
    public const string ScenarioFileError = "ScenarioFileError";
}

/// <summary>
/// Strukturierter JSONL-Protokollschreiber der Update-E2E-Umgebung. Jede Zeile enthält eine pro
/// Quelle monotone Sequenznummer, die Szenario-ID und die Prozess-ID. Der Test-Runner kann über
/// <see cref="Anfuegen"/> eigene Steuerereignisse (z. B. <see cref="UpdateE2EEreignisse.UpdateSettingsReadFailureDisabled"/>)
/// in dieselbe Datei schreiben.
/// </summary>
public sealed class UpdateE2EProtokoll
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly object _lock = new();
    private readonly string _dateiPfad;
    private readonly string _szenarioId;
    private readonly string _quelle;
    private long _seq;

    /// <inheritdoc cref="UpdateE2EProtokoll"/>
    /// <param name="dateiPfad">Pfad der JSONL-Protokolldatei.</param>
    /// <param name="szenarioId">Szenario-ID für jeden Eintrag.</param>
    /// <param name="quelle">Quellkennung der Einträge (z. B. <c>app</c> oder <c>runner</c>).</param>
    public UpdateE2EProtokoll(string dateiPfad, string szenarioId, string quelle = "app")
    {
        _dateiPfad = dateiPfad;
        _szenarioId = szenarioId;
        _quelle = quelle;
        _seq = ZaehleVorhandeneEintraege(dateiPfad, quelle);
    }

    /// <summary>Schreibt ein Protokollereignis als JSONL-Zeile.</summary>
    /// <param name="ereignis">Der Ereignisname (siehe <see cref="UpdateE2EEreignisse"/>).</param>
    /// <param name="daten">Optionale strukturierte Nutzdaten.</param>
    public void Schreibe(string ereignis, object? daten = null)
    {
        lock (_lock)
        {
            var eintrag = new UpdateE2EProtokollEintrag
            {
                Seq = ++_seq,
                ProzessId = Environment.ProcessId,
                SzenarioId = _szenarioId,
                Ereignis = ereignis,
                Daten = daten,
                Utc = DateTimeOffset.UtcNow,
                Quelle = _quelle
            };

            File.AppendAllText(
                _dateiPfad,
                JsonSerializer.Serialize(eintrag, SerializerOptions) + Environment.NewLine);
        }
    }

    /// <summary>
    /// Hängt ein Ereignis fremder Quelle (z. B. des Test-Runners) an die Protokolldatei an.
    /// Die Sequenznummer wird aus der letzten Zeile derselben Quelle fortgesetzt.
    /// </summary>
    /// <param name="dateiPfad">Pfad der JSONL-Protokolldatei.</param>
    /// <param name="szenarioId">Szenario-ID des Eintrags.</param>
    /// <param name="ereignis">Der Ereignisname.</param>
    /// <param name="daten">Optionale strukturierte Nutzdaten.</param>
    /// <param name="quelle">Quellkennung des Eintrags.</param>
    public static void Anfuegen(string dateiPfad, string szenarioId, string ereignis, object? daten = null, string quelle = "runner")
    {
        var seq = ZaehleVorhandeneEintraege(dateiPfad, quelle) + 1;
        var eintrag = new UpdateE2EProtokollEintrag
        {
            Seq = seq,
            ProzessId = Environment.ProcessId,
            SzenarioId = szenarioId,
            Ereignis = ereignis,
            Daten = daten,
            Utc = DateTimeOffset.UtcNow,
            Quelle = quelle
        };

        File.AppendAllText(
            dateiPfad,
            JsonSerializer.Serialize(eintrag, SerializerOptions) + Environment.NewLine);
    }

    private static long ZaehleVorhandeneEintraege(string dateiPfad, string quelle)
    {
        if (!File.Exists(dateiPfad))
            return 0;

        try
        {
            using var stream = new FileStream(dateiPfad, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);
            var letzteSeq = 0L;
            while (reader.ReadLine() is { } zeile)
            {
                if (zeile.Contains($"\"quelle\":\"{quelle}\"", StringComparison.Ordinal)
                    && zeile.Contains("\"seq\":", StringComparison.Ordinal))
                {
                    var seqStart = zeile.IndexOf("\"seq\":", StringComparison.Ordinal) + 6;
                    var seqEnd = seqStart;
                    while (seqEnd < zeile.Length && char.IsDigit(zeile[seqEnd]))
                        seqEnd++;
                    if (long.TryParse(zeile[seqStart..seqEnd], out var seq))
                        letzteSeq = Math.Max(letzteSeq, seq);
                }
            }

            return letzteSeq;
        }
        catch (IOException)
        {
            return 0;
        }
    }
}

/// <summary>Ein strukturierter Eintrag des Update-E2E-JSONL-Protokolls.</summary>
public sealed class UpdateE2EProtokollEintrag
{
    /// <summary>Monotone Sequenznummer innerhalb der Quelle.</summary>
    [JsonPropertyName("seq")]
    public long Seq { get; set; }

    /// <summary>ID des schreibenden Prozesses.</summary>
    [JsonPropertyName("pid")]
    public int ProzessId { get; set; }

    /// <summary>Szenario-ID aus der Testkonfiguration.</summary>
    [JsonPropertyName("scenario")]
    public string SzenarioId { get; set; } = string.Empty;

    /// <summary>Ereignisname (siehe <see cref="UpdateE2EEreignisse"/>).</summary>
    [JsonPropertyName("event")]
    public string Ereignis { get; set; } = string.Empty;

    /// <summary>Optionale strukturierte Nutzdaten.</summary>
    [JsonPropertyName("data")]
    public object? Daten { get; set; }

    /// <summary>UTC-Zeitpunkt des Ereignisses.</summary>
    [JsonPropertyName("utc")]
    public DateTimeOffset Utc { get; set; }

    /// <summary>Quellkennung (<c>app</c> oder <c>runner</c>).</summary>
    [JsonPropertyName("quelle")]
    public string Quelle { get; set; } = "app";
}
