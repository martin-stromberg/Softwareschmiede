← [Zurück zur Übersicht](index.md)

# Einstellungen — Technischer Ablauf

## Übersicht

Die Einstellungsansicht (`SettingsView`) lädt beim Öffnen alle verfügbaren SCM- und KI-Plugins über `IPluginManager`. Der Anwender wählt ein Plugin aus, woraufhin `SettingsViewModel` die vom Plugin definierten Einstellungsgruppen (`PluginSettingGroup`) abruft und für jedes Feld (`PluginSettingField`) die gespeicherten Werte via `PluginSettingsService.GetValue()` lädt. Die XAML rendert dynamisch die passenden Eingabekomponenten basierend auf `PluginSettingFieldType`. Beim Speichern werden alle Werte und die Standard-Plugin-Namen persistiert.

## Ablauf

### 1. Laden der Einstellungen beim Öffnen

Auslöser: Anwender öffnet die Einstellungsansicht.

1. `SettingsView.xaml.cs` wird initialisiert; sein `DataContext` wird auf `SettingsViewModel` gesetzt
2. Das `Loaded`-Event triggert `SettingsViewModel.LadenCommand`
3. `SettingsViewModel.LoadAsync(CancellationToken)` wird aufgerufen:
   - `AppSettingService.GetSettingAsync(AppSettingService.DefaultKiPluginKey)` → lädt String-Name des Standard-KI-Plugins
   - `AppSettingService.GetSettingAsync(AppSettingService.DefaultScmPluginKey)` → lädt String-Name des Standard-SCM-Plugins
   - `DarkModeService.GetAvailableModes()` → liefert verfügbare Design-Modi
   - `IPluginManager.GetSourceCodeManagementPlugins()` → liefert Liste aller `IGitPlugin`-Implementierungen
   - `IPluginManager.GetDevelopmentAutomationPlugins()` → liefert Liste aller `IKiPlugin`-Implementierungen
4. Die geladenen Plugin-Namen werden mit den verfügbaren Plugins abgeglichen:
   - Falls gespeicherter SCM-Plugin-Name existiert: `DefaultScmPlugin` wird auf das gefundene Plugin gesetzt
   - Sonst: `DefaultScmPlugin` wird auf das erste Plugin der Liste gesetzt
   - Analog für KI-Plugin
5. Für beide Standard-Plugins werden die Einstellungsgruppen sofort geladen (siehe Schritt 2)

Beteiligte Komponenten:
- `SettingsViewModel.LoadAsync()` — Orchestrierung des Ladevorgangs
- `AppSettingService.GetSettingAsync()` — Laden der gespeicherten Plugin-Namen
- `IPluginManager.GetSourceCodeManagementPlugins()` / `GetDevelopmentAutomationPlugins()` — Plugin-Discover
- `DarkModeService.Current` — aktuelle Design-Einstellung

### 2. Laden der Plugin-Einstellungsgruppen (SCM oder KI)

Auslöser: Plugin-Auswahl wird in ComboBox geändert, oder beim initialen Laden.

1. `SettingsView.xaml` bindet an `ComboBox.SelectionChanged`-Event
2. Code-Behind (`SettingsView.xaml.cs`) ruft den passenden Command auf:
   - Für SCM: `SettingsViewModel.ScmPluginSelectedCommand` mit Parameter `IGitPlugin plugin`
   - Für KI: `SettingsViewModel.KiPluginSelectedCommand` mit Parameter `IKiPlugin plugin`
3. Der Command ruft die private Methode auf:
   - `LoadScmPluginSettings(plugin)` oder `LoadKiPluginSettings(plugin)`
4. Diese Methode führt `LoadPluginSettings(plugin)` aus:
   - `plugin.GetSettingGroups()` wird aufgerufen → liefert `IEnumerable<PluginSettingGroup>` mit Gruppen und Feldern
   - Für jede `PluginSettingGroup`:
     - Eine neue `PluginSettingGroupEntry` wird erstellt (Container für die UI)
     - Für jedes `PluginSettingField` in der Gruppe:
       - `PluginSettingsService.GetValue(plugin, field)` wird aufgerufen → gibt den String-Wert oder `null` zurück
       - Eine neue `PluginSettingEntry` wird erstellt mit dem geladenen Wert
       - Für `Softwareschmiede.Codex.CommandLineParameters` wird `PluginSettingEntry` ohne Default-Fallback erstellt; ein fehlender Credential wird dadurch als leerer Anwenderwert angezeigt
