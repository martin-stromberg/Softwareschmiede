# Bestandsaufnahme: Unterstützung eines Staging-Branch / Basis-Branch-Konfiguration

Diese Analyse erfasst die bestehende Kodebasis bezüglich der Anforderung zur Konfigurierbarkeit eines Basis-Branch beim Git-Repository, von dem neue Feature-Branches abgezweigt werden sollen.

## Zusammenfassung

### Vorhanden
- **Repository-Konfiguration**: `GitRepository` und `RepositoryStartKonfiguration` Entities existieren und sind mit der DB verknüpft.
- **Git-Plugin-Infrastruktur**: Umfangreiches `IGitPlugin`-Interface mit Methoden für Branch-Operationen (`CreateBranchAsync`, `CheckoutRemoteBranchAsync`, `GetRemoteBranchesAsync`, `GetDefaultBranchAsync`).
- **Branch-Setup-Logik**: `EntwicklungsprozessService.ProzessStartenAsync()` und `SetupBranchAsync()` orchestrieren das Repository-Setup mit optionalem `basisBranchName`-Parameter.
- **PR-Erstellung**: `GitOrchestrationService.PullRequestErstellenAsync()` handhabt PR-Erstellung über das Git-Plugin.
- **Tests**: Unit- und Integrationstests für Repository-Setup und Git-Operationen vorhanden.

### Fehlt / Nicht implementiert
- **Persistierte Basis-Branch-Eigenschaft**: `GitRepository` hat keine Eigenschaft für einen konfigurierten Basis-Branch (z. B. `DefaultSourceBranchName` oder `BaseBranchName`).
- **Datenbank-Migration**: Keine Migration zur Ergänzung der Basis-Branch-Spalte in der `git_repository`-Tabelle.
- **Feature-Branch-Erstellung vom Basis-Branch**: `CreateBranchAsync()` erstellt einen neuen Branch, aber ohne Angabe eines Basis-Branch, von dem er abgezweigt werden soll.
- **Validierung des Basis-Branch**: Keine Logik zur Prüfung, ob ein konfigurierter Basis-Branch im Remote-Repository existiert.
- **PR-Ziel-Branch-Parameter**: `CreatePullRequestAsync()` im `IGitPlugin`-Interface unterstützt keinen `baseBranch`-Parameter für die Zielangabe.
- **UI-Komponenten**: Keine Eingabfelder für die Basis-Branch-Konfiguration bei der Repository-Zuordnung.
- **Tests für Basis-Branch-Szenarien**: Keine Unit-/Integrationstests für Basis-Branch-Validierung und Feature-Branch-Erstellung vom konfigurierten Basis-Branch.

## Details

- [Datenmodell](inventory/models.md)
- [Logik & Services](inventory/logic.md)
- [Interfaces](inventory/interfaces.md)
- [Tests](inventory/tests.md)
