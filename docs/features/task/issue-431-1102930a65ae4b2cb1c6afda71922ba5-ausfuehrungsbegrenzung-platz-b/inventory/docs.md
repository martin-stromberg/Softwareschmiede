# Dokumentationsstellen

Verifizierte Fundstellen, die die Ausführungsbegrenzung und insbesondere die bisherige
Pausen-Freigabe beschreiben (Stand vor der Umsetzung). Alle Stellen nennen aktuell
ausschließlich die Pausen-Freigabe — die Ready-/Pending-Freigabe ist noch nirgends dokumentiert.

## Aus der Anforderung genannt

| Datei | Zeile(n) | Inhalt |
|-------|----------|--------|
| `README.md` | ~30 | Feature-Übersicht: „…begrenzbarer Parallelität (global und je KI-Plugin…)… eine pausierte Aufgabe belegt trotz weiterlaufendem Prozess keinen Platz — ihr Slot gilt ab dem Setzen der Pause als frei". |
| `README.md` | ~187 | Abschnitt „Aufgaben pausieren…": pausierte Aufgabe belegt keinen Platz in beiden Zählern; Startprüfung ohne Poll; Rückkehr zur Belegung nach Pausenablauf. |
| `docs/help/einstellungen/beschreibung.md` | ~75 | Feld „Maximale parallele Ausführungen" (global + je Plugin-Gruppe „Ausführungsbegrenzung"); Pausen-Freigabe beschrieben. |
| `docs/help/plugins/github-ereignisse/beschreibung.md` | ~11 | Funktionsbeschreibung: „Eine pausierte Aufgabe belegt außerdem keinen Platz in der Ausführungsbegrenzung…". |
| `docs/help/plugins/github-ereignisse/ablauf-technisch.md` | ~33 | `ScmStartCheckService`/`ScmStartCheckTrigger`: Liste der bestehenden Auslöser (`RunningCountChanged`, Pausenpfade, `CancelAsync`). |
| `docs/help/plugins/github-ereignisse/ablauf-technisch.md` | ~34 | `ScmEventExecutionLimitService`: Belegungszählung inkl. `PausedUntilUtc`-Filter, Reservierungssemantik. |
| `docs/help/aufgaben/ablauf-technisch.md` | ~521 | „Gegenläufige Wirkung auf die SCM-Ausführungsbegrenzung": `GetOccupancyAsync`-Pausenfilter, `SetPauseAsync` → Startprüfung. |
| `docs/help/plugins/github-ereignisse/business-rules.md` | ~99–107 | Abschnitt „Ausführungsbegrenzung und Priorisierung": zweistufige Limits, Reihenfolge, „Pausierte Aufgaben belegen keinen Platz" (Z. 105 ausführlich inkl. Reservierungsfenster und `Waiting`-Hinweis). |
| `docs/help/plugins/github-ereignisse/business-rules.md` | ~137 | `CancelAsync` → `ScmStartCheckTrigger` (Auslöser-Liste der Startprüfung). |

## Zusätzlich gefundene betroffene Stellen

| Datei | Zeile(n) | Inhalt |
|-------|----------|--------|
| `docs/help/aufgaben/business-rules.md` | ~254 | Pausen-Referenzdefinition `IsCurrentlyPaused` inkl. Liste der Auswertungsstellen („SCM-Limit-Belegung"). |
| `docs/help/aufgaben/business-rules.md` | ~258 | „Eine pausierte Aufgabe belegt keinen Platz in der SCM-Ausführungsbegrenzung" (kurze Variante mit Verweis). |
| `docs/help/aufgaben/datenmodell.md` | ~36 | `PausedUntilUtc`-Modellierung; erwähnt `IsCurrentlyPaused` als Referenz. |

## XML-Dokumentation im Code (ebenfalls betroffen laut Anforderung)

| Datei | Zeile(n) | Inhalt |
|-------|----------|--------|
| `src/Softwareschmiede/Application/Services/ScmEventExecutionLimitService.cs` | 6–19 | `CliExecutionOccupancy`-Kommentar: „Pausierte Aufgaben … belegen … keinen Platz". |
| `src/Softwareschmiede/Application/Services/ScmEventExecutionLimitService.cs` | 185–188 | `GetOccupancyAsync`-Kommentar: Pausenfilter-Semantik. |
| `src/Softwareschmiede/Application/Services/ScmStartCheckService.cs` | 8–16, 26–35 | `ScmStartCheckTrigger`- und `ScmStartCheckService`-Kommentare: Auslöser-Liste ohne den neuen Wartezustands-Auslöser. |
