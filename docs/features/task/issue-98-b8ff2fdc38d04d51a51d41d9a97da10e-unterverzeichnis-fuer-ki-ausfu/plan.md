# Umsetzungsplan: Unterverzeichnis für KI-Ausführung

## Übersicht

Die Anwendung wird um die Fähigkeit erweitert, für jedes Git-Repository in einem Projekt ein Arbeitsverzeichnis relativ zum Wurzelverzeichnis des geklonten Repositories zu definieren und zu speichern. Nach dem Klonen führt die CLI-Ausführung nicht im Root-Verzeichnis, sondern im konfigurierten Unterverzeichnis aus. Die UI ermöglicht Benutzern, die Verzeichnisstruktur des externen Repositories voraus zu laden und ein Zielverzeichnis zu wählen. Das Feature umfasst Datenbankmigrationen, Service-Layer-Erweiterungen, ViewModel-Logik und UI-Komponenten.

---

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Speicherung des Arbeitsverzeichnisses | Nullable Property `WorkingDirectoryRelativePath` in `RepositoryStartKonfiguration` | `null` repräsentiert das Repository-Root, erlaubt Kompatibilität mit bestehenden Konfigurationen ohne explizites Setzen |
| Path-Traversal Prevention | `Path.GetFullPath()` + `StartsWith()`-Validierung | Sicherheitsstandard für Pfad-Validierung; verhindert `../../../etc`-Angriffe |
| Verzeichnis-Struktur-Abruf | Neue Service-Klasse `DirectoryStructureBrowserService` mit `IMemoryCache` | Repository-Pattern: Isolierung der Abruf-Logik; Caching reduziert API-Aufrufe |
| Abruf-Tiefe | Konfigurierbar via `appsettings` (Default: 2 Ebenen) | Balanciert Benutzerfreundlichkeit (ausreichend für typische Monorepos) mit Performance |
| Arbeitsverzeichnis-Auswahl UI | Optional mit Default auf Root (`"."`) | Abwärtskompatiblität; Benutzer können Root wählen, wenn kein Unterverzeichnis relevant ist |
| `StartScriptRelativePath` Interpretation | Bleibt relativ zur Repository-Root, nicht zum Arbeitsverzeichnis | Vereinfachte Logik; Startskripte sind meist im Root oder direkt unter Root |
| Fehlerbehandlung beim fehlenden Verzeichnis | Fehler mit `DirectoryNotFoundException` + Logging | Explizite Fehlersignalisierung; verhindert stille Fallbacks |

---

## Programmabläufe

### Arbeitsverzeichnis beim CLI-Start auflösen

1. `KiAusfuehrungsService.StartCliAsync()` wird aufgerufen mit `RepositoryStartKonfiguration? startConfig`
2. Falls `startConfig?.WorkingDirectoryRelativePath` gesetzt:
   - `ResolveEffectiveWorkingDirectory(localRepoPath, relativePath)` wird aufgerufen
   - `Path.Combine(localRepoPath, relativePath)` wird zu absolutem Pfad normalisiert
   - Sicherheitsprüfung: Prüfe, dass normalisierter Pfad mit normalisiertem `localRepoPath` beginnt (Path-Traversal Prevention)
   - Falls Validierung fehlschlägt: `InvalidOperationException` werfen
3. Falls Validierung erfolgreich: `ValidateWorkingDirectory(effectiveWorkdir, localRepoPath)`
   - Prüfe, ob Verzeichnis existiert
   - Falls nicht vorhanden: `DirectoryNotFoundException` werfen
4. Übergabe des effektiven Pfads an `PseudoConsoleProcessStarter` als Working Directory
5. Prozess wird im angegebenen Verzeichnis ausgeführt

Beteiligte Klassen/Komponenten: `KiAusfuehrungsService`, `PseudoConsoleProcessStarter`, `RepositoryStartKonfiguration`, `Path`

### Verzeichnisstruktur abrufen und in der UI anzeigen

