# Umsetzungsplan: Dialog für Repository-Zuweisung mit SCM-Plugin-Auswahl

## Übersicht

Der Dialog zur Repository-Zuweisung wird um eine explizite Auswahl des SCM-Plugins erweitert. Nach Plugin-Auswahl werden nur Repositories dieser Quelle angezeigt. Falls keine SCM-Plugins vorhanden sind, wird statt der Eingabekomponenten ein Hilfe-Panel mit Instruktionen angezeigt. Zusätzlich wird ein Sichtbarkeitsproblem des „Zuweisen"-Buttons im Dark-Mode behoben (hardcodiertes weiß durch Theme-Binding ersetzen).

**Umfang:**
- ViewModel-Erweiterung mit 3 neuen Properties für Plugin-Verwaltung
- XAML-UI-Erweiterung: ComboBox für Plugin-Selector, Hilfe-Panel bei fehlenden Plugins
- Dark-Mode Button-Fix (1 Zeile XAML ändern)
- Neue Unit-Tests für RepositoryAssignViewModel
- Keine Datenbankmigrationen erforderlich
- Keine Konfigurationsänderungen erforderlich

**Betroffene Komponenten:**
- `RepositoryAssignViewModel` (ViewModel)
- `RepositoryAssignDialog.xaml` + `.xaml.cs` (Dialog-View)
- `RepositoryAssignViewModelTests` (Tests — neu oder erweitert)

---

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Alternative | Begründung |
|---|---|---|---|
| Plugin-Auswahl UI | ComboBox im XAML-Markup | Custom Dropdown mit Styling, Separate User Control | ComboBox ist WPF-Standard, minimale Änderungen, direkt bindbar |
| Standard-Plugin-Auswahl | Keine automatische Auswahl — Benutzer wählt aktiv | Erstes Plugin auto-selektieren | Explizite Auswahl vermeidet unerwartete Behavior; konsistent mit Principle of Least Surprise |
| Hilfe-Panel Visibility | `InverseBoolToVisibilityConverter` + zwei Elemente (ListBox und HilfePanel) in Grid.Row 2 | Separate visuelle States, Code-Behind Toggle, Data Template Selector | Declarative Binding ist wartbar, testbar und folgt MVVM-Pattern |
| Button-Foreground Theme-Binding | `DynamicResource PrimaryTextBrush` | `ContrastTextBrush`, `HighContrastForeground`, Hardcoded weiß beibehalten | `PrimaryTextBrush` ist Standard im Projekt-Theme (Light & Dark), kontrastreich |
| Plugin-Filterung Repositories | Nach `IGitPlugin.PluginType` (String-Vergleich mit `GitRepository.PluginTyp`) | Type-Safe Enum, separate Lookup-Methode | String-Vergleich ist konsistent mit bestehender `GitRepository`-Struktur |
| Repository-Reload-Trigger | PropertyChanged-Event auf `SelectedScmPlugin`, Asynch.-Aufruf von `ReloadRepositoriesForSelectedPlugin()` | Expliziter Command, Separate Event-Handler | Event-basiert ist Binding-freundlich und reaktiv; Fire-and-Forget mit Fehlerlogging |
| Async-Error-Handling | Try-Catch in `ReloadRepositoriesForSelectedPlugin()`, Logger-Eintrag | Exception propagieren, Silent-Fail | Fehlerbehandlung lokal, Logger dokumentiert Problem, UI bleibt responsive |

---

## Programmabläufe

### Ablauf 1: Dialog-Öffnung und Plugin-Liste laden

1. `RepositoryAssignDialog` wird via `IDialogService.RepositoryZuweisenDialog()` oder direktem Aufruf geöffnet
2. Dialog setzt `DataContext` auf `RepositoryAssignViewModel` Instanz
3. ViewModel-Konstruktor wird aufgerufen (mit injiziertem `IPluginManager`)
4. Dialog-CodeBehind oder Binding triggert `LadenAsync()` (z.B. im `Loaded`-Event oder View-Initialization)
5. `LadenAsync()` ruft `_pluginManager.GetSourceCodeManagementPlugins()` auf
6. Ergebnis wird in `AvailableScmPlugins` ObservableCollection eingefügt
7. `HasScmPlugins` wird basierend auf Listengröße gesetzt: `Count > 0` → `true`, sonst `false`
8. `IsLoading`-Flag wird zurückgesetzt
9. UI-Binding stellt Plugins im ComboBox dar oder zeigt Hilfe-Panel (je nach `HasScmPlugins`)

**Beteiligte Klassen/Komponenten:** `RepositoryAssignViewModel`, `IPluginManager`, `IGitPlugin`, `RepositoryAssignDialog`

### Ablauf 2: Plugin-Auswahl und Repository-Filterung

1. Benutzer wählt SCM-Plugin aus ComboBox aus
2. `SelectedScmPlugin` Property wird gesetzt (2-Way Binding)
3. Property-Setter triggert `ReloadRepositoriesForSelectedPlugin()` als asynchrone Task (Fire-and-Forget)
4. `IsLoading = true` wird gesetzt
5. `ProjektService.GetAllRepositoriesAsync()` wird aufgerufen (existierende Methode)
6. Repositories werden gefiltert: `.Where(r => r.PluginTyp == SelectedScmPlugin.PluginType.ToString())`
7. Gefilterte Liste wird sortiert (nach Name)
8. `VerfuegbareRepositories` wird mit gefilterten und sortierten Repositories befüllt
9. `SelectedRepository` wird auf `null` zurückgesetzt (für sauberen Zustand)
10. `IsLoading = false` wird gesetzt
11. PropertyChanged-Events werden ausgelöst → UI-Binding aktualisiert ListBox

