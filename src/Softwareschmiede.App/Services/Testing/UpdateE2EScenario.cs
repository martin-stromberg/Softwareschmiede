using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Softwareschmiede.App.Services.Testing;

/// <summary>
/// Dynamisches Szenariomodell der Update-E2E-Umgebung. Die Szenariodatei wird pro Request und pro
/// markiertem Settings-Lesebefehl neu eingelesen, damit der Test-Runner Antworten, Blockaden und
/// Fehlersteuerung zur Laufzeit umschalten kann.
/// </summary>
public sealed class UpdateE2EScenario
{
    /// <summary>Abbildung exakter Request-URLs auf die zu liefernden Antworten.</summary>
    [JsonPropertyName("responses")]
    public List<UpdateE2EResponse> Answer { get; set; } = [];

    /// <summary>Steuerung des kontrollierten Settings-Lesefehlers (T-09).</summary>
    [JsonPropertyName("settingsReadFailure")]
    public UpdateE2EReadFailure? Readfailure { get; set; }

    /// <summary>Kontrollierte riskante CLI-Aufgaben für den echten Sicherheitsdialog.</summary>
    [JsonPropertyName("cliSafety")]
    public UpdateE2ECliSafety? CliSafe { get; set; }

    /// <summary>Gesteuertes Ergebnis des aufzeichnenden Prozess-Launchers.</summary>
    [JsonPropertyName("processStart")]
    public UpdateE2EProcessStart? ProcessStart { get; set; }

    /// <summary>Findet die konfigurierte Antwort für eine exakte Request-URL.</summary>
    /// <param name="url">Die absolute Request-URL.</param>
    /// <returns>Die konfigurierte Antwort oder <c>null</c>.</returns>
    public UpdateE2EResponse? FindResponse(string? url)
        => url is null
            ? null
            : Answer.FirstOrDefault(a => string.Equals(a.Url, url, StringComparison.OrdinalIgnoreCase));
}

/// <summary>Antwortdefinition für eine exakte Request-URL der Update-Release-/Paketpipeline.</summary>
public sealed class UpdateE2EResponse
{
    /// <summary>Exakte absolute URL, auf die diese Antwort abgebildet wird.</summary>
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    /// <summary>HTTP-Statuscode der Antwort (Standard 200).</summary>
    [JsonPropertyName("status")]
    public int Status { get; set; } = 200;

    /// <summary>Zusätzliche Antwort-Header (z. B. <c>Link</c> für die Pagination).</summary>
    [JsonPropertyName("headers")]
    public Dictionary<string, string>? Header { get; set; }

    /// <summary>Pfad der Antwortdatei relativ zur Testwurzel (JSON-Seite oder ZIP).</summary>
    [JsonPropertyName("bodyFile")]
    public string? BodyFile { get; set; }

    /// <summary>Inline-Antworttext (Alternative zu <see cref="BodyFile"/>).</summary>
    [JsonPropertyName("body")]
    public string? Content { get; set; }

    /// <summary>Content-Type der Antwort (Standard <c>application/json</c>).</summary>
    [JsonPropertyName("contentType")]
    public string? ContentType { get; set; }

    /// <summary>Name eines Gates: Die Antwort wird erst nach <c>{gate}.release</c> geliefert; <c>{gate}.cancel</c> bricht ab.</summary>
    [JsonPropertyName("gate")]
    public string? Gate { get; set; }

    /// <summary>
    /// Name eines Stream-Gates: Der Antwortstream liefert erst <see cref="StreamGateBytes"/> Bytes
    /// und blockiert dann bis zur Freigabe, damit echter Fortschritt im UI sichtbar bleibt.
    /// </summary>
    [JsonPropertyName("streamGate")]
    public string? StreamGate { get; set; }

    /// <summary>Anzahl Bytes, die der Stream vor dem Blockieren liefert (Standard: die Hälfte).</summary>
    [JsonPropertyName("streamGateBytes")]
    public long? StreamGateBytes { get; set; }

    /// <summary>Bei <c>true</c> wird statt einer Antwort ein absichtlicher Transportfehler geworfen.</summary>
    [JsonPropertyName("fault")]
    public bool Error { get; set; }

    /// <summary>Überschreibt die URL-basierte Klassifizierung der Anfrage im Protokoll.</summary>
    [JsonPropertyName("art")]
    public string? Kind { get; set; }
}

