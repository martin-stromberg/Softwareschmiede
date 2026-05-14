# Testplan – Systemweite Schließung der Testlücken

## Eingabe
- Quelle: `docs/tests/testluecken-systemweit.md`
- Stand: 340 Tests grün, systemweite Lücken mit Fokus auf UI-Kernflows, Infrastruktur und Orchestrierung

## Zielbild
1. P1-Lücken schließen (kritische Kernabläufe, 0%/große Lücken)
2. P2-Lücken in zentralen Services systematisch reduzieren
3. P3-Lücken (UI-Nebenkomponenten/Startup/Domain) vervollständigen
4. Nach jedem Arbeitspaket Coverage neu messen und Lückenliste aktualisieren

---

## Priorisierte Umsetzungsreihenfolge

## Welle 1 (P1) – zuerst umsetzen
1. **Agentenpakete UI** (`AgentenpaketeSeite.razor(.cs)`)
2. **AufgabeDetail Kernworkflow** (`AufgabeDetail.razor(.cs)`)
3. **ProjektDetail/ProjektListe Kernworkflow**
4. **CLI/Credential/Shutdown Infrastruktur**
5. **Startup/Hosting Smoke/Contract-Tests**

## Welle 2 (P2)
6. `GitOrchestrationService` Fehler-/Guardpfade
7. `KiAusfuehrungsService` Session-/Subscriber-Randfälle
8. `EntwicklungsprozessService` Restpfade
9. `ArbeitsverzeichnisSettingsService` Restpfade
10. `AgentPackageFileService` gezielte Fehlerpfade

## Welle 3 (P3)
11. UI-Nebenkomponenten (`Home`, `NeueAufgabe`, Badges, Layout-Reste)
12. Plugin/Domain/Contracts-Restlücken

---

## Konkrete Arbeitspakete

### AP-01 (P1): Agentenpaket-Verwaltung vollständig absichern
**Ziel-Dateien:**  
- `src/Softwareschmiede/Components/Pages/AgentenpaketeSeite.razor.cs`
- `src/Softwareschmiede/Components/Pages/AgentenpaketeSeite.razor`

**Neue Testklassen (Unit/Komponenten-nahe Logiktests):**
- `src/Softwareschmiede.Tests/Components/Pages/AgentenpaketeSeiteTests.cs`
- `src/Softwareschmiede.Tests/Components/Pages/AgentenpaketeSeiteMarkupTests.cs`

**Konkrete Testmethoden (Beispielnamen):**
- `OnInitializedAsync_ShouldLoadPackagesAndInitializeTree()`
- `SelectNodeAsync_ShouldLoadFileContent_WhenFileNodeSelected()`
- `SaveFileAsync_ShouldPersistContent_AndSetSuccessMessage()`
- `CreateDirectoryAsync_ShouldCreateSubDirectory_WhenInputValid()`
- `CreateEmptyFileAsync_ShouldCreateFile_WhenInputValid()`
- `HandleFileUpload_ShouldUploadFile_AndRefreshTree()`
- `ExecuteRenameAsync_ShouldRenameFileOrDirectory_AndReselectNode()`
- `ExecuteDeleteAsync_ShouldDeleteSelectedNode_AndClearSelection()`
- `RefreshPackageTreeAndReselectAsync_ShouldKeepSelectionAfterRename()`
- `Markup_ShouldContainTreeEditorUploadRenameDeleteBindings()`

**Benötigte Mocks/Testdaten:**
- `Mock<IAgentPackageFileService>`
- Test-`FileTreeNode` mit Paket + Unterordner + Dateien
- Upload-Testdaten: kleiner `MemoryStream` (`README.md`), `contentType = text/markdown`
- Fehlerfälle: Service wirft `InvalidOperationException` / `ArgumentException`

**Testtyp:** Unit/Komponentenlogik + Markup-Contract-Test (Dateiinhalt prüfen)

---

### AP-02 (P1): AufgabeDetail Kernworkflow schließen
**Ziel-Dateien:**  
- `src/Softwareschmiede/Components/Pages/Aufgaben/AufgabeDetail.razor.cs`
- `src/Softwareschmiede/Components/Pages/Aufgaben/AufgabeDetail.razor`

**Bestehende Klasse erweitern + neue Klasse ergänzen:**
- Bestehend: `AufgabeDetailFolgePromptTests.cs`
- Neu: `AufgabeDetailWorkflowTests.cs`

**Konkrete Testmethoden:**
- `ProzessStartenAsync_ShouldStartProcess_WhenBranchSelected()`
- `ProzessStartenAsync_ShouldSetError_WhenNoRemoteBranchAvailable()`
- `CommitAsync_ShouldCallGitOrchestration_AndSetSuccess()`
- `PushAsync_ShouldShowError_WhenServiceThrows()`
- `PullAsync_ShouldLogAndRefreshAfterPull()`
- `ResetAsync_ShouldUseSelectedResetTypeAndTargetRef()`
- `PullRequestErstellenAsync_ShouldStoreResultAndShowSuccess()`
- `AbschliessenAsync_ShouldSetStatusAndNavigateBack()`
- `ArchivierenAsync_ShouldArchiveTask_AndNavigateToProject()`
- `AufgabeLoeschenAsync_ShouldDeleteAndNavigate()`
- `Markup_ShouldContainGitActionButtonsAndStartDialogBindings()`

