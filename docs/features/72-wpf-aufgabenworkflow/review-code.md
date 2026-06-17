# Code Review — Feature 72: WPF Aufgabenworkflow (Iteration 2)

**Status: Befunde vorhanden**

Geprüfte Dateien: `KiAusfuehrungsService.cs`, `EntwicklungsprozessService.cs`, `TaskDetailViewModel.cs`, `PluginSelectionDialogService.cs`, sowie Teststellen in `AufgabeDetailFolgePromptTests`, `AufgabeDetailGitActionsBunitTests`, `AufgabeDetailWorkspacePreviewBunitTests`, `TaskDetailViewModelTestFactory`.

---

## Befunde

### Befund 1 — Race Condition: `AbsichtlichGestoppt` zu spät gesetzt (CONFIRMED)

**Datei:** `src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs`, Zeile 167

**Problem:** `StopCliAsync` setzt `handle.AbsichtlichGestoppt = true` erst *nach* dem `_handles.TryGetValue`-Aufruf, aber *bevor* der `try`-Block beginnt. Der `Exited`-Handler des Prozesses läuft auf einem Threadpool-Thread und liest `AbsichtlichGestoppt` um zu entscheiden, ob der Status `Gestoppt` oder `Fehler` ist.

**Fehler-Szenario:** Ein Prozess endet mit ExitCode != 0 (z. B. Rate-Limit-Abbruch). Der OS/CLR-`Exited`-Event feuert auf einem Threadpool-Thread, liest `AbsichtlichGestoppt == false` und emittiert `CliProcessStatus.Fehler` — einschließlich `PersistFehlgeschlagenAsync`. Erst *danach* erreicht `StopCliAsync` Zeile 167 und setzt das Flag. Der Fehler-Status ist bereits gesendet und kann nicht zurückgenommen werden. Die UI zeigt einen Fehler, obwohl der Nutzer den Prozess absichtlich gestoppt hat.

---

### Befund 2 — `IsCliRunning = true` wird bei Fehler in `StartCliAsync` nicht zurückgesetzt (CONFIRMED)

**Datei:** `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs`, Zeile 606

**Problem:** `StartCliAndUpdateStateAsync` setzt `IsCliRunning = true` (Zeile 606) *bevor* `await _kiService.StartCliAsync(...)` aufgerufen wird. Wenn `StartCliAsync` eine Exception wirft (z. B. Prozess-Start schlägt fehl), propagiert die Exception nach oben — ohne dass `IsCliRunning` zurückgesetzt wird. Die `OnCliProcessStatusChanged`-Methode feuert in diesem Fall nicht, weil kein Prozess gestartet wurde.

**Fehler-Szenario:** Prozess-Start scheitert (z. B. falscher Pfad, Zugriffsrechte). Die Exception wird in `CliAutomatischNeustartenAsync` oder `PluginWechselAsync` durch einen `catch`-Block abgefangen und `FehlerMeldung` gesetzt — aber `IsCliRunning` bleibt dauerhaft `true`. Der "Stopp"-Button bleibt aktiv, der "Starten"-Button deaktiviert, die App ist in einem inkonsistenten Zustand.

**Hinweis zu `StartenAsync`:** Der Pfad über `EntwicklungsprozessService.ProzessStartenUndCliStartenAsync` → `KiAusfuehrungsService.StartCliAsync` ist von diesem Befund nicht direkt betroffen, da `StartCliAsync` dort keine `IsCliRunning = true` Zuweisung vor dem Aufruf hat. Betroffen sind die direkten Aufrufe in `CliAutomatischNeustartenAsync` (Zeile 590) und `PluginWechselAsync` (Zeile 562).

---

### Befund 3 — `AbsichtlichGestoppt` ist kein `volatile` bool (CONFIRMED)

**Datei:** `src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs`, Zeile 341

