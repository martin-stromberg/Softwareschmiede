# Umsetzungsplan: To-Do-Liste zur Unterstützung der Aufgabengliederung und des Fortschritts

## Übersicht

Der Plan implementiert ein To-Do-Verwaltungssystem für Aufgaben. Benutzer können in der Aufgabendetailansicht To-Do-Elemente erstellen, abhaken und löschen. Das System blockiert den Abschluss einer Aufgabe, solange noch offene To-Dos vorhanden sind, und zeigt die Anzahl offener To-Dos im Ribbon-Badge an. Die Implementierung folgt den bestehenden Patterns für 1:n-Beziehungen (PullRequests, Protokolleinträge).

## Designentscheidungen

Keine — folgt bestehenden Mustern.

Die Struktur orientiert sich an den etablierten Patterns für PullRequests, Protokolleinträge und DiffResults. Die To-Do-Entity ist einfach strukturiert (nur Beschreibung, kein Prioritäten/Tags), basierend auf der Anforderung; weitere Optionen für To-Do-Kategorien, Prioritäten oder Anhänge sind in den offenen Fragen dokumentiert und können in zukünftigen Features erweitert werden.

## Programmabläufe

### Aufgabe öffnen (LadenAsync)

1. `TaskDetailViewModel.LadenAsync()` wird aufgerufen
2. Aufgabe wird via `AufgabeService.GetDetailAsync()` geladen (mit `.Include(a => a.Todos)`)
3. `Todos`-Collection wird in `TaskDetailViewModel` mit geladenen `TodoViewModel`-Instanzen befüllt
4. `OffeneTodoCount` wird basierend auf der Anzahl offener (ErledaltAm == null) Todos berechnet
5. Todo-Ansicht wird verfügbar, aber nicht automatisch angezeigt (Benutzer navigiert über Tab)

Beteiligte Klassen/Komponenten: `TaskDetailViewModel`, `AufgabeService`, `TodoService`, `Aufgabe`, `Todo`

### Neues To-Do erstellen

1. Benutzer gibt Text in das Eingabefeld der Todo-Ansicht ein
2. Benutzer klickt "Hinzufügen" oder drückt Enter
3. `TodoHinzufuegenCommand` wird ausgeführt
4. `TodoService.CreateTodoAsync(aufgabeId, beschreibung, ct)` wird aufgerufen
5. Neue `Todo` wird in Datenbank gespeichert
6. `TaskDetailViewModel` fügt neues `TodoViewModel` zur `Todos`-Collection hinzu
7. `OffeneTodoCount` wird inkrementiert und Badge aktualisiert sich

Beteiligte Klassen/Komponenten: `TaskDetailView`, `TaskDetailViewModel`, `TodoService`, `Todo`, `TodoViewModel`

### To-Do abhaken (als erledigt markieren)

1. Benutzer klickt Checkbox neben einem To-Do-Eintrag
2. `TodoAlsErledeltMarkierenCommand` wird mit `TodoViewModel`-ID ausgeführt
3. `TodoService.MarkTodoAsCompletedAsync(todoId, ct)` wird aufgerufen
4. `Todo.ErledaltAm` wird auf `DateTime.UtcNow` gesetzt und in Datenbank gespeichert
5. `TodoViewModel.IstErledigt` wird aktualisiert (triggert `PropertyChanged`)
6. UI zeigt visuelle Unterscheidung (durchgestrichener Text o. Ä.)
7. `OffeneTodoCount` wird dekrementiert und Badge aktualisiert sich

Beteiligte Klassen/Komponenten: `TodoListView`, `TodoViewModel`, `TaskDetailViewModel`, `TodoService`

### To-Do löschen

1. Benutzer klickt Delete-Button neben einem To-Do-Eintrag
2. `TodoLoeschenCommand` wird mit `TodoViewModel`-ID ausgeführt
3. `TodoService.DeleteTodoAsync(todoId, ct)` wird aufgerufen
4. `Todo` wird aus Datenbank gelöscht
5. `TaskDetailViewModel` entfernt `TodoViewModel` aus `Todos`-Collection
6. Wenn gelöschtes To-Do offen war: `OffeneTodoCount` wird dekrementiert und Badge aktualisiert sich