**Mocks/Testdaten:**
- `Mock<IGitPlugin>`, `Mock<IPluginManager>`
- InMemory-DB via `TestDbContextFactory` (Projekt + Aufgabe + Protokolle)
- Fake-Services für `EntwicklungsprozessService`, `GitOrchestrationService`, `KiAusfuehrungsService`
- Testdaten: Aufgabe mit/ohne `LokalerKlonPfad`, mit/ohne `BranchName`, Repository-Verknüpfung

**Testtyp:** Unit/Komponentenlogik + Markup-Contract

---

### AP-03 (P1): Projekt-UI Kernworkflow schließen
**Ziel-Dateien:**  
- `ProjektDetail.razor(.cs)`, `ProjektListe.razor(.cs)`

**Neue/erweiterte Klassen:**
- Bestehend erweitern: `ProjektDetailRepositoryFormTests.cs`
- Neu: `ProjektDetailWorkflowTests.cs`
- Neu: `ProjektListeTests.cs`
- Neu: `ProjektSeitenMarkupTests.cs`

**Konkrete Testmethoden:**
- `LadeAsync_ShouldLoadProjectAndTasks()`
- `ArchivierenAsync_ShouldArchiveProject()`
- `UpdateAsync_ShouldPersistEditedProject()`
- `DeleteAsync_ShouldDeleteProject_AndNavigateToList()`
- `ToggleRepositoryFormAsync_ShouldLoadPluginsAndShowForm()`
- `SpeichernAsync_ShouldCreateProject_WhenNameValid()`
- `SpeichernAsync_ShouldSetValidationError_WhenNameEmpty()`
- `ZurDetail_ShouldNavigateToProjectDetail()`
- `Markup_ShouldContainRepositoryDynamicFieldsAndNavigationLinks()`

**Mocks/Testdaten:**
- InMemory-DB mit mehreren Projekten/Aufgaben/Repositories
- `Mock<IPluginManager>` mit unterschiedlichen Plugin-Sets
- Navigation-Fake (`TestNavigationManager` Pattern wie bestehend)

**Testtyp:** Unit/Komponentenlogik + Markup-Contract

---

### AP-04 (P1): Infrastruktur kritisch absichern (CLI/Credentials/Shutdown)
**Ziel-Dateien:**  
- `CliRunner.cs`, `WindowsCredentialStore.cs`, `SystemShutdownService.cs`

**Neue Testklassen:**
- `Infrastructure/Services/CliRunnerTests.cs`
- `Infrastructure/Services/WindowsCredentialStoreTests.cs`
- `Infrastructure/Services/SystemShutdownServiceTests.cs`

**Konkrete Testmethoden:**
- `RunAsync_ShouldReturnStdOutStdErrAndExitCode()`
- `RunAsync_ShouldRespectCancellationToken()`
- `StreamAsync_ShouldForwardStdOutAndPrefixedStdErrLines()`
- `StreamAsync_ShouldCompleteChannel_WhenProcessEnds()`
- `ResolveExecutablePath_ShouldReturnAbsolutePath_WhenCommandRooted()`
- `ResolveExecutablePath_ShouldFallbackToOriginalCommand_WhenNotFound()`
- `RequestShutdownAsync_ShouldThrow_WhenExitCodeNotZero()`
- `RequestShutdownAsync_ShouldUseExpectedCommand_ForCurrentOS()` (plattformabhängig)
- `GetCredential_ShouldReturnNull_WhenCredentialMissing()` (Windows-only)
- `SetCredential_ShouldThrow_WhenNativeWriteFails()` (Windows-only)

**Mocks/Testdaten:**
- Für `CliRunner`: kleine Testprozesse (`pwsh -Command ...`) mit definiertem stdout/stderr
- Für Shutdown/Credential: plattformabhängige Tests mit `[SkippableFact]`/Runtime-Checks
- Fehlerpfade über absichtlich ungültige Befehle/Targets

**Testtyp:** Unit + leichtgewichtige Integration (Prozessausführung)

---

### AP-05 (P1): Startup/Hosting-Abdeckung ergänzen
**Ziel-Dateien:** `src/Softwareschmiede/Program.cs`, `src/Softwareschmiede.Client/Program.cs`

**Testklasse erweitern/neu:**
- Bestehend erweitern: `ProgramDiWiringTests.cs`
- Neu: `ProgramPipelineContractTests.cs`

