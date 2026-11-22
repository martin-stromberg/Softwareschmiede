# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### ScmEventDispatchTests_Reconcile.cs / ScmTaskCompletionMonitoringServiceTests.cs (Testklassen)

- **Doppelter Code** — Zwei nahezu identische Hilfsblöcke sind zwischen den Testdateien dupliziert: Die deterministische Zeitquelle existiert einmal als `TestClock` (`ScmEventDispatchTests_Reconcile.cs` Z. 214–218, neu in diesem Branch: `Now`-Setter + `GetUtcNow`) und einmal als `TestTimeProvider` (`ScmTaskCompletionMonitoringServiceTests.cs` Z. 603–608: `_now`-Feld + `Advance`); dazu kommt das wortgleiche Feld `private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)` in beiden Dateien (`ScmEventDispatchTests_Reconcile.cs` Z. 18, `ScmTaskCompletionMonitoringServiceTests.cs` Z. 21).

  Empfehlung: Eine gemeinsame deterministische `TimeProvider`-Testquelle im geteilten Testsupport (Namespace `Softwareschmiede.TestSupport`, z. B. neben `ScmDispatchFixture`) anbieten, die sowohl ein setzbares `Now` als auch `Advance` abdeckt, und beide Testklassen darauf umstellen; das identische `Json`-Feld kann in denselben Schritt in eine gemeinsame Hilfsstelle oder in `ScmEventStore.JsonOptions` (gleiche Optionen: `JsonSerializerDefaults.Web`) wandern.

## Geprüfte Dateien

- `src/Softwareschmiede/Domain/Entities/ScmIncomingEventStatus.cs` (neu)
- `src/Softwareschmiede/Domain/Entities/ScmIncomingEvent.cs`
- `src/Softwareschmiede/Application/Services/ScmEventOrchestrator.cs`
- `src/Softwareschmiede/Application/Services/ScmTaskCompletionMonitoringService.cs`
- `src/Softwareschmiede/Application/Services/PullRequestReferenceService.cs`
- `src/Softwareschmiede/Application/Services/ScmEventStore.cs`
- `src/Softwareschmiede/Application/Services/ScmEventExchangeService.cs`
- `src/Softwareschmiede/Application/Services/ScmDispatchPriority.cs`
- `src/Softwareschmiede/Application/Services/RunningAutomationsCount.cs`
- `src/Softwareschmiede.App/ViewModels/MainWindowViewModel.cs`
- `src/Softwareschmiede.App/ViewModels/ScmEventQueueViewModel.cs`
- `src/Softwareschmiede.Tests/Domain/Entities/ScmIncomingEventStatusTests.cs` (neu)
- `src/Softwareschmiede.Tests/Helpers/ScmDispatchFixture.cs`
- `src/Softwareschmiede.Tests/E2E/E2E_ScmEvents.cs`
- `src/Softwareschmiede.IntegrationTests/Services/ScmEventDispatchTests_Ordering.cs` (neu)
- `src/Softwareschmiede.IntegrationTests/Services/ScmEventDispatchTests_Reconcile.cs` (neu)
- `src/Softwareschmiede.IntegrationTests/Services/ScmTaskCompletionMonitoringServiceTests.cs`
