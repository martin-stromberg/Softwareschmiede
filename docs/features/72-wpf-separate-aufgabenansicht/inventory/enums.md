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

**Verwendung in Feature 72:**
- **Neue Aufgabe:** Status wird auf `Neu` gesetzt via `AufgabeService.CreateAsync()` (Zeile 146)
- **Edit-Panel:** Sichtbar wenn Status == `Neu` (TaskDetailViewModel.ShowEditPanel, Zeile 181)
- **Speichern:** Nach Speichern in Neuanlage bleibt Status auf `Neu` und View navigiert zurück
- **Status-Übergänge:** Folgen strikten Validierungsregeln in `AufgabeService.ValidateStatusTransition()`

---

## `AufgabenFilterTyp`
Datei: `src/Softwareschmiede/Domain/Enums/AufgabenFilterTyp.cs`

| Wert | Bedeutung |
|------|-----------|
| `Alle` | Zeigt alle Aufgaben |
| `Aktiv` | Zeigt nur aktive Aufgaben (nicht archiviert) |
| `Archiviert` | Zeigt nur archivierte Aufgaben |

**Verwendung:**
- Property `AufgabenFilter` in `ProjectDetailViewModel` (Zeile 118-122)
- Filter-UI in `ProjectDetailView.xaml` (Zeile 109-120)
- Derzeit wird Filter **nicht** in der Aufgabenliste-Filterung verwendet