Beteiligte Klassen/Komponenten: `TodoListView`, `TodoViewModel`, `TaskDetailViewModel`, `TodoService`

### Aufgabe beenden (mit To-Do-Validierung)

1. Benutzer klickt "Beenden"-Button im Ribbon
2. `AufgabeAbschliessenCommand` wird ausgeführt
3. `AufgabeService.CanCompleteTaskAsync(aufgabeId, ct)` wird aufgerufen (neue Methode)
4. Falls offene To-Dos existieren (Rückgabewert false):
   - Fehlermeldung wird angezeigt: "Diese Aufgabe kann nicht beendet werden, solange noch {count} offene To-Do(s) vorhanden sind."
   - Statusübergang wird abgebrochen
5. Falls keine offenen To-Dos (Rückgabewert true):
   - `EntwicklungsprozessService.AbschliessenAsync()` wird aufgerufen
   - Aufgabe wird beendet
   - `TaskDetailViewModel.LadenAsync()` wird aufgerufen, um UI zu aktualisieren

Beteiligte Klassen/Komponenten: `TaskDetailView`, `TaskDetailViewModel`, `EntwicklungsprozessService`, `AufgabeService`, `TodoService`

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `Todo` | Datenmodellklasse | Entity für ein einzelnes To-Do-Element mit Beschreibung und Erledigungsstatus |
| `TodoService` | Service-Klasse | Business-Logik für To-Do-CRUD-Operationen und Abfragen |
| `TodoViewModel` | ViewModel-Klasse | WPF ViewModel für ein To-Do-Element in der Bindung; implementiert `INotifyPropertyChanged` |
| `TodoListView` | UserControl | WPF-Komponente zur Anzeige und Verwaltung der To-Do-Liste in der Aufgabendetailansicht |

## Änderungen an bestehenden Klassen

### `Aufgabe` (Entity)

- **Neue Navigationseigenschaft:** `List<Todo> Todos` — Sammlung aller To-Dos dieser Aufgabe (analog zu `PullRequests`, `Protokolleintraege`, `DiffResults`)

### `SoftwareschmiededDbContext` (DbContext)

- **Neues DbSet:** `DbSet<Todo> Todos` — Ermöglicht Datenbankzugriff auf To-Do-Entitäten
- **Neue Konfiguration (OnModelCreating):** Beziehung zwischen `Aufgabe` und `Todo`:
  - `HasMany(a => a.Todos).WithOne(t => t.Aufgabe).HasForeignKey(t => t.AufgabeId).OnDelete(DeleteBehavior.Cascade)`
  - Cascade-Delete: Wenn Aufgabe gelöscht wird, werden alle zugehörigen To-Dos automatisch gelöscht

### `AufgabeService` (Service)

- **Geänderte Methode:** `GetDetailAsync()` — Ergänzung um `.Include(a => a.Todos)`, damit To-Dos beim Laden einer Aufgabe mitgeladen werden
- **Neue Methode:** `CanCompleteTaskAsync(Guid aufgabeId, CancellationToken ct)` — Validiert, ob eine Aufgabe beendet werden kann; gibt `false` zurück, wenn offene To-Dos existieren; gibt `true` zurück, wenn keine offenen To-Dos vorhanden sind

### `EntwicklungsprozessService` (Service)

- **Geänderte Methode:** `AbschliessenAsync()` — Vor Statusübergang zu `Beendet` wird `AufgabeService.CanCompleteTaskAsync()` aufgerufen; falls `false`, wird `ApplicationException` oder ähnlich mit Fehlermeldung geworfen (Fehlerbehandlung erfolgt in ViewModel)

### `TaskDetailViewModel` (ViewModel)

