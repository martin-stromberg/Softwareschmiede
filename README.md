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
- **Plugin-basierte KI-Ausführung** über eingebettete interaktive CLI-Terminal-Sitzungen (ConPTY mit diagnostiziertem Pipe-Fallback)
- **IDE-Integration** mit Visual Studio für `.sln`/`.slnx` und Visual Studio Code als Fallback
- **Dateiexplorer und Diff-Ansicht** direkt in der Aufgabendetailansicht
- **Pull-Request-Workflow** mit PR-Erstellung, Statusanzeige und GitHub-Monitoring
- **Autonome Aufgaben** mit Projektleiter-Agent und Unteragenten-Orchestrierung
- **Pausierbare Aufgaben** mit manueller Pause und automatischer Pausierung bei erkannten KI-Session-Limits
- **Programmupdate aus der Anwendung** gegen GitHub-Releases mit konfigurierbarem Update-Modus und optionaler Prerelease-Berücksichtigung
- **CLI-Aufzeichnung mit Konsolentestfenster** zur Diagnose des Terminal-Renderpfads — byte-exakter Mitschnitt, `.clireplay`-Export und zeitgesteuerte Wiedergabe

## Terminalintegration

KI-CLI-Tools (Claude CLI, GitHub Copilot CLI, Codex CLI, Devin CLI) laufen interaktiv in einem eingebetteten Terminal direkt in der Aufgabendetailansicht:

- Die Anbieter-CLI wird direkt aus der vom Plugin gelieferten Startbeschreibung gestartet — ohne `cmd.exe`-Zwischenschale. `.cmd`/`.bat`-Shims (z. B. npm-Installationen) werden automatisch zu `cmd.exe /d /s /c` normalisiert.
- Vor jedem Start prüft eine Preflight-Diagnose u. a. PTY-Verfügbarkeit, Executable-Auflösung und CLI-Health. Steht kein Pseudo-Terminal zur Verfügung oder deklariert das Plugin keine PTY-Unterstützung, greift ein diagnostizierter Pipe-Fallback — sichtbar in der Statuszeile als „… (eingeschränkter Modus – kein Pseudo-Terminal)" und als `[Terminal-Diagnose]`-Zeile im Aufgabenprotokoll. CLIs, die zwingend ein Pseudo-Terminal benötigen, schlagen stattdessen mit einer verständlichen Fehlermeldung fehl.
- Der eigene VT100/ANSI-Renderer unterstützt volle Farben, Alternate Screen (Vollbild-TUIs), Scroll-Regionen und 1000 Zeilen Scrollback; die Terminalgröße folgt Fensteränderungen automatisch.
- Beim erneuten Öffnen einer Aufgabenseite wird die Terminalanzeige aus einem begrenzten Replay-Puffer (Standard 512 KiB, `Terminal:ReplayBufferByteBudget`) wiederhergestellt.
- Jede Session wird automatisch als byte-exakter Mitschnitt mit Zeitstempel pro Ausgabe-Chunk aufgezeichnet (`CliOutputRecorder` neben `CliOutputProtokollWriter` über `CompositeTerminalOutputSink`; Budget `Terminal:AufzeichnungByteBudget`, Standard 8 MiB — `<= 0` deaktiviert den Mitschnitt). `[Terminal-Diagnose]`-Markerzeilen gelangen dabei nicht in den Mitschnitt.
- Der Mitschnitt lässt sich als `.clireplay`-Datei exportieren und im **Konsolentestfenster** (Einstellungen → Allgemein → Diagnose) zeitgesteuert durch denselben Renderpfad wieder abspielen — ein Diagnose-Werkzeug für Rendering- und Streaming-Fehler.

Details siehe [Terminal-Dokumentation](docs/help/terminal/index.md).

## Issue-Referenz in der issue.md

Beim Start einer Aufgabe erzeugt `EntwicklungsprozessService.CreateIssueFileAsync()` eine `issue.md` im lokalen Klon-Verzeichnis. Ist an der Aufgabe eine `IssueReferenz` mit gültiger Nummer hinterlegt, wird automatisch ein eigener Abschnitt in die Datei geschrieben:

```markdown
# Aufgabe: {Titel}

**Aufgaben-ID:** {Id}
**Branch:** {BranchName}
**Erstellt:** {ErstellungsDatum:yyyy-MM-dd}

## Verknüpftes Issue

**Kennung:** #{IssueNummer}
**Titel:** {IssueReferenz.Titel}

## Anforderung

{AnforderungsBeschreibung}
```

