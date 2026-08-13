# Umsetzungsplan: IDE-Plugin-System

## Übersicht

Das bestehende IDE-Aufrufsystem (`IdeOeffnenService`) wird durch ein erweiterbares Plugin-System abgelöst. IDE-Plugins werden analog zu bestehenden SCM- und KI-Plugins registriert, können ihrer Kompatibilität zu einem Repository prüfen (`Explicit`, `Fallback`, `Incompatible`) und werden von einer zentralen Auswahl-Logik (`PluginSelectionService.ResolveIdePluginAsync()`) basierend auf Aktivierungsstatus, Reihenfolge und Kompatibilität ausgewählt. Die Implementierung umfasst zwei konkrete IDE-Plugins (`VisualStudioIdePlugin` für `.sln`/`.slnx`-Dateien, `VisualStudioCodeIdePlugin` als universeller Fallback), Erweiterungen der bestehenden Plugin-Infrastruktur sowie eine neue Settings-UI-Sektion zur Verwaltung von IDE-Plugin-Aktivierung und -Reihenfolge.

---

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| IDE-Plugin-Kategorie | Neue Kategorie parallel zu Git/KI, eigene `_idePlugins`-Liste im `PluginManager` | Konsistent mit bestehender Plugin-Architektur; keine Breaking Changes an bestehenden Kategorien |
| Kompatibilitätsprüfung | Separate async Methode `CheckCompatibilityAsync()` auf `IIdePlugin` | Ermöglicht repository-spezifische Prüfung zur Laufzeit; Kompatibilität ist dynamisch (nicht zur Plugin-Initialisierung bekannt) |
| Reihenfolge-Persistierung | Neuer Setting-Schlüssel `plugins.ide.order` mit komma-getrennten Prefixen | Analog zur bestehenden Persistierungs-Infrastruktur; optional und dezentral verwaltet |
| Aktivierungsstatus | Standard-Schlüssel `plugins.enabled.<IdePluginPrefix>` | Folgt bestehendem Muster für SCM/KI-Plugins; Konsistenz in der Benutzeroberfläche |
| Fallback-Auswahl bei mehreren Fallback-Plugins | Erstes Plugin in konfigurierter Reihenfolge (`plugins.ide.order`) | Gibt Benutzer explizite Kontrolle über Priorität; transparent und nachvollziehbar |
| IDE-Plugin-Registrierung | Built-in in `PluginManager`, nicht in externer DLL (analog zu Git/KI heute) | VS und VS Code sind Kern-IDEs; keine dynamische Registrierung erforderlich; vereinfacht Test- und Deployment-Infrastruktur |
| Minimum-Anforderung aktiver Plugins | Mindestens ein IDE-Plugin muss aktiv bleiben | Verhindert, dass Benutzer alle Plugins deaktiviert (und damit IDE-Öffnen kaputt) — analog zu SCM/KI |
| IdeOeffnenService-Refaktorierung | Delegiert an `ResolveIdePluginAsync()` und `IIdePlugin.OpenRepositoryAsync()`; besteht für Kompatibilität weiter | Ermöglicht schrittweise Migration; verhindert Breaking Changes für bestehende Caller |

---

## Programmabläufe

### IDE-Plugin-Auswahl beim IDE-Aufruf

Beschreibt die Abfolge, wenn der Benutzer „IDE öffnen" triggert (z. B. Ribbon-Button, Kontextmenü):

1. Aufruf von `IdeOeffnenService.OeffneIde(repositoryPath)` oder neuer Methode `OpenRepositoryInIdeAsync(repositoryPath)`
2. Methode ruft `PluginSelectionService.ResolveIdePluginAsync(repositoryPath, ct)` auf
3. `ResolveIdePluginAsync()` ruft `PluginActivationService.GetEnabledIdePluginsAsync(ct)` auf, um nur aktivierte Plugins zu erhalten
4. Falls `plugins.ide.order` Setting existiert, sortiert `ResolveIdePluginAsync()` die aktivierten Plugins nach dieser Reihenfolge; sonst: Entdeckungsreihenfolge
5. Für jedes Plugin in Reihenfolge:
   - Ruft `plugin.CheckCompatibilityAsync(repositoryPath, ct)` auf
   - Falls `Explicit` zurückkommt: wählt dieses Plugin und bricht ab
   - Falls `Fallback` zurückkommt: merkt sich dieses Plugin, setzt fort
   - Falls `Incompatible` zurückkommt: setzt fort
6. Falls ein `Fallback`-Plugin gefunden wurde: nutzt dieses
7. Falls kein Plugin kompatibel: ruft `PluginManager.GetDefaultIdePlugin()` auf (liefert das erste registrierte Plugin)
8. Ruft `selectedPlugin.OpenRepositoryAsync(repositoryPath, ct)` auf
9. `VisualStudioIdePlugin.OpenRepositoryAsync()` sucht `.sln`-Datei, öffnet sie via `IProzessStarter.Start(ProcessStartInfo { FileName = slnPath, UseShellExecute = true })`
10. `VisualStudioCodeIdePlugin.OpenRepositoryAsync()` öffnet Verzeichnis via `IProzessStarter.Start(ProcessStartInfo { FileName = "code", Arguments = quotedPath })`

Beteiligte Klassen: `IdeOeffnenService`, `PluginSelectionService`, `PluginActivationService`, `PluginManager`, `IIdePlugin`, `VisualStudioIdePlugin`, `VisualStudioCodeIdePlugin`, `IProzessStarter`, `AppEinstellungService`

### IDE-Plugin-Aktivierung in der UI

Beschreibt den Ablauf, wenn der Benutzer in der Settings-UI ein IDE-Plugin aktiviert/deaktiviert:

1. Benutzer öffnet Settings → Plugins-Tab
2. `SettingsViewModel` lädt `IdePlugins` via `PluginManager.GetIdePlugins()`
3. Für jedes IDE-Plugin: `PluginActivationService.IsPluginEnabledAsync(pluginPrefix, ct)` abfragen, um ObservableCollection `DevelopmentEnvironmentPlugins` zu befüllen
4. Benutzer klickt Checkbox bei IDE-Plugin
5. `SettingsViewModel` ruft `PluginActivationService.SetPluginEnabledAsync(pluginPrefix, newValue, ct)` auf
6. Service speichert Setting `plugins.enabled.<IdePluginPrefix>` via `AppEinstellungService`
7. Falls Benutzer versucht, das letzte aktive Plugin zu deaktivieren: Validierung zeigt Fehlermeldung, Aktion wird abgebrochen

