# Enums

## `AufgabeLaufStatus`

**Datei:** `src/Softwareschmiede/Domain/Enums/AufgabeLaufStatus.cs`

Feingranularer Laufzeit-Substatus einer Aufgabe während ein CLI-Prozess für sie aktiv läuft (d. h. `Aufgabe.AktiveRunId` ist gesetzt).

### Werte

| Wert | Bedeutung / Zeile |
|------|-------------------|
| `Laeuft` | Die CLI läuft und hat kürzlich Ausgabe oder Eingabe verarbeitet (Zeile 24) |
| `WartetAufEingabe` | Die CLI läuft, erzeugt aber seit längerer Zeit keine Ausgabe und wartet vermutlich auf Benutzereingabe (Zeile 27) |

### Impliziter Wert

| Wert | Bedeutung |
|------|-----------|
| `null` | Kein aktiver Lauf oder noch nicht initialisiert ("Bereit") — nach der Anforderung **nicht blockierend** |

### Wichtiger Kontext (Zeile 9–19)

Das Enum ist bewusst als eigenständiges Domain-Enum modelliert, **nicht** als Wiederverwendung von `AufgabeStatus.Wartend`:

- `AufgabeStatus.Wartend` = Lebenszyklus-Zustand ("CLI hat Rate-Limit erreicht; wartet auf Wiederaufnahme")
- `AufgabeLaufStatus` = Rein beobachtender Laufzeit-Substatus (abgeleitet aus Terminal-I/O-Aktivität, siehe `Infrastructure.Terminal.CliRuntimeStatusEvaluator`)

Der `AufgabeLaufStatus` kann während des Status `Gestartet` mehrfach pro Sekunde wechseln und unterliegt keiner Transitions-Validierung.

### Herkunft der Werte

Die Werte werden von `CliProcessManager` anhand des `PseudoConsoleSession.RuntimeStatusChanged`-Ereignisses aktualisiert (dokumentiert in `Aufgabe.LaufStatus`, Zeile 58–59).
