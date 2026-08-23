# Umsetzungsplan: Initialisierungsskript für geklonte Repositories

## Übersicht

Das System wird um eine optionale Initialisierungsfunktion pro Git-Repository erweitert, die automatisch nach dem Klonen ausgeführt wird. Ein neuer Service `RepositoryInitialisierungService` führt das konfigurierte Skript mittels PowerShell aus (analog zu `RepositoryStartskriptService`). Fehler werden geloggt, blockieren aber nicht die Aufgabe. Die UI wird um ein Suchfeld für Initialisierungsskripte erweitert, mit Autocompletion basierend auf Remote-Repository-Dateien. Eine neue Datenbank-Entity `RepositoryInitialisierungKonfiguration` persistiert die Konfiguration pro Repository.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Entity-Modellierung | Separate `RepositoryInitialisierungKonfiguration`-Entität (statt direktes Feld auf `GitRepository`) | Konsistenz mit `RepositoryStartKonfiguration`; ermöglicht flexible Konfiguration und optionale Beziehung (0..1); Erweiterbarkeit für zukünftige Konfigurationsfelder |
| Fehlerbehandlung | Fehler werden geloggt (Warning), nicht als Exception propagiert | Anforderung explizit: „Fehler blockieren nicht die weitere Bearbeitung"; unterscheidet sich von `RepositoryStartskriptService`, da Initialisierung optional und nicht-kritisch ist |
| Ausführungskontext | PowerShell-Wrapper über `ICliRunner` (wie `RepositoryStartskriptService`) | Konsistenz mit Startskript-Ausführung; Unterstützung verschiedener Script-Formate (.ps1, .bat, .cmd, .sh) |
| Integrationspunkt | Nach erfolgreichem Klon, vor KI-Agent-Start (in `FinalizeStartAsync()`) | Logischer Platz im Lifecycle; blockiert nicht KI-Execution; analog zu Startskript-Hook |
| Path-Traversal-Validierung | Identisch zu `RepositoryStartskriptService` | Sicherheit gegen Directory-Escape-Versuche; bewährtes Muster im Codebase |
| Reihenfolge bei mehreren Skripten | Initialisierungsskript → Startskript (falls beide konfiguriert) | Initialisierung logisch vor Startup; Konfiguration initialisiert, Startup nutzt die initialisierte Umgebung |

## Programmabläufe

### Initialisierungsskript nach Repository-Klon

1. Benutzer (oder System) startet `EntwicklungsprozessService.ProzessStartenAsync()` oder `ProzessStartenUndCliStartenAsync()`
2. Service führt `PrepareCloneDirectoryAsync()` aus → Repository wird geklont
3. Service führt `SetupBranchAsync()` aus → Branch wird erstellt/gewechselt
4. Service führt `FinalizeStartAsync()` aus (private Methode)
5. In `FinalizeStartAsync()`:
   - Wenn `repository.InitialisierungKonfiguration` existiert und nicht `null`:
     - `RepositoryInitialisierungService.RunAsync(lokalerKlonPfad, repository.InitialisierungKonfiguration, ct)` aufrufen
     - Fehler abfangen: als Warning loggen, Nachricht in Protokoll aufnehmen
   - Dann: Startskript ausführen (falls konfiguriert) — wie bisher
6. KI-Agent-Start in `CliNeustartenAsync()` — Initialisierung bereits abgeschlossen

**Beteiligte Klassen/Komponenten:** `EntwicklungsprozessService`, `RepositoryInitialisierungService`, `RepositoryInitialisierungKonfiguration`, `GitRepository`

### Laden von Initialisierungsskript-Vorschlägen im Projekt-Detail

1. Benutzer öffnet Projekt-DetailView
2. `ProjectDetailViewModel.LadenAsync()` wird aufgerufen
3. ViewModel lädt Projekt und Repositories
4. Bei Auswahl eines Repositories (oder automatisch beim Laden):
   - Wenn Repository einen Remote-URL hat:
     - `ProjectDetailViewModel.LoadInitialisierungsskriptSuggestionsAsync()` aufrufen
     - Remote-Repository abrufen (via `IPluginManager` → SCM-Plugin)
     - Ausführbare Dateien identifizieren (.ps1, .cmd, .bat, .sh, .exe)
     - Liste in `InitialisierungsskriptSuggestionen` speichern