Beteiligte Klassen: `SettingsViewModel`, `SettingsView`, `PluginManager`, `PluginActivationService`, `AppEinstellungService`

### IDE-Plugin-Reihenfolge-Verwaltung in der UI

Beschreibt den Ablauf für Drag & Drop oder Up/Down-Buttons zur Reihenfolgeanpassung:

1. `SettingsViewModel.IdePluginOrder` ist ObservableCollection oder sortierte Liste von Plugin-Prefixen
2. Initial befüllt aus `AppEinstellungService.GetSettingAsync("plugins.ide.order", ct)` (falls vorhanden, sonst Entdeckungsreihenfolge)
3. Benutzer zieht IDE-Plugin an neue Position oder klickt Up/Down-Button
4. `SettingsViewModel` aktualisiert `IdePluginOrder`-Collection
5. Bei Speichern (oder Auto-Save): `AppEinstellungService.SetSettingAsync("plugins.ide.order", kommaGetrennteString, ct)`
6. Neue Reihenfolge wird in `ResolveIdePluginAsync()` beim nächsten IDE-Aufruf berücksichtigt

Beteiligte Klassen: `SettingsViewModel`, `SettingsView`, `AppEinstellungService`, `PluginSelectionService`

---

## Neue Klassen

| Klasse | Typ | Ort | Zweck |
|--------|-----|-----|-------|
| `IIdePlugin` | Interface | `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IIdePlugin.cs` | Schnittstelle für IDE-Plugin-Implementierungen; definiert `CheckCompatibilityAsync()` und `OpenRepositoryAsync()` |
| `IdePluginCompatibility` | Enum | `src/Softwareschmiede.Plugin.Contracts/Domain/Enums/IdePluginCompatibility.cs` | Kompatibilitätsergebnis: `Explicit`, `Fallback`, `Incompatible` |
| `VisualStudioIdePlugin` | Klasse | `src/Softwareschmiede/Domain/PluginImpl/VisualStudioIdePlugin.cs` | IDE-Plugin für Visual Studio; prüft auf `.sln`/`.slnx`-Dateien, meldet `Explicit` bei Fund, `Incompatible` sonst |
| `VisualStudioCodeIdePlugin` | Klasse | `src/Softwareschmiede/Domain/PluginImpl/VisualStudioCodeIdePlugin.cs` | IDE-Plugin für VS Code; meldet immer `Fallback`, öffnet Verzeichnis mit `code` Befehl |

---

## Änderungen an bestehenden Klassen

### `PluginKategorie` (Enum)

- **Neue Werte:** `Ide` — Kategorie für IDE-Integration-Plugins

### `PluginType` (Enum)

- **Hinweis:** Ggf. muss ein neuer Wert `Ide` hinzugefügt werden, falls die `PluginManager.TryCreateAndRegister()`-Logik einen switch-Fall auf `PluginType` hat. Alternativ können IDE-Plugins unter einen bestehenden Typ fallen. (Siehe Offene Punkte.)

### `IPlugin` (Interface)

- **Keine Änderungen erforderlich** — `IIdePlugin` erbt von `IPlugin`, nutzt bestehende Properties `PluginName`, `PluginPrefix`, `PluginType`, `GetSettingGroups()`

### `PluginManager` (Klasse)

- **Neue Eigenschaft (Private):** `_idePlugins` (List<IIdePlugin>) — Speichert geladene IDE-Plugins
- **Neue Methode (Public):** `GetIdePlugins()` → `IReadOnlyList<IIdePlugin>` — Gibt alle geladenen IDE-Plugins zurück
- **Neue Methode (Public):** `GetDefaultIdePlugin()` → `IIdePlugin` — Gibt das erste registrierte IDE-Plugin zurück oder wirft InvalidOperationException
- **Geänderte Methode:** `LoadPluginsFromDll()` (Private) — Muss zusätzlich zu `IGitPlugin` und `IKiPlugin` auf `IIdePlugin` prüfen und in `_idePlugins` registrieren
- **Geänderte Methode:** `TryCreateAndRegister()` (Private) — Muss im switch-Statement einen Fall für IDE-Plugins hinzufügen (Registrierung in `_idePlugins`)
- **Geänderte Methode:** `EnsureInitialized()` (Private) — Initialisiert auch `_idePlugins` als leere Liste

### `PluginActivationService` (Service)

- **Neue Methode (Public):** `GetEnabledIdePluginsAsync(ct)` → `IReadOnlyList<IIdePlugin>` — Filtert IDE-Plugins nach Aktivierungsstatus, nutzt bestehende `FilterEnabledAsync<IIdePlugin>()`-Logik
- **Hinweis:** Bestehende Methoden `IsPluginEnabledAsync()` und `SetPluginEnabledAsync()` funktionieren bereits generisch mit IDE-Plugin-Prefixen

### `PluginSelectionService` (Service)

- **Neue Methode (Public):** `ResolveIdePluginAsync(repositoryPath, ct = default)` → `Task<IIdePlugin>`:
  1. Ruft `PluginActivationService.GetEnabledIdePluginsAsync(ct)` auf
  2. Falls keine aktiven Plugins: ruft `PluginManager.GetDefaultIdePlugin()` auf und gibt diese zurück
  3. Liest Setting `plugins.ide.order` via `AppEinstellungService.GetSettingAsync("plugins.ide.order", ct)`
  4. Sortiert aktivierte Plugins nach in `plugins.ide.order` definierter Reihenfolge (sonst Entdeckungsreihenfolge)
  5. Iteriert über sortierte Plugins:
     - Ruft `plugin.CheckCompatibilityAsync(repositoryPath, ct)` auf
     - Falls `Explicit`: gibt dieses Plugin zurück
     - Falls `Fallback`: merkt sich, setzt fort
  6. Falls kein `Explicit`-Plugin: gibt gemerkt `Fallback`-Plugin zurück
  7. Falls kein Plugin kompatibel: gibt `PluginManager.GetDefaultIdePlugin()` zurück

### `IdeOeffnenService` (Service)

