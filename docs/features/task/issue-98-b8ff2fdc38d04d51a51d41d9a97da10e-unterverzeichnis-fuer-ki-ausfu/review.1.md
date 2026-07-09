# Plan-Review

## Ergebnis

**Status:** Offene Aufgaben vorhanden

Geprüft wurde die gesamte aktuelle Implementierung im Arbeitsverzeichnis gegen `plan.md` –
sowohl der ursprünglich committete Teil (`3f0bd9c`) als auch die aktuell uncommitteten Ergänzungen
(`LocalDirectoryPlugin.GetRepositoryStructureAsync` + Dialog „Arbeitsverzeichnis bearbeiten").
Build wurde verifiziert: `dotnet build Softwareschmiede.slnx` → **0 Fehler** (8 Warnungen, vorbestehend).
Alle im Plan gelisteten Testmethoden existieren im Testprojekt.

Die einzige Abweichung betrifft Plan-„Offener Punkt 1" (Plugin-Abdeckung von
`GetRepositoryStructureAsync`). Sie ist begründet und dokumentiert, weicht aber vom Plan-Wortlaut
(„alle Plugins implementieren sie") ab und hat eine reale funktionale Konsequenz – daher als offener
Punkt gemeldet, damit eine bewusste Produktentscheidung getroffen werden kann.

## Umgesetzte Planelemente

### Entity / Persistenz
- [x] Feld `WorkingDirectoryRelativePath` (`string?`) in `RepositoryStartKonfiguration` — vorhanden
- [x] DbContext-Konfiguration `WorkingDirectoryRelativePath`: `HasMaxLength(512)` + `IsRequired(false)` — vorhanden (`SoftwareschmiededDbContext.cs`)
- [x] Migration `AddWorkingDirectoryToRepositoryStartKonfiguration` — vorhanden (`20260708181234_...AddWorkingDirectoryToRepositoryStartKonfiguration.cs`)

### Konfiguration
- [x] `DirectoryStructureOptions` mit `CacheDurationSeconds` (300), `MaxDepth` (2), `Enabled` (true) — vorhanden
- [x] `appsettings.json`-Abschnitt `DirectoryStructure` mit den drei Werten — vorhanden

### Services
- [x] `DirectoryStructureBrowserService` (neue Klasse) mit `GetDirectoriesAsync(...)`, `IMemoryCache`-Caching (TTL), Fehler-Fallback auf leere Liste — vorhanden
- [x] `KiAusfuehrungsService.StartCliAsync(...)` um `RepositoryStartKonfiguration? startConfig = null` erweitert — vorhanden
- [x] `KiAusfuehrungsService.StartWithPseudoConsoleAsync(...)` um `startConfig`-Parameter erweitert — vorhanden
- [x] `StartPseudoConsoleProcess()` verwendet effektives Arbeitsverzeichnis (nicht blind `localRepoPath`) — vorhanden
- [x] Hilfsmethode `ResolveEffectiveWorkingDirectory(...)` (Path-Traversal-Schutz, `InvalidOperationException`) — vorhanden (siehe Hinweis 1: liegt in `WorkingDirectoryResolver`)
- [x] Hilfsmethode `ValidateWorkingDirectory(...)` (`DirectoryNotFoundException`) — vorhanden (siehe Hinweis 1)
- [x] `GitOrchestrationService.ValidateWorkingDirectoryAfterCloneAsync(...)` mit Logging — vorhanden (siehe Hinweis 2)
- [x] CLI-Verdrahtung: `EntwicklungsprozessService` lädt `startConfig` und übergibt ihn an `StartWithPseudoConsoleAsync` — vorhanden

### Plugin-API
- [x] `IGitPlugin` um `GetRepositoryStructureAsync(repositoryUrl, maxDepth, ct)` erweitert (Default: `NotSupportedException`) — vorhanden
- [x] `RepositoryDirectoryEntry` (ValueObject: `Path`, `IsDirectory`) — vorhanden
- [x] `LocalDirectoryPlugin.GetRepositoryStructureAsync(...)` implementiert (rekursiv bis `maxDepth`, Reparse-Point-/`.git`-Ausschluss) — vorhanden (uncommitted)
- [~] GitHub-/BitBucket-Plugin implementieren `GetRepositoryStructureAsync` — **NICHT umgesetzt** (bewusste, kommentierte Beibehaltung der `NotSupportedException`-Default-Implementierung; siehe „Offene Aufgaben")

### ViewModel / UI
- [x] `RepositoryAssignViewModel`: `AvailableWorkingDirectories`, `SelectedWorkingDirectory`, `IsLoadingDirectoryStructure`, `CurrentLoadDirectoryStructureTask` — vorhanden
- [x] `RepositoryAssignViewModel`: `SelectedRepository`-Setter ruft `OnSelectedRepositoryChanged()`; `LoadDirectoryStructureAsync(ct)` befüllt mit `"."`-Root, Reset + Cancel bei Wechsel — vorhanden
- [x] Private Felder `_availableWorkingDirectories`, `_selectedWorkingDirectory`, `_isLoadingDirectoryStructure`, `_dirStructureCts`, `_directoryStructureService` — vorhanden
- [x] `RepositoryAssignDialog.xaml`: zusätzliche Grid-Row, Label, ComboBox (Binding + IsEnabled invertiert), Lade-Anzeige, Hinweis-Text — vorhanden

### Tests (Plan-gelistet, alle vorhanden)
- [x] `KiAusfuehrungsServiceTests_WorkingDirectory`: `ResolveEffectiveWorkingDirectory_ShouldCombinePaths`, `_ShouldRejectPathTraversal`, `_ShouldAcceptDotAsRoot`, `ValidateWorkingDirectory_ShouldThrowWhenNotExists`, `_ShouldSucceedWhenExists`, `StartCliAsync_ShouldUseEffectiveWorkingDirectory`, `StartCliAsync_ShouldUseRepoRootWhenConfigNull`
- [x] `DirectoryStructureBrowserServiceTests`: `GetDirectoriesAsync_ShouldReturnDirectories`, `_ShouldCache_WithTTL`, `_ShouldHandleErrors_Gracefully`, `_ShouldCallPluginMethod`
- [x] `RepositoryAssignViewModelTests_WorkingDirectory`: `SelectedRepositoryChanged_ShouldLoadDirectoryStructure`, `_ShouldResetSelectedWorkingDirectory`, `_ShouldCancelPreviousLoad`, `LoadDirectoryStructureAsync_ShouldSetIsLoading_Flag`, `_ShouldPopulateDirectories_WithDotRoot`, `_ShouldSetDefaultSelectedDirectory`, `_ShouldHandleNullRepository`, `_ShouldHandleErrors_WithLogging`
- [x] `GitOrchestrationServiceTests`: `ValidateWorkingDirectoryAfterClone_ShouldThrowWhenDirectoryNotFound`, `_ShouldLogError`
- [x] `LocalDirectoryPluginTests_GetRepositoryStructureAsync` (uncommitted, über Plan hinaus): `_ShouldReturnDirectories_UpToMaxDepth`, `_ShouldExcludeGitDirectory`, `_ShouldReturnEmpty_ForNonExistentPath`, `_ShouldReturnEmpty_ForEmptyUrl`, `_ShouldThrow_WhenCancelledUpFront`, `_ShouldReturnEmpty_ForEmptyDirectory`
- [x] E2E-Tests `E2E_WorkingDirectory` sowie ProjektService-/ProjectDetailViewModel-Tests — vorhanden

### Zusätzlich implementiert (nicht in `plan.md`, aus `continue.md`-Kundenrückmeldung)
- [x] `ArbeitsverzeichnisBearbeitenViewModel` + `ArbeitsverzeichnisBearbeitenDialog(.xaml/.cs)` — nachträgliche Bearbeitung des Arbeitsverzeichnisses
- [x] `ProjectDetailViewModel.ArbeitsverzeichnisBearbeitenCommand` + Ribbon-Button; `IDialogService`/`WpfDialogService`-Erweiterung; DI-Registrierung in `App.xaml.cs`
- [x] `ProjektService.SaveRepositoryWorkingDirectoryAsync(...)` (persistiert, `"."`/leer → `null`)
- [x] Zugehörige Tests `ArbeitsverzeichnisBearbeitenViewModelTests`, `ProjectDetailViewModelTests_Arbeitsverzeichnis`

## Offene Aufgaben

- [ ] `GetRepositoryStructureAsync()` in Remote-SCM-Plugins (GitHub, BitBucket) — **bewusst nicht umgesetzt.**
  Konkrete Lücke gegenüber Plan-„Offener Punkt 1" (Wortlaut: „alle Plugins implementieren sie"): Nur
  `LocalDirectoryPlugin` implementiert die Methode. `GitHubPlugin` und `BitbucketPlugin` behalten die
  `NotSupportedException`-Default-Implementierung (jeweils mit erklärendem Kommentar: `GetAvailableRepositoriesAsync`
  liefert nur Remote-URLs ohne garantierten lokalen Klon-Pfad).

  **Bewertung:** Es handelt sich um eine *begründete und dokumentierte* Abweichung, kein Implementierungsfehler.
  Für Remote-Provider ist ein Struktur-Abruf ohne vorherigen Klon bzw. ohne einen eigenen API-Tree-Walk technisch
  nicht möglich; beides ist laut Plan („Seiteneffekte und Risiken" – kritischer Pfad) nicht Teil des MVP. Der
  `DirectoryStructureBrowserService` fängt die `NotSupportedException` ab und liefert eine leere Liste, sodass die
  UI korrekt auf den Root-Fallback (`"."`) zurückfällt – das deckt sich mit Plan-„Offener Punkt 3" (nur ComboBox
  mit Root-Fallback, keine manuelle Texteingabe).

  **Funktionale Konsequenz, die eine Entscheidung erfordert:** Für über GitHub/BitBucket zugewiesene Repositories
  ist damit ausschließlich das Root-Verzeichnis (`"."`) wählbar – eine echte Unterverzeichnis-Auswahl vor dem Klon
  ist für Remote-Provider nicht nutzbar. Das Feature ist derzeit nur für `LocalDirectoryPlugin` vollständig
  funktionsfähig.

  **Empfohlene Auflösung (eine der beiden):**
  1. Plan-Wortlaut anpassen/akzeptieren („LocalDirectory implementiert; Remote-Provider fallen bewusst auf Root
     zurück") und diese Einschränkung in der Feature-Doku/Release Notes festhalten – oder
  2. `GetRepositoryStructureAsync` für GitHub/BitBucket über die jeweilige Provider-API (Git-Tree-Endpunkt)
     nachrüsten, damit die Unterverzeichnis-Auswahl auch für Remote-Repositories funktioniert.

## Hinweise

1. **Standort der Hilfsmethoden weicht ab (unkritisch):** `ResolveEffectiveWorkingDirectory()` und
   `ValidateWorkingDirectory()` wurden nicht als Methoden von `KiAusfuehrungsService` (so der Plan-Wortlaut),
   sondern in eine neue, statische, gemeinsam genutzte Klasse `WorkingDirectoryResolver` ausgelagert.
   `KiAusfuehrungsService` und `GitOrchestrationService` nutzen sie über
   `WorkingDirectoryResolver.DetermineEffectiveWorkingDirectory(...)`. Funktional vollständig äquivalent;
   die Auslagerung vermeidet einen Service-zu-Service-Zugriff und ist die sauberere Lösung. Kein offener Punkt.

2. **`ValidateWorkingDirectoryAfterCloneAsync`:** (a) Signatur weicht minimal vom Plan ab – der geplante
   `CancellationToken ct`-Parameter fehlt (Methode arbeitet synchron und gibt `Task` zurück). (b) Die Methode
   existiert und ist getestet, wird aktuell aber an keiner produktiven Stelle nach dem Klon aufgerufen
   (kein Call-Site außerhalb der Tests gefunden). Die Existenz-/Traversal-Validierung greift zur Laufzeit dennoch,
   weil `WorkingDirectoryResolver.DetermineEffectiveWorkingDirectory(...)` beim CLI-Start dieselben Prüfungen
   ausführt. Empfehlung: bei Gelegenheit prüfen, ob die explizite Post-Klon-Validierung (früheres, klareres
   Fehlerbild) noch in den Klon-Ablauf eingebunden werden soll.

3. Der `DirectoryStructureBrowserService` ist im ViewModel als optionale (nullable) Abhängigkeit injiziert;
   Verhalten ohne Service = Collection leeren bzw. Root-Fallback. Entspricht dem Plan.
