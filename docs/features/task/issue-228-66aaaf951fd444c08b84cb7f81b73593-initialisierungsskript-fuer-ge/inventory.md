# Bestandsaufnahme: Initialisierungsskript für geklonte Repositories

Analyse des bestehenden Projektcodes in Bezug auf die Anforderung zur automatischen Ausführung von Initialisierungsskripten nach dem Klonen von Repositories. Der Fokus liegt auf den Vorbild-Strukturen (insbesondere `RepositoryStartKonfiguration` und `RepositoryStartskriptService`), die als Architektur-Vorlagen für die Implementierung dienen.

## Zusammenfassung

**Vorhandene Strukturen:**
- `GitRepository` Entität mit Navigationseigenschaft zu `RepositoryStartKonfiguration`
- `RepositoryStartKonfiguration` als separate Entität mit aktivierbarem Schalter (`Aktiv`)
- `RepositoryStartskriptService` als spezialisierter Service für Script-Ausführung mit Path-Traversal-Validierung
- `EntwicklungsprozessService` mit integriertem Hook zur Startskript-Ausführung nach dem Klonen
- `ProjectDetailViewModel` mit UI-Elementen für Projekt- und Repository-Verwaltung
- `ICliRunner` Interface zur Process-Ausführung
- `IPluginManager` für Plugin-Zugriff
- `SoftwareschmiededDbContext` mit DbSet für `RepositoryStartKonfigurationen`

**Fehlende Strukturen (noch nicht implementiert):**
- `RepositoryInitialisierungKonfiguration` Entität (offene Frage: 1:1-Beziehung oder separate Tabelle)
- `RepositoryInitialisierungService` Service
- `InitialisierungsskriptRelativePfad` Property auf `GitRepository`
- `InitialisierungsskriptSuggestionen` und `SelectedInitialisierungsskript` Properties im ViewModel
- UI-Steuerelemente im View
- Tests für Initialisierungsskript-Funktionalität

## Details

- [Datenmodelle](inventory/models.md)
- [Services und Logik](inventory/logic.md)
- [Interfaces](inventory/interfaces.md)
- [ViewModels](inventory/viewmodels.md)
- [Tests](inventory/tests.md)
