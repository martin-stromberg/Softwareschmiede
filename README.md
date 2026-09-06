# 🔨 Softwareschmiede

> KI-gestützter Softwareentwicklungs-Workflow als lokale Windows-Desktopanwendung.

[![Pre-Release](https://github.com/martin-stromberg/Softwareschmiede/actions/workflows/staging-ci.yml/badge.svg)](https://github.com/martin-stromberg/Softwareschmiede/actions/workflows/staging-ci.yml)
[![Release](https://github.com/martin-stromberg/Softwareschmiede/actions/workflows/release.yml/badge.svg)](https://github.com/martin-stromberg/Softwareschmiede/actions/workflows/release.yml)
[![License](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

Softwareschmiede bündelt Projektverwaltung, Aufgabensteuerung, Git-Workflows und KI-CLI-Ausführung in einer nativen WPF-Anwendung. Die Oberfläche läuft lokal unter Windows, speichert Arbeitsdaten in SQLite und lädt SCM-, KI- und IDE-Plugins zur Laufzeit aus dem `plugins/`-Verzeichnis.

## Überblick

- **UI:** WPF auf `.NET 10` (`src/Softwareschmiede.App`)
- **Kernlogik:** Application/Domain/Infrastructure in `src/Softwareschmiede`
- **Persistenz:** SQLite via EF Core
- **Logging:** Serilog (Konsole + Rolling File)
- **Plugin-Modell:** GitHub, BitBucket, Local Directory, GitHub Copilot, Claude CLI, Codex, Devin
- **Tests:** xUnit, FluentAssertions, Moq, FlaUI-E2E

## Kernfunktionen

- **Projekt- und Aufgabenverwaltung** mit lokalem Aufgabenstatus, Protokollierung und To-Do-Listen
- **Plugin-basierte SCM-Integration** für GitHub, BitBucket und lokale Arbeitsverzeichnisse
- **Plugin-basierte KI-Ausführung** über eingebettete CLI-Sitzungen mit ConPTY
- **IDE-Integration** mit Visual Studio für `.sln`/`.slnx` und Visual Studio Code als Fallback
- **Dateiexplorer und Diff-Ansicht** direkt in der Aufgabendetailansicht
- **Pull-Request-Workflow** mit PR-Erstellung, Statusanzeige und GitHub-Monitoring
- **Autonome Aufgaben** mit Projektleiter-Agent und Unteragenten-Orchestrierung
- **Programmupdate aus der Anwendung** gegen GitHub-Releases

## CLI-Rohausgabe exportieren

Die Aufgabendetailansicht kann die protokollierte CLI-Rohausgabe als Datei exportieren:

- In der Ribbon-Gruppe **CLI** gibt es den Button **„Rohausgabe exportieren“**.
- Der Dialogtitel lautet **„CLI-Rohausgabe exportieren“**.
- Als Vorschlagsname wird `cli-output-{AufgabeId}.raw` verwendet.
- Exportiert werden ausschließlich persistierte Protokolleinträge vom Typ `ProtokollTyp.CliOutput`.
- Die Reihenfolge der exportierten Zeilen entspricht der chronologischen Protokollreihenfolge.
- Das Ziel muss auf `.raw` enden; bei Dialog-Abbruch wird keine Datei geschrieben.

Die Implementierung liegt in `TaskDetailViewModel`, `CliRawExportService`, `IDialogService` und `WpfDialogService`. Abgedeckt wird das Feature u. a. durch `TaskDetailViewModelTests_CliRawExport`, `TaskDetailViewTests` und `E2E_CliRawExport`.

## Voraussetzungen

| Komponente | Requirement | Hinweis |
|------------|-------------|---------|
| Windows | 10 (Build 17763+) oder 11 | Pflicht für WPF, Windows Credential Store und ConPTY |
| .NET SDK | 10.0+ | benötigt für Build und Test |
| Git | aktuell | für Repository-Workflows |
| GitHub CLI (`gh`) | aktuell | für GitHub-Operationen und Releases |
| Copilot CLI (`copilot`) | optional | für `Softwareschmiede.Plugin.GitHubCopilot` |
| Claude CLI (`claude`) | optional | für `Softwareschmiede.Plugin.ClaudeCli` |
| Codex CLI (`codex`) | optional | für `Softwareschmiede.Plugin.Codex` |
| Devin CLI (`devin`) | optional | für `Softwareschmiede.Plugin.Devin` |
| Visual Studio Code (`code`) | optional | IDE-Fallback |

## Schnellstart

```powershell
git clone https://github.com/martin-stromberg/Softwareschmiede.git
cd Softwareschmiede

dotnet restore Softwareschmiede.slnx
dotnet build src/Softwareschmiede.App/Softwareschmiede.App.csproj -c Debug
dotnet run --project src/Softwareschmiede.App/Softwareschmiede.App.csproj
```

Beim Build kopiert `CopyPluginsToOutput` die Plugin-DLLs nach `bin/<Configuration>/plugins/`.

## Typischer Ablauf

1. Projekt anlegen und ein SCM-Plugin bzw. Repository zuordnen.
2. Aufgabe anlegen oder aus externen Quellen übernehmen.
3. KI-Plugin auswählen und den Entwicklungsprozess starten.
4. CLI-Ausgabe in der Aufgabendetailansicht verfolgen.
5. Optional über **„Rohausgabe exportieren“** die bisherige CLI-Ausgabe als `*.raw` sichern.
6. Änderungen prüfen, To-Dos abschließen und optional einen Pull Request erstellen.

## Konfiguration

### Zugangsdaten

Tokens werden nicht in Dateien gespeichert, sondern über den **Windows Credential Store** gelesen.

| Schlüssel | Zweck |
|-----------|-------|
| `Softwareschmiede.GitHub.Token` | GitHub Personal Access Token |
| `Softwareschmiede.ClaudeCli.Token` | Anthropic API Key für Claude CLI |
| `Softwareschmiede.Codex.ExecutablePath` | Optionaler absoluter Pfad zur Codex-CLI |
| `Softwareschmiede.Codex.CommandLineParameters` | Zusätzliche Codex-CLI-Argumente |
| `Softwareschmiede.Devin.ExecutablePath` | Optionaler absoluter Pfad zur Devin-CLI |
| `Softwareschmiede.Devin.CommandLineParameters` | Zusätzliche Devin-CLI-Argumente |

Beispiel für GitHub:

```powershell
cmdkey /generic:Softwareschmiede.GitHub.Token /user:github /pass:<DEIN_TOKEN>
```

### App-Einstellungen

Die Konfigurationsbasis liegt in `src/Softwareschmiede/appsettings*.json`. Im aktuellen Stand sind dort u. a. konfiguriert:

- `DirectoryStructure:Enabled`
- `DirectoryStructure:MaxDepth`
- `DirectoryStructure:CacheDurationSeconds`
- `AutonomAufgaben:Enabled`
- `AutonomAufgaben:DefaultTokenBudget`
- `AutonomAufgaben:DefaultRuntimeLimitMinutes`
- `AutonomAufgaben:HeartbeatTimeoutSeconds`
- `AutonomAufgaben:MaxConcurrentUnteragenten`
- `AutonomAufgaben:SkillAutogenerationEnabled`
- `AutonomAufgaben:MaxClones`
- `AutonomAufgaben:MaxFeatureBranches`

### Datenbank und Logs

- **Produktive Releases:** `%LocalAppData%\Softwareschmiede\softwareschmiede.db`
- **RC-/Entwicklungsbuilds:** `{AppContext.BaseDirectory}\softwareschmiede.db`
- **Test-Override:** `SOFTWARESCHMIEDE_TEST_DB_PATH`
- **Logs:** `{AppContext.BaseDirectory}\logs\softwareschmiede-<Datum>.log`

### `start.ps1`

`start.ps1` aktualisiert alle `Properties/launchSettings.json`-Dateien im Repository automatisch auf freie lokale HTTP-Ports. Das ist für lokale Debug-Sessions gedacht; das Skript akzeptiert dabei keinen manuellen Portparameter, sondern vergibt Ports selbst.

## Projektstruktur

```text
.
├── src/
│   ├── Softwareschmiede/                  # Kernlogik, EF Core, Services
│   ├── Softwareschmiede.App/              # WPF-Desktopanwendung
│   ├── Softwareschmiede.Plugin.Contracts/ # Plugin-Verträge
│   ├── Softwareschmiede.Tests/            # Unit-/UI-/E2E-Tests
│   └── Softwareschmiede.IntegrationTests/ # Integrationstests
├── plugins/                               # Plugin-Projekte
├── docs/                                  # Feature-, Hilfe- und CI/CD-Dokumentation
├── scripts/                               # Build-/Release-Hilfsskripte
└── Softwareschmiede.slnx
```

## Tests

Reguläre und OS-nahe Tests werden getrennt ausgeführt:

```powershell
# Reguläre Tests
dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category!=OsInterface" -c Debug
dotnet test src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj --filter "Category!=OsInterface" -c Debug

# OS-nahe Tests (best effort, inkl. E2E/ConPTY)
dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category=OsInterface" -c Debug
dotnet test src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj --filter "Category=OsInterface" -c Debug

# Nur E2E-Tests
dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category=OsInterface&FullyQualifiedName~E2E" -c Debug
```

Für Coverage verwenden die CI-Workflows `XPlat Code Coverage` und erzwingen auf `staging` eine **Line-Coverage von mindestens 70 %** für die regulären Tests.

## CI/CD

Die Repository-Automation ist branchbasiert aufgebaut:

- **`pr-staging-ci.yml`** prüft Pull Requests nach `staging` mit Format-Check, Security Scan, Build und Tests.
- **`staging-ci.yml`** baut auf Push nach `staging`, erzeugt bei Versionsänderungen Pre-Releases und prüft die Coverage-Schwelle.
- **`staging-to-main-promotion.yml`** erstellt nach erfolgreichem Staging-Lauf einen Draft-PR von `staging` nach `main`.
- **`verify-pr-source.yml`** erlaubt Pull Requests nach `main` ausschließlich aus `staging`.
- **`release.yml`** erstellt auf `main`-Pushes oder `v*.*.*`-Tags GitHub-Releases.
- **`sync-staging-with-main.yml`** erstellt nach einem Release einen automatischen Backmerge-PR von `main` nach `staging`.
- **`security-scan.yml`** führt zusätzlich einen geplanten Dependency-Sicherheitscheck aus.

Die Release-Pipeline nutzt `semantic-release` aus `package.json` sowie die GitHub CLI für Release-Erstellung und Asset-Upload.

## Dokumentation

- [Anwendungsdokumentation](docs/help/index.md)
- [CI/CD-Dokumentation](docs/CI_CD.md)
- [Contributing Guide](CONTRIBUTING.md)
- [Security Policy](SECURITY.md)
- [Änderungsprotokoll](changes.log)

## Lizenz

Dieses Projekt steht unter der [MIT-Lizenz](LICENSE).
