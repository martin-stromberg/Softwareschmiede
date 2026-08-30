# Umsetzungsplan: Git-Branch-Erstellung ohne Upstream-Tracking

## Übersicht

Die Methode `GitPluginBase.CreateBranchAsync` wird um das Flag `--no-track` erweitert, um zu verhindern, dass Git beim Checkout eines Task-Branches aus einem Basis-Branch automatisch ein implizites Upstream-Tracking einrichtet. Dies verhindert, dass externe Git-Operationen (z. B. `git push` ohne Zielangabe) versehentlich Commits direkt in den Basis-Branch pushen. Die Änderung ist rein funktional und betrifft nur die Git-Kommandozeilen-Argumente — keine neuen Klassen, Interfaces oder Migrationen sind erforderlich.

## Designentscheidungen

Keine — folgt bestehenden Mustern. Die Änderung ist rein funktional und fügt sich in die bestehende `GitPluginBase`-Implementierung ein, die Git-Kommandos über `RunGitAsync()` ausführt.

## Programmabläufe

### CreateBranchAsync mit sourceBranchName — neuer Ablauf mit --no-track

1. Aufrufer ruft `CreateBranchAsync(localPath, branchName, sourceBranchName)` auf
2. Die Methode konstruiert die Argument-Liste:
   - `["checkout", "-b", branchName, "--no-track", $"origin/{sourceBranchName}"]`
3. `RunGitAsync()` führt `git checkout -b <branchName> --no-track origin/<sourceBranchName>` aus
4. Git checked den Branch aus `origin/<sourceBranchName>` aus, verzweigt lokal als `<branchName>`, **setzt aber kein Upstream-Tracking** (explizit durch `--no-track` deaktiviert)
5. Der neue lokale Branch ist damit eigenständig; `git push` ohne Zielangabe funktioniert nicht (wie erwartet)

Beteiligte Klassen/Komponenten: `GitPluginBase<TPlugin>` (Methode `CreateBranchAsync`), `ICliRunner` (delegiert zu `RunGitAsync`)

### CreateBranchAsync ohne sourceBranchName — unverändert

1. Aufrufer ruft `CreateBranchAsync(localPath, branchName)` auf
2. Die Methode konstruiert die Argument-Liste: `["checkout", "-b", branchName]`
3. `RunGitAsync()` führt `git checkout -b <branchName>` aus
4. Git erzeugt einen lokalen Branch aus dem aktuellen HEAD ohne Upstream-Tracking (schon korekt)

Beteiligte Klassen/Komponenten: `GitPluginBase<TPlugin>` (Methode `CreateBranchAsync`)

## Neue Klassen

Keine.

## Änderungen an bestehenden Klassen

### `GitPluginBase<TPlugin>` (abstrakte Basisklasse)

- **Geänderte Methoden:** `CreateBranchAsync(string localPath, string branchName, string? sourceBranchName = null, CancellationToken ct = default)`
  - Logik (Zeilen 114–116) wird erweitert: Beim Aufbau der `args`-Liste wird für den Fall mit `sourceBranchName` das Flag `--no-track` **vor** dem Remote-Branch-Namen eingefügt.
  - Alte Args (mit sourceBranchName): `["checkout", "-b", branchName, $"origin/{sourceBranchName}"]`
  - Neue Args (mit sourceBranchName): `["checkout", "-b", branchName, "--no-track", $"origin/{sourceBranchName}"]`
  - Args ohne sourceBranchName bleiben unverändert: `["checkout", "-b", branchName]`
  - Fehlerbehandlung bleibt identisch (keine Änderung an `RunGitAsync()`-Aufruf oder Exception-Logik)

## Datenbankmigrationen

Keine.

## Validierungsregeln

Keine.

## Konfigurationsänderungen

Keine.

## Seiteneffekte und Risiken

- **Rückwärts-Kompatibilität:** Die Änderung ist additiv und völlig rückwärts-kompatibel. Bestehende Calls von `CreateBranchAsync` ohne `sourceBranchName` sind nicht betroffen. Calls **mit** `sourceBranchName` verhalten sich nach dem Fix sicherer: Sie erzeugen einen unabhängigen Branch statt eines mit automatischem Upstream-Tracking.
- **Plugin-Vererbung:** `LocalDirectoryPlugin` ruft `base.CreateBranchAsync()` auf und erbt damit automatisch das neue Verhalten. `GitHubPlugin` und `BitBucketPlugin` haben keine Überreitungen und nutzen direkt die Basismethode — keine Änderungen nötig.
- **Push-Methoden:** `GitHubPlugin.PushBranchAsync` und `BitBucketPlugin.PushBranchAsync` verwenden bereits `--set-upstream`, um das Tracking beim Push korrekt zu setzen. Diese Methoden sind durch die Änderung nicht betroffen.
- **Keine bekannten Seiteneffekte:** Das Flag `--no-track` ist ein Git-Standard-Flag und wird von allen modernen Git-Versionen (ab Git 1.7.0+) unterstützt. Keine Kompatibilitätsprobleme zu erwarten.

## Umsetzungsreihenfolge

