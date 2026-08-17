# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### TaskDetailViewModel.cs (TaskDetailViewModel)

- **Fehlende Initialisierung / Kopplung an Nutzerinteraktion** — `KannIdeAuswaehlen` (Zeile 414–423) wird ausschließlich innerhalb von `OeffneIdeInternAsync` gesetzt (Zeile 1834: `KannIdeAuswaehlen = entryPoints.Count >= 2;` und Zeile 1859 im `catch`-Block: `KannIdeAuswaehlen = false;`), also nur als Nebenwirkung eines Klicks auf den Haupt- **oder** den Dropdown-Button. `LadenAsync` (Zeile 643 ff.) ruft weder `OeffneIdeAsync` noch `OeffneIdeInternAsync` auf und berechnet `KannIdeAuswaehlen` an keiner anderen Stelle. Da `TaskDetailViewModel` als `Transient` registriert ist (`src/Softwareschmiede.App/App.xaml.cs`, `services.AddTransient<TaskDetailViewModel>();`), startet jede neu geöffnete/neu geladene Aufgabendetailansicht mit `KannIdeAuswaehlen == false` (Feld-Default), unabhängig davon, wie viele Einstiegspunkte tatsächlich existieren.

  Konsequenz: Der Dropdown-Button des Split-Buttons ist beim ersten Anzeigen der View (und nach jedem `ReloadTaskDetail`/Aufgabenwechsel) **immer unsichtbar**, selbst wenn ≥2 Einstiegspunkte vorhanden sind — er wird erst sichtbar, nachdem der Anwender bereits einmal auf den Haupt-Button geklickt und damit ungefragt den ersten (Fallback-)Einstiegspunkt geöffnet hat. Das widerspricht dem in `plan.md` beschriebenen Abschnitt „Sichtbarkeitskontrolle des Dropdown-Buttons" (Property soll „bei Initialisierung oder Aufgabenwechsel" berechnet werden) und macht den Dropdown-Button für den eigentlichen Use-Case — gezielt einen von mehreren Einstiegspunkten wählen, *bevor* versehentlich der falsche geöffnet wird — in der Praxis unbenutzbar bei einem frischen View-Aufruf.

  Dies ist kein rein theoretisches Problem, sondern bricht nachweislich zwei der neu/geändert eingecheckten E2E-Tests (siehe eigene Befunde unten), die genau dieses Verhalten (Dropdown sofort nach Reload sichtbar) voraussetzen.

  Empfehlung: `KannIdeAuswaehlen` zusätzlich einmalig nach dem Laden der Aufgabe (bzw. bei Aufgabenwechsel) berechnen — z. B. am Ende von `LadenAsync` durch Aufruf einer reinen Ermittlungsmethode (Plugin auflösen + `FindEntryPointsAsync`, **ohne** `OpenEntryPointAsync` aufzurufen), analog zum in `plan.md` Schritt „Sichtbarkeitskontrolle" beschriebenen Ablauf. Fehler bei dieser Ermittlung dürfen dabei nicht als `FehlerMeldung` angezeigt werden (nur `KannIdeOeffnen`/vorhandenes Arbeitsverzeichnis ist zu diesem Zeitpunkt relevant, ein Öffnen-Fehler ist erst beim tatsächlichen Klick fachlich relevant).

### E2E_VerzeichnisAktionen.cs (End2EndTest / VerzeichnisAktionen_ArbeitsverzeichnisUndIdeOeffnen_E2E)

- **Test wird mit hoher Wahrscheinlichkeit fehlschlagen (TimeoutException)** — Zeile 92–93: Unmittelbar nach `ReloadTaskDetail(mainWindow)` (Zeile 87, legt eine frische `TaskDetailViewModel`-Instanz an, siehe Befund oben) wird `WaitForElement(mainWindow, cf => cf.ByName("IdeOeffnenDropdown"), Short)` aufgerufen — **ohne** vorherigen Klick auf den Haupt-Button. Da `KannIdeAuswaehlen` beim Laden nicht berechnet wird, ist der Dropdown-Button zu diesem Zeitpunkt `Visibility=Collapsed`; `WaitForElement` wirft nach Ablauf von `Short` eine `TimeoutException` (`WpfTestBase.WaitForElement`, Zeile 329–330).

  Empfehlung: Erst beheben, indem der Root-Cause-Befund in `TaskDetailViewModel.cs` behoben wird (Berechnung beim Laden ergänzen); danach diesen Test unverändert laufen lassen. Alternativ, falls „on-demand beim Dropdown-Klick" (siehe `plan.md`, Designentscheidung „Einstiegspunkte-Ermittlung") tatsächlich beibehalten werden soll, muss der Test entsprechend angepasst werden (z. B. Dropdown-Sichtbarkeit erst nach einem ersten Hauptbutton-Klick prüfen) — dieser Widerspruch zwischen den beiden `plan.md`-Abschnitten „Designentscheidungen" (on-demand) und „Sichtbarkeitskontrolle des Dropdown-Buttons" (bei Initialisierung) sollte für die Umsetzung aufgelöst werden.

