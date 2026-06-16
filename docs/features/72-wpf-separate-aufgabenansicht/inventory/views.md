# UI-Views

## `ProjectDetailView.xaml` / `ProjectDetailView.xaml.cs`
Datei: `src/Softwareschmiede.App/Views/ProjectDetailView.xaml` und `.xaml.cs`

### Struktur

**XAML-Zeilen:**
- Zeile 1-6: UserControl-Definition
- Zeile 7-11: Ribbon-Menü (Border mit Gruppen)
  - Zeile 22-29: Navigation-Gruppe (Zurück-Button)
  - Zeile 31-45: Projekt-Gruppe (Speichern, Löschen)
  - Zeile 47-63: Aufgaben-Gruppe (Neue Aufgabe, Filter)
  - Zeile 65-79: Repository-Gruppe (Zuweisen, Öffnen)
- Zeile 85-234: Hauptinhalt (ScrollViewer)
  - Zeile 91-93: Loading-Anzeige
  - Zeile 95-122: Filter-Overlay (Visibility-Binding)
  - Zeile 124-169: Projekt-Kachel
  - Zeile 171-227: Aufgaben-Kachel (mit ListBox und ItemTemplate)
  - **Zeile 229-231: TaskDetailView inline (mit SelectedTaskViewModel Binding)**

### Code-behind: `ProjectDetailView.xaml.cs`

| Element | Beschreibung |
|---------|-------------|
| `AufgabeDoubleClick(sender, e)` Handler | Wird bei ListBoxItem MouseDoubleClick aufgelöst (Zeile 18-25) |
| Implementierung | Ruft `vm.AufgabeOeffnenCommand.Execute(aufgabe.Id)` auf |

### Aktuelle Aufgabenbehandlung

**Aufgabenliste (Zeile 194-225):**
```xaml
<ListBox ItemsSource="{Binding Aufgaben}" ...>
  <ListBox.ItemTemplate>
    <!-- Zeigt Titel und Status -->
  </ListBox.ItemTemplate>
  <ListBox.ItemContainerStyle>
    <EventSetter Event="MouseDoubleClick" Handler="AufgabeDoubleClick" />
  </ListBox.ItemContainerStyle>
</ListBox>
```

**Inline TaskDetailView (Zeile 229-231):**
```xaml
<views:TaskDetailView 
  DataContext="{Binding SelectedTaskViewModel}"
  Visibility="{Binding SelectedTaskViewModel, Converter={StaticResource NullOrEmptyToVisibilityConverter}}" />
```

**ÄNDERUNG erforderlich:** Diese Zeilen 229-231 müssen **entfernt** werden, da die TaskDetailView zukünftig in separater View angezeigt wird.

---

## `TaskDetailView.xaml` / `TaskDetailView.xaml.cs`
Datei: `src/Softwareschmiede.App/Views/TaskDetailView.xaml` und `.xaml.cs`

### Struktur

**XAML (bis Zeile 100):**
- Zeile 1-5: UserControl-Definition
- Zeile 6-12: Grid mit 4 Zeilen (Ribbon, Fehler, Inhalt, Footer)
- Zeile 14-81: Ribbon-Menü
  - Zeile 22-30: Navigation-Gruppe (Zurück-Button)
  - Zeile 32-54: Aufgabe-Gruppe (Speichern, Löschen, Starten, Beenden)
  - Zeile 56-78: CLI-Gruppe (Plugin-Dropdown, Start/Stop)
- Zeile 83-89: Fehlerbereich (mit FehlerMeldung Binding)
- Zeile 91-: Status-abhängiges Content-Switching
  - Edit-Panel (wenn Status == Neu)
  - CLI-Panel (wenn Status ∈ {Gestartet, InArbeit, Wartend})
  - Diff-Panel (wenn Status == Beendet)

### Status-abhängiges Content-Switching

Die TaskDetailView nutzt folgende Properties für Content-Switching:
- `ShowEditPanel` (Zeile 181 in ViewModel): True wenn Status == Neu
- `ShowCliPanel` (Zeile 184-186): True wenn Status ∈ {Gestartet, InArbeit, Wartend}
- `ShowDiffPanel` (Zeile 189): True wenn Status == Beendet

**Implementierung in XAML:**
```xaml
<ScrollViewer Visibility="{Binding ShowEditPanel, Converter={StaticResource BoolToVisibilityConverter}}">
  <!-- Edit-Panel -->
</ScrollViewer>

<!-- CLI-Panel -->
<ScrollViewer Visibility="{Binding ShowCliPanel, Converter={StaticResource BoolToVisibilityConverter}}">
  <!-- CLI-Inhalt -->
</ScrollViewer>

<!-- Diff-Panel -->
<ScrollViewer Visibility="{Binding ShowDiffPanel, Converter={StaticResource BoolToVisibilityConverter}}">
  <!-- Diff-Inhalt -->
</ScrollViewer>
```

### Code-behind: `TaskDetailView.xaml.cs`

Datei: `src/Softwareschmiede.App/Views/TaskDetailView.xaml.cs`

(CodeBehind ist minimal; die meiste Logik ist im ViewModel)

---

## Konsequenzen für Feature 72

### Derzeit (Inline-Modell):
1. ProjectDetailView zeigt Aufgabenliste
2. Doppelklick auf Aufgabe → ProjectDetailViewModel.AufgabeOeffnenCommand
3. OeffneAufgabe() erstellt TaskDetailViewModel und setzt SelectedTaskViewModel
4. TaskDetailView wird inline in ProjectDetailView gerendert (Zeile 229-231)
5. Speichern/Löschen erfolgt in TaskDetailView
6. Zurück-Navigation: vm.ZurueckAction = () => SelectedTaskViewModel = null

### Zukünftig (Separate View-Modell):
1. ProjectDetailView zeigt Aufgabenliste
2. Doppelklick auf Aufgabe → Navigation zu separater TaskDetailView
3. TaskDetailView wird fensterumfassend angezeigt (nicht inline)
4. Speichern/Löschen erfolgt in TaskDetailView
5. Zurück-Navigation: kehrt zu ProjectDetailView zurück
6. **Erforderliche Änderungen:**
   - Entfernen von Zeilen 229-231 in ProjectDetailView.xaml (inline TaskDetailView)
   - Implementierung eines Navigation-Service oder RootViewModel
   - Anpassung der Container-View (MainWindow.xaml oder neue AppContainerView)
   - Binding von ProjectDetailViewModel auf Navigation-Service für Aufgaben-Navigation
