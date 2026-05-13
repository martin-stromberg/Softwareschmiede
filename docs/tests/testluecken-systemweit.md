# Testlücken – Systemweite Coverage-Analyse (aktuell)

## Analysebasis

- Testlauf: `dotnet test .\Softwareschmiede.slnx --collect:"XPlat Code Coverage"`
- Tests: 340 erfolgreich, 0 fehlgeschlagen
- Coverage-Quellen: `TestResults/*/coverage.cobertura.xml`

## Nicht getestete Funktionalitäten (priorisiert)

### P1 – Kritische Lücken (Kernabläufe, 0% oder sehr hohe Lücke)

1. **Agentenpaket-Verwaltung UI (vollständig ungetestet)**
   - **Komponente:** Paket-/Dateibaum, Datei-Editor, Upload, Rename/Delete-Flows
   - **Dateien:**
     - `src/Softwareschmiede/Components/Pages/AgentenpaketeSeite.razor.cs` (386/386 uncovered)
     - `src/Softwareschmiede/Components/Pages/AgentenpaketeSeite.razor` (164/164 uncovered)

2. **Aufgaben-Detail UI Kernworkflow (weitgehend ungetestet)**
   - **Komponente:** Prozessstart, KI-Start/Folgeprompt, Commit/Push/Pull/Reset/PR, Abschluss/Abbruch/Archivierung
   - **Dateien:**
     - `src/Softwareschmiede/Components/Pages/Aufgaben/AufgabeDetail.razor` (164/164 uncovered)
     - `src/Softwareschmiede/Components/Pages/Aufgaben/AufgabeDetail.razor.cs` (278/472 uncovered)

3. **Projekt-UI Kernworkflow (weitgehend ungetestet)**
   - **Komponente:** Projektdetail, Repository-Plugin-Feldschema, Projektliste-Navigation/Erstellung
   - **Dateien:**
     - `src/Softwareschmiede/Components/Pages/Projekte/ProjektDetail.razor` (126/126 uncovered)
     - `src/Softwareschmiede/Components/Pages/Projekte/ProjektDetail.razor.cs` (69/154 uncovered)
     - `src/Softwareschmiede/Components/Pages/Projekte/ProjektListe.razor.cs` (36/36 uncovered)
     - `src/Softwareschmiede/Components/Pages/Projekte/ProjektListe.razor` (24/24 uncovered)

4. **Einstellungs-UI (kritische Teilbereiche ungetestet)**
   - **Komponente:** Plugin-Defaults speichern/zurücksetzen, Arbeitsverzeichnis-Flows
   - **Dateien:**
     - `src/Softwareschmiede/Components/Pages/Einstellungen.razor` (94/94 uncovered)
     - `src/Softwareschmiede/Components/Pages/Einstellungen.razor.cs` (35/348 uncovered)

5. **Startup/Hosting vollständig ungetestet**
   - **Komponente:** DI-Wiring, Middleware-Reihenfolge, App-Bootstrap
   - **Dateien:**
     - `src/Softwareschmiede/Program.cs` (50/50 uncovered)
     - `src/Softwareschmiede.Client/Program.cs` (4/4 uncovered)

6. **CLI-Prozesssteuerung (große Lücke)**
   - **Komponente:** Streaming-Pipeline, Channel-Completion, Prozess-Cleanup, Executable-Resolution
   - **Datei:**
     - `src/Softwareschmiede/Infrastructure/Services/CliRunner.cs` (125/185 uncovered)

7. **Credential- und Shutdown-Integration ungetestet**
   - **Komponente:** Windows Credential API, plattformspezifische Shutdown-Command-Ermittlung
   - **Dateien:**
     - `src/Softwareschmiede/Infrastructure/Services/WindowsCredentialStore.cs` (34/34 uncovered)
     - `src/Softwareschmiede/Infrastructure/Services/SystemShutdownService.cs` (33/33 uncovered)

---

### P2 – Hohe Lücken in zentralen Services

1. **KI-Ausführung/Session-Verwaltung**
   - **Komponente:** Status-Callbacks, Fehler-/Abbruchpfade, Subscriber-Fehlerisolierung
   - **Datei:** `src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs` (48/187 uncovered)