**Beteiligte Klassen/Komponenten:** `RepositoryAssignViewModel`, `ProjektService`, `IGitPlugin`, `GitRepository` (PluginTyp-Property)

### Ablauf 3: Fehlerbehandlung bei fehlenden Plugins

1. `LadenAsync()` wird aufgerufen
2. `IPluginManager.GetSourceCodeManagementPlugins()` gibt leere Liste zurück
3. `AvailableScmPlugins` bleibt leer
4. `HasScmPlugins = false` wird gesetzt
5. UI-Binding aktualisiert Visibility:
   - ListBox, ComboBox, Buttons werden ausgeblendet (via `BoolToVisibilityConverter` auf `HasScmPlugins`)
   - Hilfe-Panel wird angezeigt (via `InverseBoolToVisibilityConverter` auf `HasScmPlugins`)
6. Benutzer sieht Hilfetext: „Keine SCM-Plugins installiert. Um Repositories zuzuweisen, installieren Sie bitte ein SCM-Plugin (z.B. GitHub Plugin)."

**Beteiligte Klassen/Komponenten:** `RepositoryAssignViewModel`, `IPluginManager`, `RepositoryAssignDialog.xaml` (Visibility-Binding)

### Ablauf 4: Bestätigung und Dialog-Schließung

1. (Nur wenn `HasScmPlugins == true` und Repository ausgewählt ist)
2. Benutzer klickt „Zuweisen"-Button
3. `BestaetigenCommand` wird ausgeführt (bestehende Logik)
4. Dialog setzt `DialogResult = true`
5. Dialog wird geschlossen
6. Rückgabewert wird an Aufrufer propagiert

**Beteiligte Klassen/Komponenten:** `RepositoryAssignDialog.xaml.cs`, `RepositoryAssignViewModel`

---

## Neue Klassen

Keine neuen Klassen erforderlich. Die Erweiterung erfolgt ausschließlich durch neue Properties und Events in bestehenden Klassen.

| Klasse | Beschreibung | Grund |
|---|---|---|
| *(keine)* | — | Plugin-Verwaltung nutzt bestehendes `IPluginManager` und `IGitPlugin`; keine neuen Datentypen erforderlich |

---

## Änderungen an bestehenden Klassen

### `RepositoryAssignViewModel`

Datei: `src/Softwareschmiede.App/ViewModels/RepositoryAssignViewModel.cs`

#### Neue Abhängigkeiten
- **`IPluginManager`** (private readonly Field `_pluginManager`) — zur Abfrage verfügbarer SCM-Plugins

#### Neue Properties

| Property | Typ | Zweck | Hinweise |
|---|---|---|---|
| `AvailableScmPlugins` | `ObservableCollection<IGitPlugin>` | Liste aller verfügbaren SCM-Plugins | Mit PropertyChanged-Event; wird in `LadenAsync()` befüllt |
| `SelectedScmPlugin` | `IGitPlugin?` (nullable) | Aktuell vom Benutzer gewähltes Plugin | Mit PropertyChanged-Event; 2-Way Binding zum ComboBox; triggert Repository-Reload bei Änderung |
| `HasScmPlugins` | `bool` | Indikator, ob SCM-Plugins vorhanden sind | Mit PropertyChanged-Event; wird basierend auf `AvailableScmPlugins.Count > 0` gesetzt; steuert Visibility von Hilfe-Panel |

#### Neue Methoden

| Methode | Signatur | Zweck | Beschreibung |
|---|---|---|---|
| `ReloadRepositoriesForSelectedPlugin` | `private async Task` | Lädt Repositories des ausgewählten Plugins | 1. Null-Check: Wenn `SelectedScmPlugin == null`, setze `VerfuegbareRepositories` leer 2. Rufe `ProjektService.GetAllRepositoriesAsync()` auf 3. Filtere: `.Where(r => r.PluginTyp == SelectedScmPlugin.PluginType.ToString())` 4. Sortiere nach Name 5. Setze `VerfuegbareRepositories` 6. Setze `SelectedRepository = null` 7. Fehlerbehandlung: Try-Catch, Logger.LogError, `VerfuegbareRepositories.Clear()` |

#### Geänderte Methoden

| Methode | Änderung | Begründung |
|---|---|---|
| Konstruktor | Zusätzlicher optionaler Parameter: `IPluginManager? pluginManager = null` oder Overload | `IPluginManager` wird via Dependency Injection injiziert; optionaler Parameter für Rückwärts-Kompatibilität |
| `LadenAsync(CancellationToken ct)` | Erweitert (nicht ersetzt) um Plugin-Laden | Nach bestehendem Loading: Rufe `_pluginManager.GetSourceCodeManagementPlugins()` auf, befülle `AvailableScmPlugins`, setze `HasScmPlugins` |

#### Neue Event-Handler

| Handler | Ausgelöst von | Aktion |
|---|---|---|
| PropertyChanged-Handler für `SelectedScmPlugin` | `SelectedScmPlugin` Property-Setter | Ruft `_ = ReloadRepositoriesForSelectedPlugin()` auf (Fire-and-Forget mit Task-Ignorieren und Fehlerlogging) |

