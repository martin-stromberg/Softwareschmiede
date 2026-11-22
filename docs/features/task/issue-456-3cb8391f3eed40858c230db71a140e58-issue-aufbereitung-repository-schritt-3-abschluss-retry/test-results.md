# Testergebnisse — Schritt 3 (Issue #433)

## Fokussierte Suite

`SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.IntegrationTests --filter "FullyQualifiedName~ScmTaskCompletionMonitoringServiceTests"`
→ **30/30 bestanden** (inkl. 3 neuer Tests).

## Neue Tests

- `Monitor_FailingClosureCheck_BacksOffAndSuspendsAfterBudget`: Backoff-Gate überspringt
  Polls ohne Provider-Aufruf; 10 Versuche → Suspension-Eintrag; danach keine Aufrufe mehr.
- `Monitor_CompletionLockedClone_SuspendsAfterBudgetAndDiagnosesHolder`: Realer
  `FileStream`-Lock auf eine Klon-Datei → Abschlussversuche scheitern → Suspension-Eintrag
  enthält „offen gehalten von" (Restart-Manager-Diagnose findet den Testhost).
- `Monitor_CompletionRecovers_CompletesAfterBackoff`: Nach Lock-Freigabe und Backoff-Ablauf
  schließt die Aufgabe regulär ab (Budget-Heilung).

## Build/Format

- `dotnet build src/Softwareschmiede` + Integrationstest-Projekt: 0 Warnungen, 0 Fehler.
- `dotnet format Softwareschmiede.slnx --verify-no-changes`: sauber.
