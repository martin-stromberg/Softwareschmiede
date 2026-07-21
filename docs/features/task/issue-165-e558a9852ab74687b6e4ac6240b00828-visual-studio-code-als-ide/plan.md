# Umsetzungsplan - Visual Studio Code als IDE-Fallback

## Ziel

Die bestehende Aktion `IDE oeffnen` in der Aufgabendetailansicht behält den Vorrang für gefundene Visual-Studio-Solutions. Wenn im Arbeitsverzeichnis keine `*.sln` gefunden wird, kann die Anwendung optional auf Visual Studio Code zurückfallen und das Arbeitsverzeichnis mit VS Code öffnen. Der Fallback ist über eine Programmeinstellung opt-in, standardmäßig deaktiviert und greift nur, wenn VS Code auf dem System erkannt wird.

## Leitentscheidungen

- Der Fallback gilt für die vorhandene Aufgaben-Detailaktion `IDE oeffnen`, weil das Inventory keine weiteren Aufrufstellen der Aktion ausweist.
- VS Code wird robust erkannt: zuerst über `PATH` (`code.cmd`, danach `code`), danach über typische Windows-Installationspfade für Benutzer- und Systeminstallation.
- Bei aktiviertem Fallback, aber nicht gefundenem VS Code, zeigt die Aufgabendetailansicht eine verständliche Meldung über `FehlerMeldung`.
- Ein fehlender oder unparsbarer Einstellungswert wird als `false` behandelt, damit bestehende Installationen ihr Verhalten nicht ändern.
- Der Button bleibt ohne Solution deaktiviert, solange der Fallback deaktiviert ist. Ist der Fallback aktiviert und ein Arbeitsverzeichnis vorhanden, wird der Button aktiv; die Verfügbarkeit von VS Code wird beim Ausführen geprüft.

## Umsetzungsschritte

### 1. Service-API für VS-Code-Fallback ergänzen

1. In `src/Softwareschmiede/Application/Services/AppEinstellungService.cs` einen Konstanten-Key ergänzen:
   - `OpenVisualStudioCodeWhenNoSolutionFoundKey = "ide.vscode.openWhenNoSolutionFound"`
2. Eine testbare VS-Code-Auflösung einführen, vorzugsweise als kleine Application-Service-Abstraktion, damit `TaskDetailViewModel` keine PATH- oder Installationspfadlogik kennt:
   - neues Interface, z. B. `IVisualStudioCodeLocator`
   - Ergebnisobjekt, z. B. `VisualStudioCodeAvailability(bool IsAvailable, string? ExecutablePath)`
   - Infrastructure-Implementierung, z. B. `VisualStudioCodeLocator`
3. Die Locator-Implementierung prüft:
   - alle Einträge aus `PATH`, jeweils `code.cmd` und `code`
   - `%LOCALAPPDATA%\Programs\Microsoft VS Code\bin\code.cmd`
   - `%LOCALAPPDATA%\Programs\Microsoft VS Code\Code.exe`
   - `%ProgramFiles%\Microsoft VS Code\bin\code.cmd`
   - `%ProgramFiles%\Microsoft VS Code\Code.exe`
   - `%ProgramFiles(x86)%\Microsoft VS Code\bin\code.cmd`
   - `%ProgramFiles(x86)%\Microsoft VS Code\Code.exe`
4. `src/Softwareschmiede/Application/Services/IdeOeffnenService.cs` erweitern:
   - Konstruktor um den Locator ergänzen.
   - Methode `IstVisualStudioCodeVerfuegbar()` oder gleichwertig bereitstellen.
   - Methode `OeffneVisualStudioCode(string arbeitsverzeichnis)` ergänzen.
   - `OeffneVisualStudioCode` validiert, dass der Pfad nicht leer ist und das Verzeichnis existiert.
   - Wenn kein VS-Code-Befehl auflösbar ist, wird eine klare Exception oder ein fachliches Ergebnis zurückgegeben, das das ViewModel in eine Benutzer-Meldung übersetzen kann.
   - Prozessstart über `IProzessStarter.Starten(new ProzessStartAnfrage(resolvedExecutable, quotedArbeitsverzeichnis, ShellAusfuehren: false))`.
