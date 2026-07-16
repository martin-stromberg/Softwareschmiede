# Tasks: Toten Code zu Agentenpaketen entfernen

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Verifikation | Grep-Endprüfung: keine externen Referenzen auf `AgentPackageInfo`, `IAgentPackageService`, `IAgentPackageFileService`, `AgentPackageReader`, `AgentPackageFileService`, `FileTreeNode`; keine DI-Registrierung in `Program.cs` | Erledigt | Kein direkter Test — Grep über `src/` liefert 0 Treffer; `Softwareschmiede.csproj` kompiliert (0 Fehler) |
| 2 | Code-Löschung | `src/Softwareschmiede/Domain/Interfaces/IAgentPackageService.cs` löschen | Erledigt | Kein direkter Test — Datei entfernt; `Softwareschmiede.csproj` kompiliert (0 Fehler) |
| 3 | Code-Löschung | `src/Softwareschmiede/Domain/Interfaces/IAgentPackageFileService.cs` löschen | Erledigt | Kein direkter Test — Datei entfernt; `Softwareschmiede.csproj` kompiliert (0 Fehler) |
| 4 | Code-Löschung | `src/Softwareschmiede/Domain/ValueObjects/AgentPackageInfo.cs` löschen | Erledigt | Kein direkter Test — Datei entfernt; `Softwareschmiede.csproj` kompiliert (0 Fehler) |
| 5 | Code-Löschung | `src/Softwareschmiede/Domain/ValueObjects/FileTreeNode.cs` löschen (verwaist nach Löschung) | Erledigt | Kein direkter Test — Datei entfernt; `Softwareschmiede.csproj` kompiliert ohne verbleibende Referenz (0 Fehler) |
| 6 | Code-Löschung | `src/Softwareschmiede/Infrastructure/Services/AgentPackageReader.cs` löschen | Erledigt | Kein direkter Test — Datei entfernt; `Softwareschmiede.csproj` kompiliert (0 Fehler) |
| 7 | Code-Löschung | `src/Softwareschmiede/Infrastructure/Services/AgentPackageFileService.cs` löschen | Erledigt | Kein direkter Test — Datei entfernt; `Softwareschmiede.csproj` kompiliert (0 Fehler) |
| 8 | Tests | `src/Softwareschmiede.Tests/Infrastructure/Services/AgentPackageReaderTests.cs` löschen | Erledigt | Kein direkter Test — Datei entfernt; `Softwareschmiede.Tests.csproj` kompiliert (0 Fehler) |
| 9 | Tests | `src/Softwareschmiede.IntegrationTests/Services/AgentPackageFileServiceTests.cs` löschen | Erledigt | Kein direkter Test — Datei entfernt; `Softwareschmiede.IntegrationTests.csproj` kompiliert (0 Fehler) |
| 10 | Dokumentation | `README.md`: Mermaid-Knoten `APL6["AgentPackageReader / IAgentPackageService"]` entfernen | Erledigt | Kein direkter Test — Grep nach `APL6` in `README.md`: 0 Treffer |
| 11 | Dokumentation | `README.md`: Mermaid-Knoten `INL6["AgentPackageReader"]` entfernen | Erledigt | Kein direkter Test — Grep nach `INL6` in `README.md`: 0 Treffer |
| 12 | Dokumentation | `README.md`: Test-Verweiszeile `AgentPackageReader I/O-Fallback` entfernen | Erledigt | Kein direkter Test — Grep nach `AgentPackage` in `README.md`: 0 Treffer |
| 13 | Dokumentation | Gegenprüfung: Grep nach `AgentPackage` in `README.md` und `docs/**/*.md`; nur legitime `IKiPlugin`-Einträge in `docs/help/plugins/api.md` und Feature-Arbeitsdokumente verbleiben | Erledigt | Kein direkter Test — verbleibende Treffer ausschließlich in `docs/help/plugins/api.md` (bewusst erhalten) und Feature-Arbeitsdokumenten unter `docs/features/task/issue-131-.../` |
| 14 | Verifikation | Vollständigen `dotnet build` ausführen | Erledigt | `Softwareschmiede.csproj`, `Softwareschmiede.Tests.csproj`, `Softwareschmiede.IntegrationTests.csproj` jeweils 0 Fehler / 0 Warnungen (App-Projekt-Build gemäß Self-Hosting-Regel dem Lifecycle-Workflow überlassen) |
| 15 | Verifikation | `dotnet test` synchron mit `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` ausführen | Erledigt | `Softwareschmiede.Tests`: 986 erfolgreich, 1 übersprungen, 3 Fehler — alle 3 Fehler sind umgebungsbedingte WPF-E2E-/Sandbox-Probleme (`WaitForElement`-Timeout, `UnauthorizedAccessException` bei Temp-Verzeichnis-Cleanup), keiner referenziert einen entfernten Typ |
