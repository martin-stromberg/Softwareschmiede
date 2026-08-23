# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### EntwicklungsprozessService.cs (EntwicklungsprozessService)

- **Doppelter Code** — `RunInitialisierungsskriptAsync` (Zeile 583–604) und `RunStartskriptAsync` (Zeile 606–627) sind strukturell nahezu identisch: Null-Check auf Konfiguration/Service, `try`/`await RunAsync(...)`, `catch (OperationCanceledException) { throw; }`, `catch (Exception ex)` mit `LogWarning` und Rückgabe eines `"Hinweis: ..."`-Strings. Sie unterscheiden sich nur in der Konfigurations-/Service-Referenz und dem Label ("Initialisierungsskript" vs. "Startskript"). Diese Duplizierung ist im Rahmen dieses Branches entstanden (Aufteilung von `FinalizeStartAsync` aus der vorherigen Review-Iteration hat die beiden Methoden erst erzeugt).

  Empfehlung: Gemeinsame private Hilfsmethode extrahieren, die Label, den auszuführenden Skript-Aufruf (z. B. als `Func<Task>` oder generisch über die Konfiguration) und die Fehlerbehandlung/Logging-/Hinweis-Text-Erzeugung kapselt, z. B. `RunOptionalRepositoryScriptAsync(Guid aufgabeId, string scriptLabel, Func<Task> runAsync, CancellationToken ct)`. `RunInitialisierungsskriptAsync` und `RunStartskriptAsync` prüfen dann nur noch ihre jeweilige Null-Bedingung und delegieren den Rest an diese Hilfsmethode.

### ProjektService.cs (ProjektService)

- **Namenskonventionen und Einheitlichkeit** — In `SaveRepositoryInitialisierungskriptAsync` (Zeile 307–350) sowie in der zugehörigen privaten Validierungsmethode `ValidateInitialisierungsKonfiguration` (Zeile 526–527) heißt der Parameter durchgängig `initialisierungsskriptRelativePfad` (deutsches "Pfad"), während dieselbe fachliche Größe überall sonst im Feature als `InitialisierungsskriptRelativePath` (englisches "Path") benannt ist: die Entity-Property `RepositoryInitialisierungKonfiguration.InitialisierungsskriptRelativePath`, das DbContext-Mapping, die Migration und alle Testklassen. Das analoge Startskript-Feature verwendet konsequent `startScriptRelativePath`/`StartScriptRelativePath` (durchgehend "Path"). Diese Inkonsistenz existiert ausschließlich innerhalb des neuen Codes dieses Branches.

  Empfehlung: Parameter in `SaveRepositoryInitialisierungskriptAsync` und `ValidateInitialisierungsKonfiguration` von `initialisierungsskriptRelativePfad` zu `initialisierungsskriptRelativePath` umbenennen, damit die Benennung konsistent zur Entity-Property und zum Startskript-Pendant ist.

### ProjectDetailViewModel.cs (ProjectDetailViewModel)

- **Namenskonventionen und Einheitlichkeit** — Für dasselbe Konzept ("Liste vorgeschlagener Skriptpfade") wird innerhalb derselben Klasse uneinheitlich benannt: Die Property `InitialisierungsskriptSuggestionen` (Zeile 246) und das gebundene `LoadInitialisierungsskriptSuggestionenCommand` (Zeile 315, 356–358) verwenden die eingedeutschte Form "Suggestionen", während die zugehörige private Lademethode `LoadInitialisierungsskriptSuggestionsAsync` (Zeile 704) die englische Form "Suggestions" verwendet — beide Bezeichner beziehen sich auf exakt dasselbe Datum. Zusätzlich existiert im selben ViewModel bereits das rein deutsche Pendant `IssueVorschlaege`/`OffeneAnforderungen` für ein vergleichbares Konzept, wodurch drei verschiedene Schreibweisen ("Vorschläge", "Suggestionen", "Suggestions") für ähnliche Listen im selben Typ koexistieren.

  Empfehlung: Mindestens die private Methode `LoadInitialisierungsskriptSuggestionsAsync` in `LoadInitialisierungsskriptSuggestionenAsync` umbenennen, damit sie mit der Property `InitialisierungsskriptSuggestionen` und dem Command `LoadInitialisierungsskriptSuggestionenCommand` konsistent ist.

## Geprüfte Dateien

Liste aller geprüften Dateien:

- `src/Softwareschmiede/Application/Services/RepositoryInitialisierungService.cs`
- `src/Softwareschmiede/Application/Services/RepositoryScriptExecutor.cs`
- `src/Softwareschmiede/Application/Services/RepositoryStartskriptService.cs`
- `src/Softwareschmiede/Domain/Entities/RepositoryInitialisierungKonfiguration.cs`
- `src/Softwareschmiede/Domain/Entities/GitRepository.cs`
- `src/Softwareschmiede/Application/Services/EntwicklungsprozessService.cs`
- `src/Softwareschmiede/Application/Services/ProjektService.cs`
- `src/Softwareschmiede/Application/Services/AufgabeService.cs`
- `src/Softwareschmiede/Application/Services/DirectoryStructureBrowserService.cs`
- `src/Softwareschmiede/Infrastructure/Data/SoftwareschmiededDbContext.cs`
- `src/Softwareschmiede/Migrations/20260823091609_AddRepositoryInitialisierungKonfiguration.cs`
- `src/Softwareschmiede/Migrations/20260823091609_AddRepositoryInitialisierungKonfiguration.Designer.cs`
- `src/Softwareschmiede/Migrations/SoftwareschmiededDbContextModelSnapshot.cs`
- `src/Softwareschmiede.App/App.xaml.cs`
- `src/Softwareschmiede.App/ViewModels/ProjectDetailViewModel.cs`
- `src/Softwareschmiede.App/Views/ProjectDetailView.xaml`
- `src/Softwareschmiede.Tests/Application/Services/RepositoryInitialisierungServiceTests.cs`
- `src/Softwareschmiede.Tests/Application/Services/EntwicklungsprozessServiceTests_Initialisierungsskript.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/ProjectDetailViewModelTests_Initialisierungsskript.cs`
- `src/Softwareschmiede.Tests/E2E/E2E_RepositoryInitialisierungAusfuehrungTests.cs`
- `src/Softwareschmiede.Tests/E2E/E2E_RepositoryInitialisierungConfigTests.cs`

## Nicht Teil dieser Anforderung (Issue 228)

Folgende von `git diff --name-only --diff-filter=AM $(git merge-base HEAD main)` mitgelistete Dateien gehören zu bereits separat abgeschlossener Arbeit (Issue 221), die im lokalen `main` nur noch nicht gemerged ist, und wurden daher nicht geprüft:

- `src/Softwareschmiede/Application/Services/GitOrchestrationService.cs`
- `src/Softwareschmiede/Application/Services/GitWorkspaceBrowserService.cs`
- `src/Softwareschmiede/Application/Services/IGitWorkspaceBrowserService.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests.cs`
- `src/Softwareschmiede.Tests/Application/Services/GitOrchestrationServiceTests.cs`
- `src/Softwareschmiede.Tests/Application/Services/GitWorkspaceBrowserServiceTests.cs`
- `src/Softwareschmiede.Tests/Infrastructure/Terminal/PseudoConsoleSessionTests.cs`
