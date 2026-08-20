# Tasks: IDE-Plugin-System

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Datenmodell | `IdePluginCompatibility`-Enum erstellen (Werte: `Explicit`, `Fallback`, `Incompatible`) | Offen | — |
| 2 | Datenmodell | `IIdePlugin`-Interface erstellen (erbt von `IPlugin`, definiert `CheckCompatibilityAsync()` und `OpenRepositoryAsync()`) | Offen | — |
| 3 | Datenmodell | Enum-Wert `PluginKategorie.Ide` hinzufügen | Offen | — |
| 4 | Datenmodell | Enum-Wert `PluginType.Ide` hinzufügen (optional, je nach Architektur) | Offen | — |
| 5 | Plugin-Implementierung | `VisualStudioIdePlugin`-Klasse implementieren (sucht `.sln`/`.slnx`, meldet `Explicit`, öffnet via `IProzessStarter`) | Offen | — |
| 6 | Plugin-Implementierung | `VisualStudioCodeIdePlugin`-Klasse implementieren (meldet immer `Fallback`, öffnet Verzeichnis mit `code`-Befehl) | Offen | — |
| 7 | Logik | `PluginManager.GetIdePlugins()`-Methode hinzufügen | Offen | — |
| 8 | Logik | `PluginManager.GetDefaultIdePlugin()`-Methode hinzufügen | Offen | — |
| 9 | Logik | `PluginManager._idePlugins`-Feld hinzufügen und initialisieren | Offen | — |
| 10 | Logik | `PluginManager.LoadPluginsFromDll()` um `IIdePlugin`-Erkennung erweitern | Offen | — |
| 11 | Logik | `PluginManager.TryCreateAndRegister()` um IDE-Plugin-Handling erweitern | Offen | — |
| 12 | Logik | `PluginManager.EnsureInitialized()` um `_idePlugins`-Initialisierung erweitern | Offen | — |
| 13 | Logik | Built-in IDE-Plugins (`VisualStudioIdePlugin`, `VisualStudioCodeIdePlugin`) im `PluginManager` registrieren | Offen | — |
| 14 | Logik | `PluginActivationService.GetEnabledIdePluginsAsync()`-Methode hinzufügen | Offen | — |
| 15 | Logik | `PluginSelectionService.ResolveIdePluginAsync()`-Methode hinzufügen (mit Kompatibilitätsprüfung und Reihenfolge-Unterstützung) | Offen | — |
| 16 | Logik | `PluginSelectionService`-Hilfsmethode zum Parsen von `plugins.ide.order`-String hinzufügen | Offen | — |
| 17 | Logik | `IdeOeffnenService.OpenRepositoryInIdeAsync()`-Methode hinzufügen (nutzt `ResolveIdePluginAsync()` und `IIdePlugin.OpenRepositoryAsync()`) | Offen | — |
| 18 | Logik | `IdeOeffnenService` optional mit Deprecation-Hinweisen versehen (XML-Docs oder `[Obsolete]`-Attribut) | Offen | — |
| 19 | UI | `SettingsViewModel.IdePlugins`-Property hinzufügen | Offen | — |
| 20 | UI | `SettingsViewModel.DevelopmentEnvironmentPlugins`-ObservableCollection hinzufügen | Offen | — |
| 21 | UI | `SettingsViewModel.DefaultIdePlugin`-Property hinzufügen | Offen | — |
| 22 | UI | `SettingsViewModel.IdePluginOrder`-Property hinzufügen | Offen | — |
| 23 | UI | `SettingsViewModel.SelectedIdePlugins`-Property hinzufügen | Offen | — |
| 24 | UI | `SettingsViewModel.SelectedIdePluginSettings`-Property hinzufügen | Offen | — |
| 25 | UI | `SettingsViewModel.IdePluginSelectedCommand` implementieren | Offen | — |
| 26 | UI | `SettingsViewModel.IdePluginMoveUpCommand` implementieren (falls Up/Down-Buttons) | Offen | — |
| 27 | UI | `SettingsViewModel.IdePluginMoveDownCommand` implementieren (falls Up/Down-Buttons) | Offen | — |
| 28 | UI | `SettingsViewModel`-Initialisierungslogik erweitern (IDE-Plugins laden, Aktivierungsstatus abfragen, `plugins.ide.order` lesen) | Offen | — |
| 29 | UI | `SettingsViewModel`-Event-Handler für `IsEnabled`-Änderung in `DevelopmentEnvironmentPlugins` implementieren (Validierung + Persistierung) | Offen | — |
| 30 | UI | `SettingsView` um neue IDE-Plugins-Sektion im Plugins-Tab erweitern | Offen | — |
| 31 | UI | `SettingsView` mit Aktivierungs-CheckBoxen für IDE-Plugins hinzufügen | Offen | — |
| 32 | UI | `SettingsView` mit Reihenfolge-Controls (Drag & Drop oder Up/Down-Buttons) erweitern | Offen | — |
| 33 | UI | `SettingsView` mit Validierungs-UI (Tooltip/Warnung für letztes Plugin) hinzufügen | Offen | — |
| 34 | UI | `SettingsView` Bindungen für IDE-Plugin-Commands und Properties implementieren | Offen | — |
| 35 | Validierung | Validierungslogik implementieren: Mindestens ein IDE-Plugin muss aktiv bleiben | Offen | — |
| 36 | Konfiguration | Schlüssel `plugins.enabled.Softwareschmiede.VisualStudio` wird automatisch via `AppEinstellungService` persistiert | Offen | — |
| 37 | Konfiguration | Schlüssel `plugins.enabled.Softwareschmiede.VisualStudioCode` wird automatisch via `AppEinstellungService` persistiert | Offen | — |
| 38 | Konfiguration | Schlüssel `plugins.ide.order` wird automatisch via `AppEinstellungService` persistiert | Offen | — |
| 39 | Tests | Unit-Test: `VisualStudioIdePlugin.CheckCompatibilityAsync()` mit `.sln`-Datei → `Explicit` | Offen | — |
| 40 | Tests | Unit-Test: `VisualStudioIdePlugin.CheckCompatibilityAsync()` mit `.slnx`-Datei → `Explicit` | Offen | — |
| 41 | Tests | Unit-Test: `VisualStudioIdePlugin.CheckCompatibilityAsync()` ohne `.sln`/`.slnx` → `Incompatible` | Offen | — |
| 42 | Tests | Unit-Test: `VisualStudioIdePlugin.CheckCompatibilityAsync()` mit null-Pfad → Exception | Offen | — |
| 43 | Tests | Unit-Test: `VisualStudioIdePlugin.CheckCompatibilityAsync()` mit leerem Pfad → Exception | Offen | — |
| 44 | Tests | Unit-Test: `VisualStudioCodeIdePlugin.CheckCompatibilityAsync()` → immer `Fallback` | Offen | — |
| 45 | Tests | Unit-Test: `VisualStudioCodeIdePlugin.OpenRepositoryAsync()` ruft `IProzessStarter` mit `code`-Befehl auf | Offen | — |
| 46 | Tests | Unit-Test: `PluginSelectionService.ResolveIdePluginAsync()` wählt `Explicit`-Plugin | Offen | — |
| 47 | Tests | Unit-Test: `PluginSelectionService.ResolveIdePluginAsync()` wählt `Fallback`-Plugin wenn kein `Explicit` | Offen | — |
| 48 | Tests | Unit-Test: `PluginSelectionService.ResolveIdePluginAsync()` respektiert `plugins.ide.order`-Reihenfolge | Offen | — |
| 49 | Tests | Unit-Test: `PluginSelectionService.ResolveIdePluginAsync()` gibt Default-Plugin wenn keine Plugins aktiv | Offen | — |
| 50 | Tests | Unit-Test: `PluginManager.GetIdePlugins()` gibt registrierte IDE-Plugins zurück | Offen | — |
| 51 | Tests | Unit-Test: `PluginManager.GetDefaultIdePlugin()` gibt erstes Plugin zurück | Offen | — |
| 52 | Tests | Unit-Test: `PluginManager.GetDefaultIdePlugin()` wirft `InvalidOperationException` bei leerer Liste | Offen | — |
| 53 | Tests | Unit-Test: `PluginActivationService.GetEnabledIdePluginsAsync()` filtert nur aktive Plugins | Offen | — |
| 54 | Tests | Unit-Test: `PluginActivationService.IsPluginEnabledAsync()` für neues IDE-Plugin gibt `true` zurück | Offen | — |
| 55 | Tests | Unit-Test: `PluginActivationService.SetPluginEnabledAsync()` persistiert IDE-Plugin-Aktivierung | Offen | — |
| 56 | Tests | Hilfsmethode `CreateIdePlugin()` in PluginSelectionServiceTests für Mock-Plugins | Offen | — |
| 57 | Tests | Hilfsmethode `CreateAppEinstellungService()` in PluginSelectionServiceTests mit vordefinierten Settings | Offen | — |
| 58 | E2E-Tests | E2E-Test: IDE-Öffnen in Repository mit `.sln`-Datei → Visual Studio wird ausgewählt | Offen | — |
| 59 | E2E-Tests | E2E-Test: IDE-Öffnen in Repository ohne `.sln` → VS Code wird ausgewählt | Offen | — |
| 60 | E2E-Tests | E2E-Test: Visual Studio deaktiviert, IDE-Öffnen in Repo mit `.sln` → VS Code wird bevorzugt | Offen | — |
| 61 | E2E-Tests | E2E-Test: IDE-Plugin-Aktivierung in Settings wird persistiert | Offen | — |
| 62 | E2E-Tests | E2E-Test: IDE-Plugin-Reihenfolge-Änderung (Drag & Drop oder Buttons) wird persistiert | Offen | — |
| 63 | E2E-Tests | E2E-Test: Versuch, letztes aktives IDE-Plugin zu deaktivieren → wird blockiert oder Warnung angezeigt | Offen | — |
| 64 | E2E-Tests | Anpassung bestehender E2E-Tests für IDE-Öffnen (falls `IdeOeffnenService` refaktoriert wird) | Offen | — |
| 65 | Verifikation | `dotnet build` für volle Lösung durchführen (alle Compilerfehler beheben) | Offen | — |
| 66 | Verifikation | `dotnet test` mit Unit-Tests ausführen (alle Tests green) | Offen | — |
| 67 | Verifikation | `dotnet test` mit E2E-Tests ausführen (alle E2E-Tests green) | Offen | — |
