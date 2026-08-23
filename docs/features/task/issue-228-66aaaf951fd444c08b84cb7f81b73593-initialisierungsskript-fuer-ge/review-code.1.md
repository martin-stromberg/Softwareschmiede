# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### RepositoryInitialisierungService.cs (RepositoryInitialisierungService)

- **Doppelter Code** — Die gesamte Klasse (`src/Softwareschmiede/Application/Services/RepositoryInitialisierungService.cs`) ist eine nahezu identische Kopie von `RepositoryStartskriptService.cs`: identische Konstante `PowershellExecutable`, identische `ResolveScriptPath`-Methode (Pfad-Traversal-Prüfung), identische `BuildArguments`-Methode und identischer Ablauf in `RunAsync` (Aktiv-Prüfung, Existenzprüfung, CLI-Aufruf, Fehlerbehandlung). Es unterscheiden sich nur die Konfigurationstyp-Namen und ein paar Textbausteine in den Fehlermeldungen ("Startskript" vs. "Initialisierungsskript").

  Empfehlung: Gemeinsame Logik in eine geteilte Basis extrahieren, z. B. eine interne statische Hilfsklasse `RepositoryScriptExecutor` mit einer Methode `RunAsync(string repositoryRootPath, bool aktiv, string relativePath, string scriptLabel, ICliRunner cliRunner, ILogger logger, CancellationToken ct)`, die `ResolveScriptPath`, `BuildArguments` und den CLI-Aufruf kapselt. `RepositoryInitialisierungService` und `RepositoryStartskriptService` rufen diese Hilfsklasse dann nur noch mit ihren jeweiligen Konfigurationswerten auf. Alternativ: gemeinsame abstrakte Basisklasse mit Template-Method für die konfigurationsspezifischen Werte (relativer Pfad, Aktiv-Flag, Label für Fehlermeldungen).

### ProjektService.cs (ProjektService)

- **Doppelter Code** — `ValidateInitialisierungsKonfiguration(string initialisierungsskriptRelativePfad)` (Zeile 536–547) ist eine Kopie von `ValidateStartConfiguration(string startScriptRelativePath)` (Zeile 523–534): identische Prüfungen (`IsNullOrWhiteSpace`, `Path.IsPathRooted`), nur die Fehlermeldungstexte unterscheiden sich.

  Empfehlung: Eine gemeinsame private Methode `ValidateRelativeScriptPath(string relativePath, string scriptLabel)` extrahieren, die den Labeltext ("Startskript"/"Initialisierungsskript") als Parameter erhält und von beiden Aufrufstellen verwendet wird.

### EntwicklungsprozessService.cs (EntwicklungsprozessService)

- **Doppelter Code / God-Methode** — In `FinalizeStartAsync` (jetzt ca. 65 Zeilen) wurde der neue Block zur Ausführung des Initialisierungsskripts (Zeilen 560–577) nahezu identisch zum bestehenden Block für das Startskript (Zeilen 579–596) direkt darunter eingefügt: gleiche Struktur aus `if (Konfiguration is not null && Service is not null)`, `try/catch (OperationCanceledException) { throw; }`, `catch (Exception ex)` mit Hinweistext-Erzeugung und `LogWarning`. Ebenso wird die anschließende Protokollnachricht für beide Hinweise (Zeilen 606–613) mit demselben Muster zusammengesetzt. Die Methode übernimmt dadurch jetzt drei klar trennbare Aufgaben hintereinander (Initialisierungsskript ausführen, Startskript ausführen, Protokollnachricht zusammenbauen) und überschreitet die Richtgröße von ~50 Zeilen für eine Methode.

  Empfehlung: Einen privaten Helper wie `Task<string?> RunOptionalScriptAsync<TConfig>(TConfig? konfiguration, Func<TConfig, CancellationToken, Task>? runAsync, string lokalerKlonPfad, Guid aufgabeId, string skriptLabel, CancellationToken ct)` extrahieren (oder einfacher: zwei kleine private Methoden `RunInitialisierungsskriptAsync` und `RunStartskriptAsync`, die jeweils try/catch kapseln und den Hinweistext zurückgeben), und `FinalizeStartAsync` auf die Orchestrierung (Aufrufreihenfolge + Zusammenbau der Protokollnachricht) reduzieren.

### ProjectDetailViewModel.cs (ProjectDetailViewModel)

- **Doppelter Code / fehlende Wiederverwendung** — `LoadInitialisierungsskriptSuggestionsAsync` (Zeile 701 ff.) ruft `gitPlugin.GetRepositoryStructureLoadResultAsync(...)` direkt auf und dupliziert dabei Fehlerbehandlung (Status-Prüfung `result.Status != RepositoryStructureLoadStatus.Success`, `catch (OperationCanceledException) { throw; }`, `catch (Exception ex) { ...LogError...; InitialisierungsskriptLoadingFailed = true; }`), die im bereits vorhandenen `DirectoryStructureBrowserService` (`src/Softwareschmiede/Application/Services/DirectoryStructureBrowserService.cs`, per DI als Singleton registriert in `App.xaml.cs` Zeile 167) zentral gekapselt ist — inklusive TTL-Caching der Ergebnisse (`_options.CacheDurationSeconds`), das die neue Methode dadurch nicht erhält (jeder Klick auf "Laden" löst einen neuen Remote-Aufruf aus).

  Empfehlung: `DirectoryStructureBrowserService` per Konstruktor in `ProjectDetailViewModel` injizieren und dessen `GetDirectoryLoadResultAsync`-Methode (bzw. eine dort neu zu ergänzende analoge `GetFilesAsync`/`GetFileLoadResultAsync`-Methode für Datei- statt Verzeichniseinträge) verwenden, statt `gitPlugin.GetRepositoryStructureLoadResultAsync` direkt aufzurufen. Damit profitiert die Initialisierungsskript-Auswahl von Caching, zentraler Fehlerbehandlung und der `DirectoryStructureOptions.Enabled`-Schalterlogik, die für die Verzeichnisauswahl bereits gilt.

