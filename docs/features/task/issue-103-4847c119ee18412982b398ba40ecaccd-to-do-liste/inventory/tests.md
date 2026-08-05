# Tests - Bestandsaufnahme

## Bestehende Test-Struktur (für Referenzmuster)

### Unit Tests für Services

#### `AufgabeServiceTests`
Datei: `src/Softwareschmiede.Tests/Application/Services/AufgabeServiceTests.cs`

Testmuster für CRUD-Operationen auf Entities.

#### `ProtokollServiceTests`
Datei: `src/Softwareschmiede.Tests/Application/Services/ProtokollServiceTests.cs`

Testmuster für Service mit 1:n-Beziehungen (ähnlich wie Todo).

#### `PullRequestReferenzServiceTests`
Datei: `src/Softwareschmiede.Tests/Application/Services/PullRequestReferenzServiceTests.cs`

Testmuster für Verwaltung von Referenzen (ähnlich wie Todo).

#### `EntwicklungsprozessServiceTests`
Datei: `src/Softwareschmiede.Tests/Application/Services/EntwicklungsprozessServiceTests.cs`

Testmuster für Geschäftsprozess-Services. Sollte als Basis für Todo-Validierungstests verwendet werden.

### ViewModel Tests

#### `TaskDetailViewModelTests`
Datei: `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests.cs`

Testmuster für ViewModel-Funktionalität:
- LadenAsync
- Commands (SpeichernCommand, LoeschenCommand, etc.)
- Property-Änderungen
- State Management

#### `TaskDetailViewModelTests_PluginAktivierung`
Datei: `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests_PluginAktivierung.cs`

Testmuster für spezialisierte ViewModel-Szenarien.

### View Tests

#### `TaskDetailViewTests`
Datei: `src/Softwareschmiede.Tests/App/Views/TaskDetailViewTests.cs`

E2E-Tests für die TaskDetailView.

### Integration Tests

#### `AufgabeServiceTests` (Integration)
Datei: `src/Softwareschmiede.IntegrationTests/Services/AufgabeServiceTests.cs`

Testmuster für Datenbankoperationen mit echtem DbContext.

## Zu erstellende Tests

### Unit Tests für TodoService

**Datei:** `src/Softwareschmiede.Tests/Application/Services/TodoServiceTests.cs`

Sollte folgende Szenarien abdecken:
- `CreateTodoAsync` - Neues Todo erstellen
- `MarkTodoAsCompletedAsync` - Todo als erledigt markieren
- `DeleteTodoAsync` - Todo löschen
- `GetOpenTodosAsync` - Offene Todos abrufen
- `GetAllTodosAsync` - Alle Todos abrufen
- `GetTodoCountAsync` - Anzahl offener Todos

Testmuster orientiert sich an `ProtokollServiceTests` oder `PullRequestReferenzServiceTests`.

### Unit Tests für Validierungslogik

**Datei:** `src/Softwareschmiede.Tests/Application/Services/AufgabeCompletionValidationTests.cs` oder erweitert in `AufgabeServiceTests.cs`

Sollte folgende Szenarien abdecken:
- `CanCompleteTaskAsync` - Validierung bei offenen Todos
  - Mit offenen Todos: false
  - Ohne offene Todos: true
  - Mit nur erledigten Todos: true

Testmuster orientiert sich an `EntwicklungsprozessServiceTests`.

### Unit Tests für TodoViewModel

**Datei:** `src/Softwareschmiede.Tests/App/ViewModels/TodoViewModelTests.cs`

Sollte folgende Szenarien abdecken:
- Property-Binding (IstErledigt, Beschreibung)
- PropertyChanged-Events
- Commands (Delete, MarkCompleted)

### Unit Tests für TaskDetailViewModel (Erweiterung)

**Datei:** `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests.cs` (erweitern)

Sollte erweiterte Szenarien abdecken:
- LadenAsync lädt Todos
- OffeneTodoCount wird korrekt aktualisiert
- TodoHinzufuegenCommand erstellt neue Todos
- TodoLoeschenCommand löscht Todos
- TodoAlsErledeltMarkierenCommand markiert Todos
- AufgabeAbschliessenCommand validiert offene Todos
  - Mit offenen Todos: Fehler wird angezeigt
  - Ohne offene Todos: Abschluss ist erlaubt

### Integration Tests für Datenbankoperationen

**Datei:** `src/Softwareschmiede.IntegrationTests/Services/TodoServiceTests.cs`

Sollte folgende Szenarien abdecken:
- Todos werden in Datenbank gespeichert
- Cascade-Delete bei Aufgabenlöschung
- Beziehung Aufgabe ↔ Todo korrekt
- ErledaltAm wird korrekt gespeichert

Testmuster orientiert sich an `src/Softwareschmiede.IntegrationTests/Services/AufgabeServiceTests.cs`.

### E2E Tests für UI

**Datei:** `src/Softwareschmiede.Tests/App/Views/TaskDetailViewTests.cs` (erweitern) oder neuer E2E Test

Sollte folgende Szenarien abdecken:
- Aufgabe öffnen → Todo-Liste wird geladen und angezeigt
- Neues Todo erstellen → Wird der Liste hinzugefügt
- Todo abhaken → IstErledigt wird aktualisiert
- Todo löschen → Wird aus Liste entfernt
- Aufgabe mit offenen Todos beenden → Fehler wird angezeigt
- Badge zeigt korrekte Anzahl offener Todos
- Alle Todos erledigt → Badge zeigt "0"

## Test-Hilfsmethoden (zu erstellen)

### Aufgaben-Builder

Für Todo-Tests sollten Helper-Methoden ähnlich denen in bestehenden Tests erstellt werden:
- `CreateTestAufgabeAsync()` - Erstellt Test-Aufgabe
- `CreateTestTodoAsync(aufgabeId, beschreibung)` - Erstellt Test-Todo
- `MarkTodoCompletedAsync(todoId)` - Markiert Test-Todo als erledigt

## Testdaten und Fixtures

Vorhandene Patterns:
- `DatabaseFixture` (src/Softwareschmiede.IntegrationTests/Infrastructure/DatabaseFixture.cs) für DbContext-Tests
- `TestDbContextFactory` (src/Softwareschmiede.Tests/Helpers/TestDbContextFactory.cs) für Unit Tests

Sollten um Todo-Support erweitert werden (falls nötig).

## Test-Kategorisierung (CLAUDE.md)

Laut CLAUDE.md:
- Stable test lane: `dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category!=OsInterface"`
- OS-Interface Tests separat: `--filter "Category=OsInterface"`

E2E-Tests für Todo UI sollten nach Category kategorisiert werden (wahrscheinlich nicht OsInterface, da keine echte Prozess-Verwaltung nötig).
