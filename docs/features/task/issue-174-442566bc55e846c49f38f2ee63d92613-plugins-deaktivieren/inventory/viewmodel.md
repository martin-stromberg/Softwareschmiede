# ViewModel und Logik

## `SettingsViewModel`
Datei: `src/Softwareschmiede.App/ViewModels/SettingsViewModel.cs`

### Eigenschaften für Plugin-Management

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| `SourceCodeManagementPlugins` | `ObservableCollection<PluginActivationEntry>` | SCM-Plugins mit Aktivierungsstatus (Zeile 114) |
| `DevelopmentAutomationPlugins` | `ObservableCollection<PluginActivationEntry>` | KI-Plugins mit Aktivierungsstatus (Zeile 117) |
| `SelectedPlugin` | `PluginActivationEntry?` | Aktuell im Plugins-Register ausgewählter Eintrag (Zeilen 120–124) |
| `SelectedPluginSettings` | `IReadOnlyList<PluginSettingGroupEntry>` | Einstellungsgruppen des ausgewählten Plugins (Zeilen 127–131) |
| `ScmPlugins` | `IReadOnlyList<IGitPlugin>` | Alle verfügbaren SCM-Plugins (Zeile 66) |
| `KiPlugins` | `IReadOnlyList<IKiPlugin>` | Alle verfügbaren KI-Plugins (Zeile 69) |
| `DefaultScmPlugin` | `IGitPlugin?` | Standard-SCM-Plugin (Zeilen 72–76) |
| `DefaultKiPlugin` | `string?` | Standard-KI-Plugin-Prefix (Zeilen 59–62) |

### Kommandos für Plugin-Management

| Kommando | Typ | Zweck |
|----------|-----|-------|
| `ScmPluginSelectedCommand` | `RelayCommand<IGitPlugin>` | Wird ausgelöst, wenn Nutzer ein SCM-Plugin in Standard-ComboBox wählt (Zeile 146) |
| `KiPluginSelectedCommand` | `RelayCommand<IKiPlugin>` | Wird ausgelöst, wenn Nutzer ein KI-Plugin in Standard-ComboBox wählt (Zeile 149) |
| `PluginSelectedCommand` | `RelayCommand<PluginActivationEntry>` | Wird ausgelöst, wenn Nutzer einen Eintrag in den Aktivierungslisten wählt (Zeile 152) |
| `LadenCommand` | `AsyncRelayCommand` | Lädt alle Einstellungen einschließlich Plugin-Aktivierungsstatus (Zeile 137) |
| `SpeichernCommand` | `AsyncRelayCommand` | Speichert alle Einstellungen einschließlich Plugin-Aktivierungsstatus (Zeile 140) |
| `VerwerfenCommand` | `AsyncRelayCommand` | Verwirft ungespeicherte Änderungen und lädt erneut (Zeile 143) |

### Methoden für Plugin-Management

**`LoadSelectedPluginSettings(PluginActivationEntry entry)` (Zeilen 304–308)**
- Setzt `SelectedPlugin` auf den übergebenen Eintrag
- Lädt die Einstellungsgruppen des Plugins via `LadePluginEinstellungen(entry.Plugin)`
- Wird von `PluginSelectedCommand` aufgerufen

**`SelectPluginByReference(IPlugin? plugin)` (Zeilen 312–317)**
- Sucht den Aktivierungseintrag für ein über Standard-ComboBox ausgewähltes Plugin
- Ruft `LoadSelectedPluginSettings` auf, wenn gefunden
- Wird von `ScmPluginSelectedCommand` und `KiPluginSelectedCommand` aufgerufen

**`LadePluginAktivierungAsync(CancellationToken ct)` (Zeilen 319–334)**
- Lädt Aktivierungsstatus für alle Plugins via `PluginActivationService`
- Befüllt `SourceCodeManagementPlugins` und `DevelopmentAutomationPlugins` Collections
- Wird während `LadenAsync` aufgerufen

**`LadePluginEinstellungen(IPlugin plugin)` (Zeilen 336–347)**
- Gibt `IReadOnlyList<PluginSettingGroupEntry>` für ein Plugin zurück
- Strukturiert Setting-Groups und Felder des Plugins
- Wird von `LoadSelectedPluginSettings` aufgerufen

**`ValidierePluginAktivierung()` (Zeilen 374–389)**
- Prüft, dass mindestens ein SCM-Plugin aktiv ist
- Prüft, dass mindestens ein KI-Plugin aktiv ist
- Setzt `FehlerMeldung`, wenn Validierung fehlschlägt
- Wird während `SpeichernAsync` aufgerufen (Zeile 369)

**`SpeichernAsync(CancellationToken ct)` (Zeilen 249–297)**
- Validiert Pflichtfelder (Zeile 256)
- Speichert `entry.IsEnabled` für alle Plugin-Einträge via `PluginActivationService.SetPluginEnabledAsync()` (Zeilen 259–260)
- Speichert Plugin-Einstellungen via `SpeicherePluginEinstellungen()` (Zeile 262)
- Persistiert alle App-Einstellungen

**`LadeAsync(CancellationToken ct)` (Zeilen 197–247)**
- Lädt alle Einstellungen einschließlich:
  - Plugin-Listen via `IPluginManager`
  - Aktivierungsstatus via `LadePluginAktivierungAsync()` (Zeile 225)
  - Standard-Plugins (Zeilen 227–230)
  - Promptvorlagen (Zeile 232)

### Initialisierung (Constructor, Zeilen 161–195)
- Erzeugt die Kommandos mit den entsprechenden Methoden
- `PluginSelectedCommand` ruft `LoadSelectedPluginSettings` auf
- `ScmPluginSelectedCommand` und `KiPluginSelectedCommand` rufen `SelectPluginByReference` auf

### Abhängigkeiten (Injiziert)
- `PluginActivationService` (Zeile 22) — verwaltet Plugin-Aktivierungsstatus
- `PluginSettingsService` (Zeile 23) — liest/schreibt Plugin-Einstellungen
- `IPluginManager` (Zeile 21) — liefert verfügbare Plugins
