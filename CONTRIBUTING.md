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

## Setup / Voraussetzungen

- **Windows** 10 (Build 17763+) oder 11 — WPF, Windows Credential Store und die Pseudo Console API
  (ConPTY) werden benötigt.
- **.NET SDK 10.0+** inkl. der **.NET Desktop-Workload** (`Microsoft.NET.Sdk.WindowsDesktop`), da
  `Softwareschmiede.App` und `Softwareschmiede.Tests` gegen
  `net10.0-windows10.0.17763.0` (WPF) bauen.
- **Git** und optional die **GitHub CLI** (`gh`).

```powershell
# Repository klonen
git clone https://github.com/martin-stromberg/Softwareschmiede.git
cd Softwareschmiede

# Bauen
dotnet build src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj
```

Weitere Details zu Installation und Konfiguration siehe [`README.md`](README.md).

## Coding-Standards

- **Naming:** PascalCase für Klassen/Methoden, camelCase für Parameter/Variablen, Präfix `I` für
  Interfaces.
- **Async:** Alle I/O-Operationen konsequent mit `async`/`await` — keine `.Result`- oder
  `.Wait()`-Aufrufe.
- **Logging:** `ILogger<T>` in allen Services — strukturiertes Logging mit aussagekräftigen
  Nachrichten und Parametern.
- **XML-Dokumentation:** Öffentliche Typen und Member benötigen XML-Doc-Kommentare;
  `WarningsAsErrors` ist auf `CS1591` (fehlender XML-Kommentar) gesetzt, der Build schlägt sonst
  fehl.
- **Plugin-Erweiterungen:** Neue Plugins implementieren `IPlugin` + `IGitPlugin`/`IKiPlugin`,
  setzen `PluginType` und werden als eigenes Projekt unter `plugins/` eingebunden (Discovery via
  `PluginManager`, keine direkte `AddScoped<...>`-Bindung).

## Test-Anforderungen

- Testprojekt: `src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj`.
- Kategorien: Unit-/Integration-/BUnit-Tests laufen ohne Einschränkung im CI. Tests der Kategorie
  `Category=E2E` (FlaUI/WPF) und `Category=ConPTY` sind vom CI ausgeschlossen, da GitHub-hosted
  Runner keine verlässliche interaktive Desktop-/Konsolen-Session bieten.
- **Lokaler E2E-Lauf ist vor jedem PR mit UI-relevanten Änderungen Pflicht:**

  ```powershell
  dotnet build src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj -c Debug
  dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --no-build -c Debug --filter "Category=E2E"
  ```

- CI-äquivalenter Lauf (ohne E2E/ConPTY):

  ```powershell
  dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category!=E2E&Category!=ConPTY" -c Debug
  ```

- Immer zuerst `dotnet build`, dann `dotnet test` — niemals `--no-build` bei geändertem Code.

## Pull-Request-Prozess

- Jeder PR benötigt **mindestens 1 Approval** (Code-Review-Pflicht).
- Branch muss aktuell mit `main` sein (rebase oder merge vor dem PR).
- Alle CI-Checks (`dotnet test`, `security-scan`) müssen grün sein.
- PR-Beschreibung enthält: Kontext, Änderungen, Testnachweis.
- Merges in `main` ausschließlich über Pull Requests.

## Community-Standards / Verhaltensregeln

- Respektvoller, konstruktiver Umgang in Issues, PRs und Diskussionen.
- Sachliches, lösungsorientiertes Feedback in Code-Reviews.
- Sicherheitsrelevante Themen bitte **nicht** öffentlich als Issue, sondern über den in
  [`SECURITY.md`](SECURITY.md) beschriebenen privaten Meldeweg einreichen.

## Maintainer

**[martin-stromberg](https://github.com/martin-stromberg)** ist alleiniger Maintainer dieses
Repositories und verantwortlich für Review und Merge von externen Beiträgen.
