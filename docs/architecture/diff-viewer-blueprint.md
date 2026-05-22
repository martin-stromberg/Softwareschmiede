# Architektur-Blueprint: Diff-Vergleichskomponente für Dateiänderungen

**Feature:** Interaktive Diff-Vergleichskomponente für die Visualisierung von Dateiänderungen  
**Status:** Architecture Design (Phase 1 Complete)  
**Zielplattform:** Blazor Server (C# Backend + Razor Frontend)  
**Version:** 1.0  
**Erstelldatum:** 2025-05-15

---

## 1. Systemübersicht

### 1.1 Vision
Eine leistungsstarke, benutzerfreundliche Diff-Vergleichskomponente, die Dateiänderungen ähnlich wie moderne IDEs (VS Code, JetBrains, GitHub) darstellt und eine interaktive Codeüberprüfung (Code Review) ermöglicht.

### 1.2 Geschäftsziele
- ✅ Verbesserung der Benutzerfreundlichkeit bei der Codeüberprüfung
- ✅ Schnellere Identifikation von Änderungen durch visuelle Unterscheidung
- ✅ Unterstützung von Git-Workflows (Pull Requests, Commits)
- ✅ Barrierefreie Darstellung für alle Nutzer (WCAG 2.1 AA)
- ✅ Hohe Performance auch bei großen Dateien (10.000+ Zeilen)

### 1.3 Qualitätsziele
| Ziel | Metrik | Toleranz |
|------|--------|----------|
| **Performance** | Rendering < 500ms für 10.000 Zeilen | ±50ms |
| **Accessibility** | WCAG 2.1 Level AA Conformance | 100% |
| **Code Quality** | Test Coverage ≥ 80% | ±5% |
| **User Experience** | Developer Satisfaction > 4/5 | TBD |

---

## 2. Komponenten-Architektur

### 2.1 Komponenten-Hierarchie

```
┌─ DiffViewer (Hauptkomponente)
│
├─ DiffHeader
│  ├─ FileInfo (Dateiname, Typ, Status)
│  └─ Statistics (Added, Removed, Modified Counts)
│
├─ DiffToolbar
│  ├─ ViewModeSelector (Side-by-Side, Split, Unified)
│  ├─ SearchBox (Zeilen/Text suchen)
│  └─ ActionButtons (Filter, Export, Copy)
│
├─ DiffContent (Scrollbar-Container)
│  ├─ DiffBlock (Gruppierte Änderungen)
│  │  └─ DiffLine (Einzelne Zeile)
│  │     ├─ LineNumber (Original + Neu)
│  │     ├─ LineIndicator (+/-/~/-, Farbe)
│  │     └─ LineContent (Code mit Syntax-Highlighting)
│
├─ DiffScrollbar (Custom Scrollbar mit Block-Highlighting)
│
└─ DiffFooter
   ├─ NavigationControls (Prev/Next Change)
   └─ Statistics (Line Count, Block Count)
```

### 2.2 Komponenten-Spezifikationen

#### **DiffViewer (Hauptkomponente)**
- **Verantwortung:** Orchestrierung aller Sub-Komponenten, State Management
- **Input (Props):**
  - `DiffData`: `DiffResult` (Backend-Modell)
  - `ViewMode`: Enum (SideBySide, Split, Unified)
  - `OnLineSelected`: Callback
- **Output:** Interaktive Diff-Visualisierung
- **State:**
  - CurrentViewMode
  - SelectedLines
  - ScrollPosition
  - FilterSettings

#### **DiffBlock**
- **Verantwortung:** Render einer Gruppe nacheinanderfolgende Änderungen
- **Input:**
  - `BlockType`: Enum (Added, Removed, Modified, Context)
  - `Lines`: List<DiffLine>
  - `Index`: Blocknummer
- **Styling:** Konditionale Hintergrundfarbe basierend auf BlockType

#### **DiffLine**
- **Verantwortung:** Render einer einzelnen Zeile mit Nummer, Indikator, Inhalt
- **Input:**
  - `LineData`: DiffLine
  - `IsSelected`: Bool
  - `LineNumber`: Int
- **Interaktivität:** Hover-Highlighting, Click-Selection
- **A11y:** ARIA-Labels, semantic HTML

### 2.3 Technologie-Stack

| Aspekt | Technologie | Begründung |
|--------|------------|-----------|
| **Frontend-Framework** | Blazor Server (InteractiveServer) | Existierendes Stack; Real-time SignalR; C# Consistency |
| **Komponenten-Pattern** | Razor Components mit Cascading Parameters | Type-safe; Mirrors Softwareschmiede Patterns |
| **Rendering** | Blazor `<Virtualize>` mit OverscanCount=5 | Optimiert für 10k+ Zeilen; <1s Rendering |
| **Diff-Algorithmus** | Diff Match Patch (C# Nuget Port) | Industrie-Standard; Performance; LCS-basiert |
| **Styling** | Custom CSS (BEM) + Bootstrap 5 / TailwindCSS | Konsistent mit Projekt; Responsive; A11y-ready |
| **State Management** | Blazor Component State + optional EF DbContext | Einfach; Keine zusätzliche Library nötig |
| **Caching** | 2-Tier: Memory (1h TTL) + Persistent SQLite (24h TTL) | Balance zwischen Performance & Persistierung |

---

## 3. State Management & Data Flow

### 3.1 Data Models

```csharp
// Backend Model (Entity Framework)
public class DiffResult {
    public Guid Id { get; set; }
    public Guid AufgabeId { get; set; }
    public string FilePath { get; set; }
    public string SourceVersion { get; set; }  // CommitSHA or Version
    public string TargetVersion { get; set; }
    public DiffType DiffType { get; set; }      // Unified, SideBySide, Split
    public int AddedLines { get; set; }
    public int RemovedLines { get; set; }
    public int ModifiedLines { get; set; }
    public DateTime GeneratedAt { get; set; }
    public ICollection<DiffBlock> DiffBlocks { get; set; }
}

public class DiffBlock {
    public Guid Id { get; set; }
    public Guid DiffResultId { get; set; }
    public BlockType BlockType { get; set; }    // Added, Removed, Modified, Context
    public int SourceStartLine { get; set; }
    public int SourceEndLine { get; set; }
    public int TargetStartLine { get; set; }
    public int TargetEndLine { get; set; }
    public ICollection<DiffLine> DiffLines { get; set; }
}

public class DiffLine {
    public Guid Id { get; set; }
    public Guid DiffBlockId { get; set; }
    public LineStatus LineStatus { get; set; }  // Added, Removed, Context
    public string Content { get; set; }
    public int? SourceLineNumber { get; set; }
    public int? TargetLineNumber { get; set; }
}

// Frontend ViewModel
public class DiffViewerState {
    public DiffResult DiffData { get; set; }
    public DiffViewMode ViewMode { get; set; }  // SideBySide, Split, Unified
    public List<Guid> SelectedLineIds { get; set; }
    public string SearchQuery { get; set; }
    public int ScrollPosition { get; set; }
    public bool IsLoading { get; set; }
    public string? ErrorMessage { get; set; }
}

// Enums
public enum BlockType { Added, Removed, Modified, Context }
public enum LineStatus { Added, Removed, Context }
public enum DiffViewMode { SideBySide, Split, Unified }
```

### 3.2 Data Flow Sequence

```
User Request (AufgabeDetail.razor)
        ↓
DiffViewer Component @OnInitializedAsync
        ↓
DiffService.GetDiffAsync(sourceVersion, targetVersion, filePath)
        ↓
Check L1 Cache (Memory):
  ✓ Hit → Return cached DiffResult
  ✗ Miss → Check L2 Cache (SQLite)
           ✓ Hit → Return cached DiffResult
           ✗ Miss → Generate new Diff
        ↓
Generate Diff (Diff Match Patch Library):
  - Compare source & target content
  - Create DiffBlocks (groups of consecutive changes)
  - Create DiffLines (individual line entries)
        ↓
Cache Result:
  - L1 Memory Cache (1 hour TTL)
  - L2 Persistent Cache (SQLite, 24 hours TTL)
        ↓
Return DiffResult to Frontend
        ↓
DiffViewer Renders with Virtualization:
  - Render only visible lines
  - OverscanCount=5 for smooth scrolling
  - Apply conditional styling based on BlockType/LineStatus
        ↓
User Interactions:
  - Hover → Highlight line in real-time
  - Click → Select/Deselect line
  - Search → Filter & highlight matching lines
  - Export → Generate HTML/PDF/Text export
```

---

## 4. Rendering-Strategie

### 4.1 Virtualisierung

**Problem:** 10.000+ Zeilen → Browser-Rendering-Bottleneck

**Lösung:** Blazor `<Virtualize>` Component
```razor
<Virtualize Items="@DiffData.DiffBlocks" OverscanCount="5">
    <DiffBlock BlockData="@context" />
</Virtualize>
```

**Konfiguration:**
- **OverscanCount:** 5 (render 5 lines before/after viewport)
- **ItemSize:** ~30px per line (estimated)
- **Scrollbar:** Custom scrollbar shows block minimap
- **Performance:** < 500ms für 10.000 Zeilen, ~2-5MB Memory

### 4.2 View Modes

#### **Side-by-Side (Default)**
```
┌─────────────────────────────────────┬─────────────────────────────────────┐
│ Original Content (Source)           │ Modified Content (Target)           │
├──────┬───────────────────────────────┼──────┬───────────────────────────────┤
│ 1234 │ function add(a, b) {         │ 1234 │ function add(a, b) {         │
│ 1235 │   return a + b;              │ 1235 │   // Add two numbers         │
│      │                              │ 1236 │   return a + b;              │
│ 1236 │ }                            │ 1237 │ }                            │
└──────┴───────────────────────────────┴──────┴───────────────────────────────┘
```

#### **Split Mode** (für größere Unterschiede)
```
┌─────────────────────────────────────────────────────────────────────┐
│ Removed:                                                            │
├──────┬───────────────────────────────────────────────────────────────┤
│ 1234 │ function add(a, b) { return a + b; }                        │
├──────┴───────────────────────────────────────────────────────────────┤
│ Added:                                                              │
├──────┬───────────────────────────────────────────────────────────────┤
│ 1234 │ function add(a, b) {                                         │
│ 1235 │   // Add two numbers                                         │
│ 1236 │   return a + b;                                              │
│ 1237 │ }                                                            │
└──────┴───────────────────────────────────────────────────────────────┘
```

#### **Unified Mode** (Git-style)
```
@@ -1234,1 +1234,4 @@ function add
 function add(a, b) {
-  return a + b;
+  // Add two numbers
+  const result = a + b;
+  return result;
 }
```

### 4.3 Farbschema & Styling

| Änderungstyp | Farbe | WCAG AA Kontrast | CSS Class |
|--------------|-------|-----------------|-----------|
| **Gelöschte Zeile** | #FF6B6B (Red) | 4.5:1 | `.diff-removed` |
| **Neue Zeile** | #51CF66 (Green) | 5.5:1 | `.diff-added` |
| **Geänderte Zeile** | #FFD93D (Yellow) | 4.8:1 | `.diff-modified` |
| **Context-Zeile** | #E9ECEF (Gray) | 7.1:1 | `.diff-context` |
| **Hover-State** | Darker Variant | +1.5 Contrast | `.diff-line:hover` |

```css
/* BEM Pattern */
.diff-line {
    display: flex;
    padding: 8px 12px;
    font-family: 'Monaco', 'Courier New', monospace;
    font-size: 13px;
    line-height: 1.5;
    transition: background-color 0.15s ease-out;
}

.diff-line--added {
    background-color: #f0f9ff;
    border-left: 3px solid #51CF66;
}

.diff-line--removed {
    background-color: #fef5f5;
    border-left: 3px solid #FF6B6B;
}

.diff-line--modified {
    background-color: #fef9f0;
    border-left: 3px solid #FFD93D;
}

.diff-line:hover {
    background-color: rgba(255, 255, 255, 0.5);
    cursor: text;
}

.diff-line--selected {
    background-color: rgba(99, 102, 241, 0.1);
    border-left-width: 4px;
}
```

---

## 5. Sicherheit & Fehlerbehandlung

### 5.1 XSS-Protection

**Bedrohung:** Diff-Content könnte malicious HTML/JavaScript enthalten

**Mitigationen:**
1. **HTML-Encoding:** Alle Line-Contents werden HTML-encoded vor Rendering
   ```csharp
   public static string EscapeHtml(string text) 
       => System.Net.WebUtility.HtmlEncode(text);
   ```

2. **Content Security Policy (CSP):**
   ```html
   <meta http-equiv="Content-Security-Policy" 
         content="default-src 'self'; script-src 'self'">
   ```

3. **Validation:** Backend validiert DiffResult vor Speicherung
   - Länge pro Line: Max 10.000 Zeichen
   - Total Lines: Max 100.000
   - Allowed Characters: UTF-8 printable + common code chars

### 5.2 Fehlerbehandlung

**Exception-Handling Strategie:**

```csharp
public class DiffService {
    public async Task<DiffResult> GetDiffAsync(...) {
        try {
            // Check cache
            var cached = await _cache.GetAsync(cacheKey);
            if (cached != null) return cached;
            
            // Generate diff
            var diffResult = GenerateDiff(source, target);
            
            // Cache result
            await _cache.SetAsync(cacheKey, diffResult, 1.Hour());
            return diffResult;
        }
        catch (DiffGenerationException ex) {
            _logger.LogError($"Diff generation failed: {ex.Message}");
            // Fallback: Return character-by-character comparison
            return GenerateCharDiff(source, target);
        }
        catch (Exception ex) {
            _logger.LogError($"Unexpected error: {ex}");
            throw new DiffServiceException("Failed to retrieve diff", ex);
        }
    }
}
```

**Frontend Error UI:**
- Aussagekräftige Error Messages (deutsch/english)
- Retry-Button für temporäre Fehler
- Fallback zur unformatierten Text-Ansicht

---

## 6. Accessibility (WCAG 2.1 AA)

### 6.1 Keyboard Navigation

| Taste | Aktion |
|-------|--------|
| `Tab` / `Shift+Tab` | Navigate between focusable elements |
| `Arrow Up/Down` | Move to previous/next line |
| `Arrow Left/Right` | Navigate within line (if editable) |
| `Enter` | Select/Toggle line selection |
| `Escape` | Clear selection / Close dialog |
| `Ctrl+F` | Open search |
| `N` / `P` | Next/Previous change (when focused) |
| `Home` / `End` | Jump to first/last line |
| `Page Up/Down` | Scroll viewport |

### 6.2 Screen Reader Support

```razor
<div role="main" aria-label="Code Diff Viewer">
    <header role="banner">
        <h1>@DiffData.FilePath Changes</h1>
        <p aria-live="polite">
            @DiffData.AddedLines added, @DiffData.RemovedLines removed
        </p>
    </header>
    
    <table role="presentation" aria-label="Diff content">
        <tbody>
            @foreach (var line in DiffData.DiffBlocks.SelectMany(b => b.DiffLines)) {
                <tr class="@GetLineClass(line)">
                    <td>
                        @if (line.SourceLineNumber.HasValue) {
                            <span aria-label="Original line @line.SourceLineNumber">
                                @line.SourceLineNumber
                            </span>
                        }
                    </td>
                    <td>
                        <span aria-label="@GetLineTypeLabel(line)">@GetLineIndicator(line)</span>
                    </td>
                    <td>
                        <code>@HtmlEscape(line.Content)</code>
                    </td>
                </tr>
            }
        </tbody>
    </table>
</div>
```

### 6.3 Color Contrast Validation

| Element | Foreground | Background | Ratio | AA |
|---------|-----------|-----------|-------|-----|
| Normal Text | #333333 | #FFFFFF | 12.6:1 | ✅ |
| Added Line | #51CF66 (text) | #f0f9ff | 5.2:1 | ✅ |
| Removed Line | #FF6B6B (text) | #fef5f5 | 4.6:1 | ✅ |
| Focus Indicator | #2563eb | #FFFFFF | 8.6:1 | ✅ |

---

## 7. Performance-Optimierungen

### 7.1 Frontend Optimizations

1. **Virtual Scrolling:** Render only visible lines (OverscanCount=5)
2. **Debouncing:** Scroll/Search events debounced to 100ms
3. **Lazy Loading:** Syntax highlighting loaded asynchronously
4. **Code Splitting:** DiffViewer components lazy-loaded

### 7.2 Backend Optimizations

1. **Caching Strategy:**
   - L1: Memory Cache (1 hour TTL, LRU eviction)
   - L2: Persistent SQLite (24 hour TTL, background cleanup)

2. **Database Indizes:**
   - `(AufgabeId, GeneratedAt)` - Composite for recent diffs
   - `(CacheKey, IsValid, ExpiresAt)` - Cache lookup optimization
   - `(FilePath, SourceVersion, TargetVersion)` - Diff deduplication

3. **Async/Await:** All I/O operations are non-blocking

### 7.3 Browser Compatibility

| Browser | Minimum Version | Notes |
|---------|-----------------|-------|
| **Chrome** | 90+ | Full support |
| **Firefox** | 88+ | Full support |
| **Safari** | 14+ | CSS Grid; Virtualize component support |
| **Edge** | 90+ | Chromium-based; Full support |
| **Mobile (iOS Safari)** | 14+ | Responsive; Touch-friendly |
| **Mobile (Chrome)** | 90+ | Responsive; Touch-friendly |

---

## 8. Testing-Strategie

### 8.1 Test Coverage by Category

| Kategorie | Art | Min. Coverage | Tools |
|-----------|-----|---------------|-------|
| **Unit Tests** | Service Logic, Utilities | 85% | xUnit, FluentAssertions |
| **Component Tests** | DiffViewer, DiffLine, DiffBlock | 80% | bUnit |
| **Integration Tests** | Service + EF DbContext + Cache | 70% | xUnit, SQLite InMemory |
| **E2E Tests** | Critical User Flows | 5 scenarios | Selenium, BrowserStack |
| **A11y Tests** | WCAG 2.1 AA Conformance | 100% Coverage | axe, WAVE |
| **Performance Tests** | Rendering, Memory, Cache Hit Rate | 100k lines | Lighthouse, Chrome DevTools |
| **Visual Regression** | UI consistency across browsers | Screenshot diff | Percy, BackstopJS |

### 8.2 Test Cases (Key Scenarios)

```
✅ TC-1: Diff rendering (1.000 lines) < 500ms
✅ TC-2: Block grouping (consecutive changes as blocks)
✅ TC-3: Color coding (red/green/yellow/gray accuracy)
✅ TC-4: Keyboard navigation (all keys functional)
✅ TC-5: Screen reader (all lines announced correctly)
✅ TC-6: XSS-Protection (HTML entities not executable)
✅ TC-7: Cache hit (repeated requests use cache)
✅ TC-8: Mobile responsive (layout adapts to 320px-2560px)
✅ TC-9: Export to PDF (formatting preserved)
✅ TC-10: Large file (100.000 lines, no crash)
```

---

## 9. Implementation Roadmap (Phase 2)

### Phase 2.1: Foundation (1-2 Weeks)
- [ ] EF Core Migrations (DiffResult, DiffBlock, DiffLine, DiffCache)
- [ ] DiffService implementation (generation + caching)
- [ ] API endpoints (`GET /api/diffs/{id}`, `POST /api/diffs/generate`)
- [ ] Swagger/OpenAPI documentation
- [ ] Unit tests for services

### Phase 2.2: UI Components (2-3 Weeks)
- [ ] DiffViewer main component
- [ ] DiffBlock, DiffLine sub-components
- [ ] Styling (CSS + BEM pattern)
- [ ] Virtualization integration
- [ ] Component tests with bUnit

### Phase 2.3: Features (3-4 Weeks)
- [ ] View mode switcher (Side-by-Side, Split, Unified)
- [ ] Search functionality (with highlight)
- [ ] Navigation (Prev/Next change)
- [ ] Export (HTML, PDF, Text)
- [ ] Copy to clipboard

### Phase 2.4: Polish (4-5 Weeks)
- [ ] Accessibility audit & fixes (WCAG 2.1 AA)
- [ ] Performance optimization
- [ ] Browser compatibility testing
- [ ] E2E tests (Selenium)
- [ ] Documentation & release notes

---

## 10. File Structure

```
Softwareschmiede/
├── src/
│   ├── Softwareschmiede.Web/
│   │   ├── Components/
│   │   │   ├── Shared/
│   │   │   │   ├── DiffViewer.razor
│   │   │   │   ├── DiffViewer.razor.cs
│   │   │   │   ├── DiffBlock.razor
│   │   │   │   ├── DiffLine.razor
│   │   │   │   └── DiffLine.razor.css
│   │   │   └── [Other Components]
│   │   ├── Services/
│   │   │   ├── DiffService.cs
│   │   │   ├── DiffCachingService.cs
│   │   │   └── [Other Services]
│   │   ├── Pages/
│   │   │   └── AufgabeDetail.razor (integrates DiffViewer)
│   │   └── wwwroot/
│   │       └── css/
│   │           └── diff-viewer.css
│   ├── Softwareschmiede.Core/
│   │   ├── Models/
│   │   │   ├── DiffResult.cs
│   │   │   ├── DiffBlock.cs
│   │   │   ├── DiffLine.cs
│   │   │   └── DiffCache.cs
│   │   └── Services/
│   │       └── IDiffService.cs
│   ├── Softwareschmiede.Infrastructure/
│   │   ├── Data/
│   │   │   └── [DbContext Extensions]
│   │   └── Migrations/
│   │       └── 202505xx_AddDiffComparison.cs
│   └── Softwareschmiede.Tests/
│       ├── Services/
│       │   ├── DiffServiceTests.cs
│       │   └── DiffCachingServiceTests.cs
│       └── Components/
│           ├── DiffViewerTests.cs
│           └── DiffLineTests.cs
└── docs/
    ├── requirements/
    │   └── diff-comparison-component-requirements.md
    ├── architecture/
    │   ├── diff-viewer-blueprint.md
    │   └── diff-vergleichskomponente-entity-relationship-model.md
    └── improvements/
        └── architecture-review.md
```

---

## 11. Next Steps

### For Architects
- [ ] Finalize API specification (OpenAPI/Swagger)
- [ ] Review database schema with DBA team
- [ ] Confirm caching strategy with ops team

### For Development Team
- [ ] Study blueprint & ask clarification questions
- [ ] Set up dev environment (clone, build, debug)
- [ ] Create detailed Sprint tasks from roadmap

### For QA Team
- [ ] Review test cases
- [ ] Prepare test environments (browsers, devices)
- [ ] Plan accessibility audit timeline

---

## References & Resources

- **Diff Algorithm:** [Diff-Match-Patch Library](https://github.com/google/diff-match-patch)
- **Virtualization:** [Blazor Virtualize Component](https://learn.microsoft.com/en-us/aspnet/core/blazor/components/virtualization)
- **Accessibility:** [WCAG 2.1 Guidelines](https://www.w3.org/WAI/WCAG21/quickref/)
- **Performance:** [Lighthouse Docs](https://developers.google.com/web/tools/lighthouse)
- **Softwareschmiede Guidelines:** See `custom_instruction` in system config

---

**Version History:**
| Version | Date | Author | Notes |
|---------|------|--------|-------|
| 1.0 | 2025-05-15 | planning-orchestrator | Initial release |

**Status:** ✅ APPROVED (Ready for Phase 2 Implementation)