- **Neue Collection:** `ObservableCollection<TodoViewModel> Todos` — Bindung zur TodoListView
- **Neue Property:** `int OffeneTodoCount` — Gibt die Anzahl offener To-Dos zurück (wird von `PropertyChanged` notifiziert, um Badge zu aktualisieren)
- **Neue Property:** `bool IsTodoViewSelected` — Gibt an, ob Todo-Ansicht aktiv ist
- **Neue Ansicht:** `DetailAnsicht.Todos` zur enum hinzufügen
- **Neuer Command:** `ICommand TodoHinzufuegenCommand` — Erstellt neues To-Do mit der eingegebenen Beschreibung
- **Neuer Command:** `ICommand TodoAlsErledeltMarkierenCommand` — Markiert To-Do als erledigt
- **Neuer Command:** `ICommand TodoLoeschenCommand` — Löscht To-Do
- **Neuer Command:** `ICommand TodoAnsichtCommand` — Navigiert zur Todo-Ansicht
- **Geänderte Methode:** `LadenAsync()` — Ergänzung um Todos-Laden und Initialisierung von `OffeneTodoCount`
- **Geänderte Methode:** `AufgabeAbschliessenAsync()` — Vor `EntwicklungsprozessService.AbschliessenAsync()` wird Validierung aufgerufen; falls Fehler, wird `OffeneTodoCount`-Wert in Fehlermeldung angezeigt

### `TaskDetailView.xaml` (View)

- **Neue Tab/Content-Area:** "Todos"-Tab neben existierenden Tabs (Info, CLI, Diff, Dateibrowser, PullRequests)
- **Ribbon-Erweiterung:** Badge/Label an der Aufgaben-Gruppe (z. B. neben "Beenden"-Button) mit:
  - Binding zu `OffeneTodoCount`
  - Visuelle Markierung bei offenen Todos (z. B. `⚠ {count} offen` oder rote Hervorhebung)
  - Ist nur sichtbar/aktiv, wenn `OffeneTodoCount > 0`

## Datenbankmigrationen

| Migrationsname | Betroffene Tabellen/Spalten | Beschreibung der Änderung |
|---|---|---|
| `AddTodoEntity` (oder automatisch generiert) | Neue Tabelle `Todos` mit Spalten: `Id` (Guid, PK), `AufgabeId` (Guid, FK), `Beschreibung` (string), `ErledaltAm` (DateTimeOffset?), `ErstellungsDatum` (DateTimeOffset); FK zu `Aufgaben` mit Cascade-Delete | Erstellt neue Todo-Tabelle und 1:n-Beziehung zu Aufgaben |

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---|---|---|
| `Todo.Beschreibung` | Nicht null oder leer | Beim Erstellen eines To-Dos wird leere Beschreibung abgelehnt |
| `Aufgabe` bei Statusübergang zu `Beendet` | `CanCompleteTaskAsync()` muss `true` zurückgeben (keine offenen Todos) | Aufgabenabschluss wird blockiert, Fehlermeldung zeigt Anzahl offener Todos |

## Konfigurationsänderungen

Keine.

## Seiteneffekte und Risiken

- **DbContext**: Neue Entität `Todo` und neues DbSet müssen korrekt konfiguriert werden (Cascade-Delete wichtig), sonst drohen Datenbankintegrität-Probleme
- **TaskDetailView Ansichten-Enum**: Neue `DetailAnsicht.Todos`-Option könnte mit existierenden View-Selektoren kollidieren, wenn diese nicht explizit erweitert werden (gering, da Pattern etabliert)
- **AufgabeAbschliessenCommand**: Zusätzliche Validierung erhöht die Latenz beim Abschluss minimal, ist aber notwendig für die Anforderung
- **GetDetailAsync**: `.Include(a => a.Todos)` erhöht die Datenbankabfrage-Komplexität minimal, kann aber später optimiert werden (falls Performance-Problem, durch Lazy-Loading oder separate Abfrage)
- **Keine bekannten Auswirkungen auf**: Projektliste, Dateibrowser, CLI-Verwaltung, Protokoll, Pull Requests, Konfiguration — To-Dos sind isoliert auf Aufgaben-Ebene