1. Benutzer öffnet `RepositoryAssignDialog`
2. Benutzer wählt Plugin und dann externes Repository via `SelectedRepository`
3. ViewModel-Setter `SelectedRepository` ruft `OnSelectedRepositoryChanged()` auf:
   - `SelectedWorkingDirectory` wird auf `null` gesetzt (Reset)
   - Alte `CancellationTokenSource` wird abgebrochen und disposed
   - Neue `CancellationTokenSource` wird erzeugt
   - `LoadDirectoryStructureAsync()` wird asynchron gestartet
4. `LoadDirectoryStructureAsync()` führt folgendes aus:
   - Falls Plugin oder Repository nicht verfügbar: Collections leeren, Task endet
   - `IsLoadingDirectoryStructure = true`
   - `DirectoryStructureBrowserService.GetDirectoriesAsync(plugin, repository.Url, ct)` wird aufgerufen
   - Falls gecacht: Direkter Rückgabe aus Cache
   - Falls nicht gecacht: Service ruft `IGitPlugin.GetRepositoryStructureAsync()` auf, cacht Ergebnis (5 Min TTL), gibt zurück
   - Rückgabe wird in `AvailableWorkingDirectories` eingefüllt, mit `"."` (Root) am Anfang
   - `SelectedWorkingDirectory` wird auf `"."` (Default) gesetzt
   - `IsLoadingDirectoryStructure = false`
5. UI zeigt Loading-Indikator während Abruf, dann ComboBox mit Verzeichnissen

Beteiligte Klassen/Komponenten: `RepositoryAssignViewModel`, `DirectoryStructureBrowserService`, `IGitPlugin`, `IMemoryCache`, `RepositoryAssignDialog.xaml`

### Verzeichnis-Validierung nach Git-Klon

1. Nach erfolgreichem Git-Klon wird `GitOrchestrationService.ValidateWorkingDirectoryAfterClone()` aufgerufen
2. Falls `RepositoryStartKonfiguration.WorkingDirectoryRelativePath` gesetzt:
   - `ResolveEffectiveWorkingDirectory(clonePath, relativePath)` wird aufgerufen (wie oben)
   - Falls Pfad ungültig: Exception werfen, Klon als fehlgeschlagen markieren
   - Falls Verzeichnis nicht existiert: `DirectoryNotFoundException` werfen, Fehler protokollieren
3. Falls Validierung bestanden: Klon ist erfolgreich, Verzeichnis steht zur Verfügung

Beteiligte Klassen/Komponenten: `GitOrchestrationService`, `KiAusfuehrungsService`, `RepositoryStartKonfiguration`

---

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `DirectoryStructureBrowserService` | Service (Application Layer) | Abruf und Caching von Verzeichnisstrukturen aus externen Repositories über Git-Plugins |

---

## Änderungen an bestehenden Klassen

### `RepositoryStartKonfiguration` (Entity)

- **Neue Eigenschaften:**
  - `WorkingDirectoryRelativePath` (`string?`) — Relativer Pfad zum Arbeitsverzeichnis innerhalb des Repositories; `null` = Repository-Root. Beispiele: `"backend"`, `"apps/cli"`, `"."` (expliziter Root)

### `SoftwareschmiededDbContext` (DbContext Configuration)

- **Änderungen in Entity-Konfiguration:**
  - Property `WorkingDirectoryRelativePath` konfigurieren: `HasMaxLength(512)`
  - Optional: IsRequired(false) explizit setzen (Standard bei nullable String)

### `KiAusfuehrungsService` (Service)

- **Geänderte Methoden:**
  - `StartCliAsync(aufgabeId, kiPlugin, localRepoPath, optionalParameters, ct)` → Neuer Parameter hinzufügen: `RepositoryStartKonfiguration? startConfig = null`
  - `StartWithPseudoConsoleAsync(aufgabeId, kiPlugin, localRepoPath, optionalParameters, ct)` → Neuer Parameter hinzufügen: `RepositoryStartKonfiguration? startConfig = null`

