# Infrastruktur und Konfiguration

## GitHub Workflows

### `.github/workflows/release.yml`
**Datei:** `.github/workflows/release.yml` (107 Zeilen)

- **Trigger:** `push` auf `main`, manuell Tags (`v*.*.*`)
- **Permissions:** `contents: write`, `pull-requests: read` (notwendig für Semantic Release)
- **Concurrency:** Serialisiert Release-Läufe zur Vermeidung von Wettläufen bei Versionsvergabe
- **Jobs:**
  - `release`: Führt auf `windows-latest` aus, nutzt Semantic Release zur Versionsvergabe, baut Anwendung, erstellt GitHub Release
  - `release-manual-tag`: Override für manuelle Tag-Pushes (Format `vX.Y.Z`), baut unabhängig von Commit-Typen
- **Secrets:** Nur `secrets.GITHUB_TOKEN` (Standard-GitHub-Autorisierung), keine privaten Keys/Credentials

### `.github/workflows/test.yml`
**Datei:** `.github/workflows/test.yml` (57 Zeilen)

- **Trigger:** `push` auf `main`, `pull_request` zu `main` (verhindert Doppellauf)
- **Concurrency:** Bricht ältere Testläufe ab bei neuem Push
- **Job:** 20 Min Timeout auf `windows-latest`, baut und testet `Softwareschmiede.Tests.csproj`
- **Test-Filter:** Schließt E2E und ConPTY-Tests aus (`--filter "Category!=E2E&Category!=ConPTY"`)
- **Secrets:** Keine

## GitHub Actions

### `.github/actions/build-and-package/action.yml`
**Datei:** `.github/actions/build-and-package/action.yml` (86 Zeilen)

- **Zweck:** Wiederverwendbare Action für Build und Packaging
- **Eingaben:** `release-version`, `release-tag` (optional)
- **Schritte:**
  1. Setup .NET 10 SDK
  2. `dotnet restore` (ohne spezifische Secret-Management-Anforderungen)
  3. `dotnet publish` für `Softwareschmiede.App` im Release-Modus zu `publish/`
  4. Rename `Softwareschmiede.App.exe` → `Softwareschmiede.exe` (zu Unterscheidung von Dev-Build)
  5. Schreib `version.json` mit Version, Tag, Commit SHA, Timestamp
  6. Verifizierung der Publish-Ausgabe (DLLs, Plugin-Verzeichnisse)
  7. `Compress-Archive` zu `release.zip`

### Secrets in Actions
- ✅ **GITHUB_TOKEN:** Standard-GitHub-Autorisierung, kein privater Key
- ✅ **Keine benutzerdefinierten Secrets:** Workflows referenzieren nur `secrets.GITHUB_TOKEN`

## .gitignore

**Datei:** `.gitignore` (383 Zeilen)

### Secrets und Sensitive Inhalte
- `/secrets/*` — Verzeichnis für lokal gehaltene Geheimnisse
- `*.pfx` — Certificate-Dateien
- `*launchSettings.json` — Lokale Startup-Konfigurationen (ggf. mit localhost-Ports)
- `*.db`, `*.db-shm`, `*.db-wal` — Lokale SQLite-Datenbanken

### Generierte und Temp-Dateien
- `bin/`, `obj/`, `out/` — Build-Ausgaben
- `Logs/`, `logs/` — Anwendungsprotokolle
- `.vs/` — Visual Studio Cache
- `node_modules/` — NPM-Abhängigkeiten (für Semantic Release)
- `TestResult*/`, `BuildLog.*` — Test-Ergebnisse

### Projekt-spezifisch
- `/.github/agents` — Agent-Verzeichnis
- `/.github/tools` — Tool-Verzeichnis
- `/.softwareschmiede/copilot-runtime` — Laufzeitdaten
- `*.copilot.task.md*`, `*.claude.task.md*` — IDE-Aufgabendateien
- `/src/Softwareschmiede/agent-packages/*` — Lokale Agent-Pakete
- `/test-results/` — Test-Ergebnisverzeichnis

