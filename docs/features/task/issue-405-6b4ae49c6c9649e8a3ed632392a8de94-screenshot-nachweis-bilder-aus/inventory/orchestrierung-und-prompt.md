# Detailinventar: Orchestrierung, Prompt-Anhänge (`promptSuffix`), Upload-/MIME-Konventionen

Bestandteil von [inventory.md](../inventory.md).

## Orchestrierung (`src/Softwareschmiede/Application/Services/`)

### Ablauf-Zustellung (`ScmEventOrchestrator.DispatchCoreAsync`, Z. 74–256)

1. Hostaktionen nachholen (receipt-`eyes`, `waiting`-Outbox) — Z. 95–103.
2. `DeliveryAttemptId != null` → `ReconcileAttemptAsync` (Z. 105–115).
3. Revalidierung (Repository via `IScmEventSource.GetEventRepositoryAsync`, Trigger, Owner, BlockReason) — Z. 124–146.
4. Sitzungsmanagement: `cli.GetScmPromptSession(task.Id)`, ggf. CLI-Start über `EntwicklungsprozessService` / `PluginSelectionService` — Z. 148–170.
5. Kanalprüfung: `adapter.CheckExchangeAccessAsync(Path.Combine(adapter.WorkingDirectory, ScmEventExchangeProtocol.DirectoryName), ct, task.Titel)` — Z. 195–197.
6. Prompt-Anhang: `ScmPromptAppendix.Resolve(observation, await einstellungen.GetScmPromptAppendicesAsync(ct))` → `exchange.PrepareAsync(..., access.RestrictedWriteSids, promptSuffix)` — Z. 202–204.
7. Persistenz (`ExchangePath`, `ExchangeRestrictedWriteSidsJson`, `CliRunId`, `DeliveredUtc`, Status `ZustellungVorbereitet`) → `adapter.WritePromptAsync(attempt.Prompt)` → `Zugestellt`/`ZustellungUngewiss` — Z. 209–232.
8. Fehlerpfade: `ScmProviderException` → `ErrorCode` + Backoff (Z. 235–241); Konfigurations-/Workspacefehler → `Wartend` + Kommentar (Z. 242–247).

### Abschlussverarbeitung (`ReconcileAttemptAsync`, Z. 381–462)

- Rekonstruiert Versuch aus `ExchangePath`/`CliRunId`/`DeliveryAttemptId`/`ExchangeRestrictedWriteSidsJson` (Z. 383–390) → `exchange.ReadAsync`.
- `Diagnostic != null` → `VerarbeitungUngeklärt` (Z. 393–397).
- `Accepted` → `InBearbeitung`, `AcceptedUtc` (Z. 398–402); jede Kanalaktion → `ScmResponseRequest` + Outbox-Ausführung + `WriteResultAsync` (Z. 403–425).
- **`messages.Complete`-Block (Z. 426–436):** `Verarbeitet` nur, wenn alle Kanalaktionen des Versuchs `Succeeded` sind und die ID-Menge mit `complete.ActionIds` übereinstimmt — bzw. `NoRemoteAction` ohne jede Aktion. **`complete.Report` wird nur gelesen, nicht weiterverwendet** — der Host postet selbst keinen „Abschlussbericht"; der an GitHub gesendete Berichtstext ist die `body` einer `comment`-Kanalaktion (→ `ResponseBody`) oder ein vom Agenten selbst per `gh` gesetzter Kommentar.
- `PullRequestNumber` → `LinkReportedPullRequestAsync` (Z. 440–441, 464–482): Repository/Host ausschließlich aus gespeichertem Ursprung; Protokolleintrag via `ProtokollService.AddEintragAsync` (`ProtokollTyp.GitAktion`).
- Timeouts: `AcceptanceTimeout`/`CompletionTimeout` aus `ScmEventOptions` (Z. 449–458).

### Hintergrund- und Wartewege

- `ScmEventExchangeService` (`ScmEventExchangeService.cs`): 2-Sekunden-Poll über alle nicht abgeschlossenen Ereignisse mit `DeliveryAttemptId` → `DispatchAsync`; läuft nur bei aktiviertem `scm.monitoring.enabled` (Z. 21–25). Zwei Läufe nötig, weil `ReadStableAsync` erst die zweite identische Beobachtung liefert.
- `ScmEventOutbox` (s. [github-plugin.md](github-plugin.md)): `EnsureAsync`/`ExecuteAsync`; `ScmOutgoingAction`-Entity: `Id, EventId, AttemptId?, Purpose, RequestJson, Status, RemoteId, Attempts, NextAttemptUtc, ErrorCode, Error` (`src/Softwareschmiede/Domain/Entities/ScmOutgoingAction.cs`).
- `ScmIncomingEvent`-Statuswerte im Code: `Aufgenommen, Verzögert, Wartend, ZustellungVorbereitet, Zugestellt, ZustellungUngewiss, InBearbeitung, VerarbeitungUngeklärt, Verarbeitet, Abgebrochen`.

### Sitzungsadapter (`ScmPromptSessionAdapter.cs`, `ScmCliAccessEvaluator.cs`)

