# Datenmodellklassen

## `PromptVorlage`
Datei: `src/Softwareschmiede/Domain/Entities/PromptVorlage.cs`

Persistierte Promptvorlage für wiederkehrende CLI-Eingaben. **Keine zeitliche Verzögerung persistiert** – die zeitgesteuerte Versendung ist eine Laufzeit-Eigenschaft ohne Speicherung.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `Guid` | Eindeutige ID der Vorlage |
| `Name` | `string` | Anzeigename der Vorlage |
| `Prompttext` | `string` | Prompttext, der nach Platzhalterauflösung an die CLI gesendet wird |
| `Sortierung` | `int` | Sortierposition für die Anzeige |
| `ErstelltAm` | `DateTimeOffset` | Erstellungszeitpunkt |
| `AktualisiertAm` | `DateTimeOffset` | Letzter Aktualisierungszeitpunkt |

### Verwendung in TaskDetailViewModel
- `TaskDetailViewModel._selectedPromptVorlage` (privates Feld, nullable)
- `TaskDetailViewModel.SelectedPromptVorlage` (public Property, bindbar)
- `TaskDetailViewModel.PromptVorlagen` (ObservableCollection für UI-Anzeige)
