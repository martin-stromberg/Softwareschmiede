# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan lückenhaft

Der ueberarbeitete Plan adressiert alle bisherigen Befunde P-01 bis P-04 und T-01 bis T-08 aus [plan-check.1.md](plan-check.1.md). Umsetzung, Reihenfolge, Testvoraussetzungen und E2E-Nachweise fuer die sieben Akzeptanzkriterien sind weitgehend konkret. Eine Testluecke verbleibt beim ausdruecklich festgelegten Verhalten fuer nicht lesbare Update-Einstellungen: Dieser Fehlerfall ist weder in den Logiktests noch in den sichtbaren E2E-Fehlerablaeufen konkret enthalten (T-09, AK 7).

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| AK 1: Auswahlbox mit `Aus`, `Nur Pruefen` und `Bei Programmstart pruefen und ausfuehren`. | Einstellungen laden/speichern, Schritte 1 bis 3; feste Enum-Werte, exakte Labels, Bindings und Automation-Namen; U-01/U-04. | `UpdateSettings_SaveLoadAllValues` und E-01 pruefen alle drei Modi, Speichern, Wiederanzeigen und Lesbarkeit bei minimaler Fenstergroesse. | Abgedeckt |
| AK 2: `Aus` deaktiviert die Updatepruefung. | Designentscheidung zu `Aus`, Settings-Handler und gemeinsamer Ablauf sperren Start, manuelle Pruefung und direkte Installationsaufrufe; alte Angebote werden entfernt; U-04/U-05. | `Aus_BlocksBothCommandsAndDirectInvocation`, E-02 mit UI-Speichern und echtem Neustart sowie E-04 mit Einstellungswechsel und verspaeteter Antwort. Null Releaseabrufe und gesperrte/verborgene UI-Aktionen sind explizit. | Abgedeckt |
| AK 3: `Nur Pruefen` prueft ohne automatische Installation. | Einmaliger Start, Schritt 4, prueft und bietet an, ohne Sicherheitsdialog, Download, Updater oder Shutdown. Ausdrueckliche manuelle Installation bleibt erlaubt; U-05. | E-02 weist Abruf, sichtbares Angebot und ausbleibende Installation nach; E-03 prueft die anschliessende ausdrueckliche Installation. ViewModel-Tests ergaenzen die Moduspruefung. | Abgedeckt |
| AK 4: Im Startmodus wird beim Programmstart geprueft und ein gefundenes Update automatisch installiert. | `ContentRendered` nach Initialisierung und Owner-Zuweisung, einmaliges Startkennzeichen, gemeinsames Gate und Installationsverfahren; U-05. Sicherheitsbestaetigung, Abbruch und Shutdown erst nach erfolgreichem Updaterstart sind festgelegt. | E-02 prueft den echten Neustart ohne Updateklick, sofortige Releaseantwort, Dialog-Owner, Vorbereitung und Start-vor-Shutdown. E-03 prueft beide Kanaele automatisch, E-05/E-06 die benannten Negativpfade, E-07 Einmaligkeit und Parallelitaet; zusaetzliche ViewModel-/Servicetests. | Abgedeckt |
| AK 5: Checkbox aktiviert das Laden von Prereleases. | Persistentes Boolean mit Default `false`, Settings-Property, Checkbox und Automation-Name; U-01/U-04. | `UpdateSettings_SaveLoadAllValues`, SQLite-Roundtrip und E-01 mit beiden Checkboxzustaenden, Verwerfen, Wiederanzeigen und Neustart. | Abgedeckt |
| AK 6: Prereleases werden nur bei aktivierter Checkbox beruecksichtigt. | Gemeinsame Optionen, Filter nach GitHub-Flag ODER SemVer-Suffix, defensive Servicepruefung, Pagination und suffixerhaltende lokale/entfernte Normalisierung; U-02/U-03. | Client-/Servicetests fuer beide Filtermerkmale, Mehrseitenmaximum, Normalisierung und Folgeupdates ab installierter RC-Version. E-03 verbindet die UI-Auswahl mit konkretem Angebot, Asset und Paketversion, manuell und automatisch. | Abgedeckt |
| AK 7: Einstellungen werden bei Pruefung und automatischer Installation konsistent beruecksichtigt. | Gemeinsames Speichern, frische Scopes, gespeicherter Snapshot, Invalidierung, Generation/Cancellation und erneutes Lesen vor Vorbereitung/Start; U-01/U-03/U-04/U-05. DB-Lesefehler sollen den Versuch ohne Installation beenden. | SQLite-, Options-, Invalidierungs- und Nebenlaeufigkeitstests sowie E-01 bis E-04/E-07 decken regulaere Konsistenz und Einstellungswechsel ab. Konkrete Tests fuer fehlgeschlagenes Lesen der Einstellungen am Anfang und vor der Installationsuebergabe fehlen; siehe T-09. | Lücke |

