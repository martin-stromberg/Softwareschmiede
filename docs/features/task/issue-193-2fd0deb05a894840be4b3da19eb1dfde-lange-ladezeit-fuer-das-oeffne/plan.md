# Umsetzungsplan: Asynchrones Laden von Aufgabenprotokollen (Issue 193)

## Übersicht

Diese Anforderung entkoppelt das Laden von Protokolleinträgen vom Laden der Aufgabenbasisinformationen. Beim Öffnen einer Aufgabendetailansicht wird die Basisinformation (Titel, Status, Branch, Beschreibung) schnell angezeigt, während Protokolleinträge asynchron im Hintergrund mittels Fire-and-Forget-Muster nachgeladen werden. Dies erfordert Änderungen in `AufgabeService.GetDetailAsync()` (Include entfernen) und `TaskDetailViewModel.LadenAsync()` (Umstrukturierung auf asynchrones Laden).

---

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|---|---|---|
| Protokoll-Nachlade-Muster | Fire-and-Forget Pattern (`_ = LadeProtokolleAsynch(ct)`) | Blockiert nicht den UI-Thread, ermöglicht schnelle Anzeige der Basisinformation; Pattern ist Standard in Softwareschmiede Codebase |
| Fehlerbehandlung | Try-Catch mit `_logger.LogError()` | Wie in Anforderung vorgegeben; Fehler werden protokolliert, User-Notification ist optional und nicht erforderlich |
| UI-Indikator für Protokoll-Laden | Stillschweigend im Hintergrund, keine explizite UI-Änderung | Anforderung: „still im Hintergrund verborgen"; Protokoll-Collection wird still befüllt, wenn es eintrifft |
| CancellationToken-Handling | Wird an `LadeProtokolleAsynch()` weitergeleitet | Ermöglicht Abbruch des Protokoll-Ladens, wenn ViewModel disposed wird |

---

## Programmabläufe

### Aufgabendetail laden (TaskDetailViewModel.LadenAsync)

**Neuer Ablauf (asynchron, mit Protokoll-Fire-and-Forget):**

1. `IsLoading` auf `true` setzen
2. `_aufgabeService.GetDetailAsync(_aufgabeId, ct)` aufrufen → Aufgabe **ohne** Protokolleinträge
3. `Aufgabe` Property setzen → UI zeigt Basisinformation sofort
4. Fire-and-forget: `_ = LadeProtokolleAsynch(ct)` aufrufen (nicht awaited)
5. Parallel starten: `LadePullRequestsAsync()`, `_todoListViewModel.LadenAsync()`, etc. (bisherige Sequenz)
6. Im Hintergrund (unabhängig, nicht blockierend): `LadeProtokolleAsynch()` führt aus:
   - `_protokollService.GetByAufgabeAsync(_aufgabeId, ct)` aufrufen
   - Erfolg: `Protokolleintraege` ObservableCollection füllen → UI-Binding aktualisiert automatisch
   - Abbruch (`OperationCanceledException`): stillschweigend ignorieren
   - Fehler (andere Exception): Mit `_logger.LogError()` protokollieren
7. In `finally`: `IsLoading` auf `false` setzen (nach Aufgabe geladen, nicht nach Protokoll)

**Beteiligte Klassen:** `TaskDetailViewModel`, `AufgabeService`, `ProtokollService`

**Kritischer Unterschied zum aktuellen Ablauf:**
- Alt: `GetDetailAsync()` (blockiert bei großem Protokoll) → `GetByAufgabeAsync()` (sequenziell) → Andere Operationen
- Neu: `GetDetailAsync()` (schnell) → `LadeProtokolleAsynch()` (fire-and-forget) + Andere Operationen (parallel)

---

## Neue Klassen

Keine neuen Klassen erforderlich — nur Umstrukturierung bestehender Methoden.

---

## Änderungen an bestehenden Klassen

### `AufgabeService` (Anwendungs-Service)

- **Geänderte Methode:** `GetDetailAsync(Guid id, CancellationToken ct = default)` 
  - **Zeilen 56–57** (aus `logic.md`): `.Include(a => a.Protokolleintraege).ThenInclude(p => p.TestErgebnisse)` entfernen
  - **Rationale:** Basisinformation wird schneller geladen, wenn Protokoll nicht mit Include geholt wird
  - **Verhalten:** Aufgabe wird geladen mit: `Projekt`, `IssueReferenz`, `AlertReferenz`, `GitRepository` + `StartKonfiguration`, `Todos`, aber OHNE `Protokolleintraege` und deren `TestErgebnisse`
  - **Auswirkung:** Navigation Property `Aufgabe.Protokolleintraege` bleibt navigierbar (nicht geladen), aber bei EF Core `AsNoTracking()` ist kein Navigation-Traversing möglich

