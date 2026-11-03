# Tasks: Einheitlicher Pass-Einstieg für „Aufgabe abschließen"

Plan: [`issue-321-b832006b1351405c9fb896db8174f8e8-aufgabe-abschliessen-einheitliche/plan.md`](issue-321-b832006b1351405c9fb896db8174f8e8-aufgabe-abschliessen-einheitliche/plan.md)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---|---|---|---|
| 1 | Logik | `EntwicklungsprozessServiceOptions` um optionalen Parameter `PromptZeitVersandService` erweitern | Erledigt | — |
| 2 | Logik | `EntwicklungsprozessService.AbschliessenAsync` als koordinierten Pass-Einstieg implementieren (`RunAsync` + `AbschliessenCoordinatedAsync`: `StopCliAsync` → `CancelScheduledPrompt` → To-do-Check → Klon-Löschung → Status → Protokoll) | Erledigt | — |
| 3 | Logik | `ProzessStartenAsync` auf `internal` einschränken; `InternalsVisibleTo("Softwareschmiede.IntegrationTests")` in `AssemblyInfo.cs` ergänzen | Erledigt | — |
| 4 | UI | `TaskDetailViewModel.AufgabeAbschliessenAsync`: eigenen `CancelScheduledPrompt`-Aufruf entfernen | Erledigt | — |
| 5 | Logik | `ScmTaskCompletionMonitoringService.CheckTaskAsync`: eigenen `StopCliAsync`/`KiAusfuehrungsService`-Aufruf durch den Pass-Einstieg ersetzen | Erledigt | — |
| 6 | Konfiguration | `App.xaml.cs`: `PromptZeitVersandService` in die `EntwicklungsprozessServiceOptions`-Factory aufnehmen | Erledigt | — |
| 7 | Test-Infrastruktur | `TaskDetailViewModelTestFactory` und `ScmDispatchFixture`: `PromptZeitVersandService` in Options/DI aufnehmen | Erledigt | — |
| 8 | Tests (Unit) | `EntwicklungsprozessServiceTests`: Prompt-Abbruch und CLI-Stopp im Pass nachweisen | Erledigt | — |
| 9 | Tests (Integration) | `ScmTaskCompletionMonitoringServiceTests`: Prompt-Abbruch im Monitoring-Abschluss nachweisen | Erledigt | — |
| 10 | Dokumentation | `docs/help/aufgaben/ablauf-technisch.md`, `docs/help/aufgaben/business-rules.md`, `docs/help/plugins/github-ereignisse/{ablauf-technisch,business-rules}.md` an den einheitlichen Pass angleichen | Erledigt | — |
| 11 | Verifikation | `dotnet build Softwareschmiede.slnx -c Debug`; `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test` regulärer Lanes; `dotnet format Softwareschmiede.slnx --verify-no-changes` | Erledigt | — |
