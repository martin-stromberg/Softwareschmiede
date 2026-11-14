# Übersetzte Anforderung: SHA-256-Verifikation des Update-Pakets nachrüsten

**Quelle:** Issue #378 / Aufgaben-ID `ab547856-7642-458b-b6d2-bc0285bb1cfa` (Prio-1-Befund aus dem Architektur- und Sicherheitsreview 2026-11, Issue #374)
**Branch:** `task/issue-378-ab5478567642458bb6d2bc0285bb1cfa-sha-256-verifikation-des-updat`

## Fachliche Zusammenfassung

Der Release-Workflow erzeugt neben `release.zip` bereits das Update-Manifest `update.json` als separates Release-Asset (Rezepte-Format: `version`, `publishedAt`, `assets[]` mit `assetName`, `assetUrl`, `sha256`, `sizeBytes` je Asset — siehe `.github/actions/build-and-package/action.yml` und die Uploads in `.github/workflows/release.yml` / `staging-ci.yml`). Der `UpdatePackageService` validiert das heruntergeladene Paket aktuell nur auf „`Softwareschmiede.exe` + `version.json` im Paket-Root vorhanden" — Hash und Größe bleiben ungeprüft, der Vertrauensanker ist nur TLS + GitHub-Account-Integrität. Die Anforderung ergänzt die Paketvorbereitung um eine Integritätsverifikation: Vor dem Entpacken wird `update.json` abgerufen, der erwartete SHA-256 des `release.zip` gelesen und gegen den Hash des heruntergeladenen Pakets verglichen; bei Abweichung oder nicht verifizierbarem Manifest bricht die Update-Vorbereitung kontrolliert ab.

## Fachlicher Scope

- **Verarbeitungseinheit:** Die *Integritätsverifikation eines heruntergeladenen Update-Pakets* gegen das Update-Manifest innerhalb eines Update-Vorbereitungslaufs (`UpdatePackageService.PreparePackageAsync`). Zum Scope gehört der Manifest-Abruf, das Auslesen des erwarteten Hashs (und ggf. der Größe) für genau das `release.zip`-Asset sowie der Hashvergleich vor dem Entpacken — nicht die Release-Auswahl, nicht der Updater-Start, nicht der Inhalt des entpackten Pakets.
- **Auslösende Ereignisse:**
  - Jeder Update-Vorbereitungslauf auf dem gemeinsamen Installationspfad (`UpdateService.PrepareUpdateAsync` → `PreparePackageAsync`), unabhängig davon, ob die Installation per Startautomatik (`UpdateMode.BeiProgrammstartPruefenUndAusfuehren`) oder manuell über `UpdateStartenCommand` ausgelöst wurde.
  - Konkret greift die Verifikation nach erfolgreichem Download der `release.zip` (einschließlich eines etwaigen 5xx-Fallback-Downloads) und **vor** `ZipFile.ExtractToDirectory` — ein manipuliertes oder korruptes Paket darf niemals entpackt werden.