#### Implementierungsdetails

**Konstruktor-Erweiterung:**
```
Bestehend: RepositoryAssignViewModel(ProjektService projektService, ...)
Neu:       RepositoryAssignViewModel(ProjektService projektService, IPluginManager? pluginManager = null, ...)
```
- `pluginManager` wird im privaten Field gespeichert
- Optionaler Parameter für Rückwärts-Kompatibilität (Default `null`; falls null, dann `AvailableScmPlugins` bleibt leer)
- Dependency Injection sollte automatisch `IPluginManager` bereitstellen

**Property `SelectedScmPlugin` Setter:**
```
PropertyChanged-Handler oder direkt im Property-Setter:
- Rufe `_ = ReloadRepositoriesForSelectedPlugin()` auf
- Task wird nicht awaitet (Fire-and-Forget)
- Fehler werden in `ReloadRepositoriesForSelectedPlugin()` abgefangen und geloggt
```

**Methode `ReloadRepositoriesForSelectedPlugin()`:**
```
Pseudocode:
1. if (_pluginManager == null || SelectedScmPlugin == null) 
   { VerfuegbareRepositories.Clear(); return; }
2. try 
   { 
     IsLoading = true;
     var allRepos = await ProjektService.GetAllRepositoriesAsync();
     var filtered = allRepos
       .Where(r => r.PluginTyp == SelectedScmPlugin.PluginType.ToString())
       .OrderBy(r => r.Name)
       .ToList();
     VerfuegbareRepositories = new ObservableCollection<GitRepository>(filtered);
     SelectedRepository = null;
   }
3. catch (Exception ex)
   {
     Logger.LogError($"Fehler beim Laden der Repositories: {ex.Message}");
     VerfuegbareRepositories.Clear();
   }
4. finally 
   { IsLoading = false; }
```

---

### `RepositoryAssignDialog.xaml`

Datei: `src/Softwareschmiede.App/Views/RepositoryAssignDialog.xaml`

#### Dark-Mode Button-Fix (Zeile ~72)

| Change | Before | After | Grund |
|---|---|---|---|
| Button Foreground | `Foreground="White"` | `Foreground="{DynamicResource PrimaryTextBrush}"` | Theme-Binding für Dark-Mode Unterstützung; `PrimaryTextBrush` ist Standard-Text-Farbe für Light & Dark Theme |

#### Grid-Struktur erweitern

**Änderung:** `Grid.RowDefinitions` von 3 Rows auf 4 Rows erweitern

| Row | Element | Height | Zweck |
|---|---|---|---|
| 0 | Dialog Title (bestehend) | Auto | Bleibt unverändert |
| 1 | ComboBox für Plugin-Auswahl (neu) | Auto | Wähle SCM-Plugin aus |
| 2 | ListBox oder Hilfe-Panel (existierend + neu) | `*` (Star-Sizing) | Zeige Repositories (wenn Plugins vorhanden) oder Hilfe-Panel (wenn nicht) |
| 3 | Buttons (bestehend, verschieben) | Auto | OK/Cancel-Buttons; `Grid.Row` aktualisieren von 2 auf 3 |

#### Neue ComboBox für Plugin-Auswahl (Grid.Row="1")

**XAML-Struktur:**
```xaml
<ComboBox Grid.Row="1"
          ItemsSource="{Binding AvailableScmPlugins}"
          SelectedItem="{Binding SelectedScmPlugin, Mode=TwoWay}"
          DisplayMemberPath="PluginName"
          Background="{DynamicResource BackgroundBrush}"
          Foreground="{DynamicResource PrimaryTextBrush}"
          BorderBrush="{DynamicResource BorderBrush}"
          BorderThickness="1"
          Padding="8,6"
          Margin="0,0,0,12">
  <ComboBox.ItemContainerStyle>
    <Style TargetType="ComboBoxItem">
      <Setter Property="Foreground" Value="{DynamicResource PrimaryTextBrush}" />
      <Setter Property="Background" Value="{DynamicResource BackgroundBrush}" />
    </Style>
  </ComboBox.ItemContainerStyle>
</ComboBox>
```

**Binding-Details:**
- `ItemsSource` → `AvailableScmPlugins` ObservableCollection
- `SelectedItem` (2-Way) → `SelectedScmPlugin` Property
- `DisplayMemberPath="PluginName"` → Plugin-Name anzeigen
- Alle Farben via `DynamicResource` für Theme-Unterstützung
- Padding & Margin für visuellen Abstand

#### ListBox mit Visibility-Binding (Grid.Row="2", bestehend)

**Änderung:** Existierende ListBox mit Visibility-Binding ausstatten

```xaml
<ListBox Grid.Row="2"
         Visibility="{Binding HasScmPlugins, Converter={StaticResource BoolToVisibilityConverter}}"
         ItemsSource="{Binding VerfuegbareRepositories}"
         SelectedItem="{Binding SelectedRepository}"
         ... />
```

- Nur sichtbar, wenn `HasScmPlugins == true`
- Verwendet bestehenden `BoolToVisibilityConverter`

#### Hilfe-Panel (Grid.Row="2", neu)

