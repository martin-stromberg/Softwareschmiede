# Tests

## Testklassen

### `AutonomAufgabenInitialisierungsServiceTests`
Datei: `src/Softwareschmiede.Tests/Application/Services/AutonomAufgabenInitialisierungsServiceTests.cs`

#### Testmethoden zur Verzeichnisstruktur und Dateianlage:
- `InitialisiereAsync_ErzeugtArbeitsverzeichnis` — Validiert, dass alle erforderlichen Verzeichnisse angelegt werden (skills/, clones/, tasks/, logs/)
- `InitialisiereAsync_ErzeugtRepositoryKlon` — Testet, dass der Klon im `clones/repo_main/` Verzeichnis angelegt wird und das Plugin korrekt aufgerufen wird
- `InitialisiereAsync_KlontDirectVonRepositoryUrl` — Validiert, dass direkt von der `aufgabe.GitRepository.RepositoryUrl` geklont wird, nicht von einem lokalen Pfad

#### Testmethoden zur Branch-Erstellung:
- `InitialisiereAsync_ErstelltProjektBranchNachKlon` — Testet, dass nach dem Klon der Projektbranch via `CreateBranchAsync` erstellt wird
- `ErstelleProjektbranchAsync_AnlegtNeuenBranchMitGit` — Testet, dass neuer lokaler Branch anlegt wird, wenn Branch nicht remote existiert
- `ErstelleProjektbranchAsync_UeberspringtAnlage_WennLokalerBranchBereitsExistiert` — **Testet Idempotenz**: Wenn lokaler Branch bereits existiert (Retry-Fall), wird Neuanlage übersprungen (nicht mit "branch already exists" fehlgeschlagen)
- `ErstelleProjektbranchAsync_CheckoutRemoteBranch_WennExistent` — Testet, dass bestehender Remote-Branch ausgecheckt wird statt Neuanlage
- `ErstelleProjektbranchAsync_WirftException_BeiGitFehler` — Testet aussagekräftige Fehlerbehandlung bei Git-Fehlern

#### Testmethoden zu JSON-Dateien:
- `InitialisiereAsync_ErzeugtStateJson` — Validiert `state.json` mit korrektem Schema (task_id, runtime, governance, clones, subagents)
- `InitialisiereAsync_ErzeugtPermissionsJson` — Validiert `permissions.json` mit Berechtigungen und Limits

#### Testmethoden zur Validierung:
- `InitialisiereAsync_WirftArgumentException_BeiUngueltigemTokenBudget` — Testet TokenBudget-Validierung (muss > 0 sein)
- `InitialisiereAsync_WirftArgumentException_BeiUngueltigemProjektBranchName` — Testet Branch-Namen-Validierung (muss gültiger Git-Branch-Name sein)
- `InitialisiereAsync_WirftArgumentException_BeiZuKurzemInitialPrompt` — Testet Prompt-Validierung (min. 10 Zeichen)
- `InitialisiereAsync_WirftArgumentException_BeiUngueltigemLaufzeitLimit` — Testet Laufzeit-Validierung (60–1440 Minuten)
- `ErstelleArbeitsverzeichnisStrukturAsync_WirftArgumentException_BeiRelativemPfad` — Testet, dass nur absolute Pfade akzeptiert werden

#### Testmethoden zur Fehlerbehandlung:
- `InitialisiereAsync_WirftInvalidOperationException_OhneGitRepository` — Testet Fehlerbehandlung, wenn Aufgabe kein GitRepository hat
- `InitialisiereAsync_WirftInvalidOperationException_BeiFehlgeschlagenemGitKlon` — Testet Fehlerbehandlung bei fehlgeschlagenem Klon

#### **WICHTIG: Plugin-Resolution-Regressionstest:**
- **`InitialisiereAsync_VerwendetPluginAusGitRepositoryPluginTyp_NichtDasDefaultPlugin`** (Zeilen 353–403) — **Dies ist der zentrale Validierungstest für die Anforderung:** 
  - Erstellt zwei unterschiedliche Mock-Plugins (DefaultScmPlugin, TestGitPlugin)
  - Registriert TestGitPlugin am `aufgabe.GitRepository.PluginTyp = "TestGitPlugin"` 
  - Setzt DefaultScmPlugin als Global-Default
  - **Validiert, dass TestGitPlugin (nicht DefaultScmPlugin) für Klon und Branch-Erstellung verwendet wird**
  - Verbietet Aufrufe auf dem Default-Plugin mittels `.Verify(..., Times.Never)`
  - Dies ist ein Regressionstest gegen den in der Anforderung beschriebenen Bug: Das System sollte **nicht** blind das Global-Default-Plugin verwenden, sondern das Plugin anhand von `aufgabe.GitRepository.PluginTyp` auflösen

---

### Weitere Test-Klassen mit Bezug zur Anforderung

#### `AutonomAufgabenInitialisierungsServiceTestFactory`
Datei: `src/Softwareschmiede.Tests/Helpers/AutonomAufgabenInitialisierungsServiceTestFactory.cs`

**Hilfsmethoden für Tests:**

