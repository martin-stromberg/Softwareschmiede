# Enums

## `AufgabeStatus`
Datei: `src/Softwareschmiede/Domain/Enums/AufgabeStatus.cs`

| Wert | Bedeutung |
|------|-----------|
| `Neu` | Aufgabe wurde erstellt und wartet auf Bearbeitung |
| `ArbeitsverzeichnisEingerichtet` | Arbeitsverzeichnis (lokaler Klon) wurde eingerichtet |
| `Gestartet` | Aufgabe wurde gestartet (Branch erstellt, bereit für CLI) |
| `InArbeit` | CLI-Prozess läuft aktiv |
| `Wartend` | CLI hat Rate-Limit erreicht; wartet auf Wiederaufnahme |
| `Beendet` | Aufgabe wurde beendet (erfolgreich oder mit Fehler) |
| `Archiviert` | Aufgabe wurde archiviert und ist nicht mehr aktiv |
