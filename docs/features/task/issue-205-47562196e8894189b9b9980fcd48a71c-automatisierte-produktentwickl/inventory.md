# Bestandsaufnahme: UI-Integration Autonomer Aufgaben

Diese Bestandsaufnahme dokumentiert die zum Zeitpunkt der Anforderungs-Analyse existierenden Komponenten, ViewModels, Services und Tests für die UI-Integration des Autonome-Aufgaben-Features in die `TaskDetailView`. Die Anforderung sieht vor, den bisherigen separaten Dialog (`AutonomAufgabeDetailDialog`) in eine neue Registerkarte "Automatisierung" innerhalb der `TaskDetailView` zu integrieren und die Start/Stop/Resume-Buttons vom Dialog-Fenster in das Ribbon-Menü der Detailansicht zu migrieren.

## Zusammenfassung

### Existiert bereits

- **Registerkarten-Navigation:** `TaskDetailView` hat bereits ein etabliertes System von Ansicht-Buttons (Info, CLI, Diff, Dateien, PR, Todos) mit dahinter liegendem `DetailAnsicht`-Enum in `TaskDetailViewModel`
- **Ribbon-Menü-Struktur:** `TaskDetailView` hat ein funktionierendes Ribbon mit mehreren Gruppen (Navigation, Aufgabe, CLI, Dateien, Werkzeuge, Autonome Aufgabe, Issue, PR)
- **Gruppe "Autonome Aufgabe":** Existiert bereits (Zeile 190–200 in TaskDetailView.xaml), enthält derzeit nur Button "Autonome Aufgabe starten"
- **AutonomAufgabeDetailView & Dialog:** UserControl für Autonome-Aufgaben-Detail-Ansicht existiert, wird aktuell in eigenem Dialog-Fenster (`AutonomAufgabeDetailDialog`) angezeigt
- **AutonomAufgabeDetailViewModel:** Vollständig implementiert mit Start/Stop/Resume-Commands
- **AutonomAufgabeStartService:** Orchestriert Initialisierung, ruft aktuell `_dialogService.ShowAutonomAufgabeDetailAsync()` auf (Zeile 59)
- **Tests:** Umfangreiche Unit- und E2E-Tests für TaskDetailViewModel, AutonomAufgabeDetailViewModel, AutonomAufgabeStartService, E2E-Tests für Initialisierung und Agenten-Ausführung

### Fehlt / Muss erweitert werden

- **Enum-Wert:** `DetailAnsicht` braucht neuen Wert `Automatisierung`
- **Properties:** TaskDetailViewModel braucht:
  - `IsAutomatisierungViewSelected` (bool)
  - `ShowAutomatisierungPanel` (bool) — sichtbar, wenn Autonome Aufgabe existiert
  - `AutonomAufgabeDetailViewModel?` — Property zur Verwaltung des Detail-ViewModels
- **Command:** TaskDetailViewModel braucht `AutomatisierungViewCommand` zur Ansicht-Umschaltung
- **XAML-Container:** TaskDetailView braucht neuen ScrollViewer/Grid-Container für Automatisierung-Ansicht mit eingebettetem `AutonomAufgabeDetailView`
- **Ribbon-Buttons:** Gruppe "Autonome Aufgabe" braucht Start/Stop/Resume-Buttons (sichtbar wenn `ShowAutomatisierungPanel`)
- **Ansicht-Button:** Neuer Button "Automatisierung" in der Ansicht-Button-Reihe (StackPanel, Zeile 264+)
- **Service-Integration:** AutonomAufgabeStartService muss angepasst werden, um TaskDetailViewModel zu benachrichtigen statt Dialog zu öffnen
- **Tests:** Neue Unit- und E2E-Tests für Ansicht-Integration und Command-Bindings

---

## Details

### Datenmodelle & Enums

- [Enums](inventory/enums.md)
  - `DetailAnsicht` (Info, Cli, Diff, Dateibrowser, PullRequests, Todos) — **braucht neuen Wert `Automatisierung`**
- [Models](inventory/models.md)
  - `AutonomAufgabeKonfiguration` — wird nicht verändert
  - `Aufgabe` — wird nicht verändert (prüft nur `IstAutonom()`)
  - `UnteragentSpezifikation`, `SkillDefinition` — werden nicht verändert

### ViewModels

- [ViewModels](inventory/viewmodels.md)
  - `TaskDetailViewModel` — **Muss erweitert werden:** Neuer Enum-Wert, neue Properties, neuer Command
  - `AutonomAufgabeDetailViewModel` — Wird nicht verändert, nur eingebettet

### Views (XAML)