5. UI bindet `InitialisierungsskriptSuggestionen` an AutoComplete/ComboBox
6. Benutzer wählt Skript oder gibt manuell Pfad ein → `SelectedInitialisierungsskript` gesetzt
7. Benutzer klickt „Speichern" → `SaveInitialisierungsskriptAsync()` aufgerufen
8. ViewModel ruft `ProjektService.SaveRepositoryInitialisierungskriptAsync(repositoryId, path, ct)` auf
9. Service speichert `RepositoryInitialisierungKonfiguration` in Datenbank

**Beteiligte Klassen/Komponenten:** `ProjectDetailViewModel`, `ProjektService`, `IPluginManager`, `IGitPlugin`, `SoftwareschmiededDbContext`

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `RepositoryInitialisierungKonfiguration` | Entity / Datenmodellklasse | Speichert Konfiguration eines Initialisierungsskripts pro Repository (Pfad, Aktiv-Status) |
| `RepositoryInitialisierungService` | Service-Klasse | Führt Initialisierungsskript mittels PowerShell aus; validiert Pfade; loggt Fehler statt zu werfen |

## Änderungen an bestehenden Klassen

### `GitRepository` (Entity)

Datei: `src/Softwareschmiede/Domain/Entities/GitRepository.cs`

- **Neue Properties:**
  - `InitialisierungKonfiguration: RepositoryInitialisierungKonfiguration?` — Navigationseigenschaft zur Initialisierungskonfiguration (0..1 Beziehung)

### `SoftwareschmiededDbContext` (DbContext)

Datei: `src/Softwareschmiede/Domain/Data/SoftwareschmiededDbContext.cs`

- **Neue Properties:**
  - `DbSet<RepositoryInitialisierungKonfiguration> RepositoryInitialisierungKonfigurationen { get; set; }` — Tabelle für Initialisierungskonfigurationen
- **Konfiguration in `OnModelCreating()` (optional, falls noch nicht über Conventions abgedeckt):**
  - Entity-Konfiguration für `RepositoryInitialisierungKonfiguration`: Foreign Key zu `GitRepository`, Cascade Delete, Constraints

### `EntwicklungsprozessService` (Service)

Datei: `src/Softwareschmiede/Application/Services/EntwicklungsprozessService.cs`

- **Neue Abhängigkeit (optional via DI):**
  - `RepositoryInitialisierungService?` — analog zu `RepositoryStartskriptService` (über `EntwicklungsprozessServiceOptions`)
- **Änderung in `FinalizeStartAsync()` (Zeile ~549-575):**
  - Nach erfolgreichem Klon und Branch-Setup, BEVOR Startskript aufgerufen wird:
    - Prüfe: `if (repository.InitialisierungKonfiguration != null && _options.RepositoryInitialisierungService != null)`
    - Rufe auf: `await _options.RepositoryInitialisierungService.RunAsync(lokalerKlonPfad, repository.InitialisierungKonfiguration, ct)`
    - Fehlerbehandlung: `try-catch`, Fehler als Warning loggen, Nachricht in Protokoll aufnehmen
    - Keine Exception werfen — Aufgabe wird normal fortgesetzt

### `ProjectDetailViewModel` (ViewModel)

Datei: `src/Softwareschmiede.App/ViewModels/ProjectDetailViewModel.cs`

- **Neue Properties:**
  - `InitialisierungsskriptSuggestionen: ObservableCollection<string>` — Liste ausführbarer Dateien aus Remote-Repository
  - `SelectedInitialisierungsskript: string?` — Vom Benutzer ausgewähltes oder manuell eingegebenes Skript
  - `IsEditingInitialisierungsskript: bool` — Gibt an, ob Bearbeitungsmodus aktiv ist
  - `InitialisierungsskriptLoadingFailed: bool?` — Gibt an, ob Laden fehlgeschlagen ist (für Error-UI)
- **Neue Methoden:**
  - `LoadInitialisierungsskriptSuggestionsAsync(repositoryId: Guid, ct: CancellationToken): Task` — Lädt Vorschläge vom Remote-Repository
  - `SaveInitialisierungsskriptAsync(ct: CancellationToken): Task` — Persistiert ausgewähltes Skript in Datenbank
  - `CancelInitialisierungsskriptEdit(): void` — Bricht Bearbeitung ab

