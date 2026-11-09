# Enums — Bestandsaufnahme

## `AufgabeStatus`

Datei: `src/Softwareschmiede/Domain/Enums/AufgabeStatus.cs`

| Wert | Bedeutung |
|------|-----------|
| `Neu` | Aufgabe erstellt, wartet auf Bearbeitung |
| `Gestartet` | Branch erstellt, CLI läuft oder sollte laufen (aktiv: `IstAktivOderWartend`) |
| `Wartend` | CLI hat Rate-Limit erreicht; wartet auf Wiederaufnahme (aktiv: `IstAktivOderWartend`) |
| `Beendet` | Aufgabe beendet (terminal) |
| `Archiviert` | Aufgabe archiviert (terminal) |

Zugehörig: `AufgabeStatusExtensions.AktivOderWartendStatus = [Gestartet, Wartend]`, `IstAktivOderWartend()` (`src/Softwareschmiede/Domain/Enums/AufgabeStatusExtensions.cs`).

## `AufgabeAusfuehrungsStatus`

Datei: `src/Softwareschmiede/Domain/Enums/AufgabeAusfuehrungsStatus.cs`

| Wert | Bedeutung |
|------|-----------|
| `NichtGestartet` | KI-Ausführung wurde noch nicht gestartet (Default) |
| `Aktiv` | Ausführung aktiv oder nach App-Neustart wiederherzustellen — **kann ohne `AktiveRunId` persistiert sein** (`StartenAsync`, `AusfuehrungAktivSetzenAsync`) |
| `Beendet` | Ausführung beendet; Neustart nur explizit |

Zugehörig: `AufgabeAusfuehrungsStatusExtensions` (`src/Softwareschmiede/Domain/Enums/AufgabeAusfuehrungsStatusExtensions.cs`): `DarfAusfuehrungStarten` (NichtGestartet/Beendet + nicht terminaler `AufgabeStatus`), `SollCliAnzeigen` (`Status` aktiv/wartend + `AusfuehrungsStatus ∈ {Aktiv, Beendet}`) — steuert, ob `TaskDetailViewModel` die Session/Fußzeile anbindet.

## `AufgabeLaufStatus`

Datei: `src/Softwareschmiede/Domain/Enums/AufgabeLaufStatus.cs`

| Wert | Bedeutung |
|------|-----------|
| `Laeuft` | CLI läuft, kürzlich Ausgabe/Eingabe verarbeitet |
| `WartetAufEingabe` | CLI läuft, keine Ausgabe seit längerer Zeit (vermutlich Eingabe erwartet) |

Persistierter Substatus, nur gültig bei gesetzter `AktiveRunId`. Mapping aus `CliRuntimeStatus` in `CliProcessManager.OnRuntimeStatusChanged` (`Inaktiv` wird nicht abgebildet).

## `CliProcessStatus`

Datei: `src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs` (Z. 826–835)

| Wert | Bedeutung |
|------|-----------|
| `Gestartet` | Prozess läuft (nur bei echtem Neustart gefeuert, nicht bei Session-Wiederverwendung) |
| `Gestoppt` | Prozess gestoppt (`AbsichtlichGestoppt` oder Exit-Code 0) |
| `Fehler` | Fehlerhaftes Ende (Exit-Code ≠ 0 oder fataler Session-`Failed`-Laufzeitfehler) |

## `CliRuntimeStatus`

Datei: `src/Softwareschmiede/Infrastructure/Terminal/PseudoConsoleSession.cs` (Z. 649–658)

| Wert | Bedeutung |
|------|-----------|
| `Inaktiv` | Kein laufender CLI-Prozess |
| `Laeuft` | CLI läuft, kürzliche I/O-Aktivität |
| `WartetAufEingabe` | CLI läuft, keine I/O-Aktivität ≥ Warte-Schwelle (4 s) |

## `KiAusfuehrungsZustand`

Datei: `src/Softwareschmiede.App/Converters/AppConverters.cs` (Z. 116–132, `internal`)

| Wert | Bedeutung |
|------|-----------|
| `Pausiert` | `PausiertBisUtc` in Zukunft (überlagert alles) |
| `PromptInWartestellung` | Zeitgesteuerter Prompt in der Warteschlange |
| `Laeuft` | Aktiver Run ohne WartetAufEingabe-Substatus → „▶ Läuft" |
| `Wartet` | Substatus `WartetAufEingabe` oder `AufgabeStatus.Wartend` → „⏸ Wartet" |
| `Bereit` | Keine aktive Ausführung erkennbar → „✓ Bereit" |