**Problem:** `CliProcessHandle.AbsichtlichGestoppt` ist eine einfache Auto-Property (`public bool AbsichtlichGestoppt { get; set; }`) ohne `volatile`, `Interlocked` oder `lock`. Der Schreibzugriff erfolgt in `StopCliAsync` auf dem Aufrufer-Thread; der Lesezugriff im `Exited`-Handler auf einem Threadpool-Thread.

**Fehler-Szenario:** Ohne Memory-Barrier garantiert das C#-Speichermodell (ECMA-334) keine Cross-Thread-Sichtbarkeit von non-volatile Writes. Auf ARM-Hardware (Windows on ARM) oder unter aggressivem JIT/AOT-Reordering kann der `Exited`-Handler den veralteten Wert `false` lesen und fälschlicherweise `CliProcessStatus.Fehler` emittieren. Dies verschlimmert Befund 1 strukturell.

**Empfehlung:** `AbsichtlichGestoppt` als `volatile bool` deklarieren oder `Interlocked` verwenden.

---

### Befund 4 — `IsCliRunning = false` in `CliStoppenAsync` off-Dispatcher und bei nicht gestopptem Prozess (CONFIRMED)

**Datei:** `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs`, Zeile 338

**Problem:** `CliStoppenAsync` setzt `IsCliRunning = false` (Zeile 338) *bedingungslos* nach `await _kiService.StopCliAsync(...)`. `StopCliAsync` schluckt intern alle Exceptions (catch-Block Zeile 186–189 in `KiAusfuehrungsService.cs`) und wirft nie nach außen. Der direkte Set läuft zudem auf dem Task-Thread, nicht durch `_dispatcherInvoke`, im Gegensatz zu `OnCliProcessStatusChanged`.

**Fehler-Szenario (a) — Prozess noch aktiv:** Wenn `process.Kill()` fehlschlägt (z. B. Zugriffsrechte, OS-Race) und der Prozess weiterläuft, ist `IsCliRunning` im ViewModel trotzdem `false`. Der `Exited`-Event feuert später, aktualisiert aber nur State-Properties — kein Fehler wird dem Nutzer angezeigt. Die App glaubt, die CLI sei gestoppt; der Prozess läuft weiter.

**Fehler-Szenario (b) — Off-Dispatcher-Write:** Der direkte Set auf Zeile 338 (nicht über Dispatcher) feuert PropertyChanged-Notifications außerhalb des UI-Threads. `OnCliProcessStatusChanged` macht dasselbe via `_dispatcherInvoke`. Beide konkurrieren und führen zu doppelten PropertyChanged-Notifications ohne Zustandsänderung.

Das gleiche Muster existiert auch in `PluginWechselAsync`, Zeile 558.

---

### Befund 5 — `selectedScmPluginPrefix: null` in `ProzessStartenUndCliStartenAsync` (PLAUSIBLE)

**Datei:** `src/Softwareschmiede/Application/Services/EntwicklungsprozessService.cs`, Zeile 208

**Problem:** `ProzessStartenUndCliStartenAsync` ruft `ProzessStartenAsync` immer mit `selectedScmPluginPrefix: null` (4. Parameter) auf. `ProzessStartenAsync` bevorzugt `repository.PluginTyp`; wenn dieser leer ist, wird `null` an `ResolveSourceCodeManagementPluginAsync` übergeben. Die Methode fällt dann lautlos auf den alphabetisch ersten SCM-Plugin zurück.

**Fehler-Szenario:** Ein Repository, das ohne `PluginTyp`-Befüllung (Legacy-Datensatz) angelegt wurde, wird im WPF-Start-Flow mit dem falschen SCM-Plugin geklont. Es gibt keinen Dialog-Fallback für die SCM-Plugin-Auswahl im WPF-Flow (anders als beim KI-Plugin). Der Fehler äußert sich erst, wenn die Git-Operation mit dem falschen Plugin fehlschlägt — ohne klare Fehlermeldung.

---

