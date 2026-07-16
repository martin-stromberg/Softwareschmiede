# Versionierung und Release-Management

## Semantic Versioning

**Status:** ✅ **Vollständig implementiert**

### Format: Major.Minor.Patch

- **Major:** Breaking Changes (Inkompatibilität)
- **Minor:** Neue Features (abwärtskompatibel)
- **Patch:** Bugfixes (abwärtskompatibel)

### Beispiel
- Aktuelle Version: **v1.12.0** (12 Minor-Releases, zuletzt 2026-07-15)
- Format: `vX.Y.Z` (führendes `v` im Git-Tag)

## Automatische Versionsverwaltung

### `.releaserc.json` Konfiguration

**Datei:** `.releaserc.json` (27 Zeilen)

```json
{
  "branches": ["main"],
  "plugins": [
    "@semantic-release/commit-analyzer",      // Analysiert Commit-Typen
    "@semantic-release/release-notes-generator", // Generiert Release Notes
    "@semantic-release/changelog",             // Aktualisiert CHANGELOG.md
    "@semantic-release/git",                   // Committed & pusht Tags
    "@semantic-release/github"                 // Erstellt GitHub Release
  ]
}
```

### Workflow

1. **Commit-Analyse:** 
   - `feat:` → Minor-Version (z. B. v1.11.0 → v1.12.0)
   - `fix:` → Patch-Version (z. B. v1.12.0 → v1.12.1)
   - `feat!:` oder `BREAKING CHANGE:` → Major-Version (z. B. v1.0.0 → v2.0.0)
   - Andere (`refactor:`, `docs:`, `chore:`, `test:`) → keine neue Version

2. **Changelog-Update:** 
   - `@semantic-release/changelog` aktualisiert `CHANGELOG.md` automatisch
   - Format: GitHub Markdown mit Links zu Commits und PRs

3. **Git-Operationen:**
   - Erstelle Commit: `chore(release): <version>`
   - Erstelle Git-Tag: `v<version>`
   - Push zu `origin/main` mit Tags

4. **GitHub Release:**
   - Erstelle GitHub Release mit Auto-Generated Release Notes
   - Lade `release.zip` als Asset hoch

## Git-Tags

### Existierende Tags (Ascending)

```
v1.0.0   (Initialrelease)
v1.0.1
v1.1.0
v1.2.0
v1.3.0
v1.4.0
v1.5.0
v1.6.0
v1.7.0
v1.8.0
v1.8.1
v1.9.0
v1.10.0
v1.11.0
v1.12.0  (Neueste, 2026-07-15)
```

**Status:** 15 Tags, konsistent formatiert als `vX.Y.Z`

## Package.json Versionierung

**Datei:** `package.json`

```json
{
  "name": "softwareschmiede",
  "version": "0.0.0",  // Wird von Semantic Release überschrieben
  "private": true,
  "description": "KI-gestützter Softwareentwicklungs-Workflow"
}
```

**Hinweis:** `version: "0.0.0"` ist Placeholder; Semantic Release überschreibt dies nach Release-Berechnung.

## Version-Manifest im Release

### `publish/version.json` (Generiert bei Publish)

**Ort:** Wird von `.github/actions/build-and-package/action.yml` erzeugt

**Format:**
```json
{
  "version": "1.12.0",               // Semver ohne 'v'
  "tagName": "v1.12.0",              // Git-Tag
  "commit": "<GITHUB_SHA>",          // Commit-SHA
  "createdAtUtc": "2026-07-15T..."   // ISO 8601 Timestamp
}
```

**Zweck:** 
- Versionsinformation in der Anwendung selbst verfügbar
- Update-Checks vergleichen gegen `version.json`
- In Programm-Update-Feature genutzt

## Changelog

**Datei:** `CHANGELOG.md` (21 KB)

### Struktur jedes Release-Eintrags