- **Neue Methode (Public, Async):** `OpenRepositoryInIdeAsync(repositoryPath, ct = default)` → `Task`:
  1. Ruft `PluginSelectionService.ResolveIdePluginAsync(repositoryPath, ct)` auf
  2. Ruft `selectedPlugin.OpenRepositoryAsync(repositoryPath, ct)` auf
- **Geänderte Methode:** `OeffneSolution()` (Public) — Optional: Kann mit Deprecation-Hinweis versehen werden; delegiert intern an `OpenRepositoryInIdeAsync()` oder behält alte Logik (siehe Offene Punkte)
- **Hinweis:** Bestehende Methoden `FindeSolutions()`, `OeffneVisualStudioCode()`, `IstVisualStudioCodeVerfuegbar()` bleiben zur Rückwärts-Kompatibilität erhalten

### `SettingsViewModel` (ViewModel)

- **Neue Eigenschaft:** `IdePlugins` (IReadOnlyList<IIdePlugin>) — Rohe IDE-Plugin-Liste (geladen via `PluginManager.GetIdePlugins()`)
- **Neue Eigenschaft:** `DevelopmentEnvironmentPlugins` (ObservableCollection<PluginActivationEntry>) — IDE-Plugins mit Aktivierungsstatus (analog `SourceCodeManagementPlugins`, `DevelopmentAutomationPlugins`)
- **Neue Eigenschaft:** `DefaultIdePlugin` (string?) — Aktuell gewähltes Standard-IDE-Plugin-Prefix (optional, je nach UI-Design)
- **Neue Eigenschaft:** `IdePluginOrder` (List<string>) — Reihenfolge der IDE-Plugin-Prefixe (aus Setting `plugins.ide.order`)
- **Neue Eigenschaft:** `SelectedIdePlugins` (PluginActivationEntry?) — Im IDE-Plugins-Register ausgewählter Eintrag
- **Neue Eigenschaft:** `SelectedIdePluginSettings` (IReadOnlyList<PluginSettingGroupEntry>?) — Einstellungsgruppen des ausgewählten IDE-Plugins
- **Neue Event-Handler:** Auf `IsEnabled`-Änderung in `DevelopmentEnvironmentPlugins` reagieren:
  - Validierung: Falls nur noch ein Plugin aktiv ist, keine weitere Deaktivierung erlauben
  - Persistierung: `PluginActivationService.SetPluginEnabledAsync()` aufrufen
- **Neue Befehle:** 
  - `IdePluginSelectedCommand` — Wird ausgelöst wenn Nutzer ein IDE-Plugin im Register wählt (lädt Einstellungsgruppen)
  - `IdePluginMoveUpCommand` (optional, falls Up/Down-Buttons) — Verschiebt Plugin in Reihenfolge nach oben
  - `IdePluginMoveDownCommand` (optional, falls Up/Down-Buttons) — Verschiebt Plugin nach unten
- **Neue Initialization-Logik:** Beim Laden der Settings:
  - `PluginManager.GetIdePlugins()` aufrufen
  - Für jedes IDE-Plugin: `PluginActivationService.IsPluginEnabledAsync()` abfragen
  - `AppEinstellungService.GetSettingAsync("plugins.ide.order", ct)` auslesen und in `IdePluginOrder` parsen

### `SettingsView` (XAML)

- **Neue Sektion im Plugins-Tab:** Neue Gruppe „Integrierte Entwicklungsumgebungen (IDE)" neben bestehenden SCM/KI-Gruppen
- **Neue ListBox/DataGrid:** Für `DevelopmentEnvironmentPlugins` ObservableCollection mit:
  - Aktivierungs-CheckBox pro IDE-Plugin (bindet auf `PluginActivationEntry.IsEnabled`)
  - Plugin-Name anzeigen
  - Klick-Event um `IdePluginSelectedCommand` zu triggern
- **Neue Reihenfolge-Controls:** Für `IdePluginOrder` Verwaltung:
  - Option 1: Drag & Drop Sortierung (xaml.cs Code-Behind mit Drag-Event-Handler)
  - Option 2: Up/Down-Buttons pro Eintrag (bindet auf `IdePluginMoveUpCommand`, `IdePluginMoveDownCommand`)
- **Validierungs-Feedback:** Falls Benutzer versucht, das letzte Plugin zu deaktivieren:
  - CheckBox bleibt checked (Binding mit Validierung)
  - Optional: Tooltip oder Meldung "Sie müssen mindestens ein IDE-Plugin aktiviert lassen"
- **Neue Einstellungs-Panel:** Falls ausgewähltes IDE-Plugin Einstellungsfelder hat, diese im bestehenden `SelectedPluginSettings`-Panel anzeigen (analog zu SCM/KI-Plugins)

---

## Datenbankmigrationen

**Keine Datenbankmigrationen erforderlich.** 

Die Persistierung erfolgt über die bestehende `AppEinstellung`-Tabelle via `AppEinstellungService`. Neue Einträge werden On-Demand angelegt:
- `plugins.enabled.Softwareschmiede.VisualStudio` (true/false)
- `plugins.enabled.Softwareschmiede.VisualStudioCode` (true/false)
- `plugins.ide.order` (komma-getrennte Prefixe, z. B. `Softwareschmiede.VisualStudio,Softwareschmiede.VisualStudioCode`)

---

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall / Maßnahme |
|---------------|-------|---------------------|
| IDE-Plugin-Aktivierung | Mindestens ein IDE-Plugin muss aktiv bleiben | CheckBox-Deaktivierung wird verhindert; optional Warnung angezeigt |
| `plugins.ide.order` Setting | Darf nur Prefixe aktiv registrierter IDE-Plugins enthalten | Ungültige Prefixe werden beim Lesen ignoriert; wird bei Speichern bereinigt |
| `repositoryPath` in `CheckCompatibilityAsync()` | Darf nicht null oder leer sein | `ArgumentNullException` oder `ArgumentException` werfen |
| `repositoryPath` in `OpenRepositoryAsync()` | Darf nicht null oder leer sein | `ArgumentNullException` oder `ArgumentException` werfen |

---

## Konfigurationsänderungen

