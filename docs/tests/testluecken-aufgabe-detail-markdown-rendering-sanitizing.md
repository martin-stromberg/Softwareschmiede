# Testlückenanalyse – AufgabeDetail Markdown-Struktur & Sanitizing

## Kontext
- Scope:
  - `src/Softwareschmiede/Components/Pages/Aufgaben/AufgabeDetail.razor`
  - `src/Softwareschmiede/Components/Pages/Aufgaben/AufgabeDetail.razor.cs`
  - `src/Softwareschmiede/Application/Services/EntwicklungsprozessService.cs`
  - Zugehörige Tests in `AufgabeDetailFolgePromptTests` und `EntwicklungsprozessServiceTests`
- Stand: 2026-05-24

## Abgedeckte Kernbereiche

### 1) Strukturierung des KI-Arbeitsprotokolls
- Datumszeile als `# yyyy-MM-dd`
- Schrittblöcke als `## Schritt n`
- Fallback bei leerer Antwort (`Keine Ausgabe vorhanden.`)
- Normalisierung von Zeilenumbrüchen und Entfernen von Trailing-Whitespace

**Testbezug (Auszug):**
- `KiStartenAsync_ShouldPersistMarkdownArbeitsprotokoll_WithDateHeadingAndSeparatedSteps`
- `KiStartenAsync_ShouldPersistFallbackStep_WhenKiOutputIsWhitespaceOnly`
- `KiStartenAsync_ShouldNormalizeLineBreaks_AndKeepStepOrder`
- `BuildStreamingArbeitsprotokollMarkdown_ShouldCreateDateHeadingAndStepSections`
- `BuildStreamingArbeitsprotokollMarkdown_ShouldReturnFallback_WhenStreamingLinesAreEmpty`
- `BuildStreamingArbeitsprotokollMarkdown_ShouldSkipWhitespaceLines_AndKeepStepNumbersContinuous`
- `BuildStreamingArbeitsprotokollMarkdown_ShouldTrimTrailingWhitespace_PerStep`

### 2) Markdown-Rendering in der Webausgabe
- Rendering von `#` / `##` als echte HTML-Headings
- Einheitliche Ausgabe über `.protokoll-markdown.markdown-preview`

**Testbezug (Auszug):**
- `RenderProtokollInhalt_ShouldRenderMarkdownHeadings`
- `AufgabeDetailMarkupAndCss_ShouldContainExpectedProtokollClasses`

### 3) Sicherheits- und Fallback-Verhalten
- Sanitizing unsicherer Link-/Bild-Schemes (`javascript:`, `data:`, `vbscript:`)
- Case-insensitive Sanitization
- Entfernen von HTML-Event-Attributen (`on*`)
- Erhalt sicherer Schemes (`https`, `mailto`)
- Fallback-Encoding über `<pre>` bei leerem Inhalt

**Testbezug (Auszug):**
- `RenderProtokollInhalt_ShouldSanitizeJavascriptLinks`
- `RenderProtokollInhalt_ShouldSanitizeUnsafeLinkSchemes`
- `RenderProtokollInhalt_ShouldSanitizeUnsafeImageSchemes`
- `RenderProtokollInhalt_ShouldSanitizeUnsafeSchemesCaseInsensitive`
- `RenderProtokollInhalt_ShouldKeepSafeSchemesIntact`
- `SanitizeMarkdownHtml_ShouldRemoveHtmlEventHandlerAttributes`
- `SanitizeMarkdownHtml_ShouldReturnEmpty_WhenInputIsNullOrWhitespace`
- `RenderProtokollInhalt_ShouldReturnDashPre_WhenInputIsWhitespace`
- `BuildFallbackHtml_ShouldEncodeInput`

## Verbleibende Restlücke

1. **Direkter Exception-Pfad von `RenderProtokollInhalt`**
   - Der Catch-Pfad ist vorhanden und funktional dokumentiert.
   - Ein gezielter Test, der eine Exception direkt in `Markdown.ToHtml(...)` auslöst, ist ohne zusätzliche Test-Seam (Renderer-Factory/Adapter) nur eingeschränkt erzwingbar.
   - **Priorität:** niedrig (bestehende Fallback- und Sanitizing-Tests decken den Sicherheitskern bereits ab).

## Fazit

Die ehemals offenen Kernlücken für Markdown-Struktur, Rendering und Sanitizing sind im aktuellen Stand weitgehend geschlossen.  
Es verbleibt ein technischer Randfall zur expliziten Ausnahme-Injektion im Renderpfad.