### `TaskDetailViewModel` (WPF ViewModel)

- **Neue private async Methode:** `LadeProtokolleAsynch(CancellationToken ct)` 
  - **Zweck:** Lädt Protokolleinträge asynchron im Hintergrund, ohne UI zu blockieren
  - **Parameter:** `CancellationToken ct`
  - **Rückgabewert:** `Task` (void-async Pattern wird vermieden)
  - **Implementierung:**
    ```
    try {
        var protokolleintraege = await _protokollService.GetByAufgabeAsync(_aufgabeId, ct);
        Protokolleintraege.Clear();
        foreach (var eintrag in protokolleintraege)
            Protokolleintraege.Add(eintrag);
    } catch (OperationCanceledException) {
        // Erwarteter Fehler bei Abbruch — stillschweigend ignorieren
    } catch (Exception ex) {
        _logger.LogError(ex, "Fehler beim asynchronen Laden der Protokolle für Aufgabe {AufgabeId}.", _aufgabeId);
    }
    ```

- **Geänderte Methode:** `LadenAsync(CancellationToken ct)` 
  - **Zeile 644** (aus `viewmodel.md`): `await _aufgabeService.GetDetailAsync(_aufgabeId, ct)` bleibt, wird aber schneller sein
  - **Zeile 666** (aus `viewmodel.md`): `await _protokollService.GetByAufgabeAsync(_aufgabeId, ct)` wird entfernt
  - **Zeilen 667–670**: Manuelle Befüllung von `Protokolleintraege` wird entfernt
  - **Nach Zeile 644:** Neue Zeile `_ = LadeProtokolleAsynch(ct);` einfügen (Fire-and-Forget)
  - **Rationale:** Protokoll-Laden blockiert nicht mehr; UI wird schnell responsiv
  - **Auswirkung:** `IsLoading` wird nach `GetDetailAsync()` auf `false` gesetzt (nicht nach vollständigem Protokoll-Laden), aber das ist akzeptabel, da Basisinformation komplett ist

---

## Datenbankmigrationen

**Keine.** Diese Änderung modifiziert nur die Datenzugriff-Logik (EF Core Queries), nicht das Schema.

---

## Validierungsregeln

**Keine.** Keine neuen Validierungen erforderlich.

---

## Konfigurationsänderungen

**Keine.** Die Änderung ist rein technisch, kein neuer Konfigurationseintrag erforderlich.

---

## Seiteneffekte und Risiken

### Seiteneffekt 1: Andere Aufrufer von `GetDetailAsync()`
- **Beschreibung:** Wenn andere Code-Stellen `AufgabeService.GetDetailAsync()` aufrufen und auf `Aufgabe.Protokolleintraege` vertrauen, werden diese NULL/leer sein (EF Core `AsNoTracking()` lädt nicht Include-ierte Properties nicht nach).
- **Mitigation:** Code-Suche nach anderen Aufrufen durchführen (Schritt 6 der Umsetzungsreihenfolge); falls gefunden, müssen diese selbst `ProtokollService.GetByAufgabeAsync()` aufrufen oder ein alternativer Include-Weg gefunden werden.
- **Bekannte Aufrufer:** TaskDetailViewModel (wird angepasst), Tests in AufgabeServiceTests (einige Tests müssen angepasst werden)

### Seiteneffekt 2: `IsLoading` Flag wird früher auf `false` gesetzt
- **Beschreibung:** Bisher: `IsLoading = true` während GetDetailAsync + GetByAufgabeAsync; Neu: `IsLoading = true` nur während GetDetailAsync. Protokoll lädt parallel im Hintergrund, während `IsLoading` bereits `false` ist.
- **Auswirkung:** Wenn UI-Logik (z. B. Button-Deaktivierung) auf `IsLoading` prüft, könnten User-Interaktionen möglich sein, während Protokoll noch lädt. **Aber:** Anforderung sagt „Verhalten darf sich für den Benutzer nicht verändern" → Das ist akzeptabel, da Basisinformation komplett ist und User nicht auf Protokoll warten muss.
- **Mitigation:** Keine Änderung erforderlich; Verhalten ist beabsichtigt.

