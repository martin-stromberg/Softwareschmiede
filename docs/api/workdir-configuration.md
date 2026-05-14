# Technische Dokumentation – Konfigurierbares Arbeitsverzeichnis

## Zweck

Dieses Dokument beschreibt die technische Umsetzung des Features **„konfigurierbares Arbeitsverzeichnis für lokale Repositories“**.

## Überblick

Das Feature besteht aus drei Kernbausteinen:

1. **Persistente Einstellung** in `AppEinstellungen` mit Schlüssel `repositories.workdir`
2. **Laufzeit-Auflösung** des effektiven Basisverzeichnisses inkl. Fallback-Logik
3. **Verwendung im Entwicklungsprozess** beim Erzeugen des lokalen Klonpfads

## Relevante Komponenten

| Komponente | Datei | Verantwortung |
|---|---|---|
| `ArbeitsverzeichnisSettingsService` | `src/Softwareschmiede/Application/Services/ArbeitsverzeichnisSettingsService.cs` | Lesen/Speichern und Validieren der Einstellung |
| `IArbeitsverzeichnisResolver` | `src/Softwareschmiede/Domain/Interfaces/IArbeitsverzeichnisResolver.cs` | Abstraktion für Laufzeit-Auflösung |
| `ArbeitsverzeichnisResolver` | `src/Softwareschmiede/Infrastructure/Services/ArbeitsverzeichnisResolver.cs` | Ermittelt nutzbaren Basispfad, prüft Schreibbarkeit, liefert Fallback |
| `EntwicklungsprozessService` | `src/Softwareschmiede/Application/Services/EntwicklungsprozessService.cs` | Bildet finalen Klonpfad aus aufgelöstem Basispfad |
| `Einstellungen.razor(.cs)` | `src/Softwareschmiede/Components/Pages/Einstellungen.*` | UI zum Bearbeiten/Zurücksetzen der Einstellung inkl. Inline-Validierung |

## Persistenzmodell

- **Tabelle:** `AppEinstellungen`
- **Schlüssel:** `Schluessel` (unique)
- **Feature-Key:** `repositories.workdir`
- **Wertsemantik:** `null` oder leer = Default verwenden

Migration: `202605090001_Add_AppEinstellung_Workdir`

## Auflösung und Pfadbildung

### Schritt 1: Basisverzeichnis auflösen

`ArbeitsverzeichnisResolver.ResolveAsync()` liefert:

- `ResolvedPath`: nutzbarer Basispfad
- `UsedFallback`: ob Fallback aktiv ist
- `ReasonCode`: Ursache (`configured`, `no-configured-path`, `invalid-path`, `not-writable-or-unavailable`)
- `ConfiguredPath`: optional ursprünglicher Konfigurationswert

Fallback-Basis ist `Path.GetTempPath()`.

### Schritt 2: Klonpfad erzeugen

`EntwicklungsprozessService.ProzessStartenAsync(...)` erzeugt den Klonpfad so:

`Path.Combine(workdirResult.ResolvedPath, "softwareschmiede", aufgabeId.ToString())`

Damit gelten:

- Bei konfigurierter Basis `D:\Repos` → `D:\Repos\softwareschmiede\<aufgabeId>`
- Bei Fallback → `<Temp>\softwareschmiede\<aufgabeId>`

## Validierungsregeln beim Speichern

`ArbeitsverzeichnisSettingsService.ValidatePathForSave(...)` erzwingt:

- Pfad muss absolut sein
- Pfad darf keine ungültigen Zeichen enthalten
- Pfad muss in einen gültigen Full Path normalisierbar sein
- Verzeichnis muss erzeugbar/erreichbar sein (`Directory.CreateDirectory`)

## Laufzeitverhalten und Hinweise

- Ist ein gespeicherter Pfad zur Laufzeit nicht nutzbar, wird auf Fallback gewechselt.
- Der Fallback wird über `ReasonCode` geloggt.
- Beim Start eines Entwicklungsprozesses wird ein Protokolleintrag erzeugt, wenn Fallback aktiv ist.
- Die Einstellungsseite zeigt bei aktivem Fallback einen Hinweis an.

## Zusammenhang mit „separatem Arbeitsverzeichnis + Git-Workflow-Fallback“

Das hier konfigurierte Basis-Arbeitsverzeichnis steuert den Zielpfad für den Modus `SeparateWorkingDirectory`.
Darauf bauen die folgenden LocalDirectory-Workflows auf:

- **Git-Bootstrap im separaten Workspace:** Quelle wird per Dateikopie übernommen, im Working Directory `git init` ausgeführt und ein initialer Snapshot-Commit erstellt.
- **Pull ohne Merge + Nutzerhinweis:** Pull synchronisiert `SourceDirectory -> WorkingDirectory`, erzeugt keinen Merge-Commit.
- **Push als Datei-Sync statt `git push`:** Push synchronisiert `WorkingDirectory -> SourceDirectory`.
- **Delete-Sync über Git-Status:** Löschungen/Umbenennungen werden über `git status --porcelain` ermittelt und im Quellverzeichnis gespiegelt.

## Grenzen und bekannte Einschränkungen

- Das Workdir-Setting selbst stellt keine Remote-Git-Funktion bereit (kein PR/Issue/Remote-Branching).
- Konflikthafte Pull-Situationen mit lokalen Änderungen werden per Guard (`uncommitted changes`) abgebrochen.
- Ein UI-Bestätigungsdialog für Pull ist als Restpunkt dokumentiert, aber nicht automatisiert getestet.

## Tests

Relevante Testklassen:

- `ArbeitsverzeichnisSettingsServiceTests`
- `ArbeitsverzeichnisResolverTests`
- `EntwicklungsprozessServiceTests`
- `EinstellungenBaseArbeitsverzeichnisTests`
- `WorkdirMigrationTests` (Integration)
