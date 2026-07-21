# Tests

## Vorhandene Unit-Tests fuer IdeOeffnenService

`IdeOeffnenServiceTests` deckt aktuell ab:

- `FindeSolutions()` liefert `*.sln`-Dateien auf oberster Ebene alphabetisch sortiert.
- Ohne `*.sln`, bei nicht existierendem Verzeichnis und bei null/leerem Pfad kommt eine leere Liste zurueck.
- `OeffneSolution()` delegiert an `IProzessStarter` mit `ShellAusfuehren=true`.
- Leere Solution-Pfade werfen `ArgumentException`.
- Exceptions aus `IProzessStarter` werden unveraendert weitergereicht.

Fundstellen:

- `src/Softwareschmiede.Tests/Application/Services/IdeOeffnenServiceTests.cs:21`
- `src/Softwareschmiede.Tests/Application/Services/IdeOeffnenServiceTests.cs:39`
- `src/Softwareschmiede.Tests/Application/Services/IdeOeffnenServiceTests.cs:50`
- `src/Softwareschmiede.Tests/Application/Services/IdeOeffnenServiceTests.cs:72`
- `src/Softwareschmiede.Tests/Application/Services/IdeOeffnenServiceTests.cs:89`
- `src/Softwareschmiede.Tests/Application/Services/IdeOeffnenServiceTests.cs:105`

## Vorhandene ViewModel-Tests fuer IDE oeffnen

`TaskDetailViewModelTests` enthaelt einen Testblock fuer `SolutionsVorhanden / OeffneIdeCommand`:

- Ohne `.sln` ist `SolutionsVorhanden` false und `OeffneIdeCommand.CanExecute(null)` false.
- Nach Anlegen einer Solution und Reload wird der Command aktiv.
- Bei einer Solution wird direkt gestartet, ohne Dialog.
- Bei mehreren Solutions wird der Auswahl-Dialog genutzt.
- Dialog-Abbruch startet keinen Prozess.

Fundstellen:

- `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests.cs:1560`
- `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests.cs:1564`
- `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests.cs:1586`
- `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests.cs:1612`
- `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests.cs:1646`

Diese Tests muessen fuer den Fallback ergaenzt werden. Der bestehende "ohne `.sln` deaktiviert"-Assert bleibt nur fuer deaktivierten Fallback gueltig.

## Vorhandene Settings-Tests

`SettingsViewModelTests` deckt Laden/Speichern globaler und plugin-spezifischer Einstellungen ab, aber noch keine einfache boolesche App-Einstellung im Allgemein-Tab.

Relevante Muster:

- Test-DbContext und `AppEinstellungService` werden direkt verwendet.
- `LadenCommand` und `SpeichernCommand` werden ueber `AsyncRelayCommand.ExecuteAsync()` ausgefuehrt.
- App-Einstellungswerte werden nach dem Speichern wieder ueber `AppEinstellungService` verifiziert.

Fundstellen:

- `src/Softwareschmiede.Tests/App/ViewModels/SettingsViewModelTests.cs:18`
- `src/Softwareschmiede.Tests/App/ViewModels/SettingsViewModelTests.cs:31`
- `src/Softwareschmiede.Tests/App/ViewModels/SettingsViewModelTests.cs:52`
- `src/Softwareschmiede.Tests/App/ViewModels/SettingsViewModelTests.cs:146`
- `src/Softwareschmiede.Tests/App/ViewModels/SettingsViewModelTests.cs:183`

## Vorhandene E2E-Tests

`E2E_VerzeichnisAktionen` prueft die UI-Aktion end-to-end. Im Testmodus zeichnet `IProzessStarter` Prozessstarts in eine Logdatei auf.

Aktuelles Szenario:

- Arbeitsverzeichnis oeffnen schreibt einen Prozessstart.
- Ohne `*.sln` ist `IDE oeffnen` deaktiviert.
- Bei einer `*.sln` wird diese aufgezeichnet.
- Bei mehreren `*.sln` erscheint der Auswahl-Dialog und die ausgewaehlte Solution wird aufgezeichnet.

Fundstellen:

- `src/Softwareschmiede.Tests/E2E/E2E_VerzeichnisAktionen.cs:7`
- `src/Softwareschmiede.Tests/E2E/E2E_VerzeichnisAktionen.cs:14`
- `src/Softwareschmiede.Tests/E2E/E2E_VerzeichnisAktionen.cs:51`
- `src/Softwareschmiede.Tests/E2E/E2E_VerzeichnisAktionen.cs:56`
- `src/Softwareschmiede.Tests/E2E/E2E_VerzeichnisAktionen.cs:65`
- `src/Softwareschmiede.Tests/E2E/E2E_VerzeichnisAktionen.cs:79`

`E2E_SettingsKiPluginPersistence` zeigt ein Muster fuer Settings-Persistenz ueber UI: Einstellungen oeffnen, Wert setzen, speichern, Navigation weg/zurueck, Wert erneut pruefen.

Fundstellen:

- `src/Softwareschmiede.Tests/E2E/E2E_SettingsKiPluginPersistence.cs:18`
- `src/Softwareschmiede.Tests/E2E/E2E_SettingsKiPluginPersistence.cs:46`
- `src/Softwareschmiede.Tests/E2E/E2E_SettingsKiPluginPersistence.cs:67`

## Empfohlene neue Tests

- `IdeOeffnenService` oder neuer Resolver: findet VS Code via simulierter PATH-Datei bzw. bekannte Pfade, gibt nicht verfuegbar zurueck, wenn nichts gefunden wird.
- `IdeOeffnenService`: `OeffneVisualStudioCode(arbeitsverzeichnis)` startet den aufgeloesten Code-Befehl mit dem Arbeitsverzeichnis als quoted Argument.
- `TaskDetailViewModel`: ohne Solution und Fallback deaktiviert bleibt `OeffneIdeCommand` deaktiviert oder startet nichts, je nach finalem CanExecute-Design.
- `TaskDetailViewModel`: ohne Solution, Fallback aktiviert, VS Code verfuegbar startet VS Code mit `LokalerKlonPfad`.
- `TaskDetailViewModel`: Solution vorhanden und Fallback aktiviert oeffnet weiterhin die Solution, nicht VS Code.
- `TaskDetailViewModel`: Fallback aktiviert, VS Code nicht verfuegbar setzt eine verstaendliche `FehlerMeldung`.
- `SettingsViewModel`: Default ist `false`; Speichern und erneutes Laden persistiert `true`.
- Optional E2E: Setting aktivieren, Aufgabe ohne `.sln`, `IDE oeffnen` startet aufgezeichnet `code`/`code.cmd`.
