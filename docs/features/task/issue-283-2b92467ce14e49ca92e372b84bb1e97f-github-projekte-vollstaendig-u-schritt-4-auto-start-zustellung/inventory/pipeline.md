# Bestandsaufnahme-Detail: Dispatch-Pipeline und Startpfade

Zurück: [../inventory.md](../inventory.md)

## `ScmEventMonitoringService`

Datei: `src/Softwareschmiede/Application/Services/ScmEventMonitoringService.cs`

| Methode | Zeilen | Kurzbeschreibung |
|---------|--------|------------------|
| `RunOnceAsync` | 21-166 | Aufnahmephase (Z. 26-146): Repository-Auflösung, Enumeration, `ReadEventsAsync` je Referenz/Inhaltsart, ggf. `ScmEventTaskResolver.ResolveAsync` (Z. 129-136, nur bei `taskIds.Length == 0` + qualifizierter Beobachtung), `AcceptBatchAsync` (Z. 137-138). Dispatchphase (Z. 148-165): alle fälligen Inbox-Ereignisse (`Status != "Verarbeitet"`, `NextAttemptUtc` fällig oder fällige Outbox-Aktion) sequenziell über `ScmEventOrchestrator.DispatchAsync`. |
| `EnumerateRepositoryAsync` | 174-209 | repo-weite Referenz-Enumeration mit `enumeration/`-Checkpoint. |

Belegt: Ein im selben Lauf angelegter Inbox-Eintrag (Anlage → `AcceptBatchAsync` → Dispatchphase) wird noch in derselben `RunOnceAsync`-Ausführung dispatchet; `Monitor_QualifiedCommentOnUnassignedIssue_CreatesTaskAndInboxEntry` belegt `AufgabeStatus.Gestartet` nach dem Lauf.

## `ScmEventOrchestrator`

Datei: `src/Softwareschmiede/Application/Services/ScmEventOrchestrator.cs`

`DispatchCoreAsync` (Z. 72-226) — Prüfreihenfolge pro Zustellversuch:

1. Plugin muss `IScmEventSource` + `IScmEventResponder` sein (Z. 84-89).
2. Hostaktionen: „receipt"-Reaktion + fällige „waiting"-Kommentare (Z. 91-99).
3. `Status == "Verarbeitet"` → Ende (Z. 100); `DeliveryAttemptId != null` → `ReconcileAttemptAsync` (Z. 101-111); `NextAttemptUtc` in Zukunft → Return (Z. 114).
4. FIFO: älteres unverarbeitetes Ereignis derselben Aufgabe → Wartend (Z. 115-119).
5. Aufgabe gelöscht → Wartend; `BlockReason` (beendet/archiviert/`PausiertBisUtc` in Zukunft) → `WaitWithCommentAsync` (Z. 120-123).
6. Erneute Owner-/Rechte-Prüfung: `GetEventRepositoryAsync`, `RepositoryId`-Drift oder `!CanWrite` → Wartend mit Token-Anleitung; `GetRejection` mit aktueller Repo-Identität → Wartend „nicht mehr der bestätigte Repository-Owner" (Z. 124-131).
7. **`task.Status == Neu → tasks.SetStatusAsync(Gestartet)`** (Z. 132) — der Statusübergang des Auto-Starts.
8. Lebender Adapter mit anderem Prefix → Wartend; Alt-Session ohne Startvertrag → Wartend (Z. 134-138).
9. `adapter is null || !IsAlive` → CLI-Auflösung `ResolveDevelopmentAutomationPluginWithProjectScopeAsync(task.KiPluginPrefix, task.ProjektId)`; `null` → **`WaitWithCommentAsync` „Keine Standard-CLI konfiguriert. Ereignis öffnen und CLI auswählen."** (Z. 141-143). Sonst `ResolveDevelopmentAutomationPluginStrictAsync` + `CheckHealthAsync`, Simulator-Parameter `--scm-exchange`, dann `ProzessStartenUndCliStartenAsync` (kein `LokalerKlonPfad`) oder `CliNeustartenAsync` (Z. 144-151).
10. Nach Reload erneute Blocker-Prüfung (Z. 157-159) und erneut `Neu → Gestartet` (Z. 160).
11. `CliRunId` persistiert (Z. 163); `WaitForInputReadyAsync` ≠ Ready → Wartend mit Reason (TrustPrompt/Busy/Unknown, Z. 164-173); `CheckExchangeAccessAsync` ≠ Supported → Wartend (Z. 174-176).
12. `exchange.PrepareAsync` → Persistierung `ZustellungVorbereitet`/`DeliveryAttemptId`/`CliRunId`/`ExchangePath`/`Attempts++` (Z. 177-191) → `WritePromptAsync` → `Zugestellt` bzw. `ZustellungUngewiss` (Z. 192-202).

