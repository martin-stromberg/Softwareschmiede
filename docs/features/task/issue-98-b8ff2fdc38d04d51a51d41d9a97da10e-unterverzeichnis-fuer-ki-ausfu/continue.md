# Offene Aufgaben

Erstellt am: 2026-07-09
Abbruchgrund: Vom Nutzer nach Abschluss der letzten Iteration nachgereichte Folgepunkte (kein Iterationslimit-Abbruch)

Die folgenden Aufgaben wurden nach Abschluss der bisherigen Umsetzung entdeckt bzw. vom Nutzer benannt
und müssen in einem neuen Plan-/Implementierungszyklus bearbeitet werden.

## Offene Planelemente

- [ ] **Bug: `WorkingDirectoryResolver` ist inkompatibel mit `LocalDirectoryPlugin`s Workspace-Modus
      `InSourceDirectory`.** Von zwei unabhängigen Agenten-Läufen bestätigt (identischer Fehler).

  **Symptom:** `E2E_WorkingDirectory.AufgabeStarten_MitKonfiguriertemArbeitsverzeichnis_CliStartetErfolgreich_E2E`
  schlägt fehl mit `DirectoryNotFoundException`/`InvalidOperationException: Arbeitsverzeichnis nicht
  gefunden: <ClonePath>\backend`, obwohl `backend` im tatsächlichen Repository (Quellordner) existiert.

  **Root Cause:** Im `InSourceDirectory`-Modus kopiert `LocalDirectoryPlugin.CloneRepositoryAsync(...)`
  nichts in das Zielverzeichnis (`clonePath`/`localRepoPath`) — dieses enthält nur eine Pointer-Datei
  (`WriteWorkspacePointer`, `LocalDirectoryPlugin.cs` Zeile ~891–907), die tatsächliche Repository-Struktur
  bleibt im Quellordner. Nur die plugin-interne, **private** Methode
  `LocalDirectoryPlugin.ResolveWorkspacePath(string)` (Zeile ~909) kann diesen Pointer auflösen; sie wird
  von `CreateBranchAsync`, `PullAsync`, `CommitAsync`, `ResetAsync`, `MergeToSourceAsync` und
  `GetGitActionCapabilitiesAsync` genutzt, aber **nicht** von
  `WorkingDirectoryResolver.DetermineEffectiveWorkingDirectory(...)`, weil diese Methode plugin-agnostisch
  ist und `IGitPlugin` aktuell keine öffentliche Methode zur Pfad-Auflösung anbietet.

  **Betroffene Aufrufstellen (vorbestehend, kein Regressions-Bug einer einzelnen Iteration):**
  - `KiAusfuehrungsService.cs` (`StartCliAsync`/`StartWithPseudoConsoleAsync`)
  - `GitOrchestrationService.ValidateWorkingDirectoryAfterCloneAsync(...)`

  **Empfohlene Lösung:**
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
  4. Neuen Unit-Test ergänzen (z. B. in `KiAusfuehrungsServiceTests_WorkingDirectory` bzw. neue Testklasse),
     der `LocalDirectoryPlugin` im `InSourceDirectory`-Modus mit konfiguriertem Unterverzeichnis gegen
     `KiAusfuehrungsService`/`GitOrchestrationService` verifiziert (deckt das E2E-Szenario ab, ohne auf eine
     echte Desktop-Session angewiesen zu sein).
  5. Nach dem Fix: `E2E_WorkingDirectory.AufgabeStarten_MitKonfiguriertemArbeitsverzeichnis_CliStartetErfolgreich_E2E`
     erneut ausführen und grün bestätigen.

