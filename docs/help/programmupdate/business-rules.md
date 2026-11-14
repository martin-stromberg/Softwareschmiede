← [Zurück zur Übersicht](index.md)

# Programmupdate — Business Rules

## Update-Modi

Der gespeicherte Update-Modus (`UpdateMode`, persistiert als Zahl unter dem Schlüssel `updates.mode`) steuert das Gesamtverhalten:

| Modus | Wert | Startautomatik | Manuelle Prüfung („⟳ Prüfen") | Installation |
|-------|------|----------------|-------------------------------|--------------|
| `Aus` | 0 | Kein Releaseabruf | Gesperrt (`KannUpdatePruefen` = false); der Methodenrumpf bricht ebenfalls vor der Prüfung ab | Nicht möglich; `⇧ Update` ausgeblendet |
| `NurPruefen` | 1 (Default) | Einmal prüfen, Fund anbieten | Prüfen + Angebot | Nur per Klick auf „⇧ Update", mit erneuter Prüfung |
| `BeiProgrammstartPruefenUndAusfuehren` | 2 | Einmal prüfen **und** bei Fund automatisch installieren | Prüfen + nur Angebot (keine Automatik) | Automatisch beim Start bzw. per Klick mit erneuter Prüfung |

Regeln:

- `Aus` sperrt die Startprüfung, den manuellen Prüfbefehl und den manuellen Installationsstart — ohne Ausnahmen.
- Die automatische Installation ist auf den einmaligen Programmstart begrenzt (`_startInitialisierungErfolgt`). Spätere manuelle Prüfungen installieren nie automatisch, auch nicht im Startmodus.
- Die CLI-Sicherheitsprüfung samt Bestätigungsdialog gilt in allen Modi, in denen eine Installation ansteht — auch bei der Startautomatik.
- Fehlende oder ungültige gespeicherte Werte fallen auf `NurPruefen` bzw. den Update-Pfad `Produktivversionen` zurück.
- Ein Datenbank-**Lesefehler** ist kein erfolgreich gelesener fehlender Schlüssel: Er erzeugt den sichtbaren Nicht-prüfbar-Zustand und verhindert jeden Updater-Start. Es gibt keine Ersatzfreigabe durch Defaults, Snapshots oder Caches.

## Update-Pfad und exklusiver Update-Kanal

- Der gespeicherte Update-Pfad (`updates.pfad`, `AppEinstellungService.UpdatePfadKey`) kennt genau zwei gültige Werte (`UpdatePfadKanalResolver.ProduktivversionenPfad`/`VorabversionenPfad`), die auf den exklusiven `UpdateKanal` abgebildet werden: `Produktivversionen` → `UpdateKanal.Produktiv`, `Vorabversionen` → `UpdateKanal.ReleaseCandidate` (`UpdatePfadKanalResolver.ErmittleKanal`, OrdinalIgnoreCase + Trim).
- Fehlende, leere oder unbekannte gespeicherte Werte werden beim Lesen über `UpdatePfadKanalResolver.NormalisierePfad` auf `Produktivversionen` zurückgeführt; `SetUpdateSettingsAsync` lehnt Werte außerhalb der zwei bekannten Pfade per `IstGueltigerPfad` mit `ArgumentOutOfRangeException` ab — es wird dann nichts geschrieben.
- Der Kanal filtert die Kandidatenmenge **strikt exklusiv**: Im Produktiv-Kanal überspringt `GitHubReleaseClient.TryCreateCandidate` alle als Prerelease klassifizierten Einträge, im RC-Kanal alle nicht-prerelease Einträge. Stabile und Vorabversionen konkurrieren niemals gemeinsam im SemVer-Vergleich.
- Hintergrund der Trennung: RC-Installationen nutzen einen eigenen Datenbank-Speicherort (`DatenbankPfadResolver`: `version.json`-`tagName` mit „-rc"-Infix → `{Programmverzeichnis}\softwareschmiede.db`, produktive Tags → `%LocalAppData%\Softwareschmiede\softwareschmiede.db`). Ein kanalübergreifendes Update würde die Installation auf den fremden Datenbestand umschalten.
- Ein Release gilt als Prerelease, wenn das GitHub-Flag `prerelease` gesetzt **oder** das Tag ein SemVer-Prerelease-Suffix trägt (`SemanticUpdateVersion.IsPrerelease`). `UpdateInfo.IsPrerelease` führt diese Klassifikation verbindlich weiter — auch bei GitHub-markierten Tags ohne Suffix.
- **Defensivprüfung:** Ein Ergebnis, das nicht zum gewählten Kanal passt (`UpdateInfo.IsPrerelease` widerspricht `UpdateCheckOptions.Kanal` in beide Richtungen), ist eine interne Vertragsverletzung und wird nicht angeboten (`NichtPruefbar`).
- **Konsistenzwarnung Installation ↔ Kanal:** `UpdatePfadKanalResolver.ErmittleKanalFuerTagName` ordnet den `tagName` der installierten `version.json` dem Kanalmodell zu („-rc"-Infix → `ReleaseCandidate`; fehlender/leerer Tag konservativ → `ReleaseCandidate`, gleiche Regel wie `DatenbankPfadResolver`). Weicht der Installationskanal vom gewählten Kanal ab, bleibt das Ergebnis `UpdateVerfuegbar`, trägt aber eine Warn-`Message` („Der gewählte Update-Pfad passt nicht zur installierten Version. Das Update wird trotzdem angeboten."), die über `UpdateHinweis` in der Seitenleiste erscheint; zusätzlich wird ein `LogWarning` geschrieben. Die Warnung greift nur im Angebotsfall — `KeinUpdate` und `NichtPruefbar` bleiben warnfrei.
- Der Altbestand `updates.includePrereleases` wird nicht mehr gelesen oder geschrieben (stiller Verfall, keine Migration).

## SemVer-Versionsvergleich

- Gültige Versionen folgen SemVer 2.0 vollständig: `X.Y.Z[-prerelease][+metadaten]`. `UpdateVersionComparer.TryParse` toleriert führende Leerzeichen und ein führendes `v`/`V`; `Normalize` liefert die kanonische Form.
- Präzedenz: zuerst `Major`/`Minor`/`Patch`, dann Prerelease-Identifier:
  - Numerische Identifier werden numerisch verglichen und sind kleiner als nichtnumerische.
  - Nichtnumerische Identifier werden ordinal und case-sensitiv verglichen.
  - Eine kürzere identische Identifierfolge ist kleiner als deren Verlängerung (`1.0.0-rc.1` < `1.0.0-rc.1.1`).
  - Eine stabile Version ist höher als ihre eigenen Prereleases (`1.3.0` > `1.3.0-rc.2`).
- Build-Metadaten (`+…`) haben keinen Einfluss auf Rangfolge und Gleichheit, bleiben aber in Anzeige und Pfaden erhalten.
- Konsequenz für die Update-Kette im Kanal: Nach installiertem `1.3.0-rc.1` gilt im RC-Kanal (`Vorabversionen`) nur `1.3.0-rc.2` (oder ein neuerer RC) als Update — die semantisch neuere stabile `1.3.0` ist dort kein zulässiger Kandidat; im Produktiv-Kanal gilt umgekehrt ausschließlich die stabile Folgeversion. Der SemVer-Vergleich selbst ist unverändert — die Kanaltrennung wirkt auf die Kandidatenmenge, nicht auf die Präzedenz.
- `IsNewer` liefert nur bei zwei gültig geparsten Versionen ein Ergebnis; ungültige Kandidaten können nie als „neuer" gelten.

## Release-Auswahl (Pagination)

- Die Release-Suche läuft über die paginierte GitHub-Releases-Liste (`per_page=100`), nicht nur über das jeweils neueste Release.
- Pro Eintrag gilt: `draft`, ungültiger Tag oder fehlendes `release.zip`-Asset mit absoluter HTTPS-URL → Eintrag wird übersprungen. Ein unbrauchbarer Eintrag verhindert andere Kandidaten nicht.
- Gewinner ist die höchste zulässige SemVer über **alle** Seiten, unabhängig von Reihenfolge und Datum; bei gleicher Präzedenz bleibt die erste Fundstelle.
- Folge-URLs müssen HTTPS, `api.github.com` und denselben Repository-Releases-Pfad betreffen; wiederholte URLs oder ungültige Paginierung sind ein Fehler.
- HTTP-, JSON- oder Timeout-Fehler auf irgendeiner Seite erzeugen keinen Teiltreffer, sondern `Fehlgeschlagen` → `NichtPruefbar`.
- Ein gemeinsamer Timeout (`UpdateOptions.CheckTimeout`, 15 s) begrenzt alle Seiten zusammen.

## Download-Fallback auf die API-Asset-URL

- `UpdateInfo` trägt je Download-Quelle eine `UpdateDownloadRoute` — `PaketDownload` für das `release.zip`-Asset und `ManifestDownload` für das `update.json`-Manifest. Jede Route besteht aus der primären `browser_download_url` (`Primary`) und einer optionalen GitHub-API-Asset-URL (`Fallback`) der Form `https://api.github.com/repos/{owner}/{repo}/releases/assets/{id}`.
- Herkunft: `GitHubReleaseClient.CreateFallbackDownloadUrl` bevorzugt das servergelieferte Asset-Feld `url` (`GitHubAsset.Url`), sofern es `IsAllowedAssetApiUrl` besteht — absolut, HTTPS, Host `api.github.com` (OrdinalIgnoreCase). Das Host-Pinning verhindert, dass eine servergelieferte URL den Download auf einen Fremdhost lenkt. Fehlt `url` oder ist sie ungültig, wird die Fallback-URL aus `GitHubAsset.Id > 0` aufgebaut; ohne beides bleibt `Fallback` `null` und die Route bleibt fallbacklos nutzbar.
- Auslöser: ausschließlich ein empfangener HTTP-Status ≥ 500 (5xx) auf der primären Antwort — vor Stream-Beginn. Transportfehler ohne HTTP-Antwort, `429`, Timeout und ein Abriss mitten im Download lösen **keinen** Fallback aus.
- Umfang: genau ein Zweitversuch, ohne Verzögerung und ohne weitere Wiederholungen. Der Fallback-Request trägt `Accept: application/octet-stream` (sonst liefert die API JSON-Metadaten statt des Binärstroms) und `User-Agent: Softwareschmiede/1.0` (die GitHub-API lehnt Requests ohne User-Agent mit 403 ab); der primäre Request bleibt headerlos.
- Scheitert auch der Fallback oder liegt kein `Fallback` vor, gilt der bisherige Fehlerpfad: `EnsureSuccessStatusCode` → `HttpRequestException` → „Update konnte nicht vorbereitet werden."
- Der Mechanismus ist intern: kein UI-Hinweis, keine Konfiguration, kein Opt-out — sichtbar nur als `LogWarning` (mit Statuscode und beiden URLs) im Anwendungsprotokoll. Die gemeinsame Hilfsmethode `UpdatePackageService.SendWithFallbackAsync` wendet ihn gleichermaßen auf den Paket-Download (`DownloadAsync`) und den Manifest-Abruf (`DownloadManifestAsync`) an — die einzigen beiden `browser_download_url`-Zugriffe.

## Paket-Verifikation (SHA-256 gegen `update.json`)

**Beschreibung:** Vor dem Entpacken wird die Integrität des heruntergeladenen `release.zip` gegen das Update-Manifest `update.json` geprüft. Das Manifest ist ein reguläres Release-Asset (Schema: `version`, `publishedAt`, `assets[]` mit `assetName`, `assetUrl`, `sha256`, `sizeBytes`), das der Release-Workflow seit Einführung des Paket-Schritts zu jedem Release hochlädt. Die Verifikation schützt vor einem manipulierten oder korrupten Paket-Download (z. B. CDN-/Proxy-Eingriff, nachträglich ausgetauschtem Asset); sie ist ein verbindlicher Sicherheitsschritt — kein Opt-out, keine Konfiguration.

**Bedingungen:**

- Greift in **jedem** Vorbereitungslauf (`UpdatePackageService.PreparePackageAsync`) — Startautomatik und manueller Start gleichermaßen; Zeitpunkt: nach erfolgreichem ZIP-Download, vor `ZipFile.ExtractToDirectory`.
- `UpdateInfo.ManifestDownload` muss gesetzt sein; `GitHubReleaseClient` befüllt die Route aus dem `update.json`-Asset (`UpdateOptions.ManifestAssetName`) desselben Releases. Ein Release ohne Manifest-Asset bleibt Update-Angebot — das Filtern erfolgt bewusst nicht im Release-Client, sondern ausschließlich hier.
- Verbindlich ist ausschließlich `sha256` (64-stelliger Hexwert, Vergleich `OrdinalIgnoreCase`); `sizeBytes` und die Manifest-`version` werden nicht geprüft.

**Verhalten:**

- Hash stimmt überein → die Vorbereitung läuft unverändert weiter (Entpacken, Validierung, Skripterzeugung).
- `ManifestDownload == null`, Manifest-HTTP-Fehler (inkl. einmaligem 5xx-Fallback), ungültiges JSON, fehlender `release.zip`-Eintrag oder leerer/formal ungültiger `sha256` → `InvalidDataException` bzw. `HttpRequestException` → das Paket gilt als nicht verifizierbar und wird verworfen — es gibt keinen stillen Fallback auf „ungeprüft installieren", da ein solcher die Schutzwirkung durch einfaches Weglassen des Assets unterlaufen würde.
- Hash-Mismatch → `InvalidDataException` (beide Hashwerte werden per `LogWarning` protokolliert) → Abbruch vor dem ersten Schreibzugriff des Entpackens.
- Jeder Fehlschlag läuft über den bestehenden Catch-all von `PreparePackageAsync` (Aufräumen von `*.download`, `release.zip`, Extraktionsverzeichnis) zu `MainWindowUpdateFlow` → `SetError` + `UpdateHinweis` „Update konnte nicht vorbereitet werden." — kein Skript, kein Prozessstart, kein `Shutdown()`.
- Konsequenz: Ein Release ohne `update.json`-Asset (nur Altbestand vor Einführung des Manifest-Schritts) ist faktisch nicht mehr installierbar.

**Umsetzung:** `UpdatePackageService.DownloadManifestAsync` (Abruf über `SendWithFallbackAsync`), `UpdatePackageService.GetExpectedSha256` (DTOs `UpdateManifest`/`UpdateManifestAsset`, `System.Text.Json`), `UpdatePackageService.VerifyPackageHashAsync` (`SHA256.HashDataAsync` + `Convert.ToHexString`). Sichtbar als Phase `UpdatePreparationPhase.Verifizieren` (Anzeige „Verifikation" im Fortschrittsdialog). Das Manifest selbst ist nicht signiert und wird über denselben TLS-/GitHub-Kanal bezogen wie Release-Liste und ZIP — ein vollständig kompromittiertes GitHub-Release bleibt bewusstes Restrisiko; eine Authenticode-Signaturprüfung der entpackten Dateien ist nicht Teil dieser Verifikation.

## Aktualitäts- und Concurrency-Regeln

- Alle Update-Eintritte (Startautomatik, manuelle Prüfung, manueller Start) laufen über dasselbe nicht wartende `_updateGate` — ein zweiter Versuch während eines laufenden Vorgangs wird verworfen.
- Der `UpdateService` serialisiert `CheckForUpdateAsync` zusätzlich über `_checkGate`.
- Einstellungs-Generation: Jede gespeicherte Änderung der Update-Werte erhöht `_updateSettingsGeneration`, invalidiert das Angebot (`VerfuegbaresUpdate`/`UpdateVerfuegbar`/`UpdateHinweis` werden geleert) und cancelt den laufenden Ablauf.
- Nach jedem asynchronen Abschnitt werden Cancellation-Token und Generation geprüft (`IstUpdateVersuchAktuell`); verspätete Ergebnisse stellen keine Angebote wieder her und starten keine Folgeschritte.
- Vor `PrepareUpdateAsync` und unmittelbar vor `StartPreparedUpdateAsync` werden die gespeicherten Werte erneut gelesen und mit dem Snapshot verglichen — Abweichung oder Lesefehler beendet den Versuch ohne Installation, auch wenn Paket und Skript bereits vorbereitet sind.
- Der synchrone Prozessstart des Update-Skripts ist die Übergabegrenze: Erst danach folgt `IApplicationShutdownService.Shutdown()`; Vorbereitungsfehler, Startfehler oder Abbruch erzeugen keinen Shutdown.
- Ein letztes Prüfergebnis ist keine Installationsfreigabe — der manuelle Start prüft immer erneut mit den aktuellen Optionen.

## Sicherheitsprüfung vor der Installation

- `CliUpdateSafetyService.CheckAsync` lädt aktive Aufgaben und zählt als riskant, was `AufgabeLaufAktivitaet.IstAktiv(AktiveRunId, LastHeartbeatUtc, now)` als aktiv laufenden CLI-Prozess klassifiziert.
- `CliUpdateSafetyResult.RequiresConfirmation` (RiskyTaskCount > 0) führt zum Bestätigungsdialog „Update starten?" mit bis zu fünf gelisteten Aufgaben (Rest als „weitere n Aufgabe(n)").
- Ablehnung verhindert Fortschrittsdialog, Download und Start — auch für die Startautomatik.