## Fehlende oder unvollständige Testanforderungen

- [ ] **T-09: Nicht lesbare Update-Einstellungen bis zur Installationsgrenze nachweisen (AK 7).** [plan.md](plan.md), Zeile 34, legt fuer DB-Lesefehler einen sichtbaren Nicht-pruefbar-Zustand ohne Installation fest; Zeile 63 verlangt erneutes Lesen vor Vorbereitung und unmittelbar vor Updaterstart. Die konkret aufgelisteten Fehlerfaelle in `Startup_NoUpdateOrNotCheckable_DoesNotInstall` (Zeile 194) und E-05 (Zeile 228) betreffen dagegen die lokale Versionsdatei und die Releasequelle. `UpdateSettings_PersistsAcrossScopes` (Zeile 182) deckt einen Schreibfehler ab. Keiner dieser Faelle prueft einen fehlgeschlagenen Lesezugriff auf die gespeicherten Updatewerte.

  In den Logiktests einen Fehler von `GetUpdateSettingsAsync` beim ersten Lesen sowie beim erneuten Lesen vor Vorbereitung/Updaterstart vorsehen, fuer den automatischen und den gemeinsamen manuellen Pfad. Beim ersten Lesefehler darf kein Releaseabruf folgen; beim spaeteren Fehler duerfen die jeweils verbleibenden Installationsschritte und insbesondere Updaterstart/Shutdown nicht erfolgen. Bereits geladene Werte oder Defaults duerfen den gescheiterten Aktualitaetsnachweis nicht ersetzen. Sichtbarer Fehlerzustand und Freigabe von Gate/Busy gehoeren zu den Assertions.

  E-05/E-06 um entsprechende FlaUI-Varianten erweitern: Startmodus zuvor ueber die UI speichern, mit derselben DB neu starten und gezielt den Update-Settings-Lesezugriff nach erfolgreicher allgemeiner DB-Initialisierung fehlschlagen lassen; ausserdem einen spaeten Lesefehler nach erfolgreicher Vorbereitung ausloesen. Den sichtbaren Hinweis/Fehler, null Updaterstart/Shutdown und anschliessende Bedienbarkeit nachweisen. In U-06 beziehungsweise den Testvoraussetzungen den kontrollierten Fehlerausloeser und seine Freigabe festlegen; ein allgemeiner DB-Startup-Abbruch prueft diesen Benutzerfluss nicht. Die vorhandenen Szenarien koennen dafuer erweitert werden, eine neue dauerhafte FlaUI-Testmethode ist nicht erforderlich.

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Einstellungen oeffnen, alle Modi und Checkboxzustaende speichern, wiederanzeigen, verwerfen und ueber Neustart erhalten (AK 1, 5, 7). | E-01 `Settings_AllModesAndPrereleasesPersist`, echte DB und mindestens ein echter Neustart. | Abgedeckt |
| `Aus` speichern, neu starten und ausbleibende Pruefung sowie deaktivierte/verborgene Updateaktionen kontrollieren (AK 2). | E-02 `Startup_ModesDriveUpdatePipeline`; Abschlussmarker und Request-/Prozessprotokoll sichern Negativassertions ab. | Abgedeckt |
| `Nur Pruefen` speichern, neu starten, Updateangebot sehen, ohne dass automatisch installiert wird (AK 3). | E-02 mit sichtbarer Version und null Asset-/Start-/Shutdown-Ereignissen. | Abgedeckt |
| Startmodus speichern und nach echtem Neustart ohne Updateklick Fortschritt, Paketvorbereitung und Updateruebergabe beobachten (AK 4). | E-02 mit sofortiger Antwort und echtem Dialog-Owner; E-07 prueft zusaetzlich Einmaligkeit. | Abgedeckt |
| Checkbox ueber die UI aendern und tatsaechliche Stable-/Prerelease-Auswahl bis zum passenden Asset pruefen (AK 6, 7). | E-03 `PrereleaseCheckbox_SelectsMatchingAsset`, beide Checkboxzustaende manuell und bei automatischem Neustart. | Abgedeckt |
| Nach RC-Fund Checkbox deaktivieren oder `Aus` speichern; altes Angebot und verspaetete Antwort duerfen keine Installation ausloesen (AK 2, 6, 7). | E-04 `SavedChangesInvalidatePreviousOffer`, einschliesslich blockiertem Request und DB-erhaltendem Neustart. | Abgedeckt |
| Ohne neueres Release beziehungsweise bei ungueltiger lokaler Version oder Releasefehler bedienbar bleiben (AK 4, 7). | E-05 `Startup_NoUpdateOrUncheckableRemainsUsable`, sichtbarer Fehlerhinweis und ausbleibende Folgeschritte. | Abgedeckt |
| Sicherheitsdialog ablehnen/bestaetigen, Download abbrechen sowie Vorbereitungs- und Launcherfehler behandeln (AK 4, 7). | E-06 `Startup_SafetyCancelAndErrorsRemainUsable`, echte Dialoginteraktionen, sichtbare Fehler und kein Shutdown. | Abgedeckt |
| Waehrend Startpruefung/Vorbereitung Updateaktionen versuchen und Doppelinstallation ausschliessen (AK 4, 7). | E-07 `Startup_IsOnceAndCommandsStayBlocked`, kontrollierte Blockierung, UI-Zustaende und phasenbezogene Ereigniszaehler. | Abgedeckt |
| Nicht lesbare Update-Einstellungen beim Start oder bei letzter Aktualitaetspruefung sichtbar behandeln und die Installation unterbinden (AK 7). | Keine konkrete Variante in E-05/E-06 und kein gezielter Settings-Lesefehler in der Fixture vorgesehen; siehe T-09. | Lücke |
| Rollen-/Berechtigungsregeln. | Keine neuen Rollen- oder Berechtigungsanforderungen. Modusabhaengige Sichtbarkeit und CLI-Sicherheitsbestaetigung sind oben separat erfasst. | Nicht erforderlich mit Begründung |

