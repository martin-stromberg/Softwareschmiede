# Umsetzungsplan: SHA-256-Verifikation des Update-Pakets nachrüsten

## Übersicht

Der `UpdatePackageService` verifiziert das heruntergeladene `release.zip` künftig vor dem Entpacken gegen das bereits als Release-Asset existierende Manifest `update.json`: Nach erfolgreichem ZIP-Download wird das Manifest abgerufen, der erwartete SHA-256 des Paket-Assets gelesen und mit dem Hash der heruntergeladenen Datei verglichen. Bei Abweichung oder nicht verifizierbarem Manifest bricht die Vorbereitung über den bestehenden Fehlerpfad (`InvalidDataException`/`HttpRequestException` → Catch-all-Aufräumen → `SetError` im Fortschrittsdialog) kontrolliert ab — ein manipuliertes oder korruptes Paket wird niemals entpackt. Betroffen sind `UpdateModels.cs` (`UpdateInfo`, `UpdatePreparationPhase`), `UpdateOptions`, `GitHubReleaseClient`, `UpdatePackageService`, `UpdateProgressViewModel` sowie die Update-E2E-Infrastruktur (Fixture, Szenario-JSON-Erzeuger, Handler-Klassifizierung) und die zugehörigen Tests.

## Scope-Klärung

Verbindliche fachliche Abgrenzung vor der Umsetzung — übernommen bzw. konkretisiert aus dem Abschnitt `Fachlicher Scope` der `requirement.md`:

