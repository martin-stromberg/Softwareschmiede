# Bestandsaufnahme: WPF-Plugin-Verfügbarkeit für Repository-Zuweisungsdialog

Analyse des Projektcodes zur Anforderung **Dialog für Repository-Zuweisung mit SCM-Plugin-Auswahl**. Diese Bestandsaufnahme dokumentiert, welche Klassen, Interfaces, Enums und Services bereits vorhanden sind und wie sie die Plugin-Auswahl und Repository-Verwaltung unterstützen.

## Zusammenfassung

Die Infrastruktur für die Plugin-Auswahl ist bereits vorhanden:

- **IPluginManager** ist implementiert und lädt Source-Code-Management-Plugins (IGitPlugin) dynamisch
- **RepositoryAssignViewModel** existiert, lädt Repositories via `ProjektService` in `LadenAsync()`
- **RepositoryAssignDialog** (XAML + Code-Behind) zeigt Repositories in einer ListBox
- **IDialogService** abstrahiert den Dialog-Zugriff und wird vom `WpfDialogService` implementiert
- **Datenmodelle** (GitRepository, Projekt) sind strukturiert und enthalten bereits PluginTyp
- **Tests** für ProjektService existieren, aber keine spezifischen Tests für RepositoryAssignViewModel
- **Converter** (BoolToVisibilityConverter) sind verfügbar für bedingte UI-Anzeige
- **Dark-Mode Problem**: Der "Zuweisen"-Button hat `Foreground="White"` hardcodiert (Zeile 72 in XAML)

## Details

- [Datenmodell](inventory/models.md)
- [Services und Logik](inventory/logic.md)
- [Interfaces](inventory/interfaces.md)
- [Converter und UI-Utilities](inventory/converters.md)
- [Tests](inventory/tests.md)
