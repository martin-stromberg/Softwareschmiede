# UI-Komponenten

## `TaskDetailView.xaml`
Datei: `src/Softwareschmiede.App/Views/TaskDetailView.xaml`

WPF-View für die Aufgabendetailansicht mit dem Ribbon-UI.

### Aktuelle IDE-Button-Platzierung

**Zeile 180–183:**
```xaml
<controls:RibbonLargeButton ButtonIcon="🛠"
                            ButtonText="IDE öffnen"
                            AutomationName="IdeOeffnen"
                            ButtonCommand="{Binding OeffneIdeCommand}" />
```

**Ribbon-Gruppe:** "Werkzeuge" (Zeile 173)

**Aktuelle Sichtbarkeit:** Immer sichtbar wenn `KannIdeOeffnen == true`

**Verhalten bei Klick:**
- Führt das Kommando `OeffneIdeCommand` aus.
- Ruft `TaskDetailViewModel.OeffneIdeAsync` auf.
- Bei mehreren Einstiegspunkten zeigt `IdeOeffnenService.OpenRepositoryInIdeAsync` den Auswahlcallback, der `IDialogService.ShowSolutionSelectionDialogAsync` aufruft.

---

## `RibbonLargeButton.xaml` / `RibbonLargeButton.xaml.cs`
Datei: `src/Softwareschmiede.App/Controls/RibbonLargeButton.xaml` und `.cs`

Eine wiederverwendbare Ribbon-Button-Komponente.

### Eigenschaften

- **ButtonIcon** — Der Emoji/Icon für den Button (z. B. "🛠")
- **ButtonText** — Der Beschriftungstext unter dem Icon
- **AutomationName** — Name für UI-Automatisierungstests
- **ButtonCommand** — Das zu triggernde `ICommand`

### Visualisierung

- Width: 56px, Height: 68px
- Icon (TextBlock, FontSize 24) oben
- Beschriftungstext (TextBlock, FontSize 10) unten
- Hover: Hintergrundfläche wird hell
- Pressed: Hintergrund bekommt Akzentfarbe
- Disabled: Opacity 0.4

### Notizen

- Basiert auf dem `RibbonButtonBase` (Custom Control).
- Ist ein Single-Purpose-Button; es gibt keine Split-Button-Variante.
- Die gesamte Komponente ist auf einen einzelnen Button begrenzt.