- `CreateCliRunnerMockMitErfolgreicherGitAusfuehrung()` — Erstellt einen ICliRunner-Mock, der alle Git-Befehle erfolgreich simuliert (wird für "git branch --list" in Idempotenz-Prüfung verwendet)

- `CreateGitPluginMockMitErfolgreichemKlon()` — Erstellt einen IGitPlugin-Mock, dessen:
  - `CloneRepositoryAsync` das Zielverzeichnis + Marker-Datei anlegt (Callback: `Directory.CreateDirectory(zielPfad); File.WriteAllText(..., ".git-marker"...)`)
  - `GetRemoteBranchesAsync` standardmäßig leere Liste liefert (Branch existiert nicht remote)
  - `ResolveEffectiveRepositoryPathAsync` den Pfad unverändert zurückgibt

- `CreatePluginSelectionService(db, gitPlugin)` — Erstellt einen PluginSelectionService, der bei Plugin-Auflösung stets `gitPlugin` liefert (einziges registriertes und Default-Plugin). Dies ist das etablierte Test-Pattern.

- `CreateService(db, cliRunner, gitPlugin)` — Erstellt einen einsatzbereiten AutonomAufgabenInitialisierungsService mit gegebenen Dependencies

- `ErstelleProjekt(db)` — Erstellt ein Test-Projekt und fügt es dem Kontext hinzu (ohne zu speichern)

- `ErstelleAufgabeMitLokalemKlon(db, projektId, testRoot, titel, branchName)` — **Wichtig für Tests:** Erstellt eine Aufgabe mit:
  - Lokales Klon-Quellverzeichnis (`testRoot + "-quelle"`)
  - Verknüpftes `GitRepository` mit:
    - **`PluginTyp = "TestGitPlugin"`** (wird vom Test-Factory-Pattern verwendet zur Plugin-Auflösung)
    - `RepositoryUrl = quellRepo` (zeigt auf das lokale Quellverzeichnis)
    - `RepositoryName = "quelle"`

---

### `AutonomAufgabenDetailViewModelTests`
Datei: `src/Softwareschmiede.Tests/App/ViewModels/AutonomAufgabenDetailViewModelTests.cs`

Tests für das Detail-ViewModel; nicht unmittelbar relevant für die Plugin-Resolution, könnte aber die initialisierte Aufgabe und ihre Konfiguration testen.

---

### `AutonomAufgabeInitialisierungsDialogViewModelTests` und `AutonomAufgabeInitialisierungsDialogViewModelTests_BranchUndVorlagen`
Dateien: 
- `src/Softwareschmiede.Tests/App/ViewModels/AutonomAufgabeInitialisierungsDialogViewModelTests.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/AutonomAufgabeInitialisierungsDialogViewModelTests_BranchUndVorlagen.cs`

Tests für den Initialisierungs-Dialog-ViewModel; testen UI-Logik und Dateneingabe-Validierung (nicht direkt die Plugin-Resolution im Service, aber die benutzerseitige Schnittstelle dazu).

---

### E2E Tests

#### `E2E_AutonomAufgabenInitialisierung`
Datei: `src/Softwareschmiede.Tests/E2E/E2E_AutonomAufgabenInitialisierung.cs`

End-to-End Tests für den Initialisierungsablauf; testen die vollständige Initialisierung von autonomen Aufgaben inkl. UI-Interaktionen.

#### `E2E_AutonomAufgabenAgentExecution`
Datei: `src/Softwareschmiede.Tests/E2E/E2E_AutonomAufgabenAgentExecution.cs`

End-to-End Tests für die Ausführung des Projektleiter-Agenten; nicht direkt relevant für die Plugin-Resolution, aber testen die nachfolgende Ausführung.

---

## Zusammenfassung der Testabdeckung

### Was wird getestet:
1. ✅ Verzeichnisstruktur wird korrekt angelegt
2. ✅ Repository wird geklont
3. ✅ Projektbranch wird erstellt oder ausgecheckt
4. ✅ Idempotenz: Wiederholte Initialisierung schlägt nicht fehl, wenn Verzeichnis/Branch bereits existieren
5. ✅ **Plugin-Auflösung: Das richtige Plugin (aus `GitRepository.PluginTyp`) wird verwendet, nicht das Global-Default**
6. ✅ JSON-Dateien (`state.json`, `permissions.json`) werden korrekt generiert
7. ✅ Eingabe-Validierung (Branch-Name, Token-Budget, Prompt-Länge, Laufzeit-Limit)
8. ✅ Fehlerbehandlung bei fehlenden Repository, fehlgeschlagenem Klon, Git-Fehlern

### Test-Pattern:
- Tests verwenden Mocks für `IGitPlugin` und `ICliRunner`
- Test-Factory erstellt realistische Test-Objekte mit `GitRepository.PluginTyp = "TestGitPlugin"`
- Plugin-Selection-Service wird mit realistischen Mock-Implementierungen getestet
- Datenbank-Tests nutzen ein echtes `SoftwareschmiededDbContext` aus `TestDbContextFactory`

