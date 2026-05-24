# Testlücken – Changed Artifact Detection & Agentendefinitions-Compliance

## Analysebasis
- Scope: Feature „Erkennung geänderter Planungsdokumente zusätzlich zu Codedateien“ + angrenzende Agentendefinitions-Compliance.
- Testlauf (dotnet):  
  - `dotnet test .\src\Softwareschmiede.Tests\Softwareschmiede.Tests.csproj --collect:"XPlat Code Coverage" --nologo` ✅  
  - `dotnet test .\Softwareschmiede.slnx --collect:"XPlat Code Coverage" --nologo` ⚠️ 8 bekannte Failures in `Softwareschmiede.IntegrationTests.Infrastructure.Plugins.LocalDirectoryPluginIntegrationTests` (umgebungs-/gitignore-bedingt), nicht im analysierten Featurekern.
- Coverage-Datei (Unit): `src/Softwareschmiede.Tests/TestResults/ea092c4e-5b72-4259-9b08-aa6deaa4d18e/coverage.cobertura.xml`

## Nicht getestete / unzureichend getestete Funktionalitäten (priorisiert)

### Hoch
1. **Fallback-Erkennung für Planungsdokumente ist nicht branch-getestet (FR-3 / AC-2 Risiko)**
   - **Datei/Methoden:**  
     - `src/Softwareschmiede/Application/Services/GitWorkspaceBrowserService.cs`  
     - `BuildSnapshot(...)` (Fallback-Zweig `planningDocuments.Count == 0`, Zeilen ~208–213)  
     - `IsPlanningDocumentPathFallback(...)` (Zeilen ~394–410)
   - **Fehlende Testfälle:**  
     - Snapshot mit ausschließlich pfadvarianten Planungsdateien, die nur über Fallback erkannt werden (z. B. gemischte Separatoren, führende `./`, nur `SourceRelativePath`).  
     - Negativfall: `.md` außerhalb der drei erlaubten docs-Pfade darf auch im Fallback **nicht** als Planungsdokument zählen.
   - **Risiko:** Planungsdokumente werden bei Randfällen nicht erkannt; Reporting/Prompt-Kontext unvollständig.

2. **Getrennte Weiterverwendung von `PlanningDocuments` außerhalb des Services nicht abgesichert**
   - **Datei/Methoden:**  
     - `src/Softwareschmiede/Domain/ValueObjects/WorkspaceSnapshot.cs` (`PlanningDocuments`)  
     - `src/Softwareschmiede/Components/Pages/Aufgaben/AufgabeDetail.razor.cs` (`LadeWorkspaceAsync`, Anzeige-/Folgefluss)
   - **Fehlende Testfälle:**  
     - Integration/BUnit-Test: Snapshot mit `CodeFiles` + `PlanningDocuments` wird in der UI/Reporting-Schicht getrennt verarbeitet/angezeigt.  
     - Integrationstest: Nur Planungsdokumente geändert ⇒ kein „keine Änderungen“-Pfad, korrekte Ausweisung.
   - **Risiko:** Feature bleibt funktional im Service vorhanden, geht aber in nachgelagerten Flows verloren (Regression ohne direkten Service-Fehler).

3. **Agentendefinitions-Compliance: Fehlerpfade in Copilot-CLI-Health/Start fehlen**
   - **Datei/Methoden:**  
     - `plugins/Softwareschmiede.Plugin.GitHubCopilot/GitHubCopilotPlugin.cs`  
     - `CheckHealthAsync(...)` (null-Result + `Win32Exception`, Zeilen ~343–352)  
     - `StartDevelopmentAsync(...)` (Win32Exception beim Stream-Enumerator, Zeilen ~228–232)
   - **Fehlende Testfälle:**  
     - `CheckHealthAsync` mit `null`-Ergebnis des CLI-Runners.  
     - `CheckHealthAsync` mit geworfener `Win32Exception`.  
     - `StartDevelopmentAsync` wirft erwartete `InvalidOperationException` inkl. Hinweistext bei fehlendem CLI.
   - **Risiko:** Unklare Fehlersignale im produktiven Betrieb, insbesondere bei fehlerhafter CLI-Installation.