- [ ] **Lücke: `GetRepositoryStructureAsync()` ist für keines der produktiven Remote-SCM-Plugins
      implementiert (weder `GitHubPlugin` noch `BitBucketPlugin`) — das ist keine akzeptable Einschränkung,
      sondern eine Kernlücke der ursprünglichen Anforderung.**

  **Warum das ein echtes Problem ist, nicht nur ein Randfall:** Der zentrale Anwendungsfall der
  Anforderung ist, dass ein Nutzer ein Repository (typischerweise von GitHub) auswählt und dafür einen
  relativen Pfad **vor dem Klon** festlegt. Genau dafür muss die Verzeichnisstruktur des Haupt-Branches
  remote gelesen werden — ohne lokalen Checkout. Aktuell ist eine Unterverzeichnis-Auswahl daher nur für
  `LocalDirectoryPlugin` möglich; für GitHub/BitBucket zugewiesene Repositories bleibt ausschließlich das
  Root-Verzeichnis (`"."`) wählbar. Das betrifft den Hauptanwendungsfall, nicht einen Nebenfall.

  **Bisherige (fehlerhafte) Begründung, warum das nicht umgesetzt wurde:** Kommentar in
  `GitHubPlugin.cs`/`BitBucketPlugin.cs`: *„`GetAvailableRepositoriesAsync` liefert ausschließlich
  Remote-URLs (kein garantierter lokaler Klon-Pfad), daher bleibt die Default-Implementierung aus
  `IGitPlugin` (`NotSupportedException`) unverändert bestehen."* Diese Begründung ist unzutreffend: Ein
  lokaler Klon-Pfad wird für die Strukturermittlung gar nicht benötigt.

  **Beleg, dass ein Remote-Abruf ohne Klon technisch funktioniert:** `GitHubPlugin.GetDefaultBranchAsync(...)`
  (Zeile ~570) ermittelt den Standard-Branch bereits rein remote über `git ls-remote --symref
  <repositoryUrl> HEAD`, ganz ohne Checkout. `BitBucketPlugin.GetDefaultBranchAsync(...)` (Zeile ~670)
  macht dasselbe. Beide Plugins verfügen zudem bereits über funktionierende Remote-API-Infrastruktur:
  `GitHubPlugin` nutzt die `gh`-CLI mit `GH_TOKEN`-Authentifizierung (siehe `GetAvailableRepositoriesAsync`,
  `gh repo list --json ...`), `BitBucketPlugin` hat eine analoge REST-Anbindung.

  **Empfohlene Lösung (für GitHub):**
  - `GitHubPlugin.GetRepositoryStructureAsync(repositoryUrl, maxDepth, ct)` überschreiben.
  - Branch ermitteln (per `GetDefaultBranchAsync`, falls kein spezifischer Branch übergeben wird).
  - Verzeichnisstruktur per GitHub Git-Trees-API abrufen, z. B. über die vorhandene `ICliRunner`-Infrastruktur:
    `gh api repos/{owner}/{repo}/git/trees/{branch}?recursive=1`, JSON-Antwort parsen (`tree[].path`,
    `tree[].type == "tree"` für Verzeichnisse), auf `maxDepth` filtern.
  - Achtung: Die GitHub-API liefert bei sehr großen Repositories eine `truncated: true`-Flag zurück
    (Trees-API-Limit ca. 100.000 Einträge/7MB) — für den MVP-Anwendungsfall (Verzeichnisauswahl bis
    `MaxDepth`, typischerweise 2) ist das unkritisch, sollte aber nicht stillschweigend ignoriert werden
    (z. B. Logging/Warnung bei `truncated: true`).

  **Empfohlene Lösung (für BitBucket):**
  - `BitBucketPlugin.GetRepositoryStructureAsync(repositoryUrl, maxDepth, ct)` überschreiben.
  - Branch ermitteln (per `GetDefaultBranchAsync`).
  - Verzeichnisstruktur per BitBucket-REST-API abrufen: `GET
    /2.0/repositories/{workspace}/{repo_slug}/src/{branch}/?max_depth={maxDepth}` (paginiert über
    `next`-Link, `type == "commit_directory"` für Verzeichnisse).

  **Nach der Umsetzung:** `DirectoryStructureBrowserServiceTests` und neue plugin-spezifische Tests
  (analog zu `LocalDirectoryPluginTests_GetRepositoryStructureAsync`, mit gemocktem `ICliRunner`/HTTP)
  ergänzen; `plan.md`-Passage „Offener Punkt 1" (aktuell: „nur `LocalDirectoryPlugin` implementiert,
  Remote-Provider fallen bewusst auf Root zurück") entsprechend korrigieren, da die Einschränkung dann
  nicht mehr zutrifft.
