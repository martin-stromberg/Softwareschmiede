# Aufgabenliste - Anforderungsbearbeitung

Branch: `task/5c1a753c335f441294221c1e260a87c3-prereleases`

| Status | Schritt | Beschreibung | Artefakt |
|--------|---------|--------------|----------|
| [x] | 1 | Branch-Name ermitteln | - |
| [x] | 2 | Verzeichnisstruktur vorbereiten | `docs/features/task/5c1a753c335f441294221c1e260a87c3-prereleases/` |
| [x] | - | Einstiegspunkt ermittelt | - |
| [x] | 3 | Anforderung uebersetzen (Unteragent) | `requirement.md` |
| [x] | 4 | Bestandsaufnahme (lokaler Workflow, kein Unteragent verfügbar) | `inventory.md`, `inventory/` |
| [x] | 5 | Umsetzungsplanung erneut ausgefuehrt; P-01 bis P-04 und T-01 bis T-08 eingearbeitet (lokaler Workflow) | `plan.md`, `../5c1a753c335f441294221c1e260a87c3-prereleases-tasks.md` |
| [x] | 5a | Offene Punkte pruefen und ggf. Planung wiederholen | `plan.md` (aktualisiert) |
| [x] | 5b | Aktualisierten Plan erneut gegen Anforderung und Testbedarf pruefen (T-09-Nachplanung) — Plan vollstaendig | `plan-check.md` |
| [x] | 5c | Planungscommit | - |
| [x] | 6 (It. 1) | Implementierung (Unteragent): U-01..U-07 committed (Modell/Persistenz/SemVer, Release-Client/UpdateService, Settings-UI, Startfluss, Installationskern, E2E-Infrastruktur, Szenarien E-01..E-07) | Codeaenderungen |
| [x] | 7 (It. 1) | Plan-Review (Unteragent) — Status: Offene Aufgaben vorhanden | `review.1.md` |
| [x] | 8 (It. 1) | Usability-Review (Unteragent, UI-Aenderungen vorhanden) — 1 Befund (kein sichtbares Feedback bei manueller Pruefung ohne Update) | `review-usability.1.md` |
| [x] | 9 (It. 1) | Code-Review (Unteragent) — 6 Befunde (alle niedrig) | `review-code.1.md` |
| [x] | 6 (It. 2) | Review-Befunde behoben: Feedback-Text bei manueller Pruefung, HTTPS-Asset-URLs, Race-sicherer Fortschritts-Endzustand, E2E-Robustheit (Cleanup, Nav-Wiederholung, Stream-Gate-Timeout), RC-Paketpfad-Unit-Test — committed (1714e2a + Folgecommit) | Codeaenderungen |
| [x] | 7 (It. 2) | Plan-Review erneut (Unteragent) — Status: Vollstaendig umgesetzt | `review.md` |
| [x] | 8 (It. 2) | Usability-Review erneut (Unteragent) — Keine Befunde | `review-usability.md` |
| [x] | 9 (It. 2) | Code-Review erneut (Unteragent) — 10 Befunde (alle niedrig) | `review-code.2.md` |
| [x] | 6 (It. 3) | Review-Befunde behoben: IUpdateVersuchProtokoll + MainWindowUpdateDienste-Bundle, UpdateReleaseLookupResult-Vertrag, TestDbContextFactory.CreateSqlite, CreateSut-Bereinigung, MainWindowViewModelUpdateTestBase, geteilte Test-Helpers, Gate-Warte-Dedup, E2E-Cleanup, GetOfferedUpdateVersion (Rohwert korrekt — Befund 10 basierte auf falscher UIA-Annahme), FlaUI-Klick-Occlusion-Fix (ClickInForeground-Sweep ueber ~120 Stellen) | Codeaenderungen |
| [x] | 7 (It. 3) | Plan-Review uebersprungen — `review.md` traegt bereits `Vollstaendig umgesetzt` | `review.md` |
| [~] | 8 (It. 3) | Usability-Review erneut (Unteragent) — ausstehend | `review-usability.md` |
| [~] | 9 (It. 3) | Code-Review erneut (Unteragent) — ausstehend | `review-code.md` |
| [x] | 10 | Tests ausgefuehrt und dokumentiert — regulaere Spur gruen (1629/0 Fehler); OsInterface-Spur: 47/50 gruen, RunGeneralTests komplett durchgelaufen (alle Update-Szenarien), einziger Fehler feature-unabhaengiger Clipboard-Ressourcenkonflikt | `test-results.md` |
| [~] | - | Iteration oder Abschluss entscheiden — wartet auf It.-3-Reviews | - |
| [ ] | 11 | Folgeaufgaben dokumentieren (bei Schleifenabbruch) | `continue.md` |
| [ ] | 12 | Dokumentation erstellen (Unteragent) | `docs/help/` |
| [ ] | 12b | README aktualisieren (Unteragent) | `README.md` |
| [ ] | 12c | Release Notes aktualisieren (Unteragent) | `docs/RELEASE_NOTES.md` |
| [ ] | - | Feature-Verzeichnis loeschen | - |
| [ ] | - | Commit durchfuehren | - |
