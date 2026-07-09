← [Zurück zur Übersicht](index.md)

# Dialog „Arbeitsverzeichnis bearbeiten"

## Übersicht

Der Dialog „Arbeitsverzeichnis bearbeiten" ermöglicht es, das für ein bereits einem Projekt zugewiesenes
Repository konfigurierte Arbeitsunterverzeichnis nachträglich zu ändern — unabhängig von der
Erstzuweisung im [Repository-Auswahl-Dialog](dialog-repository-auswahl.md). Damit muss ein Repository
nicht neu zugewiesen werden, nur um das Arbeitsverzeichnis anzupassen.

## Einstiegspunkt

Auf der Projektdetailseite befindet sich im Ribbon-Menü in der Gruppe „Repository" der Button
**„Arbeitsverzeichnis"** (`AutomationName="ArbeitsverzeichnisBearbeiten"`, `ProjectDetailViewModel.ArbeitsverzeichnisBearbeitenCommand`).
Der Button ist nur aktiv, wenn in der Repository-Liste des Projekts ein Repository ausgewählt ist
(`CanExecute: () => _selectedRepository != null`).

## Komponenten

### Dialog-Fenster (`ArbeitsverzeichnisBearbeitenDialog`)

Modales WPF-Window analog zum Repository-Auswahl-Dialog, mit:
- **ComboBox für Arbeitsverzeichnis:** identisches Verhalten wie im Zuweisungsdialog — Liste beginnt mit
  `"."` (Repository-Root), gefolgt von den über `IGitPlugin.GetRepositoryStructureAsync()` ermittelten
  Unterverzeichnissen.
- **Lade-Indikator:** sichtbar während `IsLoadingDirectoryStructure == true`.
- **Buttons:** „Speichern" (`BestaetigenCommand`) und „Abbrechen" (`AbbrechenCommand`).

### ViewModel (`ArbeitsverzeichnisBearbeitenViewModel`)

Wird bei jedem Öffnen des Dialogs neu aus dem DI-Container aufgelöst
(`_serviceProvider.GetRequiredService<ArbeitsverzeichnisBearbeitenViewModel>()`), sodass kein Zustand
zwischen zwei Dialog-Aufrufen erhalten bleibt.

**Konstruktor-Abhängigkeiten:**
- `ILogger<ArbeitsverzeichnisBearbeitenViewModel>`
- `DirectoryStructureBrowserService?` (optional — `null` führt zu Root-only-Fallback, siehe unten)

**Properties:**

| Property | Typ | Beschreibung |
|----------|-----|--------------|
| `AvailableWorkingDirectories` | `ObservableCollection<string>` | Verfügbare Arbeitsverzeichnisse, beginnend mit `"."` |
| `SelectedWorkingDirectory` | `string?` | Vom Benutzer gewähltes Arbeitsverzeichnis |
| `IsLoadingDirectoryStructure` | `bool` | Lade-Status während des Verzeichnisstruktur-Abrufs |

**Methode `LadenAsync(IGitPlugin? gitPlugin, string repositoryUrl, string? currentWorkingDirectory, CancellationToken ct)`:**
Lädt die Verzeichnisstruktur und wählt anschließend das aktuell konfigurierte Arbeitsverzeichnis
(`currentWorkingDirectory`) vor, statt wie im Zuweisungsdialog immer auf `"."` zurückzufallen — das ist der
zentrale fachliche Unterschied zum Erstzuweisungs-Dialog:

1. Ist `directoryStructureService` und `gitPlugin` gesetzt: Verzeichnisstruktur laden (via
   `DirectoryStructureLoadHelper.LoadWithLoadingStateAsync`, siehe unten).
   - Wird der Ladevorgang abgebrochen (`CancellationToken`), bleiben `AvailableWorkingDirectories` und
     `SelectedWorkingDirectory` unverändert und `IsLoadingDirectoryStructure` wird zurückgesetzt.
2. Ist kein Service/Plugin vorhanden, oder nach erfolgreichem Laden: `AvailableWorkingDirectories` wird
   befüllt (mind. `"."`).
3. Ist `currentWorkingDirectory` gesetzt und nicht `"."`, aber nicht Teil der geladenen Liste (z. B. weil
   das Plugin keine Struktur liefern kann oder das Verzeichnis inzwischen umbenannt wurde), wird es
   trotzdem zur Liste hinzugefügt — die bestehende Auswahl darf beim Öffnen des Dialogs nie verloren gehen.
4. `SelectedWorkingDirectory` wird auf `currentWorkingDirectory` (falls vorhanden) oder `"."` gesetzt.

### Gemeinsame Lade-Logik: `DirectoryStructureLoadHelper`

`RepositoryAssignViewModel` und `ArbeitsverzeichnisBearbeitenViewModel` teilen sich die Verzeichnisstruktur-
Ladelogik über die statische Hilfsklasse `DirectoryStructureLoadHelper`
(`src/Softwareschmiede.App/ViewModels/DirectoryStructureLoadHelper.cs`):

- **`LoadWorkingDirectoriesAsync(...)`** — ruft `DirectoryStructureBrowserService.GetDirectoriesAsync(...)`
  auf und liefert die Liste inklusive vorangestelltem `"."`-Eintrag. Ein erwarteter Abbruch wird nicht
  abgefangen, sondern an den Aufrufer weitergereicht; alle anderen Fehler werden geloggt und führen zu
  einer Liste, die nur `"."` enthält.