| Eintrag | Typ | Speicherort | Standardwert | Zweck |
|---------|-----|-----------|--------------|-------|
| `plugins.enabled.Softwareschmiede.VisualStudio` | bool (als "True"/"False") | `AppEinstellung`-Tabelle | true | Aktivierungsstatus für Visual Studio IDE-Plugin |
| `plugins.enabled.Softwareschmiede.VisualStudioCode` | bool (als "True"/"False") | `AppEinstellung`-Tabelle | true | Aktivierungsstatus für Visual Studio Code IDE-Plugin |
| `plugins.ide.order` | string (komma-getrennte Prefixe) | `AppEinstellung`-Tabelle | (leer/nicht vorhanden) | Reihenfolge-Priorisierung; wird bei fehlendem Wert durch Entdeckungsreihenfolge ersetzt |

---

## Seiteneffekte und Risiken

- **IDE-Öffnen-Dialog / -Ribbon-Button:** Alle Caller von `IdeOeffnenService.OeffneSolution()` müssen weiterhin funktionieren. Falls die Methode refaktoriert wird, muss die Refaktorierung transparent sein (Delegation an neue Logik). Risiko: Benutzer können IDE nicht öffnen, wenn Refaktorierung fehlerhaft ist.

- **Settings-UI:** Neue Sektion kann zu UI-Layout-Änderungen führen (Scroll-Bar, Größe). Muss bei E2E-Tests für Settings berücksichtigt werden.

- **PluginManager-Initialisierung:** Neue IDE-Plugins werden beim Laden des PluginManager registriert. Lazy-Initialization-Logik mit Doppel-Locking muss korrekt erweitert werden, sonst können Race-Conditions entstehen.

- **Performance:** Kompatibilitätsprüfung (`CheckCompatibilityAsync()`) wird für jedes IDE-Plugin sequenziell aufgerufen. Falls Prüfung langsam ist (z. B. Dateisuche), kann IDE-Öffnen verzögert werden. Abhilfe: Ggf. Caching implementieren (aber aktuell nicht geplant, da Anforderung offen ist).

- **Legacy-Code:** Bestehende Tests für `IdeOeffnenService` können weiterhin gegen alte Methoden laufen; müssen aber evtl. angepasst werden, wenn Implementierung geändert wird (siehe Tests-Sektion).

- **PluginType-Enum:** Falls neuer `PluginType.Ide`-Wert hinzukommt, müssen alle Switch-Statements in `PluginManager` und anderen Services angepasst werden. Risiko: Compiler-Fehler bei unvollständigen Patterns.

---

## Umsetzungsreihenfolge

### Phase 1: Fundament (Enums, Interfaces, Basisklassen)

1. **`IdePluginCompatibility`-Enum erstellen**
   - Voraussetzungen: Keine
   - Beschreibung: Neue Enum-Datei `src/Softwareschmiede.Plugin.Contracts/Domain/Enums/IdePluginCompatibility.cs` mit Werten `Explicit`, `Fallback`, `Incompatible`

2. **`IIdePlugin`-Interface erstellen**
   - Voraussetzungen: `IPlugin`-Interface existiert (vorhanden); `IdePluginCompatibility`-Enum (aus Schritt 1)
   - Beschreibung: Neue Interface-Datei `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IIdePlugin.cs`, erbt von `IPlugin`, definiert `CheckCompatibilityAsync(repositoryPath, ct)` und `OpenRepositoryAsync(repositoryPath, ct)`

3. **`PluginKategorie.Ide`-Wert hinzufügen**
   - Voraussetzungen: `PluginKategorie`-Enum existiert (vorhanden)
   - Beschreibung: Existierende Enum-Datei `src/Softwareschmiede/Domain/Enums/PluginKategorie.cs` um Wert `Ide` erweitern

4. **Neuen `PluginType` für IDE-Plugins hinzufügen (optional, je nach Architektur)**
   - Voraussetzungen: `PluginType`-Enum existiert (vorhanden)
   - Beschreibung: Falls erforderlich, neuen Wert in `src/Softwareschmiede.Plugin.Contracts/Domain/Enums/PluginType.cs` hinzufügen. Klärung: Siehe Offene Punkte.

### Phase 2: IDE-Plugin-Implementierungen

5. **`VisualStudioIdePlugin`-Klasse implementieren**
   - Voraussetzungen: `IIdePlugin`-Interface (aus Schritt 2); `IProzessStarter`-Interface (vorhanden); `IdePluginCompatibility`-Enum (aus Schritt 1)
   - Beschreibung: Neue Klasse `src/Softwareschmiede/Domain/PluginImpl/VisualStudioIdePlugin.cs`:
     - Implementiert `IIdePlugin`
     - Properties: `PluginName` = "Visual Studio", `PluginPrefix` = "Softwareschmiede.VisualStudio", `PluginType` = DevelopmentAutomation (oder neuer Typ)
     - `CheckCompatibilityAsync()`: Sucht nach `.sln` oder `.slnx`-Dateien im Repo-Root, gibt `Explicit` bei Fund, sonst `Incompatible`
     - `OpenRepositoryAsync()`: Findet erste `.sln`-Datei, öffnet via `IProzessStarter` mit `UseShellExecute = true`
     - `GetSettingGroups()`: Gibt leere Liste zurück (kein Konfigurationsfelder)

6. **`VisualStudioCodeIdePlugin`-Klasse implementieren**
   - Voraussetzungen: `IIdePlugin`-Interface (aus Schritt 2); `IProzessStarter`-Interface (vorhanden); `IVisualStudioCodeLocator`-Interface (vorhanden)
   - Beschreibung: Neue Klasse `src/Softwareschmiede/Domain/PluginImpl/VisualStudioCodeIdePlugin.cs`:
     - Implementiert `IIdePlugin`
     - Properties: `PluginName` = "Visual Studio Code", `PluginPrefix` = "Softwareschmiede.VisualStudioCode", `PluginType` = DevelopmentAutomation (oder neuer Typ)
     - `CheckCompatibilityAsync()`: Gibt immer `IdePluginCompatibility.Fallback` zurück
     - `OpenRepositoryAsync()`: Öffnet Verzeichnis via `IProzessStarter` mit Befehl `code` und gequottem Pfad
     - `GetSettingGroups()`: Gibt leere Liste zurück

### Phase 3: Plugin-Manager-Erweiterung

