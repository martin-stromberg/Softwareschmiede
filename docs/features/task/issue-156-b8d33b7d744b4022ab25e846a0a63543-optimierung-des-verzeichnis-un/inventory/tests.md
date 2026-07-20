# Tests

## Testklassen für FileExplorerViewModel

### `FileExplorerViewModelTests`
Datei: `src/Softwareschmiede.Tests/App/ViewModels/FileExplorerViewModelTests.cs`

Unit-Tests für `FileExplorerViewModel`.

**Bestehende Testmethoden:**
- `Standard_LaedtWurzelknotenUeberWorkingTree` — InitialisierenAsync lädt Arbeitsbaum über Service
- `DateiAuswahl_Standard_SetztDateiInhaltAusPreview` — Auswahl eines Datei-Knotens lädt Vorschau
- `DateiAuswahl_SchnellerWechsel_BrichtTokenDesVorherigenLadevorgangsAb` — Schnelle Auswahl-Wechsel brechen vorherige Ladevorgänge ab (wichtig für Cancellation)
- `InitialisierenAsync_BrichtLaufendenDateiLadevorgangAb` — Initialisierung bricht laufende Ladevorgänge ab

**Hilfsmethode:**
- `CreateSut()` — Creates System Under Test (Mock für `IGitWorkspaceBrowserService`, Logger, Text-Diff-Service)
- `WaitForAsync(Func<bool>)` — Async-Wait-Helper für asynchrone Zustandsänderungen

**Fehlende Tests für Lazy-Loading:**
- Tests für `LadeKinderAsync` beim Aufklappen
- Tests für Depth-Tracking
- Tests für ChildrenLoaded-Flag
- Tests für Cleanup bei Collapse (wenn implementiert)
- Tests für Fehlerbehandlung beim Lazy-Load

### Verwandte Testklassen

- `FileExplorerViewModelTests_DiffNavigation` — Tests für Diff-Navigation (Nächste/Vorherige Änderung)
- `FileExplorerViewModelTests_DateiOeffnen` — Tests für "Mit Standardanwendung öffnen"

## Testklassen für GitWorkspaceBrowserService

### `GitWorkspaceBrowserServiceWorkingTreeTests`
Datei: `src/Softwareschmiede.Tests/Application/Services/GitWorkspaceBrowserServiceWorkingTreeTests.cs`

Unit-Tests für `GitWorkspaceBrowserService.LoadWorkingTreeAsync` (Standardmodus-Baumaufzählung).

**Bestehende Testmethoden:**
- `LoadWorkingTreeAsync_ListetDateienUndVerzeichnisse` — Listet vollständig alle Dateien und verschachtelte Verzeichnisse auf (testet **unbegrenzte Rekursion**)
- `LoadWorkingTreeAsync_SchliesstGitVerzeichnisAus` — `.git`-Verzeichnis wird korrekt ausgeschlossen
- `LoadWorkingTreeAsync_NichtExistierenderPfad_LiefertLeereListe` — Nicht existierende Pfade liefern leere Liste ohne Exception

**Hilfsmethode:**
- `CreateService()` — Erstellt Service mit Mock-CliRunner und NullLogger
- `CreateTempDirectory()` — Erstellt temporäres Test-Verzeichnis

**Fehlende Tests:**
- Tests für `maxInitialDepth`-Parameter in `LoadWorkingTreeAsync`
- Tests für `LoadSubtreeAsync` — neue Methode
- Tests für Depth-Tracking auf neuen Knoten
- Tests für ChildrenLoaded-Flag bei verschiedenen Tiefen
- Tests für Tiefe-Limitierung (nur zwei Ebenen initial laden)
- Tests für Performance mit großen Verzeichnisbäumen

### `GitWorkspaceBrowserServiceTests`
Datei: `src/Softwareschmiede.Tests/Application/Services/GitWorkspaceBrowserServiceTests.cs`

Weitere Unit-Tests für Service (Snapshot-Loading, Preview-Loading, etc.). Nicht direkt relevant für Lazy-Loading-Anforderung.

## Test-Fixture und Hilfsmethoden

### Häufig verwendete Muster

1. **Mock-Erstellung:** 
   ```csharp
   var gitMock = new Mock<IGitWorkspaceBrowserService>();
   gitMock.Setup(g => g.LoadWorkingTreeAsync(path, It.IsAny<CancellationToken>()))
           .ReturnsAsync(nodes);
   ```

2. **Async-Warten auf State-Changes:**
   ```csharp
   await WaitForAsync(() => sut.DateiInhalt == "expected");
   ```

3. **ViewModel-Erstellung:**
   - Mit Mock-Dependencies
   - Optional `dispatcherInvoke`-Lambda für synchrones Testing (sonst real Dispatcher)

## Coverage-Lücken

1. **ChildrenLoaded-Semantik:** Keine Tests prüfen, dass `ChildrenLoaded` korrekt gesetzt wird auf neue Knoten
2. **Depth-Tracking:** Keine Tests prüfen, dass `Depth` korrekt berechnet wird
3. **Lazy-Loading-Trigger:** Keine Tests für Aufklapp-Logik (neue `LadeKinderAsync`)
4. **Initial-Tiefe:** Keine Tests für Limitierung auf `maxInitialDepth = 2`
5. **Performance:** Keine Tests für große Verzeichnisbäume oder Abbruch bei MaxNodeCount

## Pattern: CommitAufklappenAsync als Vorbild

Die bestehenden Tests für `CommitAufklappenAsync` können als Vorlage für neue Lazy-Loading-Tests dienen:
- Prüfung von `ChildrenLoaded`-Flag (hier: `commit.ChildrenLoaded`)
- Service-Aufrufe bei Expand
- Fehlerbehandlung mit Error-Message
- IsLoadingFiles-Spinner während des Ladens

Analog sollten neue Tests für `LadeKinderAsync` strukturiert sein.
