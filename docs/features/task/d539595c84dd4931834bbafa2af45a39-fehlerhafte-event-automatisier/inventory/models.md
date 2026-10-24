# Datenmodellklassen

Betroffene Modelle/Records im Kontext der Anforderung ([`requirement.md`](../requirement.md)). Quellpfade relativ zum Repo-Root.

## `ScmIncomingEvent` — `src/Softwareschmiede/Domain/Entities/ScmIncomingEvent.cs`

Persistenter Ereignisdatensatz; `Status` und `Reason` sind die vom Orchestrator gesetzten und in der Queue sichtbaren Felder.

| Eigenschaft | Typ | Rolle im Kontext der Anforderung |
|---|---|---|
| `Sequence` | `long` | Ordnung der Ereignisse je Aufgabe |
| `Id` | `Guid` | Ereignis-Identität |
| `EventKey` | `string` | Dedup-/Korrelationsschlüssel |
| `AufgabeId` | `Guid` | Zugehörige Aufgabe |
| `ObservationJson` | `string` | Serialisierte `ScmEventObservation` (wird via `ScmEventStore.Observation(item)` gelesen) |
| `Status` | `string` (Default `"Aufgenommen"`) | `Wartend`/`Zugestellt`/`Verarbeitet`/… — der beanstandete „irreführende Grund" hängt an `Wartend`; Werte siehe [enums.md](enums.md) |
| `Reason` | `string?` | Grundtext, den `ScmEventQueueItem` 1:1 anzeigt und `CanConfirmReady` per `Contains` auswertet |
| `ReceivedUtc` | `long` | Eingangszeitpunkt |
| `Attempts` | `int` | Zustellversuche |
| `NextAttemptUtc` | `long?` | `long.MaxValue` = Fehlersperre (manueller Neuversuch nötig); `ConfirmReadyAsync` setzt es zurück (`ScmEventOrchestrator.cs:282`) |
| `ErrorCode` | `string?` | `ScmProviderFailureKind` bei Providerfehlern |
| `LeaseOwner` / `LeaseUntilUtc` | `string?` / `long?` | Aufgabenlease |
| `DeliveryAttemptId` | `Guid?` | Laufender Zustellversuch; `null` solange kein Prompt geschrieben wurde — Voraussetzung für `CanConfirmReady` |
| `CliRunId` | `string?` | Run-Identität der geprüften CLI-Sitzung (`adapter.CliRunId`, `ScmEventOrchestrator.cs:172`); Voraussetzung für `CanConfirmReady` |
| `ExchangePath` | `string?` | Kanalverzeichnis des Versuchs |
| `ExchangeRestrictedWriteSidsJson` | `string?` | Nachgewiesene eingeschränkte Schreib-SIDs |
| `DeliveredUtc` / `AcceptedUtc` | `long?` | Zustell-/Annahmezeitpunkte |
| `ReadyConfirmedRunId` | `string?` | Bestätigte Run-Id; wird in `ConfirmReadyAsync`/`ResendAsync` auf `null` gesetzt (`ScmEventOrchestrator.cs:283,299`) |

## `ScmExchangeAccess` — `src/Softwareschmiede/Application/Services/ScmCliAccessEvaluator.cs:21-26`

Record `ScmExchangeAccess(ScmExchangeAccessStatus Status, string Reason, string WorkingDirectory, Guid CliRunId, string ConfigurationFingerprint)` mit init-Property `IReadOnlyList<string> RestrictedWriteSids = []`.

