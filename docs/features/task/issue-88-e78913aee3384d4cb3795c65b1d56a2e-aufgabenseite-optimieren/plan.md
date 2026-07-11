# Umsetzungsplan: Aufgabenseite optimieren

## Zielbild

Die Aufgabendetailansicht zeigt den Kontext der aktuell geöffneten Aufgabe konsistent im Fenster und in der Fußzeile. Die Aufgabenstamminformationen sind über eine explizite `Info`-Ansicht in allen relevanten Detailzuständen erreichbar. Aufgaben aus Git-Plugins bleiben auch ohne Issue-Bezug start- und ausführbar.

## Betroffene Dateien

- `src/Softwareschmiede.App/ViewModels/MainWindowViewModel.cs`
- `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs`
- `src/Softwareschmiede.App/Views/TaskDetailView.xaml`
- `src/Softwareschmiede/Application/Services/EntwicklungsprozessService.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/MainWindowViewModelTests.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests.cs`
- `src/Softwareschmiede.Tests/Application/Services/EntwicklungsprozessServiceTests.cs`

## Arbeitspakete

### 1. Fenstertitel der Aufgabendetailansicht aktualisieren

- `TaskDetailViewModel` um einen einfachen Benachrichtigungsmechanismus für Titeländerungen ergänzen, z. B. eine `Action<string?> DetailTitelAenderungAction` analog zum bestehenden Muster in `ProjectListViewModel`.
- Nach erfolgreichem Laden der Aufgabe sowie bei Änderungen an `Aufgabe` den aktuellen `AufgabeTitel` melden.
- `MainWindowViewModel.NavigateZuAufgabe` so verdrahten, dass der Fenstertitel auf `Softwareschmiede – {AufgabeTitel}` gesetzt wird.
- Beim Öffnen der Detailansicht zunächst einen neutralen Titel wie `Softwareschmiede – Aufgabe` setzen, damit keine vorherige Ansicht im Titel stehen bleibt, bis die Aufgabe geladen ist.
- Beim Zurücknavigieren zur Dashboard-Ansicht weiterhin `Softwareschmiede – Dashboard` setzen.

### 2. CLI-Pluginname getrennt vom Laufstatus bereitstellen

- In `TaskDetailViewModel` eine neue read-only Property für die Fußzeilenanzeige einführen, z. B. `AktiverCliName`.
- Den Namen aus dem tatsächlich verwendeten KI-Plugin ableiten:
  - beim Laden aus `Aufgabe.KiPluginPrefix` über `IPluginManager.GetDevelopmentAutomationPlugins()`,
  - beim Starten, Neustarten und Pluginwechsel aus dem aufgelösten Plugin,
  - als Fallback den Prefix anzeigen, wenn kein Pluginname auflösbar ist.
- Die Property auf `null` oder leer setzen, wenn keine CLI läuft oder der Prozess stoppt/fehlschlägt.
- `CliStatusText` weiterhin für Laufzeitstatus verwenden, aber nicht mehr als Quelle für den in der Anforderung geforderten CLI-Namen behandeln.
- `TaskDetailView.xaml` in der Statusleiste auf die neue Property binden und die Anzeige nur sichtbar machen, wenn ein CLI-Name vorhanden ist.

### 3. Info-Ansicht als explizite Ansicht modellieren

- Die bisherige boolesche Umschaltung `IsInfoViewVisible` durch eine klarere View-Auswahl ergänzen oder ersetzen, z. B. mit Properties `IsInfoViewSelected`, `IsCliViewSelected`, `IsDiffViewSelected`.
- Für Kompatibilität mit bestehenden Tests kann `IsInfoViewVisible` zunächst als abgeleitete oder weiterverwendete Property erhalten bleiben, sofern das neue Verhalten eindeutig bleibt.
- Separate Commands für Ansichtswechsel einführen, z. B. `InfoViewCommand`, `CliViewCommand`, `DiffViewCommand`, statt nur `InfoCliToggleCommand`.
- Beim Laden einer Aufgabe eine sinnvolle Standardansicht setzen:
  - `Neu`: `Info` bzw. Stammdaten/Bearbeitung,
  - `Gestartet`/`Wartend`: `CLI`,
  - `Beendet`: `Diff`, sofern vorhanden, sonst `Info`.
- Sicherstellen, dass `Info` unabhängig vom Aufgabenstatus auswählbar ist.

### 4. XAML-Layout der Detailseite vereinheitlichen

