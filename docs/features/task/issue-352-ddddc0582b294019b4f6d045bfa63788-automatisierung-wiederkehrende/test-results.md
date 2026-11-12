# Test-Ergebnisse

## Ergebnis

**Status:** Fehler vorhanden

Klarstellung: Es ist **kein ausgeführter Check fehlgeschlagen** — alle durchführbaren Verifikationen (YAML-Parse, Strukturchecks, PowerShell-Parser, Live-Read-only-Checks, zeilenexakter Body-Abgleich) sind bestanden. Der Status `Fehler vorhanden` ergibt sich ausschließlich daraus, dass die reale End-to-End-Abnahme vor dem Merge **nicht durchführbar** ist (Begründung im Abschnitt „Reale End-to-End-Abnahme") und gemäß Lifecycle-Regel wie ein offener/fehlgeschlagener Test geführt wird, bis der Anwender den Post-Merge-Probelauf abgenommen oder dokumentiert verzichtet hat.

## Durchgeführte Verifikationen

Feature ist reine CI-Infrastruktur (4 Workflow-Dateien + Doku), keine C#-Änderungen — das reguläre `dotnet test` ist nicht betroffen; die Baseline wurde in `inventory/test-results/baseline-regular-tests.log` grün protokolliert (2289 Tests: 2288 bestanden, 1 übersprungen). Stattdessen wurden die folgenden realen Checks ausgeführt:

### 1. YAML-Parse und Strukturchecks (PyYAML 6.0.3)

| Datei | YAML-Parse | UTF-8 | Struktur |
|-------|-----------|-------|----------|
| `.github/workflows/create-review-issue.yml` | OK | UTF-8 ohne BOM | `on: workflow_call` mit 5 required Inputs (`label`, `label-description`, `title`, `period`, `body`, alle `type: string`) + required Secret `REVIEW_ISSUE_TOKEN`; 1 Job `ubuntu-latest`, `timeout-minutes: 10`, 3 `pwsh`-Schritte (Guard → Label → Duplikatprüfung+Erzeugung) |
| `.github/workflows/review-architecture-security.yml` | OK | UTF-8 ohne BOM | `uses: ./.github/workflows/create-review-issue.yml` (Pfad existiert); alle 5 required Inputs befüllt, keine Extra-Inputs; `secrets.REVIEW_ISSUE_TOKEN` gemappt; Job-`permissions: issues: write`; Cron `0 6 1 * *` + `workflow_dispatch`; `concurrency.group: review-architecture-security`, `cancel-in-progress: false`; `period: month`; Label `automated-review-architecture` (`automated-*`-Konvention); erste Body-Zeile frei stehend `@softwarewerkstatt` |
| `.github/workflows/review-nuget-packages.yml` | OK | UTF-8 ohne BOM | Wie oben; Cron `0 6 * * 1` + `workflow_dispatch`; `concurrency.group: review-nuget-packages`; `period: isoweek`; Label `automated-review-nuget`; erste Body-Zeile frei stehend `@softwarewerkstatt` |
| `.github/workflows/review-compliance.yml` | OK | UTF-8 ohne BOM | Wie oben; Cron `0 6 1 1 *` + `workflow_dispatch`; `concurrency.group: review-compliance`; `period: year`; Label `automated-review-compliance`; erste Body-Zeile frei stehend `@softwarewerkstatt` |

Ergebnis: **Alle 4 Dateien bestanden** — keine fehlenden/überzähligen Inputs, Secrets korrekt gemappt, `uses`-Pfad auflösbar, Crons exakt wie geplant, `cancel-in-progress: false` überall.

### 2. PowerShell-Parser-Prüfung (`System.Management.Automation.Language.Parser::ParseInput`, pwsh 7.x)

| `run`-Block in `create-review-issue.yml` | Ergebnis |
|------------------------------------------|----------|
| `Check REVIEW_ISSUE_TOKEN secret` | PASS — keine Parser-Fehler |
| `Ensure review label exists` | PASS — keine Parser-Fehler |
| `Create review issue if none is open` | PASS — keine Parser-Fehler |

### 3. actionlint

**Nicht verfügbar** — `actionlint` ist auf dem System nicht installiert (`command not found`); gemäß Vorgabe nicht installiert.

### 4. Live-Read-only-Check gegen `martin-stromberg/Softwareschmiede`

`gh` ist als `softwarewerkstatt` authentifiziert (aktiver Account; zusätzlich `martin-stromberg` via Keyring). Ausgeführte Abfragen (read-only, keine Issues/Labels angelegt):

```
gh issue list --repo martin-stromberg/Softwareschmiede --state open --label automated-review-architecture --json number  → []
gh issue list --repo martin-stromberg/Softwareschmiede --state open --label automated-review-nuget --json number         → []
gh issue list --repo martin-stromberg/Softwareschmiede --state open --label automated-review-compliance --json number    → []
```

Ergebnis: **Bestanden** — aktuell existiert kein offenes Blocker-Issue eines der drei Prüftypen; die Duplikatprüfung ist korrekt formuliert (leere Treffermenge → Erzeugung würde laufen).

### 5. Zeilenexakter Body-Abgleich gegen `gh issue view 352`

Die drei `Automatisch erzeugtes Issue:`-Blöcke aus Issue #352 (martin-stromberg/Softwareschmiede, live gelesen) wurden zeilenexakt mit den `body`-Inputs der drei Aufrufer verglichen:

| Aufrufer | Vergleich mit Issue-#352-Block | Ergebnis |
|----------|-------------------------------|----------|
| `review-architecture-security.yml` | Block 1 (Architektur-/Sicherheitsreview, 9 Prüfpunkte) | MATCH — zeilenexakt identisch |
| `review-nuget-packages.yml` | Block 2 (NuGet-Prüfung, 6 Prüfpunkte) | MATCH — zeilenexakt identisch |
| `review-compliance.yml` | Block 3 (Compliance-Review, 9 Prüfpunkte) | MATCH — zeilenexakt identisch |

Erste Zeile je Body ist frei stehend `@softwarewerkstatt` (kein führendes/nachfolgendes Zeichen).

## Fehlgeschlagene Tests

### Reale End-to-End-Abnahme

- **Akzeptanzszenarien 1–5 (`plan.md`)** — Offen: `Nicht durchführbar` vor dem Merge (Begründung im Abschnitt „Reale End-to-End-Abnahme"); Auflösung über Post-Merge-Probelauf (manuelle Abnahme durch Anwender) oder dokumentierten Verzicht.

## E2E-Abdeckung

| Szenario | Test / Testklasse | Ergebnis |
|----------|-------------------|----------|
| App-seitiger Benutzerfluss | — | Nicht erforderlich (keine App-UI-/Code-Änderung; Begründung in `plan.md` „E2E-Tests" und `plan-check.md` bestätigt) |
| Review-Zyklus end-to-end (Cron/Dispatch → Issue mit Owner-Autor → Mention-Notification → SCM-Ereignis → Aufgabe) | Reale Abnahme via `workflow_dispatch`-Probelauf (Rollout-Schritt 6) | Nicht ausgeführt — vor Merge nicht durchführbar, siehe unten |
| Duplikat-/Guard-/Kollisionspfade (Szenarien 2–4) | `workflow_dispatch`-Probeläufe | Nicht ausgeführt — vor Merge nicht durchführbar, siehe unten |

Der App-seitige Vertrag (Owner-Autor + frei stehende `@softwarewerkstatt`-Erwähnung wird akzeptiert) bleibt unverändert und ist durch bestehende, in der Baseline grüne Tests abgesichert (`ScmEventValidationServiceTests.SeparatesKonto_AuthorOrEditorMustBeOwner_NotViewer`, `ScmEventMonitoringServiceTests_Notifications.*`); die erzeugten Issues reproduzieren exakt diese getestete Eingangsgestalt (verifiziert: Body-Abgleich oben, Owner-Autorschaft via `REVIEW_ISSUE_TOKEN` erzwungen).

## Reale End-to-End-Abnahme

Betroffener Bereich: **Externe Dienste/Integrationen** (GitHub Actions, GitHub Issues, Mention-Notifications) → reale Abnahme grundsätzlich erforderlich.

| Szenario | Durchgeführt über | Ergebnis |
|----------|-------------------|----------|
| 1. Happy Path: Zyklus erzeugt Review-Issue → Mention → Ereignis → Aufgabe | Workflow-Lauf (`schedule`/`workflow_dispatch`) + `gh issue view` + SCM-Ereignisübersicht in der App | Nicht durchführbar |
| 2. Duplikatvermeidung (zweiter Lauf → „Skipped") | Zweiter `workflow_dispatch`-Lauf, Actions-Log | Nicht durchführbar |
| 3. Fehlendes `REVIEW_ISSUE_TOKEN` → klarer Fehlschlag | `workflow_dispatch` ohne Secret, Actions-Log | Nicht durchführbar |
| 4. Jahres-Kollision 1. Januar (monatlich + jährlich parallel) | Zwei `workflow_dispatch`-Probeläufe | Nicht durchführbar |
| 5. Fehlende Collaborator-Einladung `@softwarewerkstatt` | Dokumentierter Troubleshooting-Fall | Nicht durchführbar |

**Begründung „Nicht durchführbar":**

1. **`workflow_dispatch`/`schedule` benötigen die Workflow-Datei auf dem Default-Branch (`main`).** Der Merge `staging → main` steht noch aus — die Workflows sind auf GitHub noch nicht auslösbar (Plattformverhalten, in `plan.md` Seiteneffekte dokumentiert).
2. **Das Repository-Secret `REVIEW_ISSUE_TOKEN` ist noch nicht hinterlegt** (einmalige Betriebshandlung des Owners, `plan.md` Rollout-Voraussetzungen Schritte 1–2). Ohne Secret würde jeder Lauf am Guard-Schritt scheitern — ein jetziger Probelauf könnte also nur Szenario 3 prüfen, erfordert aber ebenfalls die Datei auf `main`.
3. **Ein Test-Issue mit echter `@softwarewerkstatt`-Erwähnung würde produktiv eine Review-Aufgabe in der laufenden Anwendung auslösen** — kein isolierbarer Test, sondern echte produktive Wirkung; zudem würde ein offenes Test-Issue künftige echte Zyklen über die Duplikatprüfung blockieren.

**Auflösungsweg (geplant):** Post-Merge-Probelauf gemäß `plan.md` „Rollout-Voraussetzungen" Schritt 4 bzw. Umsetzungsreihenfolge Schritt 6: PAT anlegen → `REVIEW_ISSUE_TOKEN` hinterlegen → Collaborator-Status prüfen → je Workflow `workflow_dispatch`-Probelauf mit Autor-/Label-/Body-Prüfung (`gh issue view`) und Re-Lauf auf „Skipped"-Verhalten → Ereigniseingang in der App beobachten. Das entspricht der manuellen Abnahme durch den Anwender; Ergebnis dann als `Bestanden (manuell, {Datum})` nachzutragen. Bis dahin bleibt dieses Szenario formal offen — die Entscheidung (manuelle Abnahme vs. dokumentierter Verzicht) liegt beim Anwender.

## Zusammenfassung

- Gesamt: 19 (13 Einzelchecks + 1 Verfügbarkeitsprüfung + 5 Abnahme-Szenarien)
- Bestanden: 13 (4× YAML-Parse/Struktur + 3× pwsh-Parser + 3× Live-Label-Checks + 3× Body-Abgleich)
- Fehlgeschlagen: 0 (kein ausgeführter Check fehlgeschlagen)
- Übersprungen: 1 (actionlint — nicht verfügbar)
- Offen / Nicht durchführbar: 5 (reale Abnahme, Akzeptanzszenarien 1–5 — blockiert formal den Abschluss bis Post-Merge-Probelauf oder dokumentierter Verzicht)

## Testabdeckung

**Abdeckung:** Nicht messbar — das Feature enthält keinen ausführbaren Code (nur GitHub-Actions-YAML und Markdown). Reguläre Test-Baseline (`dotnet test`, reguläre Lane) liegt grün vor: 2289 Tests, 2288 bestanden, 1 übersprungen (`inventory/test-results/baseline-regular-tests.log`, TRX `inventory-baseline-regular-tests.trx`).

## Fehlende Tests

Keine — es wurden keine Quelldateien geändert oder angelegt. Für GitHub-Actions-Workflows existiert im Repo keine Testinfrastruktur (inventory/tests.md); die Verifikation folgt der Repo-Konvention via `workflow_dispatch`-Probelauf nach dem Merge (siehe „Reale End-to-End-Abnahme").