- **Neue Methoden:**
  - `ResolveEffectiveWorkingDirectory(string repositoryRoot, string? relativePath)` : `string` — Kombiniert Wurzel-Pfad mit relativem Pfad, normalisiert, validiert gegen Path-Traversal. Rückgabe: absoluter normalisierter Pfad. Wirft `InvalidOperationException` bei Validierungsfehlern.
  - `ValidateWorkingDirectory(string effectiveWorkdir, string repositoryRoot)` : `void` — Prüft, ob Verzeichnis existiert. Wirft `DirectoryNotFoundException`, wenn nicht vorhanden.

- **Private Methoden-Anpassung:**
  - `StartPseudoConsoleProcess()` → Muss das effektive Arbeitsverzeichnis verwenden, nicht blindlings `localRepoPath`

### `GitOrchestrationService` (Service)

- **Neue Methode:**
  - `ValidateWorkingDirectoryAfterCloneAsync(string clonePath, RepositoryStartKonfiguration? startConfig, CancellationToken ct)` : `Task` — Ruft `ResolveEffectiveWorkingDirectory()` und `ValidateWorkingDirectory()` auf. Fehlerbehandlung mit Logging.

### `RepositoryAssignViewModel` (ViewModel)

- **Neue Eigenschaften:**
  - `AvailableWorkingDirectories` (`ObservableCollection<string>`, read-only) — Zeigt verfügbare Verzeichnisse des ausgewählten Repositories
  - `SelectedWorkingDirectory` (`string?`, Default: `null`) — Speichert vom Benutzer ausgewählten relativen Pfad
  - `IsLoadingDirectoryStructure` (`bool`, Default: `false`) — Zeigt, ob Verzeichnisstruktur gerade abgerufen wird
  - `CurrentLoadDirectoryStructureTask` (`Task?`, Internal) — Speichert laufenden Task für Tests

- **Geänderte Methoden/Properties:**
  - `SelectedRepository` (Setter) → Ruft `OnSelectedRepositoryChanged()` auf (neue Event-Handler-Logik)

- **Neue Methoden:**
  - `LoadDirectoryStructureAsync(CancellationToken ct)` : `Task` — Abrufen der Verzeichnisstruktur und Befüllung von `AvailableWorkingDirectories`
  - `OnSelectedRepositoryChanged()` : `void` — Wird aufgerufen, wenn `SelectedRepository` sich ändert; bricht alte Abrufe ab, startet neue

- **Private Felder:**
  - `_availableWorkingDirectories` : `ObservableCollection<string>`
  - `_selectedWorkingDirectory` : `string?`
  - `_isLoadingDirectoryStructure` : `bool`
  - `_dirStructureCts` : `CancellationTokenSource?` — Abbrechung des Abrufs bei Wechsel
  - `_directoryStructureService` : `DirectoryStructureBrowserService` (Dependency Injection)

### `RepositoryAssignDialog.xaml` (UI)

- **Grid-Layout-Anpassung:**
  - Neue RowDefinition hinzufügen (4. Zeile) für Arbeitsverzeichnis-Auswahl

- **Neue UI-Elemente nach Repository-Liste (vor Buttons):**
  - TextBlock: Label "Arbeitsverzeichnis im Repository"
  - ComboBox: Binding zu `AvailableWorkingDirectories` (ItemsSource), `SelectedWorkingDirectory` (SelectedItem), IsEnabled Binding invertiert von `IsLoadingDirectoryStructure`
  - ProgressRing/LoadingIndicator: Binding zu `IsLoadingDirectoryStructure`
  - TextBlock: Info-Text "Hinweis: '.' bedeutet Wurzelverzeichnis des Repositories"
  - Optional: ToolTip mit Erklärung

---

## Datenbankmigrationen

| Migrationsname | Betroffene Tabellen/Spalten | Beschreibung der Änderung |
|----------------|----------------------------|---------------------------|
| `AddWorkingDirectoryToRepositoryStartKonfiguration` | `RepositoryStartConfigurations.WorkingDirectoryRelativePath` | Neue nullable NVARCHAR(MAX) Spalte für relativen Pfad zum Arbeitsverzeichnis. Existierende Zeilen erhalten `NULL` (bedeutet Repository-Root). |

