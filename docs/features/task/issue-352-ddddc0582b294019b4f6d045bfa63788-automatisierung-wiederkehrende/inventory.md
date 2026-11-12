# Bestandsaufnahme: Automatisierung wiederkehrender Architektur-, Sicherheits- und Compliance-Reviews (Issue #352)

Bestandsaufnahme der GitHub-Actions-Infrastruktur unter `.github/` sowie des nachgelagerten SCM-Ereignispfads (Überwachungsmodus `SeparatesKonto`, Notifications-Inbox mit `reason = mention`), bezogen auf die Anforderung, drei zeitgesteuerte Workflows einzuführen, die Issues mit frei stehender `@softwarewerkstatt`-Erwähnung anlegen. Quelle: `requirement.md` (Issue #352, Aufgaben-ID `ddddc058-2b29-4019-b4f6-d045bfa63788`).

## Zusammenfassung

- **Keine C#-Codeänderung erforderlich/erlaubt:** Die Anforderung ist reine CI-/Automatisierungsinfrastruktur (Workflow-YAML + Dokumentation). Es existieren heute **keine** Workflows, die Issues anlegen — `gh issue` kommt in keinem der sieben vorhandenen Workflows vor.
- **Direkte Vorlagen vorhanden:** `.github/workflows/sync-staging-with-main.yml` und `staging-to-main-promotion.yml` zeigen exakt das benötigte Muster: `ubuntu-latest` + `pwsh` + `GH_TOKEN: ${{ github.token }}`, idempotente Label-Erzeugung via `gh label create <name> --force`, Duplikatprüfung (`gh pr list --state open --json number`) vor bedingter Erstellung (`gh pr create`). `.github/workflows/security-scan.yml` zeigt die `schedule`-Cron + `workflow_dispatch` + `concurrency`-Konvention (Cron: `0 4 * * 1`, Montag 04:00 UTC).
- **Owner-Prüfung verifiziert (kritisch):** `ScmEventValidationService.GetRejection` (`src/Softwareschmiede/Application/Services/ScmEventValidationService.cs`, Zeilen 33–61) verlangt im Modus `SeparatesKonto` eine frei stehende `@<ViewerLogin>`-Erwähnung **und** Autor-bzw.-Bearbeiter-Identität mit `repository.OwnerId`/`Owner`. Repository-Owner `martin-stromberg` wurde live gegen `gh repo view` verifiziert. Ein per `GITHUB_TOKEN` angelegtes Issue hätte Autor `github-actions[bot]` → Ablehnung „Textversion stammt nicht vom Repository-Owner". Sonderfall verifiziert: Wird die Textversion nachträglich **bearbeitet**, zählt der Bearbeiter statt des Autors — ein Owner-Edit der Bot-Issue-Beschreibung würde die Prüfung formal bestehen.
- **Notifications-Pfad verifiziert:** `GitHubPlugin.EnumerateNotificationsAsync` liest `GET /notifications?all=true&participating=true` unter dem SeparatesKonto-Credential (`plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.Events.cs:170-199`); `ScmEventMonitoringService` filtert `reason == "mention"`, ordnet `repository.node_id` nur registrierten Repositories zu und liest für Issue-Referenzen `Description` + `IssueComment`. Eine Erwähnung in der Issue-Beschreibung erzeugt eine Mention-Notification und wird als `Description`-Beobachtung validiert.
- **Label-Konventionen verifiziert:** Remote existieren `automated-backmerge` (#1D76DB), `automated-promotion` (#0E8A16) und `automated-sync` (#0E8A16 — wird von keinem aktuellen Workflow erzeugt, wirkt wie ein Relikt/Umbenennung von `automated-backmerge`). Keine review-bezogenen Labels vorhanden. Issue-Templates (`.github/ISSUE_TEMPLATE/`) tragen leere `labels:`.
- **Keine Custom-Secrets in Workflows:** Einzige Secret-Referenz ist `${{ secrets.GITHUB_TOKEN }}` in `staging-ci.yml:267` (Semantic-Release-Dry-Run). Für eine Issue-Erzeugung unter Owner-Identität müsste ein neues Repository-Secret (Owner-PAT) hinterlegt werden — betriebliche Voraussetzung, kein Code.
- **`schedule` feuert nur vom Default-Branch (`main`):** Durch den Staging-Workflow (Feature → `staging` → Promotion) werden Cron-Workflows erst nach der Promotion aktiv. Repo-Default-Branch ist `main` (verifiziert via `gh repo view`).
- **Dokumentationskonvention:** `docs/CI_CD.md` dokumentiert Workflows — ist aber **veraltet**: referenziert `.github/workflows/test.yml` und `.github/workflows/pr.yml`, die nicht existieren (tatsächlich `pr-staging-ci.yml`/`staging-ci.yml`); `security-scan.yml` und die Sync-/Promotion-Workflows sind dort gar nicht beschrieben. Fachlicher Vertrag des SeparatesKonto-Pfads steht in `docs/help/plugins/github-ereignisse/` (9 Dateien; `business-rules.md` Z. 31–33, `installation.md` Z. 25–28, `troubleshooting.md` Z. 55–57).
- **gh-CLI-Zustand (Faktum, keine Aktion):** `gh auth status`: `softwarewerkstatt` aktiv via `GH_TOKEN` (Scopes u. a. `notifications`, `repo`, `workflow`; Warnung: `read:org` fehlt), `martin-stromberg` im Keyring (inaktiv). `gh version 2.92.0`.
- **Kein Workflow-Testtooling:** `package.json` enthält nur Semantic-Release-Skripte; keine YAML-Lint-/Validierungsinfrastruktur für Workflows.

**Test-Ausgangszustand:** Voll-Build `dotnet build Softwareschmiede.slnx -c Debug` erfolgreich (0 Warnungen, 0 Fehler, ~15 s). Stabile Lanes mit `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1`: `Softwareschmiede.Tests` `Category!=OsInterface` → **2288 bestanden, 0 fehlgeschlagen, 1 übersprungen** (plattformbedingter Skip), Exit-Code 0; `Softwareschmiede.IntegrationTests` `Category!=OsInterface` → **280 bestanden, 0 fehlgeschlagen, 0 übersprungen**, Exit-Code 0. Nachgewiesene bestehende Testfehler: keine. Testlücke: `Category=OsInterface`-Lanes (E2E/ConPTY/Clipboard) nicht ausgeführt — Begründung und Nachweise in [tests.md](inventory/tests.md).

## Details

- [GitHub-Workflows, Composite Actions und Label-Konventionen](inventory/workflows.md)
- [Datenmodell (Ereignis-Pipeline)](inventory/models.md)
- [Logik (Validierung, Monitoring, Task-Auflösung, GitHub-Plugin)](inventory/logic.md)
- [Enums](inventory/enums.md)
- [Interfaces](inventory/interfaces.md)
- [Dokumentationsstruktur](inventory/docs.md)
- [Tests und Test-Ausgangszustand](inventory/tests.md)