### `ProjectDetailView.xaml` (View)

Datei: `src/Softwareschmiede.App/Views/ProjectDetailView.xaml`

- **Neue UI-Elemente (im Repository-Konfigurationsbereich):**
  - Label: „Initialisierungsskript:"
  - AutoComplete/ComboBox/SearchField: Bindet an `SelectedInitialisierungsskript`; ItemsSource an `InitialisierungsskriptSuggestionen`
  - Button „Laden": Triggert `LoadInitialisierungsskriptSuggestionsAsync()`
  - Button „Speichern": Triggert `SaveInitialisierungsskriptAsync()`
  - Button „Abbrechen": Triggert `CancelInitialisierungsskriptEdit()`
  - Optional: ProgressRing während Laden; ErrorTextBlock für Ladefehlermeldungen

### `ProjektService` (Service)

Datei: `src/Softwareschmiede/Application/Services/ProjektService.cs`

- **Neue Methode:**
  - `SaveRepositoryInitialisierungskriptAsync(repositoryId: Guid, initialisierungsskriptRelativePfad: string?, ct: CancellationToken): Task<RepositoryInitialisierungKonfiguration>` — Erstellt oder aktualisiert `RepositoryInitialisierungKonfiguration`; wenn `initialisierungsskriptRelativePfad` null, wird Konfiguration gelöscht

### `PluginSelectionService` oder Ähnliches (für Remote-Datei-Zugriff)

Falls noch nicht vorhanden: Helper-Methode zum Abrufen ausführbarer Dateien aus Remote-Repository via SCM-Plugin.

## Datenbankmigrationen

| Migrationsname | Betroffene Tabellen/Spalten | Beschreibung der Änderung |
|----------------|----------------------------|---------------------------|
| `AddRepositoryInitialisierungKonfiguration` | Neue Tabelle `RepositoryInitialisierungKonfigurationen` + FK-Spalte in `GitRepositories` | Erstellt Tabelle mit Spalten: `Id` (GUID PK), `GitRepositoryId` (FK zu GitRepositories), `InitialisierungsskriptRelativePfad` (nvarchar(max)), `Aktiv` (bit, Default 1). Optionale Spalte in `GitRepositories`: `InitialisierungKonfigurationId` (für 0..1-Beziehung, falls Fremdschlüssel über Join-Tabelle statt direkter FK) |

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| `InitialisierungskriptRelativePfad` | Darf nicht leer sein, wenn Konfiguration gespeichert wird | Exception bei leerer Eingabe |
| `InitialisierungskriptRelativePfad` | Muss relative Pfade sein (keine absoluten Pfade) | `InvalidOperationException` bei versucht absolutem Pfad |
| `InitialisierungskriptRelativePfad` | Darf nicht außerhalb des Repository-Roots liegen (Path-Traversal-Schutz) | `InvalidOperationException` wenn Pfad mit `..` außerhalb führt |
| `GitRepositoryId` in `RepositoryInitialisierungKonfiguration` | Muss auf existierendes Repository verweisen | Foreign Key Constraint in DB |
| `Aktiv` | Bool, Default `true` | Keine spezielle Validierung notwendig |

## Konfigurationsänderungen

| Eintrag | Typ | Standardwert | Zweck |
|---------|-----|--------------|-------|
| `EntwicklungsprozessServiceOptions.RepositoryInitialisierungService` | Service-Abhängigkeit | `null` (optional) | Dependency Injection für neuen Service (analog zu `RepositoryStartskriptService`) |

## Seiteneffekte und Risiken

- **EntwicklungsprozessService-Lifecycle:** Mit der neuen Initialisierungsskript-Ausführung wird die Aufgaben-Finalisierung um eine zusätzliche Schritte erweitert. Fehler im Initialisierungsskript werden geloggt; da sie nicht propagiert werden, können Logging-Filter/Monitore übersehen werden. **Mitigation:** Klare Logging-Kategorien und Protokolleintrag mit Status.
- **Datenbank-Migration:** Existierende Projekte erhalten leere `RepositoryInitialisierungKonfigurationen`. Kein Breaking Change, da Beziehung optional ist.
- **Plugin-Abhängigkeit:** Die Suggestion-Logik in `ProjectDetailViewModel` hängt von `IPluginManager` und SCM-Plugins ab. Falls keine Plugins vorhanden, können keine Vorschläge geladen werden. **Mitigation:** Graceful Error-Handling im ViewModel; Benutzer kann manuell Pfad eingeben.
- **E2E-Tests:** Tests mit realen Remote-Repositories (GitHub API) können rate-limited sein. **Mitigation:** Mock-Plugins oder lokale Test-Repositories verwenden.