1. **Code-Änderung in GitPluginBase.CreateBranchAsync**
   - Voraussetzungen: Keine (die Klasse und Methode existieren bereits)
   - Beschreibung: Zeilen 114–116 in `src/Softwareschmiede.Plugin.Contracts/Domain/Abstractions/GitPluginBase.cs` anpassen — `--no-track` in die args-Liste einfügen für den Fall mit `sourceBranchName`. Ein Inline-Kommentar hinzufügen, das erklärt, warum `--no-track` notwendig ist (um automatisches Upstream-Tracking zu verhindern).

2. **Neue Unit-Tests hinzufügen**
   - Voraussetzungen: Code-Änderung aus Schritt 1
   - Beschreibung: 
     - Test `CreateBranchAsync_ShouldIncludeNoTrackFlag_WhenSourceBranchNameProvided` hinzufügen in `GitPluginBaseTests`, der prüft, dass `--no-track` in der Argument-Liste für den Fall mit `sourceBranchName` vorhanden ist
     - Test-Struktur: Mock `ICliRunner`, rufe `CreateBranchAsync(localPath, "feature/x", "staging")` auf, verifiziere, dass die args-Liste exakt `["checkout", "-b", "feature/x", "--no-track", "origin/staging"]` entspricht

3. **Bestehende Unit-Tests überprüfen und ggf. anpassen**
   - Voraussetzungen: Code-Änderung aus Schritt 1
   - Beschreibung: Die drei bestehenden Tests in `GitPluginBaseTests` (`CreateBranchAsync_ShouldRunCheckoutMinusB`, `CreateBranchAsync_ShouldThrow_WhenGitCheckoutFails`, `CreateBranchAsync_ShouldPropagateCancellation`) erwarten weiterhin `["checkout", "-b", "feature/x"]` (ohne sourceBranchName), daher sind sie **nicht betroffen** und benötigen **keine Anpassung**.

4. **Build ausführen und Tests laufen lassen**
   - Voraussetzungen: Alle obigen Schritte
   - Beschreibung: `dotnet build Softwareschmiede.slnx` ausführen, um Compile-Fehler auszuschließen. Danach `dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category!=OsInterface"` ausführen, um Unit-Tests zu verifizieren. Verifizieren, dass alle Tests bestehen.

5. **Code-Review und Merge vorbereiten**
   - Voraussetzungen: Alle Schritte 1–4
   - Beschreibung: Die Änderungen sind minimal und fokussiert (ein Git-Flag hinzufügen + ein neuer Unit-Test). PR erstellen, Code-Review durchlaufen lassen, danach auf main mergen.

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `CreateBranchAsync_ShouldIncludeNoTrackFlag_WhenSourceBranchNameProvided` | `GitPluginBaseTests` | Prüft, dass die Argument-Liste `["checkout", "-b", "feature/x", "--no-track", "origin/staging"]` ist, wenn `CreateBranchAsync(localPath, "feature/x", "staging")` aufgerufen wird. Mock des `ICliRunner` simuliert erfolgreiche Git-Ausführung. |

### Betroffene bestehende Tests

Keine. Die bestehenden Tests prüfen den Fall **ohne** `sourceBranchName` und sind durch die Änderung nicht betroffen.

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| — | Keine Anpassung erforderlich |

### E2E-Tests (primärer Funktionsnachweis)

Diese Anforderung betrifft nur die Git-Kommandozeilen-Logik (Argument-Konstruktion) und keine Benutzerinteraktion über die UI. Die Änderung wird vollständig durch Unit-Tests abgedeckt (Mock des `ICliRunner` prüft die Argument-Listen). E2E-Tests sind **nicht erforderlich**, da:

1. Die Änderung rein technisch ist und keinen Benutzerfluss betrifft
2. Die Argument-Liste durch Unit-Tests vollständig verifizierbar ist
3. Das tatsächliche Git-Verhalten (kein Upstream-Tracking nach `--no-track`) ist aus dem Unit-Test ableitbar

Eine Integration-Test-Validierung (z. B. in `LocalDirectoryPluginIntegrationTests`) könnte optional durchgeführt werden, um live zu verifizieren, dass `git branch -vv` nach `CreateBranchAsync` mit `sourceBranchName` **kein** Upstream-Tracking anzeigt — dies ist aber sekundär und wird von der Argument-Verifizierung im Unit-Test bereits implizit gewährleistet.

| Priorität | Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium | Warum E2E nötig ist |
|-----------|----------|------------------------|-------------------------------|-------------------|
| — | Entfällt | — | — | Keine E2E-Tests erforderlich (rein technische, nicht UI-basierte Änderung) |

Welche bestehenden E2E-Tests müssen angepasst werden?

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| — | Keine |

## Offene Punkte

Keine. Alle technischen Punkte aus der Anforderung sind geklärt:

- ✓ BitBucket-Plugin: Keine `CreateBranchAsync`-Override vorhanden (verifiziert)
- ✓ Push-Methoden: `GitHubPlugin.PushBranchAsync` und `BitBucketPlugin.PushBranchAsync` verwenden bereits `--set-upstream` (bestätigt im Quellcode)
- ✓ Test-Abdeckung für `sourceBranchName`: Neuer Test wird hinzugefügt
- ✓ Integration-Tests: Unit-Test reicht aus (keine Benutzerfluss-Änderung)
- ✓ Dokumentation: Inline-Kommentar in der Methode ergänzen
