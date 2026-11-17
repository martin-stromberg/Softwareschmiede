# Bestandsaufnahme: SCM-Ereignisverarbeitung, Aufgabenstatus und Shutdown

## Aufnahme-Pfad ereignisgesteuerter Aufgaben

- `ScmEventMonitoringService` (`src/Softwareschmiede/Application/Services/ScmEventMonitoringService.cs`):
  periodischer Hintergrunddienst (`PeriodicBackgroundService`, `ScmEventOptions.PollInterval`).
  Pro Plugin/Repository werden Inhalte der Arten `Description`, `IssueComment`,
  `ReviewComment`, `PullRequestReview` gelesen (`IScmEventSource.ReadEventsAsync`) und über
  `ScmEventStore.AcceptBatchAsync(aufgabeId, sourceKey, batch, unassigned, triggerKeyword, ct)`
  in `ScmIncomingEvents` persistiert. Triggerprüfung über
  `ScmEventValidationService.GetRejection` (frei stehendes Trigger-Keyword
  `$Softwareschmiede` bzw. Einstellung `scm.trigger.keyword`; im SeparatesKonto-Modus
  frei stehende Erwähnung des Bot-Kontos). Unzugeordnete Trigger erzeugen über
  `ScmEventTaskResolver` eine Aufgabe.
- `ScmIncomingEvent` trägt u. a. `Status` („Aufgenommen", „Wartend", „Zugestellt",
  „InBearbeitung", „Verarbeitet", „Abgebrochen", „VerarbeitungUngeklärt" u. a.),
  `NextAttemptUtc` (Unix-ms; verschiebt die nächste Verarbeitung), `Attempts`,
  `DeliveryAttemptId`, `CliRunId`, `AufgabeId`, `Sequence`.
- Zustellung: `ScmEventMonitoringService.RunOnceAsync` wählt Ereignisse mit
  `Status != Verarbeitet/Abgebrochen` und `NextAttemptUtc <= now` (oder fällige
  Outbox-Aktionen) sequenziell aus und ruft `ScmEventOrchestrator.DispatchAsync`.
  **Ein gesetztes `NextAttemptUtc` in der Zukunft stellt ein Ereignis bereits
  heute zuverlässig zurück — Wiederaufnahme erfolgt automatisch im Poll.**

## Zustellung / Dispatch

- `ScmEventOrchestrator.DispatchCoreAsync` (`ScmEventOrchestrator.cs`):
  Aufgabenlease (`ScmTaskCoordinator`/`ScmEventStore.ClaimTaskAsync`), Receipt-Reaction
  und Waiting-Kommentar (Outbox, idempotent über `EnsureAsync` + Purpose „waiting"),
  Revalidierung (Owner, Trigger), `BlockReason(task)` — aktuell blockieren
  `Status Beendet/Archiviert` **und `PausiertBisUtc > now`** den Start
  („Aufgabe pausiert; Start gesperrt." → Status „Wartend"). Danach CLI-Start
  (`EntwicklungsprozessService.ProzessStartenUndCliStartenAsync` /
  `CliNeustartenAsync`), `WaitForInputReadyAsync`, Exchange-Versand
  (`ScmEventExchangeProtocol` → request.json/accepted/actions/complete im
  `.softwareschmiede-scm-exchange`-Verzeichnis).

## Aufgabenmodell und Status

- `Aufgabe` (`Domain/Entities/Aufgabe.cs`): `Status` (`AufgabeStatus`: Neu → Gestartet →
  Wartend/Beendet → Archiviert; Transitionen in `AufgabeService.ValidateStatusTransition`),
  `AusfuehrungsStatus`, `PausiertBisUtc`, `VorschlagAusfuehrenAbUtc`, `ScmAbschlussErkanntUtc`.
- `AufgabeService.SetPauseAsync` existiert bereits: setzt `PausiertBisUtc` (muss in der
  Zukunft liegen, Status Neu/Gestartet/Wartend erlaubt) + Protokolleintrag. Pause blockiert
  Starts, CLI-Neustarts, Recovery (`AufgabeRecoveryService`) und zeitgesteuerte Prompts
  — ein laufender Prozess wird nicht unterbrochen.
- `KiAusfuehrungsStatusKlassifikation` bildet `PausiertBisUtc > now` auf den UI-/Zähl-
  Zustand `KiAusfuehrungsZustand.Pausiert` ab (vor Laeuft/Wartet/PromptInWartestellung/
  Bereit). `ZeitgesteuerterPrompt` + `PromptZeitVersandService` existieren für
  terminierte Prompts laufender Sitzungen (HasScheduledPrompt → `PromptInWartestellung`).

## Shutdown-Funktion

- `AutoShutdownOrchestrator`: armiert bei aktiviertem Schalter einen Countdown
  (`AutoShutdownToleranzMinuten`), sobald `ILaufendeAutomatisierungenZaehlung.ZaehleAsync`
  0 liefert; danach `ISystemShutdownService.RequestShutdownAsync(erzwingen)`.
- `LaufendeAutomatisierungenZaehlung` zählt ausschließlich Aufgaben mit
  Klassifikation `Laeuft`; Kandidatenmenge = `GetAktiveAufgabenAsync` (Status
  Gestartet oder Wartend, max. 20). **`Pausiert` und `PromptInWartestellung` zählen
  nicht; Status `Neu` gehört gar nicht zur Kandidatenmenge.**
- Persistenz: `PausiertBisUtc` und `ScmIncomingEvent.NextAttemptUtc` sind DB-Felder
  (EF Core, SQLite) und überstehen App-Neustart und OS-Shutdown; nach dem Start wird
  der Poll fortgesetzt und fällige Ereignisse zugestellt.

## UI / Einstellungen

- Statusanzeige: `KiAusfuehrungsStatusConverter`/`AktiveAufgabePanelItem` (Kachel
  „Pausiert" mit Restzeit), `ScmEventQueueViewModel` (Ereignis-Inbox),
  `AufgabePausierenDialogViewModel` (manuelle Pause).
- Einstellungen: `AppEinstellungService` (Schlüssel `scm.trigger.keyword`,
  `scm.monitoring.enabled`, `scm.prompt.appendices`, Auto-Shutdown-Optionen);
  `SettingsViewModel` mit Validierung.

## Relevante Tests

- `ScmEventOrchestratorTests`, `ScmEventMonitoringServiceTests_*`
  (IntegrationTests), `AutoShutdownOrchestratorTests`,
  `LaufendeAutomatisierungenZaehlungTests`, `AufgabeServiceTests_Pause`,
  `E2E_ScmEvents`, `E2E_SessionLimitPause`, `E2E_AufgabePausieren`.
