# Umsetzungsplan: Repository-Suggestion-Panel auf der Projektübersichtsseite

## Übersicht

Das Feature erweitert die Projektübersichtsseite (`ProjectListView`) um ein neues Panel unterhalb der Projektkacheln, das alle unzugeordneten Repositories aus allen verfügbaren Git-Plugins in einer nach letzter Änderung sortierten Liste anzeigt. Der Benutzer kann per Doppelklick auf einen Listeneintrag ein neues Projekt erstellen und das Repository automatisch diesem Projekt zuweisen. Die Liste wird sowohl beim initialen Laden der Seite als auch beim Zurücknavigieren von einer Projektdetailansicht aktualisiert.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|---|---|---|
| **Repository-Aggregation** | Neue Service-Methode `GetUnassignedRepositoriesAsync()` in `ProjektService` | Zentralisiert die Aggregation und Filterung aller unzugeordneten Repositories über alle Plugins hinweg; folgt dem bestehenden Service-Layer-Pattern |
| **Fehlerbehandlung bei Plugin-Ausfällen** | Try-Catch pro Plugin mit Logging; andere Plugins werden fortgesetzt | Robustheit: Ein fehlerhaftes Plugin blockiert nicht die gesamte Liste; vgl. bestehendes Muster in `RepositoryAssignViewModel` |
| **Sortierung** | Primär nach `UpdatedAt` absteigend, sekundär nach `Name` aufsteigend (Case-insensitive) | Wie in `RepositoryAssignViewModel` bereits implementiert; Neuste zuerst ist intuitiv und zeigt aktive Repositories prominent |
| **Datumsformatierung für UpdatedAt** | Relative Zeitangaben ("vor 2 Stunden", "vor 1 Tag", etc.) | Benutzerfreundlicher und aussagekräftiger als absolute Daten; empfohlenes UI-Pattern für aktuelle Aktivitäten |
| **Projekt-Erstellung aus Panel** | Doppelklick auf Listeneintrag triggert synchrone Projekt-Erstellung + asynchrone Repository-Zuordnung | Schnell und intuitiv; Repository-Zuordnung erfolgt direkt nach Projekt-Erstellung ohne weiteren Dialog |
| **Panel-Platzierung in XAML** | Neuer `<StackPanel>` oder `<Grid>` mit `RowDefinition` in `ProjectListView` als separate Sektion unterhalb des `ItemsControl` (Projektkacheln) | Modular, scrollbar mit der gesamten Seite, volle Breite |
| **Panel-Größenanpassung** | Dynamische Höhe via `ItemsControl` mit unbegrenztem `ItemsPanel` (StackPanel vertikal); Container scrollbar | Flexibel für beliebige Repository-Mengen, keine Pagination im MVP |
| **Echtzeit-Update auf Rückkehr** | Navigation-Callback in `ProjectDetailViewModel.NavigateBackToProjectCallback` triggert Neuladen der Repositories | Ähnlich wie `NeuesProjektHinzufuegen()`-Callback; sichert Konsistenz nach Projekt-Operationen |
| **Speicherung von unzugeordneten Repositories** | Property `UnassignedRepositories` in `ProjectListViewModel` (nicht in `ProjectDetailViewModel`) | Thematisch gehört die Verwaltung zur Projektliste, nicht zum Detail; `ProjectDetailViewModel` bleibt fokussiert |

## Programmabläufe

### Laden der Projektübersichtsseite

1. `ProjectListView` wird angezeigt
2. `ProjectListViewModel.LadenCommand` wird ausgelöst (via `Loaded`-Event oder `ICommand`)
3. `LadenAsync()` lädt Projekte via `ProjektService.GetAllAsync()`
4. Parallel: `LadenAsync()` lädt unzugeordnete Repositories via `ProjektService.GetUnassignedRepositoriesAsync()`
5. `UnassignedRepositories`-Property wird mit sortierten Repositories aktualisiert
6. XAML-Binding zeigt Repositories in einem neuen Panel unterhalb der Projektkacheln an
7. Datumsformatierung für `UpdatedAt` erfolgt via Value Converter

Beteiligte Klassen/Komponenten: `ProjectListViewModel`, `ProjectListView`, `ProjektService`, `IPluginManager`, `IGitPlugin`, `AvailableRepository`, `UnassignedRepositoriesConverter`