## Umsetzungsreihenfolge

1. **Entity `Todo` anlegen**
   - Voraussetzungen: Keine
   - Beschreibung: Datei `src/Softwareschmiede/Domain/Entities/Todo.cs` mit Eigenschaften `Id`, `AufgabeId`, `Beschreibung`, `ErledaltAm`, `ErstellungsDatum` und Navigationseigenschaften `Aufgabe` (zurück zur Aufgabe)

2. **DbSet und Konfiguration in `SoftwareschmiededDbContext` hinzufügen**
   - Voraussetzungen: Entity `Todo` angelegt
   - Beschreibung: `DbSet<Todo> Todos` hinzufügen und in `OnModelCreating()` die 1:n-Beziehung mit Cascade-Delete konfigurieren (analog zu `PullRequests`, `Protokolleintraege`, `DiffResults`)

3. **Navigationseigenschaft `Aufgabe.Todos` hinzufügen**
   - Voraussetzungen: Entity `Todo` angelegt
   - Beschreibung: Property `List<Todo> Todos` zur `Aufgabe`-Entity hinzufügen

4. **Entity Framework Migration erstellen und anwenden**
   - Voraussetzungen: DbContext-Konfiguration abgeschlossen
   - Beschreibung: Migration via `dotnet ef migrations add AddTodoEntity` generieren und Migrationen-Datei überprüfen; Migration wird später angewendet

5. **`TodoService` anlegen**
   - Voraussetzungen: Entity `Todo` angelegt, `SoftwareschmiededDbContext` konfiguriert
   - Beschreibung: Neue Klasse `src/Softwareschmiede/Application/Services/TodoService.cs` mit Methoden:
     - `CreateTodoAsync(Guid aufgabeId, string beschreibung, CancellationToken ct)` — Erstellt und speichert neue Todo
     - `MarkTodoAsCompletedAsync(Guid todoId, CancellationToken ct)` — Setzt `ErledaltAm = DateTime.UtcNow`
     - `DeleteTodoAsync(Guid todoId, CancellationToken ct)` — Löscht Todo aus Datenbank
     - `GetOpenTodosAsync(Guid aufgabeId, CancellationToken ct)` — Gibt offene Todos (ErledaltAm == null) einer Aufgabe zurück
     - `GetAllTodosAsync(Guid aufgabeId, CancellationToken ct)` — Gibt alle Todos einer Aufgabe zurück
     - `GetTodoCountAsync(Guid aufgabeId, CancellationToken ct)` — Gibt Anzahl offener Todos zurück

6. **`AufgabeService` erweitern**
   - Voraussetzungen: `TodoService` angelegt
   - Beschreibung: 
     - Methode `GetDetailAsync()` um `.Include(a => a.Todos)` erweitern, damit Todos mitgeladen werden
     - Neue Methode `CanCompleteTaskAsync(Guid aufgabeId, CancellationToken ct)` hinzufügen, die `TodoService.GetOpenTodosAsync()` aufruft und `true` zurückgibt, wenn kein offenes Todo existiert

7. **`EntwicklungsprozessService` erweitern**
   - Voraussetzungen: `AufgabeService.CanCompleteTaskAsync()` angelegt
   - Beschreibung: Methode `AbschliessenAsync()` erweitern: Vor dem Statusübergang zu `Beendet` wird `AufgabeService.CanCompleteTaskAsync()` aufgerufen; falls `false`, wird `InvalidOperationException` mit Nachricht "Diese Aufgabe kann nicht beendet werden, solange noch {count} offene To-Do(s) vorhanden sind." geworfen

