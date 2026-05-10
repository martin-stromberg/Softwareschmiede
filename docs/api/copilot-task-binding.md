# API-Detail: GUID-präfixierte `.copilot-task`-Dateien im GitHubCopilot-Plugin

## Überblick
Dieses Dokument beschreibt das Laufzeitverhalten von `GitHubCopilotPlugin.StartDevelopmentAsync` für die GUID-Präfix-Lösung mit optionaler `executionId`, robuster `.gitignore`-Konsolidierung und garantiertem Cleanup.

Quellcode: `plugins/Softwareschmiede.Plugin.GitHubCopilot/GitHubCopilotPlugin.cs`

## Öffentliche Einstiegspunkte
```csharp
IAsyncEnumerable<string> StartDevelopmentAsync(
    string prompt,
    AgentInfo agent,
    string localRepoPath,
    string? model,
    string? executionId,
    CancellationToken ct = default);
```

## Vertragskonsolidierung (`startdevelopmentasync-test-overload-removal`)
- Es gilt genau ein öffentlicher Startvertrag: `StartDevelopmentAsync(prompt, agent, localRepoPath, model, executionId, ct)`.
- Der frühere test-spezifische Kurz-Overload wurde entfernt.
- Tests und Services rufen denselben kanonischen Vertrag auf; bei Bedarf wird `executionId` explizit als `null` übergeben.

## Ablauf
1. Optionales `executionId` validieren/normalisieren (`GUID` → Format `N`); bei `null`/leer neue GUID erzeugen.
2. Prompt-Datei als `{executionId}.copilot-task.md` im Repository schreiben.
3. `.gitignore` idempotent auf Zielregel `*.copilot-task.md` synchronisieren:
   - Legacy-Regeln (`/.copilot-task.md`, `.copilot-task.md`) werden konsolidiert.
   - Bereits korrekte Regel bleibt unverändert.
4. Copilot-CLI mit Dateibindung starten (`--prompt @<taskFile>`).
5. Ausgabe per Stream zurückgeben.
6. Cleanup der Task-Datei immer im `finally`; Cleanup-Fehler sind non-blocking (`Warning`).

## `executionId`-Semantik
- Zulässige Eingaben: .NET-GUID-Formate (`N`, `D`, `B`, `P`).
- Internes Zielformat: immer `N` (`^[0-9a-f]{32}$`).
- Ungültige Werte führen zu `ArgumentException` vor Datei- oder CLI-Operationen.

## `.gitignore`-Normalisierung
Die Regelprüfung ignoriert Slash-/Punkt-Präfixe und behandelt äquivalente Varianten konsistent. Kommentarzeilen (`# ...`) werden nicht als aktive Regeln gewertet.

## Fehlerverhalten
- `ArgumentException`: Ungültige `executionId`.
- `DirectoryNotFoundException`: Repository-Pfad fehlt.
- `IOException`: Schreib-/Sync-Fehler bei Task-Datei oder `.gitignore` (nach Retry).
- `OperationCanceledException`: Abbruch während I/O, Retry-Wartezeit oder Streaming.
- CLI-Fehler werden propagiert; Cleanup läuft trotzdem.

## Testnachweise
Siehe `src/Softwareschmiede.Tests/Infrastructure/Plugins/GitHubCopilotPluginTests.cs`, insbesondere:
- GUID-Dateinamenformat und `executionId`-Normalisierung
- Fehlerpfad bei ungültiger `executionId`
- Konsolidierung auf `*.copilot-task.md` ohne Duplikate
- Retry-/Abbruchverhalten bei `.gitignore`-Sperren
- Cleanup bei Erfolg und CLI-Fehler

## Traceability
- Requirements: `../requirements/startdevelopmentasync-test-overload-removal-requirements-analysis.md`
- Architektur: `../architecture/startdevelopmentasync-test-overload-removal-architecture-blueprint.md`
- ERM: `../architecture/startdevelopmentasync-test-overload-removal-entity-relationship-model.md`
- Review: `../improvements/startdevelopmentasync-test-overload-removal-architecture-review.md`
