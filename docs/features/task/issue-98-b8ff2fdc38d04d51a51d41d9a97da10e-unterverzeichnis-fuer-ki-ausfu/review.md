# Plan-Review

## Ergebnis

**Status:** Vollständig umgesetzt

Iteration 3 (letzte erlaubte Iteration) des Plan-Reviews. Geprüft wurde die gesamte aktuelle
Implementierung im Arbeitsverzeichnis unabhängig gegen den aktuellen `plan.md` — nicht nur der in
`review.2.md` offen gebliebene Punkt, sondern auch stichprobenartig bereits als erledigt markierte
Elemente aus Iteration 1/2, da sich der Code seither geändert haben könnte.

Build unabhängig verifiziert: `dotnet build Softwareschmiede.slnx` → **0 Fehler, 8 Warnungen**
(ausschließlich vorbestehende NU1903-Sicherheitswarnungen zu `SQLitePCLRaw`, unverändert gegenüber
Iteration 2). Die neuen/betroffenen Testklassen wurden unabhängig ausgeführt:
`dotnet test --filter "FullyQualifiedName~EntwicklungsprozessServiceTests_WorkingDirectoryValidation|...
GitOrchestrationServiceTests|...KiAusfuehrungsServiceTests_WorkingDirectory|...
DirectoryStructureBrowserServiceTests|...RepositoryAssignViewModelTests_WorkingDirectory|...
LocalDirectoryPluginTests_GetRepositoryStructureAsync|...ArbeitsverzeichnisBearbeitenViewModelTests|...
ProjectDetailViewModelTests_Arbeitsverzeichnis"` → **70 Tests, 0 Fehler**.

Der einzige in Iteration 2 offen gebliebene Punkt — fehlende produktive Aufrufstelle von
`GitOrchestrationService.ValidateWorkingDirectoryAfterCloneAsync(...)` — ist jetzt geschlossen: die
Methode wird in `EntwicklungsprozessService.ProzessStartenAsync(...)` direkt nach
`PrepareCloneDirectoryAsync(...)` und vor der Branch-Erstellung aufgerufen, sofern
`EntwicklungsprozessServiceOptions.GitOrchestrationService` gesetzt ist. Die Abhängigkeit wird in
`App.xaml.cs` per DI registriert und injiziert. Drei neue, unabhängig verifizierte Tests in
`EntwicklungsprozessServiceTests_WorkingDirectoryValidation.cs` decken den Erfolgsfall, den
Fehlerfall (Abbruch vor Branch-Erstellung) und die Rückwärtskompatibilität (kein
`GitOrchestrationService` konfiguriert) ab.

Die in `review.2.md` (Hinweis 1) gemeldeten plan-internen Inkonsistenzen in „Seiteneffekte und
Risiken" (Zeile ~200) und „Notizen zur Implementierung" (Zeile ~345) sind bereinigt: beide Passagen
verweisen jetzt konsistent auf die aktualisierte „Offener Punkt 1"-Fassung (nur `LocalDirectoryPlugin`
implementiert `GetRepositoryStructureAsync()`; kein GitLab-Plugin im Repository vorhanden; „Kein
offenes Risiko mehr" statt „Klärung erforderlich").

## Umgesetzte Planelemente

### Entity / Persistenz
- [x] Feld `WorkingDirectoryRelativePath` (`string?`) in `RepositoryStartKonfiguration` — vorhanden
- [x] DbContext-Konfiguration `WorkingDirectoryRelativePath`: `HasMaxLength(512)` — vorhanden (`SoftwareschmiededDbContext.cs` Z. 101)
- [x] Migration `AddWorkingDirectoryToRepositoryStartKonfiguration` + Model-Snapshot — vorhanden

### Konfiguration
- [x] `DirectoryStructureOptions` mit `CacheDurationSeconds` (300), `MaxDepth` (2), `Enabled` (true) — vorhanden, per `IOptions<>` registriert (`App.xaml.cs`)
- [x] `appsettings.json`-Abschnitt `DirectoryStructure` mit den drei Werten — vorhanden

