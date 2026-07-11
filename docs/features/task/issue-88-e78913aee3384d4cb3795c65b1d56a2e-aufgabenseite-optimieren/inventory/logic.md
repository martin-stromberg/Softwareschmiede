# Logik

## `MainWindowViewModel`
Datei: `src/Softwareschmiede.App/ViewModels/MainWindowViewModel.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|--------------|------------------|
| `NavigateToDashboard` | `private` | Setzt Dashboard als `CurrentView` und `Title` auf `Softwareschmiede - Dashboard`. |
| `NavigateToProjectList` | `private` | Setzt Projektliste als `CurrentView`, setzt `Title` auf Projektkontext und registriert eine Detailtitel-Aenderungsaktion. |
| `NavigateToSettings` | `private` | Setzt Einstellungen als `CurrentView` und `Title` auf `Softwareschmiede - Einstellungen`. |
| `NavigateZuAufgabe` | `private` | Erstellt ein `TaskDetailViewModel`, setzt `ZurueckAction` und `AufgabeId`, setzt es als `CurrentView`; der Fenstertitel wird hier nicht gesetzt. |
| `AktiveAufgabenAktualisierenAsync` | `public` | Laedt aktive Aufgaben fuer die Seitenleiste und mappt Pluginnamen. |
| `MapAktiveAufgabePanelItem` | `private` | Erzeugt Seitenleisten-Eintraege inkl. `ScmPluginName`, `KiPluginName`, `LaufStatus` und Aktiv-Markierung. |
| `ResolvePluginName` | `private` | Loest Plugin-Prefixe in Anzeigenamen auf; faellt auf Prefix zurueck. |

Abonnierte Events: `IRunningAutomationStatusSource.RunningCountChanged`, `DispatcherTimer.Tick`.

Publizierte Events: keine.

## `TaskDetailViewModel`
Datei: `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|--------------|------------------|
| `LadenAsync` | `private` | Laedt Aufgabe, CLI-Laufstatus, Protokolle, Plugins und Promptvorlagen; startet CLI automatisch neu, wenn Status `Gestartet` und kein Prozess laeuft. |
| `StartenAsync` | `private` | Loest KI-Plugin auf, nutzt Repository-URL aus `Aufgabe.GitRepository`, startet Entwicklungsprozess und CLI, bindet anschliessend die PseudoConsole-Session. |
| `StartCliAndUpdateStateAsync` | `private` | Startet die CLI fuer eine bereits laufende Aufgabe und aktualisiert `SelectedKiPluginPrefix`, `IsCliRunning` und Terminal-Session. |
| `CliAutomatischNeustartenAsync` | `private` | Startet eine CLI in `LokalerKlonPfad` mit aufgeloestem KI-Plugin neu. |
| `CliStoppenAsync` | `private` | Stoppt den CLI-Prozess fuer die aktuelle Aufgabe. |
| `CliNeustartenAsync` | `private` | Fuehrt den manuellen CLI-Neustart aus. |
| `PluginWechselAsync` | `private` | Stoppt laufende CLI, waehlt neues Plugin und startet mit bestehendem Klonpfad neu. |
| `InfoCliToggle` | `private` | Schaltet `IsInfoViewVisible` zwischen CLI- und Info-Ansicht um. |
| `OnCliProcessStatusChanged` | `private` | Reagiert auf Prozessstatus; setzt `IsCliRunning`, entfernt Session bei Stopp/Fehler und setzt `CliStatusText` auf Fehler oder inaktiv. |
| `AttachCliStatusSession` | `private` | Haengt `RuntimeStatusChanged` der `PseudoConsoleSession` an/ab und aktualisiert `CliStatusText`. |
| `UpdateCliStatusText` | `private` | Mappt `CliRuntimeStatus` auf Texte wie `CLI-Status: Ausfuehrung laeuft`, `Wartet auf Eingabe` oder `CLI inaktiv`. |

Abonnierte Events: `KiAusfuehrungsService.CliProcessStatusChanged`, `PseudoConsoleSession.RuntimeStatusChanged`.

Publizierte Events: `PseudoConsoleSessionGestartet`, `CliGestoppt`, `PromptVorlageGesendet`.

## `TaskDetailView`
Datei: `src/Softwareschmiede.App/Views/TaskDetailView.xaml`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|--------------|------------------|
| XAML-Bindings | n/a | Bindet Ribbon-Kommandos, Statuspanels und Fusszeile an `TaskDetailViewModel`. |
| CLI-Panel-Toggle | n/a | Im `ShowCliPanel` existiert ein einzelner `ToggleButton`, dessen Inhalt zwischen `CLI` und `Info` wechselt. |
| Fusszeile | n/a | Zeigt `StatusIndicatorControl` und rechts `CliStatusText`, sichtbar nur bei `ShowCliPanel`. |

Abonnierte Events: keine im XAML; Code-Behind bindet Terminal-Session ueber ViewModel-Events.

Publizierte Events: keine.

## `EntwicklungsprozessService`
Datei: `src/Softwareschmiede/Application/Services/EntwicklungsprozessService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|--------------|------------------|
| `ProzessStartenAsync` | `public` | Loest Aufgabe, Repository und SCM-Plugin auf, klont, richtet Branch ein und finalisiert den Start. |
| `ProzessStartenUndCliStartenAsync` | `public` | Kombiniert Repository-Setup und KI-CLI-Start; loest Repository/Plugin erneut ueber denselben Kontext statt nur ueber `GitRepositoryId`. |
| `ResolveRepositoryAsync` | `private` | Nutzt `Aufgabe.GitRepository`, sonst Projekt-Repositories; bei genau einem aktiven Repository wird dieses verwendet. |
| `ResolvePluginAsync` | `private` | Nutzt `repository.PluginTyp`, sonst uebergebenen Prefix, sonst Default-Plugin. |
| `SetupBranchAsync` | `private` | Erzeugt Branch fuer neue Aufgaben oder checkt vorhandenen Remote-Branch aus. |
| `ErstelleTaskBranchName` | `private static` | Baut Branch mit Issue-Nummer, wenn vorhanden; sonst `task/{aufgabe.Id:N}-{titelSlug}`. |
| `CreateIssueFileAsync` | `private` | Schreibt `issue.md` aus Aufgabentitel, ID, Branch und `AnforderungsBeschreibung`; benoetigt keine Issue-Referenz. |

Abonnierte Events: keine.

Publizierte Events: keine.

## `KiAusfuehrungsService`
Datei: `src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|--------------|------------------|
| `StartCliAsync` | `public` | Startet klassischen CLI-Prozess per `ProcessStartInfo`. |
| `StartWithPseudoConsoleAsync` | `public` | Startet `cmd.exe` mit PseudoConsole und sendet den Plugin-Befehl in die Konsole. |
| `GetPseudoConsoleSession` | `public` | Gibt die laufende Terminal-Session einer Aufgabe zurueck. |
| `StopCliAsync` | `public` | Stoppt einen laufenden CLI-Prozess. |
| `HandleProcessExited` | `private` | Entfernt Handle, ermittelt Exit-Code und publiziert `CliProcessStatusChanged`. |
| `BuildCliCommand` | `private static` | Erzeugt aus `ProcessStartInfo.FileName` und `Arguments` den Befehl fuer die ConPTY-Shell. |

Abonnierte Events: `Process.Exited`.

Publizierte Events: `CliProcessStatusChanged`, `RunningCountChanged`.