## Umsetzungsreihenfolge

1. **Erstelle neue Entity `RepositoryInitialisierungKonfiguration`**
   - Voraussetzungen: Keine
   - Beschreibung: Neue Klasse in `src/Softwareschmiede/Domain/Entities/RepositoryInitialisierungKonfiguration.cs` mit Properties: `Id`, `GitRepositoryId`, `InitialisierungsskriptRelativePfad`, `Aktiv`, Navigationseigenschaft `GitRepository`

2. **Erweiter `GitRepository`-Entity mit Navigationseigenschaft**
   - Voraussetzungen: Schritt 1 abgeschlossen
   - Beschreibung: Füge `InitialisierungKonfiguration: RepositoryInitialisierungKonfiguration?` zu `GitRepository` hinzu

3. **Aktualisiere `SoftwareschmiededDbContext`**
   - Voraussetzungen: Schritte 1–2 abgeschlossen
   - Beschreibung: Füge `DbSet<RepositoryInitialisierungKonfiguration>` hinzu; stelle sicher, dass Entity-Mapping/Conventions korrekt sind (OnModelCreating, bei Bedarf)

4. **Erstelle Datenbank-Migration**
   - Voraussetzungen: Schritte 1–3 abgeschlossen
   - Beschreibung: Ausführe `dotnet ef migrations add AddRepositoryInitialisierungKonfiguration --project src/Softwareschmiede.Migrations --startup-project src/Softwareschmiede.Domain` (oder entsprechender CLI-Befehl für dieses Projekt); verifiziere Migration

5. **Erstelle `RepositoryInitialisierungService`**
   - Voraussetzungen: `ICliRunner` Interface (bereits vorhanden), `ILogger<T>` (bereits vorhanden), Entity `RepositoryInitialisierungKonfiguration` (Schritt 1 abgeschlossen)
   - Beschreibung: Neue Service-Klasse `src/Softwareschmiede/Application/Services/RepositoryInitialisierungService.cs` mit `RunAsync()`-Methode; folge dem Muster von `RepositoryStartskriptService`, aber:
     - Fehler werden als Warning geloggt, nicht als Exception geworfen
     - Path-Traversal-Validierung identisch zu Startskript-Service

6. **Registriere `RepositoryInitialisierungService` in DI-Container**
   - Voraussetzungen: Schritt 5 abgeschlossen; DI-Konfiguration vorhanden
   - Beschreibung: Registriere Service in Startup/DI-Setup; optional als `RepositoryInitialisierungService?` in `EntwicklungsprozessServiceOptions`

7. **Erweitere `EntwicklungsprozessService`**
   - Voraussetzungen: Schritte 1, 5, 6 abgeschlossen
   - Beschreibung: 
     - Füge optionale `RepositoryInitialisierungService`-Abhängigkeit hinzu (via `EntwicklungsprozessServiceOptions` Record)
     - In `FinalizeStartAsync()`: Nach erfolgreichem Klon/Branch-Setup und VOR Startskript-Aufruf: If-Prüfung für `repository.InitialisierungKonfiguration` → `RunAsync()` mit Try-Catch → Warning-Logging bei Fehler

8. **Erweitere `ProjektService`**
   - Voraussetzungen: Schritte 1, 3 abgeschlossen; `ProjektService` existiert
   - Beschreibung: Neue Methode `SaveRepositoryInitialisierungskriptAsync()` — erstellt/aktualisiert/löscht `RepositoryInitialisierungKonfiguration`

