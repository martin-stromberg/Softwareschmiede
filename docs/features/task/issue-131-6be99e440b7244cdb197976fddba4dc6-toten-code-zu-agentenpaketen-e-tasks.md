# Tasks: Toten Code zu Agentenpaketen entfernen

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Verifikation | Grep-Endprüfung: keine externen Referenzen auf `AgentPackageInfo`, `IAgentPackageService`, `IAgentPackageFileService`, `AgentPackageReader`, `AgentPackageFileService`, `FileTreeNode`; keine DI-Registrierung in `Program.cs` | Offen | — |
| 2 | Code-Löschung | `src/Softwareschmiede/Domain/Interfaces/IAgentPackageService.cs` löschen | Offen | — |
| 3 | Code-Löschung | `src/Softwareschmiede/Domain/Interfaces/IAgentPackageFileService.cs` löschen | Offen | — |
| 4 | Code-Löschung | `src/Softwareschmiede/Domain/ValueObjects/AgentPackageInfo.cs` löschen | Offen | — |
| 5 | Code-Löschung | `src/Softwareschmiede/Domain/ValueObjects/FileTreeNode.cs` löschen (verwaist nach Löschung) | Offen | — |
| 6 | Code-Löschung | `src/Softwareschmiede/Infrastructure/Services/AgentPackageReader.cs` löschen | Offen | — |
| 7 | Code-Löschung | `src/Softwareschmiede/Infrastructure/Services/AgentPackageFileService.cs` löschen | Offen | — |
| 8 | Tests | `src/Softwareschmiede.Tests/Infrastructure/Services/AgentPackageReaderTests.cs` löschen | Offen | — |
| 9 | Tests | `src/Softwareschmiede.IntegrationTests/Services/AgentPackageFileServiceTests.cs` löschen | Offen | — |
| 10 | Dokumentation | `README.md`: Mermaid-Knoten `APL6["AgentPackageReader / IAgentPackageService"]` entfernen | Offen | — |
| 11 | Dokumentation | `README.md`: Mermaid-Knoten `INL6["AgentPackageReader"]` entfernen | Offen | — |
| 12 | Dokumentation | `README.md`: Test-Verweiszeile `AgentPackageReader I/O-Fallback` entfernen | Offen | — |
| 13 | Dokumentation | Gegenprüfung: Grep nach `AgentPackage` in `README.md` und `docs/**/*.md`; nur legitime `IKiPlugin`-Einträge in `docs/help/plugins/api.md` und Feature-Arbeitsdokumente verbleiben | Offen | — |
| 14 | Verifikation | Vollständigen `dotnet build` ausführen | Offen | — |
| 15 | Verifikation | `dotnet test` synchron mit `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` ausführen | Offen | — |
