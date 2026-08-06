# Test-Übersicht

## Testklassen

### `AufgabeServiceTests`
Datei: `src/Softwareschmiede.Tests/Application/Services/AufgabeServiceTests.cs`

Testklasse für `AufgabeService` mit Methoden zum Testen von CRUD-Operationen.

**Tests, die `GetDetailAsync` verwenden:**
- `TryAssignIssueReferenzIfNoneAsync_ShouldPersistIssueReference_WhenNoneExists()` (Line 160–172): Lädt Aufgabe nach Issue-Zuweisung mit `GetDetailAsync`
- `TryAssignIssueReferenzAndUpdateDescriptionIfNoneAsync_ShouldPersistReferenceAndDescription_WhenNoneExists()` (Line 176–191): Lädt mit `GetDetailAsync` zur Verifikation
- `TryAssignIssueReferenzAndUpdateDescriptionIfNoneAsync_ShouldKeepDescription_WhenUpdateFlagIsFalse()` (Line 195–210): Nutzt `GetDetailAsync`
- `TryAssignIssueReferenzAndUpdateDescriptionIfNoneAsync_ShouldReturnFalseAndKeepDescription_WhenReferenceExists()` (Line 214–231): Nutzt `GetDetailAsync`
- `TryAssignIssueReferenzIfNoneAsync_ShouldReturnFalseAndKeepExistingReference_WhenReferenceExists()` (Line 235–248): Nutzt `GetDetailAsync`
- `UpdateIssueReferenzAsync_ShouldOverwriteExistingReference_WhenReferenceExists()` (Line 252–264): Nutzt `GetDetailAsync`

**Wichtig**: Diese Tests laden Aufgaben mit `GetDetailAsync`, was beim Umschreiben der Include-Kette angepasst werden muss, wenn die Tests Protokoll-Verhalten prüfen wollen.

---

### `TaskDetailViewModelTests`
Datei: `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests.cs`

Umfangreiches Test-Set für `TaskDetailViewModel`. Initialisierung nutzt TestDatenbank-Factory und Mock-Services.

**Setup (Lines 38–127):**
- Erstellt `_aufgabeService` mit echtem `AufgabeService`
- Erstellt `_protokollService` mit echtem `ProtokollService`
- Mock-Plugin-Manager und Service-Provider
- Erstellt `_db` mit `TestDbContextFactory.Create()`

**Relevante Tests für LadenAsync-Verhalten:**
- `AufgabeId_Setter_UsesFireAndForgetSafely()` (Line 214–228): Prüft, dass `LadenAsync` via SafeFireAndForget ausgelöst wird
- `ShowEditPanel_IsTrue_WhenStatusNeu()` (Line 234–244): Lädt Aufgabe, prüft Panel-Sichtbarkeit
- `ShowCliPanel_IsTrue_WhenStatusGestartet()` (Line 248–258): Lädt Aufgabe mit Status
- `ShowCliPanel_IsTrue_WhenStatusWartend()` (Line 262–270): Laden mit anderer Status
- `ShowDiffPanel_IsTrue_WhenStatusBeendet()` (Line 274–284): Status-abhängiges Laden

**Wichtig**: Tests verwenden `LadenCommand.ExecuteAsync()` (auch wenn Async), um Laden synchron in Tests zu warten. Nach Umschreiben der Anforderung müssen Tests ggf. das neue asynchrone Protokoll-Laden beachten.

---

## Hilfsmethoden und Factories

### Test-Helfer in `TaskDetailViewModelTests`

| Methode | Zweck |
|---------|-------|
| `CreateSut(...)` (Line 132–176) | Erstellt eine TaskDetailViewModel-Instanz für Tests mit Mock-Services |
| `ErstelleAufgabe(AufgabeStatus status)` (Line 178–184) | Hilfsfunktion: Erstellt eine Test-Aufgabe mit optionalem Status, lädt mit `GetByIdAsync` |

### Test-Factories und Context

| Hilfsmittel | Verwendung |
|------------|-----------|
| `TestDbContextFactory.Create()` (Line 40, 25) | Erstellt einen In-Memory EF Core DbContext für Unit-Tests |
| `TestKiAusfaehrungsServiceFactory.Create()` (Line 45) | Erstellt einen Mock `KiAusfaehrungsService` für Tests |
| `TestTempDirectoryFixture` (Line 35) | Erstellt temporäre Verzeichnisse für Datei-Tests |

---

## Abhängigkeiten zwischen Tests und zu testender Code

- Tests laden über `_aufgabeService.GetDetailAsync()` oder `GetByIdAsync()` — **muss angepasst werden**, wenn Include-Struktur geändert wird
- Tests verwenden `LadenCommand.ExecuteAsync()` zum Warten auf `LadenAsync` — müssen möglicherweise angepasst werden, wenn Laden nicht mehr blockiert
- Tests prüfen `Aufgabe` Property und ObservableCollection `Protokolleintraege` — beide bleiben, aber Timing ändert sich