## Fehlende oder unvollständige Planbestandteile

Keine zusaetzlichen fachlichen Umsetzungsluecken festgestellt. Der offene Testbedarf einschliesslich des dazugehoerigen Fixture-Fehlerausloesers ist vollstaendig unter T-09 beschrieben. Die bisherigen P-01 bis P-04 sind im ueberarbeiteten Plan adressiert.

## Hinweise

Pruefbasis waren [requirement.md](requirement.md), [inventory.md](inventory.md), alle vier Detaildokumente ([Einstellungen](inventory/settings.md), [Updatepipeline](inventory/update-pipeline.md), [Startfluss](inventory/startup-flow.md), [Tests](inventory/tests.md)) und der vollstaendige [plan.md](plan.md). Zum Abgleich der Nachplanung wurde [plan-check.1.md](plan-check.1.md) herangezogen.

| Bisheriger Befund | Bewertung der Nachplanung |
|---|---|
| P-01: Reichweite von `Aus` | Adressiert: alle Eintrittspunkte, direkte Aufrufe und vorhandene Angebote; U-04/U-05, E-02/E-04. |
| P-02: Startzeitpunkt und UI-Bereitschaft | Adressiert: konkreter `ContentRendered`-Aufruf, Owner vor `Show`, keine Konstruktorpruefung; Sofortantworttest und E-02. |
| P-03: Prerelease-Kennung durchgaengig erhalten | Adressiert: gemeinsamer Normalisierungsvertrag und expliziter lokaler Provider-Verbrauch; Quellen- und Folgeupdatetests. |
| P-04: E2E-Voraussetzungen | Adressiert fuer die bisher geforderten Szenarien: HTTP/ZIP, lokale Version, DB-erhaltende Neustarts, Prozess-/Shutdown-Protokoll, echte Dialoge und konsolidierter Runner in U-06 vor U-07. |
| T-01: Drei Modi im echten Start | Adressiert durch E-02 mit UI-Speichern, Neustart und beobachtbaren positiven/negativen Ergebnissen. |
| T-02: Checkbox bis zur Release-/Assetauswahl | Adressiert durch E-03 mit echten Client-/Paketservices fuer manuelle und automatische Ausfuehrung. |
| T-03: Persistenz und Einstellungswechsel | Adressiert durch SQLite-, Options- und Generationstests sowie E-01/E-04. |
| T-04: Bisher benannte negative Start-/Installationspfade | Adressiert durch konkrete ViewModel-/Servicetests und E-05/E-06; der zusaetzlich festgelegte Settings-Lesefehler bleibt unter T-09 offen. |
| T-05: Lokale/entfernte Versionskette | Adressiert durch Normalisierungs-, Provider-, Client- und kombinierte Servicetests mit RC-Folgeupdates. |
| T-06: Mehrseitige Releaseauswahl | Adressiert durch unsortierte Mehrseitenfixtures fuer beide Kanaele, Folgeseitenfehler, Cancellation und ungueltige Pagination. |
| T-07: Nebenlaeufigkeit und Einmaligkeit | Adressiert durch blockierte Check-/Prepare-Tests, direkte Aufrufe, wiederholte Bereitschaft und E-07. |
| T-08: Bestehende Regressionen | Adressiert durch die explizite Regressionstabelle, Interface-/Mock-Anpassungen, Paket-/Skript-/Dialogtests und U-08. |

Die kontrollierte Prozess-/Shutdown-Grenze ist als Begrenzung des E2E-Nachweises ausdruecklich dokumentiert. Releaseauswahl, Download, Entpacken, Validierung, Skripterzeugung und WPF-Interaktionen bleiben im geplanten realen Ablauf. Eine tatsaechliche Installation und Betriebssystembeendigung werden dadurch nicht als getestet behauptet.

Die spaetere Abnahme fordert einen erfolgreichen vollen Build, getrennte regulaere und OS-Testlaeufe sowie alle Pflicht-E2Es. Nicht ausgefuehrte Pflichtszenarien duerfen laut Plan nicht durch Unit-Tests ersetzt werden.

`/plan-check` wurde nach dem lokalen Lifecycle-Workflow direkt ausgefuehrt, weil keine delegierbaren Unteragenten verfuegbar sind. Diese Gegenpruefung bewertet die Planvollstaendigkeit; sie bestaetigt keinen Implementierungsstand. Ausschliesslich `plan-check.md` wurde erstellt. Anforderung, Bestandsaufnahme, Plan, fruehere Gegenpruefung und Todo wurden nicht geaendert. Build und Tests wurden fuer diese Dokumentpruefung nicht ausgefuehrt.
