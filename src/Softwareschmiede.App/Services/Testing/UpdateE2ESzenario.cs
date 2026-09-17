using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Softwareschmiede.App.Services.Testing;

/// <summary>
/// Dynamisches Szenariomodell der Update-E2E-Umgebung. Die Szenariodatei wird pro Request und pro
/// markiertem Settings-Lesebefehl neu eingelesen, damit der Test-Runner Antworten, Blockaden und
/// Fehlersteuerung zur Laufzeit umschalten kann.
/// </summary>
public sealed class UpdateE2ESzenario
{
    /// <summary>Abbildung exakter Request-URLs auf die zu liefernden Antworten.</summary>
    [JsonPropertyName("responses")]
    public List<UpdateE2EAntwort> Antworten { get; set; } = [];

    /// <summary>Steuerung des kontrollierten Settings-Lesefehlers (T-09).</summary>
    [JsonPropertyName("settingsReadFailure")]
    public UpdateE2ELesefehler? Lesefehler { get; set; }

    /// <summary>Kontrollierte riskante CLI-Aufgaben für den echten Sicherheitsdialog.</summary>
    [JsonPropertyName("cliSafety")]
    public UpdateE2ECliSicherheit? CliSicherheit { get; set; }

    /// <summary>Gesteuertes Ergebnis des aufzeichnenden Prozess-Launchers.</summary>
    [JsonPropertyName("processStart")]
    public UpdateE2EProzessStart? ProzessStart { get; set; }

    /// <summary>Findet die konfigurierte Antwort für eine exakte Request-URL.</summary>
    /// <param name="url">Die absolute Request-URL.</param>
    /// <returns>Die konfigurierte Antwort oder <c>null</c>.</returns>
    public UpdateE2EAntwort? FindeAntwort(string? url)
        => url is null
            ? null
            : Antworten.FirstOrDefault(a => string.Equals(a.Url, url, StringComparison.OrdinalIgnoreCase));
}

/// <summary>Antwortdefinition für eine exakte Request-URL der Update-Release-/Paketpipeline.</summary>
public sealed class UpdateE2EAntwort
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
    public string? BodyDatei { get; set; }

    /// <summary>Inline-Antworttext (Alternative zu <see cref="BodyDatei"/>).</summary>
    [JsonPropertyName("body")]
    public string? Inhalt { get; set; }

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
    public bool Fehler { get; set; }
}

/// <summary>
/// Steuerung des kontrollierten Settings-Lesefehlers. Der Fehler greift nur auf mit dem festen
/// EF-Abfragetag markierte Reads des gewählten Versuchs und bleibt bis zur ausdrücklichen
/// Deaktivierung aktiv.
/// </summary>
public sealed class UpdateE2ELesefehler
{
    /// <summary>Aktiviert den kontrollierten Lesefehler.</summary>
    [JsonPropertyName("enabled")]
    public bool Aktiv { get; set; }

    /// <summary>
    /// Gewählter Versuch: <c>startup</c> (einmaliger Startversuch), <c>manuell</c> (manueller
    /// Prüf- oder Installationsversuch), <c>pruefen</c>/<c>starten</c> (nur diese Art) oder
    /// <c>any</c> (jeder Versuch). Reads außerhalb eines Versuchs (z. B. Settings-UI) lösen den
    /// Fehler nicht aus.
    /// </summary>
    [JsonPropertyName("attempt")]
    public string Versuch { get; set; } = "any";

    /// <summary>
    /// Lesegrenze innerhalb des Versuchs: <c>Initial</c> (erster Read), <c>BeforePreparation</c>
    /// (zweiter Read, vor dem Download) oder <c>BeforeUpdaterStart</c> (dritter Read, vor dem Start).
    /// </summary>
    [JsonPropertyName("boundary")]
    public string Grenze { get; set; } = "Initial";

    /// <summary>
    /// Bei <c>true</c> wartet der betroffene Read begrenzt auf die Freigabe-Datei
    /// <c>settingsReadFailure.release</c> bzw. den Abbruch über <c>settingsReadFailure.cancel</c>;
    /// bei <c>false</c> schlägt der Read sofort fehl.
    /// </summary>
    [JsonPropertyName("waitForRelease")]
    public bool AufFreigabeWarten { get; set; } = true;

    /// <summary>Maximale Wartezeit der Freigabe in Sekunden (Standard 60).</summary>
    [JsonPropertyName("waitTimeoutSeconds")]
    public int WartezeitSekunden { get; set; } = 60;

    /// <summary>Ordnet die konfigurierte Lesegrenze der Read-Ordnungszahl innerhalb des Versuchs zu.</summary>
    /// <returns>Die Ordnungszahl (1-basiert), ab der markierte Reads fehlschlagen.</returns>
    public int GrenzOrdinal() => Grenze switch
    {
        "BeforePreparation" => 2,
        "BeforeUpdaterStart" => 3,
        _ => 1
    };

    /// <summary>Prüft, ob die Versuchsart des laufenden Versuchs vom konfigurierten Selektor erfasst wird.</summary>
    /// <param name="versuchArt">Die Art des aktuellen Versuchs (<c>startup</c>, <c>pruefen</c>, <c>starten</c>).</param>
    /// <returns><c>true</c>, wenn der Versuch ausgewählt ist.</returns>
    public bool TrifftVersuch(string versuchArt) => Versuch switch
    {
        "any" => true,
        "manuell" => versuchArt is "pruefen" or "starten",
        var v => string.Equals(v, versuchArt, StringComparison.OrdinalIgnoreCase)
    };
}

/// <summary>Kontrollierte riskante CLI-Aufgaben für den echten Ja/Nein-Sicherheitsdialog.</summary>
public sealed class UpdateE2ECliSicherheit
{
    /// <summary>Aktiviert die Fixture-Sicherheitsprüfung (statt der produktiven Auswertung).</summary>
    [JsonPropertyName("enabled")]
    public bool Aktiv { get; set; }

    /// <summary>Beschreibungen der simuliert riskanten Aufgaben.</summary>
    [JsonPropertyName("riskyTasks")]
    public List<string> RiskanteAufgaben { get; set; } = [];
}

/// <summary>Gesteuertes Ergebnis des aufzeichnenden Update-Prozess-Launchers.</summary>
public sealed class UpdateE2EProzessStart
{
    /// <summary>Ergebnis des Starts: <c>Erfolg</c>, <c>Fehler</c> (Rückgabe <c>false</c>) oder <c>Ausnahme</c>.</summary>
    [JsonPropertyName("result")]
    public string Ergebnis { get; set; } = "Erfolg";
}