- Eine gemeinsame Ansichtsleiste in `TaskDetailView.xaml` oberhalb des Inhalts einführen.
- In dieser Leiste mindestens einen expliziten `Info`-Button anzeigen.
- Den `CLI`-Button nur anzeigen oder aktivieren, wenn `ShowCliPanel` gilt.
- Den `Diff`-Button nur anzeigen oder aktivieren, wenn `ShowDiffPanel` gilt.
- Das bestehende Info-Panel aus dem CLI-Bereich in einen gemeinsamen Info-Inhalt verschieben, der für `Neu`, `Gestartet`, `Wartend` und `Beendet` verwendbar ist.
- Das Edit-Panel für neue Aufgaben entweder in der Info-Ansicht erhalten oder als Stammdatenbereich integrieren, ohne die bestehende Speicherlogik zu verändern.
- Die alte Toggle-Leiste im CLI-Panel entfernen, damit nicht zwei konkurrierende Navigationsmodelle existieren.

### 5. Aufgaben ohne Issue-Bezug startbar halten

- `TaskDetailViewModel.StartenAsync` prüfen: Der aktuelle Aufruf übergibt bei fehlender direkter Aufgaben-Repository-Zuordnung eine leere Repository-URL. Das ist nur dann korrekt, wenn `EntwicklungsprozessService.ResolveRepositoryAsync` über den Projektkontext eindeutig auflösen kann.
- `EntwicklungsprozessService` nicht auf `IssueReferenz` verpflichten; `ErstelleTaskBranchName` und `CreateIssueFileAsync` unterstützen bereits Aufgaben ohne Issue.
- Falls ein Fehlerfall reproduzierbar ist, die Startlogik so anpassen, dass bei Aufgaben ohne Issue-Bezug kein Issue-spezifischer Wert für Branch, Repository- oder Pluginauflösung vorausgesetzt wird.
- Keine Änderung am Verhalten issue-bezogener Aufgaben vornehmen.

### 6. Tests ergänzen und anpassen

- `MainWindowViewModelTests`:
  - Test ergänzen, dass `NavigateZuAufgabeCommand` den Fenstertitel nach Laden der Aufgabe auf den Aufgabentitel setzt.
  - Test ergänzen, dass beim Wechsel auf Dashboard/andere Ansichten kein alter Aufgabentitel stehen bleibt.
- `TaskDetailViewModelTests`:
  - Test ergänzen, dass der aktive CLI-Name nach erfolgreichem Start dem Pluginnamen entspricht.
  - Test ergänzen, dass der CLI-Name nach Stopp oder Fehler geleert wird.
  - Tests für die neue Info-Navigation in Status `Neu`, `Gestartet`/`Wartend` und `Beendet` ergänzen.
  - Bestehende Tests zu `InfoCliToggleCommand` entweder migrieren oder eine Kompatibilitätsschicht gezielt absichern.
- `EntwicklungsprozessServiceTests`:
  - Test ergänzen oder vorhandenen Test schärfen, dass `ProzessStartenUndCliStartenAsync` eine Aufgabe ohne `IssueReferenz` erfolgreich startet und einen Branch im Format `task/{aufgabe.Id:N}-{slug}` erzeugt.
  - Falls Projektkontext-Auflösung beteiligt ist, einen Test für Aufgabe ohne `GitRepositoryId`, aber mit genau einem aktiven Projekt-Repository abdecken.

## Prüfschritte

- `dotnet test`
- Manuelle UI-Prüfung:
  - Aufgabe öffnen und Fenstertitel kontrollieren.
  - Aufgabe starten und CLI-Name in der Fußzeile kontrollieren.
  - CLI stoppen und prüfen, dass kein veralteter CLI-Name sichtbar bleibt.
  - Info-Ansicht bei neuer, gestarteter/wartender und beendeter Aufgabe öffnen.
  - Aufgabe ohne Issue-Bezug aus einem Git-Plugin starten.

## Risiken und Hinweise

- Der Fenstertitel hängt an asynchronem Laden. Tests sollten deshalb warten, bis die Aufgabe geladen und der Titel aktualisiert wurde.
- Die Fußzeile darf Laufstatus und Pluginname nicht vermischen. Laufstatus kann intern weiter für Statuslogik bestehen, die sichtbare Anforderung betrifft aber den Namen der ausgeführten CLI.
- Die XAML-Umstellung sollte die bestehenden Ribbon-Commands und Terminal-Session-Bindings nicht verändern.
- Bei der Repository-Auflösung ist Mehrdeutigkeit weiterhin ein gültiger Fehlerfall. Die Anforderung verlangt keine automatische Auswahl, wenn mehrere aktive Projekt-Repositories möglich sind.

## Offene Punkte

Keine.
