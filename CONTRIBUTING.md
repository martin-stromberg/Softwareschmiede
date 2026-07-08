# Contributing

Dieses Dokument beschreibt die Commit-Konventionen und Team-Richtlinien für Beiträge zu diesem Repository. Die automatisierte Release-Pipeline (siehe [`docs/CI_CD.md`](docs/CI_CD.md)) wertet die hier beschriebenen Commit-Nachrichten aus, um Versionen automatisch zu bestimmen.

## Commit Message Convention (Conventional Commits)

Alle Commits auf `main` und Feature-Branches müssen nach [Conventional Commits](https://www.conventionalcommits.org/) formatiert sein:

### Format

```
<type>(<scope>): <subject>

<body>

<footer>
```

### Types

- **feat:** Neue Feature
- **fix:** Bugfix
- **refactor:** Code-Umstrukturierung (ohne Feature/Fix)
- **docs:** Dokumentation
- **test:** Tests hinzufügen/aktualisieren
- **chore:** Build-Prozess, Dependencies, etc.
- **perf:** Performance-Verbesserungen
- **ci:** CI/CD-Konfiguration

### Versioning Impact

- `feat:` → Minor-Version (`x.Y.z`)
- `fix:` → Patch-Version (`x.y.Z`)
- `feat!:` oder `BREAKING CHANGE:` → Major-Version (`X.y.z`)
- Andere Types (`refactor:`, `docs:`, `chore:`, `test:`, `perf:`, `ci:`) → keine neue Version

### Beispiele

```
feat(terminal): Add Ctrl+V clipboard paste support

fix(core): Resolve race condition in TerminalBuffer synchronization

docs: Update README with release process

feat!: Redesign plugin API (BREAKING CHANGE: old plugin format no longer supported)
```

## Breaking Changes

Breaking Changes MÜSSEN mit `BREAKING CHANGE:` im Commit-Footer oder mit `!` nach dem Type (z. B. `feat!:`) gekennzeichnet werden. Nur so wird von Semantic Release eine Major-Version erzeugt.

## Manual Release Tag (Override)

Wenn eine bestimmte Version erzwungen werden soll, statt sie automatisch aus den Commits zu berechnen:

```bash
git tag v2.5.0
git push origin v2.5.0
```

Ein Tag-Push löst in `.github/workflows/release.yml` den eigenständigen Job `release-manual-tag` aus (unabhängig vom `release`-Job, der nur auf Pushes nach `main` reagiert). Dieser Job baut die Anwendung, verpackt sie als ZIP und erstellt direkt ein GitHub-Release für genau diesen Tag inkl. automatisch generierter Release Notes — unabhängig von den seit dem letzten Release verwendeten Commit-Typen. Der nächste reguläre Push auf `main` verwendet diesen Tag anschließend als neue Ausgangsbasis für die automatische Versionsberechnung durch Semantic Release.

## Rules

1. Commit-Typen MÜSSEN eingehalten werden.
2. Breaking Changes MÜSSEN mit `BREAKING CHANGE:` im Footer oder `!` nach Type gekennzeichnet werden.
3. Tags werden NUR gesetzt, wenn bewusst ein Versions-Override erforderlich ist.
4. Merges in `main` AUSSCHLIESSLICH über Pull Requests; Tests müssen bestanden sein.