- `IScmPromptSessionAdapter` (Z. 24–55): `CliRunId`, `WorkingDirectory` (aus `TerminalSessionStartSpec.WorkingDirectory`, `GetFullPath`, Z. 105), `PluginPrefix`, `IsAlive`, `WaitForInputReadyAsync`, `CheckExchangeAccessAsync(exchangePath, ct, sessionName)`, `ConfirmReady`, `WritePromptAsync`.
- `ScmExchangeAccess` (`ScmCliAccessEvaluator.cs` Z. 21–26): `Status` (`Supported/Denied/Unknown`), `Reason`, `WorkingDirectory`, `CliRunId`, `ConfigurationFingerprint`, `RestrictedWriteSids`.
- `KiAusfuehrungsService.GetScmPromptSession(aufgabeId)` (Z. 480–482) liefert den lauf-gebundenen Adapter.

## Prompt-Anhänge / `promptSuffix`

- Einstellungsschlüssel (`AppEinstellungService` Z. 52–64): `scm.prompt.append.general|issue|issuecomment|pr|prcomment` — **keine Defaults**, fehlende Einträge = nichts anhängen; reine Anwenderkonfiguration in `AppEinstellungen` (DB). `GetScmPromptAppendicesAsync` (Z. 389–390) liest alle fünf in einer Abfrage (`GetSettingsAsync`, Z. 396–405); `SetSettingAsync` Z. 161.
- `ScmPromptAppendix` (`ScmPromptAppendix.cs`): `SettingKeys` (Z. 14–21), `SpecificKeyFor` (Z. 33–40 — `Description` → Issue-/PR-Anhang, sonst Kommentar-Anhang der Referenzart), `Combine` (Z. 50–57 — allgemein + spezifisch, `\n\n`-getrennt), `Resolve` (Z. 66–69).
- Übergabe: Orchestrator Z. 202 → `PrepareAsync(promptSuffix:)` (Protocol Z. 172) → `ScmExchangeAttempt.PromptSuffix` (Z. 32) → `BuildPrompt` ergänzt `envelope["promptSuffix"]` nur bei nicht-leerem Wert (Z. 309–310); die `instruction` (Z. 300) nennt das Feld. `request.json` bleibt anhangsfrei (Bitgleichheit beim Wiederanlauf); die Kanalprobe (`BuildAccessProbePrompt`) trägt nie einen Anhang.
- UI: `SettingsViewModel` Z. 545–550 (Laden) unter „Automatisierung → Aufgaben ausführen", Speicherpfad über `SetSettingAsync`. Es existiert **keine ausgelieferte Vorgabe/Vorlage** für den Anhangstext — die Screenshot-Forderung steht aktuell nur in anwenderseitig konfigurierten Texten.

## Datei-Upload-/HTTP-Content-/MIME-Konventionen

- Produktivcode kennt **keinen** binären HTTP-Upload: `GitHubHttpApiClient` sendet nur `StringContent(application/json)`; `GitHubEventApiRequest` hat nur `JsonBody`. Einzige `StreamContent`-Verwendung ist der E2E-Testseam `src/Softwareschmiede.App/Services/Testing/UpdateFixtureHttpMessageHandler.cs` (Z. 82–84).
- MIME-/Datei-Validierungskonvention existiert in `BenachrichtigungsEinstellungenService` (`src/Softwareschmiede/Application/Services/BenachrichtigungsEinstellungenService.cs`): Erweiterungs-Whitelist (Z. 15–19), MIME-Whitelist (Z. 21–30), `ValidateAudioDatei` (Z. 154–181: Nicht-leer, 10-MB-Limit `MaxAudioDateigroesseBytes`, Extension, MIME, **Magic-Bytes-Signatur** `HatGueltigeDateisignatur` Z. 210–256), `NormalizeMimeType` Extension→MIME (Z. 194–208), `SanitizeDateiname` (Z. 183–192). Das ist ein Benutzer→App-Upload (DB-BLOB via `BenachrichtigungsAudioDatei`), keine HTTP-Upload-Konvention.
- `UpdateE2ESzenario.cs`/Test-Helper setzen `ContentType`-Header — sonst keine MIME-Erkennung im Repo.

## Agenten-Seite (Simulator / E2E-Steuerung)

- `plugins/Softwareschmiede.Plugin.KiSimulator/KiSimulatorPlugin.cs` (Z. 109–212): deterministisches PowerShell-Programm im SCM-Modus (`--scm-exchange`, env `SOFTWARESCHMIEDE_SCM_SIMULATOR`); liest `requestPath`, prüft Korrelation, schreibt `accepted.json` → `actions/{id}.json` (comment) → wartet auf `results/{id}.json` → `complete.json` mit `actionIds`. Fixture-Gates: `hold-accepted`, `hold-complete`, `ignore-next-attempt`, `complete-attempt-{attemptId}`, `prompts.jsonl`, `errors.log`, `unknown-banner`, `unknown-ready` (Z. 113–175).
- `src/Softwareschmiede.App/Services/Testing/ScmE2ERuntimeControl.cs` (63 Z.): E2E-Laufzeitsteuerung per `runtime-*.command`-Dateien — startet Sessions mit `--scm-exchange`, kann Sitzungen ersetzen (`Replace`).
