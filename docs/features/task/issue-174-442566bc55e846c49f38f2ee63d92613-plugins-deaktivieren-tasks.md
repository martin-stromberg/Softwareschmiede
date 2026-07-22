# Tasks: Plugins deaktivieren

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Logik/Persistenz | `PluginActivationService` (scoped) anlegen mit `IsPluginEnabledAsync`, `SetPluginEnabledAsync`, `GetEnabledSourceCodeManagementPluginsAsync`, `GetEnabledDevelopmentAutomationPluginsAsync` (Persistenz über `AppEinstellung`-Key `plugins.enabled.<PluginPrefix>`) | Offen | — |
| 2 | Konfiguration | Konstante `EnabledKeyPrefix = "plugins.enabled."` in `PluginActivationService` definieren | Offen | — |
| 3 | Konfiguration/DI | `PluginActivationService` in `App.xaml.cs` als `AddScoped` registrieren | Offen | — |
| 4 | Logik | `PluginSelectionService`: `PluginActivationService` injizieren und `GetAvailableKiPluginPrefixesAsync()` auf aktive KI-Plugins filtern | Offen | — |
| 5 | UI | `PluginActivationEntry` (ViewModel-Entry mit `PluginName`, `PluginPrefix`, `IsEnabled`, `IPlugin`) anlegen | Offen | — |
| 6 | UI | `SettingsViewModel`: Collections `SourceCodeManagementPlugins`/`DevelopmentAutomationPlugins`, `SelectedPlugin`, `SelectedPluginSettings`, `PluginSelectedCommand` hinzufügen | Offen | — |
| 7 | UI | `SettingsViewModel.LadenAsync()` erweitern: Aktivierungs-Collections befüllen und `IsEnabled` je Eintrag aus `PluginActivationService` initialisieren | Offen | — |
| 8 | UI | `SettingsViewModel.SpeichernAsync()` erweitern: geänderte Aktivierungsstatus über `SetPluginEnabledAsync` persistieren | Offen | — |
| 9 | Validierung | `SettingsViewModel`: Regel „mindestens ein Plugin je Kategorie aktiv" beim Speichern/Umschalten durchsetzen | Offen | — |
| 10 | UI | `SettingsView.xaml`: Tabs „Quellcodeverwaltung" und „KI" entfernen | Offen | — |
| 11 | UI | `SettingsView.xaml`: Tab „Plugins" mit zweispaltiger Master-Detail-Oberfläche (zwei Listen + Aktivierungs-CheckBoxen + Detail-Settings) hinzufügen; Default-Plugin-Auswahl integrieren | Offen | — |
| 12 | UI | `SettingsView.xaml.cs`: Selektions-Handler für die neue Plugin-Liste ergänzen | Offen | — |
| 13 | UI | `TaskDetailViewModel`: Eigenschaft `ZeigeKiPluginAuswahl` und Single-Plugin-Logik in `LadeVerfuegbarePluginsAsync` | Offen | — |
| 14 | UI | `TaskDetailViewModel`: Aufgabenstart umgeht KI-Plugin-Auswahl-Dialog bei genau einem aktiven Plugin | Offen | — |
| 15 | UI | `TaskDetailView.xaml`: KI-Plugin-Selector-Sichtbarkeit an `ZeigeKiPluginAuswahl` binden | Offen | — |
| 16 | UI | `RepositoryAssignViewModel`: SCM-Plugins über `PluginActivationService` laden, `HasMultipleScmPlugins` einführen, zugehörige View binden | Offen | — |
| 17 | UI | `IssueCreateDialogViewModel`: Aufbau von `VerfuegbareKiPlugins` auf aktive Plugins filtern | Offen | — |
| 18 | Tests | `PluginActivationServiceTests`: Default-aktiv, Persistenz, SCM-/KI-Filterung | Offen | — |
| 19 | Tests | `PluginSelectionServiceTests`: `GetAvailableKiPluginPrefixesAsync` liefert nur aktive Prefixe | Offen | — |
| 20 | Tests | `SettingsViewModelTests`: Aktivierungs-Collections laden, Toggle+Speichern persistiert, Validierung letztes Plugin | Offen | — |
| 21 | Tests | `TaskDetailViewModel`-Test: Selector versteckt bei einem aktiven Plugin | Offen | — |
| 22 | Tests | Bestehende `SettingsViewModelTests`/`TaskDetailViewModelTestFactory` an neue Abhängigkeit `PluginActivationService` anpassen | Offen | — |
| 23 | E2E-Tests | `E2E_PluginAktivierung` (neu): SCM-Plugin deaktivieren → aus Auswahl gefiltert; Single-KI-Plugin → Auswahl verschwindet; Persistenz nach Reload | Offen | — |
| 24 | E2E-Tests | `E2E_SettingsKiPluginPersistence` an neuen Tab „Plugins" anpassen | Offen | — |
| 25 | E2E-Tests | `E2E_SettingsCommandLineParameters` an neuen Tab „Plugins" anpassen (falls über KI-/SCM-Tab navigiert) | Offen | — |
| 26 | E2E-Tests | `E2E_PluginAuswahlUndWechsel` gegen Single-Plugin-Verhalten absichern (mind. zwei aktive KI-Plugins) | Offen | — |
