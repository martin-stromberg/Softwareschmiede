# Services

## `PluginActivationService`
Datei: `src/Softwareschmiede/Application/Services/PluginActivationService.cs`

Verwaltet den benutzerspezifischen Aktivierungsstatus je Plugin und filtert Plugin-Listen entsprechend. Speichert Einstellungen über `AppEinstellungService` mit Schlüsseln im Format `plugins.enabled.{PluginPrefix}`.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `IsPluginEnabledAsync(string pluginPrefix, CancellationToken ct)` | public | Prüft, ob Plugin aktiviert ist (Fehlender Eintrag = aktiviert) |
| `SetPluginEnabledAsync(string pluginPrefix, bool enabled, CancellationToken ct)` | public | Speichert Aktivierungsstatus via AppEinstellungService |
| `GetEnabledSourceCodeManagementPluginsAsync(CancellationToken ct)` | public | Gibt alle aktiven SCM-Plugins zurück |
| `GetEnabledDevelopmentAutomationPluginsAsync(CancellationToken ct)` | public | Gibt alle aktiven KI-Plugins zurück |
| `FilterEnabledAsync<TPlugin>(IReadOnlyList<TPlugin> plugins, CancellationToken ct)` | private | Filtert Plugin-Liste nach Aktivierungsstatus |
| `IsEnabledValue(string? wert)` | private static | Entscheidet, ob Wert als aktiviert gilt |
| `BuildKey(string pluginPrefix)` | private static | Erzeugt Schlüssel `plugins.enabled.{PluginPrefix}` |

**Abhängigkeiten:**
- `AppEinstellungService` — persistente Einstellungen
- `IPluginManager` — liefert verfügbare Plugins

**Verwendet von:**
- `SettingsViewModel.LadePluginAktivierungAsync()` — lädt Aktivierungsstatus
- `SettingsViewModel.SpeichernAsync()` — speichert Aktivierungsstatus

---

## `PluginSettingsService`
Datei: `src/Softwareschmiede/Application/Services/PluginSettingsService.cs`

Service zum Lesen und Schreiben von Plugin-Einstellungen über den `ICredentialStore`. Schlüssel werden als `<PluginPrefix>.<FieldKey>` gespeichert.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetValue(IPlugin plugin, PluginSettingField field)` | public | Gibt gespeicherten Wert für Einstellungsfeld zurück |
| `SetValue(IPlugin plugin, PluginSettingField field, string value)` | public | Speichert Wert für Einstellungsfeld |
| `DeleteValue(IPlugin plugin, PluginSettingField field)` | public | Löscht gespeicherten Wert |
| `HasValue(IPlugin plugin, PluginSettingField field)` | public | Prüft, ob Wert existiert |
| `GetAllPlugins(IEnumerable<IGitPlugin> gitPlugins, IEnumerable<IKiPlugin> kiPlugins)` | public | Gibt alle Git- und KI-Plugins als IPlugin-Liste zurück |
| `BuildKey(IPlugin plugin, PluginSettingField field)` | private static | Erzeugt Schlüssel `<PluginPrefix>.<FieldKey>` |

**Abhängigkeiten:**
- `ICredentialStore` — persistente Speicherung sensibler und allgemeiner Einstellungen

**Verwendet von:**
- `SettingsViewModel.LadePluginEinstellungen()` — lädt Einstellungen eines Plugins
- `SettingsViewModel.SpeichernAsync()` — speichert Einstellungen eines Plugins via `SpeicherePluginEinstellungen()`
