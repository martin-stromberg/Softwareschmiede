# Bestandsaufnahme: To-Do-Liste für Aufgaben

Diese Bestandsaufnahme dokumentiert den aktuellen Stand des Projekts bezüglich der Anforderung "To-Do-Liste zur Unterstützung der Aufgabengliederung und des Fortschritts". Sie untersucht, welche Datenbankmodelle, Services, ViewModels und Tests bereits existieren.

## Zusammenfassung

**Datenmodell:** Keine Todo-Entity, keine Todos-Navigation in Aufgabe. DbContext ist strukturiert für 1:n-Beziehungen, aber TodoDbSet ist nicht definiert.

**Services:** Es existiert ein AufgabeService mit Basisfunktionalität (CRUD, GetDetail, etc.), aber kein TodoService und keine Todo-Methoden.

**ViewModels:** TaskDetailViewModel hat verschiedene Ansichten (Info, Cli, Diff, Dateibrowser, PullRequests) und eine Protokolleintraege-Collection, aber keine Todos-Collection und keine Todo-Commands.

**Detailseiten:** TaskDetailView existiert mit verschiedenen Bereichen, aber kein TodoListView oder ähnliche Todo-UI-Komponenten.

**Tests:** Umfangreiche Test-Suite für Services und ViewModels, aber keine TodoService-Tests oder Todo-ViewModel-Tests.

## Details

- [Datenmodell](inventory/models.md)
- [Logik und Services](inventory/logic.md)
- [ViewModels und UI](inventory/viewmodels.md)
- [Tests](inventory/tests.md)
