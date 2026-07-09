# Umsetzungsplan: Unterverzeichnis für KI-Ausführung

> Diese Fassung wurde im zweiten Nacharbeits-Zyklus (Basis: `continue.md`, Commit `46ea76c`) aktualisiert.
> Ursprünglicher Plan siehe Git-Historie (`git show 4ecca88:docs/features/.../plan.md`). Geändert wurden
> ausschließlich der Abschnitt „Offener Punkt 1" (jetzt geschlossen) und die unten neu ergänzten Abschnitte
> „Nacharbeiten Zyklus 2".

## Übersicht

Die Anwendung wird um die Fähigkeit erweitert, für jedes Git-Repository in einem Projekt ein Arbeitsverzeichnis relativ zum Wurzelverzeichnis des geklonten Repositories zu definieren und zu speichern. Nach dem Klonen führt die CLI-Ausführung nicht im Root-Verzeichnis, sondern im konfigurierten Unterverzeichnis aus. Die UI ermöglicht Benutzern, die Verzeichnisstruktur des externen Repositories voraus zu laden und ein Zielverzeichnis zu wählen.

---

## Nacharbeiten Zyklus 2 (dieser Durchlauf)

### Punkt 1: `WorkingDirectoryResolver` inkompatibel mit `LocalDirectoryPlugin`-Workspace-Modus `InSourceDirectory`

**Root Cause:** Im `InSourceDirectory`-Modus enthält der an `KiAusfuehrungsService`/`GitOrchestrationService` übergebene
"Klon-Pfad" nur eine Pointer-Datei auf das tatsächliche Quellverzeichnis; die Verzeichnisauflösung kombinierte
den relativen Arbeitsverzeichnis-Pfad bislang direkt mit diesem Pointer-Pfad statt mit dem aufgelösten
Quellverzeichnis.

**Umsetzung:**
- `IGitPlugin` um `Task<string> ResolveEffectiveRepositoryPathAsync(string localPath, CancellationToken ct = default)`
  erweitert, Default-Implementierung gibt `localPath` unverändert zurück (korrekt für GitHub/BitBucket sowie
  `LocalDirectoryPlugin` im `SeparateWorkingDirectory`-Modus).
- `GitPluginBase<TPlugin>` überschreibt sie `virtual` mit identischem Verhalten.
- `LocalDirectoryPlugin` überschreibt sie mit der bereits vorhandenen internen Pointer-Auflösung
  (`ResolveWorkspacePath`).
- `WorkingDirectoryResolver.DetermineEffectiveWorkingDirectory(...)` (bisher synchron) wurde zu
  `DetermineEffectiveWorkingDirectoryAsync(localRepoPath, startConfig, IGitPlugin? gitPlugin = null, ct)`
  umgebaut: löst `localRepoPath` zuerst über `gitPlugin.ResolveEffectiveRepositoryPathAsync(...)` auf
  (sofern ein Plugin übergeben wurde), bevor der relative Arbeitsverzeichnis-Pfad kombiniert wird. Fällt
  defensiv auf `localRepoPath` zurück, wenn das Plugin `null`/leer liefert (robust gegen unvollständig
  konfigurierte Test-Doubles).
- `KiAusfuehrungsService.StartCliAsync` und `StartWithPseudoConsoleAsync` sowie
  `GitOrchestrationService.ValidateWorkingDirectoryAfterCloneAsync` erhalten je einen zusätzlichen optionalen
  `IGitPlugin? gitPlugin = null`-Parameter (rückwärtskompatibel) und reichen ihn an
  `DetermineEffectiveWorkingDirectoryAsync` durch. `ValidateWorkingDirectoryAfterCloneAsync` nutzt intern
  dieselbe Resolver-Methode statt Resolve/Validate separat zu duplizieren.
- `EntwicklungsprozessService.ProzessStartenAsync`/`ProzessStartenUndCliStartenAsync`: Das beim Klon
  aufgelöste `IGitPlugin` wird konsequent an beide Aufrufe weitergereicht. Dabei wurde ein zusätzlicher,
  vorbestehender Bug behoben: `Aufgabe.GitRepositoryId` wird von der App nie gesetzt (Aufgaben werden ohne
  explizite Repository-Zuordnung angelegt), daher darf die Repository-/Plugin-/Startkonfigurations-Auflösung
  in `ProzessStartenUndCliStartenAsync` nicht über dieses Feld erfolgen — sie nutzt jetzt dieselbe
  `ResolveRepositoryAsync`/`ResolvePluginAsync`-Logik wie der vorangehende Klon-Schritt (Single-Active-Repository-
  Fallback über `repositoryUrl`).

