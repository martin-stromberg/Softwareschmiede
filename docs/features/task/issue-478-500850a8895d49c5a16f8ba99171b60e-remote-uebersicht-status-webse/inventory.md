# Bestandsaufnahme: Remote-Übersicht über externe Status-Webseite

Analysiert wurde der bestehende Anwendungscode (`src/`, `plugins/`, `Softwareschmiede.slnx`) bezogen auf die Anforderung einer externen Status-Webseite mit App-seitigem Relay-Client (Snapshot-Publisher), neuem Server-Dienst und GitHub-Kommentaren als Steuerkanal (`requirement.md`, Issue #478).

## Zusammenfassung

- **Aufgabenmodell (`WorkItem`) vollständig vorhanden:** Alle in der Anforderung genannten Snapshot-Felder existieren bereits persistiert — `Status`, `ExecutionStatus`, `RunStatus`, `ActiveRunId`, `LastHeartbeatUtc`, `PausedUntilUtc`, `Purpose`, `Kind`, `IssueReference`, `PullRequests`, `ScmCompletionReference`. „Wartet auf Anwender" ist fachlich über `WorkItemRunStatus.WaitingForInput` (persistierter Laufzeit-Substatus) plus `ScmIncomingEvent.Reason`-Wartegründe abbildbar.
- **SCM-Ereigniskanal komplett vorhanden:** `ScmEventMonitoringService` (Polling über `PeriodicBackgroundService`) → `ScmEventStore.AcceptBatchAsync` → `ScmIncomingEvent`-Warteschlange mit Statuskonstanten (`ScmIncomingEventStatus`) und `Reason`-Diagnosen → `ScmEventOrchestrator`-Dispatch mit Aufgaben-Lease (`ScmTaskLease`, flüchtige GUID-Owner). Ausgehende Aktionen laufen über `ScmOutgoingAction`-Outbox → `IScmEventResponder`. Der für Kommentar-Aktionen kritische Akteurscheck sitzt in `ScmEventValidationService.GetRejection` (Akteur = Repository-Owner oder `github-actions[bot]`).
- **Muster für den Relay-Client vorhanden:** `PeriodicBackgroundService` + `AddSingleton<T>` + `AddHostedService(sp => ...)` in `App.xaml.cs` wird bereits von sechs Diensten genutzt; `ScmEventStartupGate` hält Dienste bis nach `MigrateAsync` an; `TimeProvider` ist als Singleton registriert (testbar).
- **Einstellungspersistenz vorhanden:** `AppSettingService` (Key/`GetXxxAsync`-Konvention, scoped, EF `AppSetting`-Tabelle) für Aktivierung/URL/Instanz-ID; `ICredentialStore`/`WindowsCredentialStore` als Singleton für das Relay-Token. **Eine persistente Instanz-ID existiert nicht** (`ScmTaskLease.Owner`/`LeaseOwner` sind flüchtige pro-Dispatch-GUIDs, `ScmEventOrchestrator` Zeilen ~94/~133).
- **Kein ausgehender Echtzeit-Kanal vorhanden:** Es gibt weder SignalR- noch WebSocket-Abhängigkeiten im gesamten Repo; `Microsoft.AspNetCore.SignalR.Client` wäre eine neue NuGet-Abhängigkeit. Es gibt kein Server-/ASP.NET-Core-Hosting-Projekt in der Solution — der Server-Dienst wäre ein neues Projekt bzw. neues Repo.
- **Update-/Versionsinfrastruktur vorhanden:** `ApplicationVersionProvider` (liest `version.json`), `GitHubReleaseClient` (GitHub-Releases-API), `UpdateService` als Referenz für GitHub-API-Zugriffe per `HttpClient`.
- **Wartezustands-Diagnose vorhanden:** `ScmEventQueueViewModel`/`ScmEventQueueView` zeigen die Inbox bereits in der App an; die Wartegrund-Texte (`ScmCliWaitReason`, „CLI beschäftigt", `BlockReason` etc.) liegen als `ScmIncomingEvent.Reason` persistiert vor und sind direkt in einen Snapshot projizierbar.

Test-Ausgangszustand: Vollständiger Build erfolgreich (0 Warnungen, 0 Fehler). Unit-Test-Lane (`Category!=OsInterface`): **2825 erfolgreich, 0 fehlgeschlagen, 3 übersprungen**. Integrations-Lane: **449 erfolgreich, 0 fehlgeschlagen, 0 übersprungen**. Keine nachgewiesenen Fehlschläge; die `OsInterface`-Kategorie (E2E/FlaUI, ConPTY, Clipboard) wurde nicht ausgeführt — Details in [inventory/tests.md](inventory/tests.md).

## Details

- [Datenmodell](inventory/models.md) — `WorkItem`, `ScmIncomingEvent`, `PullRequestReference`, `ScmObservationCheckpoint`, `ScmOutgoingAction`, `AppSetting` u. a.
- [Logik](inventory/logic.md) — Hintergrunddienste, Orchestrator/Dispatch, Validierung, Einstellungen, Update/Version, DI-Registrierung
- [Enums](inventory/enums.md) — `WorkItemStatus`, `WorkItemRunStatus`, `ScmAccountMode`, PR-/Workflow-Stati u. a.
- [Interfaces](inventory/interfaces.md) — `IScmEventSource`, `IScmEventResponder`, `ICredentialStore`, Update-Contracts u. a.
- [Tests](inventory/tests.md) — Test-Ausgangszustand mit gesicherten Laufprotokollen und für das Feature relevante Bestandstests/Hilfsmethoden
