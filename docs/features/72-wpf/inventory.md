# Bestandsaufnahme: WPF-Desktopanwendung Softwareschmiede

Diese Bestandsaufnahme analysiert den bestehenden Projektcode zur Anforderung für eine WPF-Desktopanwendung für Windows 11, die Entwicklungsprojekte und Aufgaben mit KI-Automatisierung verwaltet. Das System wird von einer bestehenden Blazor-Server-Anwendung durch eine native WPF-Desktopanwendung mit SQLite-Datenbank ersetzt.

## Zusammenfassung

### Bereits vorhanden

- **Datenmodell**: Umfassend implementiert mit Entitäten für `Projekt`, `Aufgabe`, `GitRepository`, `Protokolleintrag`, `TestErgebnis`, `IssueReferenz`, `AppEinstellung`, `PluginKonfiguration`, `RepositoryStartKonfiguration`, `DiffResult` und weiteren Entities.
- **Enum-System**: Alle erforderlichen Enums sind definiert (`AufgabeStatus`, `ProjektStatus`, `ProtokollTyp`, `PluginKategorie`, `BenachrichtigungsModus`, `BenachrichtigungsKanal`, `DiffType`, `DiffResultStatus`).
- **Plugin-Architektur**: Interfaces `IPlugin`, `IGitPlugin`, `IKiPlugin` sind vollständig definiert mit Methodensignaturen und Dokumentation. `PluginManager` implementiert Plugin-Discovery und -Instanziierung.
- **Service-Schicht**: Kernservices sind vorhanden:
  - `AufgabeService` – CRUD und Lebenszyklus von Aufgaben
  - `ProjektService` – Projektverwaltung
  - `ProtokollService` – Protokollverwaltung
  - `KiAusfuehrungsService` – Background-Verwaltung von KI-Ausführungen mit Session-Streaming
  - `GitOrchestrationService` – Git-Operationen und Orchestrierung
  - `EntwicklungsprozessService` – Koordination des KI-gestützten Entwicklungsprozesses
  - `AufgabeRecoveryService` – Recovery festhängender Aufgaben
  - `PluginSettingsService` – Plugin-Konfigurationsverwaltung
- **Value Objects**: `AgentInfo`, `PullRequest`, `TestResult`, `TestErgebnisInfo`, `Issue`, `PluginSettingGroup` sind als Records definiert.
- **Tests**: Unit-Tests und Integrationstests vorhanden für Services, Plugins und Ablauf-Szenarien.

### Noch nicht implementiert / Offene Punkte

- **WPF UI-Komponenten**: Keine XAML-Views oder ViewModels vorhanden. Blazor-Komponenten sind noch im Codebase (nicht entfernt).
- **Dark Mode & Einstellungs-UI**: Keine WPF-spezifische Implementierung für Dark Mode, Einstellungsseiten oder Plugin-Settings-UI.
- **Prozess-Einbettung**: Fensterhandle-Management zur Einbettung von CLI-Prozessen in WPF-Controls nicht implementiert.
- **Benachrichtigungsservice**: Keine Audio-Benachrichtigungen oder Benachrichtigungslogik entsprechend `BenachrichtigungsModus` und `BenachrichtigungsKanal`.
- **Kontextkomprimierung**: Logik für Soft-/Hard-Limits und automatische Kontextkomprimierung skizziert aber nicht vollständig.
- **Mehrsprachigkeit**: Deutsche Oberflächen vorhanden, aber keine Lokalisierungsinfrastruktur erkennbar.
- **Datenbank-Migration**: Initialisierung und EF Core Migrations existieren, aber noch begrenzt (nur InitialCreate und wenige Updates).

## Details

- [Datenmodell](inventory/models.md)
- [Enums](inventory/enums.md)
- [Interfaces](inventory/interfaces.md)
- [Services & Logik](inventory/logic.md)
- [Value Objects](inventory/value_objects.md)
- [Tests](inventory/tests.md)
