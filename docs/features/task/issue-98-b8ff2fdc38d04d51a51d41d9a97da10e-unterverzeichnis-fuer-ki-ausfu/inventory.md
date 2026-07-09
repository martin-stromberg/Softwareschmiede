# Bestandsaufnahme: Unterverzeichnis für KI-Ausführung

Diese Analyse dokumentiert den aktuellen Zustand des Projekts bezogen auf die Anforderung zur Verwaltung von Arbeitsverzeichnissen für jedes Git-Repository. Die Anforderung sieht vor, dass ein relativer Pfad zum Arbeitsverzeichnis (relativ zum Repository-Root) konfiguriert und gespeichert wird, damit die CLI-Ausführung nicht im Root-Verzeichnis, sondern im angegebenen Unterverzeichnis ausgeführt werden kann.

## Zusammenfassung

| Bereich | Status | Befunde |
|---------|--------|---------|
| **Datenmodell** | Unvollständig | `RepositoryStartKonfiguration` existiert, Property `WorkingDirectoryRelativePath` fehlt |
| **Service-Logik** | Unvollständig | `KiAusfuehrungsService` und `GitOrchestrationService` existieren, keine Logik für Arbeitsverzeichnis-Auflösung |
| **Presentation Layer** | Unvollständig | `RepositoryAssignViewModel` und Dialog existieren, UI für Verzeichnisauswahl fehlt |
| **Tests** | Unvollständig | Testklassen existieren, keine Tests für neue Funktionalität |
| **Datenbankmigrationen** | Unvollständig | Keine Migration für neue Property vorhanden |
| **Interfaces/Services** | Unvollständig | `DirectoryStructureBrowserService` nicht vorhanden |

## Details

- [Datenmodelle](inventory/models.md)
- [Logik-Services](inventory/logic.md)
- [Presentation Layer](inventory/presentation.md)
- [Tests](inventory/tests.md)