**XAML-Struktur:**
```xaml
<StackPanel Grid.Row="2"
            Visibility="{Binding HasScmPlugins, Converter={StaticResource InverseBoolToVisibilityConverter}}"
            Background="{DynamicResource BackgroundBrush}"
            BorderBrush="{DynamicResource BorderBrush}"
            BorderThickness="1"
            Padding="16"
            VerticalAlignment="Center"
            HorizontalAlignment="Stretch">
  <TextBlock Text="Keine SCM-Plugins installiert"
             FontSize="14"
             FontWeight="SemiBold"
             Foreground="{DynamicResource PrimaryTextBrush}"
             Margin="0,0,0,8" />
  <TextBlock Text="Um Repositories zuzuweisen, installieren Sie bitte ein SCM-Plugin (z.B. GitHub Plugin). Weitere Informationen finden Sie in der Dokumentation."
             FontSize="12"
             Foreground="{DynamicResource SecondaryTextBrush}"
             TextWrapping="Wrap"
             LineHeight="18" />
</StackPanel>
```

**Details:**
- Nur sichtbar, wenn `HasScmPlugins == false` (via `InverseBoolToVisibilityConverter`)
- Zwei TextBlocks: Titel + Beschreibung
- Farben via `DynamicResource` für Theme-Unterstützung
- Zentriert im verfügbaren Platz

#### Button-Update (Grid.Row="3", bestehend, verschieben)

**Änderung:** `Grid.Row` von 2 auf 3 aktualisieren für alle Button-Elemente

```xaml
<StackPanel Grid.Row="3" Orientation="Horizontal" HorizontalAlignment="Right">
  <!-- Buttons -->
</StackPanel>
```

#### Optionale Verbesserung: Button-State bei fehlenden Plugins

**Zusätzlich (optional):** `IsEnabled` auf `BestaetigenCommand` oder Button basierend auf `HasScmPlugins`

```xaml
<Button Content="Zuweisen" 
        IsEnabled="{Binding HasScmPlugins}"
        Command="{Binding BestaetigenCommand}"
        ... />
```

Begründung: Verhindert Bestätigung ohne Plugin-Auswahl; verbessertes UX-Feedback.

---

### `RepositoryAssignDialog.xaml.cs`

Datei: `src/Softwareschmiede.App/Views/RepositoryAssignDialog.xaml.cs`

**Keine Änderungen erforderlich.** Das bestehende Code-Behind ist bereits für die neuen Properties vorbereitet (Data Binding ist deklarativ in XAML); kein manueller Event-Handling nötig.

---

## Datenbankmigrationen

Keine Datenbankmigrationen erforderlich.

| Grund | Erklärung |
|---|---|
| Keine neue Entities | Keine neuen Datentypen oder Tabellen erforderlich |
| Keine neuen Felder | Repositories verwenden bereits `PluginTyp` Property; keine Schemaänderungen |
| Keine Konfigurationstabellen | Plugin-Verfügbarkeit ist zur Laufzeit dynamisch via `IPluginManager` |

---

## Validierungsregeln

| Feld / Objekt | Regel | Prüfung | Aktion bei Verletzung |
|---|---|---|---|
| `AvailableScmPlugins` (nach LadenAsync) | Mindestens 1 Plugin erforderlich für normale Funktion | `AvailableScmPlugins.Count > 0` → `HasScmPlugins = true`, sonst `false` | `HasScmPlugins = false`, Hilfe-Panel anzeigen, Dialog-Eingabe deaktivieren |
| `SelectedScmPlugin` | Muss gesetzt sein, bevor Repository-Filter angewendet wird | `SelectedScmPlugin != null` vor Repository-Abruf | Wenn `null`: `VerfuegbareRepositories` bleibt leer |
| `SelectedRepository` (vor Bestätigung) | Muss eine gültige Repository-Instanz sein | `SelectedRepository != null` | `BestaetigenCommand.CanExecute` bleibt `false` |
| `GitRepository.PluginTyp` (beim Filtern) | Muss mit `SelectedScmPlugin.PluginType.ToString()` exakt übereinstimmen | String-Vergleich Case-Sensitiv: `r.PluginTyp == SelectedScmPlugin.PluginType.ToString()` | Nicht passende Repositories werden gefiltert (nicht angezeigt) |
| Plugin-Typ-Format | String-Format muss standardisiert sein | Vergleich erfolgt auf String-Ebene; Format muss konsistent sein | Keine Regex-Validierung; einfacher String-Vergleich |

---

## Konfigurationsänderungen

Keine Konfigurationsänderungen erforderlich.

| Grund | Erklärung |
|---|---|
| Dynamische Laufzeit-Ermittlung | Plugin-Verfügbarkeit wird über `IPluginManager.GetSourceCodeManagementPlugins()` zur Laufzeit ermittelt |
| Keine Filterung über Config | Anforderung spezifiziert keine Konfiguration für Plugin-Filterung |
| Bestehende DI-Registration | `IPluginManager` ist bereits im DI-Container registriert (aus bestehender Infrastruktur) |

**Überprüfung erforderlich:** DI-Container in `Program.cs` oder Startup-Konfiguration, um sicherzustellen, dass `IPluginManager` verfügbar ist.

---

## Seiteneffekte und Risiken

