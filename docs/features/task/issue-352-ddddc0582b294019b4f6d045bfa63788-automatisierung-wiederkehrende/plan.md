# Umsetzungsplan: Automatisierung wiederkehrender Architektur-, Sicherheits- und Compliance-Reviews (Issue #352)

## Übersicht

Drei eigenständige, zeitgesteuerte GitHub-Actions-Workflows unter `.github/workflows/` legen je Review-Zyklus ein GitHub-Issue mit frei stehender `@softwarewerkstatt`-Erwähnung und festem Prüfauftrag an — monatlich (Architektur-/Sicherheitsreview), wöchentlich (NuGet-Paketprüfung) und jährlich (Compliance-/Copyright-Review). Jedes Issue entsteht unter der Identität des Repository-Owners über ein neues Repository-Secret `REVIEW_ISSUE_TOKEN` (Owner-PAT), damit die SCM-Ereignisvalidierung die Erwähnung akzeptiert; eine Label-basierte Duplikatprüfung verhindert parallele offene Issues desselben Prüftyps. Betroffen sind ausschließlich `.github/workflows/` sowie `docs/CI_CD.md` und `docs/help/plugins/github-ereignisse/troubleshooting.md` — kein C#-Code.

## Scope-Klärung

| Feld | Festlegung |
|------|------------|
| Verarbeitungseinheit | Ein *Review-Zyklus* je Prüftyp = das fällige Anlegen genau eines Issues eines Typs (`automated-review-architecture`, `automated-review-nuget`, `automated-review-compliance`). Der Zyklus gilt fachlich als abgeschlossen, sobald das Issue geschlossen ist; solange eines offen ist, wird kein neuer gestartet. |
| Auslösende Ereignisse | `schedule`-Cron (UTC) je Workflow: `0 6 1 * *` monatlich am 1. Tag 06:00 UTC; `0 6 * * 1` wöchentlich montags 06:00 UTC (2 h versetzt zum `security-scan.yml` Mo 04:00 UTC); `0 6 1 1 *` jährlich am 1. Januar 06:00 UTC. Zusätzlich `workflow_dispatch` in allen drei Workflows (Repo-Konvention, Test-/Nachholbedarf). |
| Erwartete Wirkung | Pro fälligem Zyklus ohne offenes Issue desselben Typs: ein neues GitHub-Issue mit datumsbezogenem Titel, Typlabel und dem in Issue #352 definierten Body (erste Zeile frei stehend `@softwarewerkstatt`). Autor = Repository-Owner `martin-stromberg` → Mention-Notification an `@softwarewerkstatt` → SCM-Ereignisüberwachung (`SeparatesKonto`, `reason = mention`) erzeugt Ereignis und stößt die Aufgabenbearbeitung an. Sichtbar außerdem: Workflow-Lauf in Actions mit Log „Created review issue …" bzw. „Skipped: an open review issue already exists (#N)". |
| Umgang mit fehlenden Objekten | *Offenes Issue mit Typlabel vorhanden:* Zyklus **verwerfen** — kein neues Issue, erfolgreicher Lauf mit Hinweis im Log. *Typlabel existiert nicht:* **erzeugen** (idempotent `gh label create --force` unter `GITHUB_TOKEN`). *`REVIEW_ISSUE_TOKEN`-Secret fehlt/leer:* Workflow **klar fehlschlagen** (Guard-Schritt, `exit 1` mit Anleitung) — kein Fallback auf `GITHUB_TOKEN`, weil ein `github-actions[bot]`-Issue von der Ereignisvalidierung verworfen würde (still nutzlos). *`@softwarewerkstatt` ohne Repo-Zugriff:* Issue entsteht, aber GitHub liefert keine Mention-Notification — **Betriebsvoraussetzung** (Collaborator-Einladung), dokumentiert, nicht implementiert. |

### Scope-Ausschlüsse