### RepositoryInitialisierungKonfiguration.cs (RepositoryInitialisierungKonfiguration)

- **Namenskonvention** — Die neue Property `InitialisierungsskriptRelativePfad` (`src/Softwareschmiede/Domain/Entities/RepositoryInitialisierungKonfiguration.cs`, Zeile 13) weicht vom etablierten Namensmuster der Schwesterklasse `RepositoryStartKonfiguration` ab, die für dasselbe Konzept `StartScriptRelativePath` und `WorkingDirectoryRelativePath` verwendet (englisches "Path"-Suffix statt deutschem "Pfad"). Dieselbe Inkonsistenz zieht sich konsistent durch alle neuen Stellen (Entity, DB-Spalte, Migration, `ProjektService.SaveRepositoryInitialisierungskriptAsync`-Parameter, `RepositoryInitialisierungService`), betrifft also nicht die interne Konsistenz des neuen Codes, sondern die Konsistenz mit dem bestehenden, direkt benachbarten Namensmuster im selben fachlichen Bereich (Repository-Konfiguration).

  Empfehlung: Für neue Properties in diesem Bereich das etablierte `XxxRelativePath`-Muster (Englisch) statt `XxxRelativePfad` (Deutsch) verwenden, um Konsistenz mit `RepositoryStartKonfiguration` herzustellen. Da die Property bereits in Migration, DB-Schema und Tests verankert ist, ist eine Umbenennung jetzt mit Aufwand verbunden (neue Migration erforderlich) — als Hinweis für zukünftige, gleichartige Konfigurationsklassen festhalten, falls eine Umbenennung in diesem Zug nicht mehr sinnvoll ist.

## Hinweis zum Review-Umfang

Der Befehl `git diff --name-only --diff-filter=AM $(git merge-base HEAD main)` liefert zusätzlich folgende Dateien, deren Änderungen jedoch bereits vor Beginn der Arbeit an Issue #228 committet waren (Teil der bereits separat abgeschlossenen Arbeit an Issue #221 "Commit-Liste des PR reduzieren", die im lokalen `main`-Branch dieses Sandbox-Checkouts noch nicht nachgezogen ist) und daher nicht Gegenstand dieses Reviews sind:

- `src/Softwareschmiede/Application/Services/GitOrchestrationService.cs`
- `src/Softwareschmiede/Application/Services/GitWorkspaceBrowserService.cs`
- `src/Softwareschmiede/Application/Services/IGitWorkspaceBrowserService.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests.cs`
- `src/Softwareschmiede.Tests/Application/Services/GitOrchestrationServiceTests.cs`
- `src/Softwareschmiede.Tests/Application/Services/GitWorkspaceBrowserServiceTests.cs`
- `src/Softwareschmiede.Tests/Infrastructure/Terminal/PseudoConsoleSessionTests.cs`

Geprüft wurden stattdessen die tatsächlich für Issue #228 (Initialisierungsskript für geklonte Repositories) neuen bzw. im Arbeitsverzeichnis geänderten Dateien (siehe unten).

## Geprüfte Dateien

- `src/Softwareschmiede/Application/Services/RepositoryInitialisierungService.cs`
- `src/Softwareschmiede/Domain/Entities/RepositoryInitialisierungKonfiguration.cs`
- `src/Softwareschmiede/Domain/Entities/GitRepository.cs`
- `src/Softwareschmiede/Application/Services/ProjektService.cs`
- `src/Softwareschmiede/Application/Services/AufgabeService.cs`
- `src/Softwareschmiede/Application/Services/EntwicklungsprozessService.cs`
- `src/Softwareschmiede/Infrastructure/Data/SoftwareschmiededDbContext.cs`
- `src/Softwareschmiede/Migrations/SoftwareschmiededDbContextModelSnapshot.cs`
- `src/Softwareschmiede/Migrations/20260823091609_AddRepositoryInitialisierungKonfiguration.cs`
- `src/Softwareschmiede/Migrations/20260823091609_AddRepositoryInitialisierungKonfiguration.Designer.cs`
- `src/Softwareschmiede.App/App.xaml.cs`
- `src/Softwareschmiede.App/ViewModels/ProjectDetailViewModel.cs`
- `src/Softwareschmiede.App/Views/ProjectDetailView.xaml`
- `src/Softwareschmiede.Tests/App/ViewModels/ProjectDetailViewModelTests_Initialisierungsskript.cs`
- `src/Softwareschmiede.Tests/Application/Services/EntwicklungsprozessServiceTests_Initialisierungsskript.cs`
- `src/Softwareschmiede.Tests/Application/Services/RepositoryInitialisierungServiceTests.cs`
- `src/Softwareschmiede.Tests/E2E/E2E_RepositoryInitialisierungAusfuehrungTests.cs`
- `src/Softwareschmiede.Tests/E2E/E2E_RepositoryInitialisierungConfigTests.cs`