### Zurücknavigieren von Projektdetailansicht zur Projektübersichtsseite

1. Benutzer drückt Zurück-Button in `ProjectDetailViewModel` oder wählt anderes Projekt
2. `ProjectDetailViewModel.NavigateBackToProjectCallback()` wird aufgerufen (bestehender Callback in `ProjectListViewModel`)
3. `ProjectListViewModel.KehreZuProjectZurueck()` wird ausgelöst
4. Zusätzlich: Aufruf von `ProjektService.GetUnassignedRepositoriesAsync()` zum Aktualisieren der Repositories
5. `UnassignedRepositories`-Property wird aktualisiert
6. UI reflektiert Änderungen (neue Repositories, Entfernung nun zugeordneter Repositories)

Beteiligte Klassen/Komponenten: `ProjectDetailViewModel`, `ProjectListViewModel`, `ProjektService`

### Doppelklick auf Listeneintrag (Projekt erstellen + Repository zuordnen)

1. Benutzer doppelklickt auf Repository-Listeneintrag in Panel
2. `RepositoryDoubleclickCommand` wird ausgelöst (neuer Command in `ProjectListViewModel`)
3. Synchron: Neues Projekt wird erstellt via `ProjektService.CreateAsync()` mit Standard-Namen (z.B. Repository-Name)
4. Asynchron: Repository wird dem neuen Projekt zugeordnet via `ProjektService.AddRepositoryAsync()`, Parameter: `PluginTyp`, `RepositoryUrl`, `RepositoryName` (aus `AvailableRepository`)
5. `NeuesProjektHinzufuegen()` wird aufgerufen → `LadenAsync()` wird erneut ausgeführt
6. Projektliste und Repository-Liste werden neu geladen
7. Das neue Projekt wird in den Projektkacheln sichtbar
8. Das zugeordnete Repository verschwindet aus dem Suggestions-Panel

Beteiligte Klassen/Komponenten: `ProjectListViewModel`, `ProjektService`, `AvailableRepository`

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `UnassignedRepositoriesConverter` | Value Converter (IValueConverter) | Formatiert `AvailableRepository.UpdatedAt` in relative Zeitangaben ("vor X Stunden", "vor X Tagen", etc.) |

## Änderungen an bestehenden Klassen

### `ProjektService` (Service)

- **Neue Methode:** `GetUnassignedRepositoriesAsync(CancellationToken ct)` — gibt sortierte Liste aller unzugeordneten Repositories aus allen SCM-Plugins zurück
  - Parameter: `CancellationToken ct`
  - Rückgabewert: `Task<IEnumerable<AvailableRepository>>`
  - Logik:
    1. Alle verfügbaren Repositories aus allen SCM-Plugins via `IPluginManager.GetSourceCodeManagementPlugins()` aggregieren
    2. Für jedes Plugin `GetAvailableRepositoriesAsync()` aufrufen (mit Try-Catch für robuste Fehlerbehandlung)
    3. Alle `AvailableRepository`-Objekte in flacher Liste sammeln
    4. Gegen bestehende `GitRepository.RepositoryUrl` in der DB filtern (Ausschluss zugeordneter Repositories)
    5. Sortieren: Primär nach `UpdatedAt` absteigend, sekundär nach `Name` aufsteigend (Case-insensitive)
    6. Zurückgeben als `IEnumerable<AvailableRepository>`

### `ProjectListViewModel` (ViewModel)

- **Neue Property:** `UnassignedRepositories` (`ObservableCollection<AvailableRepository>`) — hält Liste der unzugeordneten Repositories für UI-Binding
- **Neue Methode:** `LadenRepositorienSuggestionsAsync(CancellationToken ct)` (private) — lädt unzugeordnete Repositories via `ProjektService.GetUnassignedRepositoriesAsync()` und aktualisiert `UnassignedRepositories`
- **Anpassung der Methode:** `LadenAsync(CancellationToken ct)` — zusätzlicher Aufruf von `LadenRepositorienSuggestionsAsync()` parallel zu `GetAllAsync()`
- **Neue Methode:** `ProjektAusRepositoryErstellen(AvailableRepository repo)` (private, async) — erstellt neues Projekt mit Repository-Zuordnung
  - Ruft `ProjektService.CreateAsync()` mit Repository-Namen auf
  - Ruft `ProjektService.AddRepositoryAsync()` mit Plugin-Daten auf
  - Ruft `NeuesProjektHinzufuegen()` auf
