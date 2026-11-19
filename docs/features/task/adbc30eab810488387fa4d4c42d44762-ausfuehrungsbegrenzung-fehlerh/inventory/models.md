# Datenmodell — Bestandsaufnahme

## `Aufgabe`
Datei: `src/Softwareschmiede/Domain/Entities/Aufgabe.cs`

Für die Anforderung relevante Eigenschaften (Auswahl, nicht die vollständige Entität):

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `Guid` | Eindeutige ID; Schlüssel der `_handles`-Map im `KiAusfuehrungsService` und der `GetLaufendeAufgabenIds`-Menge (Z. 11) |
| `Status` | `AufgabeStatus` | Fachlicher Lebenszyklus; `Beendet`/`Archiviert` sperren Starts (`BlockReason`) und Pausen (`SetPauseAsync`) (Z. 26) |
| `AusfuehrungsStatus` | `AufgabeAusfuehrungsStatus` | Persistierter Lauf-Lebenszyklus (`NichtGestartet`/`Aktiv`/`Beendet`); Teil der Fortsetzungs-Klassifikation in `ScmDispatchPrioritaet` (Z. 29) |
| `KiPluginPrefix` | `string?` | Gespeicherte Plugin-Zuordnung; Fallback-Attribution des ProPlugin-Zählers in `GetBelegungAsync` und Filterkriterium der Session-Limit-Pause (Z. 62) |
| `AktiveRunId` | `string?` | Aktive Lauf-ID; Kriterium für `AufgabeLaufAktivitaet.IstAktiv` in der Statusklassifikation (Z. 71) |
| `LastHeartbeatUtc` | `DateTimeOffset?` | Letzter Heartbeat; Recovery-Kriterium (Z. 74) |
| `LaufStatus` | `AufgabeLaufStatus?` | Persistierter Laufzeit-Substatus (`Läuft`/`WartetAufEingabe`) (Z. 86) |
| `PausiertBisUtc` | `DateTimeOffset?` | **Zentrale Eigenschaft der Anforderung.** UTC-Pausenende; Wert in der Zukunft blockiert neue Starts, CLI-Neustarts, Recovery und Prompt-Versand — ein laufender Prozess wird nicht unterbrochen. `null`/Vergangenheit = „nicht pausiert" (Z. 97–103). Wird in `GetBelegungAsync` aktuell **nicht** geladen. |
| `ScmAbschlussErkanntUtc` | `DateTimeOffset?` | Startpunkt der konfigurierten Abschluss-Wartezeit (`ScmTaskCompletionMonitoringService`) — fachlich getrennt von der Pause (Z. 111) |
| `ScmAbschlussReferenz` | `string?` | Anzeigetext der beobachteten Abschluss-Referenz (Z. 115) |
| `Zweck` | `string?` | Zweck-Markierung (z. B. `"IssueAufbereitung"`); ausgeschlossen von der SCM-Ereignisauflösung (Z. 120) |

## `CliAusfuehrungsBelegung`
Datei: `src/Softwareschmiede/Application/Services/ScmEventExecutionLimitService.cs` (Z. 9–16)

Record: Belegung laufender CLI-Ausführungen für die Ausführungsbegrenzung.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Global` | `int` | Anzahl aller Aufgaben mit laufender CLI-Ausführung (= `laufendeIds.Count` in `GetBelegungAsync` plus offene Reservierungen in `WirksameBelegungAsync`) |
| `ProPlugin` | `IReadOnlyDictionary<string, int>` | Anzahl laufender Ausführungen je KI-Plugin-Prefix (Vergleich `OrdinalIgnoreCase`) |
| `FuerPlugin(pluginPrefix)` | Methode → `int` | Belegung eines Prefix; `0` bei leerem/unbekanntem Prefix |

## `CliStartPlatzZulassung`
Datei: `src/Softwareschmiede/Application/Services/ScmEventExecutionLimitService.cs` (Z. 24)

Record: Ergebnis der atomaren Zulassungsprüfung.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Wartegrund` | `string?` | Benutzerlesbarer Wartegrund bei erreichtem Limit; `null` bei freiem Platz |
| `Reservierung` | `IDisposable?` | Belegter Startplatz bis zur Handle-Registrierung; `null` bei vollem Limit oder unbegrenzter Konfiguration |

## `StartPlatzReservierung`
Datei: `src/Softwareschmiede/Application/Services/ScmEventExecutionLimitService.cs` (Z. 230–241)

Private, verschachtelte `sealed class` (prozessweiter Bestand in der statischen `Reservierungen`-Map).

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `Guid` | Zufällige Reservierungs-ID (`Guid.NewGuid()`) |
| `PluginPrefix` | `string` | Prefix, für den der Platz reserviert ist; leere Prefixe zählen nur ins globale Kontingent |
| `GueltigBisUtcMs` | `long` | Verfallzeitpunkt (Unix-ms), `ReservierungsLebensdauer` = 5 min |
| `Dispose()` | Methode | Entfernt die Reservierung non-blocking aus `Reservierungen` |

## `ScmDispatchPrioritaet.EreignisAuswahl`
Datei: `src/Softwareschmiede/Application/Services/ScmDispatchPrioritaet.cs` (Z. 23)

Record: Auswahlzeile der Dispatch-Priorisierung.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `Guid` | Ereignis-ID |
| `AufgabeId` | `Guid` | Zugehörige Aufgabe |
| `Sequence` | `long` | Eingangsreihenfolge |
| `Status` | `string?` | Persistierter Ereignisstatus (`"Verzögert"` markiert Klasse 2) |
| `NextAttemptUtc` | `long?` | Zielzeitpunkt (Unix-ms) für die Klasse-2-Sortierung |

## `ScmDispatchPrioritaet.AufgabenZustand`
Datei: `src/Softwareschmiede/Application/Services/ScmDispatchPrioritaet.cs` (Z. 29–30)

Record: Für die Priorisierung benötigter Aufgabenzustand — wird in `DispatchDueEventsAsync` (`ScmEventOrchestrator` Z. 55–59) per Projektion inkl. `PausiertBisUtc` geladen.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Status` | `AufgabeStatus` | Fachlicher Lebenszyklus |
| `AusfuehrungsStatus` | `AufgabeAusfuehrungsStatus` | Lauf-Lebenszyklus (letzter Lauf) |
| `PausiertBisUtc` | `DateTimeOffset?` | Pausenziel; **gesetzt — auch abgelaufen — markiert eine Fortsetzung** (Klasse 1), bewusst andere Semantik als das Pausenkriterium `> UtcNow` |

## `KiAusfuehrungsStatusDaten`
Datei: `src/Softwareschmiede/Application/Services/KiAusfuehrungsStatusDaten.cs` (Z. 15–23)

Record: Eingabe der gemeinsamen Statusklassifikation (`KiAusfuehrungsStatusKlassifikation`); `VonAufgabe` (Z. 33–45) befüllt aus der Entität inkl. `PausiertBisUtc` (Z. 23).

## `ScmDelayDirective.Result`
Datei: `src/Softwareschmiede/Application/Services/ScmDelayDirective.cs` (Z. 36–44)

Record-Struct: Ergebnis der Verzögerungs-Direktivenauswertung (`Kind`, `TargetUtc`, `Reason`, `Argument`). Bei `Kind == Deferred` setzt `ScmEventStore.PauseForDelayAsync` `Aufgabe.PausiertBisUtc = delay.TargetUtc`.
