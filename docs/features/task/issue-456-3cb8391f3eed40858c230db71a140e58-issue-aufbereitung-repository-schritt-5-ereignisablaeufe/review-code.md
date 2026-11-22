# Review Schritt 5 — Ereignisabläufe vereinheitlichen und dokumentieren (Issue #457)

## Umfang

- Neue verbindliche Hilfeseite `docs/help/plugins/github-ereignisse/ereignisablaeufe.md`:
  gemeinsame Pipeline (Eingang → Qualifizierung → Registrierung → Aufgabenzuordnung →
  Zustellung inkl. Access-Probe → Bearbeitung → Abschluss) plus vier Ereignisabläufe
  A–D (Issue erstellt, Trigger im Issue, Trigger im PR, Issue/PR geschlossen), jeweils
  abgeglichen gegen `ScmEventMonitoringService`, `ScmIssuePreparationMonitoringService`,
  `ScmEventTaskResolver`, `ScmEventOrchestrator` und `ScmTaskCompletionMonitoringService`.
- Dokumentierte Abweichungen zur #457-Skizze als Produktdesign: getrennte
  Aufbereitungsaufgabe statt Direktausführung bei Issue-Anlage; PR→Issue-Ermittlung
  über Branch-/Closing-Signale statt API-Lookup; Access-Probe nur einmalig bei frischer
  Sitzung; zwei Aufgaben (Aufbereitung + Anweisung) je Issue sind gewollte Isolation,
  keine Doppelinstanz.
- Dedupe-Regressionstest `Monitor_TriggerOnIssueWithPreparationTask_CreatesOneInstructionTaskAlongside`
  (TaskAssignment): Issue mit Aufbereitungsaufgabe + Trigger → genau eine zusätzliche
  kanonische `Instruction`-Aufgabe; wiederholte Polls erzeugen nichts weiter.
- `AddTaskAsync`-Testhelfer um `purpose`/`kind` erweitert.
- Index-Verlinkung + Release-Notes DE/EN.

## Prüfungen

- Keine Produktcode-Änderung — Schritt 5 ist Doku + Regressionstest (alle
  Verhaltensanforderungen waren bereits durch Schritte 1–4 bzw. Bestandscode abgedeckt;
  der Codeabgleich ergab keine Lücke, die Umsetzung erfordert hätte).
- Doku-Aussagen stichprobenartig gegen Quellcode verifiziert: Zweck-Ausschluss im
  Resolver, SourceKey-Dedup, Kontextsignale (Branch/Closing), Wiedervorlage-Countdown
  `ScmCompletionDetectedUtc`, Retry-Budget, Access-Probe nur bei frischer Sitzung.
- `dotnet format --verify-no-changes`: sauber.