/// <summary>
/// Steuerung des kontrollierten Settings-Lesefehlers. Der Fehler greift nur auf mit dem festen
/// EF-Abfragetag markierte Reads des gewählten Versuchs und bleibt bis zur ausdrücklichen
/// Deaktivierung aktiv.
/// </summary>
public sealed class UpdateE2EReadFailure
{
    /// <summary>Aktiviert den kontrollierten Lesefehler.</summary>
    [JsonPropertyName("enabled")]
    public bool Active { get; set; }

    /// <summary>
    /// Gewählter Versuch: <c>startup</c> (einmaliger Startversuch), <c>manuell</c> (manueller
    /// Prüf- oder Installationsversuch), <c>pruefen</c>/<c>starten</c> (nur diese Art) oder
    /// <c>any</c> (jeder Versuch). Reads außerhalb eines Versuchs (z. B. Settings-UI) lösen den
    /// Fehler nicht aus.
    /// </summary>
    [JsonPropertyName("attempt")]
    public string Attempt { get; set; } = "any";

    /// <summary>
    /// Lesegrenze innerhalb des Versuchs: <c>Initial</c> (erster Read), <c>BeforePreparation</c>
    /// (zweiter Read, vor dem Download) oder <c>BeforeUpdaterStart</c> (dritter Read, vor dem Start).
    /// </summary>
    [JsonPropertyName("boundary")]
    public string Limit { get; set; } = "Initial";

    /// <summary>
    /// Bei <c>true</c> wartet der betroffene Read begrenzt auf die Freigabe-Datei
    /// <c>settingsReadFailure.release</c> bzw. den Abbruch über <c>settingsReadFailure.cancel</c>;
    /// bei <c>false</c> schlägt der Read sofort fehl.
    /// </summary>
    [JsonPropertyName("waitForRelease")]
    public bool WaitForRelease { get; set; } = true;

    /// <summary>Maximale Wartezeit der Freigabe in Sekunden (Standard 60).</summary>
    [JsonPropertyName("waitTimeoutSeconds")]
    public int WaittimeSeconds { get; set; } = 60;

    /// <summary>Ordnet die konfigurierte Lesegrenze der Read-Ordnungszahl innerhalb des Versuchs zu.</summary>
    /// <returns>Die Ordnungszahl (1-basiert), ab der markierte Reads fehlschlagen.</returns>
    public int GrenzOrdinal() => Limit switch
    {
        "BeforePreparation" => 2,
        "BeforeUpdaterStart" => 3,
        _ => 1
    };

    /// <summary>Prüft, ob die Versuchsart des laufenden Versuchs vom konfigurierten Selektor erfasst wird.</summary>
    /// <param name="attemptKind">Die Art des aktuellen Versuchs (<c>startup</c>, <c>pruefen</c>, <c>starten</c>).</param>
    /// <returns><c>true</c>, wenn der Versuch ausgewählt ist.</returns>
    public bool MatchesAttempt(string attemptKind) => Attempt switch
    {
        "any" => true,
        "manuell" => attemptKind is "pruefen" or "starten",
        var v => string.Equals(v, attemptKind, StringComparison.OrdinalIgnoreCase)
    };
}

/// <summary>Kontrollierte riskante CLI-Aufgaben für den echten Ja/Nein-Sicherheitsdialog.</summary>
public sealed class UpdateE2ECliSafety
{
    /// <summary>Aktiviert die Fixture-Sicherheitsprüfung (statt der produktiven Auswertung).</summary>
    [JsonPropertyName("enabled")]
    public bool Active { get; set; }

    /// <summary>Beschreibungen der simuliert riskanten Aufgaben.</summary>
    [JsonPropertyName("riskyTasks")]
    public List<string> RiskyWorkItems { get; set; } = [];
}

/// <summary>Gesteuertes Ergebnis des aufzeichnenden Update-Prozess-Launchers.</summary>
public sealed class UpdateE2EProcessStart
{
    /// <summary>Ergebnis des Starts: <c>Erfolg</c>, <c>Fehler</c> (Rückgabe <c>false</c>) oder <c>Ausnahme</c>.</summary>
    [JsonPropertyName("result")]
    public string Result { get; set; } = "Erfolg";
}
