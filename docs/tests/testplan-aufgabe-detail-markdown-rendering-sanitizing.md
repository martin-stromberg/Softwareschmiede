# Testplan – AufgabeDetail Markdown-Rendering & Sanitizing

## Eingabe
- Quelle: `docs/tests/testluecken-aufgabe-detail-markdown-rendering-sanitizing.md`
- Ziel: Strukturregeln des KI-Arbeitsprotokolls sowie sichere Markdown-Webdarstellung dauerhaft absichern.

## Umsetzungsstand (2026-05-24)

### AP-01 (P1) – Persistiertes Protokollformat absichern ✅
- Datei: `src/Softwareschmiede.Tests/Application/Services/EntwicklungsprozessServiceTests.cs`
- Umgesetzte Tests:
  - `KiStartenAsync_ShouldPersistMarkdownArbeitsprotokoll_WithDateHeadingAndSeparatedSteps`
  - `KiStartenAsync_ShouldPersistFallbackStep_WhenKiOutputIsWhitespaceOnly`
  - `KiStartenAsync_ShouldNormalizeLineBreaks_AndKeepStepOrder`

### AP-02 (P1) – UI-Rendering und Sanitizing absichern ✅
- Datei: `src/Softwareschmiede.Tests/Components/Pages/Aufgaben/AufgabeDetailFolgePromptTests.cs`
- Umgesetzte Tests:
  - `RenderProtokollInhalt_ShouldRenderMarkdownHeadings`
  - `RenderProtokollInhalt_ShouldSanitizeJavascriptLinks`
  - `RenderProtokollInhalt_ShouldSanitizeUnsafeLinkSchemes`
  - `RenderProtokollInhalt_ShouldSanitizeUnsafeImageSchemes`
  - `RenderProtokollInhalt_ShouldSanitizeUnsafeSchemesCaseInsensitive`
  - `RenderProtokollInhalt_ShouldKeepSafeSchemesIntact`
  - `SanitizeMarkdownHtml_ShouldRemoveHtmlEventHandlerAttributes`
  - `SanitizeMarkdownHtml_ShouldReturnEmpty_WhenInputIsNullOrWhitespace`
  - `RenderProtokollInhalt_ShouldReturnDashPre_WhenInputIsWhitespace`
  - `BuildFallbackHtml_ShouldEncodeInput`

### AP-03 (P1) – Streaming-Layoutregeln absichern ✅
- Datei: `src/Softwareschmiede.Tests/Components/Pages/Aufgaben/AufgabeDetailFolgePromptTests.cs`
- Umgesetzte Tests:
  - `BuildStreamingArbeitsprotokollMarkdown_ShouldCreateDateHeadingAndStepSections`
  - `BuildStreamingArbeitsprotokollMarkdown_ShouldReturnFallback_WhenStreamingLinesAreEmpty`
  - `BuildStreamingArbeitsprotokollMarkdown_ShouldSkipWhitespaceLines_AndKeepStepNumbersContinuous`
  - `BuildStreamingArbeitsprotokollMarkdown_ShouldTrimTrailingWhitespace_PerStep`
  - `AufgabeDetailMarkupAndCss_ShouldContainExpectedProtokollClasses`

## Verifikation

```powershell
dotnet test .\src\Softwareschmiede.Tests\Softwareschmiede.Tests.csproj --nologo --filter "FullyQualifiedName~EntwicklungsprozessServiceTests|FullyQualifiedName~AufgabeDetailFolgePromptTests"
```

## Restpunkt

- Direkte Exception-Injektion im `Markdown.ToHtml`-Pfad von `RenderProtokollInhalt` bleibt optionaler Ausbaupunkt (eigener Test-Seam erforderlich).
