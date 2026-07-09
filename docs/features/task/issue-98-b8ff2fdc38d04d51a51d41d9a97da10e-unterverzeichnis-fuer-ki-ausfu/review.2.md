# Plan-Review

## Ergebnis

**Status:** Offene Aufgaben vorhanden

Iteration 2 des Plan-Reviews. Geprüft wurde die gesamte aktuelle Implementierung im Arbeitsverzeichnis
gegen den in dieser Iteration angepassten `plan.md`. Build unabhängig verifiziert:
`dotnet build Softwareschmiede.slnx` → **0 Fehler, 8 Warnungen** (ausschließlich vorbestehende
NU1903-Sicherheitswarnungen zu `SQLitePCLRaw`; die in `review-code.1.md` gemeldete CS1998-Warnung ist
verschwunden — der Fix ist wirksam).

Die drei Prüffragen dieser Iteration:

1. **Plan-Anpassung konsistent?** — Überwiegend ja, aber `plan.md` ist nach der Umformulierung von
   „Offener Punkt 1" nicht vollständig in sich konsistent (siehe Hinweis 1). Die Kernaussage
   (nur `LocalDirectoryPlugin` implementiert die Methode) deckt sich mit dem Code.
2. **Alle ursprünglichen Planelemente weiterhin umgesetzt?** — Ja. Die Iteration-2-Refactorings
   (`Task.Run`-Offload, TOCTOU-Robustheit, CS1998-Fix, `DirectoryStructureLoadHelper`-Extraktion) haben
   kein Planelement beschädigt; beide ViewModels nutzen jetzt die gemeinsame Hilfsklasse, alle
   Signaturen/Properties/Tests sind unverändert vorhanden.
