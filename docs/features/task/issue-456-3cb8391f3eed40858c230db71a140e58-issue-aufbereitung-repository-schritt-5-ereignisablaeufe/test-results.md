# Testergebnisse Schritt 5 — Ereignisabläufe (Issue #457)

## Build

- `dotnet build` IntegrationTests (inkl. Softwareschmiede): 0 Warnungen, 0 Fehler (voller Build vor dem Lauf).
- `dotnet format Softwareschmiede.slnx --verify-no-changes`: sauber.

## Integrationstests

`--filter "FullyQualifiedName~TaskAssignment"` → **16/16 grün**, darunter neu:

- `Monitor_TriggerOnIssueWithPreparationTask_CreatesOneInstructionTaskAlongside`:
  Bei bestehender `IssuePreparation`-Aufgabe auf Issue 9 erzeugt ein qualifizierter
  Kommentar genau eine weitere Aufgabe (`Kind=Instruction`, Titel „Issue Titel 9");
  alle Ereignisse laufen auf die Anweisungsaufgabe; ein zweiter Poll erzeugt keine
  dritte Instanz. Belegt die #457-Duplikatgarantie über Ereignisarten hinweg.

## Hinweis

Doc-only-Schritt abgesehen vom Regressionstest; die übrigen TaskAssignment-Tests
(15) decken weiterhin Dedupe je Ereignisart (Issue, PR, Branch-/Closing-Kontext,
wiederholte Polls, unqualifizierte Ereignisse) unverändert ab.