5. Die Liste wird in `SelectedScmPluginSettings` oder `SelectedKiPluginSettings` gespeichert
6. Die Binding-Maschine triggert die XAML-Neurendrung

Beteiligte Komponenten:
- `SettingsView.xaml` — `ComboBox` mit `SelectionChanged`-Event
- `SettingsView.xaml.cs` — Event-Handler, der Command aufruft
- `SettingsViewModel.ScmPluginSelectedCommand` / `SettingsViewModel.KiPluginSelectedCommand` — Commands (RelayCommand<IGitPlugin> / RelayCommand<IKiPlugin>)
- `SettingsViewModel.LoadScmPluginSettings()` / `SettingsViewModel.LoadKiPluginSettings()` — private Methoden
- `SettingsViewModel.LoadPluginSettings()` — generische Methode
- `IPlugin.GetSettingGroups()` — Plugin-definierte Methode
- `PluginSettingsService.GetValue()` — Abruf aus Credential Store
- `PluginSettingGroupEntry` — Hilfsklasse mit Eigenschaften `GroupName` und `Entries`
- `PluginSettingEntry` — Hilfsklasse mit Eigenschaften `Field` und `Value` / `BoolValue`
- `SettingsViewModel.IsCodexCommandLineParameters()` — Codex-spezifische Erkennung für `CommandLineParameters` ohne Default-Fallback

### 3. Rendering von Plugin-Einstellungen in der XAML

Auslöser: `SelectedScmPluginSettings` oder `SelectedKiPluginSettings` wird aktualisiert.

1. `SettingsView.xaml` bindet an `ItemsControl.ItemsSource="{Binding SelectedScmPluginSettings}"` (bzw. `SelectedKiPluginSettings`)
2. Das `ItemsControl` iteriert über jede `PluginSettingGroupEntry`:
   - Für jede Gruppe wird das ItemTemplate gerendert:
     - `TextBlock` zeigt `GroupName` (z. B. "Authentifizierung")
     - Verschachtelte `ItemsControl` iteriert über `Entries` (die `PluginSettingEntry`-Objekte)
3. Für jede `PluginSettingEntry` wird das ItemTemplate gerendert:
   - `TextBlock` zeigt `Field.Label` (Feldbezeichnung)
   - `ContentControl` mit `ContentTemplateSelector="{StaticResource FieldTemplateSelector}"` wird verwendet:
     - `PluginSettingFieldTemplateSelector` untersucht `entry.Field.FieldType`
     - Die entsprechende DataTemplate wird ausgewählt und das Feld gerendert:
       - **Text** → `TextBox` mit Binding `Text="{Binding Value, UpdateSourceTrigger=PropertyChanged}"`
       - **Secret** → `PasswordBox` mit `PasswordChanged`-Event und Code-Behind-Binding zu `entry.Value`
       - **Url** → `TextBox` (wie Text)
       - **Integer** → `TextBox` (ohne Validierungs-Regel; Validierung erfolgt beim Speichern)
       - **Boolean** → `CheckBox` mit Binding `IsChecked="{Binding BoolValue, UpdateSourceTrigger=PropertyChanged}"`
       - **Enum** → `ComboBox` mit `ItemsSource="{Binding Field.EnumOptions}"` und `SelectedValue="{Binding Value, UpdateSourceTrigger=PropertyChanged}"`
       - **FilePath** → `StackPanel` mit `TextBox` und Browse-Button (`OnFileSelectClick`-Handler)
   - `TextBlock` zeigt `Field.Description` (falls vorhanden) mit sekundärer Textfarbe

