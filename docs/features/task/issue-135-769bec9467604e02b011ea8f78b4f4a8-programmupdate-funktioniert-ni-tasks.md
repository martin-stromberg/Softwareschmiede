# Tasks: Programmupdate-Sicherheitsprüfung ignoriert nicht-laufende Aufgaben (Issue #135)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Logik | Filterprädikat in `CliUpdateSafetyService.CheckAsync()` von `a.LaufStatus != AufgabeLaufStatus.WartetAufEingabe` auf `a.LaufStatus == AufgabeLaufStatus.Laeuft` umstellen | Offen | — |
| 2 | Tests | Bestehenden Test `CliUpdateSafetyServiceTests.CheckAsync_ShouldTreatRunningAndNullStatusAsRisky` umbenennen und Assertions auf `RiskyTaskCount == 1` (nur `Laeuft`) korrigieren | Offen | — |
| 3 | Tests | Im angepassten Test explizit verifizieren, dass Aufgaben mit `LaufStatus == null` und `LaufStatus == AufgabeLaufStatus.WartetAufEingabe` nicht in `RiskyTasks` enthalten sind | Offen | — |
| 4 | Tests | Vollständigen Build ausführen und `CliUpdateSafetyServiceTests` grün verifizieren (kein Hintergrund-`dotnet test`, `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1`) | Offen | — |
