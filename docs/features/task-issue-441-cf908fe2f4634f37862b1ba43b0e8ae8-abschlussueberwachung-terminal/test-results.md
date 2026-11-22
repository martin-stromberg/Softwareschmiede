# Test-Ergebnisse

## Ergebnis

**Status:** Keine Fehler

Ausgeführt (jeweils synchron, mit `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` inline, nach `dotnet build Softwareschmiede.slnx` mit 0 Fehlern):

- `dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category!=OsInterface" --collect:"XPlat Code Coverage" --logger "console;verbosity=normal"` → 2738 Tests, 2735 bestanden, 3 übersprungen, 0 fehlgeschlagen (~3:48 min)
- `dotnet test src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj --filter "Category!=OsInterface" --collect:"XPlat Code Coverage" --logger "console;verbosity=normal"` → 384 Tests, 384 bestanden, 0 fehlgeschlagen (~56 s)

Re-Verifikation nach Iteration 3 (identische Fachlogik — seit Iteration 1 nur Refactorings: Konstanten-Umstellung, Helper-Extraktion, Kommentarfix; ein neuer Fremdzuordnungs-Test hinzugekommen): beide Lanes erneut vollständig gelaufen — Unit 2735 bestanden / 0 fehlgeschlagen / 3 übersprungen, Integration 385 bestanden / 0 fehlgeschlagen.

Vollständige Konsolenausgaben: [test-logs/unittests.log](test-logs/unittests.log), [test-logs/integrationtests.log](test-logs/integrationtests.log). Coverage-Rohdaten: `src/Softwareschmiede.Tests/TestResults/ae69fdb5-cbf2-4bed-ac36-940ba99aa9e0/coverage.cobertura.xml` und `src/Softwareschmiede.IntegrationTests/TestResults/416c4726-07b4-4bd2-ba38-7a6ef3699464/coverage.cobertura.xml`.

Die 3 übersprungenen Tests sind plattformbedingte Skips auf Windows (`SystemShutdownServiceTests.ResolveShutdownCommand_Linux_RemainsUnchanged`, `…_MacOS_RemainsUnchanged`, `WorkingDirectoryOpenServiceTests.Open_OnNotWindows_ThrowsPlatformNotSupportedException`) — erwartetes Verhalten, feature-unabhängig.

Alle im Plan definierten neuen Tests existieren und sind grün:

- `ScmIncomingEventStatusTests` (Unit, neu): `NonBlocking_ShouldContainExactlyTheFourReleasedStatuses`, `Constants_ShouldNameAllTenKnownStatuses`, `NonBlocking_ShouldClassifyAllKnownStatuses` (10 Fälle), `NonBlocking_ShouldExcludeUnknownStatuses` (u. a. `"ZukuenftigerStatus"`, `""`).
- `ScmTaskCompletionMonitoringServiceTests` (Integration): `Monitor_EventWithStatus_CompletionGate` (11 Fälle inkl. unbekanntem Status; `VerarbeitungUngeklärt`/`ZustellungUngewiss`/`Verarbeitet`/`Abgebrochen` freigebend, alle anderen blockierend), `Monitor_UnresolvedEvent_CompletesTaskAfterWait`.
- `ScmEventDispatchTests_Ordering` (Integration, neu): `Dispatch_FollowUpBehindTerminalUnresolved_IsDelivered` (4 Fälle), `Dispatch_FollowUpBehindTransientPredecessor_StaysWaiting` (6 Fälle), `Dispatch_MultipleFollowUps_KeepRelativeOrder`.
- `ScmEventDispatchTests_Reconcile` (Integration, neu): `Reconcile_CompleteWithPullRequestNumberWithoutAccepted_RegistersReference`, `Reconcile_ReportedPullRequest_RepeatedReconcile_LogsOnce`, `Reconcile_ReportedPullRequestAssignedToOtherTask_KeepsForeignAssignment`, `Reconcile_LateCompleteAfterUnresolved_RegistersReference`, `Reconcile_FailedCompletenessCheck_StillRegistersReference`.

## Fehlgeschlagene Tests

Keine.

## E2E-Abdeckung

Keine neuen E2E-Tests geplant — Begründung aus `plan.md`/`plan-check.md` übernommen und als plausibel bestätigt: Die Änderung liegt vollständig in Hintergrunddiensten (`ScmTaskCompletionMonitoringService`, `ScmEventOrchestrator`); es gibt keinen neuen oder geänderten UI-Eingriffspunkt. Die Integrationstests fahren den kompletten Dienstpfad mit echter SQLite-DB, allen realen Services und gemockten Plugins und weisen die sichtbaren Wirkungen (`WorkItemStatus.Completed`, `PullRequestReference`-Zeile, `LogEntries`, `Zugestellt`-Übergang) nach.

| Szenario | Test / Testklasse | Ergebnis |
|----------|-------------------|----------|
| Bestehende E2E-Tests (`E2E_ScmEvents`, `E2E_SettingsFeatureFlags`) | — | Unverändert; gehören zur `Category=OsInterface`-Lane und wurden in dieser Umgebung nicht ausgeführt (Testlücke, siehe unten) |

## Reale End-to-End-Abnahme

