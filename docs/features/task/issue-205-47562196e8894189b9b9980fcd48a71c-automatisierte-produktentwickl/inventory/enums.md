# Enums

## `DetailAnsicht`

Datei: `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs` (Zeilen 26–34)

Private enum in TaskDetailViewModel, steuert, welche Ansicht in der TaskDetailView gerade angezeigt wird.

| Wert | Bedeutung |
|------|-----------|
| `Info` | Stammdaten-Ansicht (Aufgabentitel, Anforderungsbeschreibung, Protokoll) |
| `Cli` | CLI-Ausführungs-Ansicht (nur sichtbar, wenn `ShowCliPanel == true`) |
| `Diff` | Diff-Ansicht (nur sichtbar, wenn `ShowDiffPanel == true`, also Status == Beendet) |
| `Dateibrowser` | Dateiexplorer-Ansicht (nur sichtbar, wenn lokaler Klonpfad existiert) |
| `PullRequests` | Pull-Request-Ansicht |
| `Todos` | To-Do-Listen-Ansicht |

**Zu erweitern:** Ein neuer Wert `Automatisierung` ist für die Integration der autonomen Aufgaben-Registerkarte notwendig.
