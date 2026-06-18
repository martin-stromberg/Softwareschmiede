# Enums

## `AufgabeStatus`
Datei: `src/Softwareschmiede/Domain/Enums/AufgabeStatus.cs`

| Wert | Bedeutung |
|------|-----------|
| `Neu` | Aufgabe wurde erstellt und wartet auf Bearbeitung |
| `Gestartet` | Aufgabe wurde gestartet (Branch erstellt, CLI läuft oder sollte laufen) |
| `Wartend` | CLI hat Rate-Limit erreicht; wartet auf Wiederaufnahme |
| `Beendet` | Aufgabe wurde beendet (erfolgreich oder mit Fehler) |
| `Archiviert` | Aufgabe wurde archiviert und ist nicht mehr aktiv |

**Hinweis:** Nach Anforderung sollten neue Aufgaben aus Issues den Status `AufgabeStatus.Neu` erhalten (bereits implementiert in `CreateFromIssueAsync`).
