# Anforderung: IDE-Plugins

**Aufgaben-ID:** eab6dccb-4b30-4e1a-94ef-7c2417f9b297  
**Branch:** task/issue-204-eab6dccb4b304e1a94ef7c2417f9b297-ide-plugins  
**Erstellt:** 2026-08-13

## Fachliche Zusammenfassung

Das bestehende IDE-Aufrufsystem (`IdeOeffnenService`) wird durch ein erweiterbares Plugin-System abgelöst. Beim Aufruf der IDE werden alle verfügbaren IDE-Plugins abgefragt, ob sie mit dem vorliegenden Repository kompatibel sind, und das erste kompatible Plugin wird verwendet. IDE-Plugins können zwei Prüfergebnisse zurückgeben: explizite Kompatibilität (z. B. Visual Studio bei Vorhandensein von `.sln`/`.slnx`-Dateien) oder „Fallback" (z. B. Visual Studio Code als Rückfalllösung). Die Aktivierung, Deaktivierung und Priorisierung von IDE-Plugins erfolgt analog zu bestehenden Plugin-Arten über die Programmeinstellungen.

## Betroffene Klassen und Komponenten

### Neue Klassen und Interfaces

- **`IIdePlugin`** (Plugin.Contracts) — Neues Plugin-Interface für IDE-Integrationen
  - `CheckCompatibilityAsync(repositoryPath)` → `IdePluginCompatibility` (neue ValueObject/Enum mit Werten: `Explicit`, `Fallback`, `Incompatible`)
  - `OpenRepositoryAsync(repositoryPath)` — Öffnet das Repository in der IDE
  - Erbt von `IPlugin` (PluginName, PluginPrefix, GetSettingGroups, PluginType)

- **`IdePluginCompatibility`** (Enum oder ValueObject) — Prüfergebnis für IDE-Plugin-Kompatibilität
  - `Explicit` — IDE ist explizit kompatibel (z. B. `.sln` gefunden)
  - `Fallback` — IDE wird als Rückfall verwendet
  - `Incompatible` — IDE ist nicht kompatibel (wird nicht berücksichtigt)

- **`VisualStudioIdePlugin`** (Neue Plugin-Klasse)
  - Prüft auf `.sln` oder `.slnx`-Dateien im Repository-Root
  - Meldet `Explicit` bei Vorhandensein, sonst `Incompatible`
  - Öffnet eine `.sln`-Datei mit dem Standard-Handler (analog bisherige `OeffneSolution`)

- **`VisualStudioCodeIdePlugin`** (Neue Plugin-Klasse)
  - Meldet immer `Fallback` (Rückfalllösung)
  - Öffnet das Repository-Verzeichnis in VS Code (analog bisherige `OeffneVisualStudioCode`)

### Erweiterte Klassen

- **`PluginKategorie`** (Enum)
  - Neuer Wert: `Ide` (neben bestehenden `Git` und `Ki`)

- **`IPluginManager`** (Interface)
  - Neue Methode: `GetIdePlugins()` → `IReadOnlyList<IIdePlugin>`
  - Neue Methode: `GetDefaultIdePlugin()` → `IIdePlugin` (prüft Aktivierungsstatus und meldet Fallback-Plugin)

- **`PluginActivationService`** (Scoped Service)
  - Erweiterte Methoden:
    - `GetEnabledIdePluginsAsync()` — Gibt nur aktivierte IDE-Plugins zurück
    - Existierende Methoden gelten weiterhin für SCM und KI

- **`PluginSelectionService`** (Scoped Service)
  - Neue Methode: `ResolveIdePluginAsync()` — Wählt IDE-Plugin basierend auf Kompatibilität und Reihenfolge

- **`IdeOeffnenService`** (Refaktoriert)
  - Weiterhin existierend für Kompatibilität, aber delegiert an `IIdePlugin.OpenRepositoryAsync()`
  - Öffentliche Methoden können optional auf Plugin-API hinweisen (Deprecation)

### UI-Komponenten

- **SettingsView/SettingsViewModel** (erweitert)
  - IDE-Plugins-Sektion im Plugins-Tab
  - Aktivierungs-CheckBox für jedes IDE-Plugin (analog SCM/KI-Plugins)
  - Reihenfolge-Control (Drag & Drop oder Up/Down-Buttons) zur Priorisierung
  - Aktivierungs-Validierung: mindestens ein IDE-Plugin muss aktiv bleiben

### Tests

- Unit-Tests für `VisualStudioIdePlugin.CheckCompatibilityAsync()`
- Unit-Tests für `VisualStudioCodeIdePlugin.CheckCompatibilityAsync()`
- Unit-Tests für `PluginSelectionService.ResolveIdePluginAsync()`
- Integration-Tests für IDE-Aufruf mit mehreren aktivierten Plugins