Fehlerbehandlung: `ScmProviderException` → `ErrorCode` + Backoff/`long.MaxValue` (Z. 205-211); `InvalidOperationException`/`IOException`/`UnauthorizedAccessException` → `NextAttemptUtc = long.MaxValue` + Wartehinweis-Kommentar (Z. 212-217).

Weitere Methoden:

| Methode | Zeilen | Zweck |
|---------|--------|-------|
| `DispatchAsync` | 21-26 | Einstieg; `ScmTaskCoordinator.Shared.RunAsync` um `DispatchCoreAsync`. |
| `OpenFromQueueAsync` | 29-70 | UI „Öffnen / CLI auswählen": Lease, Blocker-Prüfung (Z. 52), `Neu → Gestartet` (Z. 53), `select`-Callback (laufende Sitzung bestätigen oder Plugin wählen), `NextAttemptUtc = null`, Dispatch mit explizitem Prefix. |
| `ActivateForOpenAsync` | 229-235 | Aktiviert neue, unpausierte Aufgabe beim Öffnen. |
| `RetryAsync` | 238-247 | Löscht `NextAttemptUtc`/`ErrorCode` am Ereignis, an ausstehenden Outbox-Aktionen und **allen** Checkpoints; danach Dispatch. |
| `ConfirmReadyAsync` | 250-265 | Rungebundene Ready-Bestätigung; hebt `NextAttemptUtc`-Hartsperre auf. |
| `ResendAsync` | 268-282 | Expliziter Neuversand bei `ZustellungUngewiss`/`VerarbeitungUngeklärt` mit passender AttemptId. |
| `MutateAndDispatchAsync` | 284-311 | Gemeinsamer Lease-Rahmen für Retry/Confirm/Resend. |
| `ReconcileAttemptAsync` | 313-385 | `accepted.json`/Actions/`complete.json` → `InBearbeitung`/`Verarbeitet`/`VerarbeitungUngeklärt`; Outbox-Ausführung + `results/*.json`; Timeouts aus `ScmEventOptions`. |
| `BlockReason` | 387-389 | „Aufgabe beendet/archiviert; Start gesperrt." / „Aufgabe pausiert; Start gesperrt." / null. |
| `WaitAsync`/`WaitWithCommentAsync` | 391-405 | `Status="Wartend"` + Reason; letztere postet zusätzlich Wartehinweis-Kommentar (`<!-- softwareschmiede:event:…:waiting -->`). |
| `RenewLeaseAsync` | 407-420 | 15-s-Lease-Renewal während des Dispatchs. |

## `ScmTaskCoordinator`

Datei: `src/Softwareschmiede/Application/Services/ScmTaskCoordinator.cs`

- `RunAsync` (Z. 15-24): `SemaphoreSlim` pro Aufgabe + `AsyncLocal<IReadOnlySet<Guid>>` → **reentrant** für dieselbe Aufgabe. Der verschachtelte Aufruf `DispatchCoreAsync` → `EntwicklungsprozessService.ProzessStartenUndCliStartenAsync` (eigener `RunAsync`-Rahmen, `EntwicklungsprozessService.cs:132-133`) blockiert nicht.

## `EntwicklungsprozessService`

Datei: `src/Softwareschmiede/Application/Services/EntwicklungsprozessService.cs`