### Positive Seiteneffekte
- **Dark-Mode Button-Fix:** „Zuweisen"-Button ist jetzt im Dark-Mode lesbar (Foreground-Binding statt hardcodiert weiß)
- **Explizite Plugin-Auswahl:** Reduziert Verwirrung bei mehreren SCM-Plugins; Repositories sind gefiltert und übersichtlicher
- **Fehlerbehandlung verbessert:** Hilfe-Panel informiert Benutzer über fehlende Plugins; bessere UX
- **Responsivität:** Repository-Liste ist dynamisch; schnelle Filterung nach Plugin-Wechsel

### Risiken und Mitigationen

| Risiko | Beschreibung | Mitigation |
|---|---|---|
| **Plugin-Typ-Vergleich Case-Sensitivity** | `GitRepository.PluginTyp` (String) mit `IGitPlugin.PluginType.ToString()` (Enum-String) — Format-Mismatch möglich | Unit-Tests für Plugin-Filter-Logik schreiben; Vergleich über `.Equals(StringComparison.OrdinalIgnoreCase)` erwägen, wenn unterschiedliche Cases vorhanden sind |
| **Performance bei vielen Repositories** | `ReloadRepositoriesForSelectedPlugin()` lädt alle Repos und filtert dann (nicht optimal für >10k Repos) | Akzeptabel für typische Szenarien (<1000 Repos); ggf. in Zukunft Service-Side Filtering implementieren |
| **Race Condition in Async-Reload** | Benutzer wechselt Plugin schnell mehrfach; mehrere Reload-Tasks laufen parallel | `IsLoading`-Flag bietet UI-Feedback; Alte Results werden durch neue Binding überschrieben; Akzeptabel (kein Data-Loss) |
| **UI-Flickering bei Plugin-Wechsel** | ListBox wird geleert und neu gefüllt → kurzes Flackern | Gewünschtes Behavior (zeigt Ladevorgangsfeedback); `IsLoading`-Flag könnte Spinner/Skeleton zeigen (zukünftige Verbesserung) |
| **Null-Reference in SelectedScmPlugin-Event** | PropertyChanged-Handler könnte aufgerufen werden, bevor ViewModel vollständig initialisiert | Null-Check in `ReloadRepositoriesForSelectedPlugin()` am Anfang; `_pluginManager` kann null sein |
| **Dependency Injection fehlkonfiguriert** | `IPluginManager` nicht im DI-Container registriert → Runtime-Error | Dependency-Registrierung in Startup überprüfen; Unit-Tests mit Mocking validieren |
| **Backward Compatibility** | Neue Konstruktor-Parameter könnten bestehenden Code brechen, der `RepositoryAssignViewModel` instantiiert | Optionaler Parameter mit Standardwert verwenden; Oder neuer Konstruktor-Overload |
| **InverseBoolToVisibilityConverter nicht vorhanden** | XAML-Ressource fehlt → Runtime XAML-Parse-Error | Überprüfen, ob Converter in App.xaml oder RepositoryAssignDialog.xaml vorhanden ist; ggf. registrieren |

### Bestehende Features, die betroffen sind

- **`RepositoryAssignViewModel.LadenAsync()`:** Wird erweitert, bestehende Aufrufe bleiben kompatibel
- **`RepositoryAssignDialog`:** Neue visuelle Elemente, bestehende Buttons/Logik bleiben unverändert
- **`BestaetigenCommand`:** Kann um `IsEnabled`-Check erweitert werden (optional)
- **Existing UI Tests:** Ggf. müssen die Repository-Listen-Tests angepasst werden (Repositories sind nun gefiltert, nicht alle)

---

## Umsetzungsreihenfolge

### Phase 1: ViewModel-Grundstruktur

1. **Konstruktor-Erweiterung: `IPluginManager` Dependency Injection**
   - Voraussetzung: Keine (Basis)
   - Beschreibung: 
     - `IPluginManager` als optionalen Parameter hinzufügen: `IPluginManager? pluginManager = null`
     - Privates readonly Field `_pluginManager` definieren und speichern
     - Oder Konstruktor-Overload für alternative Signature ohne Breaking Change

2. **Properties definieren: `AvailableScmPlugins`, `SelectedScmPlugin`, `HasScmPlugins`**
   - Voraussetzung: Schritt 1
   - Beschreibung:
     - `AvailableScmPlugins`: `ObservableCollection<IGitPlugin>` mit PropertyChanged-Event (über `SetProperty`)
     - `SelectedScmPlugin`: `IGitPlugin?` (nullable) mit PropertyChanged-Event
     - `HasScmPlugins`: `bool` mit PropertyChanged-Event
     - Private Backing-Fields für alle
     - Initialisierung im Konstruktor

3. **PropertyChanged-Handler für `SelectedScmPlugin`**
   - Voraussetzung: Schritte 1, 2
   - Beschreibung:
     - Im Property-Setter oder Konstruktor einen Handler registrieren
     - Beim Ändern von `SelectedScmPlugin`: `_ = ReloadRepositoriesForSelectedPlugin()` aufrufen (Fire-and-Forget)
     - Task.Run mit try-catch alternativ zu direkt awaiten

### Phase 2: ViewModel-Methoden

4. **`LadenAsync()` erweitern: Plugin-List laden**
   - Voraussetzung: Schritte 1–3
   - Beschreibung:
     - Bestehendes `LadenAsync()` nicht ersetzen, sondern erweitern
     - Nach bestehendem Loading:
       - `_pluginManager?.GetSourceCodeManagementPlugins()` aufrufen (Null-safe operator für Rückwärts-Kompatibilität)
       - Ergebnis in `AvailableScmPlugins.Clear()` + `.AddRange()` befüllen
       - `HasScmPlugins = AvailableScmPlugins.Count > 0` setzen
     - Error-Handling: Try-Catch um Plugin-Abruf; Logger bei Exception