## Implementierungsansatz

### Plugin-Architektur

1. **Neues Interface `IIdePlugin`:**
   - Definiert zwei Methoden:
     - `Task<IdePluginCompatibility> CheckCompatibilityAsync(string repositoryPath, CancellationToken ct = default)` — Prüft Kompatibilität
     - `Task OpenRepositoryAsync(string repositoryPath, CancellationToken ct = default)` — Öffnet Repository

2. **Plugin-Registrierung:**
   - `VisualStudioIdePlugin` und `VisualStudioCodeIdePlugin` werden beim Anwendungsstart registriert (analog zu Git/KI-Plugins)
   - `PluginManager` wird um `GetIdePlugins()` und `GetDefaultIdePlugin()` erweitert

3. **Auswahl-Logik (`PluginSelectionService.ResolveIdePluginAsync()`):**
   - Iteriert über aktivierte IDE-Plugins in konfigurierter Reihenfolge
   - Fragt jedes Plugin via `CheckCompatibilityAsync()` ab
   - Wählt erstes Plugin mit `Explicit`
   - Fällt auf erstes Plugin mit `Fallback` zurück
   - Falls kein Plugin aktiv/kompatibel: liefert `GetDefaultIdePlugin()`

4. **Aktivierungs-Integration:**
   - Nutzt bestehenden `PluginActivationService`
   - Schlüsselformat: `plugins.enabled.<IdePluginPrefix>` (z. B. `plugins.enabled.Softwareschmiede.VisualStudio`)
   - Neu zu unterstützen: Reihenfolge-Persistierung (Key `plugins.ide.order` mit durch Kommata getrennte Prefixe)

5. **Einstellungs-UI:**
   - IDE-Plugins-Liste in den Einstellungen (ähnlich SCM/KI-Plugins)
   - Reihenfolge-Verwaltung durch Persistierung im AppEinstellung-Store

### Abhängigkeiten zu bestehenden Komponenten

- `PluginActivationService` — für Aktivierungsstatus-Filterung
- `AppEinstellungService` — für Persistierung von Aktivierungsstatus und Reihenfolge
- `IPluginManager` — für Plugin-Discovery und Default-Bestimmung
- `IdeOeffnenService` — wird refaktoriert, um Aufrufe an `IIdePlugin.OpenRepositoryAsync()` zu delegieren

## Konfiguration

### Persistierung

- **Aktivierungsstatus pro IDE-Plugin:**
  - Schlüssel: `plugins.enabled.<IdePluginPrefix>`
  - Wert: `true` oder `false`
  - Speicherort: `AppEinstellung`-Tabelle
  - Default (bei fehlendem Schlüssel): `true` (aktiviert)

- **Reihenfolge/Priorisierung:**
  - Schlüssel: `plugins.ide.order`
  - Wert: Durch Kommata getrennte Liste von IDE-Plugin-Prefixen (z. B. `Softwareschmiede.VisualStudio,Softwareschmiede.VisualStudioCode`)
  - Speicherort: `AppEinstellung`-Tabelle
  - Default (bei fehlendem Schlüssel): Entdeckungsreihenfolge beibehalten

- **UI-Ort:**
  - Bestehender Plugins-Tab in Einstellungen (Menü → Einstellungen → Plugins)
  - Neue Gruppe: „IDE-Plugins" (neben bestehenden „Quellcodeverwaltungs-Plugins" und „KI-Plugins")

## Offene Fragen

1. **Mehrfach-Installation Visual Studios:** Sollte `VisualStudioIdePlugin` zwischen mehreren VS-Versionen auswählen, oder nur die neueste verwenden?
   
2. **Fallback-Plugin-Auswahl:** Wenn mehrere IDE-Plugins nur `Fallback` zurückgeben (z. B. VS Code und VS), welches sollte bevorzugt werden — das erste in der Konfiguration oder das erste in der Entdeckungsreihenfolge?

3. **Minimale Anforderung an aktive Plugins:** Muss mindestens ein IDE-Plugin aktiv sein (analog zu SCM/KI), oder kann der Benutzer alle IDE-Plugins deaktivieren?

4. **Fensterbehandlung:** Soll der IDE-Aufrufdialog mit einer Auswahlliste ergänzt werden, wenn mehrere IDE-Plugins aktiv sind? Oder wird stets automatisch das beste Plugin verwendet?

5. **Legacy-Integration:** Sollten die bisherigen öffentlichen Methoden von `IdeOeffnenService` (`FindeSolutions`, `OeffneSolution`, `OeffneVisualStudioCode`) deprecated werden, oder parallel existieren?

6. **Kompatibilitäts-Caching:** Sollten Kompatibilitätsprüfungen gecacht werden, oder bei jedem Aufruf neu durchgeführt werden?
