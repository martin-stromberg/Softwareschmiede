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
| [x] | 5b | Planungscommit | Commit `ace70d0` |
| [x] | 6 | Implementierung (Unteragent) | Codeänderungen (`ResolveAlleKompatiblenIdePluginsAsync`, `ErmittleAggregierteIdeEinstiegspunkteAsync`, `FormatiereAnzeigeWert`, Tupel-Callback-Signatur, 12 neue/angepasste Tests — unabhängig verifiziert: Build 0 Fehler, 32/32 gezielte Tests grün) |
| [x] | 7 | Plan-Review (Unteragent) | `review.md` (Status „Vollständig umgesetzt", 0 Abweichungen — unabhängig per Read verifiziert) |
| [x] | 8 | Code-Review (Unteragent) | `review-code.md` (5 Befunde: 1. `ResolveAlleKompatiblenIdePluginsAsync` dupliziert Setup von `ResolveIdePluginAsync`, 2. Haupt-Button-Zweig ermittelt Arbeitsverzeichnis+Plugin doppelt (Single-Plugin + Aggregiert), 3. Aggregationsschleife ohne Fehler-Isolierung pro Plugin, 4. bekannter Mock-Duplikat-Befund aus `continue.md` weiterhin unbehoben, 5. veralteter Klassen-Doc-Kommentar) |
| [x] | 8b | Tests ausführen (Unteragent) | `test-results.md` (1328 gesamt, 1325 bestanden, 0 fehlgeschlagen, 2 übersprungen — 1 Fehlschlag `PseudoConsoleSessionTests` isoliert als bekannte, bereits mehrfach in dieser Session bestätigte Flakiness verifiziert) |
| [x] | – | Iteration oder Abschluss entscheiden | Iteration 1 (Multi-Plugin-Aggregation), 5 Befunde, Iteration 1 < 3 → zurück zu Schritt 6 (Iteration 2) |

## Multi-Plugin-Aggregation – Iteration 2

| Status | Schritt | Beschreibung | Artefakt |
|--------|---------|--------------|----------|
| [x] | 6 | Implementierung (Unteragent) | Codeänderungen (alle 5 Befunde behoben — unabhängig per `git diff` je Befund verifiziert; Build 0 Fehler, 32/32 gezielte Tests grün) |
| [x] | 7 | Plan-Review (Unteragent, bedingt) | `review.md` (unverändert gültig — Befunde betrafen keine Planabweichungen, übersprungen) |
| [x] | 8 | Code-Review (Unteragent) | `review-code.md` (1 Befund: Haupt-Button-Zweig ermittelt Plugin/Einstiegspunkte technisch redundant doppelt — Empfehlung: `ErmittleIdeEntryPointsAsync`-Aufruf entfernen, `aggregierteEintraege[0]` direkt nutzen) |
| [x] | 8b | Tests ausführen (Unteragent) | `test-results.md` (1328 gesamt, 1326 bestanden, 0 fehlgeschlagen, 2 übersprungen — unabhängig verifiziert) |
| [x] | – | Iteration oder Abschluss entscheiden | **Orchestrator-Entscheidung: Befund NICHT umgesetzt (begründet, s. u.), Zyklus als abgeschlossen behandelt statt Iteration 3.** Grund: Die vorgeschlagene Vereinfachung (`aggregierteEintraege[0]` statt `ErmittleIdeEntryPointsAsync`) ist zwar technisch redundanzfrei, würde aber eine unspezifizierte Verhaltensänderung einführen — `ErmittleAggregierteIdeEinstiegspunkteAsync` schluckt seit Iteration 2 (Befund 3) Fehler einzelner Plugins pro Plugin (try/catch+LogWarning, Fortsetzung mit nächstem Plugin), während `ErmittleIdeEntryPointsAsync` Fehler ungefangen an den äußeren catch-Block durchreicht (spezifische `FehlerMeldung`). Bei Übernahme des Befunds würde ein Fehlschlag des primären/priorisierten Plugins (z. B. Visual Studio) nicht mehr als Fehler angezeigt, sondern der Haupt-Button würde **still auf ein anderes Plugin ausweichen** (z. B. Visual Studio Code), da `aggregierteEintraege[0]` dann das nächste erfolgreiche Plugin wäre — ein Verhalten, das weder in `plan.md` noch in der ursprünglichen Anforderung vorgesehen ist und den Haupt-Button-Vertrag „öffnet immer direkt den ersten priorisierten Einstiegspunkt, 0 Einstiegspunkte → Fehler" verletzen würde. Die doppelte Ermittlung selbst ist der in `plan.md` („Seiteneffekte und Risiken" → „Doppelte Ermittlung beim Haupt-Button-Klick") bereits explizit dokumentierte, bewusst akzeptierte Trade-off. Für eine echte Behebung wäre eine explizite Plan-Revision nötig — dokumentiert in `continue.md`. |
| [x] | 8a | Folgeaufgaben dokumentieren | `continue.md` aktualisiert: altes Item (Mock-Duplikat) als behoben markiert, neues Item (abgelehnter Befund mit Begründung) ergänzt |
| [x] | 9 | Dokumentation erstellen (Unteragent) | `docs/help/` (6 Dateien aktualisiert — Retry nach Session-Limit-Abbruch des ersten Versuchs, der keine Änderungen hinterließ; unabhängig per `git diff --stat` und Stichprobe verifiziert) |
| [x] | 9b | README aktualisieren (Unteragent) | `README.md` (Dropdown-Beschreibung auf Multi-Plugin-Aggregation korrigiert; unabhängig per Read verifiziert) |
| [ ] | – | Feature-Verzeichnis löschen | Übersprungen — `continue.md` enthält weiterhin einen offenen Punkt (abgelehnter Befund, dokumentiert) |
| [ ] | – | Commit durchführen | – |
