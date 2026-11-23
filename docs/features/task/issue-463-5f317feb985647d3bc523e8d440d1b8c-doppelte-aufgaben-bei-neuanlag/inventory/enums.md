# Enums und Statuswerte — Bestandsaufnahme

Die Anforderung verwendet „Wartend" mehrfach auf unterschiedlichen Ebenen — hier die verbindliche Namensklärung.

## `ScmIncomingEventStatus` (statische Klasse mit `const string`)
Datei: `src/Softwareschmiede/Domain/Entities/ScmIncomingEventStatus.cs`

Ereignisstatus des `ScmIncomingEvent` (persistiert als `string`).

| Wert | Bedeutung |
|------|-----------|
| `Aufgenommen` | Neu registriert, noch nicht zugestellt (Default). |
| `Verzögert` | Zustellung ruht persistiert bis `NextAttemptUtc` (Verzögerungsdirektive). |
| `Wartend` | Wartet auf eine Bedingung oder Korrektur; `Reason` erklärt den Grund. **Blockierend** — auch mit `NextAttemptUtc = long.MaxValue`. Zielzustand des Frist-Abbruchs laut Anforderung. |
| `ZustellungVorbereitet` | Versuchskanal angelegt, Prompt noch nicht geschrieben. |
| `Zugestellt` | Prompt geschrieben; Annahme steht aus. |
| `ZustellungUngewiss` | Schreibversuch begonnen, Erfolg nicht bestätigt; kein automatischer Neuversand. NonBlocking. |
| `InBearbeitung` | Korrelierte Annahme eingetroffen; Abschluss steht aus. |
| `VerarbeitungUngeklärt` | Terminaler Klärungsstatus, nur manuell. NonBlocking. |
| `Verarbeitet` | Final: Annahme + bestätigter Abschluss. NonBlocking. |
| `Abgebrochen` | Final: Benutzerabbruch. NonBlocking. |
| `NonBlocking` | `IReadOnlyCollection<string>` = `[Verarbeitet, Abgebrochen, VerarbeitungUngeklärt, ZustellungUngewiss]` — gibt Vorgängerprüfung und Abschlussprüfung frei. |

## `ScmPromptReadyStatus`
Datei: `src/Softwareschmiede/Application/Services/ScmPromptSessionAdapter.cs` (Zeilen 9–21)

Nachgewiesener Eingabestatus der laufenden CLI-Sitzung — technische Basis des „wartende CLI"-Begriffs.

| Wert | Bedeutung |
|------|-----------|
| `Unknown` | Kein verifizierter Eingabeprompt. |
| `Ready` | Bestätigter KI-Eingabeprompt für den aktuellen Lauf. |
| `Busy` | Ein Turn oder eine Benutzereingabe läuft (`IsBusy`). |
| `TrustPrompt` | CLI wartet auf Vertrauensentscheidung fürs Arbeitsverzeichnis. |
| `Exited` | Gebundener Lauf beendet oder fehlgeschlagen. |

## `WorkItemStatus`
Datei: `src/Softwareschmiede/Domain/Enums/WorkItemStatus.cs`

Aufgaben-Lebenszyklus (persistiert lokalisiert: Neu/Gestartet/Wartend/Beendet/Archiviert).

| Wert | Bedeutung |
|------|-----------|
| `New` | Erstellt, wartet auf Bearbeitung. |
| `Started` | Gestartet (CLI läuft/sollte laufen). |
| `Waiting` | CLI hat Rate-Limit erreicht; wartet auf Wiederaufnahme — **nicht** identisch mit Eingabebereitschaft. |
| `Completed` / `Archived` | Beendet / archiviert; `BlockReason` des Orchestrierers. |

## `WorkItemRunStatus`
Datei: `src/Softwareschmiede/Domain/Enums/WorkItemRunStatus.cs`

