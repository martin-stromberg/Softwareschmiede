# UI — Bestandsaufnahme

## `ProjectDetailView.xaml`
Datei: `src/Softwareschmiede.App/Views/ProjectDetailView.xaml`

Die aktuelle Projektdetailansicht zeigt:
- Ladesymbol während des Ladens
- Projekttitel
- Button "+ Neue Aufgabe"
- Aufgabenliste in einer ListBox
- ContentControl für die Aufgabendetailansicht

Die Ansicht verwendet ein einfaches Layout ohne Ribbon-Menü und ohne Kacheln für Projekteigenschaften.

## `ProjectDetailView.xaml.cs`
Datei: `src/Softwareschmiede.App/Views/ProjectDetailView.xaml.cs`

Code-behind mit einem Event-Handler für das Doppelklick-Event auf Aufgaben.

## `ProjectDetailViewModel`
Datei: `src/Softwareschmiede.App/ViewModels/ProjectDetailViewModel.cs`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `ProjektId` | `Guid` | Die Projekt-ID, deren Details angezeigt werden |
| `Projekt` | `Projekt?` | Das geladene Projekt |
| `IsLoading` | `bool` | Gibt an, ob Daten geladen werden |
| `FehlerMeldung` | `string?` | Fehlermeldung bei Ladefehlern |
| `SelectedTaskViewModel` | `ViewModelBase?` | Das aktuell angezeigte Aufgaben-ViewModel |
| `Aufgaben` | `ObservableCollection<Aufgabe>` | Liste der Aufgaben des Projekts |

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `LadenAsync` | private | Lädt das Projekt und Aufgaben asynchron |
| `AufgabeErstellenAsync` | private | Erstellt eine neue Aufgabe für das Projekt |

| Command | Beschreibung |
|---------|-------------|
| `LadenCommand` | Lädt das Projekt neu |
| `AufgabeErstellenCommand` | Erstellt eine neue Aufgabe für das Projekt |
| `AufgabeOeffnenCommand` | Öffnet eine Aufgabe im Detail |

Das ViewModel implementiert `IDisposable` für das Cleanup von CancellationTokenSource und ViewModels.
