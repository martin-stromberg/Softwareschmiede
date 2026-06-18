# Lifecycle fuer Codex

Dieser Skill koordiniert die vollstaendige Bearbeitung einer Kundenanforderung. Er plant und implementiert selbst nichts Fachliches; inhaltliche Aufgaben werden an Subagenten oder an die jeweils passenden Codex-Skills/Workflows delegiert.

Der Skill kann fuer eine neue oder bereits begonnene Anforderung ausgefuehrt werden. Bei einer Fortsetzung wird anhand vorhandener Artefakte ermittelt, welche Schritte bereits abgeschlossen sind.

**Eingabe:** Die Kundenanforderung wird vom Nutzer uebergeben. Bei Fortsetzung kann sie fehlen, sofern `docs/features/{branchname}/requirement.md` bereits existiert.

## Schritt 1: Branch-Name ermitteln

Fuehre `git branch --show-current` aus.

- Ist kein Branch aktiv, brich ab und informiere den Nutzer ueber den detached-HEAD-Zustand.
- Ist der Branch `main`, `master`, `develop` oder `dev`, verweigere die Arbeit und erklaere, dass Anforderungen nicht direkt auf Hauptbranches bearbeitet werden duerfen.
- Verwende den Branch-Namen als `{branchname}` fuer alle Feature-Artefakte.

## Schritt 2: Verzeichnisstruktur vorbereiten

Erstelle `docs/features/{branchname}/`, falls es noch nicht existiert.

Erstelle oder ueberschreibe `docs/features/{branchname}/todo.md` mit:

```markdown
# Aufgabenliste - Anforderungsbearbeitung

Branch: `{branchname}`

| Status | Schritt | Beschreibung | Artefakt |
|--------|---------|--------------|----------|
| [ ] | 1 | Branch-Name ermitteln | - |
| [ ] | 2 | Verzeichnisstruktur vorbereiten | `docs/features/{branchname}/` |
| [ ] | - | Einstiegspunkt ermitteln | - |
| [ ] | 3 | Anforderung uebersetzen | `requirement.md` |
| [ ] | 4 | Bestandsaufnahme | `inventory.md`, `inventory/` |
| [ ] | 5 | Umsetzungsplanung | `plan.md` |
| [ ] | 5a | Offene Punkte pruefen und ggf. Planung wiederholen | `plan.md` aktualisiert |
| [ ] | 5b | Planungscommit | - |
| [ ] | 6 | Implementierung | Codeaenderungen |
| [ ] | 7 | Plan-Review, bedingt | `review.md` |
| [ ] | 8 | Code-Review | `review-code.md` |
| [ ] | 8b | Tests ausfuehren | `test-results.md` |
| [ ] | - | Iteration oder Abschluss entscheiden | - |
| [ ] | 8a | Folgeaufgaben dokumentieren, bei Schleifenabbruch | `continue.md` |
| [ ] | 9 | Dokumentation erstellen | `docs/help/` |
| [ ] | 9b | README aktualisieren | `README.md` |
| [ ] | - | Feature-Verzeichnis loeschen | - |
| [ ] | - | Commit durchfuehren | - |
```

Markiere danach Schritt 1 und 2 als erledigt.

## Einstiegspunkt ermitteln

Pruefe die Artefakte unter `docs/features/{branchname}/`:

| Bedingung | Einstieg |
|-----------|----------|
| `requirement.md` fehlt | Schritt 3 |
| `requirement.md` vorhanden, `inventory.md` fehlt | Schritt 4 |
| `inventory.md` vorhanden, `plan.md` fehlt | Schritt 5 |
| `plan.md` hat ungeklärte offene Punkte | Schritt 5a |
| `plan.md` vorhanden, `review.md` und `review-code.md` fehlen | Schritt 5b |
| `review.md` hat Status `Offene Aufgaben vorhanden` und `continue.md` fehlt | Schritt 6 |
| `review-code.md` hat Status `Befunde vorhanden` und `continue.md` fehlt | Schritt 6 |
| `continue.md` vorhanden | Schritt 6, nur offene Punkte aus `continue.md` |
| Reviews sind gruen, Dokumentation fehlt | Schritt 9 |
| Dokumentation existiert unter `docs/help/` | Kein weiterer Schritt |

Markiere den Einstiegspunkt in `todo.md` als erledigt und ueberspringe alle vorherigen Schritte.

## Schritt 3: Anforderung uebersetzen

Delegiere an einen leichten Subagenten oder passenden Workflow:

```text
Fuehre den Workflow "translate-requirements" mit der folgenden Kundenanforderung aus:

{anforderung}

Speichere das Ergebnis als docs/features/{branchname}/requirement.md.
Die Datei soll ausschliesslich den strukturierten Output enthalten.
```

Warte auf Abschluss. Markiere Schritt 3 als erledigt.

## Schritt 4: Bestandsaufnahme

Delegiere:

```text
Fuehre den Workflow "inventory" aus.
Die uebersetzte Anforderung liegt unter docs/features/{branchname}/requirement.md.

Speichere das Ergebnis als docs/features/{branchname}/inventory.md.
Detaildokumente kommen in docs/features/{branchname}/inventory/.
Verlinke alle Detaildokumente im Hauptdokument.
```