### E2E_TaskDetailView_IdeAuswahl.cs (End2EndTest / IdeAuswahl_KeineEinstiegspunkteUndDropdownAbbruch_E2E)

- **Test wird mit hoher Wahrscheinlichkeit fehlschlagen (TimeoutException)** — Zeile 77–79: Analog zum vorherigen Befund wird unmittelbar nach `ReloadTaskDetail(mainWindow)` (Zeile 77) `WaitForElement(mainWindow, cf => cf.ByName("IdeOeffnenDropdown"), Short)` aufgerufen, ohne dass zuvor der Haupt-Button geklickt wurde. Derselbe Root-Cause wie oben (`KannIdeAuswaehlen` wird nicht beim Laden berechnet) führt hier ebenfalls zu einer `TimeoutException`.

  Empfehlung: Siehe Empfehlung zum Befund in `TaskDetailViewModel.cs`.

### TaskDetailViewModelTests_Arbeitsverzeichnis.cs (TaskDetailViewModelTests_Arbeitsverzeichnis)

- **Veralteter/toter Verweis in XML-Doc-Kommentar** — Zeile 77: Der Kommentar zu `OeffneIdeAsync_FindetSolutionImAufgeloestenArbeitsverzeichnis` lautet „OeffneIdeAsync ruft IdeOeffnenService.FindeSolutions() … auf". `IdeOeffnenService` (inkl. der Methode `FindeSolutions`) wurde in dieser Iteration vollständig aus der Produktion entfernt; `OeffneIdeAsync` ruft diese Klasse/Methode nicht mehr auf, sondern direkt `PluginSelectionService.ResolveIdePluginAsync` und `IIdePlugin.FindEntryPointsAsync`. Der Kommentar beschreibt damit ein nicht mehr existierendes Verhalten und ist für zukünftige Leser irreführend. Dies ist der einzige verbliebene Verweis auf `IdeOeffnenService` in `src/` (per Grep über den gesamten Quellbaum verifiziert); ansonsten ist die Entfernung im Code vollständig und kompiliert sauber (`dotnet build src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj` erfolgreich, 0 Fehler/Warnungen).

  Empfehlung: Kommentar aktualisieren, z. B. „OeffneIdeAsync findet über das aufgelöste IDE-Plugin (`FindEntryPointsAsync`) eine Solution im über WorkingDirectoryResolver aufgelösten Arbeitsverzeichnis und öffnet sie, obwohl im Repository-Root keine .sln-Datei liegt."

### TaskDetailViewModelTestFactory.cs (TaskDetailViewModelTestFactory)

