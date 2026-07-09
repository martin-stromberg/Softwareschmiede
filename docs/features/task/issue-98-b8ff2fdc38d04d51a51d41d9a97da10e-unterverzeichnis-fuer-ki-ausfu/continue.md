# Offene Aufgaben

Erstellt am: 2026-07-09
Abbruchgrund: Maximale Iterationsanzahl erreicht (Iteration 3 von 3)

Die folgenden Aufgaben konnten im automatisierten Zyklus nicht abgeschlossen werden und müssen manuell
oder in einem erneuten Lauf bearbeitet werden.

## Offene Planelemente

Keine. `review.md` (Iteration 3) hat Status „Vollständig umgesetzt" — der zuvor offene Punkt
(`GitOrchestrationService.ValidateWorkingDirectoryAfterCloneAsync` nicht produktiv verdrahtet) wurde
behoben: `EntwicklungsprozessService.ProzessStartenAsync` ruft die Methode jetzt direkt nach dem Klon und
vor der Branch-Erstellung auf, sofern die neue optionale Abhängigkeit
`EntwicklungsprozessServiceOptions.GitOrchestrationService` gesetzt ist (in `App.xaml.cs` per DI
registriert). Verifiziert durch `EntwicklungsprozessServiceTests_WorkingDirectoryValidation` (3 Tests, grün).

## Code-Review-Befunde