Laufzeit-Substatus während `ActiveRunId` (persistiert u. a. als „WartetAufEingabe"), aus Terminal-I/O abgeleitet (`CliRuntimeStatusEvaluator` via `CliProcessManager`).

| Wert | Bedeutung |
|------|-----------|
| `Running` | CLI hat kürzlich Ausgabe/Eingabe verarbeitet. |
| `WaitingForInput` | CLI erzeugt länger keine Ausgabe, wartet vermutlich auf Eingabe. |

## `WorkItemExecutionStatus`
Datei: `src/Softwareschmiede/Domain/Enums/WorkItemExecutionStatus.cs`

| Wert | Bedeutung |
|------|-----------|
| `NotStarted` | Ausführung noch nicht gestartet. |
| `Active` | Aktiv bzw. Recovery-Kandidat nach Neustart. |
| `Completed` | Beendet; Kriterium der Klasse-1-Fortsetzung in `ScmDispatchPriority` (zusammen mit `New`/`Started`/`Waiting`). |

## `WorkItemKind`
Datei: `src/Softwareschmiede/Domain/Enums/WorkItemKind.cs`

Auftragsart/Anzeige-Klassifikation.

| Wert | Bedeutung |
|------|-----------|
| `Unspecified = 0` | Bestandsdaten ohne klassifizierbaren Ursprung. |
| `Manual = 1` | Manuell angelegt. |
| `Instruction = 2` | SCM-Trigger-Anweisungsaufgabe; Zieltyp der Aufbereitung nach Zustellung. |
| `PullRequest = 3` | Aus PR-Ereignis angelegt. |
| `Alert = 4` | Aus SCM-Alert angelegt. |
| `IssuePreparation = 5` | Aufbereitungs-Aufgabe bei Anlage mit `Purpose`; wechselt zu `Instruction` nach erfolgreicher Analyse-Zustellung (`ScmEventOrchestrator` ~Zeile 804). |

## `AiExecutionState`
Datei: `src/Softwareschmiede/Domain/Enums/AiExecutionState.cs`

Fachliche Klassifikation via `AiExecutionStatusClassification.Classify`.

| Wert | Bedeutung |
|------|-----------|
| `Paused` | `PausedUntilUtc` in Zukunft. |
| `PromptQueued` | Zeitgesteuerter Prompt in Warteschlange. |
| `Running` | Aktiver Lauf ohne WartetAufEingabe-Substatus. |
| `Waiting` | `WartetAufEingabe`-Substatus oder Aufgabenstatus `Wartend`. |
| `Ready` | Keine aktive Ausführung erkennbar. |

## `ScmTaskResolutionKind`
Datei: `src/Softwareschmiede/Application/Services/ScmEventTaskResolver.cs` (Zeilen 13–21)

| Wert | Bedeutung |
|------|-----------|
| `Assigned` | Genau eine Aufgabe gefunden oder angelegt. |
| `Ambiguous` | Mehrere Aufgaben tragen dieselbe Referenz. |
| `Unassigned` | Keine Aufgabe zuordenbar und keine anlegbar. |

## `ScmReferenceKind` / `ScmContentKind`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmEvents.cs`

| Enum | Werte | Bedeutung |
|------|-------|-----------|
| `ScmReferenceKind` | `Issue`, `PullRequest` | Referenzart; das Szenario betrifft `Issue`. |
| `ScmContentKind` | `Description`, `IssueComment`, `ReviewComment`, `PullRequestReview` | Quell-Arten; das Szenario paart `Description` (Aufbereitung) mit `IssueComment` (Trigger). |

## `ScmCliRecognitionState` / `ScmExchangeAccessStatus`
Dateien: `src/Softwareschmiede/Application/Services/ScmCliPromptProfile.cs`, `src/Softwareschmiede/Application/Services/ScmCliAccessEvaluator.cs`

| Enum | Werte | Bedeutung |
|------|-------|-----------|
| `ScmCliRecognitionState` | `Unknown`, `Unsupported`, `Supported` | Erkannte CLI-Identität/Version; Wartegrund-Verzweigung im Dispatch. |
| `ScmExchangeAccessStatus` | `Supported`, `Denied`, `Unknown` | Kanalzugriff; Voraussetzung jeder Prompt-Zustellung. |

## `ScmProviderFailureKind`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmEvents.cs`

`Authentication`, `Permission`, `RateLimit`, `Temporary`, `Permanent` — steuert `NextAttemptUtc` (`long.MaxValue` bei Auth/Permission) und Retry-Backoff.