- **Neuer Command:** `RepositoryDoubleclickCommand` (IAsyncRelayCommand<AvailableRepository>) — triggert `ProjektAusRepositoryErstellen()`
- **Anpassung der Methode:** `KehreZuProjectZurueck()` — zusätzlicher Aufruf von `LadenRepositorienSuggestionsAsync()` zum Aktualisieren der Suggestions nach Rückkehr
- **Neue Property:** `IsLoadingRepositories` (`bool`) — Loading-Flag für Suggestions-Panel (zur Anzeige von Laden-Indikator)

### `ProjectDetailViewModel` (ViewModel)

- **Anpassung des Callbacks:** `NavigateBackToProjectCallback` — zusätzlicher optionaler Callback zum Triggern von Suggestions-Neuladen (wird von `ProjectListViewModel` gesetzt)
  - Kann als Action definiert werden, die vom Parent aufgerufen wird, um Refresh zu signalisieren

### `ProjectListView` (XAML-View)

- **Neue Sektion:** Neues `<Grid.RowDefinition>` und entsprechender `<Grid.Row>` für Suggestions-Panel oder Alternative über `<StackPanel>` mit mehreren Sektion
- **Suggestions-Panel-Struktur:** Border mit ItemsControl für Repositories
- **Neue Controls/Bindings:**
  - ItemsControl für Repositories
  - DataTemplate für jeden Repository-Eintrag (Name, UpdatedAt mit Converter)
  - MouseDoubleClick-Binding zum Command
  - IsLoading-Indicator (optional, via `IsLoadingRepositories`-Flag)
  - Container-Styling (Breite, Höhe flexibel, Padding, Border wie Projektkacheln)

### `IGitPlugin` (Interface)

- **Keine Änderung erforderlich** — `GetAvailableRepositoriesAsync(CancellationToken)` existiert bereits und wird verwendet

### `IPluginManager` (Interface)

- **Keine Änderung erforderlich** — `GetSourceCodeManagementPlugins()` existiert bereits und wird verwendet

## Datenbankmigrationen

Keine. — Die existierenden Tabellen (`Projekt`, `GitRepository`) sind ausreichend. Es werden keine neuen Spalten oder Tabellen benötigt.

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| `AvailableRepository.UpdatedAt` | Darf nicht null sein für Sortierung | Repositories ohne `UpdatedAt` werden ans Ende sortiert (Fallback: `DateTime.MinValue`) |
| `AvailableRepository.Url` (als `RepositoryUrl`) | Darf nicht null oder leer sein | Wird nicht in die Suggestions-Liste aufgenommen (Plugin-spezifische Validierung) |
| Projekt-Name bei Erstellung aus Repository | Automatisch gesetzt auf `AvailableRepository.Name` oder `NameWithOwner` | Kein Fallback nötig, da Plugin-Namen immer vorhanden |

## Konfigurationsänderungen

Keine.

## Seiteneffekte und Risiken

- **Navigation zurück von Projektdetail:** Zusätzlicher Datenbankzugriff und API-Calls an alle Plugins beim Zurücknavigieren könnten die Benutzererfahrung verlangsamen, wenn viele Plugins aktiv sind. Mitigation: Asynchrones Laden mit `IsLoadingRepositories`-Flag und optional ein Debounce (falls multiple Navigationen schnell hintereinander erfolgen).

- **Plugin-Fehler:** Wenn ein Plugin bei `GetAvailableRepositoriesAsync()` fehlschlägt, wird es ignoriert, aber andere Plugins funktionieren weiterhin. Dies ist gewünscht, könnte aber bedeuten, dass Repositories eines fehlerhaften Plugins in den Suggestions fehlen. Logging ist erforderlich.

- **Performance bei großer Anzahl Repositories:** Bei sehr großer Anzahl unzugeordneter Repositories (hunderte oder mehr) könnte das Rendering der Liste verlangsamt werden. Mitigation im MVP: Kein Limit, aber Beobachtung für zukünftige Pagination erforderlich.