- **Irreführender Methodenname nach Signaturänderung** — `CreateVerzeichnisAktionenServices` (Zeile 84–91) gibt seit dieser Iteration nur noch einen einzelnen `ArbeitsverzeichnisOeffnenService` zurück (kein Tupel mehr), der Name im Plural („…Services") suggeriert aber weiterhin die Erzeugung mehrerer Services, wie es vor der `IdeOeffnenService`-Entfernung der Fall war. Kein funktionaler Fehler, aber irreführende Benennung.

  Empfehlung: Umbenennen zu z. B. `CreateArbeitsverzeichnisOeffnenService`, um Name und Rückgabewert wieder in Einklang zu bringen (an allen fünf Aufrufstellen: `TaskDetailViewModelTestFactory.cs` selbst, `TaskDetailViewModelTestsBase.cs`, `TaskDetailViewModelTests.cs`, `TaskDetailViewModelTests_PluginAktivierung.cs`, `TaskDetailViewModelTests_ZeitgesteuerterPrompt.cs`).

## Prüfung: Vollständigkeit der IdeOeffnenService-Entfernung

Explizit geprüft (Auftrag dieser Iteration): Produktionscode (`src/Softwareschmiede`, `src/Softwareschmiede.App`) und Testcode (`src/Softwareschmiede.Tests`) wurden vollständig nach `IdeOeffnenService` durchsucht.

- Klasse `IdeOeffnenService.cs` und `IdeOeffnenServiceTests.cs` sind vollständig gelöscht (`git status` zeigt beide als `deleted`).
- DI-Registrierung `services.AddScoped<IdeOeffnenService>();` in `App.xaml.cs` ist entfernt.
- Alle vier Konstruktor-Aufrufstellen von `TaskDetailViewModel` (Produktionscode in `App.xaml.cs`, Testfactory/-basis in `TaskDetailViewModelTests.cs`, `TaskDetailViewModelTestsBase.cs`, `TaskDetailViewModelTestFactory.cs`) wurden konsistent auf die neue, um einen Parameter kürzere Signatur umgestellt; kompiliert fehlerfrei.
- `TaskDetailViewModelTests_PluginAktivierung.cs` und `TaskDetailViewModelTests_ZeitgesteuerterPrompt.cs` wurden ebenfalls konsistent angepasst.
- `E2E_IdePluginSelection.cs` wurde korrekt von `IdeOeffnenService.OpenRepositoryInIdeAsync` auf den direkten `PluginSelectionService.ResolveIdePluginAsync` → `IIdePlugin.FindEntryPointsAsync` → `OpenEntryPointAsync`-Ablauf umgestellt (lokale Hilfsmethode `OpenRepositoryInIdeAsync`, bildet denselben Single-Entry-Point-Fall nach); alle drei Testfälle bleiben inhaltlich unverändert (VS-Fallback/VS-Code-Fallback/deaktiviertes VS-Plugin).
- Einziger verbliebener Verweis auf `IdeOeffnenService` in `src/` ist der veraltete Doc-Kommentar in `TaskDetailViewModelTests_Arbeitsverzeichnis.cs` (siehe eigener Befund oben) — dieser ist rein dokumentarisch und bricht weder Build noch Testverhalten.
- **Kein Funktionsverlust durch die Entfernung selbst festgestellt**: `OeffneIdeInternAsync` (`TaskDetailViewModel.cs`, Zeile 1819–1861) bildet exakt denselben Algorithmus wie das ehemalige `IdeOeffnenService.OpenRepositoryInIdeAsync` nach (0/1/≥2-Einstiegspunkte-Verzweigung, identische `FileNotFoundException`-Meldung, identisches Callback-Verhalten bei Abbruch). Die zusätzlich in Iteration 2 bemängelte Code-Duplikation zwischen `IdeOeffnenService` und `TaskDetailViewModel` ist durch die vollständige Entfernung der Klasse jetzt aufgelöst (nur noch eine Implementierung).
- Der oben unter „TaskDetailViewModel.cs" dokumentierte Befund zur `KannIdeAuswaehlen`-Berechnung ist **kein** Rückschritt durch die `IdeOeffnenService`-Entfernung, sondern ein bereits vorher (Split-Button-Feature insgesamt) bestehender Konstruktionsfehler, der bei dieser kritischen Prüfung mit aufgefallen ist.

Dokumentation außerhalb des Codes (`docs/help/dateisystem-integration/architektur.md`, `docs/help/dateisystem-integration/ablauf-technisch.md`, `docs/help/entwicklungsumgebungen/*.md`) referenziert `IdeOeffnenService` weiterhin, ist aber nicht Teil dieses Code-Reviews (siehe `/update-docs`).

## Geprüfte Dateien

- `src/Softwareschmiede.App/App.xaml`
- `src/Softwareschmiede.App/App.xaml.cs`
- `src/Softwareschmiede.App/Controls/RibbonButtonStyleHelper.cs`
- `src/Softwareschmiede.App/Controls/RibbonButtonStyles.xaml`
- `src/Softwareschmiede.App/Controls/RibbonLargeButton.xaml`
- `src/Softwareschmiede.App/Controls/RibbonSplitButton.xaml`
- `src/Softwareschmiede.App/Controls/RibbonSplitButton.xaml.cs`
- `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs`
- `src/Softwareschmiede.App/Views/TaskDetailView.xaml`
- `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTestsBase.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests_IdeAuswahl.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests_PluginAktivierung.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests_ZeitgesteuerterPrompt.cs`
- `src/Softwareschmiede.Tests/Application/Services/IdeOeffnenServiceTests.cs` (gelöscht, Löschung verifiziert)
- `src/Softwareschmiede.Tests/E2E/E2E_IdePluginSelection.cs`
- `src/Softwareschmiede.Tests/E2E/E2E_VerzeichnisAktionen.cs`
- `src/Softwareschmiede.Tests/E2E/E2E_TaskDetailView_IdeAuswahl.cs`
- `src/Softwareschmiede.Tests/E2E/MainTest.cs`
- `src/Softwareschmiede.Tests/Helpers/TaskDetailViewModelTestFactory.cs`
- `src/Softwareschmiede/Application/Services/IdeOeffnenService.cs` (gelöscht, Löschung verifiziert)

Zusätzlich zur Kontext-/Vollständigkeitsprüfung gelesen (nicht Teil des Diffs, aber zur Verifikation herangezogen):
- `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IIdePlugin.cs`
- `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/IdeEntryPoint.cs`
- `src/Softwareschmiede/Application/Services/PluginSelectionService.cs`
- `src/Softwareschmiede.App/Controls/RibbonButtonBase.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests_Arbeitsverzeichnis.cs`
- `src/Softwareschmiede.Tests/E2E/WpfTestBase.cs`