Beteiligte Komponenten:
- `SettingsView.xaml` — ItemsControl und DataTemplates
- `PluginSettingFieldTemplateSelector` — Klasse in Code-Behind, wählt richtige DataTemplate
- `PluginSettingGroupEntry` — Datenmodell
- `PluginSettingEntry` — Datenmodell
- `PluginSettingField` — Metadaten (Label, Description, FieldType, EnumOptions)
- Verschiedene WPF-Controls — TextBox, PasswordBox, CheckBox, ComboBox, Button

### 4. Speichern der Plugin-Einstellungen und Standard-Plugins

Auslöser: Anwender klickt den Button **Speichern**.

1. `SettingsView.xaml` bindet `SaveCommand="{Binding SaveCommand}"` an Button
2. `SettingsViewModel.SaveCommand` wird ausgelöst → ruft `SaveAsync(CancellationToken)` auf:
3. Validierung aller Felder:
   - `ValidateRequiredFields()` wird aufgerufen:
     - Für jede `PluginSettingEntry` in `SelectedScmPluginSettings` und `SelectedKiPluginSettings`:
       - Falls `Field.IsRequired` und `Value` ist leer → Fehler, Speichern abgebrochen
       - Falls `FieldType == Integer` und `Value` ist keine gültige Ganzzahl → Fehler
       - Falls `FieldType == Enum` und `Value` nicht in `Field.EnumOptions` → Fehler
   - Falls Validierung fehlgeschlagen: `ErrorMessage` wird gesetzt, Methode kehrt zurück
4. Standard-Plugins speichern:
   - `AppSettingService.SetSettingAsync(AppSettingService.DefaultKiPluginKey, DefaultKiPlugin, ct)` → speichert String-Name des KI-Plugins
   - `AppSettingService.SetSettingAsync(AppSettingService.DefaultScmPluginKey, DefaultScmPlugin?.PluginName, ct)` → speichert String-Name des SCM-Plugins
5. Plugin-Einstellungswerte speichern:
   - `SavePluginSettingsAsync(_defaultScmPlugin, _selectedScmPluginSettings)` wird aufgerufen:
     - Für jede `PluginSettingEntry` in den Einstellungsgruppen:
       - `PluginSettingsService.SetValue(plugin, entry.Field, entry.Value)` wird aufgerufen
       - Bei Boolean-Feldern: `entry.BoolValue` wird als "true"/"false" String gespeichert (Konvertierung erfolgt in `SetValue`)
       - Bei Codex-`CommandLineParameters`: Auch ein leerer Wert wird gespeichert, damit entfernte Parameter nicht durch spätere Defaults wiederhergestellt werden
   - Analog für KI-Plugins
6. Weitere Einstellungen speichern:
   - `WorkingDirectorySettingsService.SaveWorkingDirectoryAsync()`
   - `DarkModeService.SetModeAsync()` (wendet Design sofort an)
7. Erfolgsmeldung anzeigen:
   - `ErfolgsMeldung = "Einstellungen gespeichert."`

Beteiligte Komponenten:
- `SettingsViewModel.SaveAsync()` — Orchestrierung
- `SettingsViewModel.ValidateRequiredFields()` — Validierung
- `SettingsViewModel.ValidateRequiredFieldsForSettings()` — Validierung für einzelne Einstellungsgruppen
- `AppSettingService.SetSettingAsync()` — Speichert in `AppSetting`-Tabelle
- `PluginSettingsService.SetValue()` — Speichert in Credential Store
- `WorkingDirectorySettingsService.SaveWorkingDirectoryAsync()` — Speichert Arbeitsverzeichnis
- `DarkModeService.SetModeAsync()` — Wechselt Theme-Ressourcen
- `PluginSettingEntry` — Datenmodell mit `Value` und `BoolValue`

## Diagramm