- **Zuordnungs-Inkonsistenzen:** Zwischen Laden der Suggestions und Erstellung eines Projekts könnte das Repository bereits von einem anderen Benutzer zugeordnet werden (Multi-User-Szenario). Mitigation: `AddRepositoryAsync()` validiert bereits Duplikate oder schlägt fehl mit aussagekräftiger Fehlermeldung.

- **Bestehende ProjectListViewModel-Tests:** `ProjectListViewModelTests` müssen möglicherweise um neue Testfälle für `UnassignedRepositories` erweitert werden. Bestehende Tests sollten nicht brechen, aber müssen reviewed werden.

## Umsetzungsreihenfolge

1. **Service-Methode implementieren**
   - Voraussetzungen: `ProjektService` existiert, `IPluginManager` existiert, `IGitPlugin.GetAvailableRepositoriesAsync()` existiert, Datenbankzugriff via `SoftwareschmiededDbContext` funktioniert
   - Beschreibung: Implementiere `ProjektService.GetUnassignedRepositoriesAsync(CancellationToken ct)` mit vollständiger Aggregation, Filterung, Sortierung und Fehlerbehandlung

2. **Value Converter für Datumsformatierung erstellen**
   - Voraussetzungen: Neue Datei kann erstellt werden, `IValueConverter`-Interface ist verfügbar
   - Beschreibung: Erstelle `UnassignedRepositoriesConverter` (neuer Value Converter) zur Umwandlung von `DateTime` in relative Zeitangaben (z.B. "vor 2 Stunden")

3. **ProjectListViewModel erweitern**
   - Voraussetzungen: `ProjektService.GetUnassignedRepositoriesAsync()` existiert, `ProjectListViewModel` existiert, `IAsyncRelayCommand` ist verfügbar
   - Beschreibung: 
     - Füge `UnassignedRepositories`-Property hinzu
     - Füge `IsLoadingRepositories`-Property hinzu
     - Implementiere `LadenRepositorienSuggestionsAsync()`
     - Implementiere `ProjektAusRepositoryErstellen()`
     - Füge `RepositoryDoubleclickCommand` hinzu
     - Passe `LadenAsync()` an, um Suggestions parallel zu laden
     - Passe `KehreZuProjectZurueck()` an, um Suggestions zu aktualisieren

4. **ProjectListView (XAML) erweitern**
   - Voraussetzungen: `ProjectListView.xaml` existiert, `UnassignedRepositoriesConverter` existiert, `ProjectListViewModel` wurde in Schritt 3 erweitert
   - Beschreibung:
     - Füge Converter als Ressource hinzu
     - Ergänze Grid.RowDefinition für neues Suggestions-Panel
     - Implementiere ItemsControl für Repositories mit DataTemplate
     - Binde MouseDoubleClick zum Command
     - Formatiere Datumsspalte mit Converter

5. **Unit-Tests für `ProjektService.GetUnassignedRepositoriesAsync()` schreiben**
   - Voraussetzungen: `ProjektServiceTests` existiert, Test-Infrastruktur (DB Context, Mocks) ist vorhanden, Schritt 1 ist abgeschlossen
   - Beschreibung:
     - Test: Rückgabe aller Repositories, wenn alle unzugeordnet
     - Test: Filterung zugeordneter Repositories
     - Test: Sortierung nach UpdatedAt absteigend, dann Name aufsteigend
     - Test: Fehlerbehandlung bei fehlerhaften Plugins
     - Test: Rückgabe leerer Liste, wenn keine Repositories vorhanden

6. **Unit-Tests für ProjectListViewModel erweitern**
   - Voraussetzungen: `ProjectListViewModelTests` existiert, Schritt 3 ist abgeschlossen, Mocks für `ProjektService` sind vorhanden
   - Beschreibung:
     - Test: `UnassignedRepositories` wird korrekt geladen
     - Test: `RepositoryDoubleclickCommand` erstellt Projekt und ordnet Repository zu
     - Test: `IsLoadingRepositories`-Flag wird korrekt gesetzt
     - Test: `KehreZuProjectZurueck()` lädt Suggestions neu

