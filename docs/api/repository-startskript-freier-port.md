# Repository-Startskript mit freier Portzuweisung

## Übersicht

Dieses Dokument beschreibt den internen Application-Contract für das Feature „repository-startskript-freier-port“.

1. Repositorybezogene Persistenz einer Startkonfiguration
2. Ausführung eines Startskripts beim Start des Entwicklungsprozesses
3. Entkoppelte, skriptinterne Portauflösung

Es handelt sich um einen Service-/Domänen-Contract und **nicht** um einen HTTP-Endpoint-Contract.

## Technische Komponenten

### Persistenzmodell

- `RepositoryStartKonfiguration` (`src/Softwareschmiede/Domain/Entities/RepositoryStartKonfiguration.cs`)
- Beziehung: `GitRepository` 1:0..1 `RepositoryStartKonfiguration`
- EF-Mapping: `SoftwareschmiededDbContext`
- Migration: `20260514114235_202605141200_AddRepositoryStartKonfiguration`

Persistierte Felder:

| Feld | Typ | Bedeutung |
|---|---|---|
| `StartScriptRelativePath` | `string` | Relativer Skriptpfad im Repository |
| `StartScriptArgumentsTemplate` | `string?` | Optionales Argument-Template (`{Port}`, `{RepositoryPath}`) |
| `PortModus` | `RepositoryStartPortModus` | `Auto`, `Fest`, `ScriptGesteuert` |
| `PortBereichVon` / `PortBereichBis` | `int?` | Optionaler Portbereich bzw. fester Port |
| `Aktiv` | `bool` | Aktiviert/deaktiviert die Ausführung |

### Konfigurations-API (Application Layer)

- `ProjektService.SaveRepositoryStartKonfigurationAsync(...)`
- `ProjektService.GetRepositoryStartKonfigurationAsync(...)`

Validierungsregeln:

- Skriptpfad ist Pflichtfeld.
- Skriptpfad muss relativ sein (kein absoluter Pfad).
- Bei `Fest` ist ein Port erforderlich.
- Portbereich muss entweder vollständig gesetzt oder vollständig leer sein.

### Skriptausführung beim Prozessstart

- `EntwicklungsprozessService.ProzessStartenAsync(...)`
- Hook: nach erfolgreichem Clone/Branch, vor Agentenpaket-Deploy
- `RepositoryStartskriptService.RunAsync(...)` wird nur ausgeführt, wenn `StartKonfiguration` vorhanden ist

`RepositoryStartskriptService`:

- Löst den relativen Skriptpfad gegen den Klonpfad auf.
- Blockiert Pfad-Traversal außerhalb des Repository-Roots.
- Führt `powershell.exe` mit minimaler Standardargumentliste aus.
- Übergibt **keine** Portsteuer-Argumente und keine Port-Env-Variablen.

Standardargumente:

- `-NoProfile -NonInteractive -ExecutionPolicy Bypass`
- `-File <resolved script>`

Fehlerverhalten:

- Fehlendes Skript -> `InvalidOperationException`
- CLI-Fehler (`ExitCode != 0`) -> `InvalidOperationException`
- Bei Fehler wird der lokale Klonpfad im Prozessstart aufgeräumt

## Verhaltensmatrix

| Bedingung | Verhalten |
|---|---|
| `Aktiv = false` | Kein Skriptaufruf |
| `Aktiv = true`, gültiges Skript | Skript wird ausgeführt (Portlogik im Skript selbst) |
| Ungültiger/absoluter Skriptpfad | Konfiguration wird beim Speichern abgelehnt |
| Traversal `..\` außerhalb Repo | Skriptaufruf wird blockiert |
| Skriptlauf mit Exit-Code != 0 | Prozessstart bricht kontrolliert mit Fehler ab |

## Testnachweise

- `RepositoryStartskriptServiceTests`
- `ProjektServiceTests` (Save/Get + Validierung)
- `ProjektDetailRepositoryFormTests` (UI-Formular + Persistenz)
- `ProgramDiWiringTests` (DI-Registrierung)

Zusätzliche Planungs-/Testdokumente:

- [testplan-repository-startskript-freier-port.md](../tests/testplan-repository-startskript-freier-port.md)
- [testluecken-repository-startskript-freier-port.md](../tests/testluecken-repository-startskript-freier-port.md)
- [start-ps1-visual-studio-freier-http-port.md](./start-ps1-visual-studio-freier-http-port.md)

## Verknüpfte Dokumentation

- Flow: [repository-startskript-freier-port-flow.md](../flows/repository-startskript-freier-port-flow.md)
- Flow (`start.ps1` lokal): [start-ps1-visual-studio-freier-http-port-flow.md](../flows/start-ps1-visual-studio-freier-http-port-flow.md)
- Business: [F020 – Repository-Startskript mit freier Portzuweisung](../business/features/F020-repository-startskript-freier-port.md)
- Requirements: [repository-startskript-freier-port-requirements-analysis.md](../requirements/repository-startskript-freier-port-requirements-analysis.md)