- [Views](inventory/views.md)
  - `TaskDetailView.xaml` — **Muss erweitert werden:** Neue Registerkarte, neue Ribbon-Buttons, Ansicht-Button
  - `AutonomAufgabeDetailView.xaml` — Wird nicht verändert (kann als Registerkarte eingebettet werden)
  - `AutonomAufgabeDetailDialog.xaml(.cs)` — Wird nach Integration nicht mehr Standardweg, kann als Fallback erhalten bleiben oder entfernt werden

### Logik & Services

- [Logic](inventory/logic.md)
  - `AutonomAufgabeStartService` — **Muss angepasst werden:** Statt `ShowAutonomAufgabeDetailAsync()` muss eine neue Mechanismus TaskDetailViewModel benachrichtigen
  - `IDialogService` — Methode `ShowAutonomAufgabeDetailAsync()` kann deprecated werden (oder für Fallback erhalten)
  - `WpfDialogService` — Implementierung von `ShowAutonomAufgabeDetailAsync()` kann deprecated werden

### Tests

- [Tests](inventory/tests.md)
  - Bestehende Tests für TaskDetailViewModel, AutonomAufgabeDetailViewModel, Services, E2E
  - **Zu erweitern:** Tests für neue Automatisierung-Ansicht, Ribbon-Commands, Ansicht-Umschaltung

---

## Kritische Integration-Punkte

### 1. TaskDetailViewModel-Erweiterung (TaskDetailViewModel.cs)

**Zeile 26–34:** DetailAnsicht-Enum erweitern um `Automatisierung`

```csharp
private enum DetailAnsicht
{
    Info,
    Cli,
    Diff,
    Dateibrowser,
    PullRequests,
    Todos,
    Automatisierung  // <- neu
}
```

**Nach Zeile 72:** Neue Feld-Initialisierungen

```csharp
private AutonomAufgabeDetailViewModel? _autonomAufgabeDetailViewModel;
```

**Nach den bestehenden `IsXxxViewSelected`-Properties (Zeilen 343–359):** Neue Properties

```csharp
public bool IsAutomatisierungViewSelected => _ausgewaehlteAnsicht == DetailAnsicht.Automatisierung;
public bool ShowAutomatisierungPanel => /* abhängig von Aufgabe.IstAutonom() oder Konfiguration */
public AutonomAufgabeDetailViewModel? AutonomAufgabeDetailViewModel => _autonomAufgabeDetailViewModel;
```

**Nach bestehenden Ansicht-Commands (Zeile 526):** Neuer Command

```csharp
public ICommand AutomatisierungViewCommand { get; }
```

**Im Konstruktor (Zeile 639–658):** Command initialisieren

```csharp
AutomatisierungViewCommand = new RelayCommand(() => WaehleAnsicht(DetailAnsicht.Automatisierung), () => ShowAutomatisierungPanel);
```

**Neue Methode:** ViewModel setzen (wird von AutonomAufgabeStartService aufgerufen)

```csharp
public async Task SetzeAutonomAufgabeDetailViewAsync(AutonomAufgabeDetailViewModel vm)
{
    _autonomAufgabeDetailViewModel = vm;
    OnPropertyChanged(nameof(AutonomAufgabeDetailViewModel));
    OnPropertyChanged(nameof(ShowAutomatisierungPanel));
    WaehleAnsicht(DetailAnsicht.Automatisierung);
}
```

**In `WaehleAnsicht()`:** Validierung erweitern

```csharp
if (ansicht == DetailAnsicht.Automatisierung && !ShowAutomatisierungPanel)
    ansicht = DetailAnsicht.Info;
```

### 2. TaskDetailView-Erweiterung (TaskDetailView.xaml)

**Zeilen 190–200, Gruppe "Autonome Aufgabe":** Start/Stop/Resume-Buttons hinzufügen

```xaml
<controls:RibbonGroup GruppenName="Autonome Aufgabe">
    <controls:RibbonGroup.ItemsContent>
        <StackPanel Orientation="Horizontal">
            <controls:RibbonLargeButton ButtonIcon="🤖"
                                        ButtonText="Autonome Aufgabe starten"
                                        AutomationName="AutonomAufgabeInitialisieren"
                                        ButtonCommand="{Binding AutonomAufgabeInitialisierenCommand}" />
            <controls:RibbonLargeButton ButtonIcon="▶"
                                        ButtonText="Start"
                                        AutomationName="AutonomAufgabeStartAgent"
                                        ButtonCommand="{Binding AutonomAufgabeDetailViewModel.StartCommand}"
                                        Visibility="{Binding ShowAutomatisierungPanel, Converter={StaticResource BoolToVisibilityConverter}}" />
            <controls:RibbonLargeButton ButtonIcon="■"
                                        ButtonText="Stop"
                                        AutomationName="AutonomAufgabeStopAgent"
                                        ButtonCommand="{Binding AutonomAufgabeDetailViewModel.StopCommand}"
                                        Visibility="{Binding ShowAutomatisierungPanel, Converter={StaticResource BoolToVisibilityConverter}}" />
            <controls:RibbonLargeButton ButtonIcon="▶"
                                        ButtonText="Resume"
                                        AutomationName="AutonomAufgabeResumeAgent"
                                        ButtonCommand="{Binding AutonomAufgabeDetailViewModel.ResumeCommand}"
                                        Visibility="{Binding ShowAutomatisierungPanel, Converter={StaticResource BoolToVisibilityConverter}}" />
        </StackPanel>
    </controls:RibbonGroup.ItemsContent>
</controls:RibbonGroup>
```