SQL:
```sql
ALTER TABLE RepositoryStartConfigurations
ADD WorkingDirectoryRelativePath NVARCHAR(MAX) NULL;
```

---

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| `WorkingDirectoryRelativePath` | Falls gesetzt: Darf nicht `null` oder leer sein (nach Trimmen) | Leerer String wird als `null` behandelt oder abgelehnt |
| `WorkingDirectoryRelativePath` | Darf nicht außerhalb des Repository-Root liegen (Path-Traversal Prevention: `normalized.StartsWith(normalizedRoot)`) | Exception `InvalidOperationException` |
| Effektives Arbeitsverzeichnis | Muss nach dem Klon existieren | Exception `DirectoryNotFoundException` |
| Relative Pfade in UI-Eingabe | Falls freie Eingabe erlaubt: Gleiche Regeln wie oben | Validierung beim Speichern oder Submit |

---

## Konfigurationsänderungen

| Eintrag | Typ | Standardwert | Zweck |
|---------|-----|--------------|-------|
| `DirectoryStructureCacheDurationSeconds` | `int` | `300` | Caching-Dauer für abgerufene Verzeichnisstrukturen (in Sekunden) |
| `DirectoryStructureMaxDepth` | `int` | `2` | Maximale Verzeichnis-Tiefe beim Abruf der Repository-Struktur |
| `DirectoryStructureEnabled` | `bool` | `true` | Schalter zur Aktivierung/Deaktivierung der Verzeichnisstruktur-Voraus-Ladung |

Diese Einträge werden in `appsettings.json` hinzugefügt. `DirectoryStructureBrowserService` liest diese Werte über `IOptions<>` Pattern (Configuration-Klasse erforderlich).

---

## Seiteneffekte und Risiken

- **Path-Traversal-Sicherheit:** Die Validierungslogik in `ResolveEffectiveWorkingDirectory()` muss auf Windows und Unix korrekt funktionieren. `StringComparison.OrdinalIgnoreCase` auf Windows ist korrekt, auf Unix sollte `StringComparison.Ordinal` verwendet werden. Mitigation: Cross-Platform-Test durchführen.

- **Abhängigkeit von Git-Plugin API:** Feature setzt voraus, dass `IGitPlugin` eine Methode `GetRepositoryStructureAsync()` oder ähnlich bereitstellt. Falls diese Methode nicht existiert, muss zunächst das Interface erweitert werden. Mitigation: Offener Punkt, Klärung erforderlich.

- **Caching-Verhalten:** Wenn sich die Repository-Struktur ändert, wird der Cache nicht automatisch invalidiert (5 Min TTL). Benutzer sehen möglicherweise veraltete Verzeichnisse. Mitigation: TTL ist kurz genug, Manual-Refresh-Button ist optional (nicht in MVP enthalten).

- **UI-Responsivität:** Abruf der Verzeichnisstruktur erfolgt asynchron; während des Abrufes ist die ComboBox deaktiviert. Falls Abruf lange dauert, kann Benutzer nicht interagieren. Mitigation: Loading-Indikator zeigt Status; ProgressRing gibt visuelles Feedback.

- **Bestandsdaten:** Bestehende `RepositoryStartKonfiguration`-Einträge ohne `WorkingDirectoryRelativePath` (werden auf `NULL` migriert) werden als Root interpretiert. Keine Datenverluste, aber implizites Verhalten. Mitigation: Dokumentation / Mitigation Note in Release Notes.

- **`StartScriptRelativePath`-Interpretation:** `StartScriptRelativePath` bleibt relativ zur Repository-Root, nicht zum Arbeitsverzeichnis. Falls Benutzer ein Unterverzeichnis wählt und ein Startskript dort erwartet, wird das Skript nicht gefunden (es liegt Root relativ). Mitigation: Dokumentation der Erwartung; Future Enhancement: Konfigurierbares Verhalten.

