# Bestandsaufnahme: Repository-Suggestion-Panel

Diese Bestandsaufnahme analysiert den bestehenden Code der Softwareschmiede-Anwendung bezüglich der Anforderung "Repository-Suggestion-Panel auf der Projektdetailseite". Das Feature soll ein Panel hinzufügen, das unzugeordnete Repositories aus allen verfügbaren Git-Plugins in einer nach `UpdatedAt` absteigend sortierten Liste anzeigt.

## Zusammenfassung

### Was ist bereits vorhanden

- **Datenmodelle**: `Projekt`, `GitRepository`, `AvailableRepository` sind vollständig implementiert und persistiert
- **Plugin-System**: `IPluginManager` und `IGitPlugin` sind vorhanden und können alle verfügbaren SCM-Plugins auflisten
- **Repository-Abfrage-Methode**: `IGitPlugin.GetAvailableRepositoriesAsync()` existiert bereits und liefert verfügbare Repositories mit `UpdatedAt`-Eigenschaft
- **Sortierlogik**: Sortierung nach `UpdatedAt` absteigend ist bereits in `RepositoryAssignViewModel.ReloadRepositoriesForSelectedPlugin()` implementiert
- **UI-Foundation**: `ProjectDetailViewModel` existiert und lädt bereits Projekt-Details inkl. Aufgaben und Issues
- **Dialog-System**: Bestehender Repository-Zuweisungs-Dialog (`RepositoryAssignViewModel`, `RepositoryAssignDialog`) kann als Vorbild dienen
- **Tests**: Umfangreiche Unit-Tests für Service-Logik und ViewModels existieren bereits

### Was noch fehlt oder erweitert werden muss

- **Service-Methode**: Keine `GetUnassignedRepositoriesAsync()`-Methode in `ProjektService` — muss neu implementiert werden
  - Aggregiert alle Repositories aus **allen** SCM-Plugins
  - Filtert unzugeordnete Repositories (nicht in `Projekt.Repositories`)
  - Sortiert nach `UpdatedAt` absteigend
  - Fehlerbehandlung: Überspringt fehlerhafte Plugins
- **ViewModel-Property**: `ProjectDetailViewModel` benötigt eine neue `UnassignedRepositories`-Property
- **UI-Panel**: `ProjectDetailView.xaml` benötigt ein neues Panel für die unzugeordneten Repositories
- **Tests**: Unit-Tests für neue Service-Methode und ViewModel-Property erforderlich

### Offene Designfragen (aus Anforderung)

1. Plugin-Methode für Repository-Abfrage: `IGitPlugin.GetAvailableRepositoriesAsync()` existiert bereits ✓
2. Datumsformat für `UpdatedAt`: Noch zu klären (z.B. "vor 2 Stunden", "26.06.2025")
3. Panel-Platzierung: Über oder unter der Aufgaben-Kachel?
4. Panel-Größe: Fest oder flexibel?
5. Interaktion: Nur informativ oder direkte Zuordnung möglich?
6. Filter und Suche: Gewünscht?
7. Performance: Limit/Paginierung für viele Repositories?
8. Echtzeit-Updates: Automatisch oder nur beim Laden?

## Details

- [Datenmodelle](inventory/models.md) — `Projekt`, `GitRepository`, `AvailableRepository`
- [Logikklassen und Services](inventory/logic.md) — `ProjektService`, `IPluginManager`, `IGitPlugin`
- [Enums](inventory/enums.md) — `ProjektStatus`, `AufgabenFilterTyp`, `PluginType`
- [Interfaces](inventory/interfaces.md) — `IPluginManager`, `IGitPlugin`, `IPlugin`
- [ViewModels und UI](inventory/viewmodels.md) — `ProjectDetailViewModel`, `RepositoryAssignViewModel`, `ProjectDetailView`
- [Tests](inventory/tests.md) — `ProjektServiceTests`, `RepositoryAssignViewModelTests`, Testhelfer

## Kritische Erkenntnisse

1. **Plugin-Sortierung ist bereits vorhanden**: `RepositoryAssignViewModel` sortiert bereits `OrderByDescending(r => r.UpdatedAt).ThenBy(r => r.Name)`. Dies ist genau die gewünschte Sortierlogik und kann direkt wiederverwendet werden.

2. **`GetAvailableRepositoriesAsync()` ist standardisiert**: Alle Git-Plugins implementieren diese Methode und geben `AvailableRepository`-Objekte zurück. Die Integration wird dadurch vereinfacht.

3. **Fehlerbehandlung muss robust sein**: Wenn ein Plugin fehlt oder Fehler wirft, müssen andere Plugins weiter funktionieren. `RepositoryAssignViewModelTests` zeigt, dass Fehler bereits korrekt abgefangen werden.

4. **Filtering nach "unzugeordnet" ist zentral**: Es muss sichergestellt werden, dass bereits zugeordnete Repositories (via `Projekt.Repositories` und `GitRepository.RepositoryUrl`) korrekt ausgeschlossen werden. Der Vergleich muss auf Basis von `RepositoryUrl` erfolgen, um Duplikate zu vermeiden.

5. **ViewModel-Pattern ist etabliert**: Das bestehende `ProjectDetailViewModel` folgt einem klaren Pattern (LadenAsync, Property-Changes, Commands), das für die neue Feature erweitert werden kann.

## Implementierungsempfehlungen (kein Plan, nur Erkenntnisse)

- Nutze `IPluginManager.GetSourceCodeManagementPlugins()` zur Iteration über alle Plugins
- Rufe `GetAvailableRepositoriesAsync()` für jedes Plugin auf (mit Try-Catch für Fehler)
- Filtere Repositories nach `RepositoryUrl` gegen bestehende `GitRepository`-Einträge
- Nutze die bereits getestete Sortierlogik: `OrderByDescending(r => r.UpdatedAt).ThenBy(r => r.Name, ...)`
- Implementiere ähnliche Fehlerbehandlung wie in `RepositoryAssignViewModel`