Warte auf Abschluss. Markiere Schritt 4 als erledigt.

## Schritt 5: Umsetzungsplanung

Delegiere:

```text
Fuehre den Workflow "plan" aus.
Eingaben:
- docs/features/{branchname}/requirement.md
- docs/features/{branchname}/inventory.md
- docs/features/{branchname}/inventory/

Speichere den fertigen Plan als docs/features/{branchname}/plan.md.
```

Warte auf Abschluss. Markiere Schritt 5 als erledigt.

## Schritt 5a: Offene Punkte pruefen

Lies `plan.md` und pruefe den Abschnitt `Offene Punkte`.

- Ist der Abschnitt leer, markiere Schritt 5a als erledigt und fahre mit Schritt 5b fort.
- Gibt es offene Punkte, zeige sie dem Nutzer vollstaendig und warte.
- Gibt der Nutzer Antworten, delegiere die Planung erneut und beruecksichtige die Antworten.
- Signalisiert der Nutzer, dass keine weiteren Antworten folgen, fahre mit Schritt 5b fort.

Auftrag fuer erneute Planung:

```text
Fuehre den Workflow "plan" erneut aus.
Beruecksichtige diese Antworten auf offene Punkte:

{antworten}

Eingaben:
- docs/features/{branchname}/requirement.md
- docs/features/{branchname}/inventory.md
- docs/features/{branchname}/inventory/

Speichere den aktualisierten Plan als docs/features/{branchname}/plan.md.
```

Wiederhole Schritt 5a, bis keine klaerbaren offenen Punkte verbleiben oder der Nutzer den Fortgang bestaetigt.

## Schritt 5b: Planungscommit

Stage alle Dateien unter `docs/features/{branchname}/`.

Erstelle einen Commit:

```powershell
git add docs/features/{branchname}/
git commit -m "plan: {kurze Beschreibung der geplanten Anforderung}"
```

Wenn `git status` keine Aenderungen zeigt, ueberspringe den Commit. Markiere Schritt 5b als erledigt.

## Schritte 6 bis 8b: Implementierungs- und Review-Schleife

Die Schleife laeuft maximal drei Iterationen. Fuehre intern:

- Iterationszaehler, Start bei 1
- Anzahl offener Punkte der letzten Iteration, Start bei unendlich

### Schritt 6: Implementierung

Delegiere:

```text
Fuehre den Workflow "implement" aus.
Eingaben:
- docs/features/{branchname}/plan.md
- docs/features/{branchname}/requirement.md
- docs/features/{branchname}/inventory.md
- docs/features/{branchname}/inventory/

Falls docs/features/{branchname}/continue.md existiert, bearbeite ausschliesslich die dort offenen Punkte.

Andernfalls, falls Reviews vorliegen, bearbeite ausschliesslich:
- offene Planelemente aus docs/features/{branchname}/review.md
- Code-Befunde aus docs/features/{branchname}/review-code.md
```

Warte auf Abschluss. Markiere Schritt 6 als erledigt.

Bricht der Implementierungsagent wegen Widerspruch, ungeklaerter technischer Frage oder aehnlichem ab, brich den gesamten Ablauf ab und informiere den Nutzer.

### Schritt 7: Plan-Review

Ueberspringe Schritt 7, wenn `review.md` bereits den Status `Vollständig umgesetzt` traegt.

Wenn `review.md` existiert, benenne sie vor dem neuen Review in die naechste freie Datei `review.{n}.md` um.

Delegiere:

```text
Fuehre den Workflow "review-plan" aus.
Eingaben:
- docs/features/{branchname}/plan.md
- docs/features/{branchname}/inventory.md
- docs/features/{branchname}/inventory/

Speichere das Ergebnis als docs/features/{branchname}/review.md.
```

Warte auf Abschluss. Markiere Schritt 7 als erledigt.

### Schritt 8: Code-Review

Wenn `review-code.md` existiert, benenne sie vor dem neuen Review in die naechste freie Datei `review-code.{n}.md` um.

Delegiere:

```text
Fuehre den Workflow "review-code" aus.
Speichere das Ergebnis als docs/features/{branchname}/review-code.md.
```

Warte auf Abschluss. Markiere Schritt 8 als erledigt.

### Schritt 8b: Tests ausfuehren

Delegiere:

```text
Fuehre den Workflow "run-tests" aus.
Speichere das Ergebnis als docs/features/{branchname}/test-results.md.
```

Warte auf Abschluss. Markiere Schritt 8b als erledigt.

### Iteration oder Abschluss entscheiden

Lies `review.md`, `review-code.md` und `test-results.md`.

Zaehle offene Punkte dieser Iteration:

- offene Eintraege in `review.md`
- Befunde in `review-code.md`
- fehlgeschlagene Tests in `test-results.md`

Wenn `continue.md` vorhanden ist, markiere erledigte Eintraege als `- [x]`. Sind alle erledigt, benenne `continue.md` in `continue-done.md` um und markiere Schritt 10 als erledigt, falls er existiert.

