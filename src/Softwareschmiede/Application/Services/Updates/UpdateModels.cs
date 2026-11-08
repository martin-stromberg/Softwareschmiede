namespace Softwareschmiede.Application.Services.Updates;

/// <summary>Lokale Versionsinformationen aus der installierten <c>version.json</c>.</summary>
/// <param name="Version">Semantische Version ohne führendes <c>v</c>.</param>
/// <param name="TagName">Optionaler Release-Tag.</param>
/// <param name="Commit">Optionaler Commit-SHA.</param>
/// <param name="CreatedAtUtc">Optionaler Erstellungszeitpunkt der Version.</param>
public sealed record InstalledVersionInfo(
    string Version,
    string? TagName,
    string? Commit,
    DateTimeOffset? CreatedAtUtc);

/// <summary>Informationen zu einem verfügbaren GitHub-Release-Update.</summary>
/// <param name="Version">Semantische Release-Version ohne führendes <c>v</c>, inklusive Prerelease-Suffix und Build-Metadaten.</param>
/// <param name="TagName">GitHub-Release-Tag.</param>
/// <param name="AssetName">Name des Release-Assets.</param>
/// <param name="DownloadUrl">Direkte Download-URL des Release-Assets.</param>
/// <param name="PublishedAt">Optionaler Veröffentlichungszeitpunkt.</param>
/// <param name="IsPrerelease">Gibt an, ob das Release als Prerelease klassifiziert ist (GitHub-Flag <c>prerelease</c> oder SemVer-Prerelease-Suffix).</param>
/// <param name="FallbackDownloadUrl">Optionale GitHub-API-Asset-URL für den einmaligen Download-Fallback bei 5xx der primären <paramref name="DownloadUrl"/>.</param>
public sealed record UpdateInfo(
    string Version,
    string TagName,
    string AssetName,
    Uri DownloadUrl,
    DateTimeOffset? PublishedAt,
    bool IsPrerelease,
    Uri? FallbackDownloadUrl = null);

/// <summary>Persistierter Modus der Update-Prüfung mit festen Speicherwerten.</summary>
public enum UpdateMode
{
    /// <summary>Update-Prüfung und -Installation sind deaktiviert.</summary>
    Aus = 0,

    /// <summary>Es wird nur auf Updates geprüft; ein gefundenes Update wird angeboten, aber nie automatisch installiert.</summary>
    NurPruefen = 1,

    /// <summary>Beim Programmstart wird geprüft und ein gefundenes Update automatisch installiert.</summary>
    BeiProgrammstartPruefenUndAusfuehren = 2
}

/// <summary>Exklusiver Update-Kanal, der sich aus dem konfigurierten Update-Pfad ableitet.</summary>
public enum UpdateKanal
{
    /// <summary>Ausschließlich stabile Produktivversionen werden als Update-Kandidaten berücksichtigt.</summary>
    Produktiv = 0,

    /// <summary>Ausschließlich Prerelease-Versionen (Release-Kandidaten) werden als Update-Kandidaten berücksichtigt.</summary>
    ReleaseCandidate = 1
}

/// <summary>Gemeinsam geladenes, unveränderliches Paar der Update-Einstellungen.</summary>
/// <param name="Modus">Persistierter Update-Modus.</param>
/// <param name="UpdatePfad">Persistierter Update-Pfad (Kanalselektor; siehe <see cref="UpdatePfadKanalResolver"/>).</param>
public sealed record UpdateSettings(UpdateMode Modus, string UpdatePfad);

/// <summary>Unveränderlicher Optionsvertrag einer Update-Prüfung für <see cref="IUpdateService"/> und <see cref="IUpdateReleaseClient"/>.</summary>
/// <param name="UpdatePfad">Der konfigurierte Update-Pfad (Kanalselektor; siehe <see cref="UpdatePfadKanalResolver"/>).</param>
public sealed record UpdateCheckOptions(string UpdatePfad)
{
    /// <summary>Der aus <see cref="UpdatePfad"/> abgeleitete exklusive Update-Kanal.</summary>
    public UpdateKanal Kanal => UpdatePfadKanalResolver.ErmittleKanal(UpdatePfad);
}

/// <summary>
/// Ergebnis der Release-Abfrage eines <see cref="IUpdateReleaseClient"/>. Unterscheidet eine
/// fehlgeschlagene Abfrage (HTTP-Fehler, Timeout, ungültige Antwort) von einer erfolgreichen
/// Abfrage ohne passenden Release-Kandidaten.
/// </summary>
public sealed record UpdateReleaseLookupResult
{
    private UpdateReleaseLookupResult(bool erfolg, UpdateInfo? release)
    {
        Erfolg = erfolg;
        Release = release;
    }

