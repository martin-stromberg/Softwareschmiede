# Code-Review — Schritt 3: Begrenzte, sichtbare Retry-Behandlung der Abschlussüberwachung (Issue #433)

## Umsetzung

- `ScmTaskCompletionMonitoringService`: Neuer Budget-/Backoff-Rahmen um `CheckTaskAsync`.
  - `_retries: ConcurrentDictionary<Guid, CompletionRetryState>` — Zustand je Aufgabe (in-memory, dokumentiert).
  - Gate vor jedem Durchlauf: `Exhausted` oder `now < NextAttemptUtc` → Skip ohne Provider-/DB-Zugriff.
  - `CheckTaskCoreAsync` enthält den bisherigen Ablauf unverändert (Kandidaten-, Freigabe-,
    Countdown- und Koordinatorlogik); Fehlschläge aus `DetermineClosureAsync` (SCM-Provider,
    DB, Plugin) und aus `DevelopmentProcessService.CompleteAsync` (offene To-Dos,
    `IOException`/`UnauthorizedAccessException` beim Klon-Delete) zählen gemeinsam ins Budget.
  - `RecordFailureAsync`: Signatur = Exception-Typ + gekürzte Meldung (ohne Diagnose-Detail,
    damit fluktuierende Prozesslisten das Budget nicht zurücksetzen). Signaturwechsel →
    eigenes Budget; 10 Versuche → `Exhausted` + einmaliger `SystemMessage`-Protokolleintrag
    + `LogError`. Erster Fehlschlag `LogWarning`, Wiederholungen `LogDebug` (Log-Dedupe).
  - Erfolgreicher Durchlauf ohne neuen Fehlschlag entfernt den Eintrag → Budget heilt.
  - `RunOnceAsync` räumt Einträge ab, deren Aufgabe die Kandidatenmenge verlassen hat.
- `LockedFileProcessDiagnostics` (neu): Windows-Restart-Manager-Diagnose (`RmStartSession`/
  `RmRegisterResources`/`RmGetList`/`RmEndSession`), best-effort, wirft nie. Registriert
  ausschließlich Datei-Ressourcen (Verzeichnispfade lassen `RmGetList` mit
  `ERROR_ACCESS_DENIED` fehlschlagen — verifiziert); läuft nur beim ersten Fehlschlag
  einer Signatur. Ergebnis fließt als „offen gehalten von: …" in den Suspension-Eintrag.

## Offene Punkte / akzeptierte Grenzen

- Budget ist in-memory: Der dokumentierte Wiederanstoß nach Erschöpfung ist App-Neustart
  oder manueller Abschluss (im Protokolleintrag benannt). Persistente Budgets wären ein
  eigenes Feature (EF-Felder); bewusst nicht gebaut, da die Aufgabe nach Erschöpfung ohnehin
  eine manuelle Klärung braucht.
- Verzeichnis-Handles (cwd eines Prozesses) erkennt der Restart Manager nicht — nur
  Datei-Sperren. Als best-effort-Diagnose akzeptiert und in der Doku benannt.
- Kein UI-Badge: Der sichtbare Fehlerzustand ist der Protokolleintrag (SystemMessage),
  konsistent zum bisherigen Fehler-Surfacing des Dienstes.
