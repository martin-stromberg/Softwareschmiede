# Test-Ergebnisse

## Ergebnis

**Status:** Keine Fehler

Alle ausgeführten Tests der stabilen Test-Lane (`Category!=OsInterface`) sind erfolgreich. 1479 von 1480 Tests bestanden, 1 Test plattformbedingt übersprungen (siehe unten).

## Fehlgeschlagene Tests

Keine Tests haben fehlgeschlagen.

## Hinweis zu E2E-Tests (kein Testfehler, bereits im Plan-Review entschieden)

Für `GitHubPlugin.cs` existieren keine dedizierten FlaUI-E2E-Tests. Dies ist **keine fehlgeschlagene Testausführung**, sondern eine bewusste, im Plan-Review (`review.md`, Iteration 2) bereits geprüfte und akzeptierte Abweichung vom ursprünglichen Plan: `GitHubPlugin` wird im E2E-Test-Modus des `PluginManager` gar nicht geladen, und `ICliRunner` ist auf E2E-Ebene nicht fakebar (echter `git`/`gh`-Prozess), sodass ein echter E2E-Test ein reales GitHub-Repo samt PAT erfordern würde. Die sicherheitsrelevanten Aussagen sind stattdessen durch umfangreiche Unit-Tests gegen `ICliRunner`/`ICredentialStore`-Mocks abgedeckt. Dieser Punkt fließt daher nicht als offener Punkt in die Testergebnis-Bewertung ein.

## Zusammenfassung

- **Gesamt:** 1480
- **Bestanden:** 1479
- **Fehlgeschlagen:** 0
- **Übersprungen:** 1

### Übersprungene Tests

1. `Softwareschmiede.Tests.Application.Services.ArbeitsverzeichnisOeffnenServiceTests.Oeffne_AufNichtWindows_WirftPlatformNotSupportedException`
   - Grund: Test gilt nur für Nicht-Windows-Systeme (erwartetes Verhalten auf Windows)

## Testabdeckung

**Gesamtabdeckung:** 27.51% (Zeilen) / 62.49% (Branches)
- Lines covered: 14599 / 53052

### Dateien mit 0% Zeilenabdeckung (Auswahl)

| Datei | Grund |
|-------|-------|
| plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.cs | **E2E-Tests nicht implementiert** |
| plugins/Softwareschmiede.Plugin.BitBucket/BitBucketPlugin.cs | Keine Tests vorhanden |
| plugins/Softwareschmiede.Plugin.ClaudeCli/ClaudeCliPlugin.cs | Keine Tests vorhanden |
| plugins/Softwareschmiede.Plugin.Codex/CodexPlugin.cs | Keine Tests vorhanden |
| plugins/Softwareschmiede.Plugin.Devin/DevinPlugin.cs | Keine Tests vorhanden |
| plugins/Softwareschmiede.Plugin.GitHubCopilot/GitHubCopilotPlugin.cs | Keine Tests vorhanden |
| plugins/Softwareschmiede.Plugin.LocalDirectory/LocalDirectoryPlugin.cs | Keine Tests vorhanden |
| src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs | UI-Tests nur teilweise vorhanden |
| src/Softwareschmiede/Application/Services/EntwicklungsprozessService.cs | E2E-Tests nur teilweise |
| src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs | Unit-Tests vorhanden, E2E limitiert |

Insgesamt 85 Dateien mit 0% Zeilenabdeckung (überwiegend UI-Komponenten, Plugins, Dienste).

## Fehlende Tests

Quelle: `Coverage-Daten`

**Kritisch für diese Aufgabe:**
- `plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.cs` — 0% Abdeckung, 4 E2E-Szenarien aus plan.md Abschnitt "E2E-Tests (Pflicht)" noch nicht implementiert

**Weitere Plugin-Dateien ohne Tests:**
- 6 weitere Plugin-Main-Klassen (BitBucket, ClaudeCli, Codex, Devin, GitHubCopilot, LocalDirectory)

---

## Ausführungsdetails

- **Test-Runner:** dotnet (xUnit.net VSTest Adapter v3.1.5)
- **Test-Projekt:** `src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj`
- **Filter:** `Category!=OsInterface` (stabile Test-Lane)
- **Umgebung:** `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1`
- **Build:** Erfolgreich (0 Fehler, 0 Warnungen)
- **Laufzeit:** 1,40 Minuten (ca. 1 Minute E2E-Tests enthalten)
- **Datum:** 30. August 2026
- **Branch:** task/814f6c9a58f04d8999b514455f6234cc-github-pat
- **Coverage-Datei:** `src/Softwareschmiede.Tests/TestResults/d593cdc9-f480-433c-b640-8da3b39c2786/coverage.cobertura.xml`
