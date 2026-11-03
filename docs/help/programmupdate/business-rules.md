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
