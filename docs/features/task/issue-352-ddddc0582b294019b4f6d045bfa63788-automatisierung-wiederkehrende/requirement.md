# Übersetzte Anforderung: Automatisierung wiederkehrender Architektur-, Sicherheits- und Compliance-Reviews

**Quelle:** Issue #352 / Aufgaben-ID `ddddc058-2b29-4019-b4f6-d045bfa63788`
**Branch:** `task/issue-352-ddddc0582b294019b4f6d045bfa63788-automatisierung-wiederkehrende`

## Fachliche Zusammenfassung

Es werden drei eigenständige, zeitgesteuerte GitHub-Actions-Workflows unter `.github/workflows/` eingeführt, die jeweils per Cron-Trigger automatisch ein GitHub-Issue im Repository anlegen. Jedes erzeugte Issue enthält eine frei stehende `@softwarewerkstatt`-Erwähnung sowie einen festen Prüfauftrag (Architektur-/Sicherheitsreview, NuGet-Paketanalyse, Compliance-/Copyright-Review) und soll so die bestehende SCM-Ereignisüberwachung der Anwendung (Überwachungsmodus „Separates Konto", Notifications-Inbox mit `reason = mention`, siehe `docs/help/plugins/github-ereignisse/`) als Auslöser für die automatische Analyse und Bearbeitung nutzen. Vor jeder Issue-Erzeugung prüft der Workflow, ob bereits ein offenes Issue desselben Prüftyps existiert, und überspringt die Erstellung in diesem Fall (Duplikatvermeidung über Zyklen hinweg).

## Fachlicher Scope

- **Verarbeitungseinheit:** Ein *Review-Zyklus* je Prüftyp — d. h. das fällige Anlegen genau eines Issues eines bestimmten Prüftyps (Architektur-/Sicherheitsreview, NuGet-Prüfung, Compliance-Review) zum jeweiligen Fälligkeitszeitpunkt. Ein Zyklus gilt fachlich als „abgeschlossen", sobald das erzeugte Issue geschlossen ist; solange ein Issue des Typs offen ist, wird kein neuer Zyklus gestartet.
- **Auslösende Ereignisse:**
  - Cron-Fälligkeit *monatlich am 1. Tag des Monats* (Architektur- und Sicherheitsreview) — GitHub-Cron läuft auf UTC, konkrete Uhrzeit ist eine Festlegung bei der Umsetzung (Orientierung am bestehenden `security-scan.yml`: frühe UTC-Stunde).
  - Cron-Fälligkeit *wöchentlich* (NuGet-Prüfung) — der Wochentag ist in der Anforderung nicht festgelegt; Annahme: ein fester Wochentag (z. B. Montag), ggf. zeitversetzt zum bestehenden `security-scan.yml` (Montag 04:00 UTC), siehe Offene Fragen.
  - Cron-Fälligkeit *jährlich am 1. Januar* (Compliance- und Copyright-Review).
  - Ergänzend naheliegend: `workflow_dispatch` als manueller Auslöser für Test/Nachholbedarf (Konvention im Repo, z. B. `security-scan.yml`) — Annahme, nicht explizit gefordert.
- **Erwartete Wirkung:** Pro fälligem Zyklus entsteht — sofern kein offenes Issue desselben Typs existiert — ein neues GitHub-Issue mit aussagekräftigem Titel und dem im Issue-Text definierten Body (beginnend mit frei stehender `@softwarewerkstatt`-Erwähnung, gefolgt vom jeweiligen Prüfauftrag mit Aufzählungspunkten und Abschlussanweisung). Nachgelagerte Wirkung: Die Erwähnung generiert eine Benachrichtigung in der Inbox des Kontos `@softwarewerkstatt`; die Softwareschmiede-SCM-Ereignisüberwachung erzeugt daraus ein Ereignis und stößt die automatische Bearbeitung an. Sichtbar ist außerdem der Workflow-Lauf in der Actions-Übersicht inkl. Protokollausgabe („Issue erstellt #N" bzw. „übersprungen, offenes Issue #M vorhanden").
- **Umgang mit fehlenden Objekten:**
  - *Offenes Issue desselben Prüftyps vorhanden:* Der Zyklus wird **verworfen** (kein neues Issue, erfolgreicher Workflow-Lauf mit Hinweis im Log) — explizit gefordert. Voraussetzung ist eine maschinell eindeutige Identifikation des Prüftyps am Issue; da Titel frei formulierbar sind, ist als Identifikationsmerkmal ein eigenes Label je Prüftyp naheliegend (Erzeugen-falls-fehlend per `gh label create --force`, Muster aus `sync-staging-with-main.yml`) — Annahme/Entwurfsentscheidung.
  - *Label für den Prüftyp existiert noch nicht:* **erzeugen** (idempotent via `--force`), analog zum `automated-backmerge`-Label in `sync-staging-with-main.yml`.
  - *`@softwarewerkstatt`-Konto ohne Repository-Zugriff / Erwähnung ohne Zustellung:* GitHub erzeugt nur dann eine Mention-Benachrichtigung, wenn das Konto auf das Repository zugreifen kann; laut `docs/help/plugins/github-ereignisse/troubleshooting.md` muss das Bot-Konto als Collaborator eingeladen sein — Betriebsvoraussetzung, nicht Teil der Workflow-Implementierung, aber zu dokumentieren.
  - *Issue-Autor ≠ Repository-Owner:* Der Validierungsvertrag im Modus `SeparatesKonto` verlangt, dass der Autor der Erwähnung der Repository-Owner ist (`docs/help/plugins/github-ereignisse/business-rules.md`, Zeile ~33). Ein per `GITHUB_TOKEN` erstelltes Issue hat `github-actions[bot]` als Autor — die Ereignisüberwachung würde die Erwähnung dann verwerfen. **Kritische offene Frage**, siehe unten.
- **Scope-Ausschlüsse:**
  - Keine inhaltliche Prüfung im Workflow selbst: Die Actions führen die Reviews **nicht** durch, sondern erstellen ausschließlich die Auftrags-Issues; die Analyse obliegt der nachgelagerten Automatisierung (`@softwarewerkstatt`).
  - Keine Änderung am bestehenden `security-scan.yml`: Der wöchentliche Abhängigkeits-Scan (Artefakt `vulnerable-packages-scan`) bleibt unverändert bestehen; das neue wöchentliche NuGet-*Issue* ist eine zusätzliche, fachlich andere Wirkung (agentische Analyse inkl. Update-Empfehlungen statt reinem Scan-Log).
  - Keine Änderungen am C#-Quellcode, keine UI-Komponenten, keine Tests im `Softwareschmiede.Tests`-Sinne — die Lösung besteht aus Workflow-YAML und Dokumentation.
  - Keine Eskalations-/Erinnerungslogik: Ist ein Issue offen, wird schlicht übersprungen; es gibt keine Benachrichtigung über „veraltete" offene Review-Issues (nicht gefordert).
  - Kein Schließen/Verwalten der erzeugten Issues durch die Workflows.

## Betroffene Klassen und Komponenten

Es sind **keine** Datenmodellklassen, Services, Interfaces, Enums oder UI-Komponenten betroffen — das Feature ist reine CI-/Automatisierungsinfrastruktur.

### Neu zu erstellende Artefakte (Namen sind Vorschläge)

- `.github/workflows/review-architecture-security.yml` — monatlicher Cron (z. B. `0 6 1 * *`); Job prüft offene Issues des Typs und erstellt ggf. das Issue mit dem Architektur-/Sicherheits-Prüfauftrag.
- `.github/workflows/review-nuget-packages.yml` — wöchentlicher Cron (z. B. `0 6 * * 1`); analoger Aufbau mit dem NuGet-Prüfauftrag.
- `.github/workflows/review-compliance.yml` — jährlicher Cron (z. B. `0 6 1 1 *`); analoger Aufbau mit dem Compliance-/Copyright-Prüfauftrag.
- Dokumentation: Erweiterung von `docs/CI_CD.md` um einen Abschnitt zu den drei Review-Workflows (Akzeptanzkriterium „vollständig innerhalb des Repositories dokumentiert"); ggf. zusätzlich kurze Verwaltungsanleitung (Labels, Betriebsvoraussetzungen).

### Bestehende, als Referenz/Konvention relevante Artefakte

- `.github/workflows/security-scan.yml` — Vorlage für `schedule`-Trigger + `workflow_dispatch`, `concurrency`-Gruppe, explizite `permissions`.
- `.github/workflows/sync-staging-with-main.yml` — Vorlage für `gh`-CLI-Nutzung unter `pwsh` mit `GH_TOKEN: ${{ github.token }}`, `issues: write`-Permission, Label-Erzeugung via `gh label create ... --force`, Duplikatprüfung vor Erstellung (`gh pr list --state open` → bedingte `gh pr create`).
- `docs/help/plugins/github-ereignisse/` — fachlicher Vertrag des nachgelagerten Auslösers (SeparatesKonto/`reason = mention`/Owner-Prüfung), rein als Kontext, keine Änderung.

### Tests

- Es existiert keine Testinfrastruktur für GitHub-Workflows im Repo; die Verifikation erfolgt über `workflow_dispatch`-Probeläufe und die Actions-Logs (Annahme — falls eine lokale YAML-Validierung gewünscht ist, offene Frage).

## Implementierungsansatz

- **Struktur je Workflow:** Ein Job auf `ubuntu-latest` (kein Build/Checkout nötig — Issue-Body liegt inline im YAML, Muster `sync-staging-with-main.yml`), `timeout-minutes` klein (z. B. 10), `permissions: issues: write` (plus nichts Weiterem; die Erzeugung benötigt weder `contents` noch `pull-requests`).
- **Duplikatprüfung:** Vor `gh issue create` per `gh issue list --state open --label <typ-label> --json number` prüfen; bei Treffer `> 0` Schritt protokollieren und Issue-Erzeugung überspringen. Alternativ/abzusichern: Titel-Präfix-Match als Fallback, falls Issues ohne Label angelegt wurden (Annahme: Label als kanonisches Merkmal genügt, siehe Offene Fragen).
- **Issue-Erzeugung:** `gh issue create --title "<aussagekräftiger Titel>" --body <Body> --label <typ-label>`; Body exakt wie in der Anforderung definiert, beginnend mit frei stehender Zeile `@softwarewerkstatt`. Titelvorschläge: „Monatliches Architektur- und Sicherheitsreview – YYYY-MM", „Wöchentliche NuGet-Paketprüfung – KW <n>", „Jährliches Compliance- und Copyright-Review – <Jahr>" (Datumsbezug über `$env`/`pwsh`-Formatierung; Annahme, da „aussagekräftige Titel" gefordert, aber nicht vorgegeben sind).
- **Kritische Abhängigkeit — Issue-Autor:** Damit die `@softwarewerkstatt`-Erwähnung von der SCM-Ereignisüberwachung akzeptiert wird, muss der Issue-Autor der Owner-Prüfung genügen. Mit `GITHUB_TOKEN` ist der Autor `github-actions[bot]`. Mögliche Lösungen (Entwurfsentscheidung/offene Frage): (a) Erstellung unter einem PAT des Repository-Owners aus einem Repository-Secret (`GH_TOKEN: ${{ secrets.<NAME> }}`), sodass der Owner als Autor erscheint; (b) Issue per `GITHUB_TOKEN` anlegen und die Erwähnung stattdessen in einem Kommentar des Owners platzieren (gleiche Autor-Problematik); (c) Anpassung des Validierungsvertrags der Ereignisüberwachung (wäre C#-Änderung — eigentlich Scope-Ausschluss). **Vor Umsetzung zu klären.**
- **Cron-Hinweise:** `schedule` feuert nur vom Default-Branch (`main`); durch den Staging-Workflow (Feature → `staging` → Promotion nach `main`) werden die Workflows erst nach der Promotion aktiv. GitHub kann geplante Läufe bei Last verzögern und deaktiviert Schedules nach 60 Tagen Repository-Inaktivität — zu dokumentierende Plattform-Eigenschaft, nicht implementierbar.
- **Kollision am 1. Januar:** Am 1. Januar fallen monatlicher und jährlicher Trigger zusammen — fachlich unproblematisch (verschiedene Prüftypen, verschiedene Issues).

## Konfiguration

- **Keine Anwendungs-/GUI-Konfiguration** — die Workflows sind selbst die Konfiguration (Cron-Ausdrücke, Labels, Issue-Texte im YAML). Eine externe Konfigurierbarkeit (z. B. Repository-Variables für Intervall oder Zielkonto) ist nicht gefordert; lediglich das Zielkonto `@softwarewerkstatt` und die Label-Namen könnten als Workflow-`env`-Konstanten zentral oben im YAML gehalten werden (Konvention/Annahme).
- **Betriebsvoraussetzung (Repository-/Org-Ebene, außerhalb des Codes):** ggf. ein Secret mit Owner-PAT (je nach Klärung der Autor-Frage) sowie die Collaborator-Einladung des `@softwarewerkstatt`-Kontos — in der Repo-Dokumentation zu vermerken.

## Offene Fragen

1. **Issue-Autor vs. Owner-Prüfung (kritisch):** Reicht `GITHUB_TOKEN` (`github-actions[bot]` als Autor), oder muss das Issue unter der Owner-Identität (PAT aus Secret) erstellt werden, damit die `SeparatesKonto`-Validierung (Autor = Repository-Owner) die Erwähnung akzeptiert? Ist das Bot-Konto bzw. die Owner-Regel hier ggf. bereits anders konfiguriert (z. B. `Benutzerkonto`-Modus mit Trigger-Keyword statt Mention)?
2. **Identifikation „desselben Typs":** Ist die vorgeschlagene Label-basierte Duplikatprüfung akzeptabel (ein Label je Prüftyp, auto-erzeugt), oder soll die Erkennung titelbasiert erfolgen? Soll ein gemeinsames Sammel-Label (z. B. `automated-review`) plus typ-spezifischem Label verwendet werden?
3. **Wochentag und Uhrzeit der wöchentlichen Prüfung:** Nicht spezifiziert — Annahme Montag; soll sie zeitlich zum bestehenden `security-scan` (Mo 04:00 UTC) versetzt laufen, um Doppel-Runs zu entzerren?
4. **Issue-Titel:** Sind die vorgeschlagenen Titel mit Datums-/Periodenbezug (Monat, Kalenderwoche, Jahr) gewünscht, oder feste Titel ohne Datumsanteil?
5. **Labels/Meta am Issue:** Sollen die Issues zusätzlich einen Assignee, ein Projekt oder weitere Labels (z. B. `automation`) erhalten? Nicht gefordert — Standardannahme: nur das Typ-Label.
6. **Manueller Trigger:** Soll `workflow_dispatch` ergänzt werden (Repo-Konvention, erleichtert Test und Nachholung verpasster Cron-Läufe)?
7. **Benachrichtigungs-Verhalten bei Überspringen:** Reicht ein Log-Eintrag im Workflow-Lauf, wenn wegen offenem Issue übersprungen wird — oder soll (nicht gefordert) zusätzlich sichtbar gemacht werden, dass ein Review-Zyklus „überfällig" ist (z. B. Kommentar am offenen Issue)?
8. **Schutz vor paralleler Mehrfacherzeugung:** Soll eine `concurrency`-Gruppe (Muster `security-scan.yml`) gesetzt werden, um Race-Conditions zwischen Cron und `workflow_dispatch` auszuschließen (Annahme: ja, `cancel-in-progress: false`, da sonst die Duplikatprüfung/ Erstellung halbiert werden könnte)?
