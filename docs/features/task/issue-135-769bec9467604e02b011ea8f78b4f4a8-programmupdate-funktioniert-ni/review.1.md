# Plan-Review

## Ergebnis

**Status:** Vollständig umgesetzt

## Umgesetzte Planelemente

- [x] `CliUpdateSafetyService.CheckAsync()` (Methode) — Filterprädikat korrigiert: `src/Softwareschmiede/Application/Services/Updates/CliUpdateSafetyService.cs:24` nutzt jetzt `a.AktiveRunId is not null && a.LaufStatus == AufgabeLaufStatus.Laeuft` (positiver Whitelist-Vergleich statt Negativ-Vergleich). Keine Signatur-, Rückgabetyp- oder Abhängigkeitsänderung.
- [x] Test umbenannt/angepasst — `CheckAsync_ShouldTreatRunningAndNullStatusAsRisky` existiert nicht mehr; ersetzt durch `CheckAsync_ShouldTreatOnlyRunningStatusAsRisky` in `src/Softwareschmiede.Tests/Application/Services/Updates/CliUpdateSafetyServiceTests.cs:36`.
- [x] `RiskyTaskCount == 1` erwartet (Zeile 46) und ausschließlich die `Laeuft`-Aufgabe als riskant verifiziert (Zeile 47).
- [x] Explizite Prüfung, dass `null`- und `WartetAufEingabe`-Aufgaben **nicht** in `RiskyTasks` enthalten sind (Zeilen 48–49, `NotContain`).
- [x] Hilfsmethode `CreateActiveTaskAsync(string, AufgabeLaufStatus?)` — vorhanden (Zeile 52), deckt alle drei Zustände (`Laeuft`, `WartetAufEingabe`, `null`) ab; keine neue Infrastruktur nötig (plankonform).
- [x] Build + Tests verifiziert — Vollständiger Build erfolgreich, `CliUpdateSafetyServiceTests` grün (1 bestanden, 0 Fehler; `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1`, kein Hintergrund-`dotnet test`).

Planpositionen ohne Umsetzungsbedarf (plankonform als „Keine" ausgewiesen): Neue Klassen, Datenbankmigrationen, Validierungsregeln, Konfigurationsänderungen, E2E-Tests.

## Offene Aufgaben

Keine.

## Hinweise

- Der optionale Zusatztest `CheckAsync_ShouldNotTreatNullStatusAsRisky` wurde — wie im Plan bevorzugt — nicht separat angelegt, sondern in den umbenannten Haupttest integriert (`NotContain` für `nullStatus`). Das entspricht der Planempfehlung.
- Die im Plan als unabhängig markierte UI-Stelle (`AppConverters.KiAusfuehrungsStatusConverter`) wurde erwartungsgemäß nicht verändert.