- **`LoadWithLoadingStateAsync(...)`** — kapselt den in beiden ViewModels identischen Wrapper: setzt den
  Lade-Status-Callback vor/nach dem Aufruf, ruft `LoadWorkingDirectoriesAsync` auf und fängt einen
  erwarteten Abbruch ab. Liefert bei Abbruch `null` zurück (statt einer Liste) — **die Anwendung des
  Ergebnisses (Befüllung der UI-Collection, Vorauswahl) bleibt bewusst beim jeweiligen Aufrufer**, da sich
  dieses Verhalten zwischen den beiden ViewModels unterscheidet:
  - `RepositoryAssignViewModel` kehrt bei `null` (Abbruch) sofort zurück und lässt den bisherigen Zustand
    unverändert (relevant bei schnellem Repository-/Plugin-Wechsel).
  - `ArbeitsverzeichnisBearbeitenViewModel` setzt bei `null` (Abbruch) den Lade-Status explizit zurück und
    kehrt ebenfalls ohne Änderung der Auswahl zurück — anders als `RepositoryAssignViewModel` gibt es hier
    keinen automatischen Folgeaufruf (`LadenAsync` wird pro Dialogaufruf nur einmal aufgerufen), der den
    Lade-Status sonst zurücksetzen würde.

## Speichern

Bestätigt der Benutzer den Dialog (`BestaetigenCommand`), ruft `ProjectDetailViewModel.ArbeitsverzeichnisBearbeitenAsync`
`ProjektService.SaveRepositoryWorkingDirectoryAsync(repository.Id, vm.SelectedWorkingDirectory, ct)` auf,
das die `RepositoryStartKonfiguration.WorkingDirectoryRelativePath` des Repositories aktualisiert
(erstellt bei Bedarf eine neue `RepositoryStartKonfiguration`, falls noch keine existiert). Anschließend
wird die Projektansicht neu geladen, damit die aktualisierte Konfiguration sichtbar ist.

Bricht der Benutzer ab (`AbbrechenCommand`), wird keine Änderung persistiert.

## Verwendung der Änderung

Das gespeicherte Arbeitsverzeichnis wird — wie bei der Erstzuweisung — beim nächsten Start einer Aufgabe
für dieses Repository verwendet:

1. Direkt nach dem Git-Klon validiert `GitOrchestrationService.ValidateWorkingDirectoryAfterCloneAsync(...)`
   (aufgerufen aus `EntwicklungsprozessService.ProzessStartenAsync`), dass das konfigurierte
   Unterverzeichnis im geklonten Repository existiert — liefert bei einem Problem ein frühes, klares
   Fehlerbild direkt nach dem Klon, noch bevor Branch-Erstellung oder Repository-Startskript versucht
   werden.
2. Beim CLI-Start ermittelt `WorkingDirectoryResolver.DetermineEffectiveWorkingDirectoryAsync(...)` (genutzt
   von `KiAusfuehrungsService`) erneut den effektiven Pfad und prüft dessen Existenz.

Siehe [Repository-Auswahl-Dialog](dialog-repository-auswahl.md#verwendung-des-arbeitsverzeichnisses) für
Details zur Path-Traversal-Prävention und Fehlerbehandlung.

Für Repositories, die über das `LocalDirectoryPlugin` im Workspace-Modus „Im Quellverzeichnis arbeiten"
(`InSourceDirectory`) betrieben werden, wird das konfigurierte Arbeitsunterverzeichnis korrekt gegen den
tatsächlichen Quellordner aufgelöst (nicht gegen das im `InSourceDirectory`-Modus nur als Zeiger dienende
Klon-Zielverzeichnis): Sowohl `GitOrchestrationService` als auch `KiAusfuehrungsService` lösen den
tatsächlichen Repository-Pfad zuerst über `IGitPlugin.ResolveEffectiveRepositoryPathAsync(...)` auf, bevor
der relative Arbeitsverzeichnis-Pfad kombiniert wird.

## Unterschiede zum Repository-Auswahl-Dialog

| Aspekt | Repository-Auswahl-Dialog | Arbeitsverzeichnis-bearbeiten-Dialog |
|---|---|---|
| Zeitpunkt | Bei Erstzuweisung eines Repositories zu einem Projekt | Jederzeit nach der Zuweisung |
| Repository-/Plugin-Auswahl | Ja (Teil des Dialogs) | Nein — Repository und Plugin stehen bereits fest |
| Default bei fehlendem Treffer in geladener Struktur | `"."` (Root) | Aktuell konfiguriertes Verzeichnis wird beibehalten, auch wenn nicht in der geladenen Liste enthalten |
| Persistenz | Neue `RepositoryStartKonfiguration` beim Bestätigen der gesamten Zuweisung | Gezielte Aktualisierung nur des Arbeitsverzeichnis-Felds über `ProjektService.SaveRepositoryWorkingDirectoryAsync` |

## Tests

- `ArbeitsverzeichnisBearbeitenViewModelTests` (`src/Softwareschmiede.Tests/App/ViewModels/`) — deckt
  Laden mit/ohne Treffer, Vorauswahl des aktuellen Verzeichnisses, Fehlerbehandlung, Lade-Status-Toggle
  und das Verhalten bei Abbruch während des Ladens ab.
- `ProjectDetailViewModelTests_Arbeitsverzeichnis` (`src/Softwareschmiede.Tests/App/ViewModels/`) — deckt
  die Verdrahtung von `ArbeitsverzeichnisBearbeitenCommand`, den Dialog-Aufruf und das Speichern über
  `ProjektService` ab.
