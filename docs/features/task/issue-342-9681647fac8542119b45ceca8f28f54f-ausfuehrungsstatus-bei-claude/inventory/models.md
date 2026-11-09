# Datenmodell — Bestandsaufnahme

## `Aufgabe`

Datei: `src/Softwareschmiede/Domain/Entities/Aufgabe.cs`

Persistierte Entität (EF Core, SQLite; Enum-Spalten als `string`, `DateTimeOffset` als Unix-Millis — `SoftwareschmiededDbContext.OnModelCreating`, Z. 184–233). Für die Anforderung relevante Laufdaten-Felder:

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `Guid` | Primärschlüssel; zugleich Schlüssel der In-Memory-Registries (`KiAusfuehrungsService._handles`, Heartbeat-Timer) |
| `Status` | `AufgabeStatus` | Lebenszyklusstatus (Neu/Gestartet/Wartend/Beendet/Archiviert); steuert `SollCliAnzeigen` und `IstAktivOderWartend` |
| `AusfuehrungsStatus` | `AufgabeAusfuehrungsStatus` | Persistierter KI-Ausführungsstatus (NichtGestartet/Aktiv/Beendet); Vorbedingung der Kachel-Klassifikation |
| `AktiveRunId` | `string?` | ID des aktiven Laufs; wird **nur** über `AufgabeService.AktivenLaufSetzenAsync` gesetzt und über `AktivenLaufBeendenAsync`/`StartenAsync`/`AbschliessenAsync`/`StartZuruecksetzenAsync` entfernt |
| `LastHeartbeatUtc` | `DateTimeOffset?` | Letzter Heartbeat des Laufs; Lebendigkeitskriterium in `AufgabeLaufAktivitaet.IstAktiv` (Timeout 5 min) |
| `LetzterCliStartUtc` | `DateTimeOffset?` | Zeitstempel des letzten echten CLI-Prozessstarts; Sortierkriterium der aktiven Aufgabenliste |
| `LaufStatus` | `AufgabeLaufStatus?` | Laufzeit-Substatus (Laeuft/WartetAufEingabe), nur relevant bei gesetzter `AktiveRunId`; steuert „▶ Läuft" vs. „⏸ Wartet" |
| `PausiertBisUtc` | `DateTimeOffset?` | Pausen-Endzeitpunkt; überlagert in der Kachel-Klassifikation alle anderen Zustände |
| `RecoveryVersion` | `int` | Concurrency-Token für Recovery-Statusänderungen (`AufgabeRecoveryService`) |
| `KiPluginPrefix` | `string?` | Gewähltes KI-Plugin |
| `LokalerKlonPfad` | `string?` | Steuert den Startpfad: gesetzt → `CliNeustartenAsync`-Pfad statt Vollstart |
| `GitArbeitsbereich` | `GitArbeitsbereich?` ([NotMapped]) | Convenience-Bündel aus `BranchName`, `LokalerKlonPfad`, `BasisBranchName` |

Wird gelesen von: `AufgabeService.GetAktiveAufgabenAsync` (Kachel-Datenquelle), `KiAusfuehrungsStatusKlassifikation.Klassifiziere`, `TaskDetailViewModel.LadenAsync`, `AufgabeRecoveryService.ScanForRecoveryCandidatesAsync`.

## `AktiveAufgabePanelItem`

Datei: `src/Softwareschmiede.App/ViewModels/AktiveAufgabePanelItem.cs`

UI-Snapshot einer Kachel der aktiven Aufgabenliste (`init`-Properties; wird bei jedem Refresh über `AktiveAufgabenListe.ReplaceAll` komplett neu erzeugt — keine INotifyPropertyChanged-Aktualisierung der Laufdaten-Felder).

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `Guid` | Aufgaben-ID |
| `Titel` | `string` | Aufgabentitel |
| `Status` | `AufgabeStatus` | Kopierte Aufgabe-`Status` |
| `AusfuehrungsStatus` | `AufgabeAusfuehrungsStatus` | Kopierte Aufgabe-`AusfuehrungsStatus` |
| `AktiveRunId` | `string?` | Kopierte Aufgabe-`AktiveRunId` |
| `LastHeartbeatUtc` | `DateTimeOffset?` | Kopierte Aufgabe-`LastHeartbeatUtc` |
| `LaufStatus` | `AufgabeLaufStatus?` | Kopierte Aufgabe-`LaufStatus` |
| `LetzterCliStartUtc` | `DateTimeOffset?` | Kopierte Aufgabe-`LetzterCliStartUtc` |
| `PausiertBisUtc` | `DateTimeOffset?` | Kopierte Aufgabe-`PausiertBisUtc` |
| `IstPausiert` | `bool` | Abgeleitet: `PausiertBisUtc > DateTimeOffset.UtcNow` (Kachel-Abblendung) |
| `HasScheduledPrompt` | `bool` | Aus `PromptZeitVersandService.GetScheduledPromptStatus` (nicht persistiert) |
| `IsAktiv` | `bool` | Ob diese Aufgabe aktuell in der Detailansicht gezeigt wird (einziges setbares Property) |
| `SortierZeitpunkt` | `DateTimeOffset` | `LetzterCliStartUtc ?? ErstellungsDatum` |

Wird befüllt in `MainWindowViewModel.MapAktiveAufgabePanelItem` (Z. 366–410); gelesen von `KiAusfuehrungsStatusKlassifikation.Klassifiziere` via `ActiveTasksListControl.xaml`-Bindings.

## `CliProcessHandle`

Datei: `src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs` (Z. 784–824)

In-Memory-Laufhandle, verwaltet in `KiAusfuehrungsService._handles` (`ConcurrentDictionary<Guid, CliProcessHandle>`).

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `CliRunId` | `Guid` | Identität des konkreten Prozesslaufs (≠ persistierter `Aufgabe.AktiveRunId`!) |
| `AufgabeId` | `Guid` | Zugehörige Aufgabe |
| `Process` | `Process` | Verwalteter CLI-Prozess; `HasExited` ist Basis von `IsRunning` |
| `Session` | `ITerminalSession?` | Interaktive Terminal-Session (`ScmCoordinatedTerminalSession`-Wrapper), `null` bei klassischem Pipe-Start |
| `ScmPromptSession` | `ScmPromptSessionAdapter?` | SCM-Antwortkanal des Starts |
| `OutputSink` | `ITerminalOutputSink?` | Protokoll-/Recorder-Senke |
| `LastHeartbeat` | `DateTimeOffset` | In-Memory-Heartbeat (unabhängig vom persistierten `Aufgabe.LastHeartbeatUtc`) |
| `AbsichtlichGestoppt` | `bool` | Markierung durch `StopCliAsync` → Mapping auf `CliProcessStatus.Gestoppt` statt `Fehler` |

## `TerminalSessionFailedEventArgs` / `CliRuntimeStatusChangedEventArgs` / `TerminalSessionExitedEventArgs`

Datei: `src/Softwareschmiede/Infrastructure/Terminal/` (`PseudoConsoleSession.cs` Z. 660–672 für `CliRuntimeStatusChangedEventArgs`; `Failed`/`Exited`-Args deklariert im gleichen Namespace)

- `CliRuntimeStatusChangedEventArgs.Status` — neuer `CliRuntimeStatus`; Nutzlast des `RuntimeStatusChanged`-Events.
- `TerminalSessionFailedEventArgs` — trägt `Error` + `Phase` (z. B. `"ReadLoop"`, `"Write"`); über `ITerminalSession.Failure` auch nachträglich abrufbar.
- `TerminalSessionExitedEventArgs` — trägt den `ExitCode` (nullable).