| # | Einschränkung / Ausschluss | Begründung | Status |
|---|----------------------------|------------|--------|
| 1 | Workflows führen keine inhaltliche Prüfung durch — sie erstellen nur die Auftrags-Issues | Analyse obliegt der nachgelagerten Automatisierung (`@softwarewerkstatt`); explizit so gefordert | bestätigt (2026-11-12, Orchestrierung: direkte Ableitung der Anforderung bzw. nicht geforderter Zusatzumfang; finale Bestätigung via PR-Review) |
| 2 | Keine Änderung am bestehenden `security-scan.yml` | Der wöchentliche Abhängigkeits-Scan (Artefakt `vulnerable-packages-scan`) bleibt unverändert; das NuGet-Review-Issue ist eine zusätzliche, fachlich andere Wirkung (agentische Analyse statt Scan-Log) | bestätigt (2026-11-12, Orchestrierung: direkte Ableitung der Anforderung bzw. nicht geforderter Zusatzumfang; finale Bestätigung via PR-Review) |
| 3 | Keine C#-Codeänderungen, keine UI, keine `Softwareschmiede.Tests`-Änderungen | Lösung ist reine CI-/Automatisierungsinfrastruktur (Workflow-YAML + Doku) | bestätigt (2026-11-12, Orchestrierung: direkte Ableitung der Anforderung bzw. nicht geforderter Zusatzumfang; finale Bestätigung via PR-Review) |
| 4 | Keine Eskalations-/Erinnerungslogik bei offenen Review-Issues | Nicht gefordert; bei offenem Issue wird schlicht übersprungen | bestätigt (2026-11-12, Orchestrierung: direkte Ableitung der Anforderung bzw. nicht geforderter Zusatzumfang; finale Bestätigung via PR-Review) |
| 5 | Kein Schließen/Verwalten der erzeugten Issues durch die Workflows | Lebenszyklus der Issues liegt bei der nachgelagerten Bearbeitung bzw. dem Owner | bestätigt (2026-11-12, Orchestrierung: direkte Ableitung der Anforderung bzw. nicht geforderter Zusatzumfang; finale Bestätigung via PR-Review) |

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Issue-Autorschaft (Owner-Prüfung) | Neues Repository-Secret `REVIEW_ISSUE_TOKEN` = feingranulierter PAT des Owners `martin-stromberg`, beschränkt auf dieses Repository, Permission **Issues: Read and write**. Alle `gh issue`-Aufrufe im Erzeugungsschritt laufen unter `GH_TOKEN: ${{ secrets.REVIEW_ISSUE_TOKEN }}` → Autor = Owner, `ScmEventValidationService.GetRejection` (Owner-Prüfung `src/Softwareschmiede/Application/Services/ScmEventValidationService.cs:56-57`) akzeptiert. | `GITHUB_TOKEN` erzeugt Autor `github-actions[bot]` → Ablehnung „Textversion stammt nicht vom Repository-Owner" (verifiziert in inventory/logic.md). GitHub-App-Token erzeugt ebenfalls Bot-Autor (`<app>[bot]`) und löst das Problem nicht. Der verifizierte Editor-Umweg (Bot-Issue + Owner-Edit) wäre fragil und versteckt. PAT ist die einzige belastbare Lösung, die den Owner zum Autor macht. |
| Verhalten bei fehlendem/abgelaufenem Secret | Guard-Schritt als erster Job-Schritt: leeres `${{ secrets.REVIEW_ISSUE_TOKEN }}` → `Write-Error` + `exit 1` mit Anleitung (Secret anlegen, Verweis `docs/CI_CD.md`). **Kein** Fallback auf `github.token`. | Ein Fallback würde ein formal korrektes, aber von der Ereignisüberwachung verworfenes Issue erzeugen — schlimmer als ein klarer Fehlschlag, weil die Duplikatprüfung künftige Zyklen dann gegen das unbrauchbare Issue blockiert. |
| Duplikaterkennung „desselben Typs" | Ein typspezifisches `automated-*`-Label je Workflow (`automated-review-architecture`, `automated-review-nuget`, `automated-review-compliance`, Farbe `#5319E7`), idempotent per `gh label create ... --force` unter `GITHUB_TOKEN` vorab angelegt; Prüfung via `gh issue list --state open --label <label> --json number`. | Repo-Konvention `automated-*` (vgl. `automated-backmerge`, `automated-promotion`); Label ist stabiles maschinelles Merkmal, Titel sind frei formulierbar und tragen Datumsbezug — Titel-Match wäre brüchig. Issues ohne Label (manuell angelegt) blockieren bewusst nicht — sie sind nicht Teil des Automatisierungszyklus. |
| Token-Nutzung je Schritt | Label-Erzeugung und Duplikatprüfung unter `GH_TOKEN: ${{ github.token }}`; nur `gh issue create` unter `REVIEW_ISSUE_TOKEN`. | Minimaler PAT-Fußabdruck: Das Owner-Secret wird nur dort eingesetzt, wo die Identität fachlich erforderlich ist (Autor des Erwähnungstextes). Labels/Liste erfordern keine Owner-Identität. |
| Nebenläufigkeit | `concurrency: { group: <workflow-dateiname>, cancel-in-progress: false }` je Workflow (statischer Gruppenname, Muster `sync-staging-with-main.yml`). | Serialisiert Cron- gegen `workflow_dispatch`-Läufe desselben Typs; `cancel-in-progress: false` (nicht `true` wie in `security-scan.yml`), damit die Sequenz Duplikatprüfung → Erzeugung nicht halbiert werden kann — ein Abbruch zwischen Prüfung und `gh issue create` könnte sonst beim Nachfolger ein Duplikat erzeugen. |
| Issue-Body-Einbettung | Mehrzeiliger Body als pwsh-Here-String (`@'…'@`, single-quoted) direkt im `run`-Block; kein Checkout nötig. | Body ist fester Bestandteil des Workflows (Muster: inline `--body` in `sync-staging-with-main.yml`); single-quoted Here-String verhindert Interpolation und hält `@softwarewerkstatt` wörtlich in der ersten Zeile. |
| Umgebungskonstanten | Workflow-ebene `env:` mit `REVIEW_LABEL` (für die drei `gh`-Aufrufe). Das Zielkonto bleibt als Literal `@softwarewerkstatt` im Body. | Vermeidet Label-Namen-Duplikate in Label-Create/List/Create-Aufrufen; der Erwähnungstext ist fachlich fix und wird nicht parametrisiert (Interpolation im Here-String würde double-quoted-Form + Escaping erzwingen). |
| Titel-Schema | Datumsbezogen: `Monatliches Architektur- und Sicherheitsreview – YYYY-MM`, `Wöchentliche NuGet-Paketprüfung – YYYY-KW<nn>` (ISO-8601-Kalenderwoche via `[System.Globalization.ISOWeek]::GetWeekOfYear`), `Jährliches Compliance- und Copyright-Review – YYYY`. | Akzeptanzkriterium „aussagekräftige Titel": Der Periodenbezug macht Zyklus und Aktualität am Issue sofort erkennbar und entzerrt Titel über Zyklen hinweg. |

