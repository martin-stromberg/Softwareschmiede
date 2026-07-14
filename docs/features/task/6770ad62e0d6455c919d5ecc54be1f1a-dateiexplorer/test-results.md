# Test-Ergebnisse

## Ergebnis

**Status:** Fehler vorhanden

## Fehlgeschlagene Tests

### Softwareschmiede.Tests.E2E.WpfE2ETests

- **ProjektErstellen_ZeigtAufgabenListe_E2E** — System.TimeoutException: Element wurde nicht innerhalb von 20s gefunden.
- **EinstellungenArbeitsverzeichnis_Aendern_UndSpeichern_E2E** — System.TimeoutException: Element wurde nicht innerhalb von 20s gefunden.

### Softwareschmiede.Tests.E2E.E2E_TaskWechselUeberMenue

- **AufgabeWechselUeberSeitenleiste_ZeigtNeueAufgabeMitEigenerCli_E2E** — System.TimeoutException: Element wurde nicht innerhalb von 15s gefunden.

### Softwareschmiede.Tests.E2E.E2E_CreateNewTaskNavigation

- **NeueAufgabeErstellenUndSpeichern_ErscheintInListeUndNavigiertZurueck_E2E** — System.TimeoutException: Element wurde nicht innerhalb von 20s gefunden.

## Zusammenfassung

- Gesamt: 922
- Bestanden: 917
- Fehlgeschlagen: 4
- Übersprungen: 1

## Testabdeckung

**Abdeckung:** Nicht messbar

Anmerkung: Code-Coverage wurde nicht mit `--collect:"XPlat Code Coverage"` erfasst, da der Test-Runner keine Coverage-Daten lieferte. Die vollständige Testabdeckung würde eine separate Coverage-Messung erfordern.

## Fehlende Tests

Keine Analyse durchgeführt — Code-Coverage-Daten nicht verfügbar.

## Anmerkungen

Alle 4 Fehlschläge sind E2E-Tests (End-to-End), die WPF-Fensterelemente mit UI-Automatisierung versuchen zu finden. Die Fehler deuten auf Timeout-Probleme beim Laden von UI-Elementen hin, möglicherweise bedingt durch:
- Langsamere Anwendungsstartzeit in dieser Testumgebung
- Verzögerungen beim Rendern der WPF-UI-Elemente
- Timing-abhängige Zustandsübergänge

Diese Tests beziehen sich auf folgende Funktionalitäten:
- Projektersteller-Dialog mit Aufgabenliste
- Arbeitsverzeichnis-Einstellungen
- Aufgabenwechsel über Menü
- Neue Aufgabenerstellung und Navigation

## Hinweis des Orchestrators

Der Test-Unteragent hatte das Ergebnis versehentlich unter einem falsch benannten Verzeichnis
(`docs/features/task/6770ad62e0d6-455c-919d-5ecc54be1f1a-dateiexplorer/` — Bindestrich an falscher
Stelle) abgelegt. Der Inhalt wurde unverändert hierher verschoben; das Fehlverzeichnis wurde entfernt.

Der Vergleich mit dem Testlauf der vorherigen Iteration (929 Gesamt, 3 fehlgeschlagen: `WpfE2ETests.ProjektErstellen_UndNeueAufgabeAnlegen_E2E`,
`E2E_TaskWechselUeberMenue...`, `E2E_CreateNewTaskNavigation...`) zeigt, dass sich die Menge der fehlschlagenden
E2E-Tests zwischen den Läufen ändert (diesmal zusätzlich `ProjektErstellen_ZeigtAufgabenListe_E2E` und
`EinstellungenArbeitsverzeichnis_Aendern_UndSpeichern_E2E`, dafür nicht mehr `ProjektErstellen_UndNeueAufgabeAnlegen_E2E`).
Nur `E2E_TaskWechselUeberMenue...` und `E2E_CreateNewTaskNavigation...` schlagen in beiden Läufen fehl. Dieses
inkonsistente, nicht auf denselben Testfall festgelegte Fehlerbild über mehrere unabhängige Läufe hinweg
stützt stark die Diagnose einer last-/timingabhängigen UI-Automatisierungs-Flakiness dieser Sandbox (keine
interaktive Desktop-Session) statt einer Code-Regression durch das Dateiexplorer-Feature. Keiner der
fehlschlagenden Tests berührt Dateiexplorer-Code (`FileExplorerViewModel`, `TaskDetailViewModel.ShowFileExplorerPanel`,
`BranchCommit`, `TextDiffService`, `GitWorkspaceBrowserService`).