### Seiteneffekt 3: Race Conditions in Tests
- **Beschreibung:** Tests, die nach `LadenCommand.ExecuteAsync()` sofort `Protokolleintraege.Count > 0` prüfen, können flaky werden, da Protokoll asynchron lädt.
- **Mitigation:** Tests müssen angepasst werden, um auf Protokoll-Collection zu warten oder nur Aufgabe zu prüfen (Schritt 5 der Umsetzungsreihenfolge).

### Seiteneffekt 4: Abhängigkeit von `ProtokollService` in TaskDetailViewModel
- **Beschreibung:** TaskDetailViewModel nutzt bereits `_protokollService`; keine neue Abhängigkeit.
- **Mitigation:** Keine — bereits vorhanden.

---

## Umsetzungsreihenfolge

1. **Änderung `AufgabeService.GetDetailAsync()` — Include entfernen**
   - Voraussetzungen: Keine
   - Beschreibung: In Datei `src/Softwareschmiede/Application/Services/AufgabeService.cs`, Zeilen 56–57, die Include-Chain `.Include(a => a.Protokolleintraege).ThenInclude(p => p.TestErgebnisse)` entfernen. Query wird dadurch schneller, da Protokoll nicht mit geladen wird. Nach Änderung sollte die Methode folgende Includes enthalten:
     - `.Include(a => a.Projekt)`
     - `.Include(a => a.IssueReferenz)`
     - `.Include(a => a.AlertReferenz)`
     - `.Include(a => a.GitRepository).ThenInclude(r => r!.StartKonfiguration)`
     - `.Include(a => a.Todos)`
     - (NICHT: `.Include(a => a.Protokolleintraege)`)

2. **Neue Methode `TaskDetailViewModel.LadeProtokolleAsynch()`**
   - Voraussetzungen: Schritt 1 (zur Verifikation, dass GetDetailAsync schneller ist)
   - Beschreibung: In Datei `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs`, neue private async Methode `LadeProtokolleAsynch(CancellationToken ct)` hinzufügen. Implementierung wie in Abschnitt „Änderungen an bestehenden Klassen" beschrieben: Ruft `_protokollService.GetByAufgabeAsync()` auf, füllt `Protokolleintraege` Collection, Error-Handling mit Logger.

3. **Änderung `TaskDetailViewModel.LadenAsync()` — Fire-and-Forget einführen**
   - Voraussetzungen: Schritt 2 (LadeProtokolleAsynch existiert)
   - Beschreibung: In Datei `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs`, Methode `LadenAsync()` (Zeilen 634–700):
     - Nach Zeile 644 (`Aufgabe = await _aufgabeService.GetDetailAsync(...)`): Neue Zeile einfügen: `_ = LadeProtokolleAsynch(ct);`
     - Zeilen 666–670 entfernen: `var protokolleintraege = await _protokollService.GetByAufgabeAsync(...); Protokolleintraege.Clear(); foreach (...)` entfernen
     - Diese Blockierung wird durch Fire-and-Forget ersetzt, andere asynchrone Operationen können parallel laufen

4. **Überprüfung: Andere Aufrufer von `GetDetailAsync()` suchen**
   - Voraussetzungen: Schritt 1 (GetDetailAsync geändert)
   - Beschreibung: Code-Suche nach allen Aufrufen von `AufgabeService.GetDetailAsync()` im gesamten Repo durchführen (Grep: `GetDetailAsync`). Für jeden Aufrufer prüfen:
     - Erwartet der Aufrufer `Aufgabe.Protokolleintraege` populated zu sein? 
     - Falls ja: Aufrufer anpassen, um selbst `ProtokollService.GetByAufgabeAsync()` zu rufen, oder Query-Strategie überdenken
     - Falls nein: Keine Änderung erforderlich
     - Bekannte Aufrufer: TaskDetailViewModel (wird in Schritt 3 angepasst), Tests in AufgabeServiceTests (werden in Schritt 5 angepasst)

5. **Anpassung: `AufgabeServiceTests` — Tests auf Protokoll-Verhalten prüfen**
   - Voraussetzungen: Schritt 1 (GetDetailAsync geändert), Schritt 4 (bekannte Aufrufer identifiziert)
   - Beschreibung: In Datei `src/Softwareschmiede.Tests/Application/Services/AufgabeServiceTests.cs`, alle 6 Tests, die `GetDetailAsync()` nutzen (Zeilen 160–264 aus `tests.md`), überprüfen:
     - Tests, die `Aufgabe.Protokolleintraege.Count` prüfen oder `.Protokolleintraege` durchiterieren: Diese Assertions entfernen oder Tests umschreiben, um Protokolle seperarat zu laden
     - Tests, die nur Issue-/Alert-Referenzen prüfen: Keine Änderung erforderlich
     - **Konkret zu prüfen:** TryAssignIssueReferenzIfNoneAsync_*, TryAssignIssueReferenzAndUpdateDescriptionIfNoneAsync_*, UpdateIssueReferenzAsync_* — Suche nach `.Protokolleintraege` in Assertions dieser Tests

