# Ablauf und Orchestrierung

## Startpfad

Der Anwender startet eine neue Aufgabe ueber `TaskDetailViewModel.StartenCommand`. Der Command ist in `TaskDetailViewModel` so registriert, dass er nur bei `AufgabeStatus.Neu` und nicht laufender CLI ausfuehrbar ist (`TaskDetailViewModel.cs`, Zeilen 536-540).

`StartenAsync()` loest zuerst das Development-Automation-Plugin auf oder zeigt den Plugin-Auswahldialog. Danach ruft es den kombinierten Ablauf auf:

- `TaskDetailViewModel.StartenAsync()` ruft `EntwicklungsprozessService.ProzessStartenUndCliStartenAsync(...)` (`TaskDetailViewModel.cs`, Zeilen 1273-1301).
- Nach erfolgreichem Ruecksprung laedt das ViewModel die Aufgabe neu, setzt den aktiven CLI-Namen und verbindet die PseudoConsole-Session (`TaskDetailViewModel.cs`, Zeilen 1303-1311).
- Fehler werden im selben `catch` in `FehlerMeldung` abgebildet (`TaskDetailViewModel.cs`, Zeilen 1317-1322).

## Repository-Vorbereitung

`EntwicklungsprozessService.ProzessStartenUndCliStartenAsync()` umschliesst den Repository-Start und CLI-Start mit Rollback-Logik. Es ruft zuerst `ProzessStartenAsync()` auf (`EntwicklungsprozessService.cs`, Zeilen 120-135).

`ProzessStartenAsync()` fuehrt die relevanten Schritte in dieser Reihenfolge aus:

1. Aufgabe laden.
2. Repository und SCM-Plugin aufloesen.
3. lokales Klon-Verzeichnis vorbereiten und `CloneRepositoryAsync` ausfuehren.
4. optional das konfigurierte Arbeitsverzeichnis nach dem Klon validieren.
5. Branch auschecken/anlegen.
6. `issue.md` und `.gitignore` schreiben.
7. Aufgabe per `AufgabeService.StartenAsync(...)` als gestartet persistieren.

Die Kernausfuehrung steht in `EntwicklungsprozessService.cs`, Zeilen 91-107. Der Klon selbst wird in `PrepareCloneDirectoryAsync()` angestossen (`EntwicklungsprozessService.cs`, Zeilen 413-439).

## Fehler- und Rollbackverhalten

Beim kombinierten Start wird bei `OperationCanceledException` und sonstigen Exceptions `RollbackStartAsync()` ausgefuehrt (`EntwicklungsprozessService.cs`, Zeilen 164-174). Der Rollback entfernt ein bereits persistiertes lokales Klon-Verzeichnis und setzt den Aufgabenstatus zurueck auf `Neu` (`EntwicklungsprozessService.cs`, Zeilen 379-393).

Das ist fuer die Statusanzeige wichtig: Ein UI-Status zur Repository-Vorbereitung darf nicht erst nach erfolgreichem Start geloescht werden, sondern muss in `finally` oder in allen Fehlerpfaden geloescht bzw. durch Fehlerstatus ersetzt werden.

## Naheliegender Integrationspunkt

Der kurzfristig risikoarme Integrationspunkt fuer den Mindeststatus ist `TaskDetailViewModel.StartenAsync()`:

- Vor dem Await auf `ProzessStartenUndCliStartenAsync(...)`: `CliStatusText` bzw. ein neuer allgemeiner Footerstatus auf `Bereit Repository vor...` setzen.
- Nach Ruecksprung: bestehende CLI-Statuslogik ueber `AttachCliStatusSession(...)`/`UpdateCliStatusText(...)` wieder uebernehmen.
- Im Fehlerpfad: Status auf `CLI inaktiv` oder einen passenden Fehlerstatus setzen, ohne `FehlerMeldung` zu verdecken.

Ein service-seitiger Statuskanal waere sauberer fuer mehrere Views, ist aber fuer die aktuelle Akzeptanz vermutlich nicht zwingend, weil die sichtbare Fusszeile in der Aufgabendetailansicht liegt.