## Programmabläufe

### Review-Zyklus (identische Struktur in allen drei Workflows)

Jeder der drei Workflows (`review-architecture-security.yml`, `review-nuget-packages.yml`, `review-compliance.yml`) führt dieselbe Sequenz aus; sie unterscheiden sich nur in Cron-Ausdruck, Label, Titel-Format und Issue-Body:

1. Cron-Trigger (`schedule`, nur vom Default-Branch `main`) oder manueller `workflow_dispatch` startet den einzigen Job auf `ubuntu-latest` (`timeout-minutes: 10`, Shell `pwsh`).
2. **Guard-Schritt:** Schritt-`env` bindet `REVIEW_ISSUE_TOKEN: ${{ secrets.REVIEW_ISSUE_TOKEN }}`; `pwsh` prüft `[string]::IsNullOrWhiteSpace($env:REVIEW_ISSUE_TOKEN)` → bei leer `Write-Error` (Hinweis: Owner-PAT als Repository-Secret hinterlegen, Verweis auf `docs/CI_CD.md`) und `exit 1`. Der Lauf schlägt sichtbar fehl, kein Issue entsteht.
3. **Label sicherstellen:** `gh label create <REVIEW_LABEL> --color "5319E7" --description "<Typ-Beschreibung>" --force` unter `GH_TOKEN: ${{ github.token }}` — idempotent, Muster `sync-staging-with-main.yml:48-52`.
4. **Duplikatprüfung:** `gh issue list --state open --label $env:REVIEW_LABEL --json number | ConvertFrom-Json` unter `GH_TOKEN: ${{ github.token }}`; Ergebnis in Schritt-Variable.
5. **Issue erzeugen (nur wenn kein Treffer):** Unter `GH_TOKEN: ${{ secrets.REVIEW_ISSUE_TOKEN }}` wird der Titel mit Datumsbezug in `pwsh` formatiert und `gh issue create --title <Titel> --body <Here-String> --label $env:REVIEW_LABEL` ausgeführt; erste Body-Zeile ist das Literal `@softwarewerkstatt`, gefolgt vom jeweiligen Prüfauftrag. `Write-Host` protokolliert die Issue-URL. Bei Treffer: `Write-Host "Skipped: an open review issue already exists (#<Nr>)."` — erfolgreicher Lauf ohne Erzeugung.

Beteiligte Artefakte/Konventionen: `gh`-CLI (auf `ubuntu-latest` vorinstalliert), Muster `.github/workflows/sync-staging-with-main.yml` (Label-`--force`, Duplikatprüfung + bedingte Erzeugung, `Write-Host`-Skip-Hinweis), `.github/workflows/security-scan.yml` (`schedule` + `workflow_dispatch`, Kopf-Kommentar).

### Nachgelagerter Wirkpfad (Bestand — keine Umsetzung, nur Kontext des Akzeptanznachweises)

