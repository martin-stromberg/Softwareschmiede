# Test-Ergebnisse

## Ergebnis

**Status:** Fehler vorhanden

## Fehlgeschlagene Tests

Alle fehlgeschlagenen Tests befinden sich im E2E-Namespace (`Softwareschmiede.Tests.E2E`).

### Softwareschmiede.Tests.E2E

- **WpfE2ETests.WpfBasisSzenarien** — `System.Runtime.InteropServices.COMException: Ein Ereignis konnte keinen Abonnenten aufrufen. (0x80040201)`
- **ProjectDetailE2ETests.ProjektDetailSzenarien** — `System.TimeoutException: Element wurde nicht innerhalb von 15s gefunden.`
- **E2E_RepositoryManagementTests.BasisBranchVerwaltung** — `System.TimeoutException: Element wurde nicht innerhalb von 15s gefunden.`
- **E2E_RepositoryInitialisierungConfigTests.InitialisierungsskriptKonfiguration** — `System.TimeoutException: Element wurde nicht innerhalb von 20s gefunden.`
- **E2E_RepositoryInitialisierungAusfuehrungTests.InitialisierungsskriptAusfuehrung** — `System.TimeoutException: Element wurde nicht innerhalb von 20s gefunden.`
- **E2E_AutonomAufgabenFeatureFlagDisabled.AutonomAufgabeInitialisieren_ZeigtFehlermeldungStattDialog_WennFeatureFlagDeaktiviert** — `System.TimeoutException: Nach Klick auf 'Zurück' wurde innerhalb von 15s keine neue Ansicht sichtbar.`
- **End2EndTest.RunGeneralTests** — `System.TimeoutException: Element wurde nicht innerhalb von 20s gefunden.`
- **End2EndTest.RunConPtyTests** — `System.TimeoutException: Element wurde nicht innerhalb von 20s gefunden.`

## Zusammenfassung

- Gesamt: 1578
- Bestanden: 1569
- Fehlgeschlagen: 8
- Übersprungen: 1

> Hinweis: `Softwareschmiede.IntegrationTests` ist in der Solution enthalten, war jedoch nicht vorkompiliert und wurde mit `--no-build` nicht ausgeführt.

## Testabdeckung

**Abdeckung:** Nicht messbar

Coverage-Sammlung schlug fehl: Coverlet konnte `Softwareschmiede.App.dll` nicht instrumentieren, da die Datei von einem anderen Prozess (WPF-E2E-Testhost) gesperrt war.

## Fehlende Tests

_Keine Lücken aus Coverage-Daten ermittelbar (Abdeckung nicht messbar)._