8. **`TodoViewModel` anlegen**
   - Voraussetzungen: Entity `Todo` angelegt
   - Beschreibung: Neue Klasse `src/Softwareschmiede.App/ViewModels/TodoViewModel.cs` mit Properties `Id`, `Beschreibung`, `IstErledigt` (read-only, abgeleitet aus `Todo.ErledaltAm`), `ErstellungsDatum` und implementiert `INotifyPropertyChanged` für MVVM-Bindung

9. **`TaskDetailViewModel` erweitern**
   - Voraussetzungen: `TodoService` angelegt, `TodoViewModel` angelegt
   - Beschreibung:
     - `ObservableCollection<TodoViewModel> Todos` Property hinzufügen
     - `int OffeneTodoCount` Property hinzufügen (wird berechnet als Anzahl der Todos mit `ErledaltAm == null`)
     - `bool IsTodoViewSelected` Property hinzufügen
     - `DetailAnsicht.Todos` zur enum hinzufügen
     - Commands hinzufügen: `TodoHinzufuegenCommand`, `TodoAlsErledeltMarkierenCommand`, `TodoLoeschenCommand`, `TodoAnsichtCommand`
     - `LadenAsync()` um Todos-Laden erweitern: `TodoService.GetAllTodosAsync()` aufrufen und `Todos`-Collection befüllen
     - `AufgabeAbschliessenAsync()` um Validierung erweitern: `AufgabeService.CanCompleteTaskAsync()` aufrufen und bei `false` Fehler anzeigen

10. **`TodoListView.xaml` anlegen (UserControl)**
    - Voraussetzungen: `TodoViewModel` anlegen, `TaskDetailViewModel` erweitert
    - Beschreibung: Neue UserControl-Datei `src/Softwareschmiede.App/Views/TodoListView.xaml` und Code-Behind mit:
      - TextBox für neue Todo-Eingabe (mit Command-Binding zu `TodoHinzufuegenCommand`)
      - Button "Hinzufügen" (mit Command-Binding zu `TodoHinzufuegenCommand`)
      - ItemsControl oder ListBox für `Todos`-Collection mit:
        - CheckBox für Erledigt-Status (mit 2-Way-Binding zu `TodoViewModel.IstErledigt`, triggert `TodoAlsErledeltMarkierenCommand`)
        - TextBlock für Beschreibung
        - Button zum Löschen (mit Command-Binding zu `TodoLoeschenCommand`)
        - Visuelle Unterscheidung (z. B. durchgestrichener Text, reduzierte Opazität) für erledigte Todos

11. **`TaskDetailView.xaml` erweitern**
    - Voraussetzungen: `TodoListView` angelegt, `TaskDetailViewModel` erweitert
    - Beschreibung:
      - Neuer Tab "Todos" in der Tab-Leiste hinzufügen (neben Info, CLI, Diff, Dateibrowser, PullRequests)
      - `TodoListView` als Content des Todos-Tabs einbinden (via `<local:TodoListView />`)
      - Binding-Context auf `TaskDetailViewModel` setzen, DataContext-Propertypath zu `Todos` und Commands
      - Ribbon-Badge/Label ergänzen: Visuelles Element (z. B. TextBlock oder Label mit `⚠`-Symbol) mit:
        - Binding zu `TaskDetailViewModel.OffeneTodoCount`
        - Sichtbarkeit nur, wenn `OffeneTodoCount > 0`
        - Beispieltext: "⚠ {OffeneTodoCount} offen" oder rote Markierung neben "Beenden"-Button

12. **Unit Tests für `TodoService` schreiben**
    - Voraussetzungen: `TodoService` angelegt
    - Beschreibung: Neue Testklasse `src/Softwareschmiede.Tests/Application/Services/TodoServiceTests.cs` mit Tests für:
      - `CreateTodoAsync()` — Neues Todo wird erstellt und hat korrekte Werte
      - `MarkTodoAsCompletedAsync()` — Todo wird korrekt als erledigt markiert (`ErledaltAm` ist nicht null)
      - `DeleteTodoAsync()` — Todo wird gelöscht
      - `GetOpenTodosAsync()` — Gibt nur offene Todos zurück
      - `GetAllTodosAsync()` — Gibt alle Todos zurück (offen + erledigt)
      - `GetTodoCountAsync()` — Gibt korrekte Anzahl offener Todos zurück