1. Das unter Owner-Identität erzeugte Issue löst bei GitHub eine Mention-Notification für `@softwarewerkstatt` aus (Voraussetzung: Bot-Konto ist Collaborator mit Schreibrecht — Betriebsvoraussetzung).
2. `ScmEventMonitoringService.EnumerateNotificationsAsync` liest die Inbox des separaten Kontos, filtert `reason == mention` und ordnet `repository.node_id` dem registrierten Repository zu (`ScmEventMonitoringService.cs:253-299`).
3. Für `ScmReferenceKind.Issue` wird `ScmContentKind.Description` gelesen; `ScmEventValidationService.GetRejection` akzeptiert: frei stehende `@softwarewerkstatt`-Erwähnung vorhanden, `Description` von Reaktions-Dedup ausgenommen, Autor = `repository.OwnerId`/`martin-stromberg` (dank `REVIEW_ISSUE_TOKEN`).
4. `ScmEventTaskResolver.ResolveIssueAsync` legt über `AufgabeService.CreateFromIssueEventAsync` eine neue Aufgabe an (Titel = Issue-Titel, Text = Issue-Body + URL; `IssueSourceKey`-Dedup verhindert Doppelanlage) → Zustellung an die konfigurierte CLI.

### Fehlerpfad: fehlendes oder abgelaufenes `REVIEW_ISSUE_TOKEN`

1. Secret nie angelegt (oder leer): Guard-Schritt bricht mit `exit 1` ab — Lauf rot, klare Meldung mit Anleitung; keine Label-/Issue-Seiteneffekte.
2. PAT abgelaufen/entzogen (feingranulierte PATs haben max. 1 Jahr Gültigkeit): `gh issue list`/`gh issue create` schlagen mit HTTP-401/403 fehl → Schritt und Lauf fehlerhaft, im Actions-Log sichtbar; Betriebshandlung = Secret erneuern (dokumentiert in `docs/CI_CD.md`), danach `workflow_dispatch` zum Nachholen.

## Neue Klassen

Keine — das Feature erzeugt ausschließlich Workflow- und Dokumentationsartefakte:

| Artefakt | Typ | Zweck |
|----------|-----|-------|
| `.github/workflows/review-architecture-security.yml` | GitHub-Actions-Workflow | Monatlicher Cron (`0 6 1 * *`) + `workflow_dispatch`; erzeugt Issue `Monatliches Architektur- und Sicherheitsreview – YYYY-MM` mit Label `automated-review-architecture` |
| `.github/workflows/review-nuget-packages.yml` | GitHub-Actions-Workflow | Wöchentlicher Cron (`0 6 * * 1`) + `workflow_dispatch`; erzeugt Issue `Wöchentliche NuGet-Paketprüfung – YYYY-KW<nn>` mit Label `automated-review-nuget` |
| `.github/workflows/review-compliance.yml` | GitHub-Actions-Workflow | Jährlicher Cron (`0 6 1 1 *`) + `workflow_dispatch`; erzeugt Issue `Jährliches Compliance- und Copyright-Review – YYYY` mit Label `automated-review-compliance` |
| `.github/workflows/create-review-issue.yml` | Wiederverwendbarer GitHub-Actions-Workflow (`on: workflow_call`) | Enthält die gemeinsame Logik der drei Review-Workflows (Secret-Guard, Label-Erzeugung, Duplikatprüfung, Issue-Erzeugung, Titel-Suffix-Berechnung); wird von den drei zeitgesteuerten Workflows mit Inputs `label`, `label-description`, `title`, `period` (`month`/`isoweek`/`year`), `body` und Secret `REVIEW_ISSUE_TOKEN` aufgerufen |

Issue-Bodies (fest, jeweils erste Zeile frei stehend `@softwarewerkstatt`): exakt die drei in Issue #352 definierten Prüfaufträge — Architektur-/Sicherheitsreview (9 Prüfpunkte + „Erstelle eine priorisierte Liste konkreter Handlungsempfehlungen."), NuGet-Paketanalyse (6 Prüfpunkte + „Erstelle konkrete Update-Empfehlungen inklusive Risikobewertung."), Compliance-Review (9 Prüfpunkte + „Erstelle konkrete Änderungsvorschläge und liste erkannte Risiken oder Handlungsbedarfe auf.").

## Änderungen an bestehenden Klassen

Keine — es gibt keine C#-Änderungen.

Geänderte Dateien (kein Code):

### `docs/CI_CD.md`