| Szenario | Durchgeführt über | Ergebnis |
|----------|-------------------|----------|
| 1 — Auto-Abschluss trotz ungeklärtem Ereignis (Happy Path) | — | Nicht durchführbar |
| 2 — PR-Registrierung ohne `accepted.json` | — | Nicht durchführbar |
| 3 — Transientes Ereignis blockiert weiterhin | — | Nicht durchführbar |
| 4 — Verspätetes `complete.json` wird nachgeholt | — | Nicht durchführbar |
| 5 — Wieder geöffnete Referenz setzt Countdown zurück | — | Nicht durchführbar |
| 6 — `Wartend`-Folgeereignis hinter terminalem Vorgänger wird zugestellt | — | Nicht durchführbar |
| 7 — Reihenfolge unter Folgeereignissen bleibt gewahrt | — | Nicht durchführbar |
| 8 — Transienter Vorgänger hält Folgeereignis zurück | — | Nicht durchführbar |

**Begründung (gilt für alle acht Szenarien):** Eine reale Abnahme erfordert eine laufende `Softwareschmiede.App` mit eingerichtetem SCM-Provider und Agent-Plugin sowie eine Aufgabe mit echtem SCM-Ereignisfluss. Geprüfte Möglichkeiten und Blocker:

- **Kein konfigurierbarer SCM-Provider ohne UI:** Die Einrichtung (Provider-Credentials, Repository-Zuordnung, Aufgabenanlage, Plugin-Aktivierung) ist ausschließlich über die WPF-UI möglich — `Softwareschmiede.App` hat keinen Headless-/CLI-Konfigurationsmodus. UI-Automatisierung (FlaUI) gehört zur `Category=OsInterface`-Lane, die in dieser Sandbox dokumentiert instabil ist und projektregelkonform ausgeschlossen wurde.
- **ConPTY in dieser Sandbox bestätigt defekt** (siehe `CLAUDE.md`): Der für Szenario 1/6 zentrale Trigger „beendete CLI-Sitzung → `VerarbeitungUngeklärt`" hängt an der Prozess-/Sitzungssteuerung; die Umgebung kann diesen Pfad nicht zuverlässig reproduzieren.
- **Lokale Simulatoren geprüft:** `Softwareschmiede.Plugin.LocalDirectory` (SCM ohne GitHub) und `Softwareschmiede.Plugin.KiSimulator` (startet echte `powershell.exe`/`cmd.exe`-Prozesse, `AiSimulatorPlugin.cs` Z. 53–86) würden eine Abnahme ohne GitHub-Zugang grundsätzlich ermöglichen — sie lösen aber keinen der beiden UI-/Session-Blocker: Konfiguration und Aufgabenanlage bleiben UI-Pfade, und eine zweite App-Instanz würde gegen das Datenverzeichnis der laufenden Host-Instanz laufen.
- **Kein GitHub-Zugang/Credentials** in der Sandbox für den realen GitHub-Pfad.

Die Integrationstests decken alle acht Szenarien auf Dienstebene ab (echte SQLite-DB, echte Service-Kette, simulierte Plugins); dieser Nachweis ist in der E2E-Abdeckung bzw. oben dokumentiert. Eine manuelle Abnahme an einem realen, konfigurierten System bleibt ausstehend.

## Sichtprüfung der Abnahme-Screenshots

**Status:** Nicht erforderlich — keine UI-Szenarien.

## Zusammenfassung

- Gesamt: 3123
- Bestanden: 3120
- Fehlgeschlagen: 0
- Übersprungen: 3

Aufschlüsselung: Unit-Lane 2738 (2735/0/3), Integrations-Lane 385 (385/0/0 — Stand Iteration 3 inkl. `Reconcile_ReportedPullRequestAssignedToOtherTask_KeepsForeignAssignment`). `Category=OsInterface` wurde projektregelkonform in beiden Lanes ausgefiltert und ist eine dokumentierte Testlücke dieser Umgebung (instabile Spur: E2E, ConPTY, Clipboard, echte Prozessstarts).

## Testabdeckung

**Abdeckung:** 95,9 % Zeilen (Paket `Softwareschmiede`, Union beider Läufe, coverlet/Cobertura)

| Datei | Abdeckung |
|-------|-----------|
| `src/Softwareschmiede/Application/Services/ScmEventOrchestrator.cs` | 79,8 % Zeilen (Union beider Läufe) |

Kontext der im Branch geänderten Dateien (Union Unit + Integration, zeilenweise gemergt):

- `src/Softwareschmiede/Domain/Entities/ScmIncomingEventStatus.cs` — 100 % Zeilen
- `src/Softwareschmiede/Application/Services/ScmTaskCompletionMonitoringService.cs` — 87,4 % Zeilen
- `src/Softwareschmiede/Application/Services/ScmEventOrchestrator.cs` — 79,8 % Zeilen (große Bestandsdatei; Diff-Hunks weitgehend abgedeckt, eine neue Verzweigung unbedeckt — siehe „Fehlende Tests")

## Fehlende Tests

- `src/Softwareschmiede/Application/Services/ScmTaskCompletionMonitoringService.cs` — der erneute Blocker-Check unter der Aufgabensperre (Z. 137–138, `return` bei zwischenzeitlich blockierendem Ereignis) ist unbedeckt — Race-Fenster zwischen Kandidatenwahl und Abschluss, praktisch schwer deterministisch zu testen. (Der zuvor hier notierte unbedeckte Fremdzuordnungs-Zweig in `LinkReportedPullRequestAsync` ist seit Iteration 2 durch `Reconcile_ReportedPullRequestAssignedToOtherTask_KeepsForeignAssignment` abgedeckt.)
- `Category=OsInterface`-Lane — in dieser Sandbox nicht lauffähig (instabile Spur); als Testlücke der Umgebung dokumentiert, nicht als Feature-Lücke.
