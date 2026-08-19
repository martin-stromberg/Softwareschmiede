# Aufgabenliste – Anforderungsbearbeitung

Branch: `task/issue-204-eab6dccb4b304e1a94ef7c2417f9b297-ide-plugins`

| Status | Schritt | Beschreibung | Artefakt |
|--------|---------|--------------|----------|
| [x] | 1 | Branch-Name ermitteln | – |
| [x] | 2 | Verzeichnisstruktur vorbereiten | `docs/features/{branchname}/` |
| [x] | – | Einstiegspunkt ermitteln | Fortsetzungslauf: `continue.md` vorhanden → Einstieg bei Schritt 6 |
| [x] | 3 | Anforderung übersetzen (Unteragent) | `requirement.md` (bereits vorhanden — übersprungen) |
| [x] | 4 | Bestandsaufnahme (Unteragent) | `inventory.md`, `inventory/` (bereits vorhanden — übersprungen) |
| [x] | 5 | Umsetzungsplanung (Unteragent) | `plan.md` (bereits vorhanden — übersprungen) |
| [x] | 5a | Offene Punkte prüfen und ggf. Planung wiederholen | `plan.md` (aktualisiert) (bereits abgeschlossen — übersprungen) |
| [x] | 5b | Planungscommit | – (bereits erfolgt — übersprungen) |
| [x] | 6 | Implementierung (Unteragent) | Codeänderungen (dieser Lauf, Iteration 1: 6 Punkte aus `continue.md` behoben — plan.md-Widerspruch aufgelöst, `KannIdeAuswaehlen`-Root-Cause gefixt, 2 E2E-Tests verifiziert (kein Änderungsbedarf), veralteter XML-Doc-Kommentar korrigiert, README-Fehler entfernt, Testfactory-Methode umbenannt. Unabhängig verifiziert: Build 0 Fehler, Tests 1273/1274 grün, 0 fehlgeschlagen) |
| [x] | 7 | Plan-Review (Unteragent, bedingt) | `review.md` (weiterhin Status „Vollständig umgesetzt" aus früherem Durchlauf — Schritt übersprungen) |
| [x] | 8 | Code-Review (Unteragent) | `review-code.md` (Fortsetzungszyklus Iteration 1: 6 neue Befunde — alle 5 alten Befunde aus `review-code.3.md` verifiziert behoben. Neue Befunde: 1. `IIdePlugin.OpenRepositoryAsync` toter Code, 2. `KannIdeAuswaehlen`-Berechnung dupliziert, 3. `WaehleEntryPointAsync`-Fallback erzeugt ungültigen `IdeEntryPoint.Path`, 4. `SettingsView.xaml.cs` Guard-Klausel dupliziert, 5. `MoveIdePlugin` nutzt nicht `SafeFireAndForget`-Muster, 6. `PluginSelectionServiceTests_IdePlugin.CreateSut` nutzt getrennten DbContext) |
| [x] | 8b | Tests ausführen (Unteragent) | `test-results.md` (1318 gesamt, 1316 bestanden, 0 fehlgeschlagen, 2 übersprungen — unabhängig verifiziert) |
| [x] | – | Iteration oder Abschluss entscheiden | Fortsetzungszyklus Iteration 1: Code-Review nicht grün (6 Befunde) → Iteration 1 < 3 → zurück zu Schritt 6 (Iteration 2) |
| [x] | 8a | Folgeaufgaben dokumentieren (bei Schleifenabbruch) | `continue.md` (nach Iteration 3 des Fortsetzungszyklus geschrieben — siehe unten) |
| [x] | 9 | Dokumentation erstellen (Unteragent) | `docs/help/` (5 Dateien aktualisiert — veraltete `OpenRepositoryAsync`/`IdeOeffnenService`-Referenzen korrigiert; unabhängig per `git diff --stat` und Grep verifiziert) |
| [x] | 9b | README aktualisieren (Unteragent) | `README.md` (geprüft, keine Änderung nötig — bereits akkurat) |
| [ ] | – | Feature-Verzeichnis löschen | Übersprungen — `continue.md` enthält noch einen offenen Punkt (doppelter Mock-Aufbau in Tests), daher NICHT löschen |
| [ ] | – | Commit durchführen | – |

## Fortsetzungszyklus – Iteration 2

| Status | Schritt | Beschreibung | Artefakt |
|--------|---------|--------------|----------|
| [x] | 6 | Implementierung (Unteragent) | Codeänderungen (alle 6 Befunde aus `review-code.4.md` behoben — unabhängig per `git diff` je Befund verifiziert; Build 0 Fehler, 0 Warnungen) |
| [x] | 7 | Plan-Review (Unteragent, bedingt) | `review.md` (unverändert gültig — Befunde betrafen keine Planabweichungen, übersprungen) |
| [x] | 8 | Code-Review (Unteragent) | `review-code.md` (2 neue Befunde, alle 6 alten aus `review-code.4.md` verifiziert behoben. 1. `OeffneIdeInternAsync` catch-Block setzt `KannIdeAuswaehlen=false` auch wenn Öffnen fehlschlägt, obwohl Einstiegspunkte weiterhin existieren — unabhängig verifiziert per Read, Zeile 1907. 2. `SettingsView.xaml` dupliziertes StackPanel für SCM/KI- vs. IDE-Plugin-Details.) |
| [x] | 8b | Tests ausführen (Unteragent) | `test-results.md` (1315 gesamt, 1312 bestanden, 1 fehlgeschlagen, 2 übersprungen — Fehlschlag `WpfBasisSzenarien` unabhängig als Flakiness verifiziert: isolierter Rerun bestanden in 7,8s, keine Regression durch Iteration-2-Änderungen) |
| [x] | – | Iteration oder Abschluss entscheiden | Iteration 2, 2 Befunde < 6 (Iteration 1) und Iteration 2 < 3 → Fortschritt → zurück zu Schritt 6 (Iteration 3) |

## Fortsetzungszyklus – Iteration 3 (letzte Iteration, Maximum erreicht)

| Status | Schritt | Beschreibung | Artefakt |
|--------|---------|--------------|----------|
| [x] | 6 | Implementierung (Unteragent) | Codeänderungen (beide Befunde behoben — unabhängig per `git diff`/Read verifiziert: `KannIdeAuswaehlen = false;` aus catch-Block entfernt, `PluginDetailPanel`-UserControl extrahiert und zweimal in `SettingsView.xaml` mit erhaltenen Automation-Namen instanziiert; Build 0 Fehler, 0 Warnungen) |
| [x] | 7 | Plan-Review (Unteragent, bedingt) | `review.md` (unverändert gültig — übersprungen) |
| [x] | 8 | Code-Review (Unteragent) | `review-code.md` (1 neuer, geringfügiger Befund — beide alten aus `review-code.5.md` verifiziert behoben: doppelter Mock-Aufbau in `TaskDetailViewModelTests_IdeAuswahl.cs`) |
| [x] | 8b | Tests ausführen (Unteragent) | `test-results.md` (1316 gesamt, 1314 bestanden, 0 fehlgeschlagen, 2 übersprungen — ein Fehlschlag im ersten Lane-2-Lauf durch 2 volle Wiederholungen als Flakiness verifiziert, konsistent mit Iteration 2) |
| [x] | – | Iteration oder Abschluss entscheiden | Iteration 3 = Maximum erreicht → Abbruch (Step 8a), unabhängig davon dass Befunde weiter gesunken sind (1 < 2) |
| [x] | 8a | Folgeaufgaben dokumentieren (bei Schleifenabbruch) | `continue.md` (1 geringfügiger verbleibender Befund: doppelter Mock-Aufbau in `TaskDetailViewModelTests_IdeAuswahl.cs`) |

## Neue Anforderung – Dropdown aggregiert über ALLE kompatiblen IDE-Plugins

Anwenderentscheidung zur ursprünglichen „Offenen Frage 1" aus `requirement.md`: Dropdown soll nicht nur Einstiegspunkte des einen priorisierten Plugins zeigen, sondern über alle kompatiblen (Explicit + Fallback) aktivierten IDE-Plugins aggregieren (z. B. Visual Studio + Visual Studio Code gemeinsam wählbar).

| Status | Schritt | Beschreibung | Artefakt |
|--------|---------|--------------|----------|
| [x] | 5 | Umsetzungsplanung — Revision (Unteragent) | `plan.md` aktualisiert: neue Designentscheidungen, `PluginSelectionService.ResolveAlleKompatiblenIdePluginsAsync`, `TaskDetailViewModel.ErmittleAggregierteIdeEinstiegspunkteAsync`/`FormatiereAnzeigeWert`, Tupel-Callback-Signatur, Testplan; unabhängig per Read verifiziert |
| [x] | 5a | Offene Punkte prüfen | „Offene Frage 1" jetzt explizit gelöst dokumentiert |
| [ ] | 5b | Planungscommit | – |
| [ ] | 6 | Implementierung (Unteragent) | Codeänderungen |
| [ ] | 7 | Plan-Review (Unteragent, bedingt) | `review.md` |
| [ ] | 8 | Code-Review (Unteragent) | `review-code.md` |
| [ ] | 8b | Tests ausführen (Unteragent) | `test-results.md` |
| [ ] | – | Iteration oder Abschluss entscheiden | – |
