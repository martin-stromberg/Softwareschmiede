# Enums: Aufgabenworkflow Optimierung

## `AufgabeStatus`
Datei: `src/Softwareschmiede/Domain/Enums/AufgabeStatus.cs`

| Wert | Bedeutung | Status nach Anforderung |
|------|-----------|--------------------------|
| `Neu` | Aufgabe wurde erstellt und wartet auf Bearbeitung | **Behalten** ✓ |
| `ArbeitsverzeichnisEingerichtet` | Arbeitsverzeichnis (lokaler Klon) wurde eingerichtet | **Entfernen** ✗ |
| `Gestartet` | Aufgabe wurde gestartet (Branch erstellt, bereit für CLI) | **Behalten** ✓ |
| `InArbeit` | CLI-Prozess läuft aktiv | **Entfernen** ✗ |
| `Wartend` | CLI hat Rate-Limit erreicht; wartet auf Wiederaufnahme | **Behalten** ✓ |
| `Beendet` | Aufgabe wurde beendet (erfolgreich oder mit Fehler) | **Behalten** ✓ |
| `Archiviert` | Aufgabe wurde archiviert und ist nicht mehr aktiv | **Behalten** ✓ |

**Aktuelle Übergänge (validiert in `AufgabeService.ValidateStatusTransition`):**
- `Neu` → `ArbeitsverzeichnisEingerichtet`
- `ArbeitsverzeichnisEingerichtet` → `Gestartet`
- `Gestartet` → `InArbeit`
- `InArbeit` → `Beendet`, `Wartend`
- `Wartend` → `InArbeit`, `Beendet`
- `Beendet` → (keine Übergänge)
- `Archiviert` → (keine Übergänge)
- Von jedem Status → `Archiviert` (zulässig)

**Anforderung:** Nach Anforderung soll die Kette vereinfacht werden zu:
- `Neu` → `Gestartet` (direkt durch neue Aktion „Starten")
- `Gestartet` → `InArbeit` (durch CLI-Start)
- `InArbeit` → `Beendet`, `Wartend`
- `Wartend` → `InArbeit`, `Beendet`

Damit sind `ArbeitsverzeichnisEingerichtet` und `InArbeit` zu entfernen.

**Migration vorhanden:** `20260610000001_UpdateAufgabeStatusEnum` migriert alte Status zu neuen.