- Der Block `## Verknüpftes Issue` erscheint **nur**, wenn `Aufgabe.IssueReferenz != null` **und** `IssueNummer > 0`.
- Fehlt die Referenz oder ist die Nummer nicht gesetzt, entfällt der Block; die restliche `issue.md` bleibt unverändert.
- Keine Datenmodell- oder Datenbankänderung notwendig — die `IssueReferenz` wird per Eager Loading bereits in `AufgabeService.GetDetailAsync()` mitgeladen.

## CLI-Rohausgabe und -Aufzeichnung exportieren

Die Aufgabendetailansicht bietet in der Ribbon-Gruppe **CLI** zwei Exporte an:

- **„Rohausgabe exportieren"** schreibt die persistierten Protokolleinträge vom Typ `ProtokollTyp.CliOutput` zeilenweise in eine `.raw`-Datei:
  - Der Dialogtitel lautet **„CLI-Rohausgabe exportieren"**, der Vorschlagsname `cli-output-{AufgabeId}.raw`.
  - Die Reihenfolge der exportierten Zeilen entspricht der chronologischen Protokollreihenfolge.
  - Das Ziel muss auf `.raw` enden; bei Dialog-Abbruch wird keine Datei geschrieben.
- **„Aufzeichnung exportieren"** (`CliReplayExport`) schreibt den byte-exakten Mitschnitt der Session als `.clireplay`-Binärdatei für das Konsolentestfenster:
  - Der Dialogtitel lautet **„CLI-Aufzeichnung exportieren"**, der Vorschlagsname `cli-replay-{AufgabeId}.clireplay`; das Ziel muss auf `.clireplay` enden.
  - Der Mitschnitt läuft automatisch während jeder CLI-Session mit und enthält die rohen `OutputChunk`-Bytes mit Zeitstempel pro Chunk. Im Speicher bleiben die Mitschnitte der letzten acht Aufgaben (auch nach dem Session-Ende exportierbar); das Byte-Budget pro Session steuert `Terminal:AufzeichnungByteBudget` (Standard 8 MiB, `<= 0` deaktiviert den Mitschnitt). Bei Budget-Überschreitung bleibt das intakte Präfix als unvollständige Aufzeichnung erhalten.
  - Die `.clireplay`-Datei enthält neben den Chunk-Records Metadaten wie Aufgaben-ID, Plugin-Name, Start-/Endzeitpunkt, initiale Terminalgröße und das `IstVollstaendig`-Flag (`CliReplayAufzeichnungStore`).
  - Liegt für die Aufgabe keine Aufzeichnung vor (kein Session-Start oder deaktivierter Mitschnitt), erscheint ein entsprechender Fehlerhinweis.

Die Implementierung liegt in `TaskDetailViewModel`, `CliRawExportService`, `CliReplayExportService`, `CliOutputRecorder`, `CompositeTerminalOutputSink`, `CliReplayAufzeichnungStore`, `KiAusfuehrungsService` und `WpfDialogService`. Abgedeckt wird das Feature u. a. durch `TaskDetailViewModelTests_CliRawExport`, `TaskDetailViewModelTests_CliReplayExport`, `CliReplayExportServiceTests`, `CliOutputRecorderTests`, `CliReplayAufzeichnungStoreTests`, `CompositeTerminalOutputSinkTests`, `KiAusfuehrungsServiceCliAufzeichnungTests`, `TaskDetailViewTests` und `E2E_CliRawExport`.

## Konsolentestfenster

Zur Diagnose von Rendering- und Streaming-Fehlern im Terminal-Pfad (`AnsiSequenceParser` → `TerminalBuffer` → `TerminalControl`) existiert ein eigenes Diagnose-Werkzeug:

- Der Einstieg liegt in den **Einstellungen** im Tab **„Allgemein"** im Abschnitt **„Diagnose"** über die Schaltfläche **„Konsolentestfenster öffnen"** (`KonsolenTestOeffnen`). Das Fenster (Titel „Konsolentest") ist nicht modal und bleibt parallel zur Live-Ansicht nutzbar.
- **„Aufzeichnung öffnen…"** lädt eine exportierte `.clireplay`-Datei. Die `TerminalReplaySession` (eine `ITerminalSession`-Implementierung) spielt die aufgezeichneten Chunks zeitgesteuert durch denselben echten Renderpfad wie eine Live-Session — es gibt keinen separaten Replay-Renderer.
- Die Wiedergabe wird über **„Abspielen"**, **„Neu starten"** und **„Pausieren/Fortsetzen"** gesteuert. Die einstellbare **Zeitraffer-Schwelle** (Sekunden) deckelt die Wartezeit vor jedem Chunk auf diesen Wert — kürzere Pausen bleiben zeitreal, `0` bedeutet maximale Geschwindigkeit.
- Rechts zeigt eine Quell-Ansicht die aufgezeichneten Chunks synchron zur Wiedergabeposition (Index, Offset, Byteanzahl und Quelltext mit sichtbar gemachten Steuersequenzen wie `␛`, `\r`, `\n`). Bei einer unvollständigen Aufzeichnung (Budget überschritten) blendet das Fenster einen entsprechenden Hinweis ein.

