# CI/CD: Release-Pipeline

Dieses Dokument beschreibt den automatisierten Release-Workflow (`.github/workflows/release.yml`), der die fertige Anwendung nach jedem Merge in `main` als versioniertes GitHub-Release bereitstellt.

## Trigger-Bedingungen

Der Workflow (`.github/workflows/release.yml`) kennt zwei unabhängige Jobs mit unterschiedlichen Triggern:

- **Job `release`** — ausgelöst bei einem `push` auf den Branch `main` (`startsWith(github.ref, 'refs/heads/')`). Pushes auf andere Branches (z. B. Feature-Branches) lösen keinen Release-Workflow aus.
- **Job `release-manual-tag`** — ausgelöst bei einem `push` eines Tags im Format `vX.Y.Z` (`startsWith(github.ref, 'refs/tags/')`, Trigger-Pattern `tags: ['v*.*.*']`). Ein Tag-Push löst *keinen* `branches: [main]`-Trigger aus — daher ist dies ein eigener Job, siehe [`CONTRIBUTING.md`](../CONTRIBUTING.md#manual-release-tag-override).

Beide Jobs bauen und veröffentlichen dieselbe Anwendung, unterscheiden sich aber in der Versionsermittlung (siehe unten).

## Workflow-Schritte

### Job `release` (Push auf `main`)

| # | Step | Zweck |
|---|------|-------|
| 1 | `Checkout` (`actions/checkout@v4`, `fetch-depth: 0`) | Lädt das Repository mit vollständiger Git-Historie, da Semantic Release alle Commits seit dem letzten Release analysieren muss. |
| 2 | `Setup Node.js` (`actions/setup-node@v4`, Node 20 LTS) | Stellt die Node.js-Laufzeitumgebung für Semantic Release bereit. |
| 3 | `Install dependencies` (`npm ci`) | Installiert Semantic Release und die konfigurierten Plugins aus `package.json`/`package-lock.json`. |
| 4 | `Build and package` (Composite Action `.github/actions/build-and-package`) | Führt `Setup .NET 10`, `dotnet restore`, `dotnet publish src/Softwareschmiede.App/Softwareschmiede.App.csproj -c Release -o publish/` (inkl. Plugin-DLLs über das `CopyPluginsToPublishOutput`-Target), eine Prüfung auf ein vollständiges `publish/`-Verzeichnis (Guard-Step) sowie die ZIP-Erstellung (`Compress-Archive` → `release.zip`) aus. Läuft auf `windows-latest`, da die Anwendung `UseWPF` mit `net10.0-windows` als Target Framework verwendet und daher nicht plattformübergreifend gebaut werden kann. Wird identisch von beiden Jobs genutzt, um Divergenz zwischen den Build-Schritten zu vermeiden. |
| 5 | `Semantic Release` (`cycjimmy/semantic-release-action`, auf Commit-SHA gepinnt) | Analysiert Commits seit dem letzten Tag, bestimmt die neue Version, generiert Release Notes, aktualisiert `CHANGELOG.md`, committed die Version zurück in Git und erstellt das GitHub-Release inkl. Upload von `release.zip`. Ein zuvor manuell gesetzter Tag (siehe unten) dient dabei als neue Ausgangsbasis für die nächste automatisch berechnete Version. |

### Job `release-manual-tag` (Push eines Tags `vX.Y.Z`)

| # | Step | Zweck |
|---|------|-------|
| 1 | `Checkout` (`fetch-depth: 0`) | Lädt das Repository auf dem Stand des gepushten Tags. |
| 2 | `Build and package` (Composite Action `.github/actions/build-and-package`) | Identisch zum `release`-Job (kein Node/Semantic-Release-Setup nötig, da die Version durch den Tag bereits feststeht). |
| 3 | `Create GitHub Release for manual tag` (`softprops/action-gh-release`, auf Commit-SHA gepinnt) | Erstellt direkt ein GitHub-Release für den gepushten Tag (`tag_name`/`name` = Tagname), generiert Release Notes automatisch (`generate_release_notes: true`) und lädt `release.zip` als Asset hoch — unabhängig von Commit-Typen, ohne Semantic-Release-Versionsberechnung. |

## Nebenläufigkeit und Supply-Chain-Absicherung

- **Concurrency-Guard:** Der Workflow definiert `concurrency: { group: release-${{ github.ref }}, cancel-in-progress: false }`. Dadurch werden parallele Läufe für denselben Ref (z. B. schneller Merge nach `main` gefolgt von einem manuellen Tag-Push) serialisiert statt gleichzeitig ausgeführt — verhindert Wettlaufsituationen bei der von Semantic Release berechneten Version sowie dem Rück-Push von Commit/Tag nach `main`.
- **Action-Pinning:** `cycjimmy/semantic-release-action` und `softprops/action-gh-release` laufen mit `permissions: contents: write` und werden daher auf einen festen Commit-SHA statt auf einen beweglichen Versions-Tag gepinnt (Kommentar im Workflow zeigt die zugehörige Version). Damit kann ein nachträglich umgebogener Tag bei einer dieser Fremd-Actions keinen unbemerkten Code mit Schreibrechten auf das Repository ausführen. Bei einem gewollten Versions-Update muss der SHA im Workflow manuell aktualisiert werden.
- **Publish-Guard:** Die Composite Action `build-and-package` prüft nach `dotnet publish`, ob `publish/Softwareschmiede.App.exe` sowie mindestens eine Plugin-DLL unter `publish/plugins/` vorhanden sind, und bricht den Workflow andernfalls kontrolliert ab — verhindert, dass ein leeres oder unvollständiges `release.zip` unbemerkt als GitHub-Release-Asset veröffentlicht wird.

## Secrets und Umgebungsvariablen

| Name | Herkunft | Zweck |
|------|----------|-------|
| `GITHUB_TOKEN` | Automatisch von GitHub Actions bereitgestellt | Wird von Semantic Release für Git-Commits (Changelog/Version) und für die Erstellung des GitHub-Release inkl. Asset-Upload verwendet. Erfordert Schreibrechte auf `main` (über `permissions: contents: write` im Workflow gewährt). |

Es sind keine weiteren, manuell zu hinterlegenden Secrets erforderlich.

## Troubleshooting-Guide

| Fehlerbild | Ursache | Lösung |
|------------|---------|--------|
| Workflow bricht bei `Restore`/`Publish` ab | Build-Fehler im .NET-Projekt | Fehlerausgabe im Job-Log prüfen, Build lokal mit `dotnet build` reproduzieren, Fix committen und erneut auf `main` pushen. |
| Kein neues Release trotz Push auf `main` | Commit-Nachrichten entsprechen nicht dem Conventional-Commits-Format bzw. enthalten nur Types ohne Versionswirkung (`docs:`, `refactor:`, `chore:`, `test:`, `perf:`, `ci:`) | Commit-Format gemäß [`CONTRIBUTING.md`](../CONTRIBUTING.md) prüfen. Falls eine Version dennoch nötig ist, manuellen Tag setzen (`git tag vX.Y.Z && git push origin vX.Y.Z`). |
| `release.zip` fehlt oder ist leer im erstellten Release | `publish/`-Verzeichnis war zum Zeitpunkt der ZIP-Erstellung leer oder nicht vorhanden (vorangegangener Build-Fehler) | Build-Schritt (`Publish`) im Job-Log prüfen, Ursache beheben, erneut pushen. |
| Semantic Release schlägt mit Auth-Fehler fehl | `GITHUB_TOKEN` hat keine Schreibrechte auf `main` | Repository-Einstellungen prüfen (`Settings` → `Actions` → `General` → `Workflow permissions`), sicherstellen, dass `contents: write` erlaubt ist. |
| `npm ci` schlägt fehl | NPM-Registry nicht erreichbar oder `package-lock.json` inkonsistent mit `package.json` | Workflow erneut ausführen (transiente Netzwerkfehler); bei dauerhaftem Fehler `package-lock.json` lokal aktualisieren und committen. |

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