**Konkrete Testmethoden:**
- `Program_ShouldRegisterCoreInfrastructureServices()`
- `Program_ShouldRegisterApplicationServices()`
- `Program_ShouldConfigureStatusCodePagesAndAntiforgery()`
- `Program_ShouldMapRazorComponentsWithInteractiveModes()`
- `ClientProgram_ShouldCreateAndRunDefaultHost()`

**Testtyp:** Source-Contract-Tests (stringbasiert, robust gegen Refactoring mit gezielten Patterns)

---

### AP-06 (P2): GitOrchestration-Restlücken schließen
**Datei:** `GitOrchestrationService.cs`  
**Bestehende Klasse erweitern:** `GitOrchestrationServiceTests.cs`

**Neue Methoden:**
- `CommitAsync_ShouldThrow_WhenAufgabeMissing()`
- `CommitAsync_ShouldThrow_WhenKlonpfadMissing()`
- `PushAsync_ShouldPushAndLog_WhenBranchPresent()`
- `PullAsync_ShouldPullAndLog_WhenKlonpfadPresent()`
- `PullRequestErstellenAsync_ShouldThrow_WhenNoActiveRepositoryExists()`
- `PullRequestErstellenAsync_ShouldThrow_WhenBranchMissing()`
- `ExtractRepositoryIdFromUrl_ShouldSupportSshAndHttpsFormats()`

**Mocks/Testdaten:** InMemory-DB + `Mock<IGitPlugin>`, Aufgaben mit gezielten Randzuständen

---

### AP-07 (P2): KiAusfuehrungsService-Restlücken schließen
**Datei:** `KiAusfuehrungsService.cs`  
**Bestehend erweitern:** `KiAusfuehrungsServiceTests.cs`

**Neue Methoden:**
- `Subscribe_ShouldReplayBufferedLinesToNewSubscriber()`
- `Subscribe_ShouldIsolateSubscriberExceptions()`
- `AbortKiLauf_ShouldCancelRunningSession()`
- `SessionBereinigen_ShouldDisposeSessionAndRemoveState()`
- `GetBufferedLines_ShouldReturnEmpty_WhenSessionUnknown()`

**Mocks/Testdaten:** Fake-ScopeFactory/Fake-KI-Run mit kontrollierten Zeilen und Exceptions

---

### AP-08 (P2): Entwicklungsprozess/Arbeitsverzeichnis/AgentPackageService Restpfade
**Dateien:**  
- `EntwicklungsprozessService.cs`
- `ArbeitsverzeichnisSettingsService.cs`
- `AgentPackageFileService.cs`

**Testklassen:**
- `EntwicklungsprozessServiceTests.cs` erweitern
- `ArbeitsverzeichnisSettingsServiceTests.cs` erweitern
- `Softwareschmiede.IntegrationTests/Services/AgentPackageFileServiceTests.cs` erweitern

**Neue Methoden (Auszug):**
- `KiStartenAsync_ShouldHandleContextFileWriteFailure_Atomically()`
- `AbschliessenAsync_ShouldThrow_WhenAufgabeMissing()`
- `AbbrechenAsync_ShouldThrow_WhenAufgabeMissing()`
- `SaveArbeitsverzeichnisAsync_ShouldNormalizePath_BeforePersisting()`
- `RenameFileAsync_ShouldThrow_WhenTargetAlreadyExists()`
- `DeleteDirectoryAsync_ShouldThrow_WhenPathTraversalDetected()`

---

### AP-09 (P3): UI-Nebenkomponenten + Plugin/Domain-Reste
**Dateien:** `Home`, `NeueAufgabe`, `StatusBadge`, `ProjektStatusBadge`, `PluginKonfiguration`, `IGitPlugin` etc.  
**Testklassen neu:**
- `Components/Pages/HomeTests.cs`
- `Components/Pages/Aufgaben/NeueAufgabeTests.cs`
- `Components/Shared/StatusBadgeTests.cs`
- `Domain/Entities/PluginKonfigurationTests.cs`
- `Plugin.Contracts/IGitPluginContractTests.cs`

**Fokus:** Rendering/Navigation/Validierung/kleine Vertrags- und ValueObject-Tests

---

## Definition of Done pro Arbeitspaket
- Neue Tests grün in Unit + Integration
- Keine Flaky-Tests (mind. 2 lokale Wiederholungen)
- Coverage-Report neu erzeugt (`--collect:"XPlat Code Coverage"`)
- Lückenliste in `docs/tests/testluecken-systemweit.md` aktualisiert

## Ausführungs-Checkliste (operativ)
1. AP-01 bis AP-05 umsetzen, danach Testlauf + Coverage
2. AP-06 bis AP-08 umsetzen, danach Testlauf + Coverage
3. AP-09 umsetzen, finaler Testlauf + Coverage
4. Restlücken < akzeptiertem Schwellenwert dokumentieren (falls technisch begründet)

## Empfohlene Befehle
- `dotnet test .\Softwareschmiede.slnx`
- `dotnet test .\Softwareschmiede.slnx --collect:"XPlat Code Coverage"`
