# UI-Komponenten

## `TaskDetailView.xaml`
Datei: `src/Softwareschmiede.App/Views/TaskDetailView.xaml`

XAML-Ansicht für die Aufgabendetailansicht. Enthält das Ribbon-Menü, in dem die Promptvorlage-Auswahlbox (ComboBox) für die `PromptVorlage` integriert ist.

### Betroffene UI-Elemente im Ribbon:

**Promptvorlage-Bereich** (Zeilen 74–87):
```xaml
<StackPanel Margin="12,0,0,0" VerticalAlignment="Center">
    <TextBlock Text="Promptvorlage" ... />
    <ComboBox ItemsSource="{Binding PromptVorlagen}"
              SelectedItem="{Binding SelectedPromptVorlage, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}"
              DisplayMemberPath="Name"
              Width="220"
              IsEnabled="{Binding KannPromptVorlageSenden}" />
</StackPanel>
```

**Bindings:**
- `PromptVorlagen` – ObservableCollection aus `TaskDetailViewModel`
- `SelectedPromptVorlage` – Ausgewählte Vorlage, triggert `PromptVorlageAuswaehlenCommand` bei Änderung
- `KannPromptVorlageSenden` – Boolean Property, bestimmt Enabled-Zustand der ComboBox

### Nicht vorhanden (noch zu implementieren):
- Textbox-Eingabefelder für Stunde und Minute (Zielausführungszeit)
- Button zum Absenden mit Zeitangabe
- Statusanzeige für geplante Prompts („Prompt in Wartestellung")

---

## `TaskDetailView.xaml.cs`
Datei: `src/Softwareschmiede.App/Views/TaskDetailView.xaml.cs`

Code-Behind für `TaskDetailView`. Keine relevanten Details für zeitgesteuerten Versand dokumentiert.

