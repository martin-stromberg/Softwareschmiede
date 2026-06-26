# Anforderungsübersetzung: Repository-Suggestion-Panel

## Fachliche Zusammenfassung

Auf der Projektdetailseite soll ein neues Panel hinzugefügt werden, das unzugeordnete Repositories aus allen verfügbaren Git-Plugins (SCM-Plugins) in einer sortierten Liste anzeigt. Das Panel soll die Repositories nach dem Datum der letzten Änderung (`UpdatedAt`) in absteigender Reihenfolge (neuste zuerst) darstellen. Dies ermöglicht Benutzern, schnell zu identifizieren, welche Repositories noch nicht einem Projekt zugewiesen sind, und welche davon kürzlich aktiv waren.

## Betroffene Klassen und Komponenten

### Datenmodellklassen
- **`Projekt`** (Entities) — bereits vorhanden; keine neuen Eigenschaften erforderlich
- **`GitRepository`** (Entities) — bereits vorhanden; verwaltet zugeordnete Repositories pro Projekt
- **`AvailableRepository`** (ValueObjects) — bereits vorhanden; repräsentiert ein verfügbares Repository mit `UpdatedAt`-Eigenschaft für Sortierung

### Logikklassen / Services
- **`ProjektService`** — ggf. neue Methode `GetUnassignedRepositoriesAsync()` zum Laden aller unzugeordneten Repositories aus allen Git-Plugins
- **`IPluginManager`** — bereits vorhanden; verwaltet alle verfügbaren Git-Plugins und deren Repositories

### UI-Komponenten / ViewModels
- **`ProjectDetailViewModel`** — ViewModel der Projektdetailseite; neue Property für die Liste unzugeordneter Repositories
- **Neue oder erweiterte View-Komponente** — Panel zur Anzeige der Repository-Liste auf der Projektdetailseite

### Tests
- Unit-Tests für die neue Service-Methode `GetUnassignedRepositoriesAsync()`
- Unit-Tests für ViewModel-Properties und Sortierlogik
- UI-Tests zur Überprüfung der korrekt sortierten Anzeige im Panel

## Implementierungsansatz

1. **Repository-Aggregation**
   - Erweitere `ProjektService` um eine neue Methode `GetUnassignedRepositoriesAsync(Guid projektId)` oder `GetUnassignedRepositoriesAsync()` (ohne Projekt-Filter), die:
     - Über `IPluginManager` alle verfügbaren Git-Plugins (`IGitPlugin`) iteriert
     - Für jedes Plugin die verfügbaren Repositories über eine Plugin-Methode (z.B. `GetAvailableRepositoriesAsync()`) lädt
     - Die gesammelten `AvailableRepository`-Objekte als flache Liste zusammenführt
     - Repositories, die bereits in der Datenbank unter irgendeinem `Projekt.Repositories` vorhanden sind, filtert (also `GitRepository.RepositoryUrl` ausschließt)

2. **Sortierung**
   - Sortiere die resultierende Liste nach der Eigenschaft `AvailableRepository.UpdatedAt` in **absteigend** (neueste zuerst)
   - Fallback für fehlende/null `UpdatedAt`-Werte: Repository ans Ende verschieben oder Fehlerbehandlung implementieren

3. **ViewModel-Integration**
   - Füge dem `ProjectDetailViewModel` eine neue Property `UnassignedRepositories` vom Typ `ObservableCollection<AvailableRepository>` hinzu
   - Diese Property wird asynchron geladen, wenn die Projektdetailseite angezeigt wird (ggf. im `LadenAsync()` der VM)
   - Implementiere ein `Command` (optional) zum Aktualisieren/Neuladen der Liste

4. **UI-Integration**
   - Erweitere die XAML-View (`ProjectDetailView` oder `ProjectDetailPage`) um ein neues Panel/Abschnitt
   - Das Panel zeigt die `UnassignedRepositories` in einer `ListBox` oder `DataGrid` mit Spalten:
     - Repository-Name (`AvailableRepository.Name` oder `AvailableRepository.NameWithOwner`)
     - Letzter Änderungszeitpunkt (`AvailableRepository.UpdatedAt`, formatiert)
     - Optional: Plugin-Typ (falls aggregiert aus verschiedenen Plugins)
   - Sortierung ist bereits über Datenbindung gewährleistet (die Collection ist vorher sortiert)

5. **Fehlerbehandlung**
   - Falls ein Plugin die Repository-Liste nicht laden kann (Exception): Plugin überspringen, Fehler loggen, mit anderen Plugins fortfahren
   - Falls kein unzugeordnetes Repository existiert: Panel zeigt leere Liste oder "Keine unzugeordneten Repositories"

## Konfiguration

Das Feature benötigt derzeit keine zusätzliche Konfigurationsebene:
- Plugin-Auswahl ist über den `IPluginManager` zentralisiert und bereits konfigurierbar
- Sortierrichtung (absteigend nach `UpdatedAt`) ist fest vorgegeben und wird nicht konfiguriert
- Falls später gewünscht, könnte eine Benutzerpräferenz für Sortierrichtung oder Sortierfeld in den Anwendungseinstellungen hinterlegt werden

## Offene Fragen

1. **Plugin-Methode für Repository-Abfrage:**
   - Bereits `IGitPlugin` oder `IGitPlugin` erweiterbar mit einer Methode `GetAvailableRepositoriesAsync()`, die `AvailableRepository`-Objekte zurückgibt?
   - Wenn nicht, muss eine solche Methode hinzugefügt oder eine bestehende verändert werden

2. **Datumsformat für UpdatedAt:**
   - Welches Format soll für die Anzeige von `UpdatedAt` verwendet werden (z.B. "vor 2 Stunden", "2025-06-26 14:30", "26.06.2025")?

3. **Panel-Platzierung und Größe:**
   - Soll das Panel oberhalb oder unterhalb der Aufgaben-Kachel auf der Projektdetailseite angezeigt werden?
   - Soll die Panel-Höhe fest oder flexibel sein?

4. **Interaktion mit dem Panel:**
   - Sollen Benutzer ein Repository aus der Liste auswählen und direkt zuweisen können?
   - Oder ist die Liste nur informativ und der Benutzer weist über den bestehenden "Zuweisen"-Button zu?

5. **Filter und Suche:**
   - Soll die Repository-Liste durchsuchbar sein oder zusätzliche Filter (z.B. nach Plugin-Typ) bieten?

6. **Performance:**
   - Falls sehr viele Repositories verfügbar sind: Gibt es eine Paginierung oder ein Limit für die angezeigte Liste?

7. **Echtzeit-Updates:**
   - Sollen neu verfügbare Repositories automatisch erkannt und angezeigt werden, oder wird die Liste nur beim Laden der Seite aktualisiert?