```markdown
# [<Version>](commit-compare-link) (<Date>)

### Bug Fixes
* <Description> ([<commit-sha>](github-link))

### Features
* <Description> ([<commit-sha>](github-link))
```

### Beispiel (v1.12.0)

```markdown
# [1.12.0](https://github.com/martin-stromberg/Softwareschmiede/compare/v1.11.0...v1.12.0) (2026-07-15)

### Bug Fixes
* Dateiexplorer-Nacharbeiten (OOM-Schutz, Ladezustand, Cache, toter Code) ([1cf68f6](https://github.com/...))

### Features
* Dateiexplorer-Nacharbeiten abschliessen ([b0a35bf](https://github.com/...))
* Dateiexplorer-Register mit Standard- und Vergleichsmodus ([3f578e6](https://github.com/...))
```

**Status:** ✅ Konsistent formatiert, bis v1.0.0 zurückreichend

## Release-Prozess

### Automatisch (Merge nach `main`)

1. **GitHub Actions `release.yml` triggert**
2. **Semantic Release:**
   - Liest alle Commits seit letztem Tag
   - Berechnet nächste Version
   - Generiert Release Notes
   - Aktualisiert `CHANGELOG.md`
   - Committed zurück zu `main`
   - Erstellt Git-Tag
3. **Build & Publish:**
   - `.github/actions/build-and-package` baut Anwendung
   - Generiert `version.json`
   - Erstellt `release.zip`
4. **GitHub Release erstellen:**
   - Lädt `release.zip` als Release Asset hoch
   - Nutzt automatisch generierte Release Notes

### Manuell (Tag-Push)

**Verwendung:** Wenn explizite Versionskontrolle erforderlich (z. B. Hotfix, Version-Override)

```bash
git tag v2.5.0
git push origin v2.5.0
```

**Effekt:**
- Triggert separaten Job `release-manual-tag` in `.github/workflows/release.yml`
- Baut nur für diesen Tag (ignoriert Conventional Commits)
- Erstellt GitHub Release mit automatisch generierten Notes
- Unabhängig vom regulären Release-Prozess

**Hinweis:** Nächster regulärer Push auf `main` nutzt diesen Tag als neue Ausgangsbasis für Versionsberechnung.

## Offene Punkte für öffentliche Veröffentlichung

1. **Initiale öffentliche Version:** 
   - Mit welcher Version soll das öffentliche Release starten?
   - Optionen:
     - **Option A:** Aktuell bei v1.12.0 bleiben (empfohlen)
     - **Option B:** Auf v2.0.0 springen (signalisiert Stabilität)
     - **Option C:** Auf v0.1.0 zurücksetzen (Neustart-Signal)

2. **Git-Historia:**
   - Sollen alte Commits/Tags (v1.0.0–v1.11.0) entfernt werden?
   - Empfehlung: Nein, vollständige Historia ist transparent

3. **Versionierungs-Konsistenz:**
   - Sollte die .NET-Assembly-Version (`AssemblyVersion` in `.csproj`) mit Git-Tag synchronisiert werden?
   - Aktuell nicht explizit gesetzt

4. **Version-File Speicherort:**
   - `version.json` im Publish-Verzeichnis OK?
   - Alternative: Im Quellcode unter `src/` halten?

## Cutting Release (Checkliste)

### Vor Release
1. ✅ Alle Features/Fixes im `main`-Branch
2. ✅ Tests bestanden (CI/CD grün)
3. ✅ CHANGELOG reviewed (wird automatisch aktualisiert)
4. ✅ Commit-Konventionen eingehalten

### Release-Vorgang
1. Merge zu `main` oder manueller Tag-Push
2. GitHub Actions lädt Release hoch
3. Verifiziere `release.zip` Inhalt
4. Optional: Teste Release-Download/Installation

### Nach Release
1. ✅ GitHub Release sichtbar
2. ✅ CHANGELOG aktualisiert
3. ✅ `package.json` aktualisiert
4. ✅ Git-Tag gesetzt
