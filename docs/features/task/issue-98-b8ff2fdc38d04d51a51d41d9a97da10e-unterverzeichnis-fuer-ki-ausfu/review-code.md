# Code-Review: Zweiter Nacharbeits-Zyklus (Issue #98)

Status: **Keine Befunde** (2 im Review gefundene Bugs wurden innerhalb desselben Durchlaufs behoben und
durch neue Regressionstests abgesichert, bevor dieser Bericht finalisiert wurde; siehe Abschnitt „Befunde
und Behebung" für die vollständige Nachvollziehbarkeit. Ein dritter, vorbestehender Punkt außerhalb des
Scopes von `continue.md` wurde dokumentiert, aber bewusst nicht in diesem Zyklus behoben.)

## Vorgehen

Review des vollständigen Diffs (`git diff HEAD` gegen den zu Beginn dieses Laufs vorgefundenen Stand)
über die 8 Standard-Angles (Zeile-für-Zeile, entferntes Verhalten, Cross-File-Aufrufer, Wiederverwendung,
Vereinfachung, Effizienz, Flughöhe, CLAUDE.md-Konventionen), mit anschließender 1-Vote-Verifikation der
Kandidaten.

## Befunde und Behebung

### 1. [Behoben] Bitbucket Self-Hosted: falsches JSON-Schema führte zu stiller Leer-Antwort

**Datei:** `plugins/Softwareschmiede.Plugin.BitBucket/BitBucketPlugin.cs`

Die ursprüngliche Implementierung von `GetRepositoryStructureAsync` nutzte für den Self-Hosted-Modus
(Bitbucket Server/Data Center) denselben JSON-Parser wie für Bitbucket Cloud (flache `values`-Liste,
`type == "commit_directory"`, `next`-Link). Bitbucket Server liefert jedoch ein verschachteltes Schema
(`children.values`, `type == "DIRECTORY"`, Pagination über `isLastPage`/`nextPageStart` statt eines
Link-Headers) und kennt zudem keinen rekursiven `max_depth`-Parameter — der `browse`-Endpunkt liefert pro
Aufruf nur eine Verzeichnisebene.

**Auswirkung vor der Behebung:** Für Self-Hosted-Repositories wäre `GetRepositoryStructureAsync` in der
Praxis immer leer zurückgekommen (kein Fehler, keine Warnung — der Parser fand einfach keine `values` im
erwarteten Schema und lieferte stillschweigend nichts).

**Behebung:** Zwei getrennte Implementierungen — `GetCloudRepositoryStructureAsync` (unverändert, Cloud-API
mit rekursivem `max_depth`) und `GetSelfHostedRepositoryStructureAsync` (neu: breitensuchender Level-für-
Level-Aufbau über mehrere `browse`-Aufrufe mit dem korrekten `children.values`/`DIRECTORY`-Schema und
`isLastPage`/`nextPageStart`-Pagination). Guardrail gegen ausufernde Verzeichnisbäume (max. 500
Verzeichnisse pro Ebene, mit Warn-Log).

**Verifikation:** Neuer Test `GetRepositoryStructureAsync_ShouldWalkDirectoryLevels_WhenHostingModeIsSelfHosted`
mit dem korrekten zweistufigen Server-Schema (Wurzelverzeichnis liefert `backend` + `README.md`, ein
zweiter Aufruf für `backend` liefert `backend/src`) sowie
`GetRepositoryStructureAsync_ShouldReturnEmpty_WhenSelfHostedBrowseFails` für den Fehlerfall. Der
ursprüngliche (fehlerhafte) Test hatte das Cloud-Schema für den Self-Hosted-Fall verwendet und damit den
Bug maskiert — dieser Fehler in der Testkonstruktion selbst ist der eigentliche Grund, warum der Bug nicht
schon beim ersten Schreiben auffiel.

### 2. [Behoben] GitHub: Repository-ID-Auflösung schlug bei abschließendem Slash fehl

**Datei:** `plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.cs`

`TryExtractRepositoryId` nutzte manuelles String-Slicing (`LastIndexOf('/')` zweimal) statt einer
`Uri`-basierten Pfad-Zerlegung. Bei einer URL mit abschließendem Slash (z. B. aus dem Browser kopiert:
`https://github.com/owner/repo/`) ergab das Slicing `repo = ""` und `owner = "repo"` — durch die
Empty-Prüfung wurde zwar korrekt `null` zurückgegeben (keine Datenkorruption), aber die Verzeichnisstruktur-
Auflösung schlug für eine sehr gängige URL-Form fehl.