- `Status`/`Reason` fließen aus `ScmCliAccessEvaluator.Evaluate` in den Adapter (`_access`, `ScmPromptSessionAdapter.cs:99`) und werden in `CheckExchangeAccessCoreAsync` per `with`-Ausdruck überschrieben — u. a. mit dem beanstandeten Text in Zeile 163.
- Der Adapter setzt `Reason`-Texte bereits frei (u. a. „CLI-Sitzung hat sich geändert oder wurde beendet.", „CLI-Konfiguration wurde seit dem Start geändert; regulärer Neustart erforderlich.", „Antwortkanal liegt außerhalb der erfassten CLI-Schreibwurzel.", Probetexte in Zeilen 199–201). Eine Erweiterung um einen konkreten Versionsgrund ist damit strukturell vorgesehen.

## `ScmCliConfigurationFile` — `src/Softwareschmiede/Application/Services/ScmCliAccessEvaluator.cs:29`

Record `ScmCliConfigurationFile(string Path, string? Content)`; `null`-Content markiert eine beim Start nicht vorhandene Datei. Unverändertheit wird per `IsUnchanged` (`ScmCliAccessEvaluator.cs:75-79`) bytegenau geprüft.

## `ScmExchangeAccessProbe` — `src/Softwareschmiede/Application/Services/ScmEventExchangeProtocol.cs:65-73`

Record `ScmExchangeAccessProbe(Guid CliRunId, string Nonce, string ConfigurationFingerprint, string DirectoryPath, IReadOnlyList<string> RestrictedWriteSids)` mit abgeleiteten Pfaden `ChallengePath`, `ReadChallengePath`, `ProofPath`. Die nie gesendete Probe ist das Symptom der Anforderung: bei `!_recognizedCli` erreicht `CheckExchangeAccessCoreAsync` den `Supported`-Zweig nie (`ScmPromptSessionAdapter.cs:162-163` greift vorher).

## `ScmEventQueueItem` — `src/Softwareschmiede.App/ViewModels/ScmEventQueueViewModel.cs:320-349`

Unveränderlicher Anzeigesnapshot (Record) mit den Feldern `Id, TaskId, TaskTitle, Repository, Reference, Kind, Received, Status, StatusText, Body, Origin, Reason, CliRunId, AttemptId, IsBlocked`.

| Abgeleitete Eigenschaft | Zeile | Inhalt |
|---|---|---|
| `IsProcessed` | 325 | `Status == "Verarbeitet"` |
| `IsFinal` | 327 | `Verarbeitet` oder `Abgebrochen` |
| `CanCancel` | 329 | `!IsFinal` |
| `CanResend` | 331 | `AttemptId != null && Status in {ZustellungUngewiss, VerarbeitungUngeklärt}` |
| **`CanConfirmReady`** | **333** | `!IsBlocked && AttemptId is null && CliRunId is not null && Reason?.Contains("Eingabebereitschaft unbekannt", StringComparison.Ordinal) == true` — **per Textabgleich an den bisherigen Pauschalgrund gekoppelt** |
| `From(ScmIncomingEvent, Aufgabe?)` | 335-348 | Projektion; blockierte Aufgaben überschreiben den Reason-Anzeigetext |

## `ScmKiProfile` — `src/Softwareschmiede.Tests/Helpers/ScmDispatchFixture.cs:20-32` (gelinkt nach `Softwareschmiede.IntegrationTests`)

Testseitiges Terminal-Profil `ScmKiProfile(string Prefix, string Arguments, string Banner, string Prompt, IReadOnlyDictionary<string,string?> EnvironmentVariables, Func<string,Task>? OnWritePrompt)`.

| Statisches Profil | Zeile | Inhalt |
|---|---|---|
| `Simulator` | 24-26 | Prefix `Softwareschmiede.KiSimulator`, Banner `SCM_SIMULATOR_PROTOCOL_V1`, Prompt `SCM_READY_V1`, Env `SOFTWARESCHMIEDE_SCM_SIMULATOR=1` |
| `Devin(arguments, onWritePrompt)` | 29-31 | Prefix `Softwareschmiede.Devin`, Default-Argumente `--permission-mode accept-edits --respect-workspace-trust false`, **Banner `Devin CLI\nv3000.11.92` (gepinnt)**, Prompt `❭` — der einzige Parameter für abweichende Versionen ist aktuell `arguments`, nicht das Banner |

## Verwandte Value Objects (Kontext)

- `TerminalSessionStartSpec` (`src/Softwareschmiede/Domain/ValueObjects`): Startvertrag mit `FileName`, `Arguments`, `WorkingDirectory`, `EnvironmentVariables`; Eingang für `ScmCliAccessEvaluator.Evaluate`.
- `ScmEventObservation`, `ScmEventReference`, `ScmEventRepository`, `ScmContentKind`, `ScmReferenceKind`: Inhalts-/Ursprungsdaten, für die Versionserkennung irrelevant.
