# Testplan – DiffViewer für geänderte Dateien

## Eingabe
- Quelle: `docs/tests/testluecken-diffviewer-geaenderte-dateien.md`
- Ziel: Priorisierte Schließung der verbleibenden Lücken in `AufgabeDetail` + `DiffViewer` + Diff-Subkomponenten.

## Priorisierte Umsetzungsreihenfolge

## AP-01 (P0): AufgabeDetail – dateispezifische Diff-Auflösung & Fehlerpfade
**Datei:** `src/Softwareschmiede.Tests/Components/Pages/Aufgaben/AufgabeDetailWorkspacePreviewBunitTests.cs`  
**Testklasse (erweitern):** `AufgabeDetailWorkspacePreviewBunitTests`

**Testfälle (bUnit/xUnit):**
1. `AufgabeDetail_ShouldReturnNoDiff_WhenPreviewRelativePathIsWhitespace`
2. `AufgabeDetail_ShouldFallbackToSourceRelativePath_WhenPrimaryPathHasNoDiff`
3. `AufgabeDetail_ShouldSkipFallback_WhenSourceRelativePathIsWhitespace`
4. `AufgabeDetail_ShouldSkipFallback_WhenSourceRelativePathEqualsRelativePath`
5. `AufgabeDetail_ShouldShowNoDiffHint_WhenDiffLookupFails`
6. `AufgabeDetail_ShouldShowWorkspaceErrorAndResetPreview_WhenLocalClonePathIsMissing`
7. `AufgabeDetail_ShouldShowWorkspaceErrorAndResetPreview_WhenLoadSnapshotThrows`
8. `AufgabeDetail_ShouldRestorePreviouslySelectedFile_AfterWorkspaceReload`
9. `AufgabeDetail_ShouldToggleDirectoryAndResetPreview_WhenDirectoryNodeIsClicked`

## AP-02 (P0): DiffViewer – Lifecycle, Cancellation, Guard, Exceptions
**Datei:** `src/Softwareschmiede.Tests/Components/Diff/DiffViewerBunitTests.cs`  
**Testklasse (erweitern):** `DiffViewerBunitTests`

**Testfälle (bUnit/xUnit):**
1. `DiffViewer_ShouldRenderLoadingState_BeforeDiffIsLoaded`
2. `DiffViewer_ShouldNotReload_WhenDiffResultIdIsUnchanged`
3. `DiffViewer_ShouldKeepOnlyLatestResult_WhenDiffResultIdChangesQuickly`
4. `DiffViewer_ShouldShowGenericError_WhenGetDiffThrows`
5. `DiffViewer_ShouldCancelInFlightLoad_OnDisposeAsync`

## AP-03 (P1): DiffViewer – Eventpfade über Toolbar/Content
**Datei:** `src/Softwareschmiede.Tests/Components/Diff/DiffViewerBunitTests.cs`  
**Testklasse (erweitern):** `DiffViewerBunitTests`

**Testfälle (bUnit/xUnit):**
1. `DiffViewer_ShouldChangeViewMode_WhenToolbarButtonClicked`
2. `DiffViewer_ShouldApplyAndClearSearch_WhenToolbarSearchIsUsed`
3. `DiffViewer_ShouldHandleNavigationEvents_WhenNextPreviousTriggered`
4. `DiffViewer_ShouldUpdateSelectedCount_WhenLineSelectionChanges`
5. `DiffViewer_ShouldHandleExportActions_WithoutCrash`

## AP-04 (P1): DiffLine-Komponente
**Neue Datei:** `src/Softwareschmiede.Tests/Components/Diff/DiffLineBunitTests.cs`  
**Neue Testklasse:** `DiffLineBunitTests`

**Testfälle (bUnit/xUnit):**
1. `DiffLine_ShouldRenderIndicator_ForAddedRemovedModifiedContextUnknown`
2. `DiffLine_ShouldRenderLineNumbers_AndNaFallback`
3. `DiffLine_ShouldInvokeOnSelected_WhenCheckboxChanged`
4. `DiffLine_ShouldInvokeOnCopied_WhenCopyClicked`
5. `DiffLine_ShouldRenderExpectedAriaLabels`

