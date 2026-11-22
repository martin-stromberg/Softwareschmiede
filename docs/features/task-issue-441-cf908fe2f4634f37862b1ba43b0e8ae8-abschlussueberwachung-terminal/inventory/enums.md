# Enums und Statuswertemengen — Bestandsaufnahme

## `ScmTaskCompletionCheckMode`
Datei: `src/Softwareschmiede/Domain/Enums/ScmTaskCompletionCheckMode.cs`
Persistiert als Zahl unter `scm.taskcompletion.scope` (`AppSettingService.ScmTaskCompletionCheckModeKey`).

| Wert | Bedeutung |
|---|---|
| `EventDriven = 0` | Standard: nur Aufgaben mit mindestens einem `ScmIncomingEvents`-Eintrag werden beobachtet. |
| `AllWorkItems = 1` | Alle nicht beendeten/archivierten Aufgaben mit SCM-Referenz werden beobachtet. |

## `ScmIncomingEvent.Status` — String-Wertemenge (kein Enum)
`Status` ist ein freies `string`-Feld (`ScmIncomingEvent.cs` Zeile 22, Default `"Aufgenommen"`). Alle Vergleiche erfolgen als Literal-Strings; es existiert keine zentrale Konstanten- oder Klassifikationsstelle.

| Wert | Bedeutung | Gesetzt in | Gelesen/ausgewertet in |
|---|---|---|---|
| `Aufgenommen` | Neu registriert, noch nicht zugestellt (Default). | `ScmIncomingEvent.cs` Z. 22 (Default); `ScmEventOrchestrator.cs` Z. 407 (`ResendAsync`-Reset). | Implizit überall, wo „nicht `Verarbeitet`/`Abgebrochen`" als offen gilt. |
| `Verzögert` | Zustellung ruht bis `NextAttemptUtc` (Direktive `#verzögert`/`ScmDelayDirective`); Aufgabe ggf. pausiert. | `ScmEventStore.cs` Z. 101. | `ScmDispatchPriority.cs` Z. 43 (Prioritätsklasse 2); `RunningAutomationsCount.cs` Z. 60; `ScmEventOrchestrator.cs` Z. 435, 439 (Pause-Rückstufung bei Abbruch). |
| `Wartend` | Wartet auf Bedingung/Korrektur; `Reason` erklärt den Grund. UI-Filter „Wartend" = alle nicht-finalen Stati. | `ScmEventOrchestrator.cs` Z. 1004 (`WaitAsync`, u. a. via `WaitWithCommentAsync` Z. 1009). | `ScmEventQueueViewModel.cs` Z. 95, 129, 175 (Filter/EmptyText). |
| `ZustellungVorbereitet` | Versuchskanal angelegt (`DeliveryAttemptId`/`ExchangePath` gesetzt), Prompt noch nicht geschrieben. | `ScmEventOrchestrator.cs` Z. 306, 760. | `ScmEventOrchestrator.cs` Z. 582, 953 (→ `ZustellungUngewiss` beim nächsten Abgleich); `ScmEventQueueViewModel.cs` Z. 409 (Anzeigetext). |
| `Zugestellt` | Prompt erfolgreich in die CLI geschrieben; Annahme steht aus. | `ScmEventOrchestrator.cs` Z. 321, 775. | `ScmEventOrchestrator.cs` Z. 587, 958 (Timeout/Sitzungsende → `VerarbeitungUngeklärt`). |
| `ZustellungUngewiss` | Schreibversuch begonnen, Erfolg nicht bestätigt; kein automatischer Neuversand. | `ScmEventOrchestrator.cs` Z. 326, 584, 780, 955. | `ScmEventOrchestrator.cs` Z. 405 (`ResendAsync`-Zulassung); `ScmEventQueueViewModel.cs` Z. 393 (`CanResend`), 409. |
| `InBearbeitung` | Korrelierte Annahme (`accepted.json`) eingetroffen; Abschluss steht aus. | `ScmEventOrchestrator.cs` Z. 505, 848. | `ScmEventOrchestrator.cs` Z. 530, 587, 909, 958; `ScmEventQueueViewModel.cs` Z. 409. |
| `VerarbeitungUngeklärt` | Terminaler Diagnose-Status ohne automatischen Neuversand (Kollision, unlesbarer Kanal, Timeout, fehlgeschlagene Aktion). | `ScmEventOrchestrator.cs` Z. 167, 173, 499, 516, 526, 594, 627, 633, 841, 877, 888, 900, 940, 947, 965, 1030. | `ScmEventOrchestrator.cs` Z. 405 (`ResendAsync`-Zulassung); `ScmEventQueueViewModel.cs` Z. 393, 409. Rückweg nur manuell: `ResendAsync` → `Aufgenommen`, `CancelAsync` → `Abgebrochen`; `CheckStatusCommand` → erneuter Abgleich. |
| `Verarbeitet` | Final: korrelierte Annahme + bestätigter Abschluss (bzw. Issue bereits geschlossen). | `ScmEventOrchestrator.cs` Z. 537, 666, 915, 932. | „Final"-Leser überall: `ScmEventOrchestrator.cs` Z. 44, 93, 161, 182, 381, 427, 621, 996 (invers); `ScmTaskCompletionMonitoringService.cs` Z. 85, 134; `ScmEventExchangeService.cs` Z. 27; `MainWindowViewModel.cs` Z. 393; `ScmEventQueueViewModel.cs` Z. 175–176, 384, 386, 389. |
| `Abgebrochen` | Final: vom Anwender abgebrochen; keine weiteren Versuche/Remote-Aktionen. | `ScmEventOrchestrator.cs` Z. 429 (`CancelAsync` via `ExecuteUpdate`). | `ScmEventOrchestrator.cs` Z. 43, 93, 132, 182, 368, 381, 427, 996; `ScmTaskCompletionMonitoringService.cs` Z. 85, 134; `ScmEventExchangeService.cs` Z. 27; `MainWindowViewModel.cs` Z. 393; `ScmEventQueueViewModel.cs` Z. 177, 386. |

