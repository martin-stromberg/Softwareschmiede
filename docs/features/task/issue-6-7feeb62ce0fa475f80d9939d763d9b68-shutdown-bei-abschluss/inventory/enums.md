# Enums — Shutdown bei Abschluss

## `KiAusfuehrungsZustand`
Datei: `src/Softwareschmiede.App/Converters/AppConverters.cs` (vor `KiAusfuehrungsStatusKlassifikation`, ~Z. 130–137); `internal`

Fachlicher Anzeigezustand einer Aufgabe in der Seitenleiste — trägt bereits die geforderte „läuft vs. wartet"-Semantik.

| Wert | Bedeutung | Setzlogik (Kurzform) |
|------|-----------|----------------------|
| `Pausiert` | Aufgabe pausiert | `PausiertBisUtc` in Zukunft (überlagert alle übrigen Stati) |
| `PromptInWartestellung` | geplanter Prompt wartet | `HasScheduledPrompt`/`ScheduledPromptInfo` vorhanden |
| `Laeuft` | Automatisierung läuft | aktiver Lauf (Heartbeat/`AktiveRunId`/Live-Prozess) und Substatus nicht `WartetAufEingabe` |
| `Wartet` | wartet auf Eingabe / Rate-Limit | Live-/persistierter `LaufStatus == WartetAufEingabe` **oder** `AufgabeStatus.Wartend` |
| `Bereit` | kein aktiver Lauf | Fallback |

Anforderungsbezug: `Laeuft` entspricht „laufende Automatisierung"; `Wartet` zählt explizit **nicht** als laufend.

## `AufgabeLaufStatus`
Datei: `src/Softwareschmiede/Domain/Enums/AufgabeLaufStatus.cs`

Persistierter Laufzeit-Substatus (`Aufgabe.LaufStatus`), auch Live-Substatus (`ITerminalSession` → `GetLiveLaufStatus`).

| Wert | Bedeutung |
|------|-----------|
| `Laeuft` | Prozess aktiv, keine Eingabe erwartet |
| `WartetAufEingabe` | Session wartet auf Benutzereingabe (interaktiver Prompt) |

## `AufgabeStatus`
Datei: `src/Softwareschmiede/Domain/Enums/AufgabeStatus.cs`

Lebenszyklusstatus der Aufgabe.

| Wert | Bedeutung / Relevanz |
|------|----------------------|
| `Neu` | angelegt, nicht gestartet |
| `Gestartet` | in Bearbeitung |
| `Wartend` | wartet auf Rate-Limit-Freigabe — zählt in `KiAusfuehrungsStatusKlassifikation` als `Wartet` |
| `Beendet` | abgeschlossen |
| `Archiviert` | archiviert |

## `AufgabeAusfuehrungsStatus`
Datei: `src/Softwareschmiede/Domain/Enums/AufgabeAusfuehrungsStatus.cs`

Persistierter Status der KI-Ausführung (`Aufgabe.AusfuehrungsStatus`).

| Wert | Bedeutung |
|------|-----------|
| `NichtGestartet` | noch keine KI-Ausführung (Default) |
| `Aktiv` | Ausführung läuft/lief — Voraussetzung des Persistenz-Zweigs der Klassifikation |
| `Beendet` | Ausführung abgeschlossen |

## `CliRuntimeStatus`
Datei: `src/Softwareschmiede/Domain/Enums/CliRuntimeStatus.cs` (bzw. gleichnamige Datei im Terminal-/Domain-Bereich); Extensions: `CliRuntimeStatusExtensions.cs`

Live-Status der Terminal-Session (`ITerminalSession.RuntimeStatus`, Event `RuntimeStatusChanged`).

| Wert | Bedeutung / Mapping |
|------|---------------------|
| `Inaktiv` | Session beendet/inaktiv — von `CliProcessManager` ignoriert (Prozessende läuft über `Process.Exited`) |
| `Laeuft` | CLI verarbeitet → persistiert `AufgabeLaufStatus.Laeuft` |
| `WartetAufEingabe` | Prompt sichtbar → persistiert `AufgabeLaufStatus.WartetAufEingabe` |

## `CliProcessStatus`
Datei: `src/Softwareschmiede/Domain/Enums/CliProcessStatus.cs` (Domain-Enum); Event-Payload von `KiAusfuehrungsService.CliProcessStatusChanged`

| Wert | Bedeutung |
|------|-----------|
| `Gestartet` | CLI-Prozess gestartet |
| `Gestoppt` | Prozess beendet, `AbsichtlichGestoppt == true` |
| `Fehler` | Prozess beendet, nicht absichtlich (Absturz/Exit != 0) |

## `AufgabenListenModus`
Datei: `src/Softwareschmiede/Domain/Enums/AufgabenListenModus.cs`

Listenmodus der Aufgabenübersicht: `Datum`, `Repository`, `Ki` (Grouping-Schlüssel). Relevanz: Referenz für persistierte UI-Einstellung via `AufgabenListenModusService` + `AufgabenListenModusBindung` (gleiches Muster für Auto-Shutdown-Checkbox/Force denkbar — nur als Referenz, keine Umsetzungsaussage).

## Hinweis zu weiteren Enums
`UpdateVersuchStatus`/`UpdateVersuchGrenze` (Update-E2E-Protokoll) und `ScmPluginArt` sind für die Anforderung nicht direkt relevant und nur der Vollständigkeit halber im Kontext der Fußzeilen-Update-Anzeige erwähnt.
