# Aufgabendetailansicht und bestehende Zuordnung

## View und Ribbon

`src/Softwareschmiede.App/Views/TaskDetailView.xaml:178-194` definiert die Ribbon-Gruppe `Issue`. Die Gruppe ist an `CanAssignIssue` gebunden und enthält:

- `IssueZuweisenCommand` für die Auswahl eines vorhandenen Issues.
- `IssueBrowserOeffnenCommand` zum Öffnen der gespeicherten Issue-URL.

Die Detailansicht besitzt außerdem ein Fehlerbanner (`TaskDetailView.xaml:213-221`) und mehrere bestehende Ribbon-/Dialogmuster, die für verständliche Fehler und Abbruch genutzt werden können.

## ViewModel

`TaskDetailViewModel` lädt die Aufgabe über `AufgabeService.GetDetailAsync` und hält eine editierbare Kopie der Aufgabenbeschreibung (`EditAnforderungsBeschreibung`). Die geladene Aufgabe stellt `CurrentIssueReferenz` bereit.

`CanAssignIssue` (`TaskDetailViewModel.cs:371-377`) prüft aktuell:

- Aufgabe vorhanden
- kein laufender CLI-Prozess
- mindestens ein registriertes `IGitPlugin`

Die vorhandene Prüfung berücksichtigt **nicht**, ob bereits ein Issue zugeordnet ist. Der bestehende Zuweisungsbutton bleibt deshalb bei vorhandener Referenz grundsätzlich verfügbar. Für die neue Anlage-Aktion muss die CanExecute-/Visibility-Logik separat oder erweitert um `IssueReferenz == null` umgesetzt werden, ohne die bestehende Auswahlfunktion unbeabsichtigt zu verändern.

`IssueZuweisenAsync` (`TaskDetailViewModel.cs:903-939`) bestimmt den Provider anhand von `GitRepository.PluginTyp`, lädt Issues über die Repository-URL, zeigt `IssueSelectionDialog` und speichert die Auswahl über `UpdateIssueReferenzAsync`. Danach wird `LadenAsync` ausgeführt.

## Dialog- und Service-Muster

`IDialogService` abstrahiert synchrone Bestätigungsdialoge und asynchrone WPF-Dialoge. `WpfDialogService.ShowIssueSelectionDialogAsync` erzeugt den Dialog auf dem UI-Dispatcher und gibt bei Bestätigung `SelectedIssue`, bei Abbruch `null` zurück.

Das bestehende Muster ist für einen neuen `IssueCreateDialogViewModel` und einen `ShowIssueCreateDialogAsync`-Eintrag erweiterbar. Der neue Dialog benötigt zusätzlich:

- editierbaren Titel und Body,
- optionale Template-Auswahl,
- KI-Ausfüllaktion,
- Laden-/Absenden-Zustand,
- explizite Abbruchsemantik,
- sichtbare Fehler ohne Verlust der bisherigen Eingaben.

## Relevante Tests

Vorhandene Tests liegen unter `src/Softwareschmiede.Tests` und enthalten `TaskDetailViewModelTestFactory`, E2E-Tests für Navigation/Taskdetail sowie `IssueSelectionDialogViewModel`- und Provider-Tests. Sie bilden gute Anschlussstellen für CanExecute-, Dialog- und Provider-Szenarien; eine Issue-Erstellungsstrecke ist noch nicht vorhanden.
