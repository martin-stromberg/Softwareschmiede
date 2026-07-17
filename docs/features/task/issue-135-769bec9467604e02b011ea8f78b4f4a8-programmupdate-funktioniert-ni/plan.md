# Umsetzungsplan: Programmupdate-Sicherheitsprüfung ignoriert nicht-laufende Aufgaben (Issue #135)

## Übersicht

Die Sicherheitsprüfung vor einem Programmupdate (`CliUpdateSafetyService.CheckAsync()`) stuft aktuell Aufgaben mit `LaufStatus == null` fälschlicherweise als blockierend ein, weil der Filter `LaufStatus != AufgabeLaufStatus.WartetAufEingabe` verwendet und `null != WartetAufEingabe` zu `true` auswertet. Umgesetzt wird eine reine Logik-Korrektur des Filters (nur `LaufStatus == AufgabeLaufStatus.Laeuft` blockiert) sowie die Anpassung des bestehenden, an die Fehllogik gekoppelten Unit-Tests. Betroffen ist ausschließlich die Application-Schicht (`Application/Services/Updates`) und deren Test.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|------------------|------------|
| Filterkriterium in `CheckAsync()` | Positiver Vergleich `a.LaufStatus == AufgabeLaufStatus.Laeuft` statt Negativ-Vergleich | Explizite Whitelist des einzigen blockierenden Zustands ist null-sicher und deckt sich mit der Domain-Semantik (`null` = "Bereit"/nicht initialisiert, `WartetAufEingabe` = bereits geplant, nicht blockierend). Ein Negativ-Vergleich müsste `null` gesondert ausschließen und bliebe fehleranfällig. |
| `LaufStatus == null` bei gesetzter `AktiveRunId` | Als erwarteter, nicht-blockierender Übergangszustand akzeptieren — keine Initialisierung auf einen Defaultwert | `AufgabeService.AktivenLaufSetzenAsync()` setzt bewusst nur `AktiveRunId`; `LaufStatus` wird erst später von `CliProcessManager` anhand des `PseudoConsoleSession.RuntimeStatusChanged`-Ereignisses gesetzt. `null` ist damit ein legitimer „gerade gestartet, noch nicht klassifiziert"-Zustand und darf das Update nicht blockieren. (Klärt offene Frage 1 aus `requirement.md`.) |

## Programmabläufe

### Sicherheitsprüfung vor Programmupdate (korrigiert)

1. Update-Orchestrierung ruft `ICliUpdateSafetyService.CheckAsync(ct)` auf.
2. `CheckAsync()` lädt aktive Aufgaben über `_aufgabeService.GetAktiveAufgabenAsync(ct)` (Status `Gestartet`/`Wartend`, max. 20).
3. **Korrigierter Filter:** Es werden nur Aufgaben mit `AktiveRunId is not null` **und** `LaufStatus == AufgabeLaufStatus.Laeuft` als riskant erfasst.
   - `LaufStatus == null` → nicht blockierend (kein aktiver, klassifizierter Lauf).
   - `LaufStatus == AufgabeLaufStatus.WartetAufEingabe` → nicht blockierend.
4. Für jede riskante Aufgabe wird `"{Titel} ({Id})"` in die Ergebnisliste übernommen.
5. Bei mindestens einer riskanten Aufgabe wird die Anzahl über `_logger.LogInformation` protokolliert.
6. `CliUpdateSafetyResult(riskyTasks.Count, riskyTasks)` wird zurückgegeben; `RequiresConfirmation` ist `true`, sobald `RiskyTaskCount > 0`.

Beteiligte Klassen/Komponenten: `CliUpdateSafetyService`, `AufgabeService`, `Aufgabe`, `AufgabeLaufStatus`, `CliUpdateSafetyResult`

## Neue Klassen

Keine.

## Änderungen an bestehenden Klassen

### `CliUpdateSafetyService` (Application-Service)

- **Geänderte Methoden:** `CheckAsync(CancellationToken ct = default)` — Das Filterprädikat in Zeile 24 wird von `a.LaufStatus != AufgabeLaufStatus.WartetAufEingabe` auf `a.LaufStatus == AufgabeLaufStatus.Laeuft` geändert. Damit blockieren nur tatsächlich laufende CLI-Prozesse das Update; `null` und `WartetAufEingabe` sind nicht blockierend. Keine Signatur-, Rückgabetyp- oder Abhängigkeitsänderung.

## Datenbankmigrationen

Keine.

## Validierungsregeln

Keine.

## Konfigurationsänderungen

Keine.

## Seiteneffekte und Risiken