7. **`PluginManager` um IDE-Plugin-Unterstützung erweitern**
   - Voraussetzungen: `IIdePlugin`-Interface (aus Schritt 2); `VisualStudioIdePlugin` und `VisualStudioCodeIdePlugin` (aus Schritt 5–6)
   - Beschreibung:
     - Neue Private-Eigenschaft `_idePlugins` (List<IIdePlugin>) initialisieren
     - Neue Public-Methode `GetIdePlugins()` → `IReadOnlyList<IIdePlugin>` hinzufügen
     - Neue Public-Methode `GetDefaultIdePlugin()` → `IIdePlugin` hinzufügen (wirft InvalidOperationException bei leerer Liste)
     - `EnsureInitialized()` um Initialisierung von `_idePlugins` erweitern
     - `LoadPluginsFromDll()` um Prüfung auf `IIdePlugin` erweitern
     - `TryCreateAndRegister()` um Handling für IDE-Plugins erweitern
     - **Wichtig:** Built-in IDE-Plugins (`VisualStudioIdePlugin`, `VisualStudioCodeIdePlugin`) müssen im Initialisierer (z. B. in `EnsureInitialized()` oder dedizierter Methode) direkt registriert werden, nicht aus DLL geladen

8. **Built-in IDE-Plugins im `PluginManager` registrieren**
   - Voraussetzungen: `PluginManager` erweitert (aus Schritt 7); IDE-Plugin-Klassen (aus Schritt 5–6)
   - Beschreibung: IDE-Plugins als Singletons instantiieren und in `_idePlugins`-Liste registrieren (z. B. in `EnsureInitialized()` nach DLL-Scan)

### Phase 4: Service-Erweiterungen

9. **`PluginActivationService` um IDE-Plugin-Methode erweitern**
   - Voraussetzungen: `PluginManager` erweitert (aus Schritt 7); `PluginActivationService` existiert (vorhanden)
   - Beschreibung:
     - Neue Public-Methode `GetEnabledIdePluginsAsync(ct)` → `IReadOnlyList<IIdePlugin>` hinzufügen
     - Ruft bestehende `FilterEnabledAsync<IIdePlugin>()`-Logik mit IDE-Plugins auf

10. **`PluginSelectionService` um IDE-Plugin-Auflösungsmethode erweitern**
    - Voraussetzungen: `PluginSelectionService` existiert (vorhanden); `PluginActivationService` erweitert (aus Schritt 9); `AppEinstellungService` existiert (vorhanden); `IdePluginCompatibility`-Enum (aus Schritt 1)
    - Beschreibung:
      - Neue Public-Methode `ResolveIdePluginAsync(repositoryPath, ct = default)` → `Task<IIdePlugin>` hinzufügen
      - Implementiert Ablauf: Aktivierte Plugins laden → sortieren nach `plugins.ide.order` → kompatible Prüfen → `Explicit` → `Fallback` → Default
      - Hilfsmethode zum Parsen von `plugins.ide.order` String (komma-getrennte Prefixe)

11. **`IdeOeffnenService` refaktorieren**
    - Voraussetzungen: `PluginSelectionService` erweitert (aus Schritt 10); `IdeOeffnenService` existiert (vorhanden)
    - Beschreibung:
      - Neue Methode `OpenRepositoryInIdeAsync(repositoryPath, ct = default)` → `Task` hinzufügen (ruft `ResolveIdePluginAsync()` auf, dann `plugin.OpenRepositoryAsync()`)
      - Optional: Bestehende Methoden mit Delegation à la intern `OpenRepositoryInIdeAsync()` aufrufen (für Kompatibilität)
      - Optional: Deprecation-Hinweis auf öffentliche Methoden (XML-Docs oder [Obsolete]-Attribut)

### Phase 5: ViewModel und UI

12. **`SettingsViewModel` um IDE-Plugin-Properties erweitern**
    - Voraussetzungen: `SettingsViewModel` existiert (vorhanden); `PluginManager` erweitert (aus Schritt 7); `PluginActivationService` erweitert (aus Schritt 9); `AppEinstellungService` existiert (vorhanden)
    - Beschreibung:
      - Neue Properties: `IdePlugins`, `DevelopmentEnvironmentPlugins` (ObservableCollection<PluginActivationEntry>), `DefaultIdePlugin`, `IdePluginOrder`, `SelectedIdePlugins`, `SelectedIdePluginSettings`
      - Neue Event-Handler: Auf `IsEnabled`-Änderung reagieren (Validierung + Persistierung)
      - Neue Commands: `IdePluginSelectedCommand`, optional `IdePluginMoveUpCommand`, `IdePluginMoveDownCommand`
      - Initialization-Logik: IDE-Plugins laden, Aktivierungsstatus abfragen, `plugins.ide.order` lesen

13. **`SettingsView` (XAML) um IDE-Plugins-Sektion erweitern**
    - Voraussetzungen: `SettingsView` existiert (vorhanden); `SettingsViewModel` erweitert (aus Schritt 12)
    - Beschreibung:
      - Neue UI-Sektion „Integrierte Entwicklungsumgebungen (IDE)" im Plugins-Tab hinzufügen (nach SCM/KI)
      - ListBox/DataGrid für `DevelopmentEnvironmentPlugins` mit Checkboxen
      - Reihenfolge-Controls (Drag & Drop oder Up/Down-Buttons)
      - Validierungs-UI (Tooltip/Warnung bei letztem Plugin)
      - Bindungen: CheckBox → `IsEnabled`, Button-Klicks → `IdePluginSelectedCommand`, etc.

### Phase 6: Unit-Tests

14. **Unit-Tests für `VisualStudioIdePlugin.CheckCompatibilityAsync()` schreiben**
    - Voraussetzungen: `VisualStudioIdePlugin` implementiert (aus Schritt 5)
    - Beschreibung:
      - Test: `.sln`-Datei vorhanden → `Explicit` zurückgeben
      - Test: `.slnx`-Datei vorhanden → `Explicit` zurückgeben
      - Test: Keine `.sln`/`.slnx`-Datei → `Incompatible` zurückgeben
      - Test: Mehrere `.sln`-Dateien → erstes zurück, `Explicit` zurückgeben
      - Test: Null-Pfad → Exception werfen
      - Test: Leerer Pfad → Exception werfen
      - Test: Nicht-existenter Pfad → `Incompatible` zurückgeben

