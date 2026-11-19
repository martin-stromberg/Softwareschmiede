# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### ScmDispatchFixture.cs (ScmDispatchFixture)

- **Testqualität / Testisolation** — Zeile 201: `ScmEventExecutionLimitService.ReservierungenZuruecksetzen()` leert in `InitializeAsync` den prozessweit statischen Reservierungsbestand (`private static readonly Dictionary<Guid, StartPlatzReservierung> Reservierungen`). Das ist im `Softwareschmiede.Tests`-Projekt unkritisch (dort gilt `[assembly: Parallelization(Mode = ParallelMode.None)]` in `src/Softwareschmiede.Tests/AssemblyInfo.cs`), aber `Softwareschmiede.IntegrationTests` hat kein solches Attribut und keine gemeinsame `[Collection]` — die ~20 Fixture-basierten Testklassen laufen dort standardmäßig parallel. Die Rücksetzung unterscheidet nicht zwischen verwaisten und **lebenden** Reservierungen: Läuft zeitgleich ein Test einer anderen Klasse, der gerade eine Reservierung im Startfenster hält, wird sie ihm unter den Füßen weggezogen — die Zulassungsprüfung dieses Tests sieht danach einen fälschlich freien Platz. Konkret verwundbar ist `Dispatch_ConcurrentStartDuringHealthCheck_GlobalLimitOne_ExactlyOneStarts` (ScmEventDispatchTests_Limits.cs): Dort hält der erste Dispatch die Reservierung über den absichtlich blockierten Health-Check hinweg, während der zweite Dispatch vollständig läuft; eine in diesem Fenster erfolgende `InitializeAsync` eines parallel laufenden Tests lässt den zweiten Dispatch starten → `Assert.Equal("Wartend", ...)` bzw. `Assert.Empty(fixture.Sessions)` schlägt sporadisch, testablaufabhängig fehl. Da jede Fixture über einen eigenen `KiAusfuehrungsService` nur ihre eigenen Handles sieht, ist die statische Reservierungs-Map die einzige Fixture-übergreifende Kopplung — und der neue Reset macht genau diese Kopplung zu einem aktiven Störvektor statt sie zu entschärfen.

  Empfehlung: Fixture-basierte Integrationstestklassen serialisieren, damit sich Initialisierung und Reservierungsfenster nicht überlappen können — entweder eine gemeinsame xUnit-Collection (`[CollectionDefinition("ScmDispatch")]` in `Softwareschmiede.IntegrationTests` und `[Collection("ScmDispatch")]` auf allen `ScmDispatchFixture`-nutzenden Testklassen) oder einfacher `[assembly: Parallelization(Mode = ParallelMode.None)]` für die Assembly. Alternativ die Rücksetzung testseitig so einschränken, dass sie keine lebenden Reservierungen treffen kann (z. B. nur Einträge löschen, deren `GueltigBisUtcMs` bereits vor Fixture-Start angelegt wurde — erfordert einen Erstellzeitstempel an `StartPlatzReservierung`).

## Geprüfte Dateien

Liste aller geprüften Dateien:
- `src/Softwareschmiede/Application/Services/KiAusfuehrungsStatusKlassifikation.cs`
- `src/Softwareschmiede/Application/Services/ScmEventExecutionLimitService.cs`
- `src/Softwareschmiede/Application/Services/ScmEventOrchestrator.cs`
- `src/Softwareschmiede/Application/Services/AufgabeRecoveryService.cs`
- `src/Softwareschmiede/Application/Services/EntwicklungsprozessService.cs`
- `src/Softwareschmiede.Tests/Helpers/ScmDispatchFixture.cs`
- `src/Softwareschmiede.Tests/Application/Services/ScmEventExecutionLimitServiceTests.cs`
- `src/Softwareschmiede.Tests/Application/Services/KiAusfuehrungsStatusKlassifikationTests.cs`
- `src/Softwareschmiede.IntegrationTests/Services/ScmEventDispatchTests_Limits.cs`
- `src/Softwareschmiede.IntegrationTests/Services/ScmEventDispatchTests_Startpruefung.cs`