| Methode | Zeilen | Kurzbeschreibung |
|---------|--------|------------------|
| `ProzessStartenUndCliStartenAsync` | 124-133 | Unter Aufgabensperre: Pausen-/Beendet-Guard (Z. 146-150), bestehender `LokalerKlonPfad` → `CliNeustartenAsync` (Z. 153-157), sonst `ProzessStartenCoreAsync` (Klon+Branch) + `UpdateAsync` (setzt `KiPluginPrefix`) + `KiAusfuehrungsService.StartTerminalSessionAsync`; Fehler → `RollbackStartAsync` (Klon löschen + `StartZuruecksetzenAsync`, bei `strictPluginSelection` mit Vorherstatus) (Z. 164-202). |
| `CliNeustartenAsync` | 233-294 | Beendet-/Pausen-Guards (Z. 253-259), laufende Sitzung → Return (Z. 261-266), Plugin-Auflösung + `UpdateAsync`, `StartTerminalSessionAsync`. |
| `ProzessStartenCoreAsync` | 205-230 | `ResolveRepositoryAsync` → `ResolvePluginAsync` → `ValidateBaseBranchExistsAsync` → `PrepareCloneDirectoryAsync` (`gitPlugin.CloneRepositoryAsync` in `{Arbeitsverzeichnis}/softwareschmiede/{aufgabeId}`) → `SetupBranchAsync` → `FinalizeStartAsync`. |
| `SetupBranchAsync` | 556-592 | `ReviewSource`-PR → `CheckoutReviewSourceAsync`; `basisBranchName` ≠ Default → `CheckoutRemoteBranchAsync`; sonst `CreateNewTaskBranchAsync` (`ErstelleTaskBranchName`: `task/issue-{n}-{id}-{slug}`). |
| `FinalizeStartAsync` | 668-699 | Initialisierungs-/Startskript, `issue.md`, `.gitignore`, **`_aufgabeService.StartenAsync`**, Protokolleintrag. |
| `ResolveRepositoryAsync` | 747-791 | Nutzt `aufgabe.GitRepository` direkt, wenn gesetzt (bei angelegten Aufgaben stets der Fall). |
| `WirfWennPausiert` | 956-963 | `InvalidOperationException` bei aktiver Pause. |

## `AufgabeService` (Start-relevante Methoden)

Datei: `src/Softwareschmiede/Application/Services/AufgabeService.cs`

| Methode | Zeilen | Kurzbeschreibung |
|---------|--------|------------------|
| `CreateFromIssueEventAsync` | 254-318 | Anlage `Status=Neu`/`NichtGestartet`, `GitRepositoryId`, `IssueReferenz.SourceKey`-Dedup inkl. `DbUpdateException`-Re-Lookup. |
| `CreateFromPullRequestAsync` | 321-424 | Anlage `Status=Neu`/`NichtGestartet`, `PullRequestReferenz` Rolle `ReviewSource`, Unique-Index-Dedup. |
| `SetStatusAsync` | 903-916 | Statusübergang mit `ValidateStatusTransition` (Neu → Gestartet erlaubt). |
| `StartenAsync` | 765-787 | `Gestartet`/`Aktiv` + `GitArbeitsbereich(branchName, klonPfad, basisBranchName)`; Beendet/Archiviert-Guard. |
| `StartZuruecksetzenAsync` | 933-949 | Vollständiger Neuzustand nach fehlgeschlagener Startvorbereitung. |

## `ScmEventTaskResolver`

Datei: `src/Softwareschmiede/Application/Services/ScmEventTaskResolver.cs`

- `ResolveAsync` (Z. 46-50) → `ResolveIssueAsync` (Z. 52-74) / `ResolvePullRequestAsync` (Z. 76-106): Detailabruf (`ReadIssueAsync`/`ReadPullRequestAsync`), Nummerngleichheit, Bestandssuche (`IssueNummer` + `GitRepositoryId` ∈ Kandidaten bzw. `(Provider, RepositoryId, PullRequestNumber)`), `ChooseRepository` deterministisch (Z. 114-116), Anlage mit `ComposeBody`-Auftragstext (Z. 120-129), PR-Anlagerennen per Re-Query aufgelöst (Z. 99-105).
- **Keine CLI-/Status-Abhängigkeit**: Der Resolver prüft weder Standard-CLI noch Aufgabenstatus — Anlage und Wiederverwendung erfolgen unabhängig von der späteren Zustellbarkeit; `BlockReason` greift erst im Dispatch.