7. **E2E-Test: Repositories laden auf Projektübersicht**
   - Voraussetzungen: E2E-Test-Infrastruktur, UI-Test-Framework, App läuft, Schritt 1-4 sind abgeschlossen
   - Beschreibung:
     - Teste: Projektübersichtsseite wird geladen
     - Verifiziere: Suggestions-Panel wird angezeigt
     - Verifiziere: Repositories sind sortiert nach UpdatedAt
     - Verifiziere: Datumsformatierung ist relativ (z.B. "vor 2 Stunden")

8. **E2E-Test: Doppelklick auf Repository erstellt Projekt**
   - Voraussetzungen: E2E-Test-Infrastruktur, Schritt 1-7 abgeschlossen
   - Beschreibung:
     - Teste: Doppelklick auf Repository in Suggestions-Panel
     - Verifiziere: Neues Projekt wird erstellt mit Repository-Namen
     - Verifiziere: Repository wird dem Projekt zugeordnet
     - Verifiziere: Repository verschwindet aus Suggestions-Panel
     - Verifiziere: Projekt erscheint in Projektkacheln

9. **E2E-Test: Suggestions werden nach Rückkehr von Projektdetail aktualisiert**
   - Voraussetzungen: E2E-Test-Infrastruktur, Schritt 1-8 abgeschlossen, `ProjectDetailViewModel` kann navigiert werden
   - Beschreibung:
     - Teste: Von Projektliste zu Projektdetail navigieren
     - Teste: Ein Repository manuell zuordnen (via bestehender Dialog)
     - Teste: Zurück zur Projektliste navigieren
     - Verifiziere: Zugeordnetes Repository ist nicht mehr in Suggestions sichtbar

10. **Bestehende E2E-Tests für ProjectListView überprüfen und ggf. anpassen**
    - Voraussetzungen: Schritt 1-9 abgeschlossen, bestehende E2E-Tests sind identifiziert
    - Beschreibung:
      - Review: `ProjectListE2ETests` (oder ähnlich) auf Breaking Changes prüfen
      - Anpassung: Falls Tests auf spezifische XAML-Struktur prüfen, müssen diese aktualisiert werden
      - Verifizierung: Alle bestehenden Tests bestehen noch

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `GetUnassignedRepositoriesAsync_ShouldReturnAllRepositories_WhenAllUnassigned()` | `ProjektServiceTests` | Rückgabe aller verfügbaren Repositories, wenn keine zugeordnet |
| `GetUnassignedRepositoriesAsync_ShouldExcludeAssignedRepositories()` | `ProjektServiceTests` | Filterung zugeordneter Repositories (Vergleich via `RepositoryUrl`) |
| `GetUnassignedRepositoriesAsync_ShouldSortByUpdatedAtDescendingThenByNameAscending()` | `ProjektServiceTests` | Korrekte Sortierung (primär UpdatedAt absteigend, sekundär Name aufsteigend) |
| `GetUnassignedRepositoriesAsync_ShouldHandlePluginError_AndContinueWithOtherPlugins()` | `ProjektServiceTests` | Fehlerbehandlung: fehlerhaftes Plugin wird ignoriert, andere fortgesetzt |
| `GetUnassignedRepositoriesAsync_ShouldReturnEmptyList_WhenAllRepositoriesAssigned()` | `ProjektServiceTests` | Rückgabe leerer Liste, wenn keine unzugeordneten Repositories |
| `LadenRepositorienSuggestionsAsync_ShouldLoadAndPopulateUnassignedRepositories()` | `ProjectListViewModelTests` | Property wird korrekt gefüllt, IsLoadingRepositories wird gesetzt |
| `RepositoryDoubleclickCommand_ShouldCreateProjectAndAssignRepository()` | `ProjectListViewModelTests` | Command erstellt Projekt mit Repository-Zuordnung |
| `RepositoryDoubleclickCommand_ShouldReloadProjectsAndRepositories_AfterCreation()` | `ProjectListViewModelTests` | Nach Projekt-Erstellung werden Projekte und Suggestions neu geladen |
| `KehreZuProjectZurueck_ShouldReloadUnassignedRepositories()` | `ProjectListViewModelTests` | Rückkehr triggert Neuladen der Suggestions |
| `CreatePluginMockWithRepositories()` | `ProjectListViewModelTests` (Hilfsmethode) | Erstellt Mock-Plugin mit vordefiniertem Set von verfügbaren Repositories |
| `UnassignedRepositoriesConverter_ShouldFormatRelativeTime()` | `UnassignedRepositoriesConverterTests` (neue Testklasse) | Umwandlung von `DateTime` in relative Zeitangaben ("vor X Stunden", etc.) |
| `UnassignedRepositoriesConverter_ShouldHandleNullAndMinValue()` | `UnassignedRepositoriesConverterTests` | Fallback für fehlende oder sehr alte Datumsangaben |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `ProjectListViewModelTests` | `LadenAsync()` wurde erweitert um `LadenRepositorienSuggestionsAsync()` Aufruf; bestehende Tests prüfen möglicherweise nur Projekte, müssen aber auch Suggestions-Ladelogik berücksichtigen oder mocken |
| `ProjectListE2ETests` (falls vorhanden) | XAML-Struktur von `ProjectListView` hat neue Zeilen/Elemente; Navigation und Layout-Tests könnten betroffen sein |

