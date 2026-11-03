# Detail: Abschluss-Pfad und Sperr-Semantik

## `EntwicklungsprozessService.AbschliessenAsync` (Ist)

Reihenfolge heute (ohne Aufgabensperre):

1. `_aufgabeService.GetDetailAsync(aufgabeId)` — `InvalidOperationException`, wenn fehlend
2. `CanCompleteTaskAsync` — bei offenen To-dos `InvalidOperationException`
   (`AufgabeService.OffeneTodosFehlermeldungFormat`, Anzahl aus `aufgabe.Todos`)
3. `DeleteDirectoryForce(LokalerKlonPfad)`, falls gesetzt und existent
4. `_aufgabeService.AbschliessenAsync` — Status `Beendet`, `AusfuehrungsStatus Beendet`,
   `AbschlussDatum`, räumt `GitArbeitsbereich`, `AktiveRunId`, `LastHeartbeatUtc`,
   `LetzterCliStartUtc`, `LaufStatus`
5. `_protokollService.AddStatusUebergangAsync(vonStatus → Beendet)`

## `ScmTaskCompletionMonitoringService.CheckTaskAsync` (Ist)

- Vorprüfung ohne Sperre: keine offenen Ereignisse, Referenz geschlossen/gemergt,
  `ScmAbschlussErkanntUtc` gesetzt, Wartezeit abgelaufen.
- Unter `ScmTaskCoordinator.Shared.RunAsync(taskId)`:
  `ChangeTracker.Clear()` → Task neu laden → Revalidierung (Status, ErkanntUtc,
  Wartezeit, offene Ereignisse) → `KiAusfuehrungsService.StopCliAsync` →
  `EntwicklungsprozessService.AbschliessenAsync` → Protokolleintrag
  „Aufgabe automatisch beendet".
- Fehlerfänger: `InvalidOperationException`/`IOException`/`UnauthorizedAccessException`
  → Warnung, `ScmAbschlussErkanntUtc` bleibt persistiert, nächster Durchlauf versucht
  erneut.

## `TaskDetailViewModel.AufgabeAbschliessenAsync` (Ist)

- `CanCompleteTaskAsync` → bei offenen To-dos `FehlerMeldung` aus
  `OffeneTodosFehlermeldungFormat` + `TodoListViewModel.OffeneTodoCount`, kein Abschluss.
- `CancelScheduledPrompt` → `EntwicklungsprozessService.AbschliessenAsync` → `LadenAsync`.
- `KannAufgabeAbschliessen` = Status aktiv/wartend **und** `!_isCliRunning` —
  „kein laufender CLI" nur viewseitig gesichert.
- `_promptZeitVersandService` wird im VM auch an anderen Stellen verwendet
  (`Dispose`, `OnCliProcessStatusChanged`, `SchedulePrompt`,
  `GetScheduledPromptStatus`) — Feld bleibt erhalten.

## `ScmTaskCoordinator`-Semantik

- `RunAsync<T>`: Prüft `OwnedTasks.Value` (AsyncLocal); enthält es `taskId`, läuft
  `operation()` direkt — kein erneutes `WaitAsync`, kein Deadlock bei Schachtelung.
- `RunAsync` ohne Rückgabewert delegiert auf die generische Variante.
- Semaphoren werden pro `taskId` in einer `ConcurrentDictionary` gehalten
  (prozessweit, auch über DI-Instanzen hinweg).

## `ScmE2ERuntimeControl` (Ist)

- `runtime-*.command`-Dateien: `Replace=true` → `.locked` schreiben, auf `.release`
  warten, `StopCliAsync`, warten bis `GetScmPromptSession` nicht mehr lebt;
  danach in jedem Fall `StartTerminalSessionAsync` mit `--scm-exchange`.
- Zweck: E2E-Simulation eines konkurrierenden CLI-Neustarts (Sessions-Ersatz),
  um Zustell- und Bestätigungsflüsse gegen wechselnde Läufe zu prüfen.
  **Kein** Aufgaben-Abschluss: kein To-do-Check, keine Klon-Löschung, kein
  Statusübergang — daher kein Aufrufer des hier vereinheitlichten Vorgangs.
