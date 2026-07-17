# Umsetzungsplan: Programmupdate-Sicherheitsprüfung ermittelt aktiven Lauf abweichend von der Aufgabenpanel-Anzeige (Issue #135)

## Übersicht

Die Sicherheitsprüfung vor einem Programmupdate (`CliUpdateSafetyService.CheckAsync()`) entscheidet allein anhand des persistierten Feldes `LaufStatus == AufgabeLaufStatus.Laeuft`, ob eine Aufgabe blockierend läuft. Dieses Feld kann veraltet (`stale`) in der Datenbank stehenbleiben, wenn ein CLI-Prozess ohne sauberes Zurücksetzen verlorengeht (Crash, verlorenes Exited-Event, App-Neustart) — die Aufgabe erscheint dann fälschlich weiterhin als „läuft" und blockiert das Update, obwohl das Aufgabenpanel sie längst als „✓ Bereit" anzeigt. Umgesetzt wird die Angleichung der Update-Prüfung an **exakt dieselbe** Aktiv-Ermittlung, die die Anzeige im Aufgabenpanel (`KiAusfuehrungsStatusConverter`) verwendet: eine Heartbeat-Timeout-Prüfung. Um doppelte, wieder auseinanderdriftende Kopien zu vermeiden, wird diese Bedingung in eine gemeinsam genutzte Hilfsmethode der Application-Schicht extrahiert, die sowohl der WPF-Converter (Projekt `Softwareschmiede.App`) als auch der Application-Service `CliUpdateSafetyService` (Projekt `Softwareschmiede`) aufrufen.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|------------------|------------|
| Gemeinsame Aktiv-Ermittlung | Neue **statische, seiteneffektfreie** Hilfsmethode in der Application-Schicht: `AufgabeLaufAktivitaet.IstAktiv(string? aktiveRunId, DateTimeOffset? lastHeartbeatUtc, DateTimeOffset nowUtc)` | Die Bedingung ist eine reine Funktion über drei Eingaben ohne Abhängigkeiten außer der aktuellen Zeit. Ein injizierbarer Service wäre Over-Engineering (kein State, keine DI-Abhängigkeit); eine statische Methode entspricht dem bestehenden Muster (`KiAusfuehrungsStatusConverter` und `AufgabeRecoveryService` verwenden `DateTimeOffset.UtcNow` direkt). Die Zeit wird als Parameter `nowUtc` hereingereicht, damit die Methode ohne Zeitmanipulation exakt und deterministisch unit-testbar ist. |
| Ablageort der Hilfsmethode | Projekt `Softwareschmiede`, Namespace `Softwareschmiede.Application.Services` (neue Datei `AufgabeLaufAktivitaet.cs`) | Schichtengrenze: `CliUpdateSafetyService` liegt in der Application-Schicht (`Softwareschmiede`), der Converter in der UI-Schicht (`Softwareschmiede.App`), die die Application-Schicht bereits referenziert (der Converter nutzt heute schon `AufgabeRecoveryService.HeartbeatTimeoutMinutes` aus genau diesem Namespace). Die gemeinsame Logik muss daher in der Application-Schicht liegen, damit **beide** Seiten sie aufrufen können — nicht umgekehrt. |
| Timeout-Schwelle | Weiterverwendung der bestehenden Konstante `AufgabeRecoveryService.HeartbeatTimeoutMinutes` (5 Minuten) — nicht duplizieren, nicht verschieben | Der Converter referenziert diese Konstante bereits; die Hilfsmethode referenziert dieselbe Konstante. Damit existiert genau **eine** Quelle der Schwelle. Ein Verschieben der Konstante würde unnötige zusätzliche Änderungen erzeugen. |
| Blockierkriterium der Update-Prüfung: `LaufStatus` vs. Heartbeat | Die Update-Prüfung stuft eine Aufgabe **allein** anhand der Heartbeat-Aktiv-Bedingung als blockierend ein (`AktiveRunId != null` **und** frischer `LastHeartbeatUtc`); `LaufStatus` wird **nicht mehr** als Kriterium herangezogen | Explizite Anforderung: Die Prüfung muss den Status „genauso ermitteln wie die Anzeige im Aufgabenpanel". Das Panel wertet eine Aufgabe genau dann als aktiv laufend (Anzeige „▶ Läuft"/„⏸ Wartet"), wenn die Heartbeat-Bedingung erfüllt ist — unabhängig vom (potenziell veralteten) `LaufStatus`. `LaufStatus` unterscheidet im Converter nur noch den Anzeige-Substatus „Läuft" vs. „Wartet" **innerhalb** eines bereits als aktiv erkannten Laufs; für die Ja/Nein-Frage „blockiert das Update" ist er irrelevant. |
| Behandlung „aktiv, aber `LaufStatus == WartetAufEingabe`" | Blockiert das Update weiterhin, sofern der Heartbeat frisch ist | Ein lebender CLI-Prozess, der auf Benutzereingabe wartet (frischer Heartbeat), würde durch ein Update/Neustart genauso unterbrochen wie ein aktiv arbeitender. Das Panel zeigt ihn als aktiv („⏸ Wartet") an; die Update-Prüfung muss ihn folgerichtig ebenfalls als riskant behandeln. |

