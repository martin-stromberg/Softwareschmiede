# CI/CD: Release-Pipeline

Dieses Dokument beschreibt den automatisierten Release-Workflow (`.github/workflows/release.yml`), der die fertige Anwendung nach jedem Merge in `main` als versioniertes GitHub-Release bereitstellt.

## Trigger-Bedingungen

Der Workflow (`.github/workflows/release.yml`) kennt zwei unabhängige Jobs mit unterschiedlichen Triggern:

- **Job `release`** — ausgelöst bei einem `push` auf den Branch `main` (`startsWith(github.ref, 'refs/heads/')`). Pushes auf andere Branches (z. B. Feature-Branches) lösen keinen Release-Workflow aus.
- **Job `release-manual-tag`** — ausgelöst bei einem `push` eines Tags im Format `vX.Y.Z` (`startsWith(github.ref, 'refs/tags/')`, Trigger-Pattern `tags: ['v*.*.*']`). Ein Tag-Push löst *keinen* `branches: [main]`-Trigger aus — daher ist dies ein eigener Job, siehe [`CONTRIBUTING.md`](../CONTRIBUTING.md#manual-release-tag-override).

Beide Jobs bauen und veröffentlichen dieselbe Anwendung, unterscheiden sich aber in der Versionsermittlung (siehe unten).

## Workflow-Schritte

### Test-Workflow

Der separate Workflow `.github/workflows/test.yml` laeuft fuer Pull Requests nach `main` und fuer Pushes auf `main`. Er baut `src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj` auf `windows-latest` und trennt die Testauswertung:

- `Test regular` ist blockierend und fuehrt `Category!=OsInterface` aus.
- `Test OS interfaces` fuehrt `Category=OsInterface` best-effort mit `continue-on-error: true` aus.
- TRX-Dateien werden getrennt als `test-results-regular` und `test-results-os-interface` hochgeladen.

Die Trennung ersetzt den frueheren Ausschluss ueber `Category!=E2E&Category!=ConPTY`. E2E- und ConPTY-Tests behalten diese Kategorien, tragen aber zusaetzlich `OsInterface`.

### Staging-PR-Workflow

Der Workflow `.github/workflows/pr.yml` laeuft fuer Pull Requests nach `staging` und zeigt fuer regulaere Feature-PRs einen gemeinsamen Job `validate & test`. Dieser Job fuehrt zuerst die Qualitaetspruefungen aus und startet die Tests erst danach:

- `Lint and format check` prueft `dotnet format Softwareschmiede.slnx --verify-no-changes --no-restore`.
- `Security dependency scan` prueft verwundbare NuGet-Pakete inklusive transitiver Abhaengigkeiten und laedt das Scan-Log als `vulnerable-packages-pr` hoch.
- `Static code analysis` baut die Solution mit `TreatWarningsAsErrors=true`.
- Danach laufen die regulaeren Tests blockierend mit `Category!=OsInterface`; `Category=OsInterface` laeuft weiterhin best-effort mit `continue-on-error: true`.

Pull Requests von `main` nach `staging` werden weiterhin ueber `back-merge-skip` erkannt; der kombinierte Validierungs-/Testjob wird fuer diesen Sonderfall uebersprungen.

### Job `release` (Push auf `main`)

| # | Step | Zweck |
|---|------|-------|
| 1 | `Checkout` (`actions/checkout@v4`, `fetch-depth: 0`) | Lädt das Repository mit vollständiger Git-Historie, da Semantic Release alle Commits seit dem letzten Release analysieren muss. |
| 2 | `Setup Node.js` (`actions/setup-node@v4`, Node 24) | Stellt die Node.js-Laufzeitumgebung für die Semantic-Release-Versionsanalyse bereit. |
| 3 | `Install dependencies` (`npm ci`) | Installiert Semantic Release und die konfigurierten Plugins aus `package.json`/`package-lock.json`. |
| 4 | `Determine next release version` (`npx semantic-release --dry-run --no-ci`) | Analysiert Commits seit dem letzten Tag und bestimmt die naechste Version. Der Step schreibt `version`, `tag` und `should_release` in die GitHub-Outputs. Liefert Semantic Release keinen neuen Release, werden Build und Release-Erstellung uebersprungen. |
| 5 | `Build and package` (Composite Action `.github/actions/build-and-package`) | Führt `Setup .NET 10`, `dotnet restore`, `dotnet publish src/Softwareschmiede.App/Softwareschmiede.App.csproj -c Release -o publish/` (inkl. Plugin-DLLs über das `CopyPluginsToPublishOutput`-Target), eine Prüfung auf ein vollständiges `publish/`-Verzeichnis (Guard-Step) sowie die ZIP-Erstellung (`Compress-Archive` → `release.zip`) aus. Läuft auf `windows-latest`, da die Anwendung `UseWPF` mit `net10.0-windows` als Target Framework verwendet und daher nicht plattformübergreifend gebaut werden kann. Wird identisch von beiden Jobs genutzt, um Divergenz zwischen den Build-Schritten zu vermeiden. |
| 6 | `Create GitHub Release` (`gh release create`) | Erstellt den Tag und das GitHub-Release per GitHub-API am aktuellen `GITHUB_SHA`, generiert Release Notes automatisch und lädt `release.zip` hoch. Dieser Schritt pusht nicht nach `main` und bleibt dadurch kompatibel mit Branch Protection. |

### Job `release-manual-tag` (Push eines Tags `vX.Y.Z`)

| # | Step | Zweck |
|---|------|-------|
| 1 | `Checkout` (`fetch-depth: 0`) | Lädt das Repository auf dem Stand des gepushten Tags. |
| 2 | `Build and package` (Composite Action `.github/actions/build-and-package`) | Identisch zum `release`-Job (kein Node/Semantic-Release-Setup nötig, da die Version durch den Tag bereits feststeht). |
| 3 | `Create GitHub Release for manual tag` (`softprops/action-gh-release`, auf Commit-SHA gepinnt) | Erstellt direkt ein GitHub-Release für den gepushten Tag (`tag_name`/`name` = Tagname), generiert Release Notes automatisch (`generate_release_notes: true`) und lädt `release.zip` als Asset hoch — unabhängig von Commit-Typen, ohne Semantic-Release-Versionsberechnung. |

## Nebenläufigkeit und Supply-Chain-Absicherung

- **Concurrency-Guard:** Der Workflow definiert `concurrency: { group: release, cancel-in-progress: false }`. Dadurch werden parallele Release-Laeufe serialisiert statt gleichzeitig ausgeführt — verhindert Wettlaufsituationen bei der berechneten Version sowie der Tag-/Release-Erstellung.
- **Action-Pinning:** `softprops/action-gh-release` laeuft mit `permissions: contents: write` und wird daher auf einen festen Commit-SHA statt auf einen beweglichen Versions-Tag gepinnt (Kommentar im Workflow zeigt die zugehörige Version). Damit kann ein nachträglich umgebogener Tag keinen unbemerkten Code mit Schreibrechten auf das Repository ausführen. Der automatische `main`-Release nutzt die auf GitHub-hosted Runnern vorhandene `gh`-CLI statt einer externen Release-Action.
- **Publish-Guard:** Die Composite Action `build-and-package` prüft nach `dotnet publish`, ob `publish/Softwareschmiede.exe` sowie mindestens eine Plugin-DLL unter `publish/plugins/` vorhanden sind, und bricht den Workflow andernfalls kontrolliert ab — verhindert, dass ein leeres oder unvollständiges `release.zip` unbemerkt als GitHub-Release-Asset veröffentlicht wird.
- **Rename für Release-Artefakt:** Der lokale Dev-/Test-Build erzeugt `Softwareschmiede.App.exe` (Projekt-`AssemblyName`); die Composite Action benennt die veröffentlichte Kopie direkt nach `dotnet publish` in `Softwareschmiede.exe` um. Damit der in Windows angezeigte Prozess-/Anwendungsname (u. a. `FileDescription`) ebenfalls `Softwareschmiede` lautet und nicht `Softwareschmiede.App`, setzt das App-Projekt die Versionsmetadaten `AssemblyTitle`/`Product`/`Company` nur bei übergebenem `ReleaseTag` auf `Softwareschmiede` — `AssemblyName` bleibt unverändert, da `Softwareschmiede.dll` bereits die Domain-Assembly ist und Identität/Referenzen des Managed-Assembly erhalten bleiben müssen. Grund: Diese Anwendung wird mit sich selbst weiterentwickelt — eine installierte Release-Kopie hostet oft die laufende Claude-Code-Session. Teilten sich Dev-Build und Release denselben Prozessnamen, könnte Tooling, das Build-Locks oder verwaiste Testprozesse anhand des Namens auflöst, versehentlich die Release-/Host-Instanz statt einer Testinstanz treffen (siehe `CLAUDE.md`, Abschnitt „Self-Hosting-Risiko").

## Geplante Review-Workflows

Drei zeitgesteuerte Workflows legen je Review-Zyklus ein GitHub-Issue mit frei stehender `@softwarewerkstatt`-Erwähnung und festem Prüfauftrag an. Die Erwähnung erzeugt eine Mention-Benachrichtigung im Konto `@softwarewerkstatt`; die SCM-Ereignisüberwachung der Anwendung (Überwachungsmodus „Separates Konto", `reason = mention`) erzeugt daraus ein Ereignis und stößt die automatische Bearbeitung an (fachlicher Vertrag: `docs/help/plugins/github-ereignisse/`). Die Workflows führen selbst keine inhaltliche Prüfung durch — sie erstellen ausschließlich die Auftrags-Issues.

Die gemeinsame Logik (Secret-Guard, Label-Erzeugung, Duplikatprüfung, Issue-Erzeugung, Titel-Suffix-Berechnung) liegt einmalig im wiederverwendbaren Workflow `.github/workflows/create-review-issue.yml` (`on: workflow_call`). Die drei zeitgesteuerten Workflows enthalten nur noch Trigger (`schedule`/`workflow_dispatch`), `concurrency`-Gruppe sowie einen einzigen Job, der den wiederverwendbaren Workflow mit Label, Label-Beschreibung, Titel-Präfix, Periodenart (`month`/`isoweek`/`year`) und Issue-Body aufruft.

| Workflow | Intervall (Cron, UTC) | Issue-Titel | Typlabel |
|----------|------------------------|-------------|----------|
| `.github/workflows/review-architecture-security.yml` | Monatlich am 1. Tag, 06:00 (`0 6 1 * *`) | `Monatliches Architektur- und Sicherheitsreview – YYYY-MM` | `automated-review-architecture` |
| `.github/workflows/review-nuget-packages.yml` | Wöchentlich montags, 06:00 (`0 6 * * 1`; 2 h versetzt zum `security-scan.yml`) | `Wöchentliche NuGet-Paketprüfung – YYYY-KWnn` (ISO-8601-Kalenderwoche) | `automated-review-nuget` |
| `.github/workflows/review-compliance.yml` | Jährlich am 1. Januar, 06:00 (`0 6 1 1 *`) | `Jährliches Compliance- und Copyright-Review – YYYY` | `automated-review-compliance` |
| `.github/workflows/create-review-issue.yml` | — (kein eigener Schedule; wird per `workflow_call` von den drei Workflows oben aufgerufen) | `{Titel-Präfix} – {Suffix}` je Aufrufer | je Aufrufer |

Alle drei Workflows lassen sich zusätzlich manuell über `workflow_dispatch` auslösen (z. B. zum Nachholen eines verpassten Cron-Laufs).

### Ablauf je Lauf

Die Schritte laufen im Job `create-review-issue` des wiederverwendbaren Workflows `.github/workflows/create-review-issue.yml` (`ubuntu-latest`, `timeout-minutes: 10`, Shell `pwsh`); die Aufrufer liefern die Parameter über `with:`-Inputs:

1. **Guard-Schritt:** Das Repository-Secret `REVIEW_ISSUE_TOKEN` muss gesetzt sein — andernfalls schlägt der Lauf sofort sichtbar fehl (`exit 1` mit Anleitung). Es gibt bewusst keinen Fallback auf `GITHUB_TOKEN`, weil ein Issue des Autors `github-actions[bot]` von der Ereignisvalidierung verworfen würde und über die Duplikatprüfung künftige Zyklen blockierte.
2. **Label sicherstellen:** Das Typlabel wird idempotent per `gh label create ... --force` unter `GITHUB_TOKEN` angelegt (Farbe `#5319E7`, Konvention `automated-*`).
3. **Duplikatprüfung:** Existiert bereits ein offenes Issue mit dem Typlabel (`gh issue list --state open --label`), wird die Erzeugung übersprungen — erfolgreicher Lauf mit Log-Hinweis „Skipped: an open review issue already exists (#N)".
4. **Issue erzeugen:** `gh issue create` läuft unter `REVIEW_ISSUE_TOKEN`, damit der Repository-Owner als Autor erscheint — Voraussetzung für die Owner-Prüfung der `SeparatesKonto`-Ereignisvalidierung. Der Issue-Body beginnt mit der frei stehenden Zeile `@softwarewerkstatt`, gefolgt vom festen Prüfauftrag; der Lauf protokolliert die Issue-URL.

Eine `concurrency`-Gruppe je Workflow mit `cancel-in-progress: false` serialisiert Cron- gegen manuelle Läufe, sodass die Sequenz Duplikatprüfung → Erzeugung nicht halbiert werden kann.

### Aktivierung und Plattformverhalten

- `schedule`-Trigger feuern ausschließlich vom Default-Branch `main`; auch `workflow_dispatch` steht erst zur Verfügung, wenn die Workflow-Datei auf `main` liegt. Die Workflows werden daher erst nach der Promotion `staging` → `main` aktiv.
- GitHub kann geplante Läufe unter Last verzögern und deaktiviert Schedules nach 60 Tagen Repository-Inaktivität — ein verpasster Zyklus lässt sich per `workflow_dispatch` nachholen.
- Am 1. Januar fallen monatlicher und jährlicher Lauf zusammen — fachlich unproblematisch, da es sich um verschiedene Prüftypen mit eigenen Labels und eigenen `concurrency`-Gruppen handelt.

### Einmalige Betriebshandlungen (Rollout)

1. **Feingranulierten PAT des Owners anlegen:** GitHub → Settings → Developer settings → Personal access tokens → Fine-grained: Resource owner `martin-stromberg`, Repository access „Only select repositories" → dieses Repository, Repository permissions → **Issues: Read and write**. Feingranulierte PATs haben maximal 1 Jahr Gültigkeit — die Erneuerung ist als wiederkehrende Betriebsaufgabe einzuplanen.
2. **Secret hinterlegen:** PAT als Repository-Secret `REVIEW_ISSUE_TOKEN` speichern (Settings → Secrets and variables → Actions).
3. **Collaborator-Status prüfen:** `@softwarewerkstatt` muss Collaborator mit Schreibrecht auf dem Repository sein, damit GitHub die Mention-Benachrichtigung zustellt (Betriebsvoraussetzung des SeparatesKonto-Modus, siehe `docs/help/plugins/github-ereignisse/installation.md`).
4. **Probelauf:** Nach der Promotion nach `main` je Workflow einen `workflow_dispatch`-Lauf ausführen und prüfen, dass das Issue unter Autor `martin-stromberg` mit Typlabel und korrektem Body entsteht; ein zweiter Lauf muss „Skipped: an open review issue already exists" melden. Danach den Ereigniseingang in der SCM-Ereignisübersicht der Anwendung beobachten.

## Secrets und Umgebungsvariablen

| Name | Herkunft | Zweck |
|------|----------|-------|
| `GITHUB_TOKEN` | Automatisch von GitHub Actions bereitgestellt | Wird fuer die Semantic-Release-Dry-Run-Analyse sowie fuer die Erstellung des GitHub-Release inkl. Tag und Asset-Upload verwendet. Erfordert `contents: write`, aber keine Schreibfreigabe auf den geschuetzten `main`-Branch. |
| `REVIEW_ISSUE_TOKEN` | Manuell hinterlegtes Repository-Secret: feingranulierter PAT des Owners `martin-stromberg` (Repository-Zugriff nur auf dieses Repository, Permission **Issues: Read and write**) | Autor-Identität der von den Review-Workflows erzeugten Issues — die `SeparatesKonto`-Ereignisvalidierung akzeptiert Erwähnungen nur vom Repository-Owner; ein unter `GITHUB_TOKEN` erzeugtes Issue (Autor `github-actions[bot]`) würde verworfen. |

Abgesehen von `REVIEW_ISSUE_TOKEN` für die geplanten Review-Workflows (einmalige Betriebshandlung, siehe Abschnitt „Geplante Review-Workflows") sind keine weiteren, manuell zu hinterlegenden Secrets erforderlich.

## Troubleshooting-Guide

| Fehlerbild | Ursache | Lösung |
|------------|---------|--------|
| Workflow bricht bei `Restore`/`Publish` ab | Build-Fehler im .NET-Projekt | Fehlerausgabe im Job-Log prüfen, Build lokal mit `dotnet build` reproduzieren, Fix committen und erneut auf `main` pushen. |
| Kein neues Release trotz Push auf `main` | Commit-Nachrichten entsprechen nicht dem Conventional-Commits-Format bzw. enthalten nur Types ohne Versionswirkung (`docs:`, `refactor:`, `chore:`, `test:`, `perf:`, `ci:`) | Commit-Format gemäß [`CONTRIBUTING.md`](../CONTRIBUTING.md) prüfen. Falls eine Version dennoch nötig ist, manuellen Tag setzen (`git tag vX.Y.Z && git push origin vX.Y.Z`). |
| `release.zip` fehlt oder ist leer im erstellten Release | `publish/`-Verzeichnis war zum Zeitpunkt der ZIP-Erstellung leer oder nicht vorhanden (vorangegangener Build-Fehler) | Build-Schritt (`Publish`) im Job-Log prüfen, Ursache beheben, erneut pushen. |
| Release-Erstellung schlägt mit Auth-Fehler fehl | `GITHUB_TOKEN` hat keine ausreichenden Rechte fuer Tag-/Release-Erstellung | Repository-Einstellungen prüfen (`Settings` → `Actions` → `General` → `Workflow permissions`), sicherstellen, dass `contents: write` erlaubt ist. |
| Release-Job meldet `Protected branch update failed` / `GH006` | Ein Release-Step versucht direkt nach `main` zu pushen | Der automatische Release darf keinen `git push HEAD:main` ausfuehren. Stattdessen muss der Release ueber `gh release create --target $GITHUB_SHA` bzw. die GitHub-API erstellt werden. |
| `npm ci` schlägt fehl | NPM-Registry nicht erreichbar oder `package-lock.json` inkonsistent mit `package.json` | Workflow erneut ausführen (transiente Netzwerkfehler); bei dauerhaftem Fehler `package-lock.json` lokal aktualisieren und committen. |
| Review-Workflow schlägt im Guard- oder Erzeugungsschritt fehl | Repository-Secret `REVIEW_ISSUE_TOKEN` fehlt oder der hinterlegte PAT ist abgelaufen bzw. entzogen (feingranulierte PATs haben max. 1 Jahr Gültigkeit) | Secret gemäß Abschnitt „Geplante Review-Workflows" (erneut) hinterlegen und den Lauf per `workflow_dispatch` wiederholen. |

## Recovery-Verfahren bei Pipeline-Fehlern

- **Manueller Retry:** Fehlgeschlagene Workflow-Läufe können über die GitHub-Actions-UI (`Re-run jobs` bzw. `Re-run failed jobs`) erneut gestartet werden.
- **Fix + Re-Push:** Bei Build- oder Test-Fehlern wird die Ursache behoben und der Fix regulär über einen Pull Request nach `main` gemerged; der Release-Workflow läuft danach automatisch erneut.
- **Tag-Deletion (bei fehlerhaftem manuellem Tag):** Ein fälschlich gesetzter Tag kann lokal und remote gelöscht werden:
  ```bash
  git tag -d vX.Y.Z
  git push origin :refs/tags/vX.Y.Z
  ```
  Anschließend kann ein korrigierter Tag erneut gepusht werden.

## Monitoring und Logs

Der Status jedes Workflow-Laufs ist im Tab **Actions** des Repositories auf GitHub.com einsehbar. Jeder Schritt liefert ein eigenes, aufklappbares Log. Erstellte Releases inkl. Release Notes und ZIP-Download sind unter **Releases** im Repository sichtbar.