Die Implementierung liegt in `KonsolenTestViewModel`, `KonsolenTestDialog`, `TerminalReplaySession`, `CliChunkQuelltextFormatter`, `CliChunkAnzeigeEintrag`, `SettingsViewModel` und `WpfDialogService`. Abgedeckt wird das Feature u. a. durch `TerminalReplaySessionTests`, `KonsolenTestViewModelTests`, `CliChunkQuelltextFormatterTests` und den E2E-Test `E2E_KonsolenTestfenster`.

## Programmupdate

Die Anwendung kann sich selbst gegen die GitHub-Releases von `martin-stromberg/Softwareschmiede` prüfen und aktualisieren:

- In der Fußzeile der Navigations-Seitenleiste prüft der Button **„Programmupdate prüfen"** (`⟳ Prüfen`) manuell auf neue Releases. Ein gefundenes Update wird über den Button **„Programmupdate starten"** (`⇧ Update`) angeboten und vorbereitet.
- Vor der Installation prüft ein Sicherheitsdialog, ob laufende CLI-Aufgaben das Update blockieren würden. Ein Fortschrittsdialog zeigt die Phasen Download, Entpacken und Update-Vorbereitung an und erlaubt den Abbruch.
- Der eigentliche Austausch der Dateien erfolgt durch ein externes Update-Skript nach dem Beenden der Anwendung.

Der Update-Modus ist in den **Einstellungen** im Tab **„Allgemein"** unter **„Updates"** über die Auswahlbox **„Update-Modus"** konfigurierbar:

| Option | Wirkung |
|--------|---------|
| `Aus` | Update-Prüfung und -Installation sind deaktiviert; der Prüf-Button bleibt sichtbar, aber deaktiviert. |
| `Nur prüfen` (Standard) | Die Prüfung läuft; ein gefundenes Update wird angeboten, aber nie automatisch installiert. |
| `Bei Programmstart prüfen und ausführen` | Einmalig nach dem ersten Rendern des Hauptfensters wird geprüft und ein gefundenes Update automatisch installiert. |

Zusätzlich aktiviert die Checkbox **„Prerelease-Versionen laden"** die Berücksichtigung von Vorabversionen. Ein Release gilt als Prerelease, wenn das GitHub-Flag `prerelease` gesetzt ist oder der Tag ein SemVer-Prerelease-Suffix (z. B. `-rc.1`) trägt; ist die Option deaktiviert, werden beide Fälle ausgeschlossen. `GitHubReleaseClient` fragt die Releases paginiert ab (`?per_page=100`, `Link`-Header mit `rel=next`), überspringt Drafts, ungültige Tags und Einträge ohne `release.zip`-Asset und wählt die höchste zulässige SemVer-Version über alle Seiten.

Beide Werte werden in der SQLite-Einstellungstabelle unter den Schlüsseln `updates.mode` und `updates.includePrereleases` gespeichert (`AppEinstellungService.GetUpdateSettingsAsync`/`SetUpdateSettingsAsync`). Beim Speichern löst `SettingsViewModel` das Event `UpdateSettingsSaved` aus: `MainWindowViewModel` invalidiert ein bestehendes Update-Angebot und bricht einen noch nicht übergebenen Update-Vorgang ab. Vor jeder Prüfung und vor dem Updater-Start werden die gespeicherten Werte erneut gelesen.

Die Implementierung liegt in `UpdateService`, `GitHubReleaseClient`, `UpdateVersionComparer`/`SemanticUpdateVersion`, `AppEinstellungService`, `SettingsViewModel` und `MainWindowViewModel` (einmalige Startprüfung über `MainWindow.ContentRendered` → `InitializeUpdatesAfterWindowReadyAsync`). Abgedeckt wird das Feature u. a. durch `AppEinstellungServiceTests_UpdateSettings`, `UpdateServiceTests_Options`, `UpdateServiceTests_PrereleaseChain`, `UpdateVersionComparerTests_SemVer`, `GitHubReleaseClientTests_Filters`, `GitHubReleaseClientTests_Pagination`, `MainWindowViewModelTests_UpdateStartup`, `MainWindowViewModelTests_UpdateSettingsReadFailure` und `E2E_UpdateSettings`.