### Services
- [x] `DirectoryStructureBrowserService` mit `GetDirectoriesAsync(...)`, `IMemoryCache`-TTL-Caching, Fehler-Fallback auf leere Liste, `Enabled`-Schalter — vorhanden, als Singleton registriert
- [x] `KiAusfuehrungsService.StartCliAsync(...)` + `StartWithPseudoConsoleAsync(...)` um `RepositoryStartKonfiguration? startConfig = null` erweitert — vorhanden
- [x] `StartPseudoConsoleProcess()` verwendet effektives Arbeitsverzeichnis via `WorkingDirectoryResolver` — vorhanden
- [x] `WorkingDirectoryResolver.ResolveEffectiveWorkingDirectory(...)` (Path-Traversal-Schutz inkl. Sibling-Präfix, `InvalidOperationException`) — vorhanden
- [x] `WorkingDirectoryResolver.ValidateWorkingDirectory(...)` (`DirectoryNotFoundException`) — vorhanden
- [x] `GitOrchestrationService.ValidateWorkingDirectoryAfterCloneAsync(clonePath, startConfig)` mit Logging — vorhanden (`GitOrchestrationService.cs` Z. 254–277), getestet
- [x] **Neu in Iteration 3:** `EntwicklungsprozessServiceOptions.GitOrchestrationService` (optionale Abhängigkeit) — vorhanden (`EntwicklungsprozessService.cs` Z. 18–22)
- [x] **Neu in Iteration 3:** `EntwicklungsprozessService.ProzessStartenAsync(...)` ruft `_options.GitOrchestrationService?.ValidateWorkingDirectoryAfterCloneAsync(lokalerKlonPfad, repository.StartKonfiguration)` nach dem Klon und vor der Branch-Erstellung auf — vorhanden (`EntwicklungsprozessService.cs` Z. 99–102), verifiziert durch `EntwicklungsprozessServiceTests_WorkingDirectoryValidation.ProzessStartenAsync_ShouldThrowDirectoryNotFoundImmediatelyAfterClone_WhenConfiguredWorkingDirectoryMissing` (prüft zusätzlich, dass `CreateBranchAsync` dabei nie aufgerufen wird)
- [x] **Neu in Iteration 3:** `GitOrchestrationService` in `App.xaml.cs` als `AddScoped<GitOrchestrationService>()` registriert und in `EntwicklungsprozessServiceOptions` injiziert — vorhanden (`App.xaml.cs` Z. 163, 174)
- [x] CLI-Verdrahtung: `EntwicklungsprozessService` lädt `startConfig` und übergibt ihn an `StartWithPseudoConsoleAsync` — vorhanden

### Plugin-API
- [x] `IGitPlugin.GetRepositoryStructureAsync(repositoryUrl, maxDepth, ct)` mit `NotSupportedException`-Default — vorhanden
- [x] `RepositoryDirectoryEntry` (record: `Path`, `IsDirectory`) — vorhanden
- [x] `LocalDirectoryPlugin.GetRepositoryStructureAsync(...)` — vorhanden; rekursiv bis `maxDepth`, `.git`-/Reparse-Point-Ausschluss, `Task.Run`-Offload
- [x] GitHub-/BitBucket-Plugin behalten `NotSupportedException`-Default mit erklärendem Kommentar — konform zum aktualisierten Plan

### Gemeinsame UI-Ladelogik
- [x] `DirectoryStructureLoadHelper.LoadWorkingDirectoriesAsync(...)` — von `RepositoryAssignViewModel` und `ArbeitsverzeichnisBearbeitenViewModel` genutzt

### ViewModel / UI
- [x] `RepositoryAssignViewModel`: `AvailableWorkingDirectories`, `SelectedWorkingDirectory`, `IsLoadingDirectoryStructure`, `CurrentLoadDirectoryStructureTask` — vorhanden
- [x] `SelectedRepository`-Setter → `OnSelectedRepositoryChanged()`; `LoadDirectoryStructureAsync(ct)` befüllt mit `"."`-Root, Reset + Cancel bei Wechsel — vorhanden
- [x] Private Felder `_availableWorkingDirectories`, `_selectedWorkingDirectory`, `_isLoadingDirectoryStructure`, `_dirStructureCts`, `_directoryStructureService` — vorhanden
- [x] `RepositoryAssignDialog.xaml`: zusätzliche Grid-Row, Label, ComboBox (Binding + `IsEnabled` invertiert), Lade-Anzeige, Hinweis-Text — vorhanden

