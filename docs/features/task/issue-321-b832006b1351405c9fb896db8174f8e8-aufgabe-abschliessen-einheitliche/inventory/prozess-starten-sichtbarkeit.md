# Detail: Sichtbarkeit `ProzessStartenAsync`

## Produktive Aufrufer

- Keine. Produktiv ist ausschließlich `ProzessStartenUndCliStartenAsync`
  (`TaskDetailViewModel.StartenAsync` ~Z. 1601, `ScmEventOrchestrator` Z. 158)
  sowie `CliNeustartenAsync` (`ScmEventOrchestrator` Z. 161, `TaskDetailViewModel`
  ~Z. 1692) der Einstieg.

## Test-Aufrufer

- `Softwareschmiede.Tests`: `EntwicklungsprozessServiceTests`,
  `EntwicklungsprozessServiceTests_BasisBranch`,
  `EntwicklungsprozessServiceTests_Initialisierungsskript`,
  `EntwicklungsprozessServiceTests_WorkingDirectoryValidation`
  (jeweils viele Methoden zum Einzelschritt „Klon+Branch ohne CLI").
- `Softwareschmiede.IntegrationTests`: `EntwicklungsprozessServiceTests`
  (6 Methoden: Persistenz, Issue-Branch, Protokoll, Arbeitsverzeichnis-Fälle)
  und — für den produktiven Einstieg — `ScmEventDispatchTests` verwendet
  bereits `ProzessStartenUndCliStartenAsync`.

## `InternalsVisibleTo`-Lage

- `src/Softwareschmiede/Properties/AssemblyInfo.cs`:
  `[assembly: InternalsVisibleTo("Softwareschmiede.Tests")]` — nur das
  Unit-Test-Projekt.
- `Softwareschmiede.IntegrationTests` hat **keinen** Internals-Zugriff.

## Konsequenz für `internal`

Wird `ProzessStartenAsync` `internal`, kompilieren die Unit-Tests weiter
(`InternalsVisibleTo` vorhanden), die Integrationstests nur mit ergänztem
`InternalsVisibleTo("Softwareschmiede.IntegrationTests")`. Eine Umstellung der
Integrationstests auf `ProzessStartenUndCliStartenAsync` wäre die Alternative,
erfordert aber `KiAusfuehrungsService` + `ITerminalSessionFactory` im
Integrationstest-Fixture (deutlich größerer Umbau); die Issue-Beobachtung nennt
`internal` selbst als Option.