5. **`ReloadRepositoriesForSelectedPlugin()` Methode schreiben**
   - Voraussetzung: Schritte 1–4
   - Beschreibung:
     - Neue private async Task Methode (kein Return-Wert)
     - Logik:
       1. Null-Check: Wenn `_pluginManager == null` oder `SelectedScmPlugin == null` → `VerfuegbareRepositories.Clear()` return
       2. `IsLoading = true` setzen
       3. `ProjektService.GetAllRepositoriesAsync()` aufrufen
       4. Filtern: `.Where(r => r.PluginTyp == SelectedScmPlugin.PluginType.ToString())`
       5. Sortieren: `.OrderBy(r => r.Name)`
       6. In neue ObservableCollection konvertieren und `VerfuegbareRepositories` zuweisen
       7. `SelectedRepository = null` setzen
     - Error-Handling:
       - Try-Catch um gesamte Logik
       - Logger.LogError bei Exception
       - `VerfuegbareRepositories.Clear()` im Fehlerfall
     - Finally: `IsLoading = false` setzen

### Phase 3: XAML-UI

6. **Dark-Mode Button-Fix (Zeile ~72)**
   - Voraussetzung: Keine (unabhängig)
   - Beschreibung:
     - `Foreground="White"` → `Foreground="{DynamicResource PrimaryTextBrush}"`
     - Einfache Ein-Zeilen-Änderung
     - Auch Button `IsEnabled="{Binding HasScmPlugins}"` hinzufügen (optional, aber empfohlen)

7. **Grid.RowDefinitions erweitern: Von 3 auf 4 Rows**
   - Voraussetzung: Schritt 6
   - Beschreibung:
     - RowDefinitions aktualisieren:
       - Row 0: Auto (Titel, bestehend)
       - Row 1: Auto (neue ComboBox)
       - Row 2: * (ListBox + Hilfe-Panel, bestehend + neu)
       - Row 3: Auto (Buttons, verschieben)
     - Alle bestehenden Elemente: `Grid.Row` aktualisieren

8. **ComboBox für Plugin-Auswahl einfügen (Row 1)**
   - Voraussetzung: Schritt 7
   - Beschreibung:
     - Neuer ComboBox als neues Kind-Element im Grid
     - Grid.Row="1"
     - Bindings:
       - `ItemsSource="{Binding AvailableScmPlugins}"`
       - `SelectedItem="{Binding SelectedScmPlugin, Mode=TwoWay}"`
       - `DisplayMemberPath="PluginName"`
     - Styling: Background, Foreground, BorderBrush via `DynamicResource`
     - ComboBoxItem-Style für konsistente Text-Farbe
     - Padding="8,6", Margin="0,0,0,12" für Abstand

9. **ListBox mit Visibility-Binding ausstatten (Row 2)**
   - Voraussetzung: Schritte 7, 8
   - Beschreibung:
     - Bestehende ListBox mit Visibility-Binding:
       - `Visibility="{Binding HasScmPlugins, Converter={StaticResource BoolToVisibilityConverter}}"`
       - Grid.Row="2" (bereits, aber bestätigen)
     - ListBox bleibt visible nur wenn `HasScmPlugins == true`

10. **Hilfe-Panel einfügen (Row 2, zweites Child)**
    - Voraussetzung: Schritte 7–9
    - Beschreibung:
      - Neues StackPanel als zweites Kind im Grid (nach ListBox)
      - Grid.Row="2" (same Row als ListBox, aber nur einer ist sichtbar)
      - Visibility-Binding: `Visibility="{Binding HasScmPlugins, Converter={StaticResource InverseBoolToVisibilityConverter}}"`
      - Styling: Background, BorderBrush via `DynamicResource`, Padding="16", Centered
      - Zwei TextBlocks:
        - Title: "Keine SCM-Plugins installiert", FontSize="14", FontWeight="SemiBold"
        - Description: Hilfetext, FontSize="12", TextWrapping="Wrap"

11. **Button-Row aktualisieren (Grid.Row 3)**
    - Voraussetzung: Schritt 10
    - Beschreibung:
      - Existierende Button-StackPanel: Grid.Row="2" → Grid.Row="3"
      - Button-Content und Logik bleiben unverändert

### Phase 4: Tests

12. **Unit-Tests für `RepositoryAssignViewModel` schreiben**
    - Voraussetzung: Schritte 1–5
    - Beschreibung:
      - Test-Klasse `RepositoryAssignViewModelTests` anlegen/erweitern
      - Mock-Infrastruktur: `IPluginManager`, `ProjektService`, `ILogger`
      - Test-Fälle (siehe Tests-Abschnitt unten)
      - Alle 9 Tests aus dem Tests-Abschnitt implementieren

13. **Integration-Tests (E2E UI-Tests)**
    - Voraussetzung: Schritte 1–12
    - Beschreibung:
      - Dialog-UI-Tests für alle Szenarien (siehe E2E-Tests unten)
      - Überprüfung: Plugin-Load, Repository-Filter, Dark-Mode Button, Hilfe-Panel

---