13. **Unit Tests für Validierungslogik (`CanCompleteTaskAsync`) schreiben**
    - Voraussetzungen: `AufgabeService.CanCompleteTaskAsync()` angelegt
    - Beschreibung: Tests in `src/Softwareschmiede.Tests/Application/Services/AufgabeServiceTests.cs` hinzufügen für:
      - `CanCompleteTaskAsync()` mit offenen Todos — gibt `false` zurück
      - `CanCompleteTaskAsync()` ohne Todos — gibt `true` zurück
      - `CanCompleteTaskAsync()` nur mit erledigten Todos — gibt `true` zurück

14. **Unit Tests für `TodoViewModel` schreiben**
    - Voraussetzungen: `TodoViewModel` angelegt
    - Beschreibung: Neue Testklasse `src/Softwareschmiede.Tests/App/ViewModels/TodoViewModelTests.cs` mit Tests für:
      - Property-Binding: `IstErledigt`, `Beschreibung` können gebunden und aktualisiert werden
      - `PropertyChanged`-Events werden ausgelöst beim Ändern von Properties
      - Commands triggern korrekt

15. **Unit Tests für `TaskDetailViewModel` erweitern**
    - Voraussetzungen: `TaskDetailViewModel` erweitert, Tests für Todo-Commands
    - Beschreibung: Vorhandene Testklasse `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests.cs` ergänzen um:
      - `LadenAsync()` lädt Todos
      - `OffeneTodoCount` wird korrekt aktualisiert
      - `TodoHinzufuegenCommand` erstellt neues Todo
      - `TodoLoeschenCommand` löscht Todo
      - `TodoAlsErledeltMarkierenCommand` markiert Todo
      - `AufgabeAbschliessenCommand` mit offenen Todos zeigt Fehler
      - `AufgabeAbschliessenCommand` ohne Todos erlaubt Abschluss

16. **Integration Tests für Datenbankoperationen schreiben**
    - Voraussetzungen: Migration angelegt, `TodoService` angelegt
    - Beschreibung: Neue Testklasse `src/Softwareschmiede.IntegrationTests/Services/TodoServiceTests.cs` mit Tests für:
      - Todos werden in Datenbank gespeichert
      - Cascade-Delete bei Aufgabenlöschung funktioniert (Todos werden gelöscht)
      - Beziehung Aufgabe ↔ Todo korrekt
      - `ErledaltAm` wird korrekt gespeichert und abgerufen

17. **E2E Tests für UI schreiben**
    - Voraussetzungen: Alle vorherigen Schritte abgeschlossen
    - Beschreibung: Erweitern Sie `src/Softwareschmiede.Tests/App/Views/TaskDetailViewTests.cs` oder erstellen Sie neue E2E-Testklasse für:
      - Aufgabe öffnen → Todo-Liste wird geladen und angezeigt
      - Neues Todo erstellen → Wird der Liste hinzugefügt
      - Todo abhaken → Visueller Status ändert sich
      - Todo löschen → Wird aus Liste entfernt
      - Aufgabe mit offenen Todos beenden → Fehler wird angezeigt, Abschluss blockiert
      - Badge zeigt korrekte Anzahl offener Todos
      - Alle Todos erledigt → Badge zeigt "0" oder wird verborgen

