# Services – Bestandsaufnahme IDE-Plugin-System

## Plugin-Manager

### `PluginManager`
Datei: `src/Softwareschmiede/Infrastructure/Plugins/PluginManager.cs`

Lädt Plugins dynamisch aus dem Unterordner `plugins` und registriert sie nach `PluginType`.

| Methode | Sichtbarkeit | Beschreibung |
|---------|-------------|-------------|
| `GetSourceCodeManagementPlugins()` | Public | Gibt alle geladenen SCM-Plugins (IGitPlugin) zurück |
| `GetDevelopmentAutomationPlugins()` | Public | Gibt alle geladenen Development-Automation-Plugins (IKiPlugin) zurück |
| `GetDefaultSourceCodeManagementPlugin()` | Public | Gibt das erste verfügbare SCM-Plugin oder wirft InvalidOperationException |
| `GetDefaultDevelopmentAutomationPlugin()` | Public | Gibt das priorisierte Development-Automation-Plugin zurück (Copilot bevorzugt) oder wirft InvalidOperationException |
| `EnsureInitialized()` | Private | Lazy-Initialization mit doppeltem Locking |
| `IsTestMode()` | Private Static | Prüft ob `SOFTWARESCHMIEDE_TEST_DB_PATH` gesetzt ist |
| `IsAllowedInTestMode(dllFileName)` | Private Static | Whitelist für Test-Mode (LocalDirectory, KiSimulator, ClaudeCli, Codex, Devin, GitHubCopilot) |
| `DiscoverPlugins()` | Private | Sucht und lädt DLLs aus dem Plugin-Verzeichnis |
| `LoadPluginsFromDll(dllPath)` | Private | Lädt Plugins aus einer einzelnen DLL |
| `TryCreateAndRegister(pluginType, dllPath)` | Private | Instanziiert Plugin und registriert es nach PluginType |

**Interne Felder:**
- `_gitPlugins` (List<IGitPlugin>) – Registrierte SCM-Plugins
- `_kiPlugins` (List<IKiPlugin>) – Registrierte Development-Automation-Plugins
- `_initialized` (bool) – Lazy-Initialization-Flag
- `_sync` (object) – Lock für Thread-Safety

**Zu erweitern laut Anforderung:**
- `GetIdePlugins()` → `IReadOnlyList<IIdePlugin>`
- `GetDefaultIdePlugin()` → `IIdePlugin`
- Interner `_idePlugins` (List<IIdePlugin>)
- Erkennung von `IIdePlugin` in `LoadPluginsFromDll()` (zusätzlich zu IGitPlugin und IKiPlugin)
- Registrierung im `switch`-Statement in `TryCreateAndRegister()` für PluginType `Ide` (wenn neuer PluginType hinzugefügt)

---

## Plugin-Aktivierungs-Service

### `PluginActivationService`
Datei: `src/Softwareschmiede/Application/Services/PluginActivationService.cs`

Verwaltet den benutzerspezifischen Aktivierungsstatus je Plugin und filtert Plugin-Listen entsprechend.

| Methode | Sichtbarkeit | Beschreibung |
|---------|-------------|-------------|
| `IsPluginEnabledAsync(pluginPrefix, ct)` | Public | Prüft, ob das Plugin aktiviert ist. Fehlender Eintrag bedeutet aktiviert (true) |
| `SetPluginEnabledAsync(pluginPrefix, enabled, ct)` | Public | Speichert den Aktivierungsstatus für das Plugin |
| `GetEnabledSourceCodeManagementPluginsAsync(ct)` | Public | Gibt nur aktivierte SCM-Plugins zurück |
| `GetEnabledDevelopmentAutomationPluginsAsync(ct)` | Public | Gibt nur aktivierte Development-Automation-Plugins zurück |
| `FilterEnabledAsync<TPlugin>(plugins, ct)` | Private | Generische Filterlogik für Plugins |
| `IsEnabledValue(wert)` | Private Static | Parst Wert: null/leer/true → true; "false" → false |
| `BuildKey(pluginPrefix)` | Private Static | Konstruiert Schlüssel als `"plugins.enabled." + pluginPrefix` |

**Persistierung:**
- Speichert Aktivierungsstatus via `AppEinstellungService` unter Schlüssel `plugins.enabled.<PluginPrefix>`
- Wert ist "True" oder "False"
- Default (bei fehlendem Eintrag): aktiviert (true)

**Zu erweitern laut Anforderung:**
- `GetEnabledIdePluginsAsync()` → `IReadOnlyList<IIdePlugin>` (analog zu SCM/KI)

---

## Plugin-Auswahl-Service

### `PluginSelectionService`
Datei: `src/Softwareschmiede/Application/Services/PluginSelectionService.cs`

Löst die effektive Plugin-Instanz auf (explizite Auswahl → gespeicherter Default → Fallback).

