# Testplan – Separates Arbeitsverzeichnis mit Git-Workflow-Fallback

## Eingabe
- Quelle: `docs/tests/testluecken-separates-arbeitsverzeichnis-git-workflow-fallback.md`
- Ziel: Kernlogik vollständig durch Unit-/Integration-nahe Tests absichern.

## Umgesetzte Arbeitspakete
1. **LocalDirectoryPlugin-Guards und Fehlerpfade**
   - Push/Pull `NotSupported` bei `InSourceDirectory`
   - Pull-Abbruch bei dirty Workspace
   - Push-Abbruch bei identischem Workspace/Source
   - Push-Abbruch bei fehlgeschlagenem Delete-Sync-Status
2. **Fallback-Pfade**
   - Invalides `WorkspaceMode` → Fallback auf `SeparateWorkingDirectory`
   - Fehler bei fehlendem Source-Fallback
   - Pull über konfiguriertes `SourceDirectory`, wenn Pointer/Remote fehlen
3. **GitOrchestrationService Pull-Hinweise**
   - No-Merge-Hinweis für `LocalDirectoryPlugin`
   - Standard-Remote-Text für andere Plugins

## Validierung
- `dotnet test .\Softwareschmiede.slnx` erfolgreich.
- Neue Testfälle sind in bestehende Klassen integriert und folgen der bestehenden Namenskonvention.

## Offene Punkte
- UI-Bestätigungsdialog/Bestätigungs-Flow für Pull in `AufgabeDetail` ist aktuell nicht Teil dieses Testpakets.