- **Erwartete Wirkung:**
  - Stimmt der SHA-256 der heruntergeladenen `release.zip` mit dem aus `update.json` gelesenen Erwartungswert überein, läuft die Vorbereitung unverändert weiter (Entpacken, Paketvalidierung, Skripterzeugung).
  - Bei Hash-Abweichung — Manipulation oder Beschädigung des Downloads — wird die Vorbereitung mit einem kontrollierten Fehler abgebrochen (Fehlerpropagation wie bei den bisherigen Paketfehlern: `InvalidDataException` → Catch-all in `PreparePackageAsync` räumt ZIP/Teildateien/Extraktionsverzeichnis auf → `MainWindowUpdateFlow` → `SetError` im Fortschrittsdialog + `UpdateHinweis` „Update konnte nicht vorbereitet werden."). Es erfolgt weder Entpacken noch Skripterzeugung, Prozessstart oder `Shutdown()`.
  - Nicht verifizierbar heißt nicht installierbar: Fehlt das Manifest, ist es nicht abrufbar, ungültig oder enthält es keinen verwertbaren Hash für das Asset, wird das Update ebenfalls verworfen statt ungeprüft installiert (Annahme — siehe Offene Fragen).
- **Umgang mit fehlenden Objekten:**
  - *`update.json`-Asset fehlt im Release:* **verwerfen** — das Paket gilt als nicht verifizierbar; es erfolgt kein stiller Fallback auf „ohne Prüfung installieren" (Annahme; ein solcher Fallback würde die Schutzwirkung durch einfaches Weglassen des Assets unterlaufen).
  - *Manifest-Abruf schlägt fehl* (HTTP-Fehler, Timeout, Transportfehler): **verwerfen** — gleiche Fehlerkette wie Download-/Paketfehler; ob der bestehende einmalige 5xx-API-Fallback auch für den Manifest-Abruf gilt, ist eine Entwurfsentscheidung (siehe Offene Fragen).
  - *Manifest ist kein gültiges JSON oder hat nicht das erwartete Schema:* **verwerfen** — kontrollierter Fehlschlag, kein Best-Effort-Parsing.
  - *Manifest enthält keinen Eintrag mit `assetName == "release.zip"` bzw. leeren/unbrauchbaren `sha256`:* **verwerfen**.
  - *Veraltete Releases ohne `update.json`:* praktisch nur relevant, wenn das neueste zulässige Release kein Manifest trägt; da `update.json` seit Einführung des `build-and-package`-Schritts immer als Release-Asset hochgeladen wird, ist der Fall selten, führt aber nach der Annahme ebenfalls zum Abbruch (siehe Offene Fragen zur Kandidatenbehandlung).
- **Scope-Ausschlüsse:**
  - Keine Authenticode-Signaturprüfung der entpackten `Softwareschmiede.exe` — im Issue ausdrücklich als „optional perspektivisch" benannt, nicht Teil dieses Eingriffs.
  - Keine Signaturprüfung des Manifests selbst: `update.json` ist nicht signiert und wird über denselben TLS-/GitHub-Kanal bezogen wie Release-Liste und ZIP. Die Verifikation schützt vor manipuliertem/korruptem Paket-Download (z. B. CDN-/Proxy-Eingriff, nachträglich ausgetauschtem Asset bei unverändertem Manifest), nicht vor vollständig kompromittiertem GitHub-Release; dieses Restrisiko bleibt bestehen und ist bewusst nicht Teil der Anforderung.
  - Keine Änderung am Release-Workflow: `update.json` wird bereits erzeugt und hochgeladen; `.github/actions/build-and-package/action.yml`, `release.yml` und `staging-ci.yml` bleiben unverändert.
  - Keine Verifikation einzelner entpackter Dateien und keine Prüfung des entpackten `version.json` gegen den Manifest-`version`-Wert — die bisherige Root-Dateien-Validierung bleibt wie sie ist (Konsistenzprüfung Manifest-Version ↔ `UpdateInfo.Version` ggf. als Offene Frage).
  - Keine neue Benutzer-/Anwendungskonfiguration und kein Opt-out — die Verifikation ist ein Sicherheitszwang, keine Option (siehe Konfiguration).
  - Keine Auswirkung auf `UpdateMode`-/`UpdatePfad`-Einstellungen, Kanalselektion, CLI-Sicherheitsprüfung, Elevation-Entscheidung oder den Download-Fallback des ZIP — alle bleiben unverändert.

## Betroffene Klassen und Komponenten

### Zu ändernde / neue Artefakte

- `src/Softwareschmiede/Application/Services/Updates/UpdateModels.cs`
  - `UpdateInfo`: Erweiterung um die Manifest-Referenz (z. B. `ManifestDownloadUrl`, ggf. `ManifestFallbackDownloadUrl` analog `FallbackDownloadUrl`; optionale Parameter am Ende des Positional Records, wie bei `FallbackDownloadUrl` vorgelebt). Alternativ ein eigenes Manifest-Modell, falls mehr als die URL übergeben wird (Entwurfsentscheidung).
  - `UpdatePreparationPhase`: ggf. neue Phase für die Verifikation (z. B. `Verifizieren`), damit der Fortschrittsdialog den Schritt anzeigen kann — sonst läuft sie implizit zwischen `Download` und `Entpacken` (Entwurfsentscheidung, wirkt auf `UpdateProgressViewModel`-Texte).
- `src/Softwareschmiede/Infrastructure/Services/Updates/GitHubReleaseClient.cs`
  - `TryCreateCandidate`: zusätzliche Asset-Auswahl für `update.json` im selben Release (Konstante oder `UpdateOptions`-Eintrag analog `AssetName`), Aufbau der Manifest-Download-URL (und ggf. API-Fallback-URL über die vorhandenen Hilfen `CreateFallbackDownloadUrl`/`IsAllowedAssetApiUrl`, Host-Pinning auf `api.github.com`).
- `src/Softwareschmiede/Infrastructure/Services/Updates/UpdatePackageService.cs`
  - `PreparePackageAsync`: nach dem erfolgreichen ZIP-Download und der Leer-Prüfung, vor `ZipFile.ExtractToDirectory`, Manifest abrufen und verifizieren (neue private Schritte, z. B. `DownloadManifestAsync`/`VerifyPackageHashAsync`). Fehlschlag läuft über den vorhandenen Catch-all-Aufräumblock.
  - Neuer Manifest-Parser (interne DTO-Klasse für `assets[]`/`sha256`/`sizeBytes`, `JsonPropertyName`-Mapping wie in `GitHubReleaseClient`) — kann als private/geschachtelte Klasse oder eigene Datei im selben Ordner entstehen.
  - Hashberechnung auf dem heruntergeladenen `release.zip` (streaming, `System.Security.Cryptography`), OrdinalIgnoreCase-Vergleich gegen den Manifest-Hexwert.
- `src/Softwareschmiede/Application/Services/Updates/UpdateOptions.cs`
  - Ggf. neue Option `ManifestAssetName` (`"update.json"`) analog `AssetName` (Entwurfsentscheidung — eine Konstante im Client wäre ebenso denkbar, passt aber schlechter zum bestehenden Muster).
- `src/Softwareschmiede.App` (nur bei neuer Fortschrittsphase): `UpdateProgressViewModel`/`UpdateProgressDialog`-Anzeigetexte um den Verifikationsschritt ergänzen.
- Tests:
  - `src/Softwareschmiede.Tests/Application/Services/Updates/UpdatePackageServiceTests.cs`: neue Fälle für (a) Hash stimmt → Erfolg, (b) Hash-Abweichung → `InvalidDataException` + Aufräumen, (c) Manifest nicht abrufbar/unbekannte URL → Fehlschlag, (d) ungültiges Manifest/kein `release.zip`-Eintrag/leerer `sha256` → Fehlschlag, (e) ggf. `sizeBytes`-Abweichung.
  - `src/Softwareschmiede.Tests/Infrastructure/Services/GitHubReleaseClientTests*.cs`: Manifest-Asset wird korrekt aus der Release-Antwort übernommen (Auswahl, Fehlen, ungültige URL).
  - `src/Softwareschmiede.Tests/E2E/UpdateE2EFixture.cs` + `ErzeugeReleaseListenJson`/`SetzeStandardAntworten`: Die E2E-Fixture erzeugt die Paket-ZIPs selbst — sie muss zusätzlich eine `update.json`-Antwort mit dem tatsächlichen Hash (und ggf. der Größe) der generierten ZIPs liefern und das `update.json`-Asset in die simulierte Release-Liste aufnehmen, sonst scheitern sämtliche Update-E2E-Tests am neuen Pflichtschritt. `UpdateFixtureHttpMessageHandler` mappt bereits exakte URLs, ein zusätzlicher `UpdateE2EAntwort`-Eintrag genügt prinzipiell.
  - `src/Softwareschmiede.Tests/App/Services/Testing/UpdateFixtureHttpMessageHandlerTests.cs`: ggf. Ergänzung um Manifest-Request-Klassifizierung.
- Dokumentation (Lifecycle-Schritt 12, nicht Teil der Code-Änderung): `docs/help/programmupdate/ablauf-technisch.md` (Abschnitt „Gemeinsamer Installationspfad", Schritt 8; Fehlertabelle), `docs/help/programmupdate/business-rules.md` (Verifikationsregel), `docs/help/programmupdate/architektur.md` (Rolle `UpdatePackageService`).

### Nicht betroffene Artefakte

- `IUpdatePackageService`/`IUpdateReleaseClient`-Signaturen: unverändert — `PreparePackageAsync(UpdateInfo, …)` bleibt der Vertrag, die Erweiterung läuft über `UpdateInfo`-Felder und interne Schritte.
- `UpdateService`, `MainWindowUpdateFlow` (Ablauforchestrierung, Aktualitätsnachweise, CLI-Sicherheit): unverändert — die Verifikation ist ein zusätzlicher interner Schritt der Vorbereitung; Fehler landen auf dem bestehenden Fehlerpfad.
- `UpdateScriptService`, `UpdateProcessLauncher`, `CliUpdateSafetyService`: unverändert.
- Release-Workflow (`.github/`): unverändert — Manifest existiert bereits als Asset.

## Implementierungsansatz

- Reine Erweiterung des bestehenden Vorbereitungspfads — kein neuer Service-Vertrag nötig. Zwei denkbare Verteilungen (Entwurfsentscheidung, keine erfundene Festlegung):
  1. `GitHubReleaseClient` liefert im `UpdateInfo` die Manifest-URL des `update.json`-Assets (gleiche Auswahl-/Pinning-Regeln wie `release.zip`); `UpdatePackageService` lädt das Manifest innerhalb von `PreparePackageAsync`, parst den `release.zip`-Eintrag und vergleicht `sha256` (und optional `sizeBytes`) mit dem heruntergeladenen Paket. Der Abruf erfolgt frühestens in der Vorbereitung — so entsteht kein zusätzlicher Request bei jeder Startup-Prüfung. Diese Variante entspricht dem Issue-Wortlaut „Vor dem Entpacken `update.json` abrufen" am direktesten.
  2. Der Manifest-Abruf erfolgt bereits im Release-Client während `CheckForUpdateAsync` und liefert den erwarteten Hash fertig im `UpdateInfo` — würde einen Zusatzrequest in jede Prüfung legen und ist daher nicht die naheliegende Variante (hier nur als Alternative dokumentiert).
- Die Hash-Prüfung hängt strikt vor `ZipFile.ExtractToDirectory`; ein Negativbefund muss vor dem ersten Schreibzugriff des Entpackens abbrechen. Der bestehende `catch`-Block in `PreparePackageAsync` räumt `*.download`, `release.zip` und das Extraktionsverzeichnis bereits auf — der neue Fehlschlag integriert sich ohne Sonderbehandlung.
- Manifest-Parsing mit `System.Text.Json` wie üblich; das Asset-Array wird auf `assetName == _options.AssetName` (`release.zip`, OrdinalIgnoreCase konsistent zur bestehenden Asset-Suche) gefiltert. `sha256` wird als Hex-String gelesen und Ordinal-/OrdinalIgnoreCase mit dem berechneten Hash verglichen (der Workflow schreibt den Hash per `ToLowerInvariant()` klein).
- Reihenfolge innerhalb der Vorbereitung (Entwurfsentscheidung): Manifest-Abruf sinnvollerweise direkt nach Abschluss des ZIP-Downloads (ZIP liegt als `release.zip` vor, Hash kann in einem Rutsch berechnet werden); ein Abruf bereits vor/parallel zum ZIP-Download wäre ebenfalls möglich, spart aber nichts Wesentliches und erschwert die Fehlerzuordnung.
- Abhängigkeiten: keine neuen Pakete; verwendet werden `System.Security.Cryptography` (SHA-256), `System.Text.Json`, der injizierte `HttpClient` und die vorhandenen URL-Pinning-Hilfen.

## Konfiguration

- **Keine Laufzeitkonfiguration und kein Opt-out vorgesehen:** Die Verifikation ist ein verbindlicher Sicherheitsschritt jeder Paketvorbereitung — weder `UpdateOptions`-Schalter noch Anwendungseinstellung (`AppEinstellungen`) noch UI-Option.
- Einzige denkbare Optionsergänzung ist `ManifestAssetName` (`"update.json"`) in `UpdateOptions` als Deployment-Konstante analog `AssetName` — technische Konfiguration, keine Benutzereinstellung (Entwurfsentscheidung).
- Der Manifest-Abruf sollte den bestehenden Timeout-/Cancellation-Konventionen folgen (`UpdateOptions.CheckTimeout` gilt bislang nur für die Release-Prüfung; für Download-/Manifestabruf gilt das übergebene `CancellationToken` des Vorbereitungslaufs — Annahme: kein separater Timeout nötig, ggf. Offene Frage).

## Offene Fragen

1. **Manifest-Abruf bei Fehlen hart oder weich:** Annahme ist der harte Abbruch, wenn `update.json` fehlt/nicht verifizierbar ist — ein weiches Durchwinken würde die Schutzwirkung durch Weglassen des Assets unterlaufen. Konsequenz: Ein Release ohne `update.json`-Asset ist danach nicht mehr installierbar. Ist das gewünscht, oder sollen ältere Releases ohne Manifest (falls real vorkommend) weiterhin updatefähig bleiben?
2. **Kandidatenbehandlung im Release-Client:** Soll ein Release ohne `update.json`-Asset bereits in `TryCreateCandidate` als nicht nutzbar ausgefiltert werden (dann erscheint das Update gar nicht erst als Angebot), oder bleibt es Kandidat und scheitert erst in der Vorbereitung? Annahme: Verwerfen erst in der Vorbereitung ist ausreichend und näher am Issue-Wortlaut; das Filtern im Client wäre die UX-freundlichere Variante.
3. **`sizeBytes` mitprüfen:** Das Manifest trägt neben `sha256` auch `sizeBytes`. Soll die Dateigröße zusätzlich als Plausibilitätscheck geprüft werden (günstig, fängt früh ab), oder beschränkt sich die Prüfung exakt auf den Hash wie in der Empfehlung? Annahme: Hash ist verbindlich; Size-Check ist optionaler Zusatz.
4. **5xx-Fallback auch für das Manifest:** Gilt der bestehende einmalige Download-Fallback auf die API-Asset-URL (`UpdateInfo.FallbackDownloadUrl`-Mechanismus) auch für den `update.json`-Abruf (dann bräuchte `UpdateInfo` ein `ManifestFallbackDownloadUrl`), oder scheitert der Manifest-Abruf ohne Zweitversuch?
5. **Fortschrittsanzeige:** Soll die Verifikation als eigene Phase im `UpdateProgressDialog` sichtbar sein (neuer `UpdatePreparationPhase`-Wert + Anzeigetext, z. B. „Update-Paket wird verifiziert"), oder läuft sie unmarkiert zwischen `Download` und `Entpacken`? Annahme: eigene Phase erhöht die Nachvollziehbarkeit bei Fehlschlag, ist aber keine fachliche Pflicht.
6. **Konsistenz Manifest ↔ Release:** Soll `version` des Manifests gegen `UpdateInfo.Version`/`TagName` geprüft werden (erkennt vertauschte/falsche Manifest-Assets), oder ist die Prüfung auf den `release.zip`-Asset-Eintrag ausreichend?
7. **Timeout des Manifest-Abrufs:** Genügt das durchgereichte `CancellationToken` (Abbrechen über den Dialog), oder soll — analog `CheckTimeout` — ein fester Höchstwert für den kleinen JSON-Abruf gelten?