| Methode | Sichtbarkeit | Beschreibung |
|---------|-------------|-------------|
| `GetStoredDefaultPluginPrefixAsync(pluginType, ct)` | Public | Liest den gespeicherten PluginPrefix für den Plugin-Typ |
| `SaveDefaultPluginPrefixAsync(pluginType, pluginPrefix, ct)` | Public | Speichert den PluginPrefix als Standard für den Plugin-Typ |
| `SaveProjectDefaultPluginPrefixAsync(projektId, pluginType, pluginPrefix, ct)` | Public | Speichert den PluginPrefix als Projekt-Standard |
| `ResolveSourceCodeManagementPluginAsync(selectedPluginPrefix, ct)` | Public | Löst das SCM-Plugin auf (explizit → Stored → Fallback) |
| `GetAvailableKiPluginPrefixesAsync(ct)` | Public | Gibt die Prefixe aller aktiven KI-Plugins zurück |
| `ResolveDevelopmentAutomationPluginAsync(selectedPluginPrefix, ct)` | Public | Löst das KI-Plugin auf |
| `ResolveDevelopmentAutomationPluginWithProjectScopeAsync(aufgabenPluginPrefix, projektId, ct)` | Public | Löst KI-Plugin mit Projekt-Kontext auf (Aufgabe → Projekt-Default → Global-Default) |
| `ResolvePluginAsync<TPlugin>(pluginType, selectedPluginPrefix, availablePlugins, defaultResolver, fallbackSortKey, ct)` | Private | Generische Auflösungslogik |
| `TryResolveByPrefix<TPlugin>(plugins, pluginPrefix)` | Private Static | Sucht Plugin nach Prefix (case-insensitive) |
| `GetKiFallbackSortKey(plugin)` | Private Static | Copilot erhält Prefix "0-" (höhere Priorität), andere "1-" |

**Zu erweitern laut Anforderung:**
- `ResolveIdePluginAsync()` – IDE-Plugin-Auflösung mit Kompatibilitätsprüfung:
  1. Iteriert über aktivierte IDE-Plugins in konfigurierter Reihenfolge
  2. Fragt jedes Plugin via `CheckCompatibilityAsync()` ab
  3. Wählt erstes Plugin mit `Explicit`
  4. Fällt auf erstes Plugin mit `Fallback` zurück
  5. Falls kein Plugin aktiv/kompatibel: liefert `GetDefaultIdePlugin()`

---

## IDE-Öffnen-Service

### `IdeOeffnenService`
Datei: `src/Softwareschmiede/Application/Services/IdeOeffnenService.cs`

Findet `.sln`-Dateien und öffnet eine übergebene Solution.

| Methode | Sichtbarkeit | Beschreibung |
|---------|-------------|-------------|
| `FindeSolutions(arbeitsverzeichnis)` | Public | Ermittelt alle `*.sln` und `*.slnx`-Dateien auf oberster Ebene, alphabetisch sortiert. Gibt leere Liste bei fehlendem/leerem Pfad zurück |
| `OeffneSolution(solutionPfad)` | Public | Öffnet die Solution-Datei mit dem beim Betriebssystem registrierten Standardhandler (via `IProzessStarter`) |
| `IstVisualStudioCodeVerfuegbar()` | Public | Gibt an, ob Visual Studio Code aktuell auflösbar ist (via `IVisualStudioCodeLocator`) |
| `OeffneVisualStudioCode(arbeitsverzeichnis)` | Public | Öffnet das Arbeitsverzeichnis in Visual Studio Code |
| `QuoteArgument(argument)` | Private Static | Escaped Anführungszeichen im Argument für Shell-Übergabe |

**Abhängigkeiten:**
- `IProzessStarter prozessStarter` – Startet den Öffnen-Befehl
- `IVisualStudioCodeLocator visualStudioCodeLocator` – Ermittelt VS-Code-Befehl

**Laut Anforderung zu refaktorieren:**
- Wird weiterhin existieren für Kompatibilität
- Delegiert Aufrufe an `IIdePlugin.OpenRepositoryAsync()`
- Öffentliche Methoden können optional mit Deprecation-Hinweis versehen werden

---

## Einstellungs-Services

### `AppEinstellungService`
Datei: `src/Softwareschmiede/Application/Services/AppEinstellungService.cs`

Generischer Service zum Lesen und Schreiben von Anwendungseinstellungen (Key-Value-Paare).

| Methode | Sichtbarkeit | Beschreibung |
|---------|-------------|-------------|
| `GetSettingAsync(schluessel, ct)` | Public | Liest den Wert einer Einstellung (string?). Gibt null zurück wenn nicht gespeichert |
| `GetIntSettingAsync(schluessel, ct)` | Public | Liest als int?. Gibt null zurück wenn nicht parsbar |
| `GetBoolSettingAsync(schluessel, ct)` | Public | Liest als bool?. Gibt null zurück wenn nicht parsbar |
| `SetSettingAsync(schluessel, wert, ct)` | Public | Speichert oder überschreibt eine Einstellung |
| `GetSettingsAsync(keys, ct)` | Public | Liest mehrere Einstellungen in einem Batch |

**Speicherort:** `AppEinstellung`-Tabelle (Entity Framework)

**Vordefinierte Schlüssel:**
- `window.position.x`, `window.position.y`, `window.size.width`, `window.size.height` – Fensterposition
- `ui.designmode.name` – Dark-Mode-Status
- `ki.plugin.default`, `scm.plugin.default` – Standard-Plugins
- `logging.level` – Log-Level
- `ide.vscode.openWhenNoSolutionFound` – VS-Code-Fallback-Einstellung

**Zu nutzen für IDE-Plugin-Persistierung:**
- `plugins.enabled.<IdePluginPrefix>` – Aktivierungsstatus (via `PluginActivationService`)
- `plugins.ide.order` – Reihenfolge-Priorisierung (neue Einstellung laut Anforderung)

