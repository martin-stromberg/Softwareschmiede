# Bestandsaufnahme: WPF Plugin-Einstellungen und Styling

Diese Bestandsaufnahme analysiert den bestehenden Code für die Anforderung "WPF Plugin-Einstellungen und Styling" (Feature 72). Das Projekt benötigt die Erweiterung der Einstellungsansicht um zwei neue Register ("Quellcodeverwaltung" und "KI") mit dynamisch geladenen Plugin-spezifischen Einstellungspanels sowie einheitliche, designgültige Styles für alle Eingabekomponenten im Dark-Mode.

## Zusammenfassung

### Was vorhanden ist
- Grundstruktur für Plugin-Einstellungen: `PluginSettingGroup`, `PluginSettingField`, `PluginSettingFieldType` sind vollständig definiert
- `PluginSettingsService` für Lesen/Schreiben von Plugin-Einstellungswerten über `ICredentialStore`
- `IPluginManager` für Discovery und Zugriff auf SCM- und KI-Plugins
- Basis-`SettingsView` mit Registern für "Allgemein", "Quellcodeverwaltung" und "KI"
- Properties für `DefaultScmPlugin` (IGitPlugin-Objekt) und `DefaultKiPlugin` (String) in `SettingsViewModel`
- Existierende XAML-ResourceDictionaries mit Dark-Mode und Light-Mode Brushes/Farben
- `PluginSettingsViewModel` mit `PluginSettingEntry`, `PluginSettingGroupEntry`, `PluginWithSettingsEntry` Hilfsklassen für die automatische UI-Generierung
- Testabdeckung für `PluginSettingsService`

### Was fehlt offensichtlich noch
- Commands für Plugin-Wechsel (`ScmPluginSelectedCommand`, `KiPluginSelectedCommand`) in `SettingsViewModel`
- Properties für `ScmPluginSettings`, `SelectedScmPluginSettings`, `KiPluginSettings`, `SelectedKiPluginSettings` in `SettingsViewModel`
- Speichern und Laden von `DefaultScmPlugin` Einstellung in `SettingsViewModel` (momentan nur `DefaultKiPlugin`)
- XAML-UI in den "Quellcodeverwaltung" und "KI" Registern für die dynamische Anzeige von Plugin-Einstellungen (nur Plugin-Wahl vorhanden, keine Einstellungspanels)
- Data-Templates für verschiedene `PluginSettingFieldType`-Werte (Text, Secret, Url, Integer, Boolean, Enum, FilePath)
- Styles für `Label` und `CheckBox` in Dark- und Light-Theme
- Konsistenz bei der Speicherung von Standardplugins (DefaultScmPlugin ist IGitPlugin, DefaultKiPlugin ist String)

## Details

- [ViewModels](inventory/viewmodels.md)
- [Services](inventory/services.md)
- [Plugin-Interfaces und ValueObjects](inventory/plugin-interfaces.md)
- [XAML und Themes](inventory/xaml-themes.md)
- [Tests](inventory/tests.md)