Keine. `review-code.md` (Iteration 3) hat Status „Keine Befunde" — alle 4 Befunde aus Iteration 2 sowie 1
zusätzlicher, während der Iteration-3-Prüfung entdeckter Befund (Cancellation-Inkonsistenz in
`ArbeitsverzeichnisBearbeitenViewModel`) wurden noch innerhalb dieser Iteration behoben und verifiziert
(Details siehe `review-code.md`, Abschnitt „Nachtrag").

## Fehlgeschlagene Tests

- [ ] **26 von 27 Testfehlschlägen sind umgebungsbedingt** (Details in `test-results.md`): 2×
      Clipboard-Zugriff in Sandbox blockiert (`TerminalControlTests`), 1× Prozess-Timing unter Last
      (`TaskDetailViewModelTests.TestPluginWechselAsync_StopsCliAndStartsNew`, isoliert 3× verifiziert
      grün), 23× FlaUI-Timeout mangels durchgehend verfügbarer interaktiver Desktop-Session. Kein
      Handlungsbedarf im Code — bei erneutem Lauf in einer Umgebung mit stabiler Desktop-Session bzw. ohne
      Ressourcenkonkurrenz sollten diese grün sein.

- [ ] **Neu entdeckter, echter Bug (nicht durch Iteration 3 verursacht, aber durch die neue frühere
      Validierung erstmals durch einen tatsächlich durchlaufenden E2E-Test sichtbar geworden):**
      `WorkingDirectoryResolver.DetermineEffectiveWorkingDirectory(...)` ist inkompatibel mit
      `LocalDirectoryPlugin`s Workspace-Modus `InSourceDirectory`.

  **Symptom:** `E2E_WorkingDirectory.AufgabeStarten_MitKonfiguriertemArbeitsverzeichnis_CliStartetErfolgreich_E2E`
  schlägt fehl mit `DirectoryNotFoundException: Arbeitsverzeichnis nicht gefunden: <ClonePath>\backend`,
  obwohl `backend` im tatsächlichen Repository (Quellordner) existiert.

  **Root Cause:** Im `InSourceDirectory`-Modus kopiert `LocalDirectoryPlugin.CloneRepositoryAsync(...)`
  nichts in das Zielverzeichnis (`clonePath`/`localRepoPath`) — dieses enthält nur eine Pointer-Datei
  (`WriteWorkspacePointer`, `LocalDirectoryPlugin.cs` Zeile 891–907), die tatsächliche Repository-Struktur
  bleibt im Quellordner. Nur die plugin-interne, **private** Methode `LocalDirectoryPlugin.ResolveWorkspacePath(string)`
  (Zeile 909) kann diesen Pointer auflösen; sie wird von `CreateBranchAsync`, `PullAsync`, `CommitAsync`,
  `ResetAsync`, `MergeToSourceAsync` und `GetGitActionCapabilitiesAsync` genutzt, aber **nicht** von
  `WorkingDirectoryResolver.DetermineEffectiveWorkingDirectory(...)`, weil diese Methode plugin-agnostisch
  ist und `IGitPlugin` aktuell keine öffentliche Methode zur Pfad-Auflösung anbietet.

  **Betroffene Aufrufstellen (beide bereits vor Iteration 3 vorhanden, kein Iteration-3-Regressions-Bug):**
  - `KiAusfuehrungsService.cs` Zeile 106 und 190 (`StartCliAsync`/`StartWithPseudoConsoleAsync`) — seit
    Iteration 1/2 vorhanden.
  - `GitOrchestrationService.ValidateWorkingDirectoryAfterCloneAsync(...)` — neu in Iteration 3 verdrahtet,
    nutzt aber dieselbe vorbestehende, fehlerhafte Auflösungslogik. Für reale Nutzung ändert sich am
    Endergebnis nichts (der CLI-Start hätte ohnehin mit derselben Exception fehlgeschlagen); Iteration 3
    macht den Fehler nur früher sichtbar, verschlimmert ihn aber nicht.

  **Warum bisher nicht entdeckt:** `KiAusfuehrungsServiceTests_WorkingDirectory` bildet das
  `LocalDirectoryPlugin`-Pointer-Verhalten nicht nach (nutzt vermutlich direkte Verzeichnisse ohne
  Workspace-Pointer-Indirektion). Der einzige Test, der dieses Szenario end-to-end abdeckt
  (`E2E_WorkingDirectory.AufgabeStarten_MitKonfiguriertemArbeitsverzeichnis_CliStartetErfolgreich_E2E`),
  scheiterte in allen bisherigen Sandbox-Läufen an einer fehlenden interaktiven Desktop-Session
  (`TimeoutException`), bevor die UI-Interaktion die eigentliche Prüfung überhaupt erreichte. In diesem
  Lauf war die Desktop-Session zeitweise verfügbar, wodurch der zugrunde liegende Fehler erstmals real
  reproduzierbar wurde.

  **Empfohlene Lösung (für einen neuen Plan-/Implementierungszyklus, nicht in Iteration 3 umgesetzt, da
  außerhalb des Scopes der 5 zugewiesenen Punkte und eine Schnittstellenänderung erfordert):**
  1. `IGitPlugin` um eine öffentliche Methode erweitern, z. B.
     `Task<string> ResolveEffectiveRepositoryPathAsync(string localPath, CancellationToken ct = default)`,
     mit Default-Implementierung in `GitPluginBase`, die `localPath` unverändert zurückgibt (korrekt für
     GitHub/BitBucket sowie `LocalDirectoryPlugin` im `SeparateWorkingDirectory`-Modus).
  2. `LocalDirectoryPlugin` überschreibt diese Methode und liefert `ResolveWorkspacePath(localPath)`
     (bereits vorhanden, muss von `private` auf die Interface-Implementierung angehoben werden).
  3. `WorkingDirectoryResolver.DetermineEffectiveWorkingDirectory(...)` (bzw. dessen Aufrufer
     `KiAusfuehrungsService` und `GitOrchestrationService`) ruft vor der Kombination mit dem relativen
     Arbeitsverzeichnis-Pfad zuerst `gitPlugin.ResolveEffectiveRepositoryPathAsync(clonePath, ct)` auf, um
     den tatsächlichen Repository-Pfad zu ermitteln — dafür muss diesen Methoden zusätzlich eine
     `IGitPlugin`-Instanz übergeben werden (aktuell erhalten sie nur den rohen Pfad).
  4. Neuen Unit-Test in `KiAusfuehrungsServiceTests_WorkingDirectory` bzw. eine neue Testklasse ergänzen,
     die `LocalDirectoryPlugin` im `InSourceDirectory`-Modus mit konfiguriertem Unterverzeichnis gegen
     `KiAusfuehrungsService`/`GitOrchestrationService` verifiziert (deckt exakt das E2E-Szenario ab, ohne
     auf eine echte Desktop-Session angewiesen zu sein).
  5. Nach dem Fix: `E2E_WorkingDirectory.AufgabeStarten_MitKonfiguriertemArbeitsverzeichnis_CliStartetErfolgreich_E2E`
     erneut ausführen (idealerweise in einer Umgebung mit stabiler interaktiver Desktop-Session) und
     bestätigen, dass er grün wird.

## Zusätzlich in Iteration 3 behoben (über den Scope hinaus, als Nebenbefund)

- `LocalDirectoryPlugin.CopyDirectoryForSyncAsync` (genutzt von `PullAsync`/`MergeToSourceAsync`, nicht
  vom initialen Klon) kopierte ausschließlich Dateien, wodurch leere Unterverzeichnisse beim
  Datei-Sync verloren gingen. Behoben durch `EnumerateEntriesForSync(...)`, das jetzt auch Verzeichnisse
  liefert; neuer Regressionstest `CloneRepositoryAsync_ShouldPreserveEmptySubdirectory_WhenSourceContainsOne`.
  Dies ist ein eigenständiger, unabhängig entdeckter Bug (nicht die Ursache des oben beschriebenen
  `InSourceDirectory`-Problems, das ist ein separater Sachverhalt) und war **nicht** die Ursache für den
  fehlschlagenden E2E-Test — die eigentliche Klon-Kopierlogik (`CopyDirectoryWithGuardrailsAsync`) erstellte
  leere Verzeichnisse bereits korrekt.