**Zeilen 273–311, Ansicht-Buttons:** "Automatisierung"-Button hinzufügen

```xaml
<Button Content="Automatisierung"
        AutomationProperties.Name="AutomatisierungViewButton"
        Command="{Binding AutomatisierungViewCommand}"
        Padding="12,4"
        Margin="6,0,0,0"
        FontSize="12"
        Visibility="{Binding ShowAutomatisierungPanel, Converter={StaticResource BoolToVisibilityConverter}}" />
```

**Nach Zeile 582, vor Schließtag des Grids (Grid.Row="1"):** Neuer Container für Automatisierung-Ansicht

```xaml
<StackPanel Visibility="{Binding IsAutomatisierungViewSelected, Converter={StaticResource BoolToVisibilityConverter}}">
    <views:AutonomAufgabeDetailView DataContext="{Binding AutonomAufgabeDetailViewModel}" />
</StackPanel>
```

### 3. AutonomAufgabeStartService-Anpassung (AutonomAufgabeStartService.cs)

**Zeile 59:** Statt Dialog-Aufruf neuen Aufruf durchführen

**Option A:** Direkter Aufruf auf TaskDetailViewModel (muss TaskDetailViewModel als Parameter haben)

```csharp
// Statt: await _dialogService.ShowAutonomAufgabeDetailAsync(detailVm, ct);
await _taskDetailViewModel.SetzeAutonomAufgabeDetailViewAsync(detailVm);
```

**Option B:** Event/Callback-Pattern

```csharp
// Statt: await _dialogService.ShowAutonomAufgabeDetailAsync(detailVm, ct);
// Neuen Event triggern, auf dem TaskDetailViewModel abonniert
AutonomAufgabeZeigeAnAsync?.Invoke(detailVm);
```

**Option C:** Neuer Service als Koordinator

```csharp
// Neuer Service, der von beiden (AutonomAufgabeStartService und TaskDetailViewModel) verwendet wird
await _autonomAufgabenIntegrationService.ZeigeDetailAsync(detailVm, _aufgabeId, ct);
```

---

## Änderungs-Übersicht (Zusammengefasst)

| Datei | Änderung-Typ | Bemerkung |
|-------|-------------|----------|
| `TaskDetailViewModel.cs` | Erweitern | Enum-Wert, Properties, Command |
| `TaskDetailView.xaml` | Erweitern | Ribbon-Buttons, Ansicht-Button, Container |
| `AutonomAufgabeStartService.cs` | Anpassen | Statt Dialog zu öffnen, TaskDetailViewModel informieren |
| `IDialogService.cs` | Optional | `ShowAutonomAufgabeDetailAsync()` deprecated oder entfernen |
| `WpfDialogService.cs` | Optional | `ShowAutonomAufgabeDetailAsync()` deprecated oder entfernen |
| `AutonomAufgabeDetailView.xaml` | Keine | Kann unverändert eingebettet werden |
| `AutonomAufgabeDetailDialog.xaml(.cs)` | Optional | Kann als Fallback erhalten oder entfernt werden |
| `TaskDetailViewModelTests*.cs` | Erweitern | Tests für neue Enum-Wert, Properties, Commands |
| `E2E_Autonome*.cs` | Erweitern | UI-Tests für neue Registerkarte und Ribbon-Buttons |

---

## Hinweise

- **Margin-Anpassung:** AutonomAufgabeDetailView hat `Margin="24"`. Bei Einbettung als Registerkarte muss geprüft werden, ob dies noch passt oder angepasst werden muss.
- **Spacing:** Das Padding/Margin-Layout der eingebetteten View sollte mit den anderen Registerkarten-Inhalten konsistent sein.
- **Dependency Injection:** TaskDetailViewModel muss entweder AutonomAufgabeDetailViewModel direkt per Property halten oder via Callback/Event vom Service benachrichtigt werden.
- **Fallback-Dialog:** Es ist unklar, ob `AutonomAufgabeDetailDialog` und `ShowAutonomAufgabeDetailAsync()` nach der Integration entfernt oder für Fallback-Szenarien erhalten bleiben sollen (Offene Frage #3 in requirement.md).