6. **Anpassung: `TaskDetailViewModelTests` — LadenAsync und neue Methode testen**
   - Voraussetzungen: Schritt 2, 3 (LadenAsync geändert), Schritt 4 (Verständnis der Auswirkungen)
   - Beschreibung: In Datei `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests.cs`:
     - **Bestehende Tests überprüfen** (Zeilen 214–284 aus `tests.md`):
       - `AufgabeId_Setter_UsesFireAndForgetSafely()`: Fire-and-forget wird jetzt auch für Protokoll-Laden genutzt; Test-Logik bleibt, aber Verhalten ändert sich (prüfbar durch Timing-Assertion oder Mock-Verifikation)
       - Status-abhängige Tests (ShowEditPanel, ShowCliPanel, ShowDiffPanel): Nach `LadenCommand.ExecuteAsync()` ist Aufgabe geladen, aber Protokoll lädt parallel. Tests müssen warten auf Protokoll-Collection oder nur auf Aufgabe-Properties prüfen. Anpassung: `WaitFor(() => this._sut.Protokolleintraege.Count > 0)` o. ä. hinzufügen, oder nur Aufgabe-Assertions verwenden
     - **Neue Tests schreiben:**
       - `LadeProtokolleAsynch_ShouldLoadProtocols_WhenSuccessful()`: Mock `ProtokollService`, rufe `LadeProtokolleAsynch()` auf, prüfe dass `Protokolleintraege` gefüllt ist
       - `LadeProtokolleAsynch_ShouldLogError_WhenProtokollServiceFails()`: Mock ProtokollService.GetByAufgabeAsync wirft Exception, prüfe dass Error geloggt wird und Exception nicht propagiert
       - `LadeProtokolleAsynch_ShouldIgnoreCancellation_WhenCancellationTokenCancelled()`: OperationCanceledException wird nicht geworfen
       - `LadenAsync_ShouldSetAufgabeBeforeProtocolsAreLoaded()`: Timing-Test, dass Aufgabe schneller gesetzt wird als Protokoll (optional, kann komplex sein)

7. **E2E-Tests überprüfen/anpassen — TaskDetailView-Laden**
   - Voraussetzungen: Schritt 3 (LadenAsync geändert), Schritt 5, 6 (Unit-Tests bestätigen Verhalten)
   - Beschreibung: E2E-Tests, die TaskDetailView öffnen, durchsuchen (Suchbereich: `src/Softwareschmiede.Tests/E2E/` oder ähnlich):
     - Tests prüfen, dass TaskDetailView öffnet und Aufgabenbasisinformation sichtbar wird: Keine Änderung erforderlich, sollte schneller werden
     - Tests warten auf Protokoll-Anzeige: Müssen explizite Waits hinzufügen, z. B. `WaitForElement("ProtokollItemControl")` oder ähnlich, da Protokoll jetzt asynchron nachgeladen wird
     - Neue E2E-Tests schreiben (siehe Tabelle in Abschnitt „Tests" → „E2E-Tests")