**Blockierende Wirkung auf die Abschlussprüfung (Ist-Zustand):** `CheckTaskAsync` (Z. 84–86) und die Wiederholung in der `RunAsync`-Sperre (Z. 133–135) lassen nur `Verarbeitet`/`Abgebrochen` passieren — alle übrigen acht Werte (`Aufgenommen`, `Verzögert`, `Wartend`, `ZustellungVorbereitet`, `Zugestellt`, `ZustellungUngewiss`, `InBearbeitung`, `VerarbeitungUngeklärt`) blockieren.

**Reihenfolge-Regel im Dispatch:** `DispatchCoreAsync` Z. 182 blockiert ein Ereignis, solange ein früheres Ereignis derselben Aufgabe (kleinere `Sequence`) nicht `Verarbeitet`/`Abgebrochen` ist — ein `VerarbeitungUngeklärt`-Ereignis blockiert also auch Folgeereignisse.

## `ScmOutgoingAction.Status` — String-Wertemenge
Default `"Pending"` (`ScmOutgoingAction.cs` Z. 22); `"Sending"`/`"Succeeded"`/`"Uncertain"` in `ScmEventOutbox.cs` Z. 58–72. Gelesen u. a. in `ScmEventOrchestrator` (Offene-Aktions-Flush, Vollständigkeitsprüfung), `ScmEventQueueViewModel` (Diagnose/`HasOpenActions`), `DispatchDueEventsAsync` (Auswahl offener Aktionen).

## `WorkItemStatus`
Datei: `src/Softwareschmiede/Domain/Enums/WorkItemStatus.cs`

| Wert | Bedeutung |
|---|---|
| `New` / `Started` / `Waiting` | Aktive Aufgabe; `New` wird beim Dispatch auf `Started` gesetzt. |
| `Completed` / `Archived` | Endstatus; Aufgabe wird weder beobachtet noch dispatcht; `Completed` ist Ziel des Auto-Abschlusses. |

## `PullRequestStatus`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Enums/PullRequestStatus.cs`

| Wert | Bedeutung |
|---|---|
| `Unknown`/`Open`/`Closed`/`Merged` | Remote-Status des PR; `Closed`/`Merged` zählen als geschlossen, `Merged` ist final und wird nicht erneut geprüft. |

## `PullRequestReferenceRole`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Enums/PullRequestReferenceRole.cs`

| Wert | Bedeutung |
|---|---|
| `CreatedByTask = 0` | Von der Aufgabe/dem Agenten erstellter PR — Rolle aller via `SaveReportedAsync` registrierten Referenzen. |
| `ReviewSource = 1` | PR als Review-Quelle. |

## `PullRequestMonitoringPhase`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Enums/PullRequestMonitoringPhase.cs`
`Created`, `PreMergeRunning`, `PreMergeSucceeded`, `Completing`, `Completed`, `Approved`, `PostMergeUncertain`, `PostMergeRunning`, `PostMergeSucceeded`, `PostMergeFailed`, `Blocked`, `Failed`, `NotMonitored`. Initialphase via `PullRequestMonitoringPolicy.GetInitialPhase` (`src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/PullRequestMonitoringPolicy.cs`).

## `PullRequestProvider` / `PullRequestMergeStatus` / `PullRequestProviderDescriptor`
Dateien: `src/Softwareschmiede.Plugin.Contracts/Domain/Enums/PullRequestProvider.cs` (`GitHub`, `BitbucketCloud`, `BitbucketServerDataCenter`), `PullRequestMergeStatus.cs` (`Unknown`, `Mergeable`, `Blocked`, `Conflicting`, `Merged`), `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/PullRequestProviderDescriptor.cs` (Prefix-Mapping `Softwareschmiede.GitHub`/`Softwareschmiede.Bitbucket`, `TryGetProviderForPluginPrefix` mit Fallback `GitHub` im Meldungspfad).
