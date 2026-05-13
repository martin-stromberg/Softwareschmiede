# Testlücken – Feature „Separates Arbeitsverzeichnis mit Git-Workflow-Fallback“

## Identifizierte Lücken (Fokus Kernlogik)

- [x] Git-Fallback im Workspace
  - [x] Ungültiger `WorkspaceMode` fällt auf `SeparateWorkingDirectory` zurück
  - [x] Fehler bei fehlendem Source-Fallback (`SourceDirectory`) ist abgesichert
  - [x] Source-Fallback über konfigurierte `SourceDirectory`, wenn Pointer/Remote fehlen
- [x] Pull ohne Merge
  - [x] Guard für falschen Workspace-Mode (`NotSupported`)
  - [x] Fehlerpfad für dirty Workspace
- [x] Push als Dateisync inkl. Delete-Sync
  - [x] Guard für falschen Workspace-Mode (`NotSupported`)
  - [x] Guard, wenn Workspace und Source identisch sind
  - [x] Fehlerpfad bei fehlgeschlagenem `git status --porcelain`
- [x] Nutzerhinweis beim Pull (Service-Protokoll)
  - [x] „Kein Merge“-Logtext nur für `LocalDirectoryPlugin`
  - [x] Standard-Pull-Logtext für andere Plugins

## Verbleibende Restpunkte

- [ ] UI-seitige Vorab-Bestätigung vor Pull (Button-Flow in `AufgabeDetail`) ist weiterhin nicht automatisiert getestet.