### E2E-Tests (Pflicht)

| Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium |
|----------|------------------------|-------------------------------|
| Repositories werden beim Laden der Projektliste angezeigt | `ProjectListE2ETests` oder `RepositorySuggestionE2ETests` | Panel wird unterhalb der Projektkacheln in voller Breite angezeigt |
| Repositories werden nach UpdatedAt sortiert angezeigt | `ProjectListE2ETests` oder `RepositorySuggestionE2ETests` | Sortierung nach UpdatedAt absteigend (neuste zuerst) |
| Datumsformatierung ist relativ | `ProjectListE2ETests` oder `RepositorySuggestionE2ETests` | Relative Zeitdarstellung (z.B. "vor 2 Stunden") |
| Doppelklick erstellt Projekt und ordnet Repository zu | `ProjectListE2ETests` oder `RepositorySuggestionE2ETests` | Doppelklick erstellt neues Projekt und ordnet Repository zu |
| Repository verschwindet nach Zuordnung | `ProjectListE2ETests` oder `RepositorySuggestionE2ETests` | Zugeordnetes Repository ist nicht mehr in der Suggestions-Liste |
| Suggestions werden beim Zurücknavigieren aktualisiert | `ProjectListE2ETests` oder `RepositorySuggestionE2ETests` | Echtzeit-Updates beim Zurücknavigieren von Projektdetailansicht |
| Panel passt sich an Listenlänge an (scrollbar) | `ProjectListE2ETests` oder `RepositorySuggestionE2ETests` | Panel-Größe passt sich an, Seite ist scrollbar bei langer Liste |

Welche bestehenden E2E-Tests müssen angepasst werden?

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `ProjectListE2ETests` (falls vorhanden) | Layout-Tests prüfen möglicherweise auf Positionen von Elementen; neue Sektion könnte bestehende Positionen ändern. XAML-Selektoren könnten angepasst werden. |

## Offene Punkte

Alle offenen Punkte aus der Anforderung wurden geklärt und sind bereits im Plan eingearbeitet. Es gibt keine verbleibenden offenen Punkte.

Geklärte Punkte:
1. **Datumsformat:** Relative Zeitdarstellung (z.B. "vor 2 Stunden") — implementiert via Value Converter
2. **Panel-Platzierung:** Unterhalb der Projektkacheln auf der Projektübersichtsseite in voller Breite — Strukturierung in `ProjectListView` via neues Grid-Row oder StackPanel
3. **Panel-Größe:** Flexibel, Höhe passt sich an Listenlänge an — ItemsControl mit unbegrenztem StackPanel, Seite scrollbar bei Bedarf
4. **Interaktion:** Doppelklick erstellt neues Projekt und ordnet Repository zu — implementiert via `RepositoryDoubleclickCommand` mit `CreateAsync()` + `AddRepositoryAsync()`
5. **Filter/Suche:** Nicht im MVP — wird nicht implementiert
6. **Performance/Pagination:** Kein Limit — alle unzugeordneten Repositories werden geladen
7. **Echtzeit-Updates:** Beim Laden der Seite UND beim Zurücknavigieren — `LadenAsync()` + `KehreZuProjectZurueck()`