18. **Build und alle Tests durchführen**
    - Voraussetzungen: Alle Tests geschrieben
    - Beschreibung: `dotnet build` und `dotnet test` ausführen, um sicherzustellen, dass alle Tests grün sind und keine neuen Fehler eingeführt wurden

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|---|---|---|
| `CreateTodoAsync_CreatesAndSavesTodo` | TodoServiceTests | Neues Todo wird erstellt und in DB gespeichert |
| `MarkTodoAsCompletedAsync_SetsErledaltAm` | TodoServiceTests | ErledaltAm wird auf DateTime.UtcNow gesetzt |
| `DeleteTodoAsync_RemovesFromDatabase` | TodoServiceTests | Todo wird aus DB gelöscht |
| `GetOpenTodosAsync_ReturnsOnlyOpen` | TodoServiceTests | Nur offene Todos (ErledaltAm == null) werden zurückgegeben |
| `GetAllTodosAsync_ReturnsAll` | TodoServiceTests | Alle Todos (offen + erledigt) werden zurückgegeben |
| `GetTodoCountAsync_ReturnsCorrectCount` | TodoServiceTests | Korrekte Anzahl offener Todos |
| `CanCompleteTaskAsync_WithOpenTodos_ReturnsFalse` | AufgabeServiceTests | Validation blockiert bei offenen Todos |
| `CanCompleteTaskAsync_WithoutTodos_ReturnsTrue` | AufgabeServiceTests | Validation erlaubt Abschluss ohne Todos |
| `CanCompleteTaskAsync_WithOnlyCompletedTodos_ReturnsTrue` | AufgabeServiceTests | Validation erlaubt Abschluss mit nur erledigten Todos |
| `Constructor_PropertiesBindable` | TodoViewModelTests | Properties sind bindbar |
| `PropertyChanged_RaisedOnPropertyUpdate` | TodoViewModelTests | PropertyChanged-Event wird ausgelöst |
| `LoadAsync_LoadsTodos` | TaskDetailViewModelTests (erweitert) | LadenAsync lädt Todos in Collection |
| `OffeneTodoCount_UpdatesCorrectly` | TaskDetailViewModelTests (erweitert) | OffeneTodoCount wird aktualisiert |
| `TodoHinzufuegenCommand_CreatesTodo` | TaskDetailViewModelTests (erweitert) | TodoHinzufuegenCommand erstellt neues Todo |
| `TodoLoeschenCommand_DeletesTodo` | TaskDetailViewModelTests (erweitert) | TodoLoeschenCommand löscht Todo |
| `TodoAlsErledeltMarkierenCommand_MarksTodoCompleted` | TaskDetailViewModelTests (erweitert) | TodoAlsErledeltMarkierenCommand markiert Todo |
| `AufgabeAbschliessenCommand_WithOpenTodos_ShowsError` | TaskDetailViewModelTests (erweitert) | Fehler wird angezeigt, wenn offene Todos |
| `AufgabeAbschliessenCommand_WithoutOpenTodos_Succeeds` | TaskDetailViewModelTests (erweitert) | Abschluss ist möglich ohne offene Todos |
| `Cascade_Delete_DeletesAllTodos` | TodoServiceTests (Integration) | Cascade-Delete löscht Todos bei Aufgabenlöschung |
| `E2E_CreateAndCompleteTask` | TaskDetailViewTests oder neue E2E-Testklasse | Kompletter Ablauf: Aufgabe öffnen, Todo erstellen, abhaken, löschen, beenden |
| `E2E_CompletionBlocked_WithOpenTodos` | TaskDetailViewTests oder neue E2E-Testklasse | Aufgabenabschluss wird blockiert bei offenen Todos |
| `CreateTestTodoAsync()` | Test-Helper | Hilfsmethode zum Erstellen von Test-Todos |
| `CreateTestAufgabeWithTodosAsync()` | Test-Helper | Hilfsmethode zum Erstellen von Test-Aufgaben mit Todos |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|---|---|
| `TaskDetailViewModelTests` | Erweiterung um Todo-Related Tests und LadenAsync-Überprüfung (Todos müssen geladen werden) |
| `TaskDetailViewTests` (E2E) | Möglicherweise: UI-Navigation zu neuem Todo-Tab könnte andere Tab-Selektoren beeinflussen; Ribbon-Badge muss in E2E-Tests berücksichtigt werden |
| `AufgabeServiceTests` | Erweiterung um `CanCompleteTaskAsync()`-Tests |

