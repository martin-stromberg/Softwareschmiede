# UI-Komponenten und Enums

Analyse der XAML-Views und Enums bezüglich der Anforderung zur Ribbon-Integration.

## `TaskDetailView.xaml` (Ribbon-Struktur)

Datei: `src/Softwareschmiede.App/Views/TaskDetailView.xaml`

**Zusammenfassung:** UserControl für die Aufgabendetailansicht mit Ribbon-Menü, Fehlerbereich, Hauptinhalt und Statusleiste.

### Bestehende Ribbon-Gruppen (Grid.Row="0")

| Gruppe | GruppenName | Buttons | Sichtbarkeit | Bindung |
|---|---|---|---|---|
| Navigation | "Navigation" | Zurück | Immer sichtbar | `ZurueckCommand` |
| Aufgabe | "Aufgabe" | Speichern, Löschen, Starten, Beenden | Abhängig von Status | Verschiedene Commands |
| CLI | "CLI" | Plugin ändern, CLI starten, Stoppen, Promptvorlage | Nur wenn ShowCliPanel | Verschiedene Commands, Combobox für Prompts, Zeitsteuerung |
| Issue | "Issue" | Issue zuweisen, Issue öffnen | Nur wenn `CanAssignIssue` | Verschiedene Commands |

### Fehlende Ribbon-Gruppen

**Status:** NICHT VORHANDEN

#### Gruppe: "Dateien" (geplant)

Geplante Buttons (laut Anforderung, aus FileExplorerViewModel):
- Ansichtswechsel Standard/Vergleich: `StandardAnsichtCommand`, `VergleichCommand`
- Refresh: `AktualisierenCommand`
- Dateiöffnung: `DateiMitStandardanwendungOeffnenCommand`
- Arbeitsverzeichnis öffnen: `OeffneArbeitsverzeichnisCommand` (NEU)
- IDE öffnen: `OeffneIdeCommand` (NEU)

**Sichtbarkeit (geplant):**
- Hauptbuttons (Standard/Vergleich/Refresh/Dateiöffnung): Gebunden an `ShowFileExplorerPanel` (nur wenn Dateibrowser aktiv)
- Neue Buttons (Arbeitsverzeichnis/IDE): Immer sichtbar (gemäß Anforderung Absatz 1)

#### Gruppe: "Systemaktionen" oder ähnlich (geplant, Optional)

Geplante Buttons (Alternative zu separater Gruppe):
- Arbeitsverzeichnis öffnen: `OeffneArbeitsverzeichnisCommand` (NEU)
- IDE öffnen: `OeffneIdeCommand` (NEU)

**Sichtbarkeit:** Immer sichtbar

### Ansicht-Toggle-Buttons (Grid.Row="2", StackPanel)

**Bestehende Toggle-Buttons:** Info, CLI (wenn ShowCliPanel), Diff (wenn ShowDiffPanel), Dateien (wenn ShowFileExplorerPanel)

Diese Buttons sind redundant zu den Ribbon-Gruppen-Aktionen. Mit der neuen Ribbon-Integration können diese möglicherweise vereinfacht werden.

### Fehlerbereich (Grid.Row="1")

Zeigt `FehlerMeldung` wenn gesetzt. Wird für Fehler bei neuen Service-Aufrufen verwendet.

### Statusleiste (Grid.Row="3")

Zeigt `AufgabeStatus`, `CliStatusText`, `AktiverCliName`. Kann für Feedback bei Arbeitsverzeichnis-/IDE-Öffnung erweitert werden.

---

## `DateibrowserAnsichtsmodus` (Enum)

Datei: `src/Softwareschmiede.App/ViewModels/DateibrowserAnsichtsmodus.cs`

**Zusammenfassung:** Anzeigemodus des Dateiexplorers in der Aufgabendetailansicht.

| Wert | Bedeutung |
|---|---|
| `Standard` | Zeigt den vollständigen Arbeitsbaum des geklonten Repositories |
| `Vergleich` | Zeigt nur im Branch geänderte Dateien, gruppiert nach Commits |

**Verwendung:** In `FileExplorerViewModel.AktuellerModus`

---

## Fehlende Enums

### IDE-Typ-Enum (geplant, optional)

**Status:** NICHT VORHANDEN

**Geplante Werte (falls mehrere IDEs unterstützt werden sollen):**
```csharp
public enum IdeType
{
    VisualStudio,
    VisualStudioCode,
    JetBrainsRider,
    // etc.
}
```

**Anforderungs-Hinweis:** "Ist nur Visual Studio gewünscht, oder sollen auch VS Code und andere IDEs unterstützt werden?" (Offene Frage in Anforderung)

---

## UI-Controls und Custom Controls

### `RibbonGroup` (Custom Control)

Datei: Vermutlich `src/Softwareschmiede.App/Controls/RibbonGroup.cs` oder ähnlich

**Verwendung:** In `TaskDetailView.xaml` zum Gruppieren von Ribbon-Buttons

**Properties:**
- `GruppenName` (String) - Name der Gruppe (z. B. "Navigation", "CLI")
- `ItemsContent` (StackPanel oder ähnlich) - Container für Buttons

**Sichtbarkeit:** Wird über `Visibility` Binding gesteuert (z. B. `Visibility="{Binding ShowCliPanel, Converter={StaticResource BoolToVisibilityConverter}}"`)

---

### `RibbonLargeButton` (Custom Control)

**Verwendung:** In `TaskDetailView.xaml` für Ribbon-Buttons

**Wichtige Properties:**
- `ButtonIcon` (String/Emoji) - Icon für den Button (z. B. "←", "💾", "🔌")
- `ButtonText` (String) - Text-Label
- `AutomationName` (String) - Name für UI-Automation / Testing
- `ButtonCommand` (ICommand) - Gebundener Command
- `Visibility` (Visibility, optional) - Steuert ob Button sichtbar ist

---

## Zusammenfassung für Ribbon-Integration

**Bestehende Infrastruktur:**
- Ribbon-Struktur mit `RibbonGroup` und `RibbonLargeButton` Controls vorhanden
- Sichtbarkeits-Binding-Muster etabliert (via BoolToVisibilityConverter)
- Commands aus FileExplorerViewModel sind einsatzbereit
- TaskDetailViewModel Properties für Panel-Sichtbarkeit bereits vorhanden

**Erforderliche Ergänzungen:**
1. Neue Ribbon-Gruppe "Dateien" hinzufügen
2. Neue Commands in TaskDetailViewModel: `OeffneArbeitsverzeichnisCommand`, `OeffneIdeCommand`
3. Neue Properties in TaskDetailViewModel: `ShowFileSystemGroup` (optional), `SolutionFileExists`
4. Buttons in Ribbon-Markup einbinden mit Bindings zu neuen Commands
5. Optional: IDE-Typ-Enum falls mehrere IDEs unterstützt werden sollen
