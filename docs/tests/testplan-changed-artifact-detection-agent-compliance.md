# Testplan – Changed Artifact Detection & Agentendefinitions-Compliance

## Eingabe
- Quelle: `docs/tests/testluecken-changed-artifact-detection-agent-compliance.md`
- Fokus: Feature **„Erkennung geänderter Planungsdokumente + Agentendefinitions-Compliance“**
- Umsetzungsstrategie: **Unit zuerst**, danach wenige gezielte bUnit/Integrationspfade.

## Priorisierte Umsetzung

## AP-01 (P0, Unit): Fallback-Erkennung für Planungsdokumente im Snapshot absichern
**Datei:** `src/Softwareschmiede.Tests/Application/Services/GitWorkspaceBrowserServiceTests.cs`  
**Testklasse:** `GitWorkspaceBrowserServiceTests` (erweitern)

**Neue Testmethoden:**
1. `LoadSnapshotAsync_ShouldDetectPlanningDocumentsViaFallback_WhenOnlySlashAndDotVariantsExist`
2. `LoadSnapshotAsync_ShouldUseSourceRelativePath_ForPlanningDocumentFallbackDetection`
3. `LoadSnapshotAsync_ShouldNotClassifyMarkdownOutsideAllowedDocsFolders_AsPlanningDocumentInFallback`

## AP-02 (P0, Unit): Copilot-CLI Fehlerpfade + ExecutablePath absichern
**Datei:** `src/Softwareschmiede.Tests/Infrastructure/Plugins/GitHubCopilotPluginTests.cs`  
**Testklasse:** `GitHubCopilotPluginTests` (erweitern)

**Neue Testmethoden:**
1. `CheckHealthAsync_ShouldReturnFalse_WhenCliRunnerReturnsNull`
2. `CheckHealthAsync_ShouldReturnFalse_WhenCliRunnerThrowsWin32Exception`
3. `StartDevelopmentAsync_ShouldThrowInvalidOperationException_WhenMoveNextThrowsWin32Exception`
4. `CheckHealthAsync_ShouldUseConfiguredExecutablePath_WithoutQuotes`

## AP-03 (P0, Unit): Kurzfristige Randpfade im GitWorkspaceBrowserService schließen
**Datei:** `src/Softwareschmiede.Tests/Application/Services/GitWorkspaceBrowserServiceTests.cs`  
**Testklasse:** `GitWorkspaceBrowserServiceTests` (erweitern)

**Neue Testmethoden:**
1. `LoadSnapshotAsync_ShouldReturnCommitCountZero_WhenRevListFails`
2. `LoadPreviewAsync_ShouldFallbackToHeadAndHint_WhenWorkingTreeFileIsMissing`
3. `LoadPreviewAsync_ShouldPopulateOriginalContent_ForRegularTextFileWhenHeadExists`
4. `LoadPreviewAsync_ShouldSetOriginalContentNull_WhenGitShowFails`
5. `LoadSnapshotAsync_ShouldIgnoreTooShortStatusLines_WithoutThrowing`

## AP-04 (P1, Unit): Agentenpaket-Kompatibilität + Description-Fallback robust machen
**Dateien:**  
- `src/Softwareschmiede.Tests/Infrastructure/Plugins/GitHubCopilotPluginTests.cs`  
- `src/Softwareschmiede.Tests/Infrastructure/Plugins/ClaudeCliPluginTests.cs`

**Neue Testmethoden:**
1. `IsAgentPackageCompatibleAsync_ShouldReturnFalse_WhenPackagePathDoesNotExist` (beide Plugins)
2. `GetAvailableAgentsAsync_ShouldReturnEmpty_WhenPackagePathDoesNotExist` (Claude explizit absichern)
3. `GetAvailableAgentsAsync_ShouldUseFirstContentLine_WhenNoDescriptionFrontmatterExists` (GitHubCopilot)
4. `GetAvailableAgentsAsync_ShouldReturnNullDescription_WhenAgentFileCannotBeRead` (beide Plugins, I/O-Fehlerpfad)

## AP-05 (P1, Unit): AgentPackageReader I/O-Fehlerpfad ergänzen
**Datei:** `src/Softwareschmiede.Tests/Infrastructure/Services/AgentPackageReaderTests.cs`  
**Testklasse:** `AgentPackageReaderTests` (erweitern)

**Neue Testmethode:**
1. `GetPackageAsync_ShouldSetAgentDescriptionNull_WhenAgentFileReadThrows`

## AP-06 (P1, gezielt bUnit): Weiterverwendung von PlanningDocuments im AufgabeDetail-Fluss absichern
**Datei:** `src/Softwareschmiede.Tests/Components/Pages/Aufgaben/AufgabeDetailWorkspacePreviewBunitTests.cs`  
**Testklasse:** `AufgabeDetailWorkspacePreviewBunitTests` (erweitern)

**Neue Testmethoden:**
1. `AufgabeDetail_ShouldRenderPlanningDocumentEntries_WhenSnapshotContainsPlanningDocumentsOnly`
2. `AufgabeDetail_ShouldNotShowNoChangesHint_WhenOnlyPlanningDocumentsChanged`
3. `AufgabeDetail_ShouldRenderCodeAndPlanningEntries_WhenBothArtifactTypesChanged`

> Ziel dieses AP: Regression verhindern, dass das Feature „geänderte Planungsdokumente“ in der UI/Explorer-Strecke verloren geht.

## Umsetzungsreihenfolge (kurzfristig)
1. AP-01 + AP-02 (kritische Feature- und Compliance-Risiken)
2. AP-03 (hoher Stabilitätsgewinn bei geringem Implementierungsaufwand)
3. AP-04 + AP-05 (Robustheit Agentendefinitionen/Package-Reading)
4. AP-06 (gezielter Integrations-/UI-Absicherungsblock)

## Validierung nach Umsetzung

### 1) Service-Fokus (AP-01 + AP-03)
```powershell
dotnet test .\src\Softwareschmiede.Tests\Softwareschmiede.Tests.csproj --nologo --filter "FullyQualifiedName~GitWorkspaceBrowserServiceTests"
```

### 2) Plugin-Fokus (AP-02 + AP-04)
```powershell
dotnet test .\src\Softwareschmiede.Tests\Softwareschmiede.Tests.csproj --nologo --filter "FullyQualifiedName~GitHubCopilotPluginTests|FullyQualifiedName~ClaudeCliPluginTests"
```

### 3) Reader + UI-Fokus (AP-05 + AP-06)
```powershell
dotnet test .\src\Softwareschmiede.Tests\Softwareschmiede.Tests.csproj --nologo --filter "FullyQualifiedName~AgentPackageReaderTests|FullyQualifiedName~AufgabeDetailWorkspacePreviewBunitTests"
```

### 4) Finaler Gesamtlauf
```powershell
dotnet test .\src\Softwareschmiede.Tests\Softwareschmiede.Tests.csproj --nologo
```

### 5) Coverage-Check
```powershell
dotnet test .\src\Softwareschmiede.Tests\Softwareschmiede.Tests.csproj --collect:"XPlat Code Coverage" --nologo
```

## Definition of Done
- Alle P0-Testmethoden implementiert und grün.
- AP-06 stellt sicher, dass „nur Planungsdokumente geändert“ nicht im „Keine geänderten Dateien“-Pfad landet.
- Fehlerpfade für Copilot-CLI-Health/Start liefern konsistente, erwartete Ergebnisse.
- Coverage im Bereich `GitWorkspaceBrowserService`, `GitHubCopilotPlugin`, `ClaudeCliPlugin`, `AgentPackageReader`, `AufgabeDetail` sichtbar erhöht.