**Verifikation:** Neue Testklassen `KiAusfuehrungsServiceTests_WorkingDirectory_InSourceDirectory` und
`GitOrchestrationServiceTests_WorkingDirectoryInSourceDirectory` (echtes `LocalDirectoryPlugin` im
InSourceDirectory-Modus, kein Mock) decken das Szenario End-to-End ab. Per Log-Auswertung eines
E2E-Testlaufs bestätigt: das effektive Arbeitsverzeichnis wird nun korrekt als
`<Quellverzeichnis>\backend` aufgelöst (vorher: unveränderter Klon-Pfad). Das mit dem ConPTY-Prozessstart
selbst zusammenhängende Timeout im E2E-Test ist eine von diesem Fix unabhängige Umgebungseinschränkung
(siehe `test-results.md`).

### Punkt 2: Remote-Verzeichnisstruktur für `GitHubPlugin`/`BitbucketPlugin`

**Vorherige Einschränkung:** `GetRepositoryStructureAsync()` war ausschließlich für `LocalDirectoryPlugin`
implementiert; GitHub/BitBucket-Repositories konnten für die Arbeitsverzeichnis-Auswahl nur die Repository-
Root (`"."`) verwenden.

**Umsetzung:**
- `GitHubPlugin.GetRepositoryStructureAsync(repositoryUrl, maxDepth, ct)`: ermittelt den Standard-Branch
  über das bereits vorhandene `GetDefaultBranchAsync` und ruft die Verzeichnisstruktur rein remote über
  `gh api repos/{owner}/{repo}/git/trees/{branch}?recursive=1` ab (kein lokaler Klon nötig). Filtert auf
  `type == "tree"` (Verzeichnisse) und auf `maxDepth` (Pfadsegment-Anzahl). Loggt eine Warnung bei
  `truncated: true`, statt den Umstand stillschweigend zu ignorieren. Fehler (CLI-Fehler, ungültiges JSON)
  führen zu einer leeren Liste statt einer Exception (konsistent mit `LocalDirectoryPlugin`s
  Fehlerverhalten).
- `BitbucketPlugin.GetRepositoryStructureAsync(repositoryUrl, maxDepth, ct)`: analog über die Bitbucket-
  REST-API (`GET /2.0/repositories/{workspace}/{repo_slug}/src/{branch}/?max_depth={maxDepth}` für Cloud,
  äquivalente `/rest/api/1.0/.../browse`-Route für Self-Hosted). Paginiert über den `next`-Link (Obergrenze
  50 Seiten als Sicherheitsnetz gegen Endlosschleifen bei fehlerhaften API-Antworten). Filtert auf
  `type == "commit_directory"`.
- Beide Plugins erhalten eine private `TryExtractRepositoryId(repositoryUrl)`-Hilfsmethode, die HTTPS-,
  SSH- (GitHub) bzw. Cloud-/Self-Hosted-Browser-URLs (BitBucket) robust in die für die jeweilige API
  benötigte Repository-ID auflöst, ohne bei unbekanntem Format zu werfen (liefert `null`, Aufrufer gibt
  dann eine leere Liste zurück und loggt eine Warnung).
- `plan.md`-Passage „Offener Punkt 1" (siehe unten, ursprünglicher Wortlaut in der Git-Historie) ist damit
  nicht mehr zutreffend und wird als geschlossen markiert.

**Verifikation:** Neue Testklassen `GitHubPluginTests_GetRepositoryStructureAsync` (6 Tests, gemockter
`ICliRunner`, u. a. Tiefenfilterung, Fehlerfall, SSH-URL-Auflösung, `truncated`-Flag, ungültiges JSON) und
`BitbucketPluginTests_GetRepositoryStructureAsync` (6 Tests, u. a. Tiefenfilterung, Pagination über
`next`-Link, Fehlerfall, API-Fehlerobjekt, Self-Hosted-URL-Auflösung).

---

## Offener Punkt 1 (ursprünglich, jetzt geschlossen)

> Ursprünglicher Text (Zyklus 1): "Git-Plugin API wird um `GetRepositoryStructureAsync()` erweitert.
> Vollständig implementiert wird sie für `LocalDirectoryPlugin` ... Remote-Provider-Plugins (GitHub,
> BitBucket) behalten die `NotSupportedException`-Default-Implementierung ... die Verzeichnisauswahl fällt
> für diese Provider automatisch auf Root (`.`) zurück."
>
> **Status jetzt:** Geschlossen. Siehe „Nacharbeiten Zyklus 2 / Punkt 2" oben — `GitHubPlugin` und
> `BitbucketPlugin` implementieren `GetRepositoryStructureAsync()` jetzt vollständig remote, ohne lokalen
> Klon. Die Einschränkung „fällt automatisch auf Root zurück" gilt nur noch, wenn die jeweilige Remote-API
> nicht erreichbar ist oder die Repository-URL nicht geparst werden kann (Fehlerfall, nicht Regelfall).

Restliche Planinhalte (Designentscheidungen, Programmabläufe, Datenbankmigrationen, Validierungsregeln,
Konfigurationsänderungen, Umsetzungsreihenfolge, Tests) unverändert — siehe Git-Historie
(`git show 4ecca88:docs/features/task/issue-98-b8ff2fdc38d04d51a51d41d9a97da10e-unterverzeichnis-fuer-ki-ausfu/plan.md`)
für den vollständigen ursprünglichen Plan.