- **Fehlerbehandlung bei fehlgeschlagenem Abruf:** Falls `DirectoryStructureBrowserService.GetDirectoriesAsync()` fehlschlägt (z.B. Private Repo ohne Auth), werden nur `"."` (Root) zurückgegeben. Benutzer kann dann nur Root wählen, nicht aber feststellen, ob andere Verzeichnisse verfügbar sind. Mitigation: Fehler-Logging; Optional: UI-Fehler-Message.

---

## Umsetzungsreihenfolge

1. **Entity & Migration: `WorkingDirectoryRelativePath` hinzufügen**
   - Voraussetzungen: EF Core Migration-Infrastruktur vorhanden
   - Beschreibung: Property in `RepositoryStartKonfiguration` hinzufügen, DbContext konfigurieren, Migration erzeugen und Code einbinden

2. **Configuration-Klasse erstellen**
   - Voraussetzungen: `IOptions<>` Pattern bekannt, `appsettings.json` zugänglich
   - Beschreibung: Configuration-Klasse (z.B. `DirectoryStructureOptions`) mit Eigenschaften `CacheDurationSeconds`, `MaxDepth`, `Enabled` erstellen. In `appsettings.json` Standardwerte eintragen. Configuration in Startup/DI registrieren.

3. **`DirectoryStructureBrowserService` implementieren**
   - Voraussetzungen: `IPluginManager` verfügbar, `IMemoryCache` registriert, `ILogger<>` registriert
   - Beschreibung: Neue Service-Klasse mit `GetDirectoriesAsync()` Methode. Abhängigkeiten via DI injizieren. Caching-Logik mit TTL implementieren. Fehlerbehandlung mit Fallback auf leere Liste oder nur Root.

4. **`KiAusfuehrungsService` erweitern**
   - Voraussetzungen: Service existiert, neue Parameter-Signatures müssen rückwärts-kompatibel sein (optionale Parameter mit Default `null`)
   - Beschreibung: `ResolveEffectiveWorkingDirectory()` und `ValidateWorkingDirectory()` Hilfsmethoden hinzufügen. `StartCliAsync()` und `StartWithPseudoConsoleAsync()` um `RepositoryStartKonfiguration? startConfig` Parameter erweitern. `StartPseudoConsoleProcess()` anpassen, um effektives Arbeitsverzeichnis zu verwenden.

5. **`GitOrchestrationService` erweitern**
   - Voraussetzungen: Service existiert, `KiAusfuehrungsService`-Hilfsmethoden vorhanden
   - Beschreibung: Neue Methode `ValidateWorkingDirectoryAfterCloneAsync()` hinzufügen. Diese wird nach erfolgreichem Klon aufgerufen, um Pfad zu validieren.

6. **`RepositoryAssignViewModel` erweitern**
   - Voraussetzungen: ViewModel existiert, `DirectoryStructureBrowserService` verfügbar, `CancellationTokenSource` bekannt
   - Beschreibung: Neue Properties `AvailableWorkingDirectories`, `SelectedWorkingDirectory`, `IsLoadingDirectoryStructure`, `CurrentLoadDirectoryStructureTask` hinzufügen. `SelectedRepository`-Setter um Callback `OnSelectedRepositoryChanged()` erweitern. `LoadDirectoryStructureAsync()` Methode implementieren. Dependency Injection von `DirectoryStructureBrowserService` anpassen.

7. **`RepositoryAssignDialog.xaml` erweitern**
   - Voraussetzungen: Dialog existiert, ViewModel-Properties verfügbar
   - Beschreibung: Grid-Layout mit zusätzlicher Row für Arbeitsverzeichnis. ComboBox für `AvailableWorkingDirectories` mit Binding auf `SelectedWorkingDirectory`. ProgressRing für `IsLoadingDirectoryStructure`. Info-TextBlock. UI-Elemente deaktiviert während Laden.

8. **`RepositoryAssignViewModel`-Tests erweitern**
   - Voraussetzungen: Testklasse existiert, Test-Infrastruktur (Mocks, Fixtures) vorhanden
   - Beschreibung: Tests für `SelectedRepository`-Wechsel (triggert Laden), Directory-Struktur-Befüllung, `SelectedWorkingDirectory`-Reset, Fehlerbehandlung. Mock `DirectoryStructureBrowserService` für verschiedene Szenarien.