- **Neuer Abschnitt „Geplante Review-Workflows":** Tabelle der drei Workflows (Datei, Cron/Intervall, Issue-Titel-Schema, Typlabel), Beschreibung der Guard-/Label-/Duplikat-/Erzeugungs-Sequenz, Hinweis auf Aktivierung erst nach Promotion `staging` → `main` (`schedule` und `workflow_dispatch` feuern nur vom Default-Branch), Plattformverhalten (Cron-Verzögerung unter Last, Deaktivierung von Schedules nach 60 Tagen Repo-Inaktivität) sowie Kollision am 1. Januar (monatlich + jährlich, fachlich unproblematisch — verschiedene Prüftypen).
- **Abschnitt „Secrets und Umgebungsvariablen":** Tabellenzeile für `REVIEW_ISSUE_TOKEN` (Herkunft: manuell hinterlegtes Repository-Secret, feingranulierter PAT des Owners `martin-stromberg`, Repository-Zugriff nur auf dieses Repo, Permission Issues: Read and write; Zweck: Issue-Erzeugung unter Owner-Identität für die `SeparatesKonto`-Owner-Prüfung) und Anpassung des Satzes „Es sind keine weiteren, manuell zu hinterlegenden Secrets erforderlich." — künftig mit `REVIEW_ISSUE_TOKEN`-Ausnahme und Verweis auf die einmalige Betriebshandlung inkl. Anleitung (PAT anlegen, als Actions-Secret hinterlegen, max. 1-jährige Gültigkeit → Erneuerung).
- **Troubleshooting-Tabelle:** neue Zeile „Review-Workflow schlägt im Guard-/Erzeugungsschritt fehl" → Secret fehlt oder PAT abgelaufen/entzogen → Secret (erneut) hinterlegen, `workflow_dispatch` wiederholen.

### `docs/help/plugins/github-ereignisse/troubleshooting.md`

- **Neuer Eintrag** im Abschnitt zum SeparatesKonto-Fehlerbild: „Workflow-erzeugtes Review-Issue erzeugt kein Ereignis" — Ursachen: Issue-Autor ist `github-actions[bot]` (Secret `REVIEW_ISSUE_TOKEN` fehlte und ein Bot-Issue wurde manuell/früher erzeugt → Issue löschen und Zyklus unter korrektem Secret wiederholen), Issue-Body wurde von Nicht-Owner bearbeitet (`lastEditedAt` → Bearbeiter zählt statt Autor), `@softwarewerkstatt` nicht Collaborator mit Schreibrecht (keine Mention-Notification zugestellt), Repository nicht als aktives `GitRepository` registriert. Verweis auf `docs/CI_CD.md` für die Secret-Einrichtung.

## Datenbankmigrationen

Keine.

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| `REVIEW_ISSUE_TOKEN` (Repository-Secret) | Darf nicht leer/undefiniert sein — Guard-Schritt vor jeder `gh`-Nutzung | `Write-Error` + `exit 1`, Lauf schlägt sichtbar fehl, kein Issue, keine Label-Erzeugung |
| Offenes Issue mit Typlabel | `gh issue list --state open --label <label>` → `Count -eq 0` nötig für Erzeugung | Treffer vorhanden → Erzeugung übersprungen, `Write-Host`-Hinweis mit Issue-Nummer, erfolgreicher Lauf |
| Issue-Body | Erste Zeile frei stehendes `@softwarewerkstatt` (kein Zeichen aus `QuoteChars` davor/danach — `HasFreeStandingTrigger`) | Bei Verstoß würde die Ereignisüberwachung die Erwähnung verwerfen — der Body ist fixer Workflow-Inhalt, Review der YAML sichert die Form ab |
| Issue-Autor | `gh issue create` nur unter `REVIEW_ISSUE_TOKEN` (Owner-Identität) | `github-actions[bot]`-Autor → Verwerfung „Textversion stammt nicht vom Repository-Owner" — durch Token-Zuordnung je Schritt erzwungen, kein Fallback |

## Konfigurationsänderungen

| Eintrag | Typ | Standardwert | Zweck |
|---------|-----|--------------|-------|
| `REVIEW_ISSUE_TOKEN` | Repository-Secret (Actions) | — (manuell einmalig zu hinterlegen) | Feingranulierter PAT des Owners `martin-stromberg` (nur dieses Repo, Issues: Read and write); Autor-Identität der erzeugten Issues |
| `env: REVIEW_LABEL` | Workflow-`env`-Konstante je YAML | `automated-review-architecture` / `automated-review-nuget` / `automated-review-compliance` | Kanonisches Duplikat-/Typlabel in den drei `gh`-Aufrufen |
| Cron-Ausdrücke | Workflow-Trigger je YAML | `0 6 1 * *`, `0 6 * * 1`, `0 6 1 1 *` | Fälligkeit der drei Review-Typen (UTC) |

Keine Anwendungs-/GUI-Konfiguration — die Workflows sind selbst die Konfiguration.

## Seiteneffekte und Risiken