## Aufgaben pausieren und Session-Limits erkennen

Reguläre Aufgaben lassen sich zeitgebunden pausieren. Zusätzlich erkennt die Anwendung Session-Limits in der CLI-Ausgabe und pausiert betroffene Aufgaben automatisch.

### Pause einstellen und aufheben

- In der Ribbon-Gruppe **Aufgabe** der Aufgabendetailansicht öffnet **„Pause einstellen"** (`PauseEinstellen`) den `AufgabePausierenDialog` mit Datum- und Uhrzeit-Eingabe (Vorbelegung: nächste volle Minute).
- Der Button ist nur sichtbar, wenn die Aufgabe den Status `Neu`, `Gestartet` oder `Wartend` hat und keine Autonome Aufgabe ist.
- `AufgabeService.SetPauseAsync` persistiert den UTC-Zeitpunkt in `Aufgabe.PausiertBisUtc` und schreibt einen `ProtokollTyp.SystemMeldung`-Eintrag. Über **„Pause aufheben"** im selben Dialog lässt sich eine aktive Pause vorzeitig beenden.
- Der Pause-Endzeitpunkt muss in der Zukunft liegen. Das Ende der Pause ist rein zeitbasiert — es gibt keine automatische Wiederaufnahme der CLI.

### Anzeige

- Die Kachel in der Seitenleisten-Sektion **„Aktive Aufgaben"** zeigt `⏸ Pausiert (noch hh:mm:ss)` — ab 24 Stunden Restzeit `d.hh:mm:ss` — und wird per `Opacity`-Trigger auf `0,55` abgeschwächt (`KiAusfuehrungsStatusConverter`, `ActiveTasksListControl`).
- Die Detailansicht blendet im Ribbon zusätzlich `⏸ Pausiert bis dd.MM.yyyy HH:mm` ein.

### Wirkung einer aktiven Pause

- Blockiert: manueller Start (`ProzessStartenAsync`/`ProzessStartenUndCliStartenAsync`), CLI-Neustart (`CliNeustartenAsync`), Wiederherstellung (`AufgabeRecoveryService`) sowie das Senden und Planen von Prompts.
- Bereits geplante zeitgesteuerte Prompts verschiebt der `PromptZeitVersandService` auf das Pausenende statt sie zu verwerfen.
- Ein laufender CLI-Prozess wird niemals unterbrochen — die Pause greift nur für neu ausgelöste Aktionen.

### Automatische Pause bei Session-Limit

- Enthält eine CLI-Ausgabezeile den Marker `[[SOFTWARESCHMIEDE_RATE_LIMIT:<ISO8601>]]` mit gültigem Zeitstempel, ruft der `CliOutputProtokollWriter` den `KiPluginLimitService`.
- Der Service persistiert den Reset-Zeitpunkt als `AppEinstellung` unter `plugins.sessionlimit.<KiPluginPrefix>` und pausiert alle aktiv laufenden regulären Aufgaben desselben Plugin-Prefix (pro Aufgabe ein `SystemMeldung`-Eintrag).
- Eine manuell gesetzte, später endende Pause wird dabei nicht verkürzt (Max-Semantik); ein bereits abgelaufener Zeitpunkt wird zwar persistiert, löst aber keine Pause aus.
- Marker ohne gültigen Zeitstempel erzeugen weiterhin nur den `ProtokollTyp.RateLimit`-Eintrag.

### Update-Sicherheitsprüfung

- `CliUpdateSafetyService.CheckAsync` wertet Aufgaben, deren KI-Plugin ein zukünftiges Session-Limit gespeichert hat, nicht als Update-Risiko — die sonst übliche Heartbeat-Toleranz entfällt für sie komplett.

Abgedeckt wird das Feature u. a. durch `KiPluginLimitServiceTests`, `AufgabeServiceTests_Pause`, `AufgabePausierenDialogViewModelTests`, `CliUpdateSafetyServiceTests` sowie die E2E-Tests `E2E_AufgabePausieren` und `E2E_SessionLimitPause`.

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
5. Optional die bisherige CLI-Ausgabe als `*.raw` (**„Rohausgabe exportieren“**) oder den byte-exakten Mitschnitt als `*.clireplay` (**„Aufzeichnung exportieren“**) sichern.
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
- `Terminal:ReplayBufferByteBudget`
- `Terminal:DefaultCols`
- `Terminal:DefaultRows`
- `Terminal:AufzeichnungByteBudget`

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