3. **Implementierung (Iter. 1 + 2) vollständig und konsistent?** — Bis auf eine offene Aufgabe ja.
   Der zuvor gemeldete Plugin-Punkt ist durch die Plan-Umformulierung aufgelöst; neu bzw. weiterhin
   offen ist ausschließlich die produktive Verdrahtung von `ValidateWorkingDirectoryAfterCloneAsync`
   (siehe „Offene Aufgaben").

## Umgesetzte Planelemente

### Entity / Persistenz
- [x] Feld `WorkingDirectoryRelativePath` (`string?`) in `RepositoryStartKonfiguration` — vorhanden
- [x] DbContext-Konfiguration `WorkingDirectoryRelativePath`: `IsRequired(false)` + `HasMaxLength(512)` — vorhanden (`SoftwareschmiededDbContext.cs` Z. 101–103)
- [x] Migration `AddWorkingDirectoryToRepositoryStartKonfiguration` + Model-Snapshot — vorhanden

### Konfiguration
- [x] `DirectoryStructureOptions` mit `CacheDurationSeconds` (300), `MaxDepth` (2), `Enabled` (true) — vorhanden
- [x] `appsettings.json`-Abschnitt `DirectoryStructure` mit den drei Werten — vorhanden

### Services
- [x] `DirectoryStructureBrowserService` mit `GetDirectoriesAsync(...)`, `IMemoryCache`-TTL-Caching, Fehler-Fallback auf leere Liste, `Enabled`-Schalter — vorhanden
- [x] `KiAusfuehrungsService.StartCliAsync(...)` + `StartWithPseudoConsoleAsync(...)` um `RepositoryStartKonfiguration? startConfig = null` erweitert — vorhanden
- [x] `StartPseudoConsoleProcess()` verwendet effektives Arbeitsverzeichnis via `WorkingDirectoryResolver.DetermineEffectiveWorkingDirectory(...)` — vorhanden
- [x] `WorkingDirectoryResolver.ResolveEffectiveWorkingDirectory(...)` (Path-Traversal-Schutz inkl. Sibling-Präfix, `InvalidOperationException`) — vorhanden (siehe Hinweis 2 zur Auslagerung)
- [x] `WorkingDirectoryResolver.ValidateWorkingDirectory(...)` (`DirectoryNotFoundException`) — vorhanden
- [x] `GitOrchestrationService.ValidateWorkingDirectoryAfterCloneAsync(...)` mit Logging — Methode vorhanden und getestet (Aufruf-Verdrahtung siehe „Offene Aufgaben")
- [x] CLI-Verdrahtung: `EntwicklungsprozessService` lädt `startConfig` und übergibt ihn an `StartWithPseudoConsoleAsync` — vorhanden

### Plugin-API
- [x] `IGitPlugin.GetRepositoryStructureAsync(repositoryUrl, maxDepth, ct)` mit `NotSupportedException`-Default — vorhanden
- [x] `RepositoryDirectoryEntry` (record: `Path`, `IsDirectory`) — vorhanden
- [x] `LocalDirectoryPlugin.GetRepositoryStructureAsync(...)` — vorhanden; rekursiv bis `maxDepth`, `.git`-/Reparse-Point-Ausschluss, `Task.Run`-Offload (kein UI-Thread-Blocking), per-Verzeichnis-`try/catch` gegen `UnauthorizedAccessException`/`IOException` (Iteration-2-Robustheit)
- [x] GitHub-/BitBucket-Plugin behalten `NotSupportedException`-Default — **konform zum aktualisierten Plan** (Plan-„Offener Punkt 1" wurde umformuliert; jeweils mit erklärendem Kommentar im Code)

### Gemeinsame UI-Ladelogik (Iteration 2)
- [x] `DirectoryStructureLoadHelper.LoadWorkingDirectoriesAsync(...)` — extrahiert; von `RepositoryAssignViewModel` und `ArbeitsverzeichnisBearbeitenViewModel` genutzt (behebt Code-Review-Duplizierung)

### ViewModel / UI
- [x] `RepositoryAssignViewModel`: `AvailableWorkingDirectories`, `SelectedWorkingDirectory`, `IsLoadingDirectoryStructure`, `CurrentLoadDirectoryStructureTask` — vorhanden
- [x] `SelectedRepository`-Setter → `OnSelectedRepositoryChanged()`; `LoadDirectoryStructureAsync(ct)` befüllt mit `"."`-Root, Reset + Cancel bei Wechsel — vorhanden
- [x] Private Felder `_availableWorkingDirectories`, `_selectedWorkingDirectory`, `_isLoadingDirectoryStructure`, `_dirStructureCts`, `_directoryStructureService` — vorhanden
- [x] `RepositoryAssignDialog.xaml`: zusätzliche Grid-Row, Label, ComboBox (Binding + `IsEnabled` invertiert), Lade-Anzeige, Hinweis-Text — vorhanden

### Tests (Plan-gelistet — alle vorhanden, Build grün)
- [x] `KiAusfuehrungsServiceTests_WorkingDirectory` (Resolve/Validate/StartCli-Szenarien inkl. Sibling-Präfix)
- [x] `DirectoryStructureBrowserServiceTests` (Return/Cache-TTL/Fehler-Fallback/Plugin-Aufruf)
- [x] `RepositoryAssignViewModelTests_WorkingDirectory` (Load/Reset/Cancel/IsLoading/DotRoot/Default/NullRepo/Fehler)
- [x] `GitOrchestrationServiceTests` (`ValidateWorkingDirectoryAfterClone_ShouldThrowWhenDirectoryNotFound` / `_ShouldLogError`)
- [x] `LocalDirectoryPluginTests_GetRepositoryStructureAsync` (MaxDepth/`.git`-Ausschluss/NonExistent/EmptyUrl/EmptyDir/Cancelled)
- [x] E2E `E2E_WorkingDirectory` + `ProjektServiceTests` / `ProjectDetailViewModelTests_Arbeitsverzeichnis` (Zusatzfeature)

## Offene Aufgaben

- [ ] **`ValidateWorkingDirectoryAfterCloneAsync` ist nicht in den produktiven Klon-Ablauf verdrahtet.**
  Der Plan-Programmablauf „Verzeichnis-Validierung nach Git-Klon" (plan.md Z. 63–72) beschreibt
  ausdrücklich, dass diese Methode *nach einem erfolgreichen Git-Klon* aufgerufen wird. Die Methode
  existiert vollständig und ist getestet (`GitOrchestrationService.cs` Z. 254–277), besitzt aber
  **keine produktive Aufrufstelle** — eine repo-weite Suche findet nur die Definition und die beiden
  Test-Call-Sites. Der beschriebene, frühe/klarere Fehlerpfad direkt nach dem Klon ist damit nicht
  realisiert.

  *Teilweise umgesetzt / Mitigation:* Die Existenz- und Path-Traversal-Prüfung greift zur Laufzeit
  dennoch, weil `WorkingDirectoryResolver.DetermineEffectiveWorkingDirectory(...)` beim CLI-Start
  (`KiAusfuehrungsService`) dieselben Prüfungen ausführt und dieselbe `DirectoryNotFoundException` /
  `InvalidOperationException` wirft. Die Plan-Designentscheidung „Fehler mit `DirectoryNotFoundException`
  + Logging" ist damit funktional erfüllt — nur der *Zeitpunkt* (nach Klon vs. bei CLI-Start) weicht ab.

  *Empfohlene Auflösung (eine der beiden):*
  1. `ValidateWorkingDirectoryAfterCloneAsync(...)` nach dem erfolgreichen Klon im Orchestrierungs-/
     Klon-Ablauf aufrufen (entspricht dem Plan-Programmablauf, liefert früheres Fehlerbild) — oder
  2. Plan-Programmablauf bewusst anpassen: Validierung erfolgt am CLI-Start; die Post-Klon-Methode
     entfällt bzw. wird als reine Hilfs-/Test-API dokumentiert.

## Hinweise

1. **`plan.md`-interne Inkonsistenz nach der Umformulierung (dokumentationsseitig, kein Code-Gap).**
   Die neu gefasste „Offener Punkt 1"-Aussage (Z. 320–322: nur `LocalDirectoryPlugin` implementiert,
   Remote-Provider bleiben bei `NotSupportedException`) steht im Widerspruch zu zwei nicht
   mitgezogenen Passagen:
   - „Notizen zur Implementierung" (Z. 337): *„Git-Plugin Integration (Offener Punkt 1): … Dies
     betrifft alle Plugins (GitHub, Bitbucket, GitLab). Empfehlung: Klärung vor Start der
     Implementierung."* — impliziert weiterhin eine Implementierung für alle Plugins, nennt ein nicht
     existierendes GitLab-Plugin und enthält eine bereits obsolete „Klärung vor Start"-Empfehlung.
   - „Seiteneffekte und Risiken" (Z. 192): *„Mitigation: Offener Punkt, Klärung erforderlich."* — der
     Punkt ist inzwischen geklärt; Wortlaut ist veraltet.
   Empfehlung: diese beiden Passagen an die aktualisierte „Offener Punkt 1"-Fassung angleichen, damit
   der Plan in sich konsistent ist.

2. **Standort der Hilfsmethoden weicht bewusst vom Plan-Wortlaut ab (unkritisch, bereits in Iter. 1
   akzeptiert).** `ResolveEffectiveWorkingDirectory()` / `ValidateWorkingDirectory()` liegen nicht als
   Methoden von `KiAusfuehrungsService`, sondern in der statischen, gemeinsam genutzten Klasse
   `WorkingDirectoryResolver`. `KiAusfuehrungsService` (CLI-Start) und `GitOrchestrationService`
   (Post-Klon) nutzen sie darüber. Funktional äquivalent; vermeidet Service-zu-Service-Kopplung.

3. **`ValidateWorkingDirectoryAfterCloneAsync` weicht in der Signatur minimal vom Plan ab:** der im
   Plan gelistete `CancellationToken ct`-Parameter fehlt (Methode arbeitet synchron und gibt `Task`
   zurück). Unkritisch, da rein I/O-basierte Existenzprüfung.

4. **Funktionale Einschränkung für Remote-Provider (bewusst, plan-konform):** Für über GitHub/BitBucket
   zugewiesene Repositories ist ausschließlich das Root-Verzeichnis (`"."`) wählbar — eine echte
   Unterverzeichnis-Auswahl vor dem Klon ist nur mit `LocalDirectoryPlugin` verfügbar. Der
   `DirectoryStructureBrowserService` fängt die `NotSupportedException` ab und liefert eine leere Liste,
   sodass die UI korrekt auf den Root-Fallback zurückfällt (deckt sich mit Plan-„Offener Punkt 3").
   Dies ist keine Lücke gegenüber dem *aktualisierten* Plan, sollte aber in Feature-Doku/Release Notes
   festgehalten werden.