```mermaid
flowchart TD
    A[Einstellungen öffnen] --> B[LoadAsync aufgerufen]
    B --> C[Plugins laden<br/>IPluginManager.GetSourceCodeManagementPlugins<br/>IPluginManager.GetDevelopmentAutomationPlugins]
    C --> D[Standard-Plugin-Namen laden<br/>AppEinstellungService.GetSetting]
    D --> E[Plugins Lookup:<br/>Name mit verfügbarem Plugin abgleichen]
    E --> F[Initial: Einstellungsgruppen laden<br/>LadePluginEinstellungen für Standard-Plugins]
    F --> G[UI anzeigen: Register SCM und KI]
    G --> H{Benutzer ändert Plugin?}
    H -->|Ja| I[ScmPluginSelectedCommand<br/>oder KiPluginSelectedCommand]
    I --> F
    H -->|Nein| J{Benutzer klickt Speichern?}
    J -->|Nein| K[Benutzer ändert Werte<br/>Echtzeit-Binding]
    K --> J
    J -->|Ja| L[ValidierePflichtfelder]
    L -->|Fehler| M[FehlerMeldung anzeigen]
    M --> N[Abbruch]
    L -->|OK| O[Standard-Plugins speichern<br/>AppEinstellungService.SetSetting]
    O --> P[Plugin-Einstellungen speichern<br/>PluginSettingsService.SetValue]
    P --> Q[Weitere Einstellungen speichern<br/>Arbeitsverzeichnis, Design]
    Q --> R[ErfolgsMeldung anzeigen]
```

## Fehlerbehandlung

### Validierungsfehler beim Speichern

Wenn Validierung fehlgeschlagen:
- `ErrorMessage` wird mit sprechender Nachricht gesetzt (z. B. "Pflichtfeld 'API-Schlüssel' darf nicht leer sein")
- Die Methode kehrt sofort zurück, `PluginSettingsService.SetValue()` wird nicht aufgerufen
- Der Anwender sieht die rote Fehlermeldung oben im Dialog
- Die Einstellung wird nicht persistiert

### Fehler beim Laden von Plugins

Falls `IPluginManager` fehlschlägt oder keine Plugins zurückliefert:
- `ScmPlugins` oder `AiPlugins` ist leer
- Die Register zeigen leere ComboBoxes oder deaktivierte Eingabefelder
- Beim Laden eines Standard-Plugin-Namens wird `FirstOrDefault()` aufgerufen → liefert `null`
- `DefaultScmPlugin` / `DefaultAiPlugin` bleiben `null`
- Die UI reagiert mit leeren Einstellungspanels (durch XAML Bindings)

### Fehler beim Speichern in `PluginSettingsService`

Falls `SetValue()` wirft eine Exception:
- Die Exception wird von `SaveAsync()` gecatcht
- `ErrorMessage` wird mit der Exception-Message gesetzt
- Die Methode kehrt zurück, weitere Einstellungen werden nicht gespeichert
- Der Anwender wird über das Fehler-Banner informiert

## Automatisches Herunterfahren (Auto-Shutdown)

### Übersicht

Der `AutoShutdownOrchestrator` (Application-Layer, Singleton) überwacht die Anzahl fachlich laufender Automatisierungen und armiert bei aktiviertem Schalter einen Countdown über die konfigurierte Wartezeit. Läuft der Countdown ab, wird der OS-Shutdown über `ISystemShutdownService` angefordert. Die UI-Seite liegt in `MainWindowAutoShutdownFlow` (Seitenleisten-Fußzeile des `MainWindow`); Persistenz läuft über `AutoShutdownSettingsService` → `AppSettingService` (Key-Value-Tabelle `AppSettingen`, keine Migration).

### Ablauf

#### 1. Fachliche Lauf-Zählung

1. `RunningAutomationsCount.CountAsync` öffnet einen DI-Scope (`IServiceScopeFactory`) und lädt Kandidaten über `IActiveWorkItemsService.GetActiveWorkItemsAsync`.
2. Pro Aufgabe wird ein `AiExecutionStatusData` aus `WorkItem.Status`, `ExecutionStatus`, `ActiveRunId`, `LastHeartbeatUtc`, `RunStatus`, `PausedUntilUtc`, `IRunningAutomationStatusSource.GetLiveRunStatus(id)` und `PromptTimeDispatchService.GetScheduledPromptStatus(id)` befüllt.
3. `AiExecutionStatusClassification.Classify` (aus der App in den Application-Layer verschoben, `public`) liefert den `AiExecutionState`; gezählt wird ausschließlich `Running`.
4. Zusätzlich zählt der Dienst `ScmIncomingEvents` mit Status `Verzögert` (per Verzögerungs-Keyword zurückgestellte SCM-Ereignisse) als ausstehende Automatisierungen — ein laufender Delay-Timer blockiert den Shutdown-Countdown unabhängig von der Aufgabenkandidatenmenge.
5. `MainWindowViewModel` wendet dieselbe Klassifikation (über den `internal` Adapter in `AppConverters.cs`) auf die gemappte `ActiveWorkItemsList` an und setzt `RunningAutomationsCount` — identisches Ergebnis ohne zweite DB-Abfrage.