9. **`DirectoryStructureBrowserService`-Tests schreiben**
   - Voraussetzungen: Test-Framework vorhanden, Mock `IGitPlugin` verfügbar
   - Beschreibung: Tests für erfolgreichen Abruf, Caching-Verhalten (TTL), Fehlerbehandlung, Fallback auf leere Liste. Mock `IGitPlugin.GetRepositoryStructureAsync()`.

10. **`KiAusfuehrungsService`-Tests erweitern**
    - Voraussetzungen: Testklasse existiert
    - Beschreibung: Tests für `ResolveEffectiveWorkingDirectory()` (korrekte Kombinierung, Path-Traversal Prevention, Edge-Cases). Tests für `ValidateWorkingDirectory()` (existierendes/nicht-existierendes Verzeichnis). Tests für `StartCliAsync()` mit `startConfig` Parameter.

11. **`GitOrchestrationService`-Tests erweitern**
    - Voraussetzungen: Testklasse existiert
    - Beschreibung: Tests für `ValidateWorkingDirectoryAfterCloneAsync()` — Erfolgsfall, Fehlerfall (fehlende Verzeichnis), Fehlerfall (Path-Traversal).

12. **E2E-Tests: Repository-Zuweisung mit Arbeitsverzeichnis-Auswahl**
    - Voraussetzungen: E2E-Test-Framework vorhanden, TestPlugin mock verfügbar
    - Beschreibung: Benutzer öffnet Dialog, wählt Plugin, wählt Repository, wartet auf Abruf der Verzeichnisstruktur, wählt Arbeitsverzeichnis, speichert Zuweisung. Verify: `RepositoryStartKonfiguration.WorkingDirectoryRelativePath` ist in Datenbank gespeichert.

13. **E2E-Tests: CLI-Ausführung mit Arbeitsverzeichnis**
    - Voraussetzungen: CLI-Ausführung-E2E-Tests vorhanden
    - Beschreibung: Test mit `WorkingDirectoryRelativePath` gesetzt vs. nicht gesetzt. Verify: Prozess läuft im korrekten Verzeichnis (z.B. prüfe Prozess-Working-Directory oder führe Kommando aus, das aktuelles Verzeichnis prüft).

