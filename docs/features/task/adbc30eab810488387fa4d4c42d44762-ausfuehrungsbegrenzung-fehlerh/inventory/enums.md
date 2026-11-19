# Enums — Bestandsaufnahme

## `KiAusfuehrungsZustand`
Datei: `src/Softwareschmiede/Domain/Enums/KiAusfuehrungsZustand.cs`

Fachliche Zustandsklassifikation der KI-Ausführung (gemeinsame Basis der Status-Converter und der Lauf-Zählung).

| Wert | Bedeutung |
|------|-----------|
| `Pausiert` | Aufgabe ist pausiert (`PausiertBisUtc` in der Zukunft) — **Referenzdefinition des Pausenkriteriums** |
| `PromptInWartestellung` | Ein zeitgesteuerter Prompt steht in der Warteschlange |
| `Laeuft` | CLI-Ausführung läuft (aktiver Run ohne `WartetAufEingabe`-Substatus) |
| `Wartet` | Ausführung wartet (Substatus `WartetAufEingabe` oder `AufgabeStatus.Wartend`) |
| `Bereit` | Keine aktive Ausführung erkennbar |

## `AufgabeStatus`
Datei: `src/Softwareschmiede/Domain/Enums/AufgabeStatus.cs`

| Wert | Bedeutung |
|------|-----------|
| `Neu` | Erstellt, wartet auf Bearbeitung; pausierbar (`SetPauseAsync`, `PauseForDelayAsync`) |
| `Gestartet` | Gestartet (Branch erstellt, CLI läuft oder sollte laufen); pausierbar |
| `Wartend` | CLI hat Rate-Limit erreicht; wartet auf Wiederaufnahme; pausierbar |
| `Beendet` | Beendet; `BlockReason`-Sperre, nicht pausierbar |
| `Archiviert` | Archiviert; `BlockReason`-Sperre, nicht pausierbar |

Erweiterung `AufgabeStatusExtensions` (`src/Softwareschmiede/Domain/Enums/AufgabeStatusExtensions.cs`): `AktivOderWartendStatus = [Gestartet, Wartend]` — Kandidatenmenge der Session-Limit-Pause (`KiPluginLimitService`) und des Recovery-Scans.

## `AufgabeAusfuehrungsStatus`
Datei: `src/Softwareschmiede/Domain/Enums/AufgabeAusfuehrungsStatus.cs`

| Wert | Bedeutung |
|------|-----------|
| `NichtGestartet` | KI-Ausführung noch nicht gestartet |
| `Aktiv` | Ausführung aktiv oder nach App-Neustart wiederherzustellen; Kriterium der Session-Limit-Pause |
| `Beendet` | Ausführung beendet; mit Status `Neu`/`Gestartet`/`Wartend` Fortsetzungskriterium (Klasse 1) in `ScmDispatchPrioritaet` |

## `ScmDelayDirective.ResultKind`
Datei: `src/Softwareschmiede/Application/Services/ScmDelayDirective.cs` (Z. 24–32)

| Wert | Bedeutung |
|------|-----------|
| `None` | Keine frei stehende Direktive im Text |
| `Deferred` | Gültige Direktive mit Zielzeitpunkt → Ereignisstatus „Verzögert" + `PausiertBisUtc`-Pause |
| `Invalid` | Direktive erkannt, Angabe verworfen (kein Eingriff in die Pause) |

## `ScmPromptReadyStatus`
Datei: `src/Softwareschmiede/Application/Services/ScmPromptSessionAdapter.cs` (Z. 9–21)

Eingabestatus des Session-Adapters; in `DispatchCoreAsync` Gate vor `WritePromptAsync`. Werte: `Unknown`, `Ready`, `Busy`, `TrustPrompt`, `Exited`. Für die Platzfreigabe nicht direkt relevant, aber Teil des Fortsetzungspfads, der ohne Zulassungsprüfung läuft.

## `CliProcessStatus`
Datei: `src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs` (Z. 894–903)

Status des `CliProcessStatusChanged`-Ereignisses des `KiAusfuehrungsService`; von `CliProcessManager` für den Heartbeat-Lifecycle verwertet.

| Wert | Bedeutung |
|------|-----------|
| `Gestartet` | Prozess läuft |
| `Gestoppt` | Prozess wurde gestoppt |
| `Fehler` | Prozess ist mit einem Fehler beendet |