Beteiligte Komponenten:
- `RunningAutomationsCount` / `IRunningAutomationsCount` — Zählservice
- `AiExecutionStatusClassification`, `AiExecutionStatusData`, `AiExecutionState` — geteilte Klassifikation
- `IActiveWorkItemsService`, `IRunningAutomationStatusSource`, `PromptTimeDispatchService` — Datenquellen

#### 2. Aktivierung und Optionen (UI → Orchestrator)

1. Checkbox „Nach Leerlauf PC herunterfahren" → `MainWindowAutoShutdownFlow.AutoShutdownEnabled` → `IAutoShutdownOrchestrator.SetEnabled(bool)`; bei `true` wird sofort eine Auswertung angestoßen und der 5-s-Sicherheitstick armiert, bei `false` wird ein laufender Countdown disarmiert.
2. Textbox „Wartezeit (Minuten):" → `AutoShutdownToleranceMinutes`-Setter validiert (`int.TryParse`, 0–`AppSettingService.AutoShutdownMaxToleranceMinutes` = 1440); gültig → `AutoShutdownSettingsService.SaveToleranceMinutesAsync` + `SetOptions(toleranceMinutes, force)`, ungültig → `AutoShutdownToleranceError` sichtbar, nichts wird angewendet.
3. Checkbox „Herunterfahren erzwingen" → `SaveForceAsync` + `SetOptions`.
4. `SetOptions` speichert beide Werte im Orchestrator; bei laufendem Countdown und geänderter Toleranz wird `CountdownEndeUtc = now + neueToleranz` neu armiert. `Erzwingen` wird erst beim Auslösen ausgewertet.

#### 3. Countdown-Lebenszyklus (Orchestrator)

1. Trigger-Quellen: `IRunningAutomationStatusSource.RunningCountChanged`, `WorkItemRunDataChangedNotifier.RunDataChanged`, periodischer 5-s-Sicherheitstick (`TimeProvider`-Timer, nur solange aktiviert) sowie `SetEnabled`/`SetOptions`. Alle Trigger laufen in die koaleszierte `EvaluateAsync` (`SemaphoreSlim` + Erneut-Flag — parallele Trigger während einer laufenden Auswertung erzeugen höchstens einen Nachlauf).
2. `EvaluateOnceAsync`: `count = await _zaehlung.CountAsync()`; unter `_syncLock`:
   - `count > 0` → `_shutdownTriggered = false`, laufender Countdown wird disarmiert, `StateChanged` gefeuert.
   - `enabled && count == 0 && !_shutdownTriggered && CountdownEndeUtc == null` → `CountdownEndeUtc = TimeProvider.GetUtcNow() + toleranz`, Countdown-`ITimer` auf die Restlaufzeit armiert.
   - sonst → keine Änderung; ein armiertes Countdown läuft bei unveränderter Anzahl mit dem bestehenden Endzeitpunkt weiter (verhindert Verhungern durch periodische Heartbeat-`RunDataChanged`-Ereignisse).
3. Countdown-Timer-Callback → `OnCountdownElapsedAsync`: Final-Recheck (`enabled` und `CountAsync() == 0`) → `_shutdownTriggered = true`, `StateChanged`, Warn-Log, `ISystemShutdownService.RequestShutdownAsync(force)`. Negativer Recheck → erneute `EvaluateAsync`. Der Shutdown wird pro Null-Phase höchstens einmal ausgelöst.
4. `Dispose`: Abmeldung beider Events, Entsorgung beider `ITimer`.