## Tests

### Neue Unit-Tests

| Test-Klasse | Test-Methode | Eingabe / Vorbedingung | Erwartetes Ergebnis |
|---|---|---|---|
| `RepositoryAssignViewModelTests` | `LadenAsync_ShouldLoadAvailablePlugins_WhenPluginsExist` | `IPluginManager` returned 3 SCM-Plugins | `AvailableScmPlugins.Count == 3`; alle Plugins in Collection |
| `RepositoryAssignViewModelTests` | `LadenAsync_ShouldSetHasScmPlugins_ToTrue_WhenPluginsAvailable` | Mindestens 1 Plugin vorhanden | `HasScmPlugins == true` |
| `RepositoryAssignViewModelTests` | `LadenAsync_ShouldSetHasScmPlugins_ToFalse_WhenNoPluginsAvailable` | `IPluginManager` returns leere Liste | `HasScmPlugins == false`; `AvailableScmPlugins.Count == 0` |
| `RepositoryAssignViewModelTests` | `SelectedScmPluginChanged_ShouldReloadRepositories_FilteredByPluginType` | 3 Plugins, 5 Repositories (3 davon für Plugin A, 2 für B); Plugin A wird ausgewählt | `VerfuegbareRepositories` enthält nur 3 Repositories für Plugin A |
| `RepositoryAssignViewModelTests` | `SelectedScmPluginChanged_ShouldClearRepositories_WhenPluginUnselected` | Repositories vorher geladen; `SelectedScmPlugin = null` | `VerfuegbareRepositories.Count == 0` |
| `RepositoryAssignViewModelTests` | `SelectedScmPluginChanged_ShouldSetIsLoading_FlagDuringReload` | Plugin wird ausgewählt; Reload läuft (simuliert mit Delay) | `IsLoading == true` am Anfang der Reload; `IsLoading == false` nach Completion |
| `RepositoryAssignViewModelTests` | `ReloadRepositoriesForSelectedPlugin_ShouldLogError_WhenServiceThrows` | `ProjektService.GetAllRepositoriesAsync()` wirft Exception | `Logger.LogError` wird aufgerufen; `VerfuegbareRepositories` bleibt leer; Exception wird nicht propagiert |
| `RepositoryAssignViewModelTests` | `RepositorySelection_ShouldEnableBestaetigenCommand_WhenRepositorySelected` | Repository wird in `SelectedRepository` gesetzt | `BestaetigenCommand.CanExecute(null) == true` |
| `RepositoryAssignViewModelTests` | `RepositorySelection_ShouldDisableBestaetigenCommand_WhenRepositoryUnselected` | `SelectedRepository = null` | `BestaetigenCommand.CanExecute(null) == false` |

### Betroffene bestehende Tests

| Test-Klasse | Test-Methode | Grund der Anpassung | Aktion |
|---|---|---|---|
| `ProjektServiceTests` | (Alle Repository-Abruf-Tests) | Keine Änderung erforderlich | `ProjektService.GetAllRepositoriesAsync()` bleibt unverändert; wird weiterhin alle Repos returned |
| `WpfDialogServiceTests` (falls existiert) | Tests für `RepositoryZuweisenDialog` | Neue `IPluginManager`-Abhängigkeit im ViewModel; Mock-Setup muss angepasst werden | `RepositoryAssignViewModel` Constructor-Aufruf mit Mock-IPluginManager aktualisieren |
| (Existing Repository-List Tests) | (UI-Tests, die ListBox prüfen) | Repositories sind nun gefiltert nach Plugin; nicht alle Repositories werden angezeigt | Test-Daten anpassen, um Plugin-Filter zu berücksichtigen; ggf. erwarten weniger Repositories in ListBox |

### E2E-Tests (UI-Szenarien)

| Szenario | Schritte | Erwartetes Verhalten |
|---|---|---|
| **Dialog mit verfügbaren Plugins öffnen** | 1. Dialog öffnen 2. `LadenAsync()` wartet auf Completion | ComboBox zeigt verfügbare Plugins; ListBox ist sichtbar und leer (bis Plugin ausgewählt); Hilfe-Panel ist ausgeblendet; Buttons sind enabled |
| **Dialog ohne Plugins öffnen** | 1. Dialog öffnen 2. `IPluginManager.GetSourceCodeManagementPlugins()` returns leere Liste | Hilfe-Panel ist sichtbar (Title + Description); ComboBox, ListBox, Buttons sind ausgeblendet oder disabled; Benutzer kann nicht interagieren |
| **Plugin aus ComboBox auswählen** | 1. Dialog öffnen 2. Plugin A aus ComboBox wählen 3. Warten auf `ReloadRepositoriesForSelectedPlugin()` | ListBox wird mit Repositories für Plugin A aktualisiert; `SelectedRepository` wird zurückgesetzt (null); `IsLoading` zeigt Übergangszustand |
| **Plugin wechseln triggert Repository-Reload** | 1. Dialog öffnen 2. Plugin A wählen 3. Plugin B wählen | ListBox wird zuerst mit A-Repos gefüllt, dann mit B-Repos; `SelectedRepository == null`; Repository-Liste ist unterschiedlich für A und B |
| **Repository auswählen und bestätigen** | 1. Dialog öffnen 2. Plugin wählen 3. Repository aus ListBox wählen 4. "Zuweisen"-Button klicken | Dialog schließt; `DialogResult == true`; Aufrufer erhält ausgewähltes Repository |
| **Dark-Mode Button-Visibility** | 1. App im Dark-Theme öffnen 2. Dialog öffnen 3. "Zuweisen"-Button beobachten | Button-Text ist lesbar (weiß oder Hell-Text auf dunklem Hintergrund); nicht weiß auf dunkelgrau verschwindend |
| **Abbruch-Button** | 1. Dialog öffnen 2. Plugin wählen 3. "Abbrechen"-Button klicken | Dialog schließt; `DialogResult == false`; Aufrufer hat keine Auswahl |
| **Plugin-Liste ist leer nach LadenAsync** | 1. Dialog öffnen 2. `IPluginManager` returns leere Liste 3. LadenAsync beendet | `HasScmPlugins == false`; Hilfe-Panel zeigt; Keine Exceptions; Dialog ist in Fehler-State, aber nicht gecrasht |