## Programmabläufe

### Aktiv-Ermittlung (gemeinsame Hilfsmethode)

1. Aufrufer übergibt `aktiveRunId`, `lastHeartbeatUtc` und `nowUtc`.
2. `AufgabeLaufAktivitaet.IstAktiv(...)` liefert `true`, wenn **alle** Bedingungen erfüllt sind:
   - `aktiveRunId` ist nicht `null`,
   - `lastHeartbeatUtc` ist nicht `null`,
   - `nowUtc - lastHeartbeatUtc.Value < TimeSpan.FromMinutes(AufgabeRecoveryService.HeartbeatTimeoutMinutes)` (strikt kleiner — genau wie im Converter, exakt an der Schwelle gilt „nicht aktiv").
3. Andernfalls `false`.

Beteiligte Klassen/Komponenten: `AufgabeLaufAktivitaet`, `AufgabeRecoveryService` (Konstante)

### Anzeige im Aufgabenpanel (angepasst, Verhalten unverändert)

1. `KiAusfuehrungsStatusConverter.Convert()` baut wie bisher ein `StatusDaten`-Record aus `Aufgabe` bzw. `AktiveAufgabePanelItem`.
2. Bei `HasScheduledPrompt` → „⏳ Prompt in Wartestellung" (unverändert; UI-eigenes Konzept, für die Update-Prüfung irrelevant).
3. **Statt der bisherigen Inline-Bedingung** ruft der Converter `AufgabeLaufAktivitaet.IstAktiv(status.AktiveRunId, status.LastHeartbeatUtc, DateTimeOffset.UtcNow)` auf.
   - `true` → weiterhin Unterscheidung über `LaufStatus`: `WartetAufEingabe` → „⏸ Wartet", sonst → „▶ Läuft".
4. Sonst: `Status == Wartend` → „⏸ Wartet", andernfalls „✓ Bereit" (unverändert).

Beteiligte Klassen/Komponenten: `KiAusfuehrungsStatusConverter`, `AufgabeLaufAktivitaet`

### Sicherheitsprüfung vor Programmupdate (korrigiert)

1. Update-Orchestrierung ruft `ICliUpdateSafetyService.CheckAsync(ct)` auf.
2. `CheckAsync()` lädt aktive Aufgaben über `_aufgabeService.GetAktiveAufgabenAsync(ct)` (Status `Gestartet`/`Wartend`, materialisierte `List<Aufgabe>`, max. 20).
3. **Korrigierter Filter:** Eine Aufgabe gilt als riskant, wenn `AufgabeLaufAktivitaet.IstAktiv(a.AktiveRunId, a.LastHeartbeatUtc, DateTimeOffset.UtcNow)` `true` liefert — identisch zur Panel-Anzeige.
   - Veralteter `LaufStatus == Laeuft` mit abgelaufenem/`null`-Heartbeat → **nicht** blockierend (Kern der Fehlerbehebung).
   - `AktiveRunId == null` → nicht blockierend.
4. Für jede riskante Aufgabe wird `"{Titel} ({Id})"` in die Ergebnisliste übernommen.
5. Bei mindestens einer riskanten Aufgabe wird die Anzahl über `_logger.LogInformation` protokolliert (unverändert).
6. `CliUpdateSafetyResult(riskyTasks.Count, riskyTasks)` wird zurückgegeben; `RequiresConfirmation` ist `true`, sobald `RiskyTaskCount > 0`.

Beteiligte Klassen/Komponenten: `CliUpdateSafetyService`, `AufgabeService`, `Aufgabe`, `AufgabeLaufAktivitaet`, `CliUpdateSafetyResult`

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `AufgabeLaufAktivitaet` | Statische Hilfsklasse (Application-Schicht) | Kapselt die einzige Definition der „ist der CLI-Lauf einer Aufgabe aktiv?"-Bedingung (Heartbeat-Timeout). Wird von `KiAusfuehrungsStatusConverter` (Anzeige) und `CliUpdateSafetyService` (Update-Prüfung) genutzt, damit beide dieselbe Bedingung verwenden. |

## Änderungen an bestehenden Klassen

### `CliUpdateSafetyService` (Application-Service)

- **Geänderte Methoden:** `CheckAsync(CancellationToken ct = default)` — Das Filterprädikat in Zeile 24 (`a.AktiveRunId is not null && a.LaufStatus == AufgabeLaufStatus.Laeuft`) wird durch den Aufruf `AufgabeLaufAktivitaet.IstAktiv(a.AktiveRunId, a.LastHeartbeatUtc, DateTimeOffset.UtcNow)` ersetzt. Damit blockieren nur Aufgaben mit tatsächlich frischem Heartbeat das Update; veraltete `LaufStatus`-Werte blockieren nicht mehr. Der bislang importierte Namespace `Softwareschmiede.Domain.Enums` wird nicht mehr benötigt (kein direkter `AufgabeLaufStatus`-Bezug mehr) und kann entfallen. Keine Signatur-, Rückgabetyp- oder Abhängigkeitsänderung.

### `KiAusfuehrungsStatusConverter` (WPF-Converter, `AppConverters.cs`)

- **Geänderte Methoden:** `Convert(...)` — Die Inline-Bedingung (aktuell Zeilen 126–128: `status.AktiveRunId != null && status.LastHeartbeatUtc != null && DateTimeOffset.UtcNow - status.LastHeartbeatUtc.Value < TimeSpan.FromMinutes(AufgabeRecoveryService.HeartbeatTimeoutMinutes)`) wird durch `AufgabeLaufAktivitaet.IstAktiv(status.AktiveRunId, status.LastHeartbeatUtc, DateTimeOffset.UtcNow)` ersetzt. Verhalten identisch; die Substatus-Unterscheidung `LaufStatus == WartetAufEingabe` → „⏸ Wartet" vs. „▶ Läuft" bleibt im `true`-Zweig unverändert. Reine Extraktion, keine Verhaltensänderung des Converters.

## Datenbankmigrationen

Keine.

## Validierungsregeln

Keine.

## Konfigurationsänderungen

Keine.

## Seiteneffekte und Risiken

- **Verhaltensänderung Update-Prüfung (beabsichtigt):** Aufgaben mit veraltetem `LaufStatus == Laeuft`, aber abgelaufenem/`null`-Heartbeat blockieren das Update nicht mehr — das ist die eigentliche Fehlerbehebung. Umgekehrt blockiert ab sofort auch eine Aufgabe mit frischem Heartbeat und `LaufStatus == WartetAufEingabe` das Update (vorher nur `Laeuft`); dies ist konsistent mit der Panel-Anzeige („⏸ Wartet" gilt dort als aktiv laufend) und daher gewollt.
- **Converter (`KiAusfuehrungsStatusConverter`):** Reine Extraktion der bestehenden Bedingung in `AufgabeLaufAktivitaet.IstAktiv`. Das Anzeigeverhalten bleibt exakt gleich; die bestehenden `KiAusfuehrungsStatusConverterTests` (inkl. `KiAusfuehrungsStatusConverterTests_ZeitgesteuerterPrompt`) müssen weiterhin unverändert grün sein und dienen als Regressionsabsicherung der Extraktion.
- **Einzige Quelle der Aktiv-Bedingung:** Nach der Extraktion existiert die Heartbeat-Timeout-Logik nur noch in `AufgabeLaufAktivitaet`. Ein repository-weiter Suchlauf nach `LastHeartbeatUtc` / `HeartbeatTimeoutMinutes` bestätigt vor Abschluss, dass keine weitere Inline-Kopie derselben Bedingung übrig bleibt (Converter und Service rufen beide die Methode auf). `AufgabeRecoveryService.ScanForRecoveryCandidatesAsync` verwendet eine verwandte, aber bewusst andere Bedingung (serverseitiger DB-Query mit `IRunningAutomationStatusSource`, kein `AktiveRunId`-Bezug) und bleibt unverändert — sie ist kein Duplikat der Anzeige-Bedingung.
- **`CliUpdateSafetyServiceTests`:** Der bestehende Test `CheckAsync_ShouldTreatOnlyRunningStatusAsRisky` ist an die alte, rein `LaufStatus`-basierte Logik gekoppelt (seine drei Testaufgaben haben alle einen frischen Heartbeat, weil `AktivenLaufSetzenAsync` ihn setzt — unter der neuen Logik wären alle drei riskant). Er muss auf die Heartbeat-Semantik umgestellt werden (siehe Tests).

## Umsetzungsreihenfolge

1. **Hilfsklasse `AufgabeLaufAktivitaet` anlegen**
   - Voraussetzungen: Konstante `AufgabeRecoveryService.HeartbeatTimeoutMinutes` (bereits vorhanden).
   - Beschreibung: Neue Datei `src/Softwareschmiede/Application/Services/AufgabeLaufAktivitaet.cs` mit statischer Methode `bool IstAktiv(string? aktiveRunId, DateTimeOffset? lastHeartbeatUtc, DateTimeOffset nowUtc)`, die die Heartbeat-Timeout-Bedingung exakt wie der bisherige Converter-Inline-Ausdruck abbildet (striktes `<` gegen `TimeSpan.FromMinutes(AufgabeRecoveryService.HeartbeatTimeoutMinutes)`).

2. **`KiAusfuehrungsStatusConverter` auf die Hilfsmethode umstellen**
   - Voraussetzungen: Schritt 1.
   - Beschreibung: Inline-Bedingung (Zeilen 126–128 in `AppConverters.cs`) durch `AufgabeLaufAktivitaet.IstAktiv(...)` ersetzen; Substatus-Zweig unverändert lassen. Bestehende Converter-Tests müssen grün bleiben (Regressionsnachweis der Extraktion).

3. **`CliUpdateSafetyService.CheckAsync()` auf die Hilfsmethode umstellen**
   - Voraussetzungen: Schritt 1.
   - Beschreibung: Filterprädikat auf `AufgabeLaufAktivitaet.IstAktiv(a.AktiveRunId, a.LastHeartbeatUtc, DateTimeOffset.UtcNow)` umstellen; nicht mehr benötigten `using Softwareschmiede.Domain.Enums;` entfernen.

4. **Unit-Tests für `AufgabeLaufAktivitaet` anlegen**
   - Voraussetzungen: Schritt 1.
   - Beschreibung: Neue Testklasse mit Fällen frisch/stale/`null`-Heartbeat/`null`-RunId und Schwellen-Grenzfall (siehe Tests).

5. **`CliUpdateSafetyServiceTests` auf Heartbeat-Semantik umstellen**
   - Voraussetzungen: Schritte 1 und 3; Testhilfe `CreateActiveTaskAsync` vorhanden.
   - Beschreibung: `CreateActiveTaskAsync` um eine Möglichkeit erweitern, `LastHeartbeatUtc` gezielt zu überschreiben (Default: frischer Heartbeat wie bisher). Testfälle auf Heartbeat-Frische umstellen (siehe Tests).

6. **Build + Tests verifizieren**
   - Voraussetzungen: Schritte 1–5.
   - Beschreibung: Vollständigen Build ausführen, anschließend `AufgabeLaufAktivitaetTests`, `CliUpdateSafetyServiceTests` und die `KiAusfuehrungsStatusConverterTests` (Regression) laufen lassen. Projektregeln beachten: `dotnet test` nie im Hintergrund, `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` setzen, keine `Softwareschmiede.App.exe` beenden.

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|---------------------|------------|-------------------------------------|
| `IstAktiv_ShouldReturnTrue_WhenRunIdSetAndHeartbeatFresh` | `AufgabeLaufAktivitaetTests` (neu) | Frischer Heartbeat (`nowUtc` minus wenige Sekunden) + gesetzte `aktiveRunId` → `true`. |
| `IstAktiv_ShouldReturnFalse_WhenHeartbeatOlderThanTimeout` | `AufgabeLaufAktivitaetTests` | Heartbeat älter als `HeartbeatTimeoutMinutes` → `false` (Kernfall gegen Stale-Status). |
| `IstAktiv_ShouldReturnFalse_WhenHeartbeatExactlyAtTimeout` | `AufgabeLaufAktivitaetTests` | Grenzfall: Heartbeat exakt `HeartbeatTimeoutMinutes` alt → `false` (striktes `<`, deckt sich mit Converter). |
| `IstAktiv_ShouldReturnFalse_WhenHeartbeatNull` | `AufgabeLaufAktivitaetTests` | `lastHeartbeatUtc == null` (bei gesetzter `aktiveRunId`) → `false`. |
| `IstAktiv_ShouldReturnFalse_WhenRunIdNull` | `AufgabeLaufAktivitaetTests` | `aktiveRunId == null` (auch bei frischem Heartbeat) → `false`. |
| `CheckAsync_ShouldTreatTaskWithFreshHeartbeatAsRisky` | `CliUpdateSafetyServiceTests` | Aufgabe mit `AktiveRunId != null`, `LaufStatus == Laeuft` und frischem `LastHeartbeatUtc` → riskant (`RequiresConfirmation == true`, in `RiskyTasks` enthalten). |
| `CheckAsync_ShouldNotTreatTaskWithStaleHeartbeatAsRisky` | `CliUpdateSafetyServiceTests` | Aufgabe mit `AktiveRunId != null`, `LaufStatus == Laeuft`, aber `LastHeartbeatUtc` älter als die Timeout-Schwelle → **nicht** riskant (`RequiresConfirmation == false`). Explizite Absicherung des vom Anwender gemeldeten Fehlers. |
| Hilfsmethode: Erweiterung von `CreateActiveTaskAsync` um einen optionalen `LastHeartbeatUtc`-Override | `CliUpdateSafetyServiceTests` | Ermöglicht das Erzeugen von Aufgaben mit gezielt veraltetem Heartbeat, ohne die bestehende Standardnutzung (frischer Heartbeat) zu brechen. |

Hinweis (Teststruktur): Die neuen `CliUpdateSafetyServiceTests`-Fälle werden als kleine, fokussierte Tests angelegt (je ein Verhalten), statt in einen einzelnen Mehrzweck-Test zusammengefasst zu werden.

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `CliUpdateSafetyServiceTests.CheckAsync_ShouldTreatOnlyRunningStatusAsRisky` | An die alte, rein `LaufStatus`-basierte Logik gekoppelt: seine drei Aufgaben haben alle einen frischen Heartbeat (aus `AktivenLaufSetzenAsync`) und wären unter der neuen Heartbeat-Logik alle riskant. Wird durch die neuen, Heartbeat-basierten Testfälle ersetzt (Test entfernen bzw. in `CheckAsync_ShouldTreatTaskWithFreshHeartbeatAsRisky` überführen). |
| `KiAusfuehrungsStatusConverterTests` (alle Fälle) | Keine Anpassung nötig — dienen als Regressionsnachweis, dass die Extraktion das Anzeigeverhalten nicht verändert. Müssen unverändert grün bleiben. |

### E2E-Tests (Pflicht)

| Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium |
|----------|------------------------|-------------------------------|
| — | — | Kein sinnvoller E2E-Test möglich/erforderlich: Die Änderung betrifft ein internes Filterprädikat eines Services und eine verhaltensneutrale Extraktion einer Converter-Bedingung. Der reale Update-Auslöseablauf (Download/Neustart) ist nicht deterministisch in einem E2E-Test reproduzierbar; der Stale-Heartbeat-Zustand (verlorener Prozess) lässt sich in einer UI-E2E-Sitzung nicht zuverlässig herstellen. Die korrigierte Logik (frisch → riskant, stale/`null` → nicht riskant) wird vollständig und deterministisch durch die neuen Unit-Tests in `AufgabeLaufAktivitaetTests` und `CliUpdateSafetyServiceTests` abgedeckt; die unveränderte Panel-Anzeige durch die bestehenden `KiAusfuehrungsStatusConverterTests`. |

Bestehende betroffene E2E-Tests: Keine.

## Offene Punkte

Keine.
