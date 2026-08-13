# Bestandsaufnahme: IDE-Plugin-System

Diese Bestandsaufnahme dokumentiert die bestehende Plugin-Architektur des Softwareschmiede-Repositories bezogen auf die Anforderung zur Einführung eines IDE-Plugin-Systems (siehe `requirement.md`).

Der Fokus liegt auf den Komponenten, die für IDE-Plugins relevant sind oder erweitert werden müssen: Plugin-Interfaces, Enums, Services für Aktivierung und Auswahl, IDE-Öffnen-Funktionalität, sowie die bestehenden Settings-UI und Persistierungs-Infrastruktur.

---

## Zusammenfassung

### Bestehende Plugin-Architektur (Git & KI)

Das Repository hat bereits ein ausgereiftes Plugin-System für zwei Kategorien:

1. **SCM-Plugins (Source Code Management)** – Implementieren `IGitPlugin`:
   - Beispiele: GitHub, GitLab, BitBucket, LocalDirectory
   - Verwaltet Repositories, Branches, Pull Requests, Issues
   - Unterstützt Remote und lokale Git-Operationen

2. **KI-Plugins (Development Automation)** – Implementieren `IKiPlugin`:
   - Beispiele: GitHub Copilot, Claude CLI, Codex, Devin
   - Startet CLI-Prozesse mit konfigurierbaren Parametern
   - Unterstützt Session-Fortsetzung

### Infrastruktur für IDE-Plugin-System vorhanden

**Bestehende Komponenten, die wiederverwendet werden können:**

- **`PluginManager`** – Lädt und verwaltet Plugins nach PluginType
- **`PluginActivationService`** – Speichert/prüft Aktivierungsstatus pro Plugin (via AppEinstellungService)
- **`PluginSelectionService`** – Löst effektive Plugin-Instanz auf (explizit → Stored → Fallback)
- **`AppEinstellungService`** – Persistiert Key-Value-Paare in DB
- **`SettingsViewModel`** – Zeigt Plugins in der UI an mit Aktivierungsstatus
- **`PluginActivationEntry`** – ViewModel-Eintrag für Plugin-Liste
- **`IdeOeffnenService`** – Aktuelle IDE-Öffnen-Logik (muss refaktoriert werden)

### Was noch fehlt (zu implementieren)

1. **`IIdePlugin`** – Neues Interface für IDE-Plugins (erbt von `IPlugin`)
   - `CheckCompatibilityAsync(repositoryPath)` → `IdePluginCompatibility`
   - `OpenRepositoryAsync(repositoryPath)`

2. **`IdePluginCompatibility`** – Enum oder ValueObject für Kompatibilitätsprüfung
   - `Explicit` – IDE explizit kompatibel
   - `Fallback` – IDE als Rückfalllösung
   - `Incompatible` – IDE nicht kompatibel

3. **Konkrete IDE-Plugins:**
   - `VisualStudioIdePlugin` – Prüft auf `.sln`/`.slnx`-Dateien
   - `VisualStudioCodeIdePlugin` – Immer Fallback, öffnet als Verzeichnis

4. **`PluginManager`-Erweiterungen:**
   - `GetIdePlugins()` → `IReadOnlyList<IIdePlugin>`
   - `GetDefaultIdePlugin()` → `IIdePlugin`
   - Erkennung von `IIdePlugin` in Plugin-Discovery

5. **`PluginActivationService`-Erweiterungen:**
   - `GetEnabledIdePluginsAsync()` → `IReadOnlyList<IIdePlugin>`

6. **`PluginSelectionService`-Erweiterungen:**
   - `ResolveIdePluginAsync()` – IDE-Plugin-Auflösung mit Kompatibilitätsprüfung
   - Unterstützung von `plugins.ide.order`-Setting für Reihenfolge

7. **`PluginKategorie`-Erweiterung:**
   - Neuer Wert: `Ide`

8. **UI-Erweiterungen (SettingsViewModel/SettingsView):**
   - Neue „IDE-Plugins"-Sektion neben SCM/KI
   - Aktivierungs-Checkboxen
   - Reihenfolge-Management (Drag & Drop oder Up/Down-Buttons)
   - Validierung: Mindestens 1 IDE-Plugin muss aktiv sein

9. **Refaktorierung:**
   - `IdeOeffnenService` – Delegiert an `IIdePlugin.OpenRepositoryAsync()`
   - Optional: Deprecation-Hinweise auf öffentliche Methoden