2. **Git-Orchestrierung**
   - **Komponente:** Guard-/Fehlerpfade (Branch/Repo), Repository-URL-Extraktion, PR-Randfälle
   - **Datei:** `src/Softwareschmiede/Application/Services/GitOrchestrationService.cs` (31/129 uncovered)

3. **Entwicklungsprozess-Orchestrierung**
   - **Komponente:** Kontextmodus/Komprimierung, atomare Context-File-Operationen, Abschluss-/Abbruchpfade
   - **Datei:** `src/Softwareschmiede/Application/Services/EntwicklungsprozessService.cs` (88/553 uncovered)

4. **AgentPackageFileService**
   - **Komponente:** Rename/Delete/Upload/Path-Security-Fehlerpfade, Tree-Refresh-Randfälle
   - **Datei:** `src/Softwareschmiede/Infrastructure/Services/AgentPackageFileService.cs` (47/263 uncovered)

5. **Arbeitsverzeichnis-Einstellungen**
   - **Komponente:** Pfadvalidierung/-normalisierung, Verzeichniserstellung-Fehlerfälle, Legacy-ValidatePathForSave
   - **Datei:** `src/Softwareschmiede/Application/Services/ArbeitsverzeichnisSettingsService.cs` (14/80 uncovered)

---

### P3 – Mittlere/geringere, aber vorhandene Lücken

1. **Plugin Discovery / Registrierung**
   - **Datei:** `src/Softwareschmiede/Infrastructure/Plugins/PluginManager.cs` (24/120 uncovered)

2. **UI-Nebenkomponenten vollständig ungetestet**
   - `src/Softwareschmiede/Components/Pages/Home.razor.cs` (25/25 uncovered)
   - `src/Softwareschmiede/Components/Pages/Home.razor` (21/21 uncovered)
   - `src/Softwareschmiede/Components/Pages/Aufgaben/NeueAufgabe.razor.cs` (63/63 uncovered)
   - `src/Softwareschmiede/Components/Pages/Aufgaben/NeueAufgabe.razor` (16/16 uncovered)
   - `src/Softwareschmiede/Components/Shared/StatusBadge.razor` (24/24 uncovered)
   - `src/Softwareschmiede/Components/Shared/ProjektStatusBadge.razor` (2/2 uncovered)
   - `src/Softwareschmiede/Components/Layout/MainLayout.razor` (5/5 uncovered)

3. **Plugin-Implementierungen mit Restlücken**
   - `plugins/Softwareschmiede.Plugin.LocalDirectory/LocalDirectoryPlugin.cs` (39/391 uncovered)
   - `plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.cs` (22/485 uncovered)
   - `plugins/Softwareschmiede.Plugin.ClaudeCli/ClaudeCliPlugin.cs` (7/177 uncovered)
   - `plugins/Softwareschmiede.Plugin.GitHubCopilot/GitHubCopilotPlugin.cs` (5/177 uncovered)

4. **Kleine Domain-/Contract-Lücken**
   - `src/Softwareschmiede/Domain/Entities/PluginKonfiguration.cs` (7/7 uncovered)
   - `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IGitPlugin.cs` (1/1 uncovered)
   - einzelne ValueObjects mit Teilabdeckung (`PluginSettingGroup`, `PluginSettingField`, `AgentInfo`)

## Coverage-Lücken nach Komponentenbereich

- Startup/Hosting: **54/54 uncovered (100.0%)**
- UI Projekte-Seiten: **255/340 uncovered (75.0%)**
- UI Aufgaben-Seiten: **521/715 uncovered (72.9%)**
- UI sonstige Seiten: **733/1046 uncovered (70.1%)**
- UI Layout/Shared: **35/54 uncovered (64.8%)**
- Infrastructure Services: **240/598 uncovered (40.1%)**
- Infrastructure PluginManager: **24/120 uncovered (20.0%)**
- Application Services: **206/1625 uncovered (12.7%)**
- Domain Model: **9/84 uncovered (10.7%)**
- Plugin Contracts: **8/117 uncovered (6.8%)**
- Plugin Implementierungen: **73/1230 uncovered (5.9%)**
