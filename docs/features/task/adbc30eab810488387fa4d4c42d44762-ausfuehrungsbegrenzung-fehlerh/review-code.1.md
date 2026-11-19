# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### ScmEventOrchestrator.cs (ScmEventOrchestrator)

- **Dokumentation/Kommentar** — Zeile 448: Tippfehler im Kommentar „Die zurückgestufte Pausen kann" (Plural/Singular-Fehler → korrekt: „Die zurückgestufte Pause"). Zudem erläutert der Kommentar nur den Pausen-Rückstufungs-Fall, obwohl `startPruefungsAusloeser?.StartpruefungAnfordern()` in Zeile 450 bei **jedem** erfolgreichen Abbruch auslöst — auch bei Abbrüchen ganz ohne Pausenbezug. Das ist fachlich korrekt (ein abgebrochenes früheres Ereignis entsperrt spätere Ereignisse derselben Aufgabe, vgl. die Früheres-Ereignis-Sperre in `DispatchCoreAsync`), wird vom Kommentar aber nicht abgedeckt und kann Leser zur falschen Annahme verleiten, der Auslöser sei auf den Verzögert-Zweig beschränkt.

  Empfehlung: Tippfehler korrigieren („Pausen" → „Pause") und den Kommentar um den zweiten Auslösegrund erweitern, z. B. „Ein Abbruch kann einen Ausführungsplatz freigeben (zurückgestufte Pause) oder spätere Ereignisse derselben Aufgabe entsperren — die Startprüfung soll das nicht erst beim nächsten Poll sehen."

### ScmEventExecutionLimitService.cs (ScmEventExecutionLimitService)

- **Fehlende Kapselung / Doppelter Code** — Zeile 221: Das Prädikat „Pausenzeitpunkt gesetzt und in der Zukunft" (`x.PausiertBisUtc is null || x.PausiertBisUtc <= jetzt`, invertiert gelesen) wird mit dieser Änderung an einer weiteren Stelle inline wiederholt. Dasselbe Prädikat steht bereits an mindestens vier weiteren Stellen im Produktivcode (`ScmEventOrchestrator.cs:999`, `KiAusfuehrungsStatusKlassifikation.cs:25`, `EntwicklungsprozessService.cs:989`, `AufgabeRecoveryService.cs:86`), teilweise mit `DateTimeOffset.UtcNow` statt des injizierten `TimeProvider`. Die neue Stelle selbst ist korrekt (nutzt `time.GetUtcNow()`), verfestigt aber die ungekapselte Wiederholung.

  Empfehlung: Eine zentrale Hilfsmethode einführen, z. B. eine `DateTimeOffset?`-Extension `IstAktuellPausiert(DateTimeOffset jetzt)` (funktioniert auch auf anonymen Projektionen), diese im neuen Code verwenden und die bestehenden Stellen nachziehen — letztere ersetzt damit nebenbei auch die `DateTimeOffset.UtcNow`-Direktaufrufe.

## Geprüfte Dateien

- `src/Softwareschmiede/Application/Services/ScmEventExecutionLimitService.cs`
- `src/Softwareschmiede/Application/Services/ScmEventOrchestrator.cs`
- `src/Softwareschmiede.Tests/Application/Services/ScmEventExecutionLimitServiceTests.cs`
- `src/Softwareschmiede.IntegrationTests/Services/ScmEventDispatchTests_Limits.cs`
- `src/Softwareschmiede.IntegrationTests/Services/ScmEventDispatchTests_Startpruefung.cs`
