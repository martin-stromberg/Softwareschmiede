← [Zurück zur Übersicht](index.md)

# Repository-Suggestions

## Zweck

Das Repository-Suggestions-Panel auf der Projektübersichtsseite zeigt unzugeordnete Repositories aus allen verfügbaren SCM-Plugins an und ermöglicht es Benutzern, schnell neue Projekte zu erstellen und Repositories zuzuordnen.

## Funktionsweise

### Anzeige und Sortierung

Das Panel listet alle Repositories auf, die:
- Von mindestens einem verfügbaren SCM-Plugin stammen
- Noch nicht einem Projekt zugeordnet wurden (nicht in der `GitRepositories`-Tabelle existieren)

Die Liste wird sortiert nach:
1. **Primär:** Letztes Änderungsdatum (`UpdatedAt`) — absteigend (neueste zuerst)
2. **Sekundär:** Repository-Name — aufsteigend, alphabetisch, case-insensitive

### Relative Zeitformatierung

Das Änderungsdatum wird benutzerfreundlich formatiert:
- **< 1 Minute:** "gerade eben"
- **1-59 Minuten:** "vor X Minuten"
- **1-23 Stunden:** "vor X Stunden"
- **1-29 Tage:** "vor X Tagen"
- **30-364 Tage:** "vor X Monaten"
- **>= 365 Tage:** "vor X Jahren"
- **Fehlendes Datum:** "unbekannt"

Die Formatierung erfolgt via `UnassignedRepositoriesConverter`, eines WPF-ValueConverters.

### Projekterstellung durch Doppelklick

Ein Doppelklick auf einen Repository-Eintrag:
1. Findet das zugeordnete SCM-Plugin
2. Erstellt ein neues Projekt mit dem Repository-Namen
3. Ordnet das Repository dem Projekt zu
4. Lädt die Projekt- und Suggestions-Listen neu

Das neue Projekt erscheint sofort in den Projektkacheln, und der Repository-Eintrag verschwindet aus dem Panel.

### Fehlertoleranz

Falls ein SCM-Plugin bei der Abfrage seiner Repositories fehlschlägt:
- Das Plugin wird übersprungen
- Repositories von anderen Plugins werden weiterhin angezeigt
- Der Fehler wird im Anwendungsprotokoll vermerkt

## Beteiligte Komponenten

| Komponente | Typ | Beschreibung |
|---|---|---|
| `ProjektService.GetUnassignedRepositoriesAsync()` | Service-Methode | Aggregiert unzugeordnete Repositories aus allen SCM-Plugins, filtert Duplikate und sortiert sie |
| `ProjectListViewModel.UnassignedRepositories` | Property | ObservableCollection aller unzugeordneten Repositories für UI-Binding |
| `ProjectListViewModel.IsLoadingRepositories` | Property | Loading-Flag zur Anzeige eines Lade-Indikators im Panel |
| `ProjectListViewModel.LadenRepositorienSuggestionsAsync()` | Private Methode | Lädt Suggestions via Service und aktualisiert Collection |
| `ProjectListViewModel.ProjektAusRepositoryErstellen()` | Private Methode | Erstellt Projekt aus Repository und ordnet Repository zu |
| `ProjectListViewModel.RepositoryDoubleclickCommand` | Command | AsyncRelayCommand für Doppelklick-Verarbeitung |
| `ProjectListViewModel.FindPluginPrefixForRepositoryAsync()` | Private Methode | Findet das SCM-Plugin für einen Repository-URL |
| `ProjectListView.xaml` | UI | Suggestions-Panel mit ItemsControl, Border und Lade-Indikator |
| `UnassignedRepositoriesConverter` | ValueConverter | Konvertiert DateTime in relative Zeitangaben |
| `IPluginManager` | Interface | Liefert verfügbare SCM-Plugins |
| `IGitPlugin.GetAvailableRepositoriesAsync()` | Plugin-Methode | Liefert verfügbare Repositories pro Plugin |
| `AvailableRepository` | ValueObject | Hält Repository-Daten: Name, NameWithOwner, Url, UpdatedAt |

## Datenfluss

```
Projektübersichtsseite wird geladen
  ↓
ProjectListViewModel.LadenAsync() aufgerufen
  ├─→ ProjektService.GetAllAsync()
  │   └─→ Projekte-Collection aktualisiert
  └─→ LadenRepositorienSuggestionsAsync() aufgerufen
      ├─→ ProjektService.GetUnassignedRepositoriesAsync()
      │   ├─→ Alle zugeordneten Repository-URLs abfragen (HashSet)
      │   ├─→ Für jedes Plugin: GetAvailableRepositoriesAsync()
      │   │   ├─→ Erfolg: Repositories sammeln
      │   │   └─→ Fehler: Loggen, nächstes Plugin
      │   ├─→ Nach Url filtern (nicht in HashSet)
      │   ├─→ Nach UpdatedAt DESC, Name ASC sortieren
      │   └─→ Gefilterte Liste zurückgeben
      └─→ UnassignedRepositories-Collection aktualisiert
          └─→ UI-Binding aktualisiert Panel
```

## Einschränkungen

- Falls alle verfügbaren Repositories bereits zugeordnet sind, zeigt das Panel einen leeren Zustand
- Die Plugin-Fehlertoleranz bedeutet, dass Repositories eines fehlerhaften Plugins nicht sichtbar sind, bis das Plugin wieder funktioniert
- Das Panel wird nur beim Laden der Seite und beim Zurücknavigieren von der Projektdetailansicht aktualisiert (nicht in Echtzeit bei Änderungen von außen)
- Pagination oder Limits existieren nicht; sehr große Repository-Mengen (hunderte+) könnten zu Performance-Problemen führen

## Testabdeckung

Die Implementierung wird durch folgende Tests abgedeckt:

| Test | Klasse | Beschreibung |
|---|---|---|
| `GetUnassignedRepositoriesAsync_ShouldReturnAllRepositories_WhenAllUnassigned()` | ProjektServiceTests | Rückgabe aller verfügbaren Repositories, wenn keine zugeordnet |
| `GetUnassignedRepositoriesAsync_ShouldExcludeAssignedRepositories()` | ProjektServiceTests | Filterung zugeordneter Repositories |
| `GetUnassignedRepositoriesAsync_ShouldSortByUpdatedAtDescendingThenByNameAscending()` | ProjektServiceTests | Korrekte Sortierung |
| `GetUnassignedRepositoriesAsync_ShouldHandlePluginError_AndContinueWithOtherPlugins()` | ProjektServiceTests | Fehlertoleranz bei Plugin-Ausfällen |
| `LadenRepositorienSuggestionsAsync_ShouldLoadAndPopulateUnassignedRepositories()` | ProjectListViewModelTests | ViewModel-Property wird korrekt gefüllt |
| `RepositoryDoubleclickCommand_ShouldCreateProjectAndAssignRepository()` | ProjectListViewModelTests | Projekt-Erstellung und Repository-Zuordnung |
| `UnassignedRepositoriesConverter_ShouldFormatRelativeTime()` | UnassignedRepositoriesConverterTests | Relative Zeitformatierung |
| `UnassignedRepositoriesConverter_ShouldHandleNullAndMinValue()` | UnassignedRepositoriesConverterTests | Fallback-Verhalten bei fehlenden Daten |
