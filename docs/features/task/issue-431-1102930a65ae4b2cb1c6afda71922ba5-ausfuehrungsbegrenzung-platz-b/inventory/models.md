# Datenmodell

Nur die für die Anforderung relevanten Eigenschaften sind aufgeführt.

## `WorkItem`

Datei: `src/Softwareschmiede/Domain/Entities/WorkItem.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `Guid` | Primärschlüssel; Join-Key zu `ScmIncomingEvent.WorkItemId` und zu den Prozess-Handles (`AiExecutionService._handles`). |
| `Status` | `WorkItemStatus` | Lebenszyklus (`New`/`Started`/`Waiting`/`Completed`/`Archived`); `Completed`/`Archived` sperren Starts (`ScmEventOrchestrator.BlockReason`). |
| `ExecutionStatus` | `WorkItemExecutionStatus` | Lauf-Lebenszyklus; `Completed` auf `New`/`Started`/`Waiting` markiert Fortsetzungsereignisse in `ScmDispatchPriority`. |
| `AiPluginPrefix` | `string?` | Gespeichertes KI-Plugin; Fallback der Plugin-Attribution in `GetOccupancyAsync`, wenn keine lebende SCM-Sitzung existiert. |
| `ActiveRunId` | `string?` | ID des aktiven Laufs; gesetzt solange ein CLI-Prozess läuft. |
| `RunStatus` | `WorkItemRunStatus?` | Persistierter Laufzeit-Substatus (`Running`/`WaitingForInput`), aus `CliRuntimeStatus` abgeleitet; laut Anforderung **kein** Freigabekriterium. |
| `PausedUntilUtc` | `DateTimeOffset?` | Bestehendes Freigabekriterium: `IsCurrentlyPaused` (`> now`, strikt) filtert die Aufgabe aus beiden Zählern. |
| `ScmCompletionDetectedUtc` | `DateTimeOffset?` | Abschlussbeobachtung (`ScmTaskCompletionMonitoringService`); für die Freigabe nicht relevant. |
| `Purpose` | `string?` | Zweck-Markierung (z. B. `"IssueAufbereitung"`); Aufbereitungsaufträge zählen regulär ins Limit. |
| `GitRepositoryId` | `Guid?` | Repository-Bezug; Dispatch lädt die Aufgabe inkl. `GitRepository`. |

## `ScmIncomingEvent`

Datei: `src/Softwareschmiede/Domain/Entities/ScmIncomingEvent.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Sequence` | `long` | Persistente Eingangsreihenfolge; ältere unverarbeitete Ereignisse blockieren spätere (`ScmEventOrchestrator` Z. 182). |
| `Id` | `Guid` | Ereignis-ID (Default `Guid.NewGuid()`). |
| `WorkItemId` | `Guid` | Zugeordnete Aufgabe — Schlüssel des Pending-Guards. |
| `Status` | `string` | Freier Statusstring, Default `"Aufgenommen"`; **Endstatus:** `"Verarbeitet"`, `"Abgebrochen"`. Werte siehe [enums.md](enums.md). |
| `Reason` | `string?` | Benutzerlesbarer Wartegrund (z. B. Limit-Text aus `GetStartBlockReason`). |
| `NextAttemptUtc` | `long?` | Frühester nächster Versuch (Unix-ms); gesetzt bei `Verzögert` und hartem Stopp (`long.MaxValue`). |
| `DeliveryAttemptId` | `Guid?` | Kennzeichnet einen begonnenen Zustellversuch; Reconciliation läuft am Limit vorbei. |
| `CliRunId` | `string?` | Gebundener CLI-Lauf der Zustellung. |
| `Attempts` | `int` | Zustellversuchszähler. |
| `Purpose` | `string?` | Interne Auftragsart (z. B. `ScmIssuePreparationService.Purpose` = Issue-Aufbereitung); eigener Ausführungspfad. |
| `LeaseOwner` / `LeaseUntilUtc` | `string?` / `long?` | (Im Code ungenutzte Felder — Aufgabenleases liegen in `ScmTaskLeases` über `ScmEventStore.ClaimTaskAsync`.) |

Bemerkung: `ScmIncomingEvent.Status` ist bewusst ein `string`, kein Enum — neue Pending-Prüfungen
müssen denselben Literalvergleich verwenden wie die Bestandsmuster.

## `ScmStartSlotReservationPool.StartSlotReservation` (Laufzeitmodell, nicht persistiert)

Datei: `src/Softwareschmiede/Application/Services/ScmStartSlotReservationPool.cs` (Z. 54–65)

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `Guid` | Reservierungs-ID. |
| `PluginPrefix` | `string` | Prefix des reservierten Starts; addiert sich auf `occupancy.ProPlugin`. |
| `ValidUntilUtcMs` | `long` | Verfallzeitpunkt; Lebensdauer `ScmEventExecutionLimitService.ReservationLifetime` = 5 min. |
| `Dispose()` | — | Gibt den Platz non-blocking frei. |

## `CliExecutionOccupancy` (Ergebnis-Record der Belegungszählung)

Datei: `src/Softwareschmiede/Application/Services/ScmEventExecutionLimitService.cs` (Z. 12–19)

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Global` | `int` | Anzahl aller Aufgaben mit laufender, nicht pausierter CLI-Ausführung inkl. Handles ohne Aufgabenzeile. |
| `ProPlugin` | `IReadOnlyDictionary<string, int>` | Laufende, nicht pausierte Ausführungen je KI-Plugin-Prefix (OrdinalIgnoreCase). |
| `ForPlugin(prefix)` | Methode | Liefert die Plugin-Zählung; `0` bei unbekanntem/leerem Prefix. |

## `CliStartSlotAdmission` (Ergebnis-Record der Zulassungsprüfung)

Datei: `src/Softwareschmiede/Application/Services/ScmEventExecutionLimitService.cs` (Z. 27)

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `WaitReason` | `string?` | Benutzerlesbarer Wartegrund bei vollem Limit; `null` = Platz frei. |
| `Reservation` | `IDisposable?` | Belegter Startplatz bis zur Handle-Registrierung; `null` bei vollem Limit oder unbegrenzter Konfiguration. |
