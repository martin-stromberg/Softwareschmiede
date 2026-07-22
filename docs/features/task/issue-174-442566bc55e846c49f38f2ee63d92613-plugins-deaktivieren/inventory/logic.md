# Logik & Services

## `IPluginManager`
Datei: `src/Softwareschmiede/Domain/Interfaces/IPluginManager.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetSourceCodeManagementPlugins()` | Public | Gibt alle geladenen SCM-Plugins zurück (ungefiltert) |
| `GetDevelopmentAutomationPlugins()` | Public | Gibt alle geladenen Development-Automation-Plugins zurück (ungefiltert) |
| `GetDefaultSourceCodeManagementPlugin()` | Public | Gibt das erste verfügbare SCM-Plugin zurück |
| `GetDefaultDevelopmentAutomationPlugin()` | Public | Gibt das priorisierte Development-Automation-Plugin zurück (bevorzugt Copilot) |

### Hinweise
- **FEHLEND**: Methoden `GetEnabledSourceCodeManagementPlugins()`, `GetEnabledDevelopmentAutomationPlugins()`, `SetPluginEnabled()`, `IsPluginEnabled()`
- Die aktuellen Methoden geben **alle** geladenen Plugins zurück, ohne Aktivierungsstatus zu berücksichtigen.

## `PluginManager`
Datei: `src/Softwareschmiede/Infrastructure/Plugins/PluginManager.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetSourceCodeManagementPlugins()` | Public | Ruft `EnsureInitialized()` auf, gibt `_gitPlugins`-Liste zurück |
| `GetDevelopmentAutomationPlugins()` | Public | Ruft `EnsureInitialized()` auf, gibt `_kiPlugins`-Liste zurück |
| `GetDefaultSourceCodeManagementPlugin()` | Public | Gibt erstes Element von `_gitPlugins` oder wirft Exception |
| `GetDefaultDevelopmentAutomationPlugin()` | Public | Sortiert KI-Plugins nach Priorität (Copilot prioritär), gibt erstes Element |
| `EnsureInitialized()` | Private | Lazy-Initialization mit Thread-Lock; ruft `DiscoverPlugins()` einmalig auf |
| `DiscoverPlugins()` | Private | Durchsucht Plugin-Verzeichnis, lädt DLLs dynamisch |
| `LoadPluginsFromDll()` | Private | Lädt Plugin-Typen aus einer DLL |
| `TryCreateAndRegister()` | Private | Instanziiert Plugin und registriert es in `_gitPlugins` oder `_kiPlugins` |

### Eigenschaften
- `_gitPlugins`: `List<IGitPlugin>` — geladene SCM-Plugins
- `_kiPlugins`: `List<IKiPlugin>` — geladene KI-Plugins
- `_initialized`: `bool` — Flag für Lazy-Loading mit Thread-Lock
- `_applyTestModeFilter`: `bool` — Nur Standard-Plugin-Verzeichnis nutzen
- `_pluginDirectory`: `string` — Pfad zum Plugin-Verzeichnis

### Hinweise
- Discovery erfolgt einmalig beim ersten Zugriff (Lazy-Loading mit Lock).
- Test-Mode-Filter verhindert Laden unerwünschter Plugins im Test-Kontext.
- **FEHLEND**: Integration mit einem `PluginActivationService` zur Filterung aktiver Plugins.

## `PluginSelectionService`
Datei: `src/Softwareschmiede/Application/Services/PluginSelectionService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetStoredDefaultPluginPrefixAsync()` | Public | Liest gespeicherten Default-Plugin-Prefix aus der DB |
| `SaveDefaultPluginPrefixAsync()` | Public | Speichert Default-Plugin-Prefix (global) |
| `SaveProjectDefaultPluginPrefixAsync()` | Public | Speichert Default-Plugin-Prefix (pro Projekt) |
| `ResolveSourceCodeManagementPluginAsync()` | Public | Löst effektives SCM-Plugin auf: explizite Auswahl → Default → Fallback |
| `GetAvailableKiPluginPrefixesAsync()` | Public | Gibt alle verfügbaren KI-Plugin-Prefixe zurück |
| `ResolveDevelopmentAutomationPluginAsync()` | Public | Löst effektives KI-Plugin auf: explizite Auswahl → Default → Fallback |
| `ResolveDevelopmentAutomationPluginWithProjectScopeAsync()` | Public | Löst KI-Plugin mit Projekt-Kontext auf |

### Abhängigkeiten
- Nutzt `IPluginManager` zur Abfrage verfügbarer Plugins
- Nutzt `PluginDefaultSettingsService` zur Persistierung von Default-Auswahlen

### Hinweise
- `GetAvailableKiPluginPrefixesAsync()` (Zeile 56-65) gibt ungefilterte Plugins zurück.
- **FEHLEND**: Integration mit Aktivierungsstatus — sollte nur aktive Plugins zurückgeben.

## `PluginSettingsService`
Datei: `src/Softwareschmiede/Application/Services/PluginSettingsService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetAllPlugins()` | Public | Gibt alle übergebenen Git- und KI-Plugins als flache Liste zurück |
| `GetValue()` | Public | Gibt Einstellungswert für ein Plugin-Feld aus dem Credential Store |
| `SetValue()` | Public | Speichert Einstellungswert für ein Plugin-Feld im Credential Store |
| `DeleteValue()` | Public | Löscht Einstellungswert für ein Plugin-Feld |
| `HasValue()` | Public | Prüft, ob für ein Feld bereits ein Wert gespeichert ist |

### Hinweise
- Speichert Einstellungen im `ICredentialStore` (nicht in der Datenbank).
- Schlüsselformat: `<PluginPrefix>.<FieldKey>`

## `AppEinstellungService`
Datei: `src/Softwareschmiede/Application/Services/AppEinstellungService.cs` (kurze Untersuchung erforderlich)

- Verwaltet Schlüssel-Wert-Paare aus der `AppEinstellung`-Tabelle
- Wird von `SettingsViewModel` zur Persistierung von Einstellungen genutzt
- **Wird potenziell genutzt zur Persistierung des Plugin-Aktivierungsstatus** (falls JSON-Spalte gewählt wird)

### Hinweise
- `AppEinstellungService` nutzt das Datenbank-Kontext (`SoftwareschmiededDbContext`)
- Existierende Keys: `DefaultKiPluginKey`, `DefaultScmPluginKey`, `DesignModeKey`, etc.