**Behebung:** HTTPS-Zweig auf `Uri.AbsolutePath`-basierte Segment-Zerlegung umgestellt (identisches Muster
wie bereits im `BitbucketPlugin` verwendet) — robust gegen abschließende Slashes, Query-Strings und
Fragmente.

**Verifikation:** Neuer Test `GetRepositoryStructureAsync_ShouldResolveRepositoryId_FromUrlWithTrailingSlash`.

### 3. [Dokumentiert, nicht in diesem Zyklus behoben] `TaskDetailViewModel`-Restart-Pfad übergibt kein `startConfig`/`gitPlugin`

**Datei:** `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs:773` (`StartCliAndUpdateStateAsync`,
aufgerufen von `PluginWechselnUndCliNeuStartenAsync` und `CliAutomatischNeustartenAsync`)

Dieser Aufrufer von `KiAusfuehrungsService.StartWithPseudoConsoleAsync` übergibt weder `startConfig` noch
`gitPlugin` — anders als der initiale Start über `EntwicklungsprozessService.ProzessStartenUndCliStartenAsync`.
Das bedeutet: ein CLI-Neustart nach Plugin-Wechsel oder automatischer Neustart nach Rate-Limit-Erkennung
berücksichtigt das konfigurierte Arbeitsunterverzeichnis nicht (weder die ursprüngliche `startConfig`-Logik
aus Zyklus 1 noch die `InSourceDirectory`-Auflösung aus diesem Zyklus).

**Warum nicht in diesem Zyklus behoben:** Diese Lücke ist nicht durch die in `continue.md` beschriebenen
Punkte abgedeckt (die sich explizit auf `KiAusfuehrungsService.StartCliAsync`/`StartWithPseudoConsoleAsync`
und `GitOrchestrationService.ValidateWorkingDirectoryAfterCloneAsync` als Methoden beziehen, nicht auf
jeden einzelnen Aufrufer) und bestand bereits vor diesem Zyklus unverändert. Eine Behebung würde zusätzlich
klären, woher `TaskDetailViewModel` das benötigte `IGitPlugin`/`RepositoryStartKonfiguration` für den
Restart-Fall bezieht (aktuell hat die Klasse keinen `GitOrchestrationService`/`ProjektService`-Zugriff für
diesen Zweck) — das ist ein eigenständiger, nicht-trivialer Planungspunkt.

**Empfehlung:** Als eigener Punkt in einem Folgezyklus aufnehmen (in `continue.md` dieses Laufs
dokumentiert).

## Sonstige geprüfte Punkte ohne Befund

- GitHub-Tree-Filterung (`type == "tree"`) schließt `blob`- und `commit`-Einträge (Submodule) korrekt aus.
- `maxDepth`-Semantik (`path.Count(c => c == '/') + 1 <= maxDepth`) ist konsistent mit der bereits
  vorhandenen `LocalDirectoryPlugin`-Implementierung.
- `WorkingDirectoryResolver`s Umbau auf `DetermineEffectiveWorkingDirectoryAsync` erhält alle bisherigen
  Validierungen (Path-Traversal-Schutz, Existenz-Prüfung) unverändert; der neue Fallback auf `localRepoPath`
  bei `null`/leerem Plugin-Ergebnis ist zusätzlich robust gegen unvollständig konfigurierte Test-Doubles
  (siehe Testlauf-Notiz unten) und verändert das Verhalten für reale Plugins nicht (deren
  Default-Implementierung liefert ohnehin `localPath` unverändert).
- Alle Aufrufer von `ValidateWorkingDirectoryAfterCloneAsync`, `StartCliAsync`, `StartWithPseudoConsoleAsync`
  (Produktivcode und Tests) kompilieren und verhalten sich mit den neuen optionalen Parametern
  rückwärtskompatibel.
- Bitbucket-Cloud-Pagination: Sicherheitsnetz von 50 Seiten gegen Endlosschleifen bei fehlerhaften
  API-Antworten vorhanden; jetzt mit Warn-Log bei Erreichen der Obergrenze (vorher still).
- Keine CLAUDE.md-Regelverstöße gefunden (Build-vor-Test-Regel und Sub-Agenten-Verifikationsregel wurden
  in diesem Lauf eingehalten, siehe `test-results.md`).