5. Die Argumentquotierung zentral im Service halten, z. B. durch `"`-Escaping für `arbeitsverzeichnis`, damit Tests exakt prüfen können, dass der Ordner als Argument übergeben wird.

### 2. DI-Registrierung ergänzen

1. In `src/Softwareschmiede.App/App.xaml.cs` die neue Locator-Abstraktion registrieren.
2. Die Registrierung so wählen, dass Unit-Tests den Locator einfach durch Fakes ersetzen können.
3. Im Testmodus den vorhandenen `AufzeichnenderProzessStarter` unverändert weiterverwenden; er reicht für die Beobachtung des VS-Code-Prozessstarts aus.

### 3. Programmeinstellung und Settings-UI anbinden

1. In `src/Softwareschmiede.App/ViewModels/SettingsViewModel.cs` eine boolesche Property ergänzen, z. B. `OpenVisualStudioCodeWhenNoSolutionFound`.
2. In `LadenAsync()` laden:
   - `await _einstellungService.GetBoolSettingAsync(AppEinstellungService.OpenVisualStudioCodeWhenNoSolutionFoundKey, ct) ?? false`
3. In `SpeichernAsync()` speichern:
   - `await _einstellungService.SetBoolSettingAsync(AppEinstellungService.OpenVisualStudioCodeWhenNoSolutionFoundKey, OpenVisualStudioCodeWhenNoSolutionFound, ct)`
4. In `src/Softwareschmiede.App/Views/SettingsView.xaml` im Tab `Allgemein` eine Checkbox ergänzen:
   - Beschriftung: `Visual Studio Code oeffnen, wenn keine Visual-Studio-Solution gefunden wurde`
   - `IsChecked` TwoWay an die neue ViewModel-Property binden.
   - AutomationName vergeben, z. B. `OpenVisualStudioCodeWhenNoSolutionFound`.
5. Keine Datenbankmigration anlegen, weil `AppEinstellungen` ein generischer Key/Value-Store ist.

### 4. TaskDetailViewModel: CanExecute und Ausführung anpassen

1. `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs` um `AppEinstellungService` erweitern, damit der aktuelle Fallback-Wert geladen werden kann.
2. Internen Zustand ergänzen:
   - `_openVisualStudioCodeWhenNoSolutionFound`
   - Property `KannIdeOeffnen => SolutionsVorhanden || (ShowFileExplorerPanel && _openVisualStudioCodeWhenNoSolutionFound)`
3. `OeffneIdeCommand` auf `KannIdeOeffnen` umstellen.
4. Beim Laden bzw. Setzen der Aufgabe die Einstellung laden oder im `LadenAsync()` nach `Aufgabe = ...` aktualisieren.
5. Nach Änderungen an `SolutionsVorhanden`, `ShowFileExplorerPanel` und `_openVisualStudioCodeWhenNoSolutionFound` `OnPropertyChanged(nameof(KannIdeOeffnen))` sowie `NotifyCanExecuteChanged()` für `OeffneIdeCommand` auslösen.
6. `OeffneIdeAsync()` so strukturieren:
   - Wenn `_solutionPfade.Count > 0`, unverändert Solution öffnen bzw. bei mehreren Solutions den Dialog zeigen.
   - Wenn `_solutionPfade.Count == 0` und Fallback deaktiviert ist, ohne Prozessstart zurückkehren. Dieser Pfad bleibt normalerweise wegen `CanExecute` unerreichbar.
   - Wenn kein Arbeitsverzeichnis vorhanden ist, ohne Prozessstart zurückkehren oder vorhandenes Fehlerkonzept nutzen.
   - Wenn Fallback aktiviert ist, `IdeOeffnenService.OeffneVisualStudioCode(Aufgabe.LokalerKlonPfad)` ausführen.
   - Bei nicht verfügbarem VS Code `FehlerMeldung = "Keine Visual-Studio-Solution gefunden und Visual Studio Code wurde nicht gefunden."` oder gleichwertig setzen.
   - Bei Prozessstartfehlern die bestehende Meldungsform `IDE konnte nicht geöffnet werden: ...` weiterverwenden.
