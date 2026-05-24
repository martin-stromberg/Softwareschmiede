# Testplan – AufgabeDetail: Projektanzeige unter dem Titel

## Ziel
Absichern, dass die Aufgabendetailseite den Projekttext direkt unter dem Titel korrekt, fallback-sicher und als reinen Text rendert.

## Scope
- `src/Softwareschmiede/Components/Pages/Aufgaben/AufgabeDetail.razor`
- `src/Softwareschmiede/Components/Pages/Aufgaben/AufgabeDetail.razor.cs`
- `src/Softwareschmiede/Application/Services/AufgabeService.cs`
- `src/Softwareschmiede.Tests/Components/Pages/Aufgaben/AufgabeDetailGitActionsBunitTests.cs`

## Abgedeckte Tests
- `AufgabeDetail_ShouldShowProjectNameBelowTitle_WhenProjectIsAssigned`
- `AufgabeDetail_ShouldShowProjectFallback_WhenProjectNameIsEmpty`
- `AufgabeDetail_ShouldShowProjectFallback_WhenProjectNameIsWhitespace`
- `AufgabeDetail_ShouldRenderProjectNameAsPlainText_WhenProjectNameContainsHtml`
- `AufgabeDetail_ShouldRenderProjectTextDirectlyBelowTitle`

## Qualitätskriterien
- Anzeige für alle Anwender ohne zusätzliche Berechtigungsprüfung sichtbar.
- Fallback-Text bei fehlendem Projektnamen stabil.
- Kein HTML-Injection-Risiko durch unescaped Ausgabe.

## Validierung
- Build: `dotnet build .\Softwareschmiede.slnx`
- Fokus-Testlauf: `dotnet test .\Softwareschmiede.slnx --no-build --filter "FullyQualifiedName~AufgabeDetailGitActionsBunitTests"`