### Persistierungs-Schlüssel (AppEinstellungService)

Bestehend:
- `plugins.enabled.<PluginPrefix>` – Aktivierungsstatus
- `ki.plugin.default`, `scm.plugin.default` – Standard-Plugins

Neu zu unterstützen:
- `plugins.ide.order` – Reihenfolge der IDE-Plugins (komma-getrennte Prefixe)

---

## Details

### [Interfaces](inventory/interfaces.md)

Detaillierte Übersicht der bestehenden und zu implementierenden Plugin-Interfaces:
- `IPlugin` (Basis)
- `IGitPlugin` (SCM)
- `IKiPlugin` (KI)
- `IPluginManager` (Manager)
- `IVisualStudioCodeLocator` (IDE-Hilfsmittel)
- **Zu implementieren: `IIdePlugin`**

---

### [Enums](inventory/enums.md)

Übersicht der Enums für Plugin-Kategorisierung und -Typen:
- `PluginKategorie` (Git, Ki, **zu erweitern: Ide**)
- `PluginType` (SourceCodeManagement, DevelopmentAutomation)
- **Zu implementieren: `IdePluginCompatibility`**

---

### [Services](inventory/services.md)

Detaillierte Übersicht der Services für Plugin-Management:
- `PluginManager` – Discovery und Registrierung
- `PluginActivationService` – Aktivierungsstatus
- `PluginSelectionService` – Plugin-Auflösung
- `IdeOeffnenService` – IDE-Öffnen (zu refaktorieren)
- `AppEinstellungService` – Persistierung

---

### [UI-Komponenten](inventory/ui-components.md)

Übersicht der ViewModel- und View-Komponenten:
- `SettingsViewModel` – Zentrale Verwaltung
- `PluginActivationEntry` – Listeneintrag für Plugins
- `PluginSettingEntry` / `PluginSettingGroupEntry` – Einstellungen-UI
- **Zu erweitern: IDE-Plugins-Sektion in Settings**

---

### [Tests](inventory/tests.md)

Übersicht der bestehenden Test-Klassen für Plugins:
- Unit-Tests: `PluginSelectionServiceTests`, `PluginActivationServiceTests`, `IdeOeffnenServiceTests`
- E2E-Tests: `E2E_PluginAktivierung`, `E2E_PluginAuswahlUndWechsel`
- **Zu implementieren: Tests für IDE-Plugin-Kompatibilität und -Auswahl**

---

## Architektur-Übersicht

```
IPlugin (Basis-Interface)
├─ IGitPlugin (SCM-Plugins: GitHub, GitLab, etc.)
├─ IKiPlugin (KI-Plugins: Copilot, Claude CLI, etc.)
└─ IIdePlugin (IDE-Plugins: Visual Studio, VS Code)

PluginManager (registriert und gibt Plugins nach Typ zurück)
├─ GetSourceCodeManagementPlugins() → List<IGitPlugin>
├─ GetDevelopmentAutomationPlugins() → List<IKiPlugin>
└─ GetIdePlugins() (NEU) → List<IIdePlugin>

PluginActivationService (filtert aktive Plugins)
├─ GetEnabledSourceCodeManagementPluginsAsync()
├─ GetEnabledDevelopmentAutomationPluginsAsync()
└─ GetEnabledIdePluginsAsync() (NEU)

PluginSelectionService (löst beste Plugin-Instanz auf)
├─ ResolveSourceCodeManagementPluginAsync()
├─ ResolveDevelopmentAutomationPluginAsync()
└─ ResolveIdePluginAsync() (NEU, mit Kompatibilitätsprüfung)

AppEinstellungService (persistiert Einstellungen)
└─ Schlüssel: plugins.enabled.<Prefix>, plugins.ide.order (NEU)

SettingsViewModel (zeigt Plugins in UI an)
├─ SourceCodeManagementPlugins (Observable)
├─ DevelopmentAutomationPlugins (Observable)
└─ DevelopmentEnvironmentPlugins (NEU, Observable)
```

---

## Schlüssel-Erkenntnisse

1. **Plugin-Discovery ist zentralisiert:** Der `PluginManager` lädt alle verfügbaren Plugins einmalig (Lazy-Init mit Double-Locking) aus dem `plugins/`-Verzeichnis. IDE-Plugins werden analog registriert.