7. Die bestehende Property `SolutionsVorhanden` beibehalten, weil sie weiterhin den Solution-Zustand beschreibt und in Tests/Dokumentation genutzt wird.

### 5. Unit-Tests ergänzen und anpassen

1. `src/Softwareschmiede.Tests/Application/Services/IdeOeffnenServiceTests.cs` erweitern:
   - VS Code via Fake-Locator verfügbar: `OeffneVisualStudioCode` startet den aufgelösten Befehl.
   - Arbeitsverzeichnis wird quoted als Argument übergeben.
   - Nicht vorhandenes oder leeres Arbeitsverzeichnis wirft eine klare Exception.
   - Nicht verfügbarer Locator führt zu einer klar prüfbaren Exception oder Ergebnisbehandlung.
   - Bestehende `OeffneSolution`-Tests bleiben unverändert grün.
2. Tests für den neuen Locator ergänzen, falls die Implementierung nicht vollständig per Fake abgedeckt ist:
   - findet `code.cmd` in einem simulierten PATH-Verzeichnis.
   - findet `code`, wenn `code.cmd` fehlt.
   - gibt nicht verfügbar zurück, wenn PATH und bekannte Pfade leer sind.
   - bekannte Windows-Pfade über injizierbare Environment-/FileSystem-Abstraktion oder schmalen testbaren Konstruktor prüfen; keine Tests schreiben, die echte Entwicklerrechner-Installationen voraussetzen.
3. `src/Softwareschmiede.Tests/App/ViewModels/SettingsViewModelTests.cs` erweitern:
   - Default nach `LadenCommand`: `false`.
   - Speichern von `true` persistiert den neuen Key.
   - erneutes Laden liest `true`.
4. `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests.cs` erweitern/anpassen:
   - Ohne `.sln` und Fallback `false`: `SolutionsVorhanden == false`, `KannIdeOeffnen == false`, Command deaktiviert.
   - Ohne `.sln`, Fallback `true`, Arbeitsverzeichnis vorhanden, VS Code verfügbar: Command aktiv und Prozessstart für VS Code.
   - Mit `.sln` und Fallback `true`: Solution hat Vorrang; kein VS-Code-Start.
   - Ohne `.sln`, Fallback `true`, VS Code nicht verfügbar: kein Prozessstart und verständliche `FehlerMeldung`.
   - Mehrere Solutions behalten den bestehenden Auswahl-Dialog.

### 6. E2E-Tests aktualisieren

1. `src/Softwareschmiede.Tests/E2E/E2E_VerzeichnisAktionen.cs` so anpassen, dass der bestehende Ohne-Solution-Fall weiterhin den deaktivierten Button bei Default-Setting `false` prüft.
2. Optional, aber empfohlen: eigenes E2E-Szenario ergänzen:
   - Einstellung über die Settings-UI aktivieren und speichern.
   - Aufgabe ohne `.sln` öffnen.
   - `IDE oeffnen` auslösen.
   - `prozess-starts.log` enthält einen Eintrag für den VS-Code-Befehl und das Arbeitsverzeichnis als Argument.
3. Falls echte VS-Code-Verfügbarkeit für E2E nicht stabil kontrollierbar ist, die E2E-Abdeckung auf Settings-Persistenz und Command-Aktivierung beschränken und den Prozessstart auf Unit-/ViewModel-Ebene mit Fake-Locator absichern.

### 7. Dokumentation aktualisieren

1. `docs/help/dateisystem-integration/beschreibung.md`:
   - Verhalten ohne Solution von "Button deaktiviert" auf "optional VS-Code-Fallback bei aktivierter Einstellung" ändern.
   - Vorrang von `*.sln` vor VS Code beschreiben.
