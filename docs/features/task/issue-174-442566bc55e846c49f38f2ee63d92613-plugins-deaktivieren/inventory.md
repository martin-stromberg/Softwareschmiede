# Bestandsaufnahme: Plugins deaktivieren

Diese Analyse dokumentiert die bestehenden Plugin-Management-Komponenten des Softwareschmiede-Projekts im Hinblick auf die Implementierung eines Aktivierungsstatus-Features für Plugins.

## Zusammenfassung

### Vorhanden
- **Datenmodell:** `PluginKonfiguration` Entity hat bereits ein `Aktiviert`-Feld (bool, Standard: true)
- **Service-Logik:** `PluginManager`, `PluginSelectionService`, `PluginSettingsService` existieren und handhaben Plugin-Discovery und -Verwaltung
- **UI:** `SettingsViewModel` laden Plugins über `IPluginManager`, `TaskDetailViewModel` laden verfügbare KI-Plugins über `PluginSelectionService`
- **Tests:** Unit-Tests für `PluginManager` (Discovery), `SettingsViewModel` (Plugin-Auswahl) vorhanden

### Teilweise vorhanden
- **Aktivierungsstatus-Feld:** Existiert in `PluginKonfiguration`, wird aber **nicht persistiert** und **nicht in Filterlogik verwendet**

### Fehlend
- **IPluginManager-Methoden:** `GetEnabledSourceCodeManagementPlugins()`, `GetEnabledDevelopmentAutomationPlugins()`, `SetPluginEnabled()`, `IsPluginEnabled()`
- **Service-Klasse:** `PluginActivationService` zum Laden/Managen des In-Memory-Caches der Aktivierungsstatus
- **Persistierung:** Speicherung des Aktivierungsstatus in `AppEinstellung` (als JSON-Spalte) oder separate Tabelle + zugehörige Migration
- **SettingsViewModel-Erweiterung:** 
  - ObservableCollections mit aktivierungsfähigen Plugin-ViewModels
  - Toggle-Command zum Ändern des Aktivierungsstatus
- **Neue ViewModel-Klasse:** `IPluginActivationViewModel` (oder ähnlich) für darstellbare Plugins mit Aktivierungsstatus
- **Filterung in Views:** 
  - `TaskDetailViewModel` und `ProjectDetailViewModel` nutzen noch ungefilterte Plugin-Listen
  - Single-Plugin-Verhalten (Selector verstecken, wenn nur ein Plugin aktiv) nicht implementiert
- **UI-Komponenten:** Neuer Tab "Plugins" in `SettingsView` mit zwei-spaltiger Ansicht nicht vorhanden
- **Tests:** Keine Tests für Aktivierungsstatus-Logik, Persistierung, oder Filterung

## Details

- [Datenmodell](inventory/models.md)
- [Logik & Services](inventory/logic.md)
- [Enums](inventory/enums.md)
- [Interfaces](inventory/interfaces.md)
- [ViewModels](inventory/viewmodels.md)
- [Tests](inventory/tests.md)