15. **Unit-Tests für `VisualStudioCodeIdePlugin.CheckCompatibilityAsync()` schreiben**
    - Voraussetzungen: `VisualStudioCodeIdePlugin` implementiert (aus Schritt 6)
    - Beschreibung:
      - Test: Beliebiger Pfad → `Fallback` zurückgeben
      - Test: Null-Pfad → Exception werfen
      - Test: Leerer Pfad → Exception werfen

16. **Unit-Tests für `PluginSelectionService.ResolveIdePluginAsync()` schreiben**
    - Voraussetzungen: `PluginSelectionService` erweitert (aus Schritt 10); Test-Infrastruktur für `PluginActivationService` + `AppEinstellungService` (vorhanden)
    - Beschreibung:
      - Test: Ein Plugin aktiv, `Explicit` → gibt dieses Plugin zurück
      - Test: Mehrere Plugins, erste `Explicit` → gibt erste zurück
      - Test: Erste `Fallback`, zweite `Explicit` → gibt zweite zurück
      - Test: Erste `Incompatible`, zweite `Fallback` → gibt zweite zurück
      - Test: Reihenfolge aus `plugins.ide.order` beachtet
      - Test: Keine aktivierten Plugins → gibt Default zurück
      - Test: Kein Plugin kompatibel → gibt Default zurück

17. **Unit-Tests für neue/geänderte `PluginManager`-Methoden schreiben**
    - Voraussetzungen: `PluginManager` erweitert (aus Schritt 7)
    - Beschreibung:
      - Test: `GetIdePlugins()` gibt registrierte IDE-Plugins zurück
      - Test: `GetDefaultIdePlugin()` gibt erstes Plugin zurück
      - Test: `GetDefaultIdePlugin()` wirft InvalidOperationException bei leerer Liste

18. **Unit-Tests für `PluginActivationService.GetEnabledIdePluginsAsync()` schreiben**
    - Voraussetzungen: `PluginActivationService` erweitert (aus Schritt 9)
    - Beschreibung:
      - Test: Aktivierte Plugins werden zurückgegeben
      - Test: Deaktivierte Plugins werden herausgefiltert
      - Test: Leere Liste bei allen deaktiviert

### Phase 7: E2E-Tests

19. **E2E-Test für IDE-Plugin-Auswahl schreiben**
    - Voraussetzungen: App startet (vorhanden); Test-Infrastruktur für WPF E2E (vorhanden)
    - Beschreibung:
      - Szenario 1: Repository mit `.sln`-Datei → IDE-Öffnen wählt Visual Studio (`Explicit`)
      - Szenario 2: Repository ohne `.sln` → IDE-Öffnen wählt VS Code (`Fallback`)
      - Szenario 3: Visual Studio deaktiviert → IDE-Öffnen wählt VS Code
      - Verifiziere: Korrekte IDE wird gestartet

20. **E2E-Test für IDE-Plugin-Aktivierung in Settings schreiben**
    - Voraussetzungen: SettingsView geladen (vorhanden); Test-Infrastruktur (vorhanden)
    - Beschreibung:
      - Settings öffnen → Plugins-Tab
      - Visual Studio deaktivieren → Speichern
      - Settings neuladen → Visual Studio bleibt deaktiviert
      - Verifiziere: Aktivierungsstatus wird persistiert

21. **E2E-Test für IDE-Plugin-Reihenfolge in Settings schreiben**
    - Voraussetzungen: SettingsView geladen (vorhanden); E2E-Test-Infrastruktur (vorhanden)
    - Beschreibung (je nach UI-Implementierung):
      - Drag & Drop oder Up/Down: VS Code nach oben verschieben
      - Speichern
      - Settings neuladen → Reihenfolge ist geändert
      - IDE-Öffnen in Repository ohne `.sln` → VS Code wird bevorzugt (höhere Priorität)
      - Verifiziere: Reihenfolge wird korrekt angewendet

22. **E2E-Test für IDE-Öffnen mit refaktoriertem `IdeOeffnenService` anpassen (falls betroffen)**
    - Voraussetzungen: `IdeOeffnenService` refaktoriert (aus Schritt 11)
    - Beschreibung:
      - Bestehende Tests für IDE-Öffnen weiterhin funktionsfähig
      - Falls Refaktorierung externe Caller betrifft, Tests anpassen

### Phase 8: Validierung und Dokumentation

23. **Validierungslogik für IDE-Plugin-Aktivierung implementieren**
    - Voraussetzungen: `SettingsViewModel` erweitert (aus Schritt 12); `SettingsView` erweitert (aus Schritt 13)
    - Beschreibung:
      - Im ViewModel: CheckBox-Deaktivierung blockieren, wenn letztes Plugin aktiv ist
      - UI-Feedback: Tooltip oder Meldung anzeigen
      - Unit-Tests: Validierungslogik testen

24. **Build- und Test-Verifikation durchführen**
    - Voraussetzungen: Alle vorherigen Schritte abgeschlossen
    - Beschreibung:
      - `dotnet build` für volle Lösung (ohne `--no-build`)
      - `dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category!=OsInterface"` ausführen
      - Alle Unit-Tests müssen green sein
      - Ggf. `--filter "Category=OsInterface"` separat laufen lassen (E2E)