9. **Erweitere `ProjectDetailViewModel`**
   - Voraussetzungen: Schritte 1, 8 abgeschlossen; `IPluginManager` verfügbar
   - Beschreibung: 
     - Properties: `InitialisierungsskriptSuggestionen`, `SelectedInitialisierungsskript`, `IsEditingInitialisierungsskript`, `InitialisierungsskriptLoadingFailed`
     - Methoden: `LoadInitialisierungsskriptSuggestionsAsync()`, `SaveInitialisierungsskriptAsync()`, `CancelInitialisierungsskriptEdit()`
     - Integration: In `LadenAsync()` optional beim Laden eines Repositories suggestioning aufrufen (oder lazy load bei Repository-Auswahl)

10. **Aktualisiere `ProjectDetailView.xaml`**
    - Voraussetzungen: Schritt 9 abgeschlossen
    - Beschreibung: UI-Elemente hinzufügen (Label, AutoComplete/ComboBox, Buttons, optional Progress/Error-UI)

11. **Erstelle Unit-Tests für `RepositoryInitialisierungService`**
    - Voraussetzungen: Schritt 5 abgeschlossen; Test-Projekt und Mock-Infrastruktur vorhanden
    - Beschreibung: `RepositoryInitialisierungServiceTests` mit Testfällen analog zu `RepositoryStartskriptServiceTests` (siehe Tests-Abschnitt)

12. **Erweitere/erstelle Tests für `EntwicklungsprozessService`**
    - Voraussetzungen: Schritte 7, 11 abgeschlossen
    - Beschreibung: Tests für Initialisierungsskript-Integration nach Klon; Fehlertoleranz-Tests

13. **Erweitere Tests für `ProjectDetailViewModel`**
    - Voraussetzungen: Schritte 9, 11 abgeschlossen
    - Beschreibung: Tests für `LoadInitialisierungsskriptSuggestionsAsync()`, `SaveInitialisierungsskriptAsync()`, Property-Binding

14. **Erstelle E2E-Tests**
    - Voraussetzungen: Schritte 7–10 abgeschlossen; App läuft, Datenbank initialisiert
    - Beschreibung: E2E-Test für Happy Path: Projekt öffnen → Repository mit Initialisierungsskript konfigurieren → Aufgabe starten → Initialisierungsskript wird ausgeführt

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `RunAsync_ShouldSucceed_WhenInitializationScriptExecutes` | `RepositoryInitialisierungServiceTests` | Erfolgreiches Ausführen eines gültigen Initialisierungsskripts |
| `RunAsync_ShouldLogWarning_WhenInitializationScriptFails` | `RepositoryInitialisierungServiceTests` | Fehler werden geloggt (Warning), nicht als Exception geworfen |
| `RunAsync_ShouldThrow_WhenPathTraversalAttempted` | `RepositoryInitialisierungServiceTests` | Sicherheit: Path-Traversal mit `..` wird verhindert, Exception geworfen |
| `RunAsync_ShouldSkipExecution_WhenConfigurationIsInactive` | `RepositoryInitialisierungServiceTests` | Bei `Aktiv = false` wird Skript nicht ausgeführt (nur Logging) |
| `RunAsync_ShouldThrow_WhenScriptFileNotFound` | `RepositoryInitialisierungServiceTests` | Wenn Skriptdatei nicht existiert, wird Exception geworfen |
| `ResolveScriptPath_ShouldValidatePathBoundary` | `RepositoryInitialisierungServiceTests` | Hilfsmethode: Path-Validierung funktioniert korrekt |
| `CreateSut()` | `RepositoryInitialisierungServiceTests` | Hilfsmethode: Erstellt Service-Instance mit Mock `ICliRunner` |
| `CreateConfig()` | `RepositoryInitialisierungServiceTests` | Hilfsmethode: Erstellt Standard-`RepositoryInitialisierungKonfiguration` |
| `CreateScript(relativePath)` | `RepositoryInitialisierungServiceTests` | Hilfsmethode: Erstellt temporäre Test-Skriptdatei |
| `ProzessStartenAsync_ShouldExecuteInitializationScript_AfterClone` | `EntwicklungsprozessServiceTests` | Integration: Initskript wird nach Klon, vor KI-Start ausgeführt |
| `ProzessStartenAsync_ShouldNotBlockTask_WhenInitializationScriptFails` | `EntwicklungsprozessServiceTests` | Fehlertoleranz: Aufgabe läuft weiter, auch wenn Initskript fehlschlägt |
| `ProzessStartenAsync_ShouldExecuteInitializationThenStartScript_InOrder` | `EntwicklungsprozessServiceTests` | Reihenfolge: Initialisierungsskript vor Startskript (falls beide konfiguriert) |
| `LoadInitialisierungsskriptSuggestionsAsync_ShouldFetchFromRemote` | `ProjectDetailViewModelTests` | ViewModel: Remote-Repository wird abgefragt, Suggestions werden geladen |
| `LoadInitialisierungsskriptSuggestionsAsync_ShouldHandleNetworkError_Gracefully` | `ProjectDetailViewModelTests` | ViewModel: Fehler beim Remote-Zugriff werden gehandhabt, UI bleibt responsiv |
| `SaveInitialisierungsskriptAsync_ShouldPersist_SelectedScript` | `ProjectDetailViewModelTests` | ViewModel: Ausgewähltes Skript wird in Datenbank gespeichert |
| `SaveInitialisierungsskriptAsync_ShouldCreateConfiguration_IfNotExists` | `ProjectDetailViewModelTests` | ViewModel: `RepositoryInitialisierungKonfiguration` wird bei Bedarf angelegt |
| `E2E_ProjectDetailView_ConfigureInitializationScript_AndExecuteOnClone` | E2E Test (neue Klasse) | Happy Path: Benutzer konfiguriert Initialisierungsskript, Aufgabe wird gestartet, Skript wird ausgeführt |
| `E2E_ProjectDetailView_InitializationScript_FailureDoesNotBlockTask` | E2E Test (neue Klasse) | Fehlertoleranz E2E: Selbst wenn Initialisierungsskript fehlschlägt, wird Aufgabe nicht blockiert |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `EntwicklungsprozessServiceTests` | Kann betroffen sein, wenn Tests `FinalizeStartAsync()` mocken/prüfen; `RepositoryInitialisierungService` muss in Mocks konfiguriert sein |
| `ProjectDetailViewModelTests` | Kann betroffen sein, wenn Tests Properties oder Ladeverhalten prüfen; neue Properties müssen berücksichtigt werden |
| E2E Tests für Repository-Klon | Wenn bestehende E2E-Tests Klon-Prozess prüfen und Logging/Meldungen validieren, können diese durch zusätzliche Initialisierungs-Logs beeinträchtigt werden |

