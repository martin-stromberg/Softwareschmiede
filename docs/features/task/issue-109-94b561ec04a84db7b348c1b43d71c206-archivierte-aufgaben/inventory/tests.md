# Tests und Abdeckung

## Relevante Testdateien

- `src/Softwareschmiede.Tests/App/ViewModels/ProjectDetailViewModelTests.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/ProjectDetailViewModelTests_Arbeitsverzeichnis.cs`
- `src/Softwareschmiede.Tests/Application/Services/AufgabeServiceTests.cs`
- `src/Softwareschmiede.Tests/Domain/Enums/AufgabeStatusEnumTests.cs`
- `src/Softwareschmiede.Tests/Domain/Enums/AufgabeStatusTransitionTests.cs`
- `src/Softwareschmiede.Tests/E2E/ProjectDetailE2ETests.cs`

## Bestehende ViewModel-Abdeckung

`ProjectDetailViewModelTests` deckt unter anderem ab:

- Projekt speichern, aktualisieren, loeschen
- Repository zuweisen und Arbeitsverzeichnis persistieren
- Aufgabe oeffnen und Callback auf `TaskDetailViewModel` setzen
- neue Aufgabe erstellen und zur Aufgabendetailansicht navigieren
- einzelne Aufgabe nach Aenderung neu laden
- Issues laden und in Aufgaben umwandeln

Es gibt aktuell keinen Test fuer:

- getrennte Collections fuer beendete und nicht beendete Aufgaben
- Zuordnung von `Neu`, `Gestartet`, `Wartend` zu "nicht beendet"
- Zuordnung von `Beendet` zu "beendet"
- Verhalten bei leerer Aufgabenliste, nur beendeten Aufgaben oder nur nicht beendeten Aufgaben in der Projektdetailansicht

## Bestehende Service-Abdeckung

`AufgabeServiceTests` deckt relevante Statusoperationen ab:

- `CreateAsync` erstellt `Neu`
- `AbschliessenAsync` setzt `Beendet` und `AbschlussDatum`
- `VerwerfenAsync` kann `Neu` archivieren oder loeschen
- `ArchivierenAsync` ist in den vorhandenen Service-Tests nicht prominent fuer die Projektdetaildarstellung relevant
- `GetAktiveAufgabenAsync` filtert `Gestartet` und `Wartend`

Fuer diese Anforderung ist wahrscheinlich kein neuer Service-Test zwingend, sofern die Logik im ViewModel liegt. Wenn eine neue Status-Extension eingefuehrt wird, sollte sie mit Domain-Tests abgedeckt werden.

## Bestehende E2E-Abdeckung

`ProjectDetailE2ETests` prueft:

- Projektdetail oeffnen und zurueck
- Projekt bearbeiten, speichern, loeschen
- Aufgabe neu anlegen und nach Rueckkehr in der Aufgabenliste sehen
- Filter-Overlay oeffnen und RadioButton auswaehlen
- Repository-Dialoge

Es fehlt ein E2E-Test fuer die neue Ziel-UI:

- beendete Aufgabenliste ist initial zugeklappt
- nicht beendete Aufgaben sind sichtbar
- Aufklappen zeigt beendete Aufgaben

## Empfohlene neue Tests

- ViewModel-Test: Seed mit `Neu`, `Gestartet`, `Wartend`, `Beendet`, `Archiviert`; nach Laden enthalten offene/nicht beendete Aufgaben nur die ersten drei und beendete Aufgaben nur `Beendet`.
- ViewModel-Test: leeres Projekt fuehrt zu leeren Collections ohne Fehler.
- ViewModel-Test: nur beendete Aufgaben fuehrt zu leerer offener Liste und gefuellter beendeter Liste.
- XAML/E2E-Test: `BeendeteAufgabenExpander` ist beim Oeffnen nicht expandiert und die `BeendeteAufgabenListe` wird nach Expandieren sichtbar.