### Offene Fragen
- Inwieweit werden `.json`-Dateien (z. B. `appsettings.*.json`) bereits durch Umgebungsvariablen in Produktion ersetzt?

## Build-Skripte

### `publish.ps1`
**Datei:** `publish.ps1` (6.056 Zeilen)

- **Zweck:** Lokale Publikation und Packaging
- **Secrets-Check:** Keine hardcodierten API-Keys oder Credentials erkannt
- *Detaillierte Analyse ausstehend*

### `start.ps1`
**Datei:** `start.ps1` (17 KB)

- **Zweck:** Lokales Repository-Startskript
- **Secrets-Check:** Keine hardcodierten API-Keys oder Credentials erkannt
- *Detaillierte Analyse ausstehend*

### `build.ps1`
- *Wird als Teil der Anforderung erwähnt, existiert aber nicht explizit — Builds laufen via `dotnet build` in Workflows*

## .claude/hooks

**Verzeichnis:** `.claude/hooks/` (16 Dateien)

### Sicherheits-relevante Hooks

#### `build_before_test.py`
- Baut Solution vor `dotnet test`
- Warnt vor laufender `Softwareschmiede.App.exe` (kann DLL-Lock verursachen)
- Nutzt `dotnet_lock.py` für Cross-Process-Lock-Management
- **Secrets:** Keine

#### `log_token_usage.py`
- Protokolliert Token-Verbrauch für Claude-API
- **Secrets:** Keine Hardcoding erkannt

#### Andere Hooks
- `check_*.py` — Validierungsskripte für Code-Qualität (XML-Kommentare, Enums, Entity States, etc.)
- `dotnet_lock.py` — Koordination zwischen Build und Test-Läufen
- `release_build_lock.py` — Freigabe des Build-Locks nach Test-Abschluss
- `test-csharp-startup.ps1` — WPF-Smoke-Test beim Stop-Hook

**Secrets:** Keine erkannt

## Konfigurationsdateien

### `src/Softwareschmiede/appsettings.json`
```json
{
  "Hosting": { "BasePath": "/" },
  "Logging": { "LogLevel": { "Default": "Information" } },
  "AllowedHosts": "*",
  "DirectoryStructure": { "CacheDurationSeconds": 300, "MaxDepth": 2, "Enabled": true }
}
```
- ✅ Keine Secrets, nur Standard-Einstellungen

### `src/Softwareschmiede/appsettings.Development.json`
- ✅ Gleiche Struktur wie oben, keine Secrets

### `src/Softwareschmiede/appsettings.Production.json`
- *Noch zu überprüfen*

### `CLAUDE.md`
- Leitfaden für Claude Code Session
- Enthält Richtlinien für Testing, Build, CI/CD
- **Secrets-Check:** Keine erkannt (enthält interne Dokumentation, aber keine Credentials)

### `.releaserc.json`
```json
{
  "branches": ["main"],
  "plugins": [
    "@semantic-release/commit-analyzer",
    "@semantic-release/release-notes-generator",
    "@semantic-release/changelog",
    "@semantic-release/git",
    "@semantic-release/github"
  ]
}
```
- Konfiguriert Semantic Release
- Assets: `CHANGELOG.md`, `package.json`, `release.zip`
- ✅ Keine Secrets

## Offene Punkte für öffentliche Veröffentlichung

1. **Secrets-Scan:** Noch keine vollständige automatisierte Secrets-Scanning (z. B. via `TruffleHog`, `detect-secrets`)
2. **Interne Kommentare:** Code-Review erforderlich für `INTERNAL:`, `CONFIDENTIAL:`, `TODO:` mit sensiblem Kontext
3. **Systempfade:** Überprüfung auf hartcodierte Windows-Pfade (z. B. `C:\Entwicklung\`, `C:\Users\`, UNC-Pfade)
4. **Debug-Endpunkte:** Überprüfung auf Test-spezifische APIs oder Routen (aktuell hauptsächlich Domain/Service-Layer, eher unproblematisch)
5. **Repository-Settings:** GitHub-Seite noch nicht auf "Public" gesetzt, Branch-Protection nicht konfiguriert