---

## Offene Punkte

Alle technischen und fachlichen Fragen wurden durch die Bestandsaufnahme geklärt. Die folgende Tabelle zeigt ursprüngliche offene Fragen und ihre Antworten:

| # | Ursprünglicher offener Punkt | Geklärt durch | Designentscheidung | Status |
|---|---|---|---|---|
| 1 | Fehlerbehandlung bei fehlenden Plugins: Soll Dialog deaktiviert oder nur Warnung? | Anforderung + Best Practice | `HasScmPlugins = false`, Hilfe-Panel anzeigen; Dialog-Eingabe deaktivieren (User kann nicht ohne Plugin arbeiten) | ✓ Geklärt |
| 2 | Standard-Plugin automatisch auswählen? | Anforderung + UX-Best Practice | Nein, Benutzer muss aktiv auswählen; explizit ist besser als implizit | ✓ Geklärt |
| 3 | Hilfe-Text Inhalt und Format? | Spezifiziert im Anforderungs-Dokument | „Keine SCM-Plugins installiert. Um Repositories zuzuweisen, installieren Sie bitte ein SCM-Plugin (z.B. GitHub Plugin)." | ✓ Geklärt |
| 4 | Plugin-Details in ComboBox (nur Name oder weitere Infos)? | Anforderung, Usability | Nur `PluginName` via `DisplayMemberPath="PluginName"` | ✓ Geklärt |
| 5 | Dark-Mode Theme-Farbe für Button-Text? | Projekt-Konvention überprüft | `DynamicResource PrimaryTextBrush` (Standard-Text-Farbe im Project-Theme) | ✓ Geklärt |
| 6 | Abhängigkeits-Injection für `IPluginManager`? | Projekt-DI-Infrastruktur + Best Practice | Konstruktor-Parameter (optionaler Standardwert für Rückwärts-Kompatibilität) | ✓ Geklärt |

**Ergebnis:** Keine ungeklärten offenen Punkte — alle Anforderungen sind spezifiziert und können implementiert werden.

---

## Zusammenfassung: Kritische Implementierungsnotizen

1. **Converter-Registrierung überprüfen:** `InverseBoolToVisibilityConverter` muss in XAML-Ressourcen verfügbar sein (App.xaml oder Dialog-Resources). Falls nicht vorhanden, registrieren oder alternative Logik verwenden.

2. **Plugin-Typ-Vergleich Konsistenz:** String-Vergleich `GitRepository.PluginTyp` vs. `IGitPlugin.PluginType.ToString()` — prüfen, ob Format und Case-Sensitivität konsistent sind. Tests schreiben!

3. **DI-Container Konfiguration:** `IPluginManager` muss in Service-Registration vorhanden sein (Program.cs oder Startup). Überprüfung erforderlich.

4. **Fire-and-Forget Async-Handling:** `ReloadRepositoriesForSelectedPlugin()` wird aus Property-Setter aufgerufen → Task muss ignoriert werden mit Fehlerlogging: `_ = ReloadRepositoriesForSelectedPlugin();`

5. **IsLoading-Flag-Semantik:** Überprüfung, wie `IsLoading` aktuell im ViewModel verwendet wird und ob es für Async-Reload auch genutzt werden kann.

6. **Backward Compatibility:** Konstruktor-Parameter optional machen oder Overload verwenden, um bestehenden Code nicht zu brechen.

7. **Theme-Ressourcen:** `BackgroundBrush`, `PrimaryTextBrush`, `SecondaryTextBrush`, `BorderBrush` müssen in Projekt-Theme vorhanden sein; sonst Fallback-Farben verwenden.

---

## Dateien zum Anlegen oder Ändern

| Datei | Aktion | Priorität |
|---|---|---|
| `src/Softwareschmiede.App/ViewModels/RepositoryAssignViewModel.cs` | Ändern: +3 Properties, +1 Methode, Konstruktor erweitern | Hoch |
| `src/Softwareschmiede.App/Views/RepositoryAssignDialog.xaml` | Ändern: Dark-Mode Fix (1 Zeile), +ComboBox, +Hilfe-Panel, Grid-Struktur | Hoch |
| `src/Softwareschmiede.App/Views/RepositoryAssignDialog.xaml.cs` | Keine Änderungen erforderlich | — |
| `src/Softwareschmiede.Tests/Application/ViewModels/RepositoryAssignViewModelTests.cs` | Neu oder erweitert: +9 Test-Methoden | Hoch |