- **Notifications-Volumen `@softwarewerkstatt`:** Pro Lauf max. eine neue Mention — gering; fachlich gewünscht.
- **Label-Liste des Repos:** Drei neue Labels erscheinen dauerhaft im Label-Bestand (Konvention `automated-*`, einheitlich `#5319E7`).
- **`docs/CI_CD.md` ist an anderen Stellen veraltet** (referenziert `test.yml`/`pr.yml`, beschreibt `security-scan.yml`/Sync-/Promotion-Workflows nicht — inventory/docs.md): bewusst **nicht** Teil dieses Plans (kein Scope-Bezug); die neuen Abschnitte beschreiben ausschließlich den Ist-Stand der Review-Workflows.
- **Plattformverhalten:** `schedule` feuert nur vom Default-Branch `main` → Workflows sind erst nach Promotion `staging` → `main` aktiv (auch `workflow_dispatch` benötigt die Datei auf `main`); GitHub kann Läufe verzögern und Schedules nach 60 Tagen Inaktivität abschalten — dokumentiert, nicht implementierbar.
- **PAT-Lebenszyklus:** Ablauf/Entzug des `REVIEW_ISSUE_TOKEN` lässt den Erzeugungsschritt mit Auth-Fehler scheitern — sichtbar, dokumentierter Betriebsfall, kein stilles Fehlverhalten. Die Duplikatprüfung unter `GITHUB_TOKEN` ist davon unabhängig.
- **Race-Condition Cron ↔ `workflow_dispatch`:** durch `concurrency`-Gruppe mit `cancel-in-progress: false` serialisiert; ein zwischen Prüfung und Erzeugung angestoßener zweiter Lauf wartet statt doppelt zu erzeugen.
- **Manuell erzeugte Bot-Issues:** Legt jemand das Review-Issue einmalig manuell unter `GITHUB_TOKEN` an (z. B. um das Secret zu „umgehenen"), blockiert es als unbrauchbares offenes Issue künftige Zyklen — dokumentierter Troubleshooting-Fall (Issue schließen/löschen).

## Rollout-Voraussetzungen (einmalige Betriebshandlungen, dokumentiert in `docs/CI_CD.md`)

Diese Schritte liegen außerhalb des Commits und sind Voraussetzung für den ersten erfolgreichen Lauf:

1. **Owner-PAT anlegen:** Der Repository-Owner `martin-stromberg` erstellt einen feingranulierten PAT (GitHub → Settings → Developer settings → Personal access tokens → Fine-grained): Resource owner `martin-stromberg`, Repository access „Only select repositories" → dieses Repository, Repository permissions → **Issues: Read and write**, Ablauf max. 1 Jahr (Erneuerung als wiederkehrende Betriebsaufgabe vermerken).
2. **Secret hinterlegen:** PAT als Repository-Secret `REVIEW_ISSUE_TOKEN` (Settings → Secrets and variables → Actions) speichern.
3. **Collaborator-Status prüfen:** `@softwarewerkstatt` muss Collaborator mit Schreibrecht auf dem Repository sein (Betriebsvoraussetzung für die Mention-Zustellung; bereits Setup des SeparatesKonto-Modus, siehe `docs/help/plugins/github-ereignisse/installation.md:25-28`).
4. **Nach Promotion nach `main`:** je Workflow einen `workflow_dispatch`-Probelauf ausführen und prüfen: Issue unter Autor `martin-stromberg` mit Typlabel und korrektem Body erstellt; wiederholter Lauf meldet „Skipped". Danach Ereigniseingang in der SCM-Ereignisübersicht der Anwendung beobachten (echte Abnahme des Gesamtpfads).

## Umsetzungsreihenfolge

1. **`.github/workflows/review-architecture-security.yml` anlegen**
   - Voraussetzungen: `gh`-CLI auf `ubuntu-latest` vorinstalliert (Fakt); Muster `sync-staging-with-main.yml`/`security-scan.yml` im Repo vorhanden. Keine weiteren.
   - Beschreibung: Vollständiger Workflow nach dem oben beschriebenen Programmablauf — Kopf-Kommentar (Zweck, `REVIEW_ISSUE_TOKEN`-Pflicht, Verweis `docs/CI_CD.md`), Trigger `schedule` + `workflow_dispatch`, `permissions: issues: write`, `concurrency`-Gruppe `review-architecture-security` (`cancel-in-progress: false`), `env: REVIEW_LABEL: automated-review-architecture`, Job `ubuntu-latest`/`timeout-minutes: 10`, Schritte Guard → Label (`--force`, `#5319E7`, unter `github.token`) → Duplikatprüfung (unter `github.token`) → bedingte `gh issue create` (unter `REVIEW_ISSUE_TOKEN`, Titel `Monatliches Architektur- und Sicherheitsreview – YYYY-MM`, Body = Architektur-/Sicherheits-Prüfauftrag mit führender `@softwarewerkstatt`-Zeile) mit `Write-Host`-Protokollierung beider Ausgänge.

2. **`.github/workflows/review-nuget-packages.yml` anlegen**
   - Voraussetzungen: Schritt 1 als Strukturvorlage (identischer Aufbau).
   - Beschreibung: Gleiche Sequenz; Cron `0 6 * * 1` (montags 06:00 UTC, versetzt zu `security-scan.yml` Mo 04:00 UTC), `concurrency`-Gruppe `review-nuget-packages`, Label `automated-review-nuget`, Titel `Wöchentliche NuGet-Paketprüfung – YYYY-KW<nn>` (ISO-Kalenderwoche via `[System.Globalization.ISOWeek]::GetWeekOfYear((Get-Date).ToUniversalTime())`), Body = NuGet-Prüfauftrag.

3. **`.github/workflows/review-compliance.yml` anlegen**
   - Voraussetzungen: Schritt 1 als Strukturvorlage.
   - Beschreibung: Gleiche Sequenz; Cron `0 6 1 1 *` (1. Januar 06:00 UTC), `concurrency`-Gruppe `review-compliance`, Label `automated-review-compliance`, Titel `Jährliches Compliance- und Copyright-Review – YYYY`, Body = Compliance-/Copyright-Prüfauftrag.

4. **`docs/CI_CD.md` erweitern**
   - Voraussetzungen: Schritte 1–3 (dokumentiert wird der Ist-Stand); bestehende Datei.
   - Beschreibung: Neuer Abschnitt „Geplante Review-Workflows" (Tabelle + Ablauf + Aktivierungs-/Plattformhinweise), Secrets-Tabelle um `REVIEW_ISSUE_TOKEN` erweitern, Satz „keine weiteren Secrets" anpassen, Troubleshooting-Zeile für Guard-/Auth-Fehlschlag ergänzen, einmalige Betriebshandlung (PAT-Anlage + Secret + Collaborator) als Rollout-Anleitung dokumentieren.

5. **`docs/help/plugins/github-ereignisse/troubleshooting.md` ergänzen**
   - Voraussetzungen: Schritte 1–4 (Verweisziel `docs/CI_CD.md` existiert dann).
   - Beschreibung: Neuer Troubleshooting-Eintrag „Workflow-erzeugtes Review-Issue erzeugt kein Ereignis" mit den vier Ursachen (Bot-Autor, Nicht-Owner-Edit, fehlende Collaborator-Einladung, nicht registriertes Repository) und Verweis auf die Secret-Einrichtung in `docs/CI_CD.md`.

6. **Rollout-Betriebshandlungen (Owner, außerhalb des Commits)**
   - Voraussetzungen: Merge bis `main` (für `schedule`/`workflow_dispatch`-Aktivierung); Schritte 1–5 gemergt.
   - Beschreibung: PAT anlegen → `REVIEW_ISSUE_TOKEN`-Secret hinterlegen → Collaborator-Status `@softwarewerkstatt` prüfen → `workflow_dispatch`-Probelauf je Workflow inkl. Autor-/Label-/Body-Prüfung und Re-Lauf auf „Skipped"-Verhalten → Ereigniseingang in der App beobachten.

## Tests

### Neue Tests

Keine automatisierten Testmethoden — es existiert keine Testinfrastruktur für GitHub-Workflows im Repo (`package.json` enthält nur Semantic-Release-Skripte, kein YAML-Lint). Die Verifikation folgt der im Repo vorgelebten Konvention (`security-scan.yml`): `workflow_dispatch`-Probelauf + Auswertung des Actions-Logs, ergänzt um die reale Abnahme des Gesamtpfads (Issue-Autor, Label, Ereigniseingang in der App) — siehe Rollout-Schritt 6.

### Betroffene bestehende Tests

Keine — es werden weder Signaturen, Verhalten noch Datenstrukturen des Anwendungscodes geändert.

### E2E-Tests (primärer Funktionsnachweis)

Keine E2E-Tests erforderlich: Die Anforderung berührt keinen über die Anwendungs-UI oder eine Nutzeraktion in der App erreichbaren Ablauf — die neuen Artefakte sind GitHub-Actions-YAML und Dokumentation. Der einzige „Benutzerfluss" ist der Workflow-Lauf auf GitHub selbst (plattformseitig, nicht Teil des App-Testumfangs); sein sichtbares Ergebnis (Issue + Actions-Log) wird über `workflow_dispatch`-Probeläufe als reale Abnahme nachgewiesen. Der nachgelagerte App-Pfad (Mention → Ereignis → Aufgabe) bleibt unverändert; dass ein Issue mit Owner-Autor und frei stehender `@softwarewerkstatt`-Erwähnung akzeptiert wird, ist durch die bestehenden Integrations-/Unittests des `SeparatesKonto`-Vertrags abgesichert (z. B. `ScmEventMonitoringServiceTests_Notifications.Monitor_SeparatesKonto_MentionNotification_EntersInboxViaResolver`, `ScmEventValidationServiceTests.SeparatesKonto_AuthorOrEditorMustBeOwner_NotViewer`) — die neuen Issues erzeugen exakt diese bereits getestete Eingangsgestalt.

Anzupassende bestehende E2E-Tests: Keine.

## Akzeptanzszenarien

| # | Szenario | Auslöser | Ausgangszustand | Schritte | Sichtbare Wirkung | Nachweis |
|---|----------|----------|------------------|----------|-------------------|----------|
| 1 | Happy Path: monatlicher Zyklus erzeugt Review-Issue und Ereignis | `schedule` am 1. des Monats (oder `workflow_dispatch`-Probelauf) | `REVIEW_ISSUE_TOKEN` hinterlegt, `@softwarewerkstatt` Collaborator, Repo in App registriert + `SeparatesKonto` aktiv, kein offenes Issue mit `automated-review-architecture`, Workflow auf `main` | Guard → Label sicherstellen → Duplikatprüfung (0 Treffer) → `gh issue create` unter Owner-PAT → GitHub stellt Mention-Notification zu → App liest Inbox (`reason = mention`), validiert `Description` (frei stehende Erwähnung, Autor = Owner) → `ScmEventTaskResolver` legt Aufgabe an → Zustellung | Neues Issue „Monatliches Architektur- und Sicherheitsreview – YYYY-MM" (Autor `martin-stromberg`, Label, Body mit `@softwarewerkstatt`); Actions-Log „Created review issue"; Ereignis + neue Aufgabe in der Anwendung | Reale Abnahme via `workflow_dispatch` (Rollout-Schritt 6): Actions-Log, Issue-Prüfung (`gh issue view` Autor/Label/Body), Ereignisübersicht in der App |
| 2 | Duplikatvermeidung über Zyklen | Nächster fälliger Lauf desselben Typs | Offenes Issue mit Typlabel aus Szenario 1 vorhanden | Guard → Label → Duplikatprüfung (1 Treffer) → Erzeugung übersprungen | Kein neues Issue; erfolgreicher Lauf mit Log „Skipped: an open review issue already exists (#N)" | Zweiter `workflow_dispatch`-Lauf direkt nach Szenario 1; Actions-Log |
| 3 | Fehlendes Secret schlägt klar fehl | `workflow_dispatch` | `REVIEW_ISSUE_TOKEN` nicht hinterlegt/gelöscht | Guard-Schritt → `Write-Error` + `exit 1` | Lauf rot; Log nennt Secret-Name, Anleitung und `docs/CI_CD.md`-Verweis; **kein** Issue, **kein** Label | `workflow_dispatch`-Lauf ohne Secret; Actions-Log |
| 4 | Jährlicher Zyklus am 1. Januar (Kollision mit monatlichem Lauf) | `schedule` am 1. Januar | Wie Szenario 1; Label `automated-review-compliance` | Beide Workflows laufen unabhängig (eigene `concurrency`-Gruppen, verschiedene Labels) | Zwei getrennte Issues verschiedener Prüftypen | Nach Promotion: Cron-Lauf bzw. zwei `workflow_dispatch`-Probeläufe |
| 5 | Fehlende Collaborator-Einladung (Betriebsfehlzustand) | Fälliger Lauf | Wie Szenario 1, aber `@softwarewerkstatt` ohne Repo-Zugriff | Issue wird korrekt unter Owner erzeugt — GitHub stellt aber keine Mention-Notification zu | Issue vorhanden, **kein** Ereignis in der App; Fehlerbild dokumentiert in `troubleshooting.md` | Dokumentierter Troubleshooting-Fall; Behebung: Collaborator einladen, Issue schließen, `workflow_dispatch` wiederholen |

Wartesituation/externe Abhängigkeit (Szenario 1): Zwischen Issue-Erzeugung und Ereigniseingang liegt das Poll-Intervall der App (`ScmEventOptions.PollInterval`, Default 60 s); der Nutzer sieht den Übergang am Ereignis in der SCM-Ereignisübersicht — kein manueller Eingriff nötig; ein verpasster Cron-Lauf (Plattformverzögerung/60-Tage-Deaktivierung) wird über `workflow_dispatch` nachgeholt.

## Offene Punkte

Keine.
