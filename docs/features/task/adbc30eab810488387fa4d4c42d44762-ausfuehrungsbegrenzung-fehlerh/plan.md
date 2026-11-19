# Umsetzungsplan: Ausführungsbegrenzung muss pausierte Ausführungen freigeben (Fehlerkorrektur zu Issue #410)

## Übersicht

Die Belegungszählung der Ausführungsbegrenzung (`ScmEventExecutionLimitService.GetBelegungAsync`) behandelt pausierte Aufgaben mit laufendem CLI-Prozess weiterhin als belegten Platz. Eine Aufgabe mit `PausiertBisUtc` in der Zukunft soll weder in den globalen Zähler (`scm.execution.maxparallel`) noch in den Plugin-Zähler (`plugins.maxparallel.<prefix>`) einfließen, sodass der Platz mit dem Setzen der Pause frei wird und ein wartendes Ereignis über den bereits vorhandenen ereignisgesteuerten Auslöser (`ScmStartPruefungsAusloeser`) bzw. den 60-s-Poll sofort nachrücken kann. Betroffen ist ausschließlich die Limit-Bewertung — der geteilte Handle-Vertrag (`ICliExecutionLimitSource`), Reservierungsmechanik, Wartegrund-Texte und Priorisierung bleiben unverändert.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Ort der Pausen-Filterung | Filterung in `ScmEventExecutionLimitService.GetBelegungAsync` über die bereits vorhandene `Aufgaben`-DB-Projektion (um `PausiertBisUtc` erweitert) | Die Alternative — Erweiterung von `ICliExecutionLimitSource` um eine pausbewusste Abfrage — würde den geteilten Vertrag „laufender Prozess" verändern, der gemeinsam von `CliProcessManager.AbgleichAsync` (Laufdaten-Sicherstellung auch für pausierte Läufe), `AufgabeRecoveryService` (`IsRunning`), Statusanzeige und indirekt `AutoShutdownOrchestrator`/`ScmStartPruefungsService` genutzt wird. Der DB-Filter lässt die Handle-Semantik unangetastet und kostet keinen zusätzlichen Datenbankzugriff, da die Aufgaben ohnehin geladen werden. `GetBelegungAsync` ist der einzige Durchlasspunkt für `PruefeUndReserviereStartPlatzAsync` und `GetStartBlockReasonAsync` (beide über `WirksameBelegungAsync`) — eine Filterung dort wirkt konsistent auf Global- und ProPlugin-Zähler. |
| Pausenkriterium | `PausiertBisUtc > time.GetUtcNow()` (injizierter `TimeProvider`) | Identisch zur Referenzdefinition in `KiAusfuehrungsStatusKlassifikation` (`KiAusfuehrungsZustand.Pausiert`), `ScmEventOrchestrator.BlockReason` („Aufgabe pausiert; Start gesperrt."), `AufgabeRecoveryService` und `PromptZeitVersandService`. Abgelaufene, aber noch gesetzte Werte zählen wieder als belegt — konsistent zur Fortsetzungs-Semantik der `ScmDispatchPrioritaet` (gesetzt markiert Fortsetzung, keine Pause). |
| Auslöser-Anbindung `CancelAsync` | Optionaler Konstruktorparameter `ScmStartPruefungsAusloeser?` am `ScmEventOrchestrator`; Aufruf von `StartpruefungAnfordern()` nach der Pausen-Rückstufung | `CancelAsync` ist die einzige produktive Schreibstelle von `PausiertBisUtc` ohne Auslösermeldung (per `ExecuteUpdateAsync`, Z. 442–443). Sie kann einen Platz freigeben, wenn eine abgelaufene Direktiven-Pause auf ein noch zukünftiges Ziel zurückgestuft wird. Das optionale-Parameter-Muster entspricht `AufgabeService`, `ScmEventStore` und `KiPluginLimitService`; der Auslöser ist als Singleton registriert und wird per DI aufgelöst. |
| Reservierungs-Interaktion | Keine Änderung: Offene Reservierungen (`StartPlatzReservierung`) bleiben vom Pausenfilter unberührt | Eine kurz vor der Pause erteilte Reservierung belegt den Platz regulär bis zu ihrem Verfall (`ReservierungsLebensdauer`, 5 min) oder ihrer Freigabe. Das verhindert, dass ein gerade im Startfenster befindlicher Dispatch seinen Platz verliert. |

## Programmabläufe

### Belegungszählung mit Pausenfilter

1. `PruefeUndReserviereStartPlatzAsync` bzw. `GetStartBlockReasonAsync` nimmt `ZulassungsGate` und ruft `WirksameBelegungAsync` (unverändert).
2. `WirksameBelegungAsync` ruft `GetBelegungAsync` und addiert offene Reservierungen (unverändert).
3. `GetBelegungAsync` lädt `cli.GetLaufendeAufgabenIds()`; bei leerer Menge frühzeitige Rückgabe (unverändert).
4. Die `Aufgaben`-Projektion wird um `PausiertBisUtc` erweitert (`Id`, `KiPluginPrefix`, `PausiertBisUtc`).
5. `jetzt = time.GetUtcNow()` wird einmal bestimmt. Projizierte Aufgaben mit `PausiertBisUtc > jetzt` gelten als pausiert und werden aus der Zählung entfernt:
   - `Global` = Anzahl der laufenden IDs abzüglich der pausierten Aufgaben. Laufende Handles ohne auflösbare `Aufgaben`-Zeile (keine Projektionszeile vorhanden) haben kein `PausiertBisUtc` und zählen weiterhin als belegt — die bisherige Zählsemantik bleibt erhalten.
   - `ProPlugin`: pausierte Aufgaben werden vor der Plugin-Attribution übersprungen — sie entfallen aus beiden Zählern, auch ohne auflösbares Prefix kein Restbeleg.
   - Ablaufene Pausen (`PausiertBisUtc <= jetzt`) und `null` zählen wie bisher voll.
6. `GetStartBlockReason` (reine Auswertung), Wartegrund-Texte und Reservierungsmechanik bleiben unverändert.

Beteiligte Klassen/Komponenten: `ScmEventExecutionLimitService`, `CliAusfuehrungsBelegung`, `ICliExecutionLimitSource`, `Aufgabe`, `TimeProvider`

### Platzfreigabe bei Pause → ereignisgesteuerter Folgestart

1. Ein Pausen-Setzpfad persistiert `PausiertBisUtc` in der Zukunft und meldet anschließend `ScmStartPruefungsAusloeser.StartpruefungAnfordern()` — alle produktiven Pfade bereits vorhanden: `AufgabeService.SetPauseAsync` (manuell/UI), `ScmEventStore.AcceptBatchAsync`/`PauseForDelayAsync` (`#verzögert`-Direktive), `KiPluginLimitService.VerarbeiteRateLimitAsync` (Session-Limit).
2. `ScmStartPruefungsService` koalesziert die Auslöser (250 ms Sammelzeit) und ruft `ScmEventOrchestrator.DispatchDueEventsAsync` — denselben priorisierten Batch wie der 60-s-Poll des `ScmEventMonitoringService`.
3. In `DispatchCoreAsync`/`DispatchAufbereitungAsync` prüft `PruefeUndReserviereStartPlatzAsync` erneut; die pausbewusste `GetBelegungAsync` sieht den frei gewordenen Platz unter der `ZulassungsGate`-Sperre konsistent (die Pause wurde vor dem Auslöser-Aufruf committet).
4. Das nächste startbare Ereignis erhält eine `StartPlatzReservierung` und wird zugestellt — ohne auf den Poll zu warten.

Beteiligte Klassen/Komponenten: `AufgabeService`, `ScmEventStore`, `KiPluginLimitService`, `ScmStartPruefungsAusloeser`, `ScmStartPruefungsService`, `ScmEventOrchestrator`, `ScmEventExecutionLimitService`

### Pausen-Rückstufung bei Abbruch eines verzögerten Ereignisses (`CancelAsync`)

1. `CancelAsync` bricht ein Ereignis im Status „Verzögert" ab und stuft die Direktiven-Pause der Aufgabe per `ExecuteUpdateAsync` auf das nächste verbleibende Direktivenziel zurück bzw. auf `null` (Z. 435–444, unveränderte Fachlogik).
2. Neu: Nach erfolgreicher Änderung ruft der `ScmEventOrchestrator` `startPruefungsAusloeser?.StartpruefungAnfordern()` auf, damit ein durch die zurückgestufte Pause frei werdender Platz nicht erst beim nächsten Poll gesehen wird (Randfall: abgelaufene Pause → Rückstufung auf zukünftiges Ziel).
3. `notifier.Notify()` und der übrige Ablauf bleiben unverändert.

Beteiligte Klassen/Komponenten: `ScmEventOrchestrator`, `ScmStartPruefungsAusloeser`, `ScmEventChangeNotifier`

### Pausenende bei weiterlaufendem Prozess

1. Läuft der CLI-Prozess einer Aufgabe nach Ablauf von `PausiertBisUtc` weiter, zählt sie ab dem Ablaufzeitpunkt wieder als belegt (`GetBelegungAsync` filtert nur `> jetzt`).
2. Die Fortsetzung der pausierten Aufgabe über eine noch lebende SCM-Sitzung (`adapter.IsAlive` in `DispatchCoreAsync`, Z. 217) durchläuft keine Zulassungsprüfung — die Platzfreigabe entzieht der pausierten Aufgabe weder Fortsetzungsfähigkeit noch Sichtbarkeit für `IsRunning`, `CliLaufAktiv`, `CliProcessManager.AbgleichAsync` und Recovery.
3. Die Zahl tatsächlich laufender Prozesse kann das konfigurierte Limit dadurch zeitweise überschreiten — das ist die fachlich geforderte Semantik (das Limit begrenzt Starts, nicht laufende Prozesse) und gilt als verbindliche Annahme.

Beteiligte Klassen/Komponenten: `ScmEventExecutionLimitService`, `ScmEventOrchestrator`, `IScmPromptSessionAdapter`, `KiAusfuehrungsService`

## Neue Klassen

Keine.

## Änderungen an bestehenden Klassen

### `ScmEventExecutionLimitService` (Klasse)

- **Geänderte Methoden:** `GetBelegungAsync` — Projektion `new { x.Id, x.KiPluginPrefix }` um `x.PausiertBisUtc` erweitern; `jetzt = time.GetUtcNow()` einmal bestimmen; Aufgaben mit `PausiertBisUtc > jetzt` vor der `Global`-Zählung und der Plugin-Attribution herausfiltern. `Global` zählt danach nur noch nicht-pausierte laufende Aufgaben (inkl. laufender Handles ohne Aufgabenzeile); `ProPlugin` enthält keine Einträge pausierter Aufgaben mehr.
- **Unverändert:** `PruefeUndReserviereStartPlatzAsync`, `GetStartBlockReasonAsync`, `GetStartBlockReason`, `GetLimitsAsync`, `WirksameBelegungAsync`, `ParseLimit`, `StartPlatzReservierung`, alle Konstanten und Sperren.

### `ScmEventOrchestrator` (Klasse)

- **Neue Abhängigkeit:** Optionaler Konstruktorparameter `ScmStartPruefungsAusloeser?` (letzter Parameter, Muster wie `AufgabeService`, `ScmEventStore`, `KiPluginLimitService`; DI-Auflösung über den bereits registrierten Singleton in `App.xaml.cs` Z. 313 und `ScmDispatchFixture` Z. 170).
- **Geänderte Methoden:** `CancelAsync` — nach dem Pausen-Rückstufungsblock (`ExecuteUpdateAsync` auf `Aufgaben`, Z. 442–443) `startPruefungsAusloeser?.StartpruefungAnfordern()` aufrufen, sobald ein Ereignis tatsächlich abgebrochen wurde (`changed != 0`).

## Datenbankmigrationen

Keine.

## Validierungsregeln

Keine.

## Konfigurationsänderungen

Keine. Das Verhalten wirkt unter den bestehenden Einstellungen `scm.execution.maxparallel` (global) und `plugins.maxparallel.<prefix>` (je KI-Plugin) sowie dem Laufzeitschalter „Aufgaben ausführen" (`GetScmMonitoringEnabledAsync`). Leer/ungesetzt bleibt unbegrenzt.

## Seiteneffekte und Risiken

- **Zeitweilige Limit-Überschreitung nach Fortsetzung:** Endet die Pause bei weiterlaufendem Prozess und wird die Sitzung fortgesetzt, kann die Zahl laufender Prozesse das konfigurierte Limit überschreiten (die Fortsetzung benötigt keinen neuen Startplatz). Fachlich gefordert und dokumentiert.
- **Folgestart-Konkurrenz:** Durch die Platzfreigabe können neue Ereignisse starten, während die pausierte Aufgabe weiterläuft; kehrt die pausierte Aufgabe über die lebende Sitzung zurück, laufen beide parallel — gewollt, da das Limit nur Starts begrenzt.
- **Reservierungs-Race:** Eine unmittelbar vor dem Pausen-Setzen erteilte Reservierung belegt den Platz weiter, bis sie freigegeben wird oder nach `ReservierungsLebensdauer` verfällt — unverändertes, korrektes Verhalten der atomaren Zulassung.
- **Handles ohne Aufgabenzeile:** Laufende CLI-Prozesse, deren `Aufgabe` nicht in der Projektion auflöst (z. B. gelöschte Aufgabe), besitzen kein `PausiertBisUtc` und zählen weiterhin voll — bewusst konservativ.
- **`AufgabeStatus.Wartend` ohne `PausiertBisUtc`:** Aufgaben im Status „Wartend" ohne gesetzte Pausenzeit zählen weiterhin als belegt; das Pausenkriterium bleibt ausschließlich `PausiertBisUtc > UtcNow`.
- **`CancelAsync` feuert den Auslöser zusätzlich:** Unschädlich — der `ScmStartPruefungsService` koalesziert Auslöser und `DispatchDueEventsAsync` ist idempotent; ohne registrierte Überwachung oder bei deaktiviertem Schalter wirkungslos.
- **Bestehende Features unverändert:** `CliProcessManager.AbgleichAsync` (Laufdaten-Sicherstellung), `LaufendeAutomatisierungenZaehlung` (Auto-Shutdown zählt pausierte bereits nicht), Statusanzeige, Recovery und Prompt-Versand sehen pausierte laufende Prozesse weiterhin — die Filterung greift nur in der Limit-Belegung.

## Umsetzungsreihenfolge

1. **Pausenfilter in `GetBelegungAsync` einbauen**
   - Voraussetzungen: Keine — `TimeProvider` ist bereits injiziert, `Aufgabe.PausiertBisUtc` existiert, die Projektion lädt die Aufgaben bereits.
   - Beschreibung: Projektion um `PausiertBisUtc` erweitern; einmalige Bestimmung von `jetzt` über `time.GetUtcNow()`; pausierte Aufgaben (`PausiertBisUtc > jetzt`) aus `Global` und vor der Plugin-Attribution aus `ProPlugin` herausfiltern; XML-Dokumentation der Methode und ggf. des Records um die Pausen-Semantik ergänzen.

2. **`ScmStartPruefungsAusloeser` an `ScmEventOrchestrator.CancelAsync` anbinden**
   - Voraussetzungen: `ScmStartPruefungsAusloeser` ist als Singleton registriert (`App.xaml.cs`, `ScmDispatchFixture`) — im Repo vorhanden.
   - Beschreibung: Optionalen Konstruktorparameter `ScmStartPruefungsAusloeser?` ergänzen; in `CancelAsync` nach der Pausen-Rückstufung `StartpruefungAnfordern()` aufrufen, wenn `changed != 0`.

3. **Unit-Tests der Belegungszählung (`ScmEventExecutionLimitServiceTests`)**
   - Voraussetzungen: Schritt 1; vorhandene Testinfrastruktur (`TestDbContextFactory`, `Mock<ICliExecutionLimitSource>`, lokale Helfer).
   - Beschreibung: `LaufendeAufgabeAsync` um einen optionalen `pausiertBisUtc`-Parameter erweitern; neue Fakten für Pausenfälle (global/pro Plugin frei, ohne Prefix aus beiden Zählern, abgelaufene Pause belegt, gemischte Belegung, Reservierungs-Interaktion).

4. **Integrations-Tests der Platzfreigabe (`ScmEventDispatchTests_Limits`)**
   - Voraussetzungen: Schritt 1; `ScmDispatchFixture` (liefert `AufgabeService` mit injiziertem Auslöser, `SetSettingAsync`, `Sessions`-Beobachtung) — vorhanden.
   - Beschreibung: `Dispatch_TaskPaused_SlotFreed_WaitingEventStarts` (globales Limit) und Plugin-Variante; `Dispatch_TaskPauseExpired_SlotStillOccupied` (abgelaufene Pause belegt weiterhin).

5. **Integrations-Test des Triggers (`ScmEventDispatchTests_Startpruefung`)**
   - Voraussetzungen: Schritte 1 und 2; laufender `ScmStartPruefungsService` in der Fixture — vorhanden.
   - Beschreibung: `StartPruefung_PauseGesetzt_WartendesEreignisStartetOhnePoll` — `SetPauseAsync` auf die laufende Aufgabe löst den Folgestart des wartenden Ereignisses ohne Poll aus.

6. **Vollbuild und relevante Test-Suite ausführen**
   - Voraussetzungen: Schritte 1–5.
   - Beschreibung: `dotnet build` der Lösung, danach `dotnet test` auf `Softwareschmiede.Tests` und `Softwareschmiede.IntegrationTests` (bereichsbezogen, gefiltert auf die SCM-Dispatch-/Limit-Tests plus voller Lauf der berührten Suiten); Formatierungsprüfung `dotnet format Softwareschmiede.slnx --verify-no-changes`.

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `LaufendeAufgabeAsync` (Erweiterung) | `ScmEventExecutionLimitServiceTests` | Optionaler Parameter `pausiertBisUtc` setzt `Aufgabe.PausiertBisUtc` beim Anlegen; bestehende Aufrufe bleiben unverändert |
| `GetStartBlockReasonAsync_PausierteAufgabe_GibtGlobalenPlatzFrei` | `ScmEventExecutionLimitServiceTests` | Globales Limit 1, eine laufende Aufgabe mit zukünftigem `PausiertBisUtc` → Start frei (`null`) |
| `GetStartBlockReasonAsync_PausierteAufgabe_GibtPluginPlatzFrei` | `ScmEventExecutionLimitServiceTests` | Plugin-Limit 1, pausierte Aufgabe mit gleichem Prefix → Start für dieses Plugin frei |
| `GetStartBlockReasonAsync_PausierteAufgabeOhnePrefix_EntfaelltAusBeidenZaehlern` | `ScmEventExecutionLimitServiceTests` | Pausierte Aufgabe ohne Plugin-Attribution gibt auch den globalen Platz frei (kein Restbeleg) |
| `GetStartBlockReasonAsync_AbgelaufenePause_ZaehltAlsBelegt` | `ScmEventExecutionLimitServiceTests` | `PausiertBisUtc` in der Vergangenheit → Aufgabe belegt weiterhin global und pro Plugin |
| `GetStartBlockReasonAsync_GemischtPausiertUndAktiv_ZaehltNurAktive` | `ScmEventExecutionLimitServiceTests` | Eine pausierte plus eine aktive laufende Aufgabe bei Limit 2 → belegt; bei Limit 1 nur die aktive zählt |
| `PruefeUndReserviereStartPlatzAsync_PausierterLaufUndOffeneReservierung_ReservierungBelegtPlatz` | `ScmEventExecutionLimitServiceTests` | Pausierter Lauf gibt Handle-Platz frei, eine offene `StartPlatzReservierung` blockiert weiterhin bis Dispose |
| `Dispatch_TaskPaused_SlotFreed_WaitingEventStarts` | `ScmEventDispatchTests_Limits` | Globales Limit 1: erstes Ereignis zugestellt, zweites wartet mit Limit-Grund; nach `SetPauseAsync` (Zukunft) auf die laufende Aufgabe startet das wartende Ereignis bei erneutem Dispatch |
| `Dispatch_TaskPaused_PluginSlotFreed_WaitingEventStarts` | `ScmEventDispatchTests_Limits` | Plugin-Limit-Variante: Pause der laufenden Aufgabe gibt den Plugin-Platz frei |
| `Dispatch_TaskPauseExpired_SlotStillOccupied` | `ScmEventDispatchTests_Limits` | Aufgabe mit abgelaufenem `PausiertBisUtc` und laufendem Prozess belegt den Platz weiterhin — zweites Ereignis wartet |
| `StartPruefung_PauseGesetzt_WartendesEreignisStartetOhnePoll` | `ScmEventDispatchTests_Startpruefung` | Laufender `ScmStartPruefungsService`: `SetPauseAsync` auf die laufende Aufgabe löst den sofortigen Folgestart des wartenden Ereignisses aus (ohne Poll, via `WaitForAsync`-Muster) |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `LaufendeAufgabeAsync`-Aufrufe in `ScmEventExecutionLimitServiceTests` | Optionale Parametererweiterung — Quellkompatibilität bleibt bestehen, keine Aufrufanpassung nötig |

Keine weiteren bestehenden Tests betroffen: Signaturen, Verhalten und Datenstrukturen aller übrigen Komponenten bleiben unverändert; die neue Filterung wirkt nur innerhalb der bisher ungetesteten Belegungszählung.

### E2E-Tests (Pflicht)

Es gibt keine neue oder geänderte Benutzerinteraktion (kein UI-Change, keine neue Einstellung) — der Fehlerfall ist ein Backend-Timing-Problem der Zulassungskontrolle. Die End-to-End-Abdeckung erfolgt über die Integrations-Tests der `ScmDispatchFixture` (echte SQLite, echte Services, echter CLI-Kindprozess), die den vollständigen Ablauf „Ereignis → Dispatch → Limit → Pause → Folgestart" abbilden:

| Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium |
|----------|------------------------|-------------------------------|
| Pausierte Ausführung gibt globalen Platz frei; wartendes Ereignis startet | `ScmEventDispatchTests_Limits` / `Dispatch_TaskPaused_SlotFreed_WaitingEventStarts` | Platz gilt ab Pausen-Setzen als frei (globaler Zähler) |
| Pausierte Ausführung gibt Plugin-Platz frei | `ScmEventDispatchTests_Limits` / `Dispatch_TaskPaused_PluginSlotFreed_WaitingEventStarts` | Konsequente Filterung im Plugin-Zähler |
| Ablaufene Pause belegt weiter | `ScmEventDispatchTests_Limits` / `Dispatch_TaskPauseExpired_SlotStillOccupied` | `PausiertBisUtc <= UtcNow` zählt als belegt |
| Pausen-Setzen führt zum sofortigen Folgestart | `ScmEventDispatchTests_Startpruefung` / `StartPruefung_PauseGesetzt_WartendesEreignisStartetOhnePoll` | Ereignisgesteuerte Zustellung ohne Poll-Durchlauf |

Angepasste bestehende E2E-/Integrations-Tests: Keine.

## Offene Punkte

Keine.

Die drei in der Anforderung benannten offenen Fragen sind geklärt und im Plan eingearbeitet:

- **Pausenpfad des Kundenfalls:** Die Bestandsaufnahme hat verifiziert, dass alle produktiven Pausen-Setzpfade (`AufgabeService.SetPauseAsync`, `ScmEventStore.PauseForDelayAsync`, `KiPluginLimitService.VerarbeiteRateLimitAsync`) bereits `ScmStartPruefungsAusloeser.StartpruefungAnfordern()` melden. Die einzige Schreibstelle ohne Auslöser (`ScmEventOrchestrator.CancelAsync`) wird über Schritt 2 angebunden.
- **Pausendefinition:** `PausiertBisUtc > UtcNow` gilt als verbindlich (konsistent zu `KiAusfuehrungsZustand.Pausiert` und dem `BlockReason`-Text). `AufgabeStatus.Wartend` ohne gesetzte Pausenzeit zählt weiterhin als belegt.
- **Überschreitung nach Fortsetzung:** Als akzeptabel bestätigt — das Limit begrenzt Starts, nicht laufende Prozesse; die Fortsetzung über eine lebende Sitzung durchläuft nachweislich keine Zulassungsprüfung (`DispatchCoreAsync` Z. 217).