---

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `ResolveEffectiveWorkingDirectory_ShouldCombinePaths` | `KiAusfuehrungsServiceTests` | Kombiniert Repository-Root + relativen Pfad korrekt zu absolutem Pfad |
| `ResolveEffectiveWorkingDirectory_ShouldRejectPathTraversal` | `KiAusfuehrungsServiceTests` | Wirft Exception bei Pfad-Traversal-Versuchen (z.B. `"../../../etc"`) |
| `ResolveEffectiveWorkingDirectory_ShouldAcceptDotAsRoot` | `KiAusfuehrungsServiceTests` | `"."` wird korrekt als Root interpretiert |
| `ValidateWorkingDirectory_ShouldThrowWhenNotExists` | `KiAusfuehrungsServiceTests` | Wirft `DirectoryNotFoundException`, wenn Verzeichnis nicht existiert |
| `ValidateWorkingDirectory_ShouldSucceedWhenExists` | `KiAusfuehrungsServiceTests` | Keine Exception, wenn Verzeichnis existiert |
| `GetDirectoriesAsync_ShouldReturnDirectories` | `DirectoryStructureBrowserServiceTests` | Service gibt Liste von Verzeichnis-Pfaden zurück |
| `GetDirectoriesAsync_ShouldCache_WithTTL` | `DirectoryStructureBrowserServiceTests` | Zweiter Abruf kommt aus Cache, TTL wird respektiert |
| `GetDirectoriesAsync_ShouldHandleErrors_Gracefully` | `DirectoryStructureBrowserServiceTests` | Bei Fehler wird leere Liste zurückgegeben, kein Exception geworfen |
| `GetDirectoriesAsync_ShouldCallPluginMethod` | `DirectoryStructureBrowserServiceTests` | Ruft `IGitPlugin.GetRepositoryStructureAsync()` auf |
| `SelectedRepositoryChanged_ShouldLoadDirectoryStructure` | `RepositoryAssignViewModelTests` | Beim Ändern von `SelectedRepository` wird `LoadDirectoryStructureAsync()` aufgerufen |
| `SelectedRepositoryChanged_ShouldResetSelectedWorkingDirectory` | `RepositoryAssignViewModelTests` | `SelectedWorkingDirectory` wird auf `null` gesetzt |
| `SelectedRepositoryChanged_ShouldCancelPreviousLoad` | `RepositoryAssignViewModelTests` | Alte CancellationTokenSource wird abgebrochen |
| `LoadDirectoryStructureAsync_ShouldSetIsLoading_Flag` | `RepositoryAssignViewModelTests` | `IsLoadingDirectoryStructure` wird während Abruf auf `true` gesetzt, danach `false` |
| `LoadDirectoryStructureAsync_ShouldPopulateDirectories_WithDotRoot` | `RepositoryAssignViewModelTests` | `AvailableWorkingDirectories` wird mit `"."` (Root) + Abruf-Ergebnis befüllt |
| `LoadDirectoryStructureAsync_ShouldSetDefaultSelectedDirectory` | `RepositoryAssignViewModelTests` | `SelectedWorkingDirectory` wird auf `"."` (Default) gesetzt |
| `LoadDirectoryStructureAsync_ShouldHandleNullRepository` | `RepositoryAssignViewModelTests` | Bei `SelectedRepository = null` wird Collection geleert, kein Exception |
| `LoadDirectoryStructureAsync_ShouldHandleErrors_WithLogging` | `RepositoryAssignViewModelTests` | Bei Service-Fehler wird Collection mit nur `"."` befüllt, Fehler geloggt |
| `StartCliAsync_ShouldUseEffectiveWorkingDirectory` | `KiAusfuehrungsServiceTests` | Prozess wird mit effektivem Arbeitsverzeichnis gestartet (wenn `startConfig` gesetzt) |
| `StartCliAsync_ShouldUseRepoRootWhenConfigNull` | `KiAusfuehrungsServiceTests` | Prozess verwendet Repository-Root, wenn `startConfig = null` |
| `ValidateWorkingDirectoryAfterClone_ShouldThrowWhenDirectoryNotFound` | `GitOrchestrationServiceTests` | Wirft Exception, wenn Verzeichnis nach Klon nicht vorhanden |
| `ValidateWorkingDirectoryAfterClone_ShouldLogError` | `GitOrchestrationServiceTests` | Fehler wird geloggt (Logger.LogWarning/Error) |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `TestCliStartAsync` (KiAusfuehrungsServiceTests) | Methodensignatur ändert sich: neuer Parameter `startConfig`. Test muss `null` übergeben, um bisheriges Verhalten zu prüfen. |
| `StartCliAsync_ShouldReturnHandle_WhenPluginProvidesValidProcessStartInfo` | Neuer Parameter in Signatur; Test muss angepasst werden. |
| `LadenAsync_*` (RepositoryAssignViewModelTests) | Tests können unverändert bleiben, da neue Properties unabhängig sind. Optional: Extend zu prüfen, dass `AvailableWorkingDirectories` bei Start leer ist. |

### E2E-Tests (Pflicht)

| Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium |
|----------|------------------------|-------------------------------|
| Repository-Dialog öffnen, Repository wählen, Verzeichnisstruktur wird geladen | `RepositoryAssignE2ETests` (neu oder erweitert) | Verzeichnisse werden korrekt abgerufen und angezeigt |
| Arbeitsverzeichnis auswählen und Zuweisung speichern | `RepositoryAssignE2ETests` | `WorkingDirectoryRelativePath` wird in Datenbank gespeichert |
| CLI-Prozess mit Arbeitsverzeichnis starten (Happy Path) | `KiAusfuehrungsServiceE2ETests` (neu oder erweitert) | Prozess läuft im konfigurierten Unterverzeichnis (nicht Root) |
| CLI-Prozess ohne Arbeitsverzeichnis starten (Default Root) | `KiAusfuehrungsServiceE2ETests` | Prozess läuft im Repository-Root (Abwärtskompatibilität) |
| Fehlerfall: Angegebenes Arbeitsverzeichnis existiert nicht nach Klon | `GitOrchestrationE2ETests` (neu oder erweitert) | Fehler wird mit aussagekräftiger Meldung geworfen |
| Fehlerfall: Path-Traversal-Versuch wird abgelehnt | `KiAusfuehrungsServiceE2ETests` | Exception wird geworfen, Prozess wird nicht gestartet |

