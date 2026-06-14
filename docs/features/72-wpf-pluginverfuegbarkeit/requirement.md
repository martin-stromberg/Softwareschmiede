# Kundenanforderung: Dialog für Repository-Zuweisung mit SCM-Plugin-Auswahl

## Fachliche Zusammenfassung

Der Dialog für die Zuweisung eines Repository zu einem Projekt wird erweitert, um eine explizite Auswahl des SCM-Plugins (Source Code Management) zu ermöglichen. Nach Auswahl des Plugins werden die verfügbaren Repositories aus dieser Quelle angezeigt. Falls keine SCM-Plugins vorhanden sind, wird statt der Eingabekomponenten ein Hilfe-Panel mit Instruktionen angezeigt. Zusätzlich wird ein Sichtbarkeitsproblem des „Zuweisen"-Buttons im Dark-Mode behoben.

## Betroffene Klassen und Komponenten

### UI-Komponenten / Views
- `RepositoryAssignDialog.xaml` / `RepositoryAssignDialog.xaml.cs` — Dialog-Fenster (wird erweitert um Plugin-Auswahl)
- Neues UI-Element: `PluginSelectionPanel` oder `NoPluginPanel` für Hilfetext

### ViewModels
- `RepositoryAssignViewModel` — wird erweitert:
  - Neue Property: `AvailableScmPlugins` (ObservableCollection von `IGitPlugin`)
  - Neue Property: `SelectedScmPlugin` (aktuell gewähltes SCM-Plugin)
  - Neue Property: `HasScmPlugins` (gibt an, ob SCM-Plugins verfügbar sind)
  - Event oder Command: `OnScmPluginSelected` (wird ausgelöst nach Plugin-Auswahl)
  - Abhängigkeit: `IPluginManager` (zum Abrufen der Plugins)

### Services
- `IDialogService` — möglicherweise aktualisiert (falls die Signatur des Repository-Zuweisungs-Dialogs sich ändert)
- Neue Abhängigkeit: `IPluginManager` für die Repository-Liste

### Enums / ValueObjects
- Keine neuen Klassen erforderlich (wird `IGitPlugin` verwendet)

### Tests
- `RepositoryAssignViewModelTests` — neue Testfälle:
  - Plugin-Liste wird geladen
  - Nach Plugin-Auswahl werden Repositories aktualisiert
  - Beim Fehlen von Plugins wird `HasScmPlugins = false` gesetzt

## Implementierungsansatz

### Schritt 1: ViewModel-Erweiterung
1. `RepositoryAssignViewModel` erhält Abhängigkeit zu `IPluginManager`
2. Im Konstruktor oder in einer neuen Methode `LadenAsync()` werden SCM-Plugins abgerufen: `IPluginManager.GetSourceCodeManagementPlugins()`
3. Property `AvailableScmPlugins` wird mit diesen Plugins gefüllt
4. Property `SelectedScmPlugin` wird als 2-Way-Binding in XAML verwendet
5. Ein `PropertyChangedEventHandler` oder ein Command `PluginSelectedCommand` reagiert auf Auswahl und lädt die Repositories des gewählten Plugins

### Schritt 2: Repository-Laden nach Plugin-Auswahl
1. Aktuelle Logik (`LadenAsync()` / `GetAllRepositoriesAsync`) wird analysiert
2. Die Methode wird erweitert, um das ausgewählte Plugin zu berücksichtigen
3. Falls `SelectedScmPlugin != null`, werden nur Repositories aus dieser Quelle geladen
4. Falls `SelectedScmPlugin == null`, bleibt die Liste leer oder zeigt einen Hinweis

### Schritt 3: Bedingte UI-Anzeige (Hilfe-Panel)
1. Neuer Container in XAML: `<Grid>` oder `<Viewbox>` mit zwei Child-Elementen:
   - Element A: `<StackPanel>` mit Plugin-Auswahl und Repository-Liste (sichtbar wenn `HasScmPlugins == true`)
   - Element B: `<StackPanel>` mit Hilfetext (sichtbar wenn `HasScmPlugins == false`)
2. Sichtbarkeit wird über `Visibility` und ein Converter gebunden: `{Binding HasScmPlugins, Converter={StaticResource BoolToVisibilityConverter}}`

### Schritt 4: Dark-Mode Button-Fix
1. `RepositoryAssignDialog.xaml`: Button „Zuweisen" (Zeile 68–73)
2. Problem: `Foreground="White"` ist hardcodiert
3. Lösung: `Foreground="{DynamicResource ButtonForegroundBrush}"` oder ähnliches (je nach bestehender Designkonvention)
4. Alternativ: `Foreground="{DynamicResource HighContrastForeground}"` zur Gewährleistung maximaler Lesbarkeit

## Konfiguration

Keine projektspezifische Konfiguration erforderlich. Die Plugin-Verfügbarkeit wird dynamisch über `IPluginManager` ermittelt und ist zur Laufzeit verfügbar. Falls gewünscht, könnte eine Anwendungseinstellung eingeführt werden, um bestimmte Plugin-Typen zu filtern (ist aber nicht in der Anforderung enthalten).

## Offene Fragen

1. **Fehlerbehandlung**: Falls `IPluginManager.GetSourceCodeManagementPlugins()` leer ist — soll der Dialog deaktiviert werden oder soll es nur eine Warnung geben?
2. **Standard-Plugin**: Soll nach Dialog-Öffnung automatisch das erste/Standard-Plugin ausgewählt werden, oder muss der Benutzer aktiv auswählen?
3. **Hilfe-Text Inhalt**: Wie lautet der exakte Hilfetext für den Fall, dass keine SCM-Plugins vorhanden sind? (z.B. „Kein SCM-Plugin installiert. Bitte installieren Sie ein Git-Plugin unter …")
4. **Plugin-Details**: Sollen im Dropdown die Plugin-Namen (`PluginName`) angezeigt werden oder zusätzliche Informationen (z.B. PluginType, installierte Version)?
5. **Dark-Mode Farben**: Welche Theme-Variable soll für die Button-Beschriftung verwendet werden? Bestehen bereits Konventionen im Projekt? (Überprüfung in der Ressourcen-Datei erforderlich)
6. **Abhängigkeits-Injektion**: Wie wird `IPluginManager` in `RepositoryAssignViewModel` injiziert? Über den bestehenden Konstruktor oder über eine separate Factory-Methode?