---

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|---|---|---|
| `LadeProtokolleAsynch_ShouldLoadProtocols_WhenSuccessful()` | TaskDetailViewModelTests | Dass `LadeProtokolleAsynch()` `ProtokollService.GetByAufgabeAsync()` aufruft und Ergebnis in `Protokolleintraege` Collection befüllt wird |
| `LadeProtokolleAsynch_ShouldLogError_WhenProtokollServiceFails()` | TaskDetailViewModelTests | Dass Exception von ProtokollService geloggt wird und nicht propagiert (Fire-and-Forget bleibt sauber) |
| `LadeProtokolleAsynch_ShouldIgnoreCancellation_WhenCancelled()` | TaskDetailViewModelTests | Dass `OperationCanceledException` von `_protokollService.GetByAufgabeAsync(ct)` stillschweigend ignoriert wird |
| `LadenAsync_ShouldNotWaitForProtocols()` | TaskDetailViewModelTests | Timing-Verifikation: Nach `LadenAsync` ist Aufgabe gesetzt, aber Protokoll-Collection ist möglicherweise noch leer (oder wird parallel gefüllt). Mock ProtokollService, delay einfügen, prüfen dass Aufgabe schnell gesetzt ist. |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|---|---|
| `AufgabeServiceTests.TryAssignIssueReferenzIfNoneAsync_*()` (6 Tests) | `GetDetailAsync()` lädt jetzt keine `Protokolleintraege` mehr. Wenn diese Tests Protokoll-Properties prüfen (z. B. `.Aufgabe.Protokolleintraege.Count`), müssen diese Assertions entfernt oder Test umgeschrieben werden, um Protokolle separat zu laden. Falls nur Issue-/Alert-Referenzen geprüft: Keine Änderung. |
| `TaskDetailViewModelTests.AufgabeId_Setter_UsesFireAndForgetSafely()` | Fire-and-forget wird jetzt auch für `LadeProtokolleAsynch()` genutzt (zusätzlich zu LadenAsync selbst). Test-Semantik bleibt (Verify Mock-Aufrufe), aber Verhalten ändert sich. Evtl. Timing-Assertion anpassen. |
| `TaskDetailViewModelTests.ShowEditPanel_IsTrue_WhenStatusNeu()`, `ShowCliPanel_IsTrue_WhenStatusGestartet()`, `ShowCliPanel_IsTrue_WhenStatusWartend()`, `ShowDiffPanel_IsTrue_WhenStatusBeendet()` | Nach `LadenCommand.ExecuteAsync()` ist Aufgabe geladen (schnell), aber Protokoll lädt parallel. Tests, die auf `Protokolleintraege.Count > 0` warten, müssen angepasst werden: Entweder explizite Waits hinzufügen, oder nur Aufgabe-Assertions verwenden (wenn möglich). Prüfe: Verwenden diese Tests Protokoll-Properties in Assertions? Falls ja: Anpassung erforderlich. Falls nein: Evtl. keine Änderung. |

### E2E-Tests (Pflicht)

| Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium |
|---|---|---|
| Task-Detail-View öffnet, Basisinformation wird schnell angezeigt | E2E_TaskDetail (angenommen, existiert oder wird angelegt) | Aufgabentitel, Status, Branch sind sichtbar kurz nach View-Öffnung (nicht blockiert von Protokoll-Laden) |
| Protokolle werden asynchron nachgeladen und in UI angezeigt | E2E_TaskDetail | Nach kurzer Verzögerung werden Protokolleinträge in der ProtokollControl sichtbar |
| Fehler beim Protokoll-Laden beeinflussen nicht Aufgabenbasisinfo | E2E_TaskDetail | Falls ProtokollService fehlschlägt (simuliert via Mock oder Test-Umgebung), bleibt Aufgabenbasisinfo sichtbar; Fehler wird nur geloggt (nicht in UI sichtbar, wenn nicht explizit angezeigt) |

**Betroffene bestehende E2E-Tests:**
- Alle E2E-Tests, die TaskDetailView öffnen (z. B. `E2E_OpenTaskDetail_*`, `E2E_TaskDetail_*`), müssen überprüft werden:
  - Tests, die direkt nach View-Öffnung auf Protokoll-Elemente prüfen (z. B. `GetElement("ProtokollList")`), können fehlschlag, da Protokoll asynchron lädt
  - Mitigation: Explizite Waits hinzufügen, z. B. `WaitForElement("ProtokollListItem", timeout: 5000)` oder ähnlich
  - Falls Tests nur Aufgabenbasisinfo prüfen: Keine Änderung erforderlich, Tests sollten schneller grün werden

---

## Offene Punkte

**Keine.** Alle Anforderungs-Punkte wurden durch die Anforderung selbst oder das Inventory beantwortet:

1. **UI-Lade-Indikator:** Anforderung spezifiziert: „still im Hintergrund verborgen" → Keine explizite UI-Änderung, ObservableCollection wird still gefüllt
2. **Fehlerbehandlung:** Anforderung spezifiziert: „Fehler protokollieren" → Try-Catch mit Logger implementiert
3. **Todos asynchron laden:** Außerhalb Scope dieser Anforderung → Keine Änderung
4. **Tests anpassen:** Gehört dazu → Aufgelistet in Umsetzungsreihenfolge
5. **Andere Aufrufer von GetDetailAsync():** Wird in Schritt 4 der Umsetzungsreihenfolge untersucht und behandelt