Beteiligte Komponenten:
- `AutoShutdownOrchestrator` / `IAutoShutdownOrchestrator` — `SetEnabled`, `SetOptions`, `CountdownEndUtc`, `IsEnabled`, Event `StateChanged`
- `TimeProvider` — DI-registriert; `CreateTimer` für Countdown (One-Shot) und Sicherheitstick (5 s); mit `FakeTimeProvider` deterministisch testbar
- `ISystemShutdownService` — OS-Shutdown-Auslösung

#### 4. Countdown-Anzeige (UI)

1. `AutoShutdownUiSichtbar` (`MainWindowViewModel`) = `LaufendeAutomatisierungenAnzahl > 0 || AutoShutdownAktiviert || AutoShutdownCountdownAktiv` — der Block bleibt im Countdown sichtbar, damit deaktiviert werden kann; der gesamte Block ist zusätzlich an `IsNavigationExpanded` gebunden (eingeklappte Navigation blendet ihn aus).
2. Orchestrator-Event `StateChanged` → `MainWindowAutoShutdownFlow.RefreshCountdownDisplay` liest `CountdownEndUtc` per Dispatcher: gesetzt → `AutoShutdownCountdownText` („Herunterfahren in X Min. Y Sek." / „X Std. Y Min." / „Y Sek.") und 1-s-`DispatcherTimer` für die Restzeit-Ticks; `null` → Timer gestoppt, Text geleert.
3. Beim Start lädt `LoadSettingsAsync` einmalig die persistierten Nebenwerte (kein Write-Back, kein Enabled) und reicht sie per `SetOptions` an den Orchestrator; ein zwischenzeitlich vom Anwender geänderter Wert wird nicht überschrieben.

#### 5. OS-Shutdown-Auslösung

1. `SystemShutdownService.RequestShutdownAsync(force, ct)` ruft `ResolveShutdownCommand(force)` (`internal`, für Argument-Mapping-Tests).
2. Windows: `shutdown /s /f /t 0` bei `force == true`, sonst `shutdown /s /t 0`. Linux/macOS: bestehende Kommandos unverändert — das Force-Flag hat dort kein Äquivalent.
3. Prozessstart (`UseShellExecute = false`, `CreateNoWindow = true`) + `WaitForExitAsync`; `ExitCode != 0` → `InvalidOperationException` (vom Orchestrator geloggt, Auslöse-Flag zurückgesetzt, Neuauswertung).

#### 6. Testmodus-Aufzeichnung

1. `App.xaml.cs` `ConfigureServices`: Ist `SOFTWARESCHMIEDE_TEST_DB_PATH` gesetzt (wie beim `IProzessStarter`-Testmodus), wird `ISystemShutdownService` → `RecordingSystemShutdownService` registriert, sonst `SystemShutdownService`.
2. `RecordingSystemShutdownService.RequestShutdownAsync` hängt eine Zeile (`erzwingen|utc|pid`) an `system-shutdowns.log` im Verzeichnis der Testdatenbank an — kein Prozessstart, kein OS-Kontakt. Damit kann kein E2E-/Unit-Test ein echtes Herunterfahren auslösen.

### Fehlerbehandlung

- **Zählfehler in `EvaluateOnceAsync`:** Warn-Log, Zustand bleibt unverändert; der nächste Trigger wertet erneut aus.
- **Zählfehler im Final-Recheck:** `CountdownEndUtc` wird zurückgesetzt und neu ausgewertet — der Orchestrator bleibt nicht mit totem One-Shot-Timer hängen.
- **Fehler beim `RequestShutdownAsync`:** Error-Log, `_shutdownTriggered` wird zurückgesetzt und neu ausgewertet — bei einem transienten Fehler kann die nächste Auswertung erneut armieren.
- **Ungültige Wartezeit-Eingabe:** `AutoShutdownToleranceError` sichtbar; weder persistiert noch an den Orchestrator weitergereicht (letzter gültiger Wert bleibt wirksam). `SetOptions` normalisiert negative Werte defensiv auf 0; `GetAutoShutdownToleranceMinutesAsync` clampt gespeicherte Werte auf 0–1440.
