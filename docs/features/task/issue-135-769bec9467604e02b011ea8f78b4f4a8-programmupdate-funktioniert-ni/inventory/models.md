# Datenmodelle

## `UpdateProgressViewModel`

Datei: `src/Softwareschmiede.App/ViewModels/UpdateProgressViewModel.cs`

ViewModel für den Fortschrittsdialog der Update-Vorbereitung. Erbt von `ViewModelBase`.

| Eigenschaft | Typ | Sichtbarkeit | Beschreibung |
|-------------|-----|-------------|-----------|
| `PhaseText` | `string` | public (get), private (set) | Aktuelle Phase als Text, z. B. "Download", "Entpacken" |
| `Message` | `string` | public (get), private (set) | Aktuelle Fortschrittsmeldung |
| `Percent` | `double` | public (get), **private (set)** | Fortschritt in Prozent; **PROBLEM: private set verursacht WPF-Binding-Fehler** |
| `IsIndeterminate` | `bool` | public (get), private (set) | Gibt an, ob der Fortschritt ohne konkreten Prozentwert angezeigt wird |
| `HasError` | `bool` | public (get), private (set) | Gibt an, ob ein Fehler angezeigt wird |
| `CanClose` | `bool` | public (get), private (set) | Gibt an, ob der Dialog geschlossen werden darf |
| `CanCancel` | `bool` | public (get), private (set) | Gibt an, ob die Vorbereitung noch abgebrochen werden kann; triggert `RelayCommand.Refresh` bei Änderung |
| `CancelCommand` | `ICommand` | public (get) | Befehl zum Abbrechen der Vorbereitung |

**Interne Felder:**
- `_phaseText`, `_message`, `_percent`, `_isIndeterminate`, `_hasError`, `_canClose`, `_canCancel`
- `_cancelAction` (Action) - callback für Abbruch-Anforderung

**Verwendetes Pattern:**
- `SetProperty()` aus `ViewModelBase` zum Aktualisieren von Eigenschaften mit PropertyChanged-Notification