    /// <summary>Gibt an, ob die Release-Abfrage technisch erfolgreich war.</summary>
    public bool Erfolg { get; }

    /// <summary>Das neueste zulässige Release oder <c>null</c>, wenn kein Kandidat gefunden wurde.</summary>
    public UpdateInfo? Release { get; }

    /// <summary>Die Release-Abfrage ist fehlgeschlagen.</summary>
    public static UpdateReleaseLookupResult Fehlgeschlagen { get; } = new(false, null);

    /// <summary>Die Release-Abfrage war erfolgreich, lieferte aber keinen passenden Kandidaten.</summary>
    public static UpdateReleaseLookupResult KeinTreffer { get; } = new(true, null);

    /// <summary>Erzeugt ein erfolgreiches Ergebnis mit dem gefundenen Release.</summary>
    /// <param name="release">Das neueste zulässige Release.</param>
    public static UpdateReleaseLookupResult Gefunden(UpdateInfo release) => new(true, release);
}

/// <summary>Status einer Update-Prüfung.</summary>
public enum UpdateCheckStatus
{
    /// <summary>Es ist kein neueres Update verfügbar.</summary>
    KeinUpdate,

    /// <summary>Ein neueres Update ist verfügbar.</summary>
    UpdateVerfuegbar,

    /// <summary>Die Update-Prüfung konnte nicht zuverlässig durchgeführt werden.</summary>
    NichtPruefbar
}

/// <summary>Ergebnis einer Update-Prüfung.</summary>
/// <param name="Status">Fachlicher Prüfstatus.</param>
/// <param name="Update">Optionales verfügbares Update.</param>
/// <param name="Message">Optionale Diagnose- oder Benutzerhinweismeldung.</param>
public sealed record UpdateCheckResult(UpdateCheckStatus Status, UpdateInfo? Update = null, string? Message = null)
{
    /// <summary>Erzeugt ein Ergebnis ohne verfügbares Update.</summary>
    public static UpdateCheckResult KeinUpdate(string? message = null) => new(UpdateCheckStatus.KeinUpdate, null, message);

    /// <summary>Erzeugt ein Ergebnis mit verfügbarem Update.</summary>
    public static UpdateCheckResult UpdateVerfuegbar(UpdateInfo update, string? message = null) => new(UpdateCheckStatus.UpdateVerfuegbar, update, message);

    /// <summary>Erzeugt ein Ergebnis für eine nicht prüfbare Situation.</summary>
    public static UpdateCheckResult NichtPruefbar(string? message = null) => new(UpdateCheckStatus.NichtPruefbar, null, message);
}

/// <summary>Phase der Update-Vorbereitung.</summary>
public enum UpdatePreparationPhase
{
    /// <summary>Das Release-Asset wird heruntergeladen.</summary>
    Download,

    /// <summary>Das Release-ZIP wird entpackt.</summary>
    Entpacken,

    /// <summary>Das externe Update-Skript wird vorbereitet.</summary>
    UpdateVorbereiten
}

/// <summary>Fortschrittsmeldung während der Update-Vorbereitung.</summary>
/// <param name="Phase">Aktuelle Phase.</param>
/// <param name="Percent">Optionaler Prozentwert von 0 bis 100.</param>
/// <param name="Message">Anzeigetext für den Fortschrittsdialog.</param>
public sealed record UpdatePreparationProgress(UpdatePreparationPhase Phase, double? Percent, string Message);

/// <summary>Ergebnis einer erfolgreich vorbereiteten Update-Installation.</summary>
/// <param name="ZipPath">Pfad zur heruntergeladenen ZIP-Datei.</param>
/// <param name="ExtractedDirectory">Pfad zum entpackten Release-Verzeichnis.</param>
/// <param name="ScriptPath">Pfad zum erzeugten Update-Skript.</param>
/// <param name="LogPath">Pfad zur Update-Logdatei.</param>
/// <param name="RequiresElevation">Gibt an, ob der Skriptstart erhöhte Rechte anfordern soll.</param>
public sealed record UpdatePreparationResult(
    string ZipPath,
    string ExtractedDirectory,
    string ScriptPath,
    string LogPath,
    bool RequiresElevation);

/// <summary>Ergebnis der CLI-Sicherheitsprüfung vor einem Update.</summary>
/// <param name="RiskyTaskCount">Anzahl riskanter aktiver CLI-Aufgaben.</param>
/// <param name="RiskyTasks">Kurze Beschreibungen der riskanten Aufgaben.</param>
public sealed record CliUpdateSafetyResult(int RiskyTaskCount, IReadOnlyList<string> RiskyTasks)
{
    /// <summary>Gibt an, ob eine Sicherheitsbestätigung erforderlich ist.</summary>
    public bool RequiresConfirmation => RiskyTaskCount > 0;
}