---

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `CheckCompatibilityAsync_ShouldReturnExplicit_WhenSlnExists` | `VisualStudioIdePluginTests` | `.sln`-Datei im Root wird gefunden, `Explicit` zurückgegeben |
| `CheckCompatibilityAsync_ShouldReturnExplicit_WhenSlnxExists` | `VisualStudioIdePluginTests` | `.slnx`-Datei im Root wird gefunden, `Explicit` zurückgegeben |
| `CheckCompatibilityAsync_ShouldReturnIncompatible_WhenNoSlnFound` | `VisualStudioIdePluginTests` | Keine `.sln`/`.slnx`-Datei, `Incompatible` zurückgegeben |
| `CheckCompatibilityAsync_ShouldThrowArgumentNullException_WhenPathIsNull` | `VisualStudioIdePluginTests` | Null-Pfad wirft Exception |
| `CheckCompatibilityAsync_ShouldThrowArgumentException_WhenPathIsEmpty` | `VisualStudioIdePluginTests` | Leerer Pfad wirft Exception |
| `CheckCompatibilityAsync_ShouldReturnFallback_Always` | `VisualStudioCodeIdePluginTests` | VS Code meldet immer `Fallback` |
| `OpenRepositoryAsync_ShouldCallProzessStarter_WithCodeCommand` | `VisualStudioCodeIdePluginTests` | `IProzessStarter` wird mit `code`-Befehl aufgerufen |
| `ResolveIdePluginAsync_ShouldReturnExplicitPlugin_WhenAvailable` | `PluginSelectionServiceTests` | Plugin mit `Explicit` wird bevorzugt |
| `ResolveIdePluginAsync_ShouldReturnFallbackPlugin_WhenNoExplicitAvailable` | `PluginSelectionServiceTests` | Plugin mit `Fallback` wird nach `Explicit` bevorzugt |
| `ResolveIdePluginAsync_ShouldRespectPluginOrder_FromSetting` | `PluginSelectionServiceTests` | `plugins.ide.order` Setting wird beachtet |
| `ResolveIdePluginAsync_ShouldReturnDefaultPlugin_WhenNoPluginActive` | `PluginSelectionServiceTests` | Default-Plugin wird zurückgegeben, wenn keine Plugins aktiv |
| `GetIdePlugins_ShouldReturnRegisteredPlugins` | `PluginManagerTests` | `GetIdePlugins()` gibt registrierte IDE-Plugins zurück |
| `GetDefaultIdePlugin_ShouldReturnFirstPlugin` | `PluginManagerTests` | `GetDefaultIdePlugin()` gibt erstes Plugin zurück |
| `GetDefaultIdePlugin_ShouldThrowInvalidOperationException_WhenNoPluginsRegistered` | `PluginManagerTests` | `GetDefaultIdePlugin()` wirft Exception bei leerer Liste |
| `GetEnabledIdePluginsAsync_ShouldFilterByActivationStatus` | `PluginActivationServiceTests` | `GetEnabledIdePluginsAsync()` filtert nur aktive Plugins |
| `IsPluginEnabledAsync_ShouldReturnTrueByDefault` | `PluginActivationServiceTests` | Neues IDE-Plugin ist standardmäßig aktiviert |
| `SetPluginEnabledAsync_ShouldPersistIdePluginActivation` | `PluginActivationServiceTests` | Aktivierungsstatus wird persistiert |
| `E2E_IdePluginSelection_RepositoryWithSln` | `E2E_IdePluginSelection` | Visual Studio wird für Repository mit `.sln` ausgewählt |
| `E2E_IdePluginSelection_RepositoryWithoutSln` | `E2E_IdePluginSelection` | VS Code wird für Repository ohne `.sln` ausgewählt |
| `E2E_IdePluginSelection_VisualStudioDisabled` | `E2E_IdePluginSelection` | VS Code wird bevorzugt, wenn Visual Studio deaktiviert |
| `E2E_IdePluginActivationInSettings` | `E2E_IdePluginSettings` | IDE-Plugin-Aktivierung wird in Settings persistiert |
| `E2E_IdePluginOrder_DragDrop` (oder `_UpDownButtons`) | `E2E_IdePluginSettings` | Reihenfolge wird korrekt geändert und persistiert |
| `CreateIdePlugin(name, prefix, compatibility)` | `PluginSelectionServiceTests` (Hilfsmethode) | Mock IDE-Plugin für Tests |
| `CreateAppEinstellungService(settings)` | `PluginSelectionServiceTests` (Hilfsmethode) | Test-AppEinstellungService mit vordefinierten Settings |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `IdeOeffnenServiceTests.FindeSolutions_*` | Falls `IdeOeffnenService` refaktoriert wird, kann Testlogik angepasst sein |
| `IdeOeffnenServiceTests.OeffneSolution_*` | Falls Refaktorierung erfolgt, Tests müssen ggf. neue Methode `OpenRepositoryInIdeAsync()` testen oder alt Methoden weiterhin validieren |
| `IdeOeffnenServiceTests.OeffneVisualStudioCode_*` | Ähnlich wie OeffneSolution |
| `E2E_PluginAktivierung` | Falls E2E-Test-Infrastruktur mit IDE-Plugin-Aktivierung erweitert wird (optional) |
| `E2E_PluginAuswahlUndWechsel` | Ggf. muss E2E-Test-Framework auf IDE-Plugins erweitert werden (optional) |
| `E2E_SettingsKiPluginPersistence` | Falls Settings-E2E-Tests erweitert werden um IDE-Plugins (optional) |

Falls keine Breaking Changes in bestehenden Klassen auftreten: Viele Tests können unverändert bleiben, da sie Mock-Objekte verwenden.

### E2E-Tests (Pflicht)

| Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium |
|----------|------------------------|-------------------------------|
| Repository mit `.sln`-Datei → IDE öffnen | `E2E_IdePluginSelection` | Visual Studio wird automatisch ausgewählt und gestartet |
| Repository ohne `.sln`-Datei → IDE öffnen | `E2E_IdePluginSelection` | VS Code wird automatisch ausgewählt und gestartet |
| Visual Studio deaktiviert → IDE öffnen in Repo mit `.sln` | `E2E_IdePluginSelection` | VS Code wird als Fallback verwendet |
| IDE-Plugin in Settings aktivieren/deaktivieren | `E2E_IdePluginSettings` | Aktivierungsstatus wird in DB persistiert und beim nächsten Laden korrekt angewendet |
| IDE-Plugin-Reihenfolge ändern (Drag & Drop oder Buttons) | `E2E_IdePluginSettings` | Neue Reihenfolge wird in DB persistiert; nächster IDE-Aufruf respektiert Reihenfolge |
| Letztes aktives IDE-Plugin deaktivieren versuchen | `E2E_IdePluginSettings` | UI blockiert Deaktivierung oder zeigt Fehlermeldung; Plugin bleibt aktiv |

Welche bestehenden E2E-Tests müssen angepasst werden?

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `E2E_PluginAktivierung` (falls existiert) | Ggf. müssen IDE-Plugins in Test-Daten initialisiert werden (optional) |
| Tests die `IdeOeffnenService` direkt aufrufen | Falls Refaktorierung erfolgt, müssen Aufrufe evtl. an neue Methoden angepasst werden |
| Beliebige E2E-Tests mit IDE-Interaktion | Falls App-Workflow IDE-Öffnen involviert, Tests müssen neue Plugin-Auswahl-Logik berücksichtigen |

