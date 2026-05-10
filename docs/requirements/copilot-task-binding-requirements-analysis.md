# Requirements Analysis: `.copilot-task.md` Binding and `.gitignore` Synchronization

## 1. Überblick
- **Ziel:** Der übergebene Prompt wird als `.copilot-task.md` im lokalen Repository persistiert und als bindende Anforderung an das `copilot`-CLI übergeben.
- **Scope:** `GitHubCopilotPlugin.StartDevelopmentAsync` inkl. Dateischreiben, Ignore-Regel-Synchronisation und CLI-Start.
- **Implementierungsstand:** Abgeschlossen und durch Unit-Tests abgesichert.

## 2. Funktionale Anforderungen (Soll/Ist)
| Kennung | Beschreibung | Priorität | Status | Nachweis |
|---------|--------------|-----------|--------|----------|
| FR-1 | Prompt wird vor CLI-Start in `{repo}/.copilot-task.md` geschrieben. | MUST | ✅ Implementiert | `plugins/Softwareschmiede.Plugin.GitHubCopilot/GitHubCopilotPlugin.cs` |
| FR-2 | `.gitignore` enthält genau eine Regel für `/.copilot-task.md`. | MUST | ✅ Implementiert | `EnsureGitIgnoreRuleAsync` + Tests |
| FR-2.1 | Bereits vorhandene äquivalente Regeln führen zu keinem Duplikat. | HIGH | ✅ Implementiert | `IsEquivalentGitIgnoreRule` + Tests |
| FR-2.2 | Kommentierte Zeilen gelten nicht als aktive Regel. | HIGH | ✅ Implementiert | Testfall „commented rule“ |
| FR-3 | CLI liest den Prompt über `--prompt @<dateiPfad>`. | MUST | ✅ Implementiert | `BuildCopilotArgs` |
| FR-4 | Bei nicht nutzbarem Repository-Pfad erfolgt Fail-Fast. | MUST | ✅ Implementiert | `DirectoryNotFoundException` in `StartDevelopmentAsync` |
| FR-5 | Bei dauerhaftem Schreibfehler in `.gitignore` wird kein CLI-Aufruf gestartet. | MUST | ✅ Implementiert | Retry-Logik + IOException-Test |

## 3. Nicht-funktionale Anforderungen (Soll/Ist)
| Kennung | Beschreibung | Status | Nachweis |
|---------|--------------|--------|----------|
| NFR-1 | Deterministisch: Keine doppelten `.gitignore`-Einträge bei wiederholter Ausführung. | ✅ Erfüllt | Mehrere Idempotenz-Tests |
| NFR-2 | Robustheit bei kurzzeitiger Dateisperre (Retry). | ✅ Erfüllt | Retry-Test mit transientem Lock |
| NFR-3 | Klares Logging über Datei-Erstellung und Sync-Status. | ✅ Erfüllt | `LogInformation` in `StartDevelopmentAsync` |
| NFR-4 | UTF-8 ohne BOM für `.gitignore`-Schreibvorgang. | ✅ Erfüllt | `Utf8NoBom` in Plugin |

## 4. Akzeptanzkriterien (abgenommen)
1. Beim Start eines KI-Laufs existiert `.copilot-task.md` mit exaktem Promptinhalt.
2. Die Regel `/.copilot-task.md` wird bei Bedarf ergänzt, sonst nicht dupliziert.
3. Der CLI-Aufruf nutzt die Datei als Promptquelle (`@...`) und startet erst nach erfolgreichem Sync.
4. Fehlerfälle (Pfad fehlt, Schreibfehler, Cancellation) führen zu reproduzierbaren Exceptions.

## 5. Testabdeckung (ausgewählte Nachweise)
- `StartDevelopmentAsync_ShouldWritePromptFile_AndPassAgentAndModelToCli`
- `StartDevelopmentAsync_ShouldAppendGitIgnoreRule_WhenMissing`
- `StartDevelopmentAsync_ShouldNotDuplicateGitIgnoreRule_WhenEquivalentRuleExists`
- `StartDevelopmentAsync_ShouldCreateGitIgnoreAndInsertRule_WhenGitIgnoreDoesNotExist`
- `StartDevelopmentAsync_ShouldThrowIOException_WhenGitIgnoreWriteFailsAfterMaxRetries`
- `StartDevelopmentAsync_ShouldRetryGitIgnoreWriteAndSucceed_OnTransientIOException`

Quelle: `src/Softwareschmiede.Tests/Infrastructure/Plugins/GitHubCopilotPluginTests.cs`

## 6. Out-of-Scope
- Mergen konkurrierender `.gitignore`-Änderungen aus mehreren parallelen Prozessen.
- Historisierung oder Versionierung von `.copilot-task.md`.
- Parsing strukturierter Taskformate jenseits Markdown-Plaintext.

## 7. Versionierung
| Version | Datum | Änderung |
|---------|-------|----------|
| 1.0 | 2026-05-10 | Initiale Planungsanforderung |
| 2.0 | 2026-05-10 | Auf finalen Implementierungsstand und Testnachweise aktualisiert |