Entscheidung:

| Bedingung | Aktion |
|-----------|--------|
| `Vollständig umgesetzt`, `Keine Befunde`, `Keine Fehler` | Schleife erfolgreich beenden, weiter mit Schritt 9 |
| Iterationszaehler < 3 und offene Punkte < offene Punkte der letzten Iteration | Fortschritt erkannt, Zaehler erhoehen, zurueck zu Schritt 6 |
| Iterationszaehler = 3 oder offene Punkte >= offene Punkte der letzten Iteration | Schleife abbrechen, weiter mit Schritt 8a |

## Schritt 8a: Folgeaufgaben dokumentieren

Erstelle oder ueberschreibe `docs/features/{branchname}/continue.md`:

```markdown
# Offene Aufgaben

Erstellt am: {heutiges Datum}
Abbruchgrund: {Maximale Iterationsanzahl erreicht | Kein Fortschritt zwischen den letzten zwei Iterationen}

Die folgenden Aufgaben konnten im automatisierten Zyklus nicht abgeschlossen werden
und muessen manuell oder in einem erneuten Lauf bearbeitet werden.

## Offene Planelemente

{Eintraege aus review.md als Checkboxen}

## Code-Review-Befunde

{Befunde aus review-code.md als Checkboxen}

## Fehlgeschlagene Tests

{Tests aus test-results.md als Checkboxen}
```

Markiere Schritt 8a als erledigt.

Fuege in `todo.md` vor der letzten Tabellenzeile `Commit durchfuehren` diese Zeile ein:

```markdown
| [ ] | 10 | Nacharbeiten abschliessen, offene Punkte aus `continue.md` | `continue-done.md` |
```

Fahre mit Schritt 9 fort.

## Schritt 9: Dokumentation

Wenn dieser Lauf wegen vorhandener `continue.md` bei Schritt 6 gestartet wurde und Dokumentation bereits existiert:

- Pruefe, ob die Korrekturen grundlegende Programmlogik veraendert haben.
- Wenn nicht, ueberspringe Schritt 9 und markiere ihn als erledigt.

Andernfalls delegiere:

```text
Fuehre den Workflow "update-docs" aus.
Eingaben:
- docs/features/{branchname}/requirement.md
- docs/features/{branchname}/plan.md
```

Warte auf Abschluss. Markiere Schritt 9 als erledigt.

## Schritt 9b: README aktualisieren

Wenn Schritt 9 uebersprungen wurde, ueberspringe auch Schritt 9b und markiere ihn als erledigt.

Andernfalls delegiere:

```text
Fuehre den Workflow "update-readme" aus.
Eingaben:
- docs/features/{branchname}/requirement.md
- docs/features/{branchname}/plan.md
```

Warte auf Abschluss. Markiere Schritt 9b als erledigt.

## Feature-Verzeichnis loeschen

Pruefe, ob alle Tabellenzeilen in `todo.md` ausser `Feature-Verzeichnis loeschen` und `Commit durchfuehren` erledigt sind.

Wenn ja:

1. Markiere `Feature-Verzeichnis loeschen` und `Commit durchfuehren` als erledigt.
2. Loesche das Feature-Verzeichnis ueber Git:

```powershell
git rm -r docs/features/{branchname}/
```

Die Planungsartefakte bleiben durch den Planungscommit in der Git-Historie erhalten.

## Abschluss: Commit durchfuehren

Stage alle geaenderten und neuen Dateien, die zur Anforderung gehoeren. Das Feature-Verzeichnis ist bereits durch den vorherigen Schritt gestaged.

Erstelle einen Commit:

```powershell
git commit -m "feat: {kurze Beschreibung der umgesetzten Anforderung}"
```

Informiere den Nutzer, dass die Anforderung abgeschlossen und committed wurde.

## Automatische Fortsetzung bei verbleibender continue.md

Pruefe, ob `docs/features/{branchname}/continue.md` noch existiert.

- Nicht vorhanden: kein weiterer Schritt.
- Vorhanden und dieser Lauf war bereits eine Fortsetzung: Informiere den Nutzer, dass offene Punkte verbleiben und manuelle Intervention noetig ist.
- Vorhanden und dieser Lauf war kein Fortsetzungslauf: Starte den Lifecycle erneut ohne neue Anforderung und bearbeite die offenen Punkte aus `continue.md`.

## Hinweise zur Ausfuehrung in Codex

- Ersetze `{branchname}`, `{anforderung}`, `{antworten}` und Beschreibungen immer durch echte Werte.
- Nutze Subagenten, wenn die Umgebung sie bereitstellt. Entdecke entsprechende Tools bei Bedarf ueber die verfuegbaren Multi-Agent-Tools.
- Wenn keine Subagenten verfuegbar sind, fuehre den passenden Workflow selbst aus und halte dich an dieselben Artefaktpfade.
- Der Abbruch eines Implementierungsschritts ist kein Ablauf-Fehler, sondern ein Signal fuer eine menschliche Entscheidung.
