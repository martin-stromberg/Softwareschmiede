# Enums

## `AufgabeStatus`
Datei: `src/Softwareschmiede/Domain/Enums/AufgabeStatus.cs`

| Wert | Bedeutung |
|------|-----------|
| `Neu` | Aufgabe wurde erstellt und wartet auf Bearbeitung. |
| `Gestartet` | Aufgabe wurde gestartet (Branch erstellt, CLI läuft oder sollte laufen). |
| `Wartend` | **CLI hat Rate-Limit erreicht; wartet auf Wiederaufnahme.** |
| `Beendet` | Aufgabe wurde beendet (erfolgreich oder mit Fehler). |
| `Archiviert` | Aufgabe wurde archiviert und ist nicht mehr aktiv. |

**Anforderungsrelevanz:** `Wartend` deckt die fachliche Rate-Limit-Semantik bereits ab (inkl. Transitions-Validierung `Gestartet ↔ Wartend` in `AufgabeService.ValidateStatusTransition`). Ein eigener `Pausiert`-Wert existiert nicht.

## `AufgabeStatusExtensions` (Extension-Member zum Enum)
Datei: `src/Softwareschmiede/Domain/Enums/AufgabeStatusExtensions.cs`

| Member | Bedeutung |
|--------|-----------|
| `AktivOderWartendStatus` | `static readonly AufgabeStatus[]` = `[Gestartet, Wartend]` (Z. 7) |
| `IstAktivOderWartend()` | Extension: `true` für `Gestartet` oder `Wartend` (Z. 12) |

Verwendet u. a. in `AufgabeService` (EF-Prädikat `IstAktivOderWartendPredicate`, Z. 20) und `AufgabeRecoveryService` (`IstRecoveryStatus`, Z. 223).

## `AufgabeAusfuehrungsStatus`
Datei: `src/Softwareschmiede/Domain/Enums/AufgabeAusfuehrungsStatus.cs`

| Wert | Bedeutung |
|------|-----------|
| `NichtGestartet` | KI-Ausführung wurde noch nicht gestartet. |
| `Aktiv` | KI-Ausführung ist aktiv oder soll nach App-Neustart wiederhergestellt werden. |
| `Beendet` | KI-Ausführung beendet; erneuter Start muss explizit ausgelöst werden. |

## `AufgabeLaufStatus`
Datei: `src/Softwareschmiede/Domain/Enums/AufgabeLaufStatus.cs`

| Wert | Bedeutung |
|------|-----------|
| `Laeuft` | CLI läuft und hat kürzlich Ausgabe/Eingabe verarbeitet. |
| `WartetAufEingabe` | CLI läuft, erzeugt aber länger keine Ausgabe (vermutlich Eingabe erwartet). |

**Wichtige Abgrenzung (Dokumentationskommentar im Code):** `AufgabeLaufStatus.WartetAufEingabe` ist ein rein beobachtender Laufzeit-Substatus während `Gestartet` — explizit **nicht** identisch mit `AufgabeStatus.Wartend` (Rate-Limit-Lebenszyklus mit Transitions-Validierung).

## `ProtokollTyp`
Datei: `src/Softwareschmiede/Domain/Enums/ProtokollTyp.cs`

| Wert | Bedeutung |
|------|-----------|
| `Prompt` | Prompt an den KI-Agenten. |
| `KiAntwort` | Antwort des KI-Agenten. |
| `StatusUebergang` | Statusübergang der Aufgabe. |
| `TestErgebnis` | Ergebnis eines Testlaufs. |
| `GitAktion` | Git-Aktion (Commit, Push, Branch, …). |
| `CliOutput` | Ausgabezeile eines eingebetteten CLI-Prozesses. |
| `RateLimit` | **Erkannter Rate-Limit-Marker aus der CLI-Ausgabe.** |
| `SystemMeldung` | Interne Systemmeldung (z. B. Prozessende mit Fehlercode). |

**Anforderungsrelevanz:** `RateLimit` ist bereits vorhanden und wird von `ProtokollService.AddCliOutputAsync` erzeugt.

## `CliProcessStatus`
Datei: `src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs` (Z. 731)

| Wert | Bedeutung |
|------|-----------|
| `Gestartet` | Prozess läuft. |
| `Gestoppt` | Prozess wurde gestoppt. |
| `Fehler` | Prozess mit Fehler beendet. |

Wird via `KiAusfuehrungsService.CliProcessStatusChanged` publiziert und von `CliProcessManager` sowie `TaskDetailViewModel` verarbeitet.

## `CliRuntimeStatus`
Datei: `src/Softwareschmiede/Infrastructure/Terminal/PseudoConsoleSession.cs` (Z. 400)

| Wert | Bedeutung |
|------|-----------|
| `Inaktiv` | Kein laufender CLI-Prozess aktiv. |
| `Laeuft` | CLI läuft, kürzlich I/O-Aktivität. |
| `WartetAufEingabe` | CLI läuft, aber länger keine Ausgabe (vermutlich Eingabe erwartet). |

Wird von `PseudoConsoleSession.RuntimeStatusChanged` publiziert und von `CliProcessManager.OnRuntimeStatusChanged` auf `AufgabeLaufStatus` übersetzt.

## `PluginKategorie`
Datei: `src/Softwareschmiede/Domain/Enums/PluginKategorie.cs`

| Wert | Bedeutung |
|------|-----------|
| `Git` | Git-Provider-Plugin. |
| `Ki` | KI-Plugin. |
| `Ide` | IDE-Integrations-Plugin. |

Wird in `PluginKonfiguration.PluginKategorie` verwendet.

## `PluginType`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Enums/PluginType.cs`

| Wert | Bedeutung |
|------|-----------|
| `SourceCodeManagement` | SCM-Plugin (`IGitPlugin`). |
| `DevelopmentAutomation` | KI-Plugin (`IKiPlugin`). |
| `Ide` | IDE-Plugin (`IIdePlugin`). |

Wird u. a. von `PluginManager` (Registrierung) und `PluginSelectionService` (Default-Schlüssel) verwendet.