## AP-05 (P1): DiffToolbar + DiffContent
**Neue Dateien:**  
- `src/Softwareschmiede.Tests/Components/Diff/DiffToolbarBunitTests.cs`  
- `src/Softwareschmiede.Tests/Components/Diff/DiffContentBunitTests.cs`

**Testfälle DiffToolbar (bUnit/xUnit):**
1. `DiffToolbar_ShouldEmitViewModeChanged_ForAllButtons`
2. `DiffToolbar_ShouldTriggerSearch_OnInputChange`
3. `DiffToolbar_ShouldNavigateNextPrevious_OnEnterAndShiftEnter`
4. `DiffToolbar_ShouldClearSearch_OnEscapeAndClearButton`
5. `DiffToolbar_ShouldToggleFilters_WithoutError`
6. `DiffToolbar_ShouldEmitExportFormats_OnExportActions`

**Testfälle DiffContent (bUnit/xUnit):**
1. `DiffContent_ShouldRenderEmptyState_WhenNoBlocksExist`
2. `DiffContent_ShouldFilterLines_CaseInsensitive_BySearchTerm`
3. `DiffContent_ShouldReuseCachedVisibleLines_WhenSearchTermUnchanged`
4. `DiffContent_ShouldDelegateLineSelectionCallback`
5. `DiffContent_ShouldHandleCopyCallbackError_WithoutCrash`

## AP-06 (P2): DiffHeader + DiffFooter
**Neue Dateien:**  
- `src/Softwareschmiede.Tests/Components/Diff/DiffHeaderBunitTests.cs`  
- `src/Softwareschmiede.Tests/Components/Diff/DiffFooterBunitTests.cs`

**Testfälle DiffHeader (bUnit/xUnit):**
1. `DiffHeader_ShouldRenderStatusBadge_ForPendingGeneratedCachedErrorUnknown`
2. `DiffHeader_ShouldRenderMetadata_GeneratedAtAndGeneratedBy`

**Testfälle DiffFooter (bUnit/xUnit):**
1. `DiffFooter_ShouldRenderStatusBadge_ForPendingGeneratedCachedErrorUnknown`
2. `DiffFooter_ShouldRenderExpiresAt_WhenPresent`
3. `DiffFooter_ShouldInvokeScrollToTop_JsInterop`
4. `DiffFooter_ShouldHandleScrollToBottom_JsInteropError_WithoutCrash`

## Validierungskommandos (Reihenfolge)

### 1) Nach AP-01
```powershell
dotnet test .\src\Softwareschmiede.Tests\Softwareschmiede.Tests.csproj --nologo --filter "FullyQualifiedName~AufgabeDetailWorkspacePreviewBunitTests"
```

### 2) Nach AP-02 + AP-03
```powershell
dotnet test .\src\Softwareschmiede.Tests\Softwareschmiede.Tests.csproj --nologo --filter "FullyQualifiedName~DiffViewerBunitTests"
```

### 3) Nach AP-04 bis AP-06
```powershell
dotnet test .\src\Softwareschmiede.Tests\Softwareschmiede.Tests.csproj --nologo --filter "FullyQualifiedName~DiffLineBunitTests|FullyQualifiedName~DiffToolbarBunitTests|FullyQualifiedName~DiffContentBunitTests|FullyQualifiedName~DiffHeaderBunitTests|FullyQualifiedName~DiffFooterBunitTests"
```

### 4) Finaler Gesamtlauf
```powershell
dotnet test .\Softwareschmiede.slnx --nologo
```

### 5) Coverage-Validierung
```powershell
dotnet test .\Softwareschmiede.slnx --collect:"XPlat Code Coverage" --nologo
```

## Definition of Done
- Alle P0- und P1-Testfälle implementiert.
- Fokusläufe + Gesamtlauf grün.
- Kein Regression-Fail in bestehenden Diff-/AufgabeDetail-Tests.
- Coverage im Bereich `AufgabeDetail`/`DiffViewer`/`Components/Diff/*` sichtbar erhöht.