| Feld | Festlegung |
|------|------------|
| Verarbeitungseinheit | Die Integritätsverifikation genau eines heruntergeladenen Update-Pakets gegen das Update-Manifest innerhalb eines Vorbereitungslaufs (`UpdatePackageService.PreparePackageAsync`): Manifest-Abruf, Auslesen des erwarteten `sha256` für das `release.zip`-Asset, Hashvergleich vor dem Entpacken. Nicht zum Scope: Release-Auswahl, Updater-Start, Inhalt des entpackten Pakets. |
| Auslösende Ereignisse | Jeder Update-Vorbereitungslauf auf dem gemeinsamen Installationspfad (`UpdateService.PrepareUpdateAsync` → `PreparePackageAsync`), gleichermaßen bei Startautomatik (`UpdateMode.BeiProgrammstartPruefenUndAusfuehren`) und manuellem `UpdateStartenCommand`. Die Verifikation greift nach erfolgreichem ZIP-Download (inkl. etwaigem 5xx-Fallback) und **vor** `ZipFile.ExtractToDirectory`. |
| Erwartete Wirkung | Hash stimmt überein → Vorbereitung läuft unverändert weiter (Entpacken, Paketvalidierung, Skripterzeugung). Hash-Abweichung oder nicht verifizierbares Manifest → kontrollierter Abbruch über den bestehenden Fehlerpfad (Catch-all räumt `*.download`, `release.zip`, Extraktionsverzeichnis auf → `MainWindowUpdateFlow` → `SetError` + `UpdateHinweis` „Update konnte nicht vorbereitet werden."). Weder Entpacken noch Skripterzeugung, Prozessstart oder `Shutdown()`. |
| Umgang mit fehlenden Objekten | *`update.json`-Asset fehlt im Release* (`UpdateInfo.ManifestDownloadUrl == null`): **verwerfen** — `InvalidDataException` in der Vorbereitung, kein stiller Fallback. *Manifest-Abruf schlägt fehl* (HTTP-Fehler inkl. einmaligem 5xx-Fallback, Transportfehler, Abbruch): **verwerfen** — gleiche Fehlerkette wie Downloadfehler. *Manifest ist kein gültiges JSON / hat nicht das erwartete Schema / enthält keinen `release.zip`-Eintrag / `sha256` leer oder kein 64-stelliger Hexwert*: **verwerfen** — `InvalidDataException`, kein Best-Effort-Parsing. *Veraltete Releases ohne `update.json`:* bleiben Update-Kandidaten (kein Filtern im Release-Client), scheitern aber zwangsläufig in der Vorbereitung. |

### Scope-Ausschlüsse

| # | Einschränkung / Ausschluss | Begründung | Status |
|---|----------------------------|------------|--------|
| 1 | Keine Authenticode-Signaturprüfung der entpackten `Softwareschmiede.exe`. | Im Issue ausdrücklich als „optional perspektivisch" benannt; separater, größerer Eingriff. | bestätigt (2026-11-14, implizit durch Issue-/requirement.md-Wortlaut) |
| 2 | Keine Signaturprüfung des Manifests selbst — `update.json` wird über denselben TLS-/GitHub-Kanal bezogen wie Release-Liste und ZIP. Die Verifikation schützt vor manipuliertem/korruptem Paket-Download (CDN-/Proxy-Eingriff, nachträglich ausgetauschtes Asset bei unverändertem Manifest), nicht vor einem vollständig kompromittierten GitHub-Release. | Dieses Restrisiko ist in der Anforderung bewusst benannt und nicht Teil des Eingriffs. | bestätigt (2026-11-14, implizit durch Issue-/requirement.md-Wortlaut) |
| 3 | Keine Änderung am Release-Workflow — `.github/actions/build-and-package/action.yml`, `release.yml`, `staging-ci.yml` bleiben unverändert. | `update.json` wird bereits erzeugt und als Release-Asset hochgeladen. | bestätigt (2026-11-14, implizit durch Issue-/requirement.md-Wortlaut) |
| 4 | Keine Verifikation einzelner entpackter Dateien und keine Konsistenzprüfung zwischen Manifest-`version` und `UpdateInfo.Version`. | Die bisherige Root-Dateien-Validierung bleibt; die Versions-Kreuzprüfung bringt keinen Sicherheitsgewinn, weil ein vertauschtes Manifest bereits über den Hash-Mismatch erkannt wird (siehe Designentscheidungen). | bestätigt (2026-11-14, autonome Entscheidung gemäß Vorgabe: „kleiner Eingriff, großer Gewinn") |
| 5 | `sizeBytes` aus dem Manifest wird nicht geprüft — verbindlich ist ausschließlich `sha256`. | Ein Größenmismatch impliziert zwingend einen Hash-Mismatch; die Zusatzprüfung hätte keinen Schutzgewinn, die Anforderung benennt den Hash als Empfehlung. | bestätigt (2026-11-14, autonome Entscheidung gemäß Vorgabe) |
| 6 | Ein Release ohne `update.json`-Asset bleibt ein Update-Angebot und scheitert erst in der Vorbereitung — es wird nicht bereits im Release-Client ausgefiltert. Ältere Releases ohne Manifest sind danach faktisch nicht mehr installierbar. | Issue-nähere Variante („vor dem Entpacken `update.json` abrufen"); harter Abbruch ist die im Issue intendierte Wirkung. Der Verwerf-Punkt bleibt ein einziger, client-unabhängiger Durchsetzungspunkt in der Vorbereitung. | bestätigt (2026-11-14, autonome Entscheidung gemäß Vorgabe) |
| 7 | Keine Laufzeitkonfiguration und kein Opt-out — weder `UpdateOptions`-Schalter noch `AppEinstellungen` noch UI-Option; die Verifikation ist ein verbindlicher Sicherheitsschritt jeder Paketvorbereitung. | Ein Opt-out würde die Schutzwirkung unterlaufen; Anforderung legt die Verifikation als Zwang fest. | bestätigt (2026-11-14, implizit durch Issue-/requirement.md-Wortlaut) |

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Transport der Manifest-Referenz (Offene Frage 4, Teil) | `UpdateInfo` erhält zwei optionale Positional-Parameter am Ende: `Uri? ManifestDownloadUrl = null` und `Uri? ManifestFallbackDownloadUrl = null` — flach, analog `FallbackDownloadUrl` | Folgt exakt dem vorgelebten `FallbackDownloadUrl`-Muster; ein eigenes Manifest-Modell lohnt erst, wenn mehr als zwei URLs transportiert werden. Quellkompatibel: bestehende 6-/7-Argument-Konstruktionen kompilieren unverändert. |
| Manifest-Asset-Selektion im Client (Offene Frage 2) | `TryCreateCandidate` sucht zusätzlich das `_options.ManifestAssetName`-Asset mit absoluter HTTPS-`browser_download_url` und füllt die beiden Manifest-URLs; fehlt/ungültig → beide `null`, Kandidat bleibt bestehen (kein Filtern) | „Verwerfen erst in der Vorbereitung" ist näher am Issue-Wortlaut und hält den Durchsetzungspunkt an einer einzigen Stelle — auch ein `UpdateInfo` aus einer anderen Quelle kann nicht unverifiziert entpackt werden. |
| Name des Manifest-Assets | Neue init-Eigenschaft `UpdateOptions.ManifestAssetName = "update.json"` | Deployment-Konstante analog `AssetName` — passt zum bestehenden Optionsmuster und vermeidet eine streuende Literal-Konstante. Keine Benutzereinstellung. |
| Abrufzeitpunkt des Manifests | Innerhalb `PreparePackageAsync`, direkt nach `File.Move(partialZipPath, zipPath)` + Leer-Prüfung und vor der `Entpacken`-Phase | Entspricht dem Issue-Wortlaut wörtlich; entsteht kein Zusatzrequest bei jeder Startup-Prüfung (die Release-Abfrage liefert die Manifest-URL aus der ohnehin gelesenen Release-JSON); die ZIP liegt beim Hashing bereits final vor. Ein Abruf vor/parallel zum ZIP-Download würde nichts sparen und die Fehlerzuordnung erschweren. |
| Manifest-Fallback (Offene Frage 4) | Derselbe einmalige 5xx-Fallback wie beim ZIP: `ManifestDownloadUrl` mit Standard-GET, bei Status ≥ 500 und vorhandener `ManifestFallbackDownloadUrl` genau ein Zweitversuch über die API-Asset-URL (wiederverwendete `CreateFallbackDownloadUrl`/`SendDownloadRequestAsync`-Mechanik) | Die kleine JSON-Datei unterliegt denselben transienten CDN-Fehlern wie die ZIP; die symmetrische Wiederverwendung kostet nur ein `UpdateInfo`-Feld und eine Bedingung — kein neuer Mechanismus. |
| Fortschrittsphase (Offene Frage 5) | Neuer Enum-Wert `UpdatePreparationPhase.Verifizieren` (zwischen `Download` und `Entpacken`), ein `Report` vor Manifest-Abruf+Hashvergleich mit Meldung „Update-Paket wird verifiziert."; `UpdateProgressViewModel.Apply` mappt auf `PhaseText = "Verifikation"` | Additive, billige Erweiterung; macht den Sicherheitsschritt im Dialog nachvollziehbar und erzeugt automatisch ein `PreparationPhase`-Ereignis (`phase = "Verifizieren"`) im E2E-Protokoll — Beleg dafür, dass der Schritt real läuft. |
| Manifest-Parsing | Private DTOs `UpdateManifest`/`UpdateManifestAsset` (nur `assets`, `assetName`, `sha256`) als geschachtelte Klassen in `UpdatePackageService`; `System.Text.Json` + `JsonPropertyName` wie in `GitHubReleaseClient`; `JsonException`/`null` → `InvalidDataException` | Gleiches Parsing-Muster wie der Release-Client; der DTO enthält nur die tatsächlich gelesenen Felder (keine `version`/`sizeBytes`/`assetUrl` — siehe Ausschluss 4/5). |
| Hash-Berechnung und -Vergleich | Streaming über `FileStream` + `SHA256.HashDataAsync` (in `System.Security.Cryptography`), Hexwert via `Convert.ToHexString`, Vergleich `StringComparison.OrdinalIgnoreCase` gegen den Manifest-Wert | Der Workflow schreibt den Hash per `ToLowerInvariant()`; IgnoreCase deckt Groß-/Kleinschreibung ab. Streaming vermeidet eine zweite Vollkopie der ZIP im Speicher. |
| `sha256`-Plausibilität | Manifest-Eintrag muss `assetName == _options.AssetName` (OrdinalIgnoreCase, konsistent zur Asset-Suche) und einen nicht-leeren `sha256` im 64-Hex-Format tragen, sonst `InvalidDataException` | „Unbrauchbarer Hash → verwerfen" aus der Anforderung; ein formal kaputter Wert wird so als Manifestfehler statt als kryptischer Hash-Mismatch diagnostiziert. |
| Versions-Konsistenz (Offene Frage 6) | Kein Vergleich von Manifest-`version` gegen `UpdateInfo.Version`/`TagName` | Ein vertauschtes/falsches Manifest führt ohnehin zum Hash-Mismatch; eine zusätzliche Gleichheitsregel schafft nur Abbruchfläche bei Formatdrift (Tag-Normalisierung vs. Manifest-Feld) ohne Sicherheitsgewinn. |
| Timeout des Manifest-Abrufs (Offene Frage 7) | Kein separater Timeout — es gilt das durchgereichte `CancellationToken` des Vorbereitungslaufs (Dialog-Abbrechen/Caller), wie beim ZIP-Download | `CheckTimeout` ist bewusst auf die Release-Prüfung beschränkt; ein zweiter Timeout-Mechanismus für einen Kleinst-Request wäre ein Sonderweg ohne Mehrwert. |
| E2E-Klassifizierung der Manifest-URL | `UpdateFixtureHttpMessageHandler.Klassifiziere`: URLs mit Endung `.json` → `"manifest"` (vor dem `sonstige`-Fallback) | Hält `art == "asset"`-Zählasserts (z. B. `AssertiereInstallationsNachweis`, `T09StartupPhaseAsync`) unverändert bei den bisherigen Werten und macht Manifest-Abrufe im Protokoll eindeutig lesbar. |

## Programmabläufe

### Release-Abfrage liefert die Manifest-Referenz

1. `GitHubReleaseClient.GetLatestReleaseAsync` lädt die paginierte Release-Liste wie bisher (unverändert: `CheckTimeout`, Pinning, Kanalfilter).
2. `TryCreateCandidate` führt die bestehenden Filter aus (`draft`, Tag-Parsing, Kanal, `_options.AssetName`-Asset mit `IsAbsoluteHttpsUrl`).
3. **Neu:** In derselben `release.Assets`-Liste wird das Manifest-Asset gesucht: `a.Name == _options.ManifestAssetName` (OrdinalIgnoreCase) mit `IsAbsoluteHttpsUrl(a.BrowserDownloadUrl)`. Treffer → `ManifestDownloadUrl = new Uri(asset.BrowserDownloadUrl)` und `ManifestFallbackDownloadUrl = CreateFallbackDownloadUrl(manifestAsset)` (vorhandene `url`-/ID-Ableitung mit `api.github.com`-Pinning). Kein/unbrauchbarer Treffer → beide `null` (optional `LogWarning`); der Kandidat bleibt nutzbar.
4. `UpdateInfo` wird mit den zwei neuen Argumenten konstruiert; `UpdateReleaseLookupResult`/`UpdateCheckResult` bleiben unverändert.

Beteiligte Klassen/Komponenten: `GitHubReleaseClient`, `UpdateInfo`, `UpdateOptions`

### Update-Vorbereitung mit Manifest-Verifikation (Happy Path)

1. `UpdateService.PrepareUpdateAsync` delegiert unverändert an `IUpdatePackageService.PreparePackageAsync(update, progress, ct)` — der Vertrag bleibt.
2. `PreparePackageAsync` legt Arbeitsverzeichnisse an und bereinigt Altartefakte (unverändert).
3. `progress.Report(UpdatePreparationPhase.Download, …)` → `DownloadAsync(update, partialZipPath, …)` (ZIP-Download mit bestehendem 5xx-Fallback, unverändert) → `File.Move` auf `release.zip` → Leer-Prüfung (unverändert).
4. **Neu:** `progress.Report(new UpdatePreparationProgress(UpdatePreparationPhase.Verifizieren, null, "Update-Paket wird verifiziert."))`.
5. **Neu:** `DownloadManifestAsync(update, ct)`: `update.ManifestDownloadUrl is null` → `InvalidDataException` („Update-Manifest nicht verfügbar"); sonst `SendDownloadRequestAsync(ManifestDownloadUrl, useApiHeaders: false)`; bei Status ≥ 500 und `ManifestFallbackDownloadUrl is not null` genau ein Zweitversuch mit `useApiHeaders: true`; `EnsureSuccessStatusCode`; JSON-Inhalt lesen.
6. **Neu:** `GetExpectedSha256(manifestJson)`: `JsonSerializer.Deserialize<UpdateManifest>`; `JsonException`, `null`-Ergebnis, fehlendes/leeres `assets` oder kein Eintrag mit `assetName == _options.AssetName` bzw. leerem/nicht-64-Hex-`sha256` → `InvalidDataException` mit jeweils aussagekräftiger Meldung.
7. **Neu:** `VerifyPackageHashAsync(zipPath, expectedSha256, ct)`: `FileStream` auf `zipPath` → `SHA256.HashDataAsync` → `Convert.ToHexString` → `OrdinalIgnoreCase`-Vergleich; Mismatch → `InvalidDataException`.
8. `progress.Report(UpdatePreparationPhase.Entpacken, …)` → `ZipFile.ExtractToDirectory` → `ValidateExtractedPackage` → `progress.Report(UpdatePreparationPhase.UpdateVorbereiten, …)` → `CreateScriptAsync` → `UpdatePreparationResult` (alles unverändert).

Beteiligte Klassen/Komponenten: `UpdatePackageService`, `UpdateInfo`, `UpdateOptions`, `UpdatePreparationPhase`, `IUpdateScriptService`

### Kontrollierter Abbruch bei nicht verifizierbarem oder manipuliertem Paket

1. Jede Exception aus den Schritten 5–7 (`InvalidDataException` bei fehlendem/ungültigem Manifest oder Hash-Mismatch; `HttpRequestException` bei endgültigem Manifest-HTTP-Fehler; `OperationCanceledException` bei Abbruch) läuft in den bestehenden Catch-all von `PreparePackageAsync`.
2. Der Catch-all löscht `*.download`, `release.zip` und das Extraktionsverzeichnis und rethrowed — unverändert.
3. Propagation: `UpdateService.PrepareUpdateAsync` → `MainWindowUpdateFlow.InstalliereUpdateAsync` → `catch (Exception)` → `SetError("Update konnte nicht vorbereitet werden. Details wurden protokolliert.")` + `UpdateHinweis` (bei `OperationCanceledException` der bestehende Abbruchpfad).
4. Es erfolgt weder Entpacken noch Skripterzeugung, `IUpdateProcessLauncher.Start` oder `Shutdown()`.

Beteiligte Klassen/Komponenten: `UpdatePackageService`, `UpdateService`, `MainWindowUpdateFlow`, `UpdateProgressViewModel`

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `UpdateManifest` | Private geschachtelte DTO-Klasse in `UpdatePackageService` | Deserialisierungsziel des `update.json`-Schemas; einziges Feld `assets` (`List<UpdateManifestAsset>`) — nur tatsächlich gelesene Felder. |
| `UpdateManifestAsset` | Private geschachtelte DTO-Klasse in `UpdatePackageService` | Ein Manifest-Asset-Eintrag: `assetName`, `sha256` (`JsonPropertyName`-Mapping). |

## Änderungen an bestehenden Klassen

### `UpdateInfo` (Positional Record, `src/Softwareschmiede/Application/Services/Updates/UpdateModels.cs`)

- **Neue Parameter:** `ManifestDownloadUrl` (`Uri?`, Standard `null`) — Browser-Download-URL des `update.json`-Assets; `ManifestFallbackDownloadUrl` (`Uri?`, Standard `null`) — GitHub-API-Asset-URL für den einmaligen 5xx-Fallback. Beide nach `FallbackDownloadUrl` angehängt (quellkompatibel).

### `UpdatePreparationPhase` (Enum, `src/Softwareschmiede/Application/Services/Updates/UpdateModels.cs`)

- **Neuer Wert:** `Verifizieren` — das heruntergeladene Paket wird gegen das Update-Manifest verifiziert; logisch zwischen `Download` und `Entpacken` einsortiert.

### `UpdateOptions` (Klasse, `src/Softwareschmiede/Application/Services/Updates/UpdateOptions.cs`)

- **Neue Eigenschaft:** `ManifestAssetName` (`string`, init, Standard `"update.json"`) — Name des erwarteten Manifest-Assets, analog `AssetName`.

### `GitHubReleaseClient` (Klasse, `src/Softwareschmiede/Infrastructure/Services/Updates/GitHubReleaseClient.cs`)

- **Geänderte Methoden:** `TryCreateCandidate` — zusätzliche Asset-Auswahl `name == _options.ManifestAssetName` (OrdinalIgnoreCase) + `IsAbsoluteHttpsUrl(BrowserDownloadUrl)`; befüllt `ManifestDownloadUrl`/`ManifestFallbackDownloadUrl` (Letztere über die vorhandene `CreateFallbackDownloadUrl`-Hilfe) beim `UpdateInfo`-Aufruf. Fehlt das Asset oder die URL ist ungültig: beide `null`, kein Ausschluss des Kandidaten (optional `LogWarning`).
- **Unverändert:** Release-Listen-Abruf, Pinning, Pagination, Kanalfilter — der Manifest-Bezug entsteht aus der bereits geladenen Release-JSON ohne Zusatzrequest.

### `UpdatePackageService` (Klasse, `src/Softwareschmiede/Infrastructure/Services/Updates/UpdatePackageService.cs`)

- **Geänderte Methoden:** `PreparePackageAsync` — nach `File.Move` + Leer-Prüfung und vor dem `Entpacken`-Report: `Verifizieren`-Report + Aufruf der Verifikationsschritte. Catch-all unverändert (deckt die neuen Fehlschläge ab).
- **Neue Methoden:**
  - `DownloadManifestAsync(UpdateInfo update, CancellationToken ct) → Task<string>` — wirft `InvalidDataException` bei `ManifestDownloadUrl == null`; GET via `SendDownloadRequestAsync` (primär ohne, Fallback mit API-Headern); einmaliger 5xx-Fallback auf `ManifestFallbackDownloadUrl`; `EnsureSuccessStatusCode`; liefert den JSON-Text.
  - `GetExpectedSha256(string manifestJson) → string` — deserialisiert `UpdateManifest`; wirft `InvalidDataException` bei `JsonException`/`null`/fehlendem `release.zip`-Eintrag/leerem oder nicht-64-Hex-`sha256`.
  - `VerifyPackageHashAsync(string zipPath, string expectedSha256, CancellationToken ct) → Task` — streaming-SHA-256 der ZIP (`SHA256.HashDataAsync` + `Convert.ToHexString`); wirft `InvalidDataException` bei `OrdinalIgnoreCase`-Mismatch.
- **Neue geschachtelte Typen:** `UpdateManifest`, `UpdateManifestAsset` (siehe Neue Klassen).
- **Neue Imports:** `System.Security.Cryptography`, `System.Text.Json`, `System.Text.Json.Serialization`.

### `UpdateProgressViewModel` (Klasse, `src/Softwareschmiede.App/ViewModels/UpdateProgressViewModel.cs`)

- **Geänderte Methoden:** `Apply` — `switch`-Zweig `UpdatePreparationPhase.Verifizieren => "Verifikation"` ergänzen (ohne Zweig würde der Fallback „Vorbereitung" greifen).

### `UpdateFixtureHttpMessageHandler` (Klasse, `src/Softwareschmiede.App/Services/Testing/UpdateFixtureHttpMessageHandler.cs`)

- **Geänderte Methoden:** `Klassifiziere` — zusätzlicher Zweig: URLs mit Endung `.json` (OrdinalIgnoreCase) → `"manifest"`; vor dem `sonstige`-Fallback, nach den `/releases`-Regeln (die API-URL enthält `/releases` und bleibt `"release"` bzw. `"asset"`).

### `UpdateE2EFixture` (Klasse, `src/Softwareschmiede.Tests/E2E/UpdateE2EFixture.cs`)

- **Neue Konstanten/Properties:** `StableManifestAssetId = 91011`, `RcManifestAssetId = 91012`; `StableManifestAssetApiUrl`/`RcManifestAssetApiUrl` (via `AssetApiUrl`); `StableManifestDownloadUrl`/`RcManifestDownloadUrl` (`https://e2e.invalid/{SzenarioId}/update-{version}.json`).
- **Neue Methoden:**
  - `ErstelleManifestAssetJson(apiUrl, assetId, downloadUrl) → object` — Asset-Objekt wie `ErstelleAssetJson`, aber `name = StandardOptionen.ManifestAssetName`.
  - `ErzeugeManifestJson(version, downloadUrl, zipPfad) → string` — Workflow-Schema (`version`, `releaseNotes`, `publishedAt`, `assets[1]` mit `platform`, `runtimeIdentifier`, `assetName`, `assetUrl`, `sha256` = `Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(zipPfad))).ToLowerInvariant()`, `sizeBytes` = Dateilänge). Wird von `SetzeStandardAntworten` und von Szenario-Mutationen (z. B. `DefektesZipPhaseAsync`) verwendet.
- **Geänderte Methoden:**
  - `ErstelleReleaseJson` — optionaler Parameter `manifestAssetJson = null`; `assets` enthält dann zusätzlich das Manifest-Asset (quellkompatibel für Hilfsaufrufe ohne Manifest).
  - `ErzeugeReleaseListenJson` — übergibt je Release das passende `ErstelleManifestAssetJson`.
  - `SetzeStandardAntworten` — zusätzliche `UpdateE2EAntwort`-Einträge: `StableManifestDownloadUrl`/`RcManifestDownloadUrl` → `Status 200`, `Inhalt = ErzeugeManifestJson(…)`.

### `End2EndTest` (E2E-Szenarien, `src/Softwareschmiede.Tests/E2E/E2E_UpdateFixture.cs`, `src/Softwareschmiede.Tests/E2E/E2E_UpdateSettings.cs`)

- **Geändert:** `DefektesZipPhaseAsync` — beim Tausch der ausgelieferten Datei auf `defekt.zip` wird auch die Manifest-Antwort auf `ErzeugeManifestJson(StableVersion, fixture.StableDownloadUrl, defektesPaket)` aktualisiert, damit die Phase weiterhin den ZIP-Parse-Fehler (nicht den Hash-Mismatch) prüft.
- **Geändert:** `ErzeugeRcUndHoeheresStableReleaseJson` — RC-Release erhält das Manifest-Asset (`RcManifestAssetApiUrl`/`RcManifestAssetId`/`RcManifestDownloadUrl`); das `9.9.9`-Stable-Release bleibt ohne Manifest-Asset (wird nie heruntergeladen — legitimer Testfall „Kandidat ohne Manifest").
- **Neu:** `ManipuliertesPaketPhaseAsync` — siehe Tests/E2E.
- **Geändert:** `Fixture_UsesIsolatedRealUpdatePipeline` — Assertions auf das `PreparationPhase`-Ereignis `phase == "Verifizieren"` und den `HttpRequest` mit `art == "manifest"` ergänzen; Methoden-Dokumentation anpassen.

## Datenbankmigrationen

Keine.

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| `UpdateInfo.ManifestDownloadUrl` | Muss gesetzt sein (`!= null`); die URL stammt ausschließlich aus einer HTTPS-`browser_download_url` des Manifest-Assets. | `InvalidDataException` — Paket nicht verifizierbar, Abbruch vor Entpacken. |
| Manifest-HTTP-Abruf | Erfolgsstatus erforderlich; bei ≥ 500 genau ein Zweitversuch über `ManifestFallbackDownloadUrl` (falls vorhanden). | `HttpRequestException` via `EnsureSuccessStatusCode` — gleiche Fehlerkette wie Downloadfehler. |
| Manifest-JSON | Muss zu `UpdateManifest` deserialisierbar sein (Objekt mit `assets`-Array). | `JsonException`/`null` → `InvalidDataException` — kein Best-Effort-Parsing. |
| Manifest-Asset-Eintrag | Genau ein Eintrag mit `assetName == _options.AssetName` (OrdinalIgnoreCase) erforderlich. | `InvalidDataException` — Manifest enthält keinen verwertbaren Paket-Eintrag. |
| `assets[].sha256` | Nicht-leerer 64-stelliger Hexwert erforderlich. | `InvalidDataException` — unbrauchbarer Hash gilt als nicht verifizierbar. |
| SHA-256 der `release.zip` | Berechneter Hash == Manifest-`sha256` (OrdinalIgnoreCase). | `InvalidDataException` — Abbruch vor dem ersten Schreibzugriff des Entpackens. |

## Konfigurationsänderungen

| Eintrag | Typ | Standardwert | Zweck |
|---------|-----|--------------|-------|
| `UpdateOptions.ManifestAssetName` | `string` (init) | `"update.json"` | Deployment-Konstante für den Namen des Manifest-Assets (analog `AssetName`); technische Festlegung, keine Benutzereinstellung. |

Keine Laufzeitkonfiguration, kein Opt-out, keine `appsettings`-/`AppEinstellungen`-Änderung; kein separater Timeout — es gilt das Aufrufer-`CancellationToken`.

## Seiteneffekte und Risiken

- **Releases ohne `update.json`-Asset:** Werden weiterhin als Update angeboten, scheitern aber zwangsläufig in der Vorbereitung mit „Update konnte nicht vorbereitet werden." — bewusste Wirkung (harter Abbruch statt ungeprüfter Installation). Betroffen wären nur Releases, die vor Einführung des `build-and-package`-Manifest-Schritts erzeugt wurden.
- **`UpdateInfo` (Positional Record):** Zwei neue optionale End-Parameter — quellkompatibel für alle bestehenden Konstruktionen (6/7 Argumente); keine Signaturänderung an `IUpdatePackageService`/`IUpdateReleaseClient`/`IUpdateService`.
- **Startup-Prüfung:** Kein zusätzlicher HTTP-Request in `CheckForUpdateAsync` — die Manifest-URL wird aus der ohnehin geladenen Release-JSON gelesen. Verhalten/Performanz der Prüfung unverändert.
- **Update-E2E-Pipeline:** Ein zusätzlicher Fixture-Request (`art == "manifest"`) und ein zusätzliches `PreparationPhase`-Ereignis pro Vorbereitungslauf; `art == "asset"`-Zählasserts bleiben durch die neue Klassifizierung stabil. Ohne Fixture-Erweiterung würden alle Update-E2E-Tests am `UnknownHttpRequest` scheitern — die Erweiterung ist Pflichtbestandteil.
- **`UpdatePackageServiceTests`-Infrastruktur:** `BytesHttpHandler` (eine Antwort für alle Requests) kann kein Manifest liefern — `CreateSut` muss auf `RoutingHttpHandler` mit ZIP- und Manifest-Route umgestellt werden; bestehende Tests bleiben fachlich unverändert gültig.
- **`DefektesZipPhaseAsync`:** Ohne Manifest-Neuberechnung würde die Phase künftig den Hash-Mismatch statt des ZIP-Parse-Fehlers prüfen — die Manifest-Antwort wird mitberechnet (siehe Änderungen).
- **Fortschrittsdialog:** Ohne `Apply`-Erweiterung würde die `Verifizieren`-Phase als generisches „Vorbereitung" angezeigt — Anzeigetext wird ergänzt.

## Umsetzungsreihenfolge

1. **`UpdateInfo` und `UpdatePreparationPhase` erweitern** (`UpdateModels.cs`)
   - Voraussetzungen: Keine.
   - Beschreibung: Zwei optionale Positional-Parameter `ManifestDownloadUrl`/`ManifestFallbackDownloadUrl` (inkl. XML-Doku) anhängen; Enum-Wert `Verifizieren` ergänzen.
2. **`UpdateOptions.ManifestAssetName` ergänzen**
   - Voraussetzungen: Keine.
   - Beschreibung: init-Eigenschaft mit Standard `"update.json"` + XML-Doku.
3. **`GitHubReleaseClient.TryCreateCandidate` erweitern**
   - Voraussetzungen: Schritte 1–2 (neue `UpdateInfo`-Parameter, `ManifestAssetName`).
   - Beschreibung: Manifest-Asset-Auswahl, `ManifestDownloadUrl`/`ManifestFallbackDownloadUrl` (via `CreateFallbackDownloadUrl`) im `UpdateInfo`-Aufruf befüllen.
4. **`UpdatePackageService` um Manifest-Abruf und Hash-Verifikation erweitern**
   - Voraussetzungen: Schritte 1–2.
   - Beschreibung: `Verifizieren`-Report einbauen; `DownloadManifestAsync`, `GetExpectedSha256`, `VerifyPackageHashAsync` sowie DTOs `UpdateManifest`/`UpdateManifestAsset` implementieren; Imports ergänzen.
5. **`UpdateProgressViewModel.Apply` erweitern**
   - Voraussetzungen: Schritt 1.
   - Beschreibung: `Verifizieren`-Zweig mit `PhaseText = "Verifikation"`.
6. **Unit-Test-Infrastruktur anpassen**
   - Voraussetzungen: Schritte 1–4 (sonst schlagen alle `CreateSut`-basierten Tests am fehlenden Manifest fehl).
   - Beschreibung: `UpdatePackageServiceTests`: `CreateSut` auf `RoutingHttpHandler` mit ZIP- + Manifest-Route umstellen (Manifest-Hash aus den servierten ZIP-Bytes berechnen), `CreateUpdateInfo` um `manifestDownloadUrl`/`manifestFallbackDownloadUrl` erweitern, Manifest-URL-Konstante hinzufügen. `GitHubReleaseClientTests`: Release-JSON-Helfer um `update.json`-Asset erweitern.
7. **E2E-Infrastruktur erweitern**
   - Voraussetzungen: Schritte 1–4 (Fixture muss die produktive URL-Auswahl bedienen).
   - Beschreibung: `Klassifiziere` (`"manifest"`), `UpdateE2EFixture` (Konstanten, `ErstelleManifestAssetJson`, `ErzeugeManifestJson`, `ErstelleReleaseJson`-Parameter, `SetzeStandardAntworten`, `ErzeugeReleaseListenJson`), `E2E_UpdateSettings`-Helfer (`ErzeugeRcUndHoeheresStableReleaseJson` mit RC-Manifest-Asset).
8. **Neue Unit-Tests schreiben**
   - Voraussetzungen: Schritte 4, 6.
   - Beschreibung: Verifikationsfälle in `UpdatePackageServiceTests` (Hash-Match/Mismatch, fehlende/fehlerhafte/unbekannte Manifest-URL, ungültiges Manifest, Fallback), Manifest-URL-Übernahme in `GitHubReleaseClientTests`, `Verifizieren`-Anzeige in `UpdateProgressViewModelTests`, Manifest-Klassifizierung in `UpdateFixtureHttpMessageHandlerTests` (siehe Tests).
9. **E2E-Anpassungen und -Szenario**
   - Voraussetzungen: Schritte 7–8.
   - Beschreibung: `DefektesZipPhaseAsync` (Manifest-Neuberechnung), `ManipuliertesPaketPhaseAsync` (neu, in `Startup_SafetyCancelAndErrorsRemainUsable` eingehängt), `Fixture_UsesIsolatedRealUpdatePipeline` (Manifest-/Phase-Assertions).
10. **Verifikation**
    - Voraussetzungen: Schritte 1–9.
    - Beschreibung: `dotnet format Softwareschmiede.slnx --verify-no-changes` (bei Befund: ohne Flag fixen), `dotnet build Softwareschmiede.slnx`, `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category!=OsInterface"`, Integration-Tests-Lane `Category!=OsInterface` sowie Update-E2E-Subset `--filter "FullyQualifiedName~End2EndTest.General_Update"` — alle Läufe synchron im Vordergrund (nie im Hintergrund), `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` bei jedem Lauf inline gesetzt.

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `PreparePackageAsync_ShouldVerifyAndExtract_WhenManifestHashMatches` | `UpdatePackageServiceTests` | Happy Path: Manifest wird nach dem ZIP-Download abgerufen (`RequestedUrls`-Reihenfolge: ZIP → Manifest), `Verifizieren`-Phase reportet, Paket entpackt + Skript erzeugt. |
| `PreparePackageAsync_ShouldThrowInvalidDataAndClean_WhenHashMismatches` | `UpdatePackageServiceTests` | Manifest mit abweichendem `sha256` → `InvalidDataException`; `release.zip`/`*.download`/`extracted/{Version}` aufgeräumt; Extraktionsverzeichnis enthält keine Dateien (kein Entpacken erfolgt). |
| `PreparePackageAsync_ShouldThrow_WhenManifestUrlIsMissing` | `UpdatePackageServiceTests` | `ManifestDownloadUrl = null` → `InvalidDataException`; kein Extraktionsversuch. |
| `PreparePackageAsync_ShouldThrow_WhenManifestDownloadFails` | `UpdatePackageServiceTests` | Manifest-URL liefert 4xx/5xx ohne Fallback → `HttpRequestException`, Aufräumen. |
| `PreparePackageAsync_ShouldUseManifestFallback_WhenManifestReturns5xx` | `UpdatePackageServiceTests` | Manifest-Primär-URL 5xx → genau ein Request auf `ManifestFallbackDownloadUrl` mit `Accept: application/octet-stream`; Verifikation und Vorbereitung laufen danach erfolgreich. |
| `PreparePackageAsync_ShouldThrowInvalidData_WhenManifestIsUnusable` (Theory/MemberData) | `UpdatePackageServiceTests` | Varianten: kein JSON, JSON ohne `assets`, `assets` ohne `release.zip`-Eintrag, leerer `sha256`, nicht-64-Hex-`sha256` → je `InvalidDataException`, Aufräumen. |
| `GetLatestReleaseAsync_ShouldReturnManifestUrls_WhenManifestAssetExists` | `GitHubReleaseClientTests` | Release-JSON mit `update.json`-Asset → `ManifestDownloadUrl` = `browser_download_url`, `ManifestFallbackDownloadUrl` aus `url`/`id` abgeleitet. |
| `GetLatestReleaseAsync_ShouldKeepCandidate_WhenManifestAssetIsMissingOrInvalid` (Theory) | `GitHubReleaseClientTests` | Release ohne `update.json` bzw. mit ungültiger Manifest-`browser_download_url` → Kandidat bleibt `Gefunden`, `ManifestDownloadUrl`/`ManifestFallbackDownloadUrl` sind `null` (dokumentiert das Nicht-Filtern). |
| `Apply_ShouldShowVerifikation_WhenPhaseIsVerifizieren` | `UpdateProgressViewModelTests` | `UpdatePreparationPhase.Verifizieren` → `PhaseText == "Verifikation"`, `IsIndeterminate`. |
| `SendAsync_KlassifiziertManifestUrlAlsManifest` | `UpdateFixtureHttpMessageHandlerTests` | `*.json`-URL → `art == "manifest"` im `HttpRequest`-Ereignis. |
| `ErzeugeManifestJson` / `ErstelleManifestAssetJson` (Hilfsmethoden) | `UpdateE2EFixture` | Manifest im Workflow-Schema mit tatsächlichem SHA-256/`sizeBytes` der Fixture-ZIPs; Manifest-Asset-Eintrag für die simulierte Release-Liste. |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| Alle `CreateSut`-basierten Fälle in `UpdatePackageServiceTests` | `CreateSut`/`BytesHttpHandler` liefern jedem Request dieselben Bytes — der neue Pflicht-Manifestabruf bräche sofort. Umstellung auf `RoutingHttpHandler` mit ZIP-Route (bisherige Bytes/Status) + Manifest-Route (korrekter Hash); `CreateUpdateInfo` befüllt `ManifestDownloadUrl` standardmäßig. |
| `PreparePackageAsync_ShouldDownloadExtractAndCreateScript_WhenZipIsValid` | Zusätzliche Asserts: `Verifizieren`-Phase im Report, Manifest-Request nach dem ZIP-Request. |
| `GetLatestReleaseAsync_ShouldReturnRelease_WhenAssetExists` (`GitHubReleaseClientTests`) | Release-JSON um `update.json`-Asset erweitern; `ManifestDownloadUrl`/`ManifestFallbackDownloadUrl`-Asserts ergänzen. |
| `DefektesZipPhaseAsync` (`E2E_UpdateSettings.cs`) | Manifest-Antwort muss den Hash der servierten `defekt.zip` tragen, damit weiterhin der ZIP-Parse-Fehler (nicht der Hash-Mismatch) geprüft wird. |
| `ErzeugeRcUndHoeheresStableReleaseJson` (`E2E_UpdateSettings.cs`) | RC-Release braucht das Manifest-Asset, sonst scheitert die RC-Installation in `UpdatePfad_SelectsExclusiveChannelAsset`. |
| `Fixture_UsesIsolatedRealUpdatePipeline` (`E2E_UpdateFixture.cs`) | Assertions auf `PreparationPhase` mit `phase == "Verifizieren"` und `HttpRequest` mit `art == "manifest"` ergänzen. |

### E2E-Tests (primärer Funktionsnachweis)

Die Anforderung ändert den realen Update-Benutzerfluss (neuer Pflicht-Schritt zwischen Download und Entpacken, sichtbar als neue Phase im Fortschrittsdialog und als Abbruchfehler). Gemäß Konsolidierungsregel werden **keine** neuen Testmethoden mit eigenem App-Start ergänzt: Die Nachweise hängen in den bestehenden konsolidierten Läufen `General_UpdateFixtureSmoke_E2E` und `General_UpdateStartvarianten_E2E`.

| Priorität | Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium | Warum E2E nötig ist |
|-----------|----------|------------------------|-------------------------------|-------------------|
| Pflicht | Happy Path: Vollständige Update-Installation mit echtem Manifest-Abruf und Verifizieren-Phase | `E2E_UpdateFixture.cs` → `Fixture_UsesIsolatedRealUpdatePipeline` (läuft in `End2EndTest.General_UpdateFixtureSmoke_E2E`) | Verifikation läuft real (HTTP-Request `art == "manifest"`, `PreparationPhase phase == "Verifizieren"`), Installation bis Launcher-/Shutdown-Grenze | Nur der E2E-Lauf verbindet die echte DI-Verkabelung `GitHubReleaseClient` → `UpdateInfo` → `UpdatePackageService` → Fortschrittsdialog über die Fixture-HTTP-Grenze. |
| Pflicht | Manipuliertes Paket (Hash-Abweichung) → kontrollierter Abbruch | `E2E_UpdateSettings.cs` → neue `ManipuliertesPaketPhaseAsync`, eingehängt in `Startup_SafetyCancelAndErrorsRemainUsable` (`End2EndTest.General_UpdateStartvarianten_E2E`) | Hash-Mismatch → `PreparationFailed`, Fehlerdialog „konnte nicht vorbereitet", kein `UpdateStartAttempt`/`UpdateProcessStartRecorded`/`ShutdownRequested`, App bleibt benutzbar | Der Abbruch ist ein anwendersichtbarer Fehlerfluss (Dialog + Hinweis + kein Stillstand); Unit-Tests decken die Exception, nicht den UI-/Systemweg. |

Welche bestehenden E2E-Tests müssen angepasst werden?

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `Fixture_UsesIsolatedRealUpdatePipeline` (`E2E_UpdateFixture.cs`) | Manifest-Abruf- und `Verifizieren`-Phase-Assertions ergänzen. |
| `DefektesZipPhaseAsync` (`E2E_UpdateSettings.cs`) | Manifest-Antwort auf die servierte `defekt.zip` neu berechnen (siehe oben). |
| `ErzeugeRcUndHoeheresStableReleaseJson` (`E2E_UpdateSettings.cs`) | RC-Release um Manifest-Asset ergänzen. |
| `UpdatePfad_SelectsExclusiveChannelAsset`, `Startup_ModesDriveUpdatePipeline`, `AssetFallbackErfolgPhaseAsync`, T09-Phasen u. a. (`E2E_UpdateSettings.cs`) | Keine Code-Anpassung nötig — laufen durch die Fixture-Erweiterung (`SetzeStandardAntworten` liefert Manifest-Antworten); `art == "asset"`-Zählasserts bleiben durch die `"manifest"`-Klassifizierung stabil. Zu verifizieren, nicht zu ändern. |

## Akzeptanzszenarien

| # | Szenario | Auslöser | Ausgangszustand | Schritte | Sichtbare Wirkung | Nachweis |
|---|----------|----------|------------------|----------|-------------------|----------|
| 1 | Happy Path: Update wird verifiziert und installiert | Startautomatik (`UpdateMode.BeiProgrammstartPruefenUndAusfuehren`) oder Nutzer klickt „Update installieren" | Neueres Release mit `release.zip`- und `update.json`-Asset; `update.json` enthält den korrekten SHA-256 der ZIP | `GetLatestReleaseAsync` liefert `UpdateInfo` mit Manifest-URLs → `PreparePackageAsync`: Download → `Verifizieren` (Manifest-GET, Hash-Match) → Entpacken → Skript → Updater-Start → `Shutdown()` | Fortschrittsdialog zeigt Phase „Verifikation"; Update läuft durch wie bisher | E2E `General_UpdateFixtureSmoke_E2E` (Manifest-Request + Phase + Installationsnachweis); Unit `...ShouldVerifyAndExtract_WhenManifestHashMatches` |
| 2 | Manipuliertes/korruptes Paket: Hash-Mismatch | Wie #1 | Release-Liste und Manifest intakt, aber die ausgelieferte ZIP weicht vom Manifest-`sha256` ab (z. B. CDN-Manipulation) | Download → `Verifizieren` → Hash-Mismatch → `InvalidDataException` → Aufräumen → `SetError` | Fortschrittsdialog: „Update konnte nicht vorbereitet werden."; kein Entpacken, kein Skript, kein Prozessstart, kein `Shutdown()`; ZIP-Artefakte gelöscht | E2E `ManipuliertesPaketPhaseAsync` in `General_UpdateStartvarianten_E2E`; Unit `...ShouldThrowInvalidDataAndClean_WhenHashMismatches` |
| 3 | Manifest-Asset fehlt im Release | Wie #1 | Release enthält nur `release.zip` (z. B. Alt-Release vor Manifest-Einführung); `ManifestDownloadUrl == null` | Download → `Verifizieren` → `InvalidDataException` (kein Manifest) → Aufräumen → `SetError` | Wie #2: Abbruch statt ungeprüfter Installation | Unit `...ShouldThrow_WhenManifestUrlIsMissing` + `GetLatestReleaseAsync_ShouldKeepCandidate_WhenManifestAssetIsMissingOrInvalid` |
| 4 | Manifest-Abruf vorübergehend gestört (5xx) | Wie #1 | Primäre Manifest-URL liefert 5xx; `ManifestFallbackDownloadUrl` gesetzt | Download → `Verifizieren` → Manifest-GET 5xx → einmaliger API-Fallback → Manifest gelesen → Hash-Match → Installation | Update läuft trotz gestörtem Primärabruf durch | Unit `...ShouldUseManifestFallback_WhenManifestReturns5xx` |
| 5 | Manifest ungültig oder ohne verwertbaren Eintrag | Wie #1 | `update.json` ist kein JSON / ohne `assets` / ohne `release.zip`-Eintrag / mit leerem oder formal ungültigem `sha256` | Download → `Verifizieren` → `InvalidDataException` → Aufräumen → `SetError` | Wie #2: Abbruch; Fehlerdetails im Update-Log | Unit `...ShouldThrowInvalidData_WhenManifestIsUnusable` |
| 6 | Abbruch durch Nutzer während der Verifikation | Nutzer klickt „Abbrechen" im Fortschrittsdialog | Verifikationsphase läuft (Manifest-GET oder Hashing) | `CancellationToken` greift → `OperationCanceledException` → Aufräumen → Abbruchhinweis | Dialog zeigt Abbruchzustand; keine Nebenwirkungen | Bestehender Abbruchmechanismus (gleiche Token-Strecke wie Download); indirekt durch E2E-Abbruchphase mitgedeckt |

## Offene Punkte

Keine — alle sieben offenen Fragen der `requirement.md` wurden fachlich entschieden und sind als Designentscheidungen bzw. Scope-Ausschlüsse eingearbeitet: (1) harter Abbruch bei fehlendem/nicht verifizierbarem Manifest, (2) kein Filtern im Release-Client — Verwerfen erst in der Vorbereitung, (3) `sizeBytes` wird nicht geprüft, (4) einmaliger 5xx-Fallback auch für das Manifest, (5) eigene Phase `Verifizieren` im Fortschrittsdialog, (6) keine Versions-Konsistenzprüfung Manifest ↔ Release, (7) kein separater Timeout — das Aufrufer-`CancellationToken` gilt.