- **`CliUpdateSafetyServiceTests`:** Der bestehende Test `CheckAsync_ShouldTreatRunningAndNullStatusAsRisky()` erwartet `RiskyTaskCount == 2` und bricht nach der Korrektur (erwartet dann 1). Muss angepasst werden (siehe Tests).
- **UI/Converter (`AppConverters.KiAusfuehrungsStatusConverter`, Zeile 133):** Nutzt `LaufStatus == AufgabeLaufStatus.WartetAufEingabe` ausschließlich zur Anzeige („⏸ Wartet" vs. „▶ Läuft") und ist von der Filterkorrektur unabhängig — kein Anpassungsbedarf. (Klärt offene Frage 2 aus `requirement.md`: Es existiert keine weitere Stelle mit derselben fehlerhaften Filterlogik; ein repository-weiter Suchlauf über `LaufStatus (!=|==) AufgabeLaufStatus` bestätigt, dass nur `CliUpdateSafetyService.cs:24` betroffen ist.)
- **Fachliches Verhalten:** Nutzer können ein Programmupdate künftig starten, während Aufgaben lediglich als „Bereit" (`null`) oder „Wartet auf Eingabe" markiert sind, ohne unnötige Blockade-/Bestätigungsabfrage. Aufgaben mit aktiv laufendem CLI-Prozess lösen weiterhin die Bestätigungsabfrage aus.

## Umsetzungsreihenfolge

1. **Filterkorrektur in `CliUpdateSafetyService.CheckAsync()`**
   - Voraussetzungen: Keine (Enum `AufgabeLaufStatus`, Entity `Aufgabe.LaufStatus` und `CliUpdateSafetyResult` sind bereits vorhanden).
   - Beschreibung: Filterprädikat auf `a.AktiveRunId is not null && a.LaufStatus == AufgabeLaufStatus.Laeuft` umstellen.

2. **Bestehenden Unit-Test an die korrigierte Semantik anpassen**
   - Voraussetzungen: Schritt 1 abgeschlossen; `CliUpdateSafetyServiceTests` und Hilfsmethode `CreateActiveTaskAsync` sind vorhanden.
   - Beschreibung: Test umbenennen (z. B. `CheckAsync_ShouldTreatOnlyRunningStatusAsRisky`), `RiskyTaskCount`-Erwartung auf `1` setzen, ausschließlich die `Laeuft`-Aufgabe als riskant verifizieren und explizit prüfen, dass `null`- und `WartetAufEingabe`-Aufgaben **nicht** in `RiskyTasks` enthalten sind.

3. **Build + Tests verifizieren**
   - Voraussetzungen: Schritte 1–2 abgeschlossen.
   - Beschreibung: Vollständigen Build ausführen, anschließend `CliUpdateSafetyServiceTests` (und angrenzende Update-/AufgabeService-Tests) laufen lassen. Projektregeln beachten: `dotnet test` nie im Hintergrund, `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` setzen, keine `Softwareschmiede.App.exe` beenden.

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|---------------------|------------|-------------------------------------|
| (optional) `CheckAsync_ShouldNotTreatNullStatusAsRisky` | `CliUpdateSafetyServiceTests` | Falls die `null`-Nicht-Blockade nicht bereits vollständig im angepassten Haupttest abgedeckt wird: dedizierte Prüfung, dass eine Aufgabe mit `LaufStatus == null` und gesetzter `AktiveRunId` `RequiresConfirmation == false` liefert. Bevorzugt jedoch in den umbenannten Haupttest integrieren, statt separat anzulegen. |

Hinweis: Die Hilfsmethode `CreateActiveTaskAsync(string, AufgabeLaufStatus?)` existiert bereits und deckt alle drei Zustände (`Laeuft`, `WartetAufEingabe`, `null`) ab — keine neue Infrastruktur nötig.

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `CliUpdateSafetyServiceTests.CheckAsync_ShouldTreatRunningAndNullStatusAsRisky` | An die Fehllogik gekoppelt (`RiskyTaskCount == 2`, `null` als riskant). Nach der Korrektur ist nur `Laeuft` riskant (`RiskyTaskCount == 1`); Test umbenennen und Assertions auf die korrigierte Semantik umstellen. |

### E2E-Tests (Pflicht)

| Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium |
|----------|------------------------|-------------------------------|
| — | — | Kein sinnvoller E2E-Test möglich/erforderlich: Die Änderung betrifft ausschließlich ein internes Filterprädikat eines Services ohne neue oder geänderte UI-Interaktion. Der reale Update-Auslöseablauf (Download/Neustart) ist nicht deterministisch in einem E2E-Test reproduzierbar. Der Happy Path (`null`/`WartetAufEingabe` nicht blockierend, `Laeuft` blockierend) wird vollständig und deterministisch durch den angepassten Unit-Test in `CliUpdateSafetyServiceTests` abgedeckt. |

Bestehende betroffene E2E-Tests: Keine.

## Offene Punkte

Keine.