### Plan-Konsistenz (Dokumentation)
- [x] „Seiteneffekte und Risiken" (Z. ~200) an aktualisierte „Offener Punkt 1"-Fassung angeglichen — vorhanden, kein Widerspruch mehr
- [x] „Notizen zur Implementierung" (Z. ~345) an aktualisierte „Offener Punkt 1"-Fassung angeglichen (kein GitLab-Plugin mehr erwähnt) — vorhanden, kein Widerspruch mehr

### Tests (Plan-gelistet — alle vorhanden, Build grün, unabhängig ausgeführt)
- [x] `KiAusfuehrungsServiceTests_WorkingDirectory` (Resolve/Validate/StartCli-Szenarien inkl. Sibling-Präfix)
- [x] `DirectoryStructureBrowserServiceTests` (Return/Cache-TTL/Fehler-Fallback/Plugin-Aufruf)
- [x] `RepositoryAssignViewModelTests_WorkingDirectory` (Load/Reset/Cancel/IsLoading/DotRoot/Default/NullRepo/Fehler)
- [x] `GitOrchestrationServiceTests` (`ValidateWorkingDirectoryAfterClone_ShouldThrowWhenDirectoryNotFound` / `_ShouldLogError`)
- [x] `LocalDirectoryPluginTests_GetRepositoryStructureAsync` (MaxDepth/`.git`-Ausschluss/NonExistent/EmptyUrl/EmptyDir/Cancelled)
- [x] E2E `E2E_WorkingDirectory` + `ProjektServiceTests` / `ProjectDetailViewModelTests_Arbeitsverzeichnis` (Zusatzfeature)
- [x] **Neu:** `EntwicklungsprozessServiceTests_WorkingDirectoryValidation` (produktive Verdrahtung: Erfolgsfall, Fehlerfall vor Branch-Erstellung, Rückwärtskompatibilität ohne `GitOrchestrationService`)

## Offene Aufgaben

Keine.

## Hinweise

1. **Standort der Hilfsmethoden weicht bewusst vom Plan-Wortlaut ab (unkritisch, bereits in Iter. 1/2
   akzeptiert).** `ResolveEffectiveWorkingDirectory()` / `ValidateWorkingDirectory()` liegen nicht als
   Methoden von `KiAusfuehrungsService`, sondern in der statischen, gemeinsam genutzten Klasse
   `WorkingDirectoryResolver`. Funktional äquivalent; vermeidet Service-zu-Service-Kopplung.

2. **`ValidateWorkingDirectoryAfterCloneAsync` weicht in der Signatur minimal vom Plan ab:** der im
   Plan gelistete `CancellationToken ct`-Parameter fehlt (Methode arbeitet synchron und gibt `Task`
   zurück). Unkritisch, da rein I/O-basierte Existenzprüfung; unverändert gegenüber Iteration 2.

3. **Funktionale Einschränkung für Remote-Provider (bewusst, plan-konform):** Für über GitHub/BitBucket
   zugewiesene Repositories ist ausschließlich das Root-Verzeichnis (`"."`) wählbar. Keine Lücke
   gegenüber dem Plan.

4. **E2E-UI-Automatisierungstests:** Laut `test-results.md` scheitern 15 FlaUI-basierte E2E-Tests
   (u. a. `E2E_WorkingDirectory.*`) sowie 2 Clipboard-Tests umgebungsbedingt (keine interaktive
   Desktop-Session in der Sandbox). Diese wurden in dieser Iteration nicht erneut ausgeführt, da sie
   eine interaktive Desktop-Session voraussetzen, die in dieser Umgebung nicht verfügbar ist; die
   zugehörigen Unit-/Integrationstests für dasselbe Verhalten wurden unabhängig ausgeführt und sind
   grün. Kein Plan-Gap, sondern eine bekannte Umgebungseinschränkung.