### Befund 6 — `CancellationToken` in `PluginSelectionDialogService` nur einmal geprüft (PLAUSIBLE)

**Datei:** `src/Softwareschmiede.App/Services/PluginSelectionDialogService.cs`, Zeile 17

**Problem:** `ShowPluginSelectionDialogAsync` ruft `ct.ThrowIfCancellationRequested()` einmalig (Zeile 15) auf, dann `System.Windows.Application.Current.Dispatcher.Invoke(...)` — ein synchroner, blockierender Aufruf ohne Timeout und ohne Cancellation-Unterstützung.

**Fehler-Szenario:** Beim App-Shutdown wird das übergebene `CancellationToken` abgebrochen, während der Plugin-Auswahl-Dialog offen ist. `Dispatcher.Invoke` kann die Cancellation nicht beobachten und blockiert, bis der Nutzer den Dialog manuell schließt. Der Shutdown-Sequenz hängt. In extremen Fällen muss der Prozess vom OS abgewürgt werden.

---

### Befund 7 — `TaskDetailViewModelTestFactory` verdrahtet `EntwicklungsprozessService` ohne `KiAusfuehrungsService` (PLAUSIBLE/latent)

**Datei:** `src/Softwareschmiede.Tests/Helpers/TaskDetailViewModelTestFactory.cs`, Zeile 29

**Problem:** Die Factory erstellt `KiAusfuehrungsService` (Zeile 20) und übergibt ihn ans `TaskDetailViewModel` (Zeile 40), aber *nicht* an den `EntwicklungsprozessService` (Zeile 29–35, 6-Parameter-Konstruktor). Der `EntwicklungsprozessService`-Instanz hat daher `_kiAusfuehrungsService == null`.

**Fehler-Szenario:** Wenn ein Test, der die Factory verwendet (aktuell `ProjectListViewModelTests`, `ProjectDetailViewModelTests`), `vm.StartenCommand.Execute(null)` aufruft, wird im ViewModel `_entwicklungsprozessService.ProzessStartenUndCliStartenAsync(...)` aufgerufen, was mit `InvalidOperationException("KiAusfuehrungsService ist nicht konfiguriert.")` wirft. Aktuell rufen diese Tests `StartenCommand` nicht auf — aber die Factory ist für zukünftige Tests eine Falle.

---

## Zusammenfassung

| # | Datei | Zeile | Schwere | Status | Problem |
|---|-------|-------|---------|--------|---------|
| 1 | `KiAusfuehrungsService.cs` | 167 | Hoch | CONFIRMED | Race Condition: `AbsichtlichGestoppt` zu spät gesetzt — falscher `Fehler`-Status bei absichtlichem Stop |
| 2 | `TaskDetailViewModel.cs` | 606 | Hoch | CONFIRMED | `IsCliRunning = true` vor `StartCliAsync`, bleibt bei Exception dauerhaft `true` |
| 3 | `KiAusfuehrungsService.cs` | 341 | Mittel | CONFIRMED | `AbsichtlichGestoppt` nicht `volatile` — keine Cross-Thread-Sichtbarkeitsgarantie |
| 4 | `TaskDetailViewModel.cs` | 338 | Mittel | CONFIRMED | `IsCliRunning = false` off-Dispatcher und bedingungslos auch bei nicht gestopptem Prozess |
| 5 | `EntwicklungsprozessService.cs` | 208 | Mittel | PLAUSIBLE | `selectedScmPluginPrefix: null` — falscher Plugin-Fallback für Repos ohne `PluginTyp` |
| 6 | `PluginSelectionDialogService.cs` | 17 | Niedrig | PLAUSIBLE | CT nur einmal geprüft, `Dispatcher.Invoke` blockiert bei Shutdown |
| 7 | `TaskDetailViewModelTestFactory.cs` | 29 | Niedrig | PLAUSIBLE | Factory verdrahtet `EntwicklungsprozessService` ohne `KiAusfuehrungsService` — latente Falle für Tests |