### Mittel
4. **Copilot-ExecutablePath-Konfiguration ungetestet**
   - **Datei/Methoden:**  
     - `plugins/Softwareschmiede.Plugin.GitHubCopilot/GitHubCopilotPlugin.cs`  
     - `GetCopilotCommand()` (Zeilen ~106–114)
   - **Fehlende Testfälle:**  
     - Konfigurierter absoluter Pfad (inkl. führender/trailing Quotes) wird korrekt verwendet.
   - **Risiko:** Falsch konfigurierte Executable-Pfade bleiben unentdeckt bis Laufzeit.

5. **Kompatibilitätsprüfung für nicht vorhandene Agentenpaketpfade unzureichend getestet**
   - **Datei/Methoden:**  
     - `plugins/Softwareschmiede.Plugin.GitHubCopilot/GitHubCopilotPlugin.cs` – `IsAgentPackageCompatibleAsync(...)`  
     - `plugins/Softwareschmiede.Plugin.ClaudeCli/ClaudeCliPlugin.cs` – `GetAvailableAgentsAsync(...)`, `IsAgentPackageCompatibleAsync(...)`
   - **Fehlende Testfälle:**  
     - Nicht existierender Package-Pfad liefert robust `false`/`[]` ohne Nebenwirkungen.
   - **Risiko:** Instabile Behandlung bei fehlerhaften Paketpfaden in UI/Orchestrierung.

6. **Agent-Description-Fallback/Fehlerbehandlung nur teilweise getestet**
   - **Datei/Methoden:**  
     - `plugins/Softwareschmiede.Plugin.GitHubCopilot/GitHubCopilotPlugin.cs` – `ReadAgentDescription(...)`  
     - `plugins/Softwareschmiede.Plugin.ClaudeCli/ClaudeCliPlugin.cs` – `ReadAgentDescription(...)`
   - **Fehlende Testfälle:**  
     - Frontmatter ohne `description:` ⇒ erste inhaltliche Zeile wird korrekt verwendet (für GitHubCopilot-Plugin aktuell fehlend).  
     - Datei-Lesefehler (I/O) ⇒ `null` statt Exception.
   - **Risiko:** Inkonsistente Agentenbeschreibung bzw. Laufzeitfehler bei defekten Dateien.

### Niedrig
7. **Randpfade im GitWorkspaceBrowserService nur teilweise abgesichert**
   - **Datei/Methoden:**  
     - `src/Softwareschmiede/Application/Services/GitWorkspaceBrowserService.cs`  
     - `ReadCommitCountAsync(...)` Fehlerpfad (Zeilen ~120–123)  
     - `LoadPreviewAsync(...)` „Datei existiert nicht mehr“ + Textvorschau-Pfad (Zeilen ~83–114)  
     - `ReadHeadContentAsync(...)` Fehlerpfad (Zeilen ~322–324)  
     - `ReadStatusEntriesAsync(...)` kurze/inkonsistente Statuszeilen (Zeilen ~149–151)
   - **Fehlende Testfälle:**  
     - Commit-Count-CLI-Fehler ergibt CommitCount=0 ohne Abbruch.  
     - Vorschau für fehlende Datei mit Fallback auf HEAD-Inhalt.  
     - Vorschau normaler Textdatei inkl. `OriginalContent`-Ladung über `git show`.  
     - `git show`-Fehler liefert `OriginalContent = null`.  
     - Parser ignoriert zu kurze Statuszeilen robust.
   - **Risiko:** Fehlklassifikation/Preview-Inkonsistenz bei Git-Randzuständen.

8. **AgentPackageReader: I/O-Fehlerzweig ungetestet**
   - **Datei/Methoden:**  
     - `src/Softwareschmiede/Infrastructure/Services/AgentPackageReader.cs`  
     - `ReadFirstLine(...)` Catch-Zweig (Zeile ~76)
   - **Fehlende Testfälle:**  
     - Nicht lesbare `.agent.md`-Datei führt zu `Beschreibung = null` statt Exception.
   - **Risiko:** Gering; betrifft primär Robustheit bei beschädigten Paketdateien.

