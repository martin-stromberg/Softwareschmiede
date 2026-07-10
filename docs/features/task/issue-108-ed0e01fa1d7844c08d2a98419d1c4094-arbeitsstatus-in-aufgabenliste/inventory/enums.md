# Enums und Erweiterungen

## `AufgabeStatus`
Datei: `src\Softwareschmiede\Domain\Enums\AufgabeStatus.cs`

| Wert | Bedeutung |
|------|-----------|
| `Neu` | Aufgabe wurde erstellt und wartet auf Bearbeitung |
| `Gestartet` | Aufgabe wurde gestartet (Branch erstellt, CLI läuft oder sollte laufen) |
| `Wartend` | CLI hat Rate-Limit erreicht; wartet auf Wiederaufnahme |
| `Beendet` | Aufgabe wurde beendet (erfolgreich oder mit Fehler) |
| `Archiviert` | Aufgabe wurde archiviert und ist nicht mehr aktiv |

### Relevante Status für die Anforderung:
- `Gestartet` und `Wartend` → "aktiv" (werden in der Aufgabenliste angezeigt)
- `Wartend` → zeigt "⏸ Wartet" im Converter an
- `Gestartet` → zeigt "▶ Läuft" oder "✓ Bereit" je nach Heartbeat

---

## `AufgabeStatusExtensions`
Datei: `src\Softwareschmiede\Domain\Enums\AufgabeStatusExtensions.cs`

| Erweiterung | Beschreibung |
|-------------|-------------|
| `AktivOderWartendStatus` | Static readonly Array: `[AufgabeStatus.Gestartet, AufgabeStatus.Wartend]` — einzige Quelle der Wahrheit für "aktive oder wartende Aufgaben" |
| `IstAktivOderWartend(this AufgabeStatus)` | Erweiterungsmethode: Gibt `true` zurück, wenn Status `Gestartet` oder `Wartend` ist |

### Verwendung in der Anforderung:
- `AufgabeService` nutzt `AktivOderWartendStatus` in LINQ-Queries
- `AufgabeRecoveryService` prüft mit `IstAktivOderWartend()`, ob eine Aufgabe Recovery-Kandidat ist
- Der Converter nutzt `Status == AufgabeStatus.Wartend`, um "⏸ Wartet" anzuzeigen

---

## `CliProcessStatus`
Datei: `src\Softwareschmiede\Application\Services\KiAusfuehrungsService.cs` (Zeile 647–655)

| Wert | Bedeutung |
|------|-----------|
| `Gestartet` | Prozess läuft |
| `Gestoppt` | Prozess wurde gestoppt |
| `Fehler` | Prozess ist mit einem Fehler beendet |

### Verwendung in der Anforderung:
- Wird als Parameter des `CliProcessStatusChanged`-Events ausgelöst
- **Nicht direkt** vom `KiAusfuehrungsStatusConverter` genutzt (dieser arbeitet mit `Aufgabe` und deren Eigenschaften)
- **Potenzielle Verwendung:** ViewModels könnten dieses Event abonnieren, um die Aufgabenliste zu aktualisieren