Welche bestehenden E2E-Tests müssen angepasst werden:

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| Alle E2E-Tests für CLI-Ausführung | Falls Tests hart-kodiert `localRepoPath` als Working Directory erwarten: Müssen angepasst werden, um Flexibilität zu erlauben. In der Regel sollten Tests `workingDirectory` oder die verwendete Property nicht explizit prüfen, sondern nur Prozess-Output prüfen. |
| Repository-Zuweisung E2E-Tests | Falls vorhanden: Müssen erweitert werden, um neue Arbeitsverzeichnis-Auswahl zu prüfen. |

---

## Offene Punkte

Alle ursprünglich offenen Punkte wurden mit dem Anwender geklärt; die jeweils empfohlenen Vorschläge wurden bestätigt und sind bereits in die obigen Abschnitte (Designentscheidungen, Programmabläufe, Validierungsregeln, Konfigurationsänderungen) eingearbeitet:

1. Git-Plugin API wird um `GetRepositoryStructureAsync()` erweitert (alle Plugins implementieren sie).
2. Verzeichnistiefe: 2 Ebenen (konfigurierbar).
3. Fehlerbehandlung bei Abruf-Fehler: nur ComboBox mit Root-Fallback, keine manuelle Texteingabe.
4. Path-Traversal-Schutz per `StartsWith()`-Validierung ist ausreichend (kein Symlink-Handling im MVP).
5. Bestandsdaten ohne `WorkingDirectoryRelativePath` werden als `null` (= Root) behandelt, keine explizite Migration nötig.
6. `StartScriptRelativePath` bleibt relativ zur Repository-Root, nicht zum Arbeitsverzeichnis.
7. Arbeitsverzeichnis-Auswahl ist optional mit Default Root.
8. Kein manueller Refresh-Button im MVP; Cache-TTL 5 Minuten.

---

## Notizen zur Implementierung

- **Abhängigkeits-Reihenfolge:** Schritte 1–3 sind Voraussetzungen für die Service-Implementierung (Schritte 4–5). Schritte 4–5 sind Voraussetzung für UI-Komponenten (Schritt 6). Tests können parallel zu Implementierung geschrieben werden.

- **Git-Plugin Integration (Offener Punkt 1):** Dies ist der kritische Pfad. Falls `IGitPlugin.GetRepositoryStructureAsync()` nicht existiert, muss diese zuerst implementiert werden. Dies betrifft alle Plugins (GitHub, Bitbucket, GitLab). Empfehlung: Klärung vor Start der Implementierung.

- **Path-Handling auf Windows vs. Unix:** Tests sollten auf beiden Plattformen durchgeführt werden. `Path.Combine()` und `Path.GetFullPath()` sind plattformsicher, aber String-Vergleiche müssen korrekt konfiguriert werden (siehe Schritt 4).

- **Rückwärts-Kompatibilität:** Alle Änderungen an `KiAusfuehrungsService` und `GitOrchestrationService` müssen rückwärts-kompatibel sein (optionale Parameter, Default-Werte). Bestehende Aufrufe ohne neue Parameter dürfen nicht brechen.

- **Logging:** Alle neuen Fehlerbehandlungen sollten via `ILogger<>` geloggt werden. Dies hilft bei Diagnose in Produktion.

- **E2E-Test-Infrastruktur:** E2E-Tests müssen mit TestPlugins arbeiten können, die `GetRepositoryStructureAsync()` mocken. Bestehende TestPlugin-Infrastruktur muss evtl. erweitert werden.