### E2E-Tests (Pflicht)

| Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium |
|----------|------------------------|-------------------------------|
| Happy Path: Initialisierungsskript wird konfiguriert und ausgeführt | `E2E_RepositoryInitialisierungTests.cs` oder neue Testklasse in `src/Softwareschmiede.Tests/E2E/` | Initialisierungsskript nach Klon automatisch ausgeführt; Ausgabe wird protokolliert |
| Fehlertoleranz: Initialisierungsskript-Fehler blockiert nicht die Aufgabe | Wie oben | Aufgabe wird trotz Initialisierungsskript-Fehler normal fortgesetzt; Fehler wird geloggt |
| UI: Projekt-DetailView zeigt Initialisierungsskript-Felder | `E2E_ProjectDetailViewTests.cs` (Erweiterung) oder neue E2E-Klasse | Projekt-DetailView hat Label, Eingabefeld und Speichern/Abbrechen-Buttons; UI reagiert auf Benutzer-Eingaben |

Welche bestehenden E2E-Tests müssen angepasst werden?

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `E2E_EntwicklungsprozessTests` (falls vorhanden) | Falls Tests Klon-Prozess prüfen und Logging-Ausgaben validieren, müssen neue Initialisierungs-Logs berücksichtigt werden; Logs-Validierung muss um Initialisierungs-Meldungen erweitert werden |

## Offene Punkte

Keine — die wichtigsten Designentscheidungen wurden in der Anforderung klar vorgegeben und folgen etablierten Mustern im Codebase.

**Hinweis zu technischen Details:**
- Remote-Datei-Zugriff (für Vorschlagslogik) hängt von konkreter Plugin-API ab; Details werden bei Schritt 9 (ViewModel-Erweiterung) geklärt.
- Genaue Platzierung von UI-Elementen im ProjectDetailView (Layout, Reiter, Section) wird bei Schritt 10 entschieden.
