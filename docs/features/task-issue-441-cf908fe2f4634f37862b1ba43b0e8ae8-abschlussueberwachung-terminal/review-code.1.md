# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### ScmIncomingEventStatus.cs (ScmIncomingEventStatus)

- **Toter Code / Speculative Generality** — `BlocksTaskCompletion` (Z. 64) und `IsTransient` (Z. 71) haben keinen einzigen produktiven Aufrufer. Sämtliche Produktiv-Prüfstellen (`ScmTaskCompletionMonitoringService.CheckTaskAsync` Z. 86/137, `ScmEventOrchestrator.DispatchCoreAsync` Z. 182) müssen laut eigener XML-Doku ohnehin `!NonBlocking.Contains(x.Status)` im EF-Ausdruck verwenden; nur die neuen Unit-Tests rufen die Hilfsmethoden auf — getestet wird damit Code, den niemand benutzt.

  Empfehlung: Beide Methoden entfernen (und die zugehörigen Asserts in `ScmIncomingEventStatusTests` mit ihnen). Falls ein In-Memory-Aufrufer absehbar ist, genügt eine einzige Methode — siehe nächster Befund.

- **Doppelter Code** — `BlocksTaskCompletion` und `IsTransient` (Z. 64/71) sind zwei öffentliche Methoden mit identischer Implementierung (`!NonBlocking.Contains(status)`); die Doku bestätigt selbst „deckungsgleich". Zwei deckungsgleiche Einstiegspunkte laden zu divergierender Nutzung ein, ohne Mehrwert zu liefern.

  Empfehlung: Höchstens eine der beiden Methoden behalten; bei Beibehaltung beider sollte die eine die andere delegieren (`IsTransient(status) => BlocksTaskCompletion(status)`), statt den Ausdruck zu duplizieren.

### ScmEventOrchestrator.cs (ScmEventOrchestrator)

- **Namenskonventionen und Einheitlichkeit / hardcodierte Werte statt Konstanten** — Die neue Klasse `ScmIncomingEventStatus` deklariert die Statuswerte ausdrücklich als „verbindlich", doch dieselbe geänderte Datei verwendet weiterhin durchgehend String-Literale: u. a. `DispatchDueEventsAsync` (Z. 43–44), `DispatchCoreAsync` (Z. 93, 132, 161, 167, 321, 326, 537, 584, 586, 589, 596), `CancelAsync` (Z. 427–431), `IsCancelledAsync` (Z. 1014), `WaitAsync` (Z. 1022). Damit existieren im selben File zwei Wahrheitsquellen für dieselben Werte; ein Tippfehler in einem Literal bliebe kompilierbar und fiele nicht auf.

  Empfehlung: In `ScmEventOrchestrator.cs` sämtliche Vergleiche und Zuweisungen der Ereignis-Stati auf die Konstanten `ScmIncomingEventStatus.*` umstellen (z. B. `item.Status == ScmIncomingEventStatus.Abgebrochen`, `item.Status = ScmIncomingEventStatus.VerarbeitungUngeklärt`). Mindestens die unmittelbar von diesem Change betroffenen Methoden (`DispatchCoreAsync`, `ReconcileAttemptAsync`, `IsCancelledAsync`, `LinkReportedPullRequestAsync`-Kontext) sollten konsistent sein.

- **Doppelter Code** — `LinkReportedPullRequestAsync` (Z. 989–990) dupliziert das Lookup-Prädikat `Provider + RepositoryId + PullRequestNumber`, das `PullRequestReferenceService.SaveReportedAsync` (`PullRequestReferenceService.cs` Z. 111–116) intern identisch erneut ausführt — inklusive der vorab separat abgeleiteten Provider-Auflösung (`PullRequestProviderDescriptor.TryGetProviderForPluginPrefix`, Z. 984, die `SaveReportedAsync` Z. 107–108 wiederholt) und der doppelten `PullRequestRepositoryId.Normalize`-Anwendung. Weicht die Abgleichslogik im Service später ab, divergieren Vorprüfung und tatsächliche Speicherentscheidung still.

  Empfehlung: Eine öffentliche Find-Methode auf `PullRequestReferenceService` ergänzen (z. B. `FindByKeyAsync(pluginPrefix, repositoryId, pullRequestNumber)`), die Provider-Ableitung und Normalisierung intern kapselt; `LinkReportedPullRequestAsync` und `SaveReportedAsync` nutzen beide dieselbe Auflösung.

### ScmEventDispatchTests_Ordering.cs / ScmTaskCompletionMonitoringServiceTests.cs (Testklassen)

- **Doppelter Code** — Die private Hilfsmethode `SetEventStatusAsync` ist in beiden Dateien wortgleich dupliziert (`ScmEventDispatchTests_Ordering.cs` Z. 117–124, `ScmTaskCompletionMonitoringServiceTests.cs` Z. 482–488): identische Scope-Erzeugung, identisches `ExecuteUpdateAsync` auf `ScmIncomingEvents.Status`.

  Empfehlung: Den Helper einmalig in die gemeinsam genutzte `ScmDispatchFixture` verschieben (z. B. `fixture.SetEventStatusAsync(eventId, status)`), analog zu den bereits dort liegenden Hilfsmethoden `EventAsync`/`DispatchAsync`, und beide Testklassen darauf umstellen.

## Geprüfte Dateien

- `src/Softwareschmiede/Domain/Entities/ScmIncomingEventStatus.cs` (neu)
- `src/Softwareschmiede/Application/Services/ScmEventOrchestrator.cs`
- `src/Softwareschmiede/Application/Services/ScmTaskCompletionMonitoringService.cs`
- `src/Softwareschmiede.Tests/Domain/Entities/ScmIncomingEventStatusTests.cs` (neu)
- `src/Softwareschmiede.IntegrationTests/Services/ScmEventDispatchTests_Ordering.cs` (neu)
- `src/Softwareschmiede.IntegrationTests/Services/ScmEventDispatchTests_Reconcile.cs` (neu)
- `src/Softwareschmiede.IntegrationTests/Services/ScmTaskCompletionMonitoringServiceTests.cs`