2. `docs/help/dateisystem-integration/ablauf-anwender.md`:
   - Ablauf für aktivierten Fallback ergänzen.
   - Hinweisfall bei nicht gefundenem VS Code dokumentieren.
3. `docs/help/dateisystem-integration/ablauf-technisch.md`:
   - neue Verzweigung nach `0` Solutions beschreiben.
   - Settings-Prüfung, VS-Code-Locator und Prozessstart über `IProzessStarter` ergänzen.
4. `docs/help/dateisystem-integration/architektur.md`:
   - neue Locator-Abstraktion und Erweiterung von `IdeOeffnenService` aufnehmen.
5. `docs/help/einstellungen/beschreibung.md` und `docs/help/einstellungen/ablauf-anwender.md`:
   - neue Checkbox im Allgemein-Tab dokumentieren.
6. `docs/help/einstellungen/datenmodell.md` oder eine vorhandene Key-Übersicht:
   - `ide.vscode.openWhenNoSolutionFound`, Typ `bool`, Default `false` aufnehmen.
7. `docs/help/index.md`:
   - Kurzbeschreibung der Dateisystem-Integration um optionalen VS-Code-Fallback ergänzen.

### 8. Verifikation

1. Unit-Tests ausführen:
   - `dotnet test`
2. Falls die Test-Suite zu groß oder instabil ist, mindestens gezielt ausführen:
   - `dotnet test --filter IdeOeffnenServiceTests`
   - `dotnet test --filter SettingsViewModelTests`
   - `dotnet test --filter TaskDetailViewModelTests`
3. E2E-Test ausführen, wenn die lokale Testumgebung dafür eingerichtet ist:
   - gezielt `E2E_VerzeichnisAktionen`
4. Manuelle Smoke-Prüfung:
   - Default-Setting prüfen: Aufgabe ohne `.sln` zeigt `IDE oeffnen` deaktiviert.
   - Setting aktivieren: Aufgabe ohne `.sln` macht `IDE oeffnen` verfügbar.
   - Mit `.sln`: weiterhin Solution-Öffnen statt VS Code.
   - Aktivierter Fallback ohne VS Code: verständliche Fehlermeldung.

## Risiken und Gegenmaßnahmen

- Async Settings-Laden kann CanExecute-Zustand verzögert aktualisieren. Gegenmaßnahme: Nach dem Laden der Einstellung PropertyChanged und `NotifyCanExecuteChanged()` auslösen.
- Tests dürfen nicht von einer echten VS-Code-Installation abhängen. Gegenmaßnahme: Locator abstrahieren und in Unit-/ViewModel-Tests faken.
- PATH-Suche und absolute Pfade unterscheiden sich bei `ShellAusfuehren`. Gegenmaßnahme: Locator liefert einen konkret startbaren Befehl, und `IdeOeffnenService` nutzt diesen einheitlich.
- Bestehende E2E-Erwartung "ohne `.sln` deaktiviert" darf nicht versehentlich verloren gehen. Gegenmaßnahme: Default-Setting-`false` explizit weiter testen.

## Akzeptanzprüfung

- Solution vorhanden: `IDE oeffnen` öffnet weiterhin die gefundene oder ausgewählte `.sln`.
- Keine Solution, Fallback deaktiviert: VS Code wird nicht gestartet; bestehendes Default-Verhalten bleibt erhalten.
- Keine Solution, Fallback aktiviert, VS Code verfügbar: Arbeitsverzeichnis wird in VS Code geöffnet.
- Keine Solution, Fallback aktiviert, VS Code nicht verfügbar: kein Prozessstart und nachvollziehbare Meldung.
- Neue Einstellung ist im Allgemein-Tab änderbar, wird gespeichert und nach erneutem Laden wieder angezeigt.
- Dokumentation beschreibt Einstellung, Verhalten, technische Verzweigung und Default-Wert.

## Offene Punkte

Keine.