Falls keine Breaking Changes: Falls `IdeOeffnenService.OeffneSolution()` weiterhin funktioniert (Delegation), viele Tests unverändert.

---

## Offene Punkte

**Wichtig:** Diese Fragen sind bisher unbeantwortet und müssen vor/während der Implementierung geklärt werden. Einige haben Empfehlungen.

| # | Offener Punkt | Empfohlener Vorschlag |
|---|---------------|----------------------|
| 1 | **Mehrfach-Installation Visual Studios:** Sollte `VisualStudioIdePlugin` zwischen mehreren VS-Versionen auswählen, oder nur die neueste verwenden? | Empfehlung: **Neueste Version verwenden**. `VisualStudioIdePlugin.CheckCompatibilityAsync()` sollte alle `.sln`-Dateien finden, aber `OpenRepositoryAsync()` öffnet die erste/älteste `.sln` mit dem Betriebssystem-Standardhandler (nicht VS-Version-spezifisch). Falls Benutzer mehrere VS-Versionen hat, das Betriebssystem entscheidet, welche öffnet. Vereinfachter Ansatz: Keine aktive Versionspriorisierung, Windows-Registry/Standardhandler nutzen. |
| 2 | **Fallback-Plugin-Auswahl:** Wenn mehrere IDE-Plugins nur `Fallback` zurückgeben (z. B. VS Code und VS in Zukunft), welches sollte bevorzugt werden — das erste in der Konfiguration oder das erste in der Entdeckungsreihenfolge? | Empfehlung: **Erstes in konfigurierter Reihenfolge (`plugins.ide.order`)**. Gibt Benutzer explizite Kontrolle; wenn `plugins.ide.order` nicht gesetzt ist, wird Entdeckungsreihenfolge genutzt. Vorhersehbar und consistent. |
| 3 | **Minimale Anforderung an aktive Plugins:** Muss mindestens ein IDE-Plugin aktiv sein (analog zu SCM/KI), oder kann der Benutzer alle IDE-Plugins deaktivieren? | Empfehlung: **Ja, mindestens ein Plugin muss aktiv bleiben**. Begründung: Wenn alle deaktiviert, kann Benutzer IDE nicht öffnen (fehlende Fallback-Strategie). Analog zu SCM/KI-Plugins. SettingsViewModel sollte Deaktivierung des letzten aktiven Plugins blockieren. |
| 4 | **Fensterbehandlung:** Soll der IDE-Aufrufdialog mit einer Auswahlliste ergänzt werden, wenn mehrere IDE-Plugins aktiv sind? Oder wird stets automatisch das beste Plugin verwendet? | Empfehlung: **Automatisches Best-Plugin, kein Dialog**. Begründung: Der Anforderungstext spricht von „automatischer Auswahl" (erstes mit `Explicit`, dann Fallback). Dialog würde UI komplexer machen und Benutzer-Erlebnis verlangsamen. Falls Benutzer explizite Auswahl möchte, kann er IDE deaktivieren/priorisieren in Settings. |
| 5 | **Legacy-Integration:** Sollten die bisherigen öffentlichen Methoden von `IdeOeffnenService` (`FindeSolutions`, `OeffneSolution`, `OeffneVisualStudioCode`) deprecated werden, oder parallel existieren? | Empfehlung: **Parallel existieren, optional Deprecation-Hinweise**. Begründung: Minimiert Breaking Changes; Caller haben Zeit, auf neue Logik zu migrieren. Öffentliche Methoden können auf neue interne `OpenRepositoryInIdeAsync()` delegieren oder alt Logik behalten (mit Deprecation-Attribut oder XML-Hinweis). |
| 6 | **Kompatibilitäts-Caching:** Sollten Kompatibilitätsprüfungen gecacht werden, oder bei jedem Aufruf neu durchgeführt werden? | Empfehlung: **Nicht cachen, bei jedem Aufruf prüfen**. Begründung: Kompatibilitätsprüfung ist schnell (Datei-Existenz-Prüfung), gelegentliche Aufrufe; Caching würde unnötige Komplexität einführen. Falls Performance-Problem: später cacheable. |
| 7 | **`PluginType` für IDE-Plugins:** Sollte ein neuer Enum-Wert `PluginType.Ide` eingeführt werden, oder können IDE-Plugins unter einen bestehenden Typ fallen? | Empfehlung: **Neuer `PluginType.Ide`-Wert**. Begründung: Klarheit und Konsistenz mit bestehenden `PluginType.SourceCodeManagement` und `PluginType.DevelopmentAutomation`. Ermöglicht zukünftige Plug-in-Spezifik. (Falls aktuell nur Built-in-Plugins: nicht kritisch, aber empfohlen.) |

---

## Zusammenfassung der Reihenfolge-Abhängigkeiten

```
1. IdePluginCompatibility-Enum
   ├─ 2. IIdePlugin-Interface (benötigt IdePluginCompatibility)
   │  ├─ 5. VisualStudioIdePlugin (benötigt IIdePlugin, IProzessStarter)
   │  ├─ 6. VisualStudioCodeIdePlugin (benötigt IIdePlugin, IProzessStarter, IVisualStudioCodeLocator)
   │  │  ├─ 7. PluginManager-Erweiterung (benötigt IIdePlugin)
   │  │  ├─ 8. Built-in-Registrierung (benötigt IDE-Plugin-Klassen)
   │  │  ├─ 9. PluginActivationService-Erweiterung (benötigt PluginManager)
   │  │  └─ 10. PluginSelectionService-Erweiterung (benötigt PluginActivationService, AppEinstellungService, IdePluginCompatibility)
   │  │     └─ 11. IdeOeffnenService-Refaktorierung (benötigt PluginSelectionService)
   │  │        └─ 12. SettingsViewModel-Erweiterung (benötigt PluginManager, PluginActivationService, AppEinstellungService)
   │  │           └─ 13. SettingsView-Erweiterung (benötigt SettingsViewModel)
   │  │
   │  ├─ 14–18. Unit-Tests (benötigen implementierte Klassen)
   │  └─ 19–22. E2E-Tests (benötigen SettingsView + IdeOeffnenService)
   │
   ├─ 3. PluginKategorie.Ide (independent)
   └─ 4. PluginType.Ide (optional, independent)
```