### E2E-Tests (Pflicht)

| Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium |
|---|---|---|
| Aufgabe öffnen und Todo-Liste wird angezeigt | TaskDetailViewTests oder neue E2E-Testklasse | "Aufgabe öffnen → To-Do-Liste wird geladen und angezeigt" |
| Neues Todo erstellen | TaskDetailViewTests oder neue E2E-Testklasse | "To-Do erstellen → TodoService wird aufgerufen, To-Do wird der Liste hinzugefügt" |
| Todo abhaken | TaskDetailViewTests oder neue E2E-Testklasse | "To-Do abhaken → TodoService markiert als `ErledaltAm = DateTime.UtcNow`" |
| Todo löschen | TaskDetailViewTests oder neue E2E-Testklasse | "To-Do löschen → TodoService.DeleteTodoAsync, TodoViewModel entfernt aus Collection" |
| Aufgabe mit offenen Todos beenden (blockiert) | TaskDetailViewTests oder neue E2E-Testklasse | "Aufgabe beenden mit offenen To-Dos → Validierungsprüfung lädt `GetOpenTodosAsync()`, falls != 0, UI zeigt Fehler und verhindert Status-Übergang" |
| Badge zeigt offene Todo-Anzahl | TaskDetailViewTests oder neue E2E-Testklasse | "Badge zeigt korrekte Anzahl offener Todos im Ribbon" |
| Aufgabe beenden nach Erledigung aller Todos (erfolgreich) | TaskDetailViewTests oder neue E2E-Testklasse | "Alle To-Dos erledigt → Badge zeigt '0', Beenden ist erlaubt" |

Welche bestehenden E2E-Tests müssen angepasst werden?

Keine bekannten E2E-Tests sollten durch To-Do-Feature beeinträchtigt werden, da Todos eine neue, isolierte Funktionalität sind. Falls TaskDetailViewTests jedoch alle Tabs durchlaufen oder den Ribbon überprüfen, müssen diese möglicherweise aktualisiert werden, um den neuen Todo-Tab zu berücksichtigen.

## Offene Punkte

Alle offenen Fragen aus der Anforderung wurden mit Standardentscheidungen geklärt:

1. **Priorität von To-Dos:** Nicht implementiert. Todos werden nach Erstellungsdatum sortiert (aufsteigend oder absteigend, wird in Implementation detailliert).
2. **To-Do-Kategorien/Beschriftungen:** Nicht implementiert. Todos sind einfache Text-Elemente ohne Tags oder Kategorien.
3. **Löschverhalten:** Nur über die UI möglich. Sobald Aufgabe beendet ist, können To-Dos nicht mehr gelöscht werden (wird in Implementation überprüft).
4. **To-Do-Details:** Nur Beschreibung. Keine Fälligkeitsdaten, zugeordneten Personen oder Anhänge.
5. **UI-Position:** Separater Tab in der Aufgabendetailansicht neben anderen Tabs (Info, CLI, Diff, Dateibrowser, PullRequests).
6. **Fehlermeldung bei Beendungsversuch:** Zeigt Anzahl offener To-Dos (z. B. "Diese Aufgabe kann nicht beendet werden, solange noch 3 offene To-Do(s) vorhanden sind.").
7. **Ribbon-Badge-Design:** Zeigt sowohl Zahl als auch Kontext (z. B. "⚠ 3 offen" oder einfach Markierung mit Zahl, wird in Implementation detailliert).
8. **Zu-Do-Verlauf:** Erledigte To-Dos werden nicht archiviert; Löschen ist jederzeit möglich. Der Verlauf ist nicht einsehbar.
9. **Massenverwaltung:** Nicht implementiert. Todos können nur einzeln abhakt/gelöscht werden.
10. **Export/Reporting:** Nicht implementiert. Nur In-App-Anzeige.

Keine weiteren offenen Punkte bestehen.