2. **Aktivierungsstatus ist persistent:** Der `PluginActivationService` speichert den Aktivierungsstatus in der DB via `AppEinstellungService` unter Schlüsseln wie `plugins.enabled.Softwareschmiede.VisualStudio`. Default bei fehlendem Eintrag: aktiviert.

3. **Plugin-Auswahl ist mehrstufig:** Der `PluginSelectionService` folgt einem klaren Muster:
   - Explizite Auswahl (falls vorhanden)
   - Gespeicherter Standard
   - Fallback (erste verfügbare/sortierte Instanz)
   - Globaler Default (via PluginManager)

4. **IDE-Plugins brauchen Kompatibilitätsprüfung:** Im Gegensatz zu SCM/KI-Plugins, die registriert und aktiviert werden, müssen IDE-Plugins zusätzlich die Repository-Kompatibilität prüfen. Die neue Methode `CheckCompatibilityAsync()` ersetzt teilweise die alte Logik von `IdeOeffnenService.FindeSolutions()`.

5. **Settings-UI folgt bestehendem Muster:** Die IDE-Plugins-Sektion wird analog zu SCM/KI-Plugins implementiert: ObservableCollection von `PluginActivationEntry`, Checkboxen für Aktivierung, optional Reihenfolge-Control.

6. **Reihenfolge-Persistierung ist neu:** Ein neuer Schlüssel `plugins.ide.order` speichert die priorisierte Reihenfolge. Die Auflösungslogik in `PluginSelectionService.ResolveIdePluginAsync()` iteriert in dieser Reihenfolge über die aktivierten Plugins.

---

## Abhängigkeitsanalyse

### Abhängigkeiten der neuen IDE-Plugin-Komponenten

- `IIdePlugin` → `IPlugin`, `IdePluginCompatibility`
- `VisualStudioIdePlugin`, `VisualStudioCodeIdePlugin` → `IIdePlugin`, `IProzessStarter`, `IVisualStudioCodeLocator`
- `PluginManager` → ändert sich, um `IIdePlugin` zu unterstützen (Discovery, Registrierung)
- `PluginActivationService` → neue Methode `GetEnabledIdePluginsAsync()` (folgt bestehendem Pattern)
- `PluginSelectionService` → neue Methode `ResolveIdePluginAsync()` (mit Kompatibilitätsprüfung)
- `SettingsViewModel` → neue Properties für IDE-Plugins (analog SCM/KI)
- `IdeOeffnenService` → wird refaktoriert, um an IDE-Plugins zu delegieren (ggf. deprecated)

### Keine Breaking Changes

Die bestehenden SCM- und KI-Plugin-Interfaces und Services bleiben unverändert. IDE-Plugins werden als separate Kategorie/Liste parallel registriert und verwaltet.

---

## Offene Fragen aus der Anforderung

1. **Mehrfach-Installation Visual Studios:** Sollte `VisualStudioIdePlugin` zwischen mehreren VS-Versionen auswählen, oder nur die neueste verwenden? *(Klärung erforderlich)*

2. **Fallback-Plugin-Auswahl:** Wenn mehrere IDE-Plugins nur `Fallback` zurückgeben, welches sollte bevorzugt werden — das erste in der Konfiguration oder das erste in der Entdeckungsreihenfolge? *(Klärung erforderlich)*

3. **Minimale Anforderung an aktive Plugins:** Muss mindestens ein IDE-Plugin aktiv sein (analog zu SCM/KI), oder kann der Benutzer alle IDE-Plugins deaktivieren? *(Klärung erforderlich)*

4. **Fensterbehandlung:** Soll der IDE-Aufrufdialog mit einer Auswahlliste ergänzt werden, wenn mehrere IDE-Plugins aktiv sind? Oder wird stets automatisch das beste Plugin verwendet? *(Klärung erforderlich)*

5. **Legacy-Integration:** Sollten die bisherigen öffentlichen Methoden von `IdeOeffnenService` (`FindeSolutions`, `OeffneSolution`, `OeffneVisualStudioCode`) deprecated werden, oder parallel existieren? *(Klärung erforderlich)*

6. **Kompatibilitäts-Caching:** Sollten Kompatibilitätsprüfungen gecacht werden, oder bei jedem Aufruf neu durchgeführt werden? *(Klärung erforderlich)*

