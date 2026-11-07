# Umsetzungsplan: Persistierter zeitgesteuerter Prompt (Issue #265)

## Übersicht

Der `PromptZeitVersandService` hält geplante zeitgesteuerte Prompts heute nur in einer In-Memory-Warteschlange; sie gehen beim Verlassen der Aufgabendetailansicht, bei CLI-Stopp und bei App-Neustart verloren. Die Umsetzung ergänzt eine EF-Core-/SQLite-Persistenzschicht (neue Entity `ZeitgesteuerterPrompt` + Migration), einen Re-Arm-Mechanismus beim App-Start, eine eventbasierte Nachzustellung beim nächsten CLI-Session-Start (inkl. Retry bei Sendefehlern), einen manuellen Storno-Button in der Detailansicht sowie `SystemMeldung`-Protokolleinträge bei Hintergrundversand/Nachzustellung. Betroffen sind `PromptZeitVersandService`, `TaskDetailViewModel`, `TaskDetailView` (XAML), `EntwicklungsprozessService`, `ProtokollService` (Wiederverwendung), `SoftwareschmiededDbContext` und `App.xaml.cs`.

## Scope-Klärung

| Feld | Festlegung |
|------|------------|
| Verarbeitungseinheit | Ein *geplanter zeitgesteuerter Prompt*: gebündelter Datensatz aus `AufgabeId`, bereits platzhalteraufgelöstem `PromptText` (Auflösung bleibt in `TaskDetailViewModel.SchedulePromptAsync` via `PromptVorlagenPlatzhalterService.Resolve`) und UTC-Zielzeitpunkt. Pro Aufgabe maximal ein Plan (Ersetzungsregel bleibt bestehen, wird per Unique-Index auf `AufgabeId` durchgesetzt). |
| Auslösende Ereignisse | (1) Ribbon-Button `ZeitgesteuertSenden` in `TaskDetailView` → `SchedulePromptAsync`; (2) Timer-Fälligkeit `HandleTimerElapsedAsync`; (3) App-Start → `RestoreScheduledPromptsAsync` (Re-Arm, neu); (4) CLI-Session-Start `CliProcessStatusChanged(Gestartet)` → Nachzustellung wartender/fälliger Pläne (neu); (5) Aufgabenabschluss `EntwicklungsprozessService.AbschliessenCoordinatedAsync` → Storno inkl. Datensatzlöschung; (6) Pause (`PausiertBisUtc`) → Verschiebung des persistierten Zielzeitpunkts; (7) Storno-Button `WartestellungAufheben` in der `TaskDetailView` → `CancelScheduledPromptAsync` (manueller Storno, neu). |
| Erwartete Wirkung | Der Prompttext wird zum Zielzeitpunkt per `ITerminalSession.WritePromptAsync` an die laufende CLI-Session geschrieben — auch wenn die Aufgabendetailansicht nicht geöffnet ist (Sessions in `KiAusfuehrungsService._handles` überdauern das Verlassen der Ansicht bereits heute). `PromptSent`-Event, `ScheduledPromptStatus`/`ScheduledPromptTimeDisplay` in der Detailansicht und `HasScheduledPrompt` (`⏳`-Indikator) in der aktiven Aufgabenliste spiegeln den persistierten Zustand — auch über Detailansicht-Wechsel und App-Neustarts hinweg. Bei Versand ohne geöffnete Detailansicht sowie bei Nachzustellung nach Session-Start wird zusätzlich ein `Protokolleintrag` (`ProtokollTyp.SystemMeldung`) auf der Aufgabe geschrieben. |
| Umgang mit fehlenden Objekten | *Keine aktive `ITerminalSession` zum Fälligkeitszeitpunkt:* Datensatz bleibt persistiert; In-Memory-Eintrag geht in den Zustand „wartet auf Session" über; Zustellung erfolgt beim nächsten `CliProcessStatusChanged(Gestartet)` für die Aufgabe (kein automatischer CLI-Start). *Fehlschlag von `WritePromptAsync` (sonstiger Sendefehler ungleich `ObjectDisposedException`, bei dem die Session ebenfalls als nicht mehr zustellfähig gilt):* gleiche Behandlung — Datensatz + Plan bleiben erhalten, Eintrag geht in „wartet auf Session" über, erneuter Zustellversuch beim nächsten `CliProcessStatusChanged(Gestartet)`. *Überfälliger Plan beim App-Start:* bleibt persistiert; da nach App-Start grundsätzlich keine Session existiert, wird er ebenfalls „wartet auf Session" und bei nächstem CLI-Start der Aufgabe zugestellt — kein Sofortversand beim Start ohne Session, aber auch kein Verwerfen. *Aufgabe abgeschlossen:* `CancelScheduledPromptAsync` löscht Datensatz + In-Memory-Eintrag (Semantik unverändert: Abschluss storniert). *Aufgabe beim Schreiben des Protokolleintrags nicht mehr vorhanden:* Eintrag wird defensiv übersprungen (Warn-Log) — der erfolgreiche Versand bleibt gültig, der Protokolleintrag darf den Versandpfad nicht brechen. *Aufgabe physisch gelöscht:* `DeleteBehavior.Cascade` entfernt den Datensatz. *Verwaister Datensatz (Aufgabe nicht mehr vorhanden) beim Re-Arm:* Datensatz wird beim Laden verworfen/gelöscht (defensiv; dürfte durch FK-Cascade nicht vorkommen). *Dauerhaft wartender Plan (Aufgabe ohne erneuten CLI-Start):* kein TTL/Verfall — der Datensatz bleibt unbegrenzt bestehen und ist über `⏳`/Statusanzeige sichtbar; auflösbar über den Storno-Button, Ersetzen, Abschluss oder Löschung (Festlegung 2026-11-07). |

### Scope-Ausschlüsse

| # | Einschränkung / Ausschluss | Begründung | Status |
|---|----------------------------|------------|--------|
| 1 | Versand bei nicht laufender App (komplett beendetem Prozess) ist nicht möglich — es gibt keinen externen Scheduler. „Im Hintergrund" bedeutet: App läuft, Detailansicht geschlossen oder Fenster minimiert. | CLI-Sessions sind ConPTY-Kindprozesse der App; ohne laufende App existiert weder Session noch Zustellweg. Ein externer Scheduler-Prozess wäre ein separates Großvorhaben. | bestätigt (2026-11-07) |
| 2 | Kein automatischer CLI-(Neu-)Start, um einen fälligen Plan zuzustellen. Ein fälliger Plan „wartet auf Session"; die Zustellung erfolgt beim nächsten `CliProcessStatusChanged(Gestartet)` — also nur an eine bereits laufende oder vom Anwender (neu) gestartete Session. | Ein impliziter Prozessstart würde unerwartet Klon/Arbeitsverzeichnis-Seiteneffekte auslösen und den Session-Lebenszyklus (u. a. `CliProcessManager`, Heartbeats) verkomplizieren. | bestätigt (2026-11-07) |
| 3 | Keine Datumsauswahl — die Zielzeit bleibt HH:mm des heutigen Tages (bestehende UI-Granularität unverändert). | Scope-Begrenzung; die persistierte `ZielZeitpunktUtc` trägt ohnedies ein Datum (Pausenverschiebung kann bereits über Mitternacht liegen). Der ursprünglich ebenfalls ausgeschlossene manuelle Storno wurde auf Anwenderentscheid (2026-11-07) in den Scope aufgenommen (siehe Ablauf „Manueller Storno"). | bestätigt (2026-11-07) |

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Persistenzmodell | Neue eigene Entity `ZeitgesteuerterPrompt` mit eigener Tabelle und Unique-Index auf `AufgabeId` (Domain Entity / Data Mapper via EF Core). Die flachen `Aufgabe`-Spalten `VorschlagPrompt`/`VorschlagAusfuehrenAbUtc` werden **nicht** wiederverwendet. | Die Vorschlags-Felder dienen einem anderen fachlichen Zweck (gespeicherter Prompt-Vorschlag). Eine eigene Tabelle bildet die Warteschlangen-Semantik sauber ab, erzwingt die Ersetzungsregel datenbankseitig und folgt dem etablierten Muster warteschlangenartiger Entities (`ScmOutgoingAction`, `Todo`). |
| DB-Zugriff aus dem Singleton | Scoped Service `ZeitgesteuerterPromptStore` (Service Layer / Repository-artig), aufgelöst über die bereits vorhandene `IServiceScopeFactory` im `PromptZeitVersandService`. | Entspricht exakt dem bestehenden Erweiterungspunkt (`GetPausiertBisUtcAsync` löst `AufgabeService` per Scope). Kapselt EF-Details aus dem Singleton heraus und bleibt mit InMemory-`TestDbContextFactory` testbar. Ohne `scopeFactory` (bestehende Unit-Test-Konstruktion) entfällt die Persistenz wie bisher die Pausenprüfung. |
| Re-Arm beim App-Start | Expliziter Aufruf `RestoreScheduledPromptsAsync()` auf dem `PromptZeitVersandService`-Singleton in `App.OnStartup`, **nach** `db.Database.MigrateAsync()` — analog `EnsureInitialPromptVorlagenAsync`/`CliProcessManager`-Auflösung. **Kein** `IHostedService`. | Hosted Services starten bereits innerhalb von `_host.StartAsync()` (`App.xaml.cs` Zeile 80), also **vor** `MigrateAsync()` (Zeile 96). Bei einem Update, das die neue Tabelle erzeugt, würde `StartAsync` auf eine noch nicht existierende Tabelle zugreifen. Der explizite Aufruf nach der Migration garantiert die Reihenfolge und folgt dem vorhandenen Start-Muster. |
| Semantik bei CLI-Stopp | Der `CancelScheduledPrompt`-Aufruf in `TaskDetailViewModel.OnCliProcessStatusChanged` entfällt: Ein persistierter Plan überlebt einen CLI-Stopp/-Abbruch und wird bei der nächsten Session zugestellt. | Andernfalls würde jeder vorzeitige CLI-Abbruch (Fehler, manueller Stopp) den Plan verlieren — das widerspräche dem Kernziel „kein geplanter Prompt geht verloren". Ein CLI-Neustart ist ein regulärer, vom Anwender steuerbarer Vorgang; der Plan bleibt fachlich gültig, bis er versendet oder durch Abschluss/Ersetzen aufgehoben wird. |
| Nachzustellung ohne Session | Eventbasiert: `PromptZeitVersandService` abonniert `KiAusfuehrungsService.CliProcessStatusChanged` und prüft bei `Gestartet`, ob ein wartender/fälliger Plan für die Aufgabe existiert. Kein Polling, kein Retry-Timer. | Das Ereignis existiert bereits und markiert exakt den Übergang „Session verfügbar". Ein zusätzlicher Timer/Poller wäre redundante Komplexität. |
| Status-Synchronisation UI | Neues Event `ScheduledPromptChanged` (`Action<Guid>`) auf `PromptZeitVersandService`, gefeuert bei Planen/Storno/Versand/„wartet auf Session". `TaskDetailViewModel` abonniert es und befüllt `ScheduledPromptStatus`/`ScheduledPromptTimeDisplay` aus `GetScheduledPromptStatus`. `MainWindowViewModel` bleibt unverändert (Refresh über `_aktualisierungsTimer`). | Nach dem Entfernen der impliziten Stornos fehlt dem ViewModel ein Signal, wenn der Plan extern aufgehoben wird (Aufgabenabschluss) oder der Zustand wechselt. Das Event ersetzt die bisherige implizite Kopplung. Die Panel-Liste wird bereits periodisch über den Dispatcher-Timer aktualisiert und liest `HasScheduledPrompt` weiterhin synchron aus `GetScheduledPromptStatus`. |
| Manueller Storno | Button „Wartestellung aufheben" (`AutomationProperties.Name="WartestellungAufheben"`) innerhalb des bereits sichtbarkeitsgesteuerten Status-`StackPanel` in `TaskDetailView` (`Visibility` via `NullOrEmptyToVisibilityConverter` auf `ScheduledPromptStatus`, `TaskDetailView.xaml` Zeilen ~154–163) → `AsyncRelayCommand` `CancelScheduledPromptCommand` → `CancelScheduledPromptAsync`. Statusanzeige wird über `ScheduledPromptChanged` geleert. | Anwenderentscheid 2026-11-07: Storno kommt in den Scope. Platzierung in der Statusanzeige ergibt die korrekte Sichtbarkeit ohne eigenen Konverter; `AsyncRelayCommand` + `AutomationName` folgen den bestehenden Mustern (`SchedulePromptCommand`, `ZeitgesteuertSenden`/`ScheduledPromptStatus`). |
| Protokolleintrag bei Hintergrundversand | `PromptZeitVersandService` schreibt nach erfolgreichem `WritePromptAsync` einen `Protokolleintrag` (`ProtokollTyp.SystemMeldung`) über den per `IServiceScopeFactory` aufgelösten `ProtokollService.AddEintragAsync` — immer bei Nachzustellung über `CliProcessStatusChanged(Gestartet)`, und beim Timer-/Sofortversand dann, wenn `PromptSent` keinen Abonnenten hat (kein ViewModel = keine geöffnete Detailansicht). Fehler (z. B. Aufgabe nicht mehr vorhanden) werden geloggt und brechen den Versand nicht. | Anwenderentscheid 2026-11-07: Nachvollziehbarkeit von Hintergrundversand/Nachzustellung im Aufgabenprotokoll. `ProtokollService` ist bereits scoped registriert (`App.xaml.cs` Zeile 201) und `AddEintragAsync(SystemMeldung)` ist das etablierte Muster (`AufgabeService.SetPauseAsync` Zeile 858, `ScmTaskCompletionMonitoringService`). `PromptSent`-Abonnentenzahl ist der vorhandene, einfachste Indikator für eine geöffnete Detailansicht — `TaskDetailViewModel` ist der einzige produktive Abonnent. |
| Retry-Semantik bei Zustellfehlern | Jeder nicht erfolgreiche Zustellversand — keine Session, `ObjectDisposedException` oder sonstiger `WritePromptAsync`-Fehler — überführt den Eintrag in „wartet auf Session"; Datensatz bleibt, erneuter Versuch beim nächsten `CliProcessStatusChanged(Gestartet)`. Es gibt keinen Retry-Timer und keine Fehlerzähler. | Anwenderentscheid 2026-11-07: vereinheitlicht alle Fehlschlag-Fälle auf den bereits geplanten „wartet auf Session"-Mechanismus — ein einziger Wiederaufnahmeweg statt mehrerer Sonderfälle. Ein transiente Sendefehler darf den persistierten Plan genauso wenig verlieren wie eine fehlende Session. |
| Verfall wartender Pläne | Kein TTL/Verfall: ein persistierter Plan bleibt unbegrenzt bestehen, bis er versendet, manuell storniert, ersetzt oder mit der Aufgabe abgeschlossen/gelöscht wird. | Anwenderentscheid 2026-11-07: der Zustand ist über `⏳`/Statusanzeige sichtbar und über ausreichend viele Wege auflösbar; ein automatischer Verfall würde stillschweigend Daten verlieren. |

## Programmabläufe

### Planen eines zeitgesteuerten Prompts (erweitert)

1. Anwender wählt Promptvorlage + HH:mm und klickt `ZeitgesteuertSenden` → `TaskDetailViewModel.SchedulePromptAsync` validiert Stunde/Minute, baut `targetTime` aus heutigem Datum, löst Platzhalter via `PromptVorlagenPlatzhalterService.Resolve` (unverändert).
2. `PromptZeitVersandService.SchedulePromptAsync` lädt `PausiertBisUtc` und verschiebt die Zielzeit ggf. auf das Pausenende (unverändert).
3. Zielzeit ≤ jetzt → `RemoveEntry` + `SendPromptAsync` sofort; **kein** persistierter Datensatz (nichts zu wiederherstellen).
4. Zielzeit in Zukunft → `ZeitgesteuerterPromptStore.UpsertAsync(aufgabeId, promptText, zielZeitpunktUtc)` schreibt/ersetzt den Datensatz (DB zuerst), danach `RemoveEntry` + neuer `ScheduledPromptEntry` in `_scheduledPrompts` + `timer.Change` (bestehende Reihenfolge-Garantie bleibt).
5. `ScheduledPromptChanged` feuern → UI zeigt „Prompt in Wartestellung" + Zielzeit.

Beteiligte Klassen: `TaskDetailViewModel`, `PromptVorlagenPlatzhalterService`, `PromptZeitVersandService`, `ZeitgesteuerterPromptStore`, `SoftwareschmiededDbContext`, `TimeProvider`/`ITimer`.

### Fälligkeit mit aktiver Session

1. `ITimer`-Callback → `HandleTimerElapsedAsync(aufgabeId)`: Eintrag unter `_lock` entnehmen.
2. Aktuelles `PausiertBisUtc` laden; bei aktiver Pause: Eintrag mit `TargetTime = bis` zurücklegen, Timer neu armieren **und** `ZeitgesteuerterPromptStore.UpdateZielZeitpunktAsync` den Datensatz aktualisieren (neu).
3. Keine Pause → `SendPromptAsync`: `GetTerminalSession` liefert Session → `WritePromptAsync` → bei Erfolg `ZeitgesteuerterPromptStore.LoeschenAsync(aufgabeId)` (neu) → `PromptSent` + `ScheduledPromptChanged` feuern.
4. Protokolleintrag (neu): Hat `PromptSent` **keine** Abonnenten (keine geöffnete Detailansicht), schreibt `SendPromptAsync` zusätzlich einen `Protokolleintrag` (`ProtokollTyp.SystemMeldung`, z. B. „Zeitgesteuerter Prompt versendet.") über den per `IServiceScopeFactory` aufgelösten `ProtokollService.AddEintragAsync`. Fehler — insbesondere eine zwischenzeitlich gelöschte Aufgabe (FK-Verletzung) — werden geloggt und beeinflussen den erfolgreichen Versand nicht.
5. Schlägt `WritePromptAsync` fehl (keine Session, `ObjectDisposedException` oder sonstiger Fehler), geht der Eintrag in „wartet auf Session" über — siehe Ablauf „Fälligkeit ohne aktive Session / Sendefehler".

Beteiligte Klassen: `PromptZeitVersandService`, `ZeitgesteuerterPromptStore`, `AufgabeService`, `ProtokollService`, `ITerminalSession`, `KiAusfuehrungsService`.

### Fälligkeit ohne aktive Session / Sendefehler

1. `HandleTimerElapsedAsync` → `SendPromptAsync` findet keine Session (`GetTerminalSession` = `null`), oder `WritePromptAsync` scheitert (`ObjectDisposedException` oder sonstiger Sendefehler).
2. Statt bisherigem stillem Verwurf: Der **Datensatz bleibt persistiert**; der In-Memory-Eintrag wird im Zustand „wartet auf Session" gehalten (Timer disposed, `ScheduledPromptEntry.WartetAufSession = true`), sodass `GetScheduledPromptStatus` den Plan weiterhin meldet. Es gibt **keinen** Retry-Timer — der einzige Wiederaufnahmeweg ist der nächste Session-Start.
3. `ScheduledPromptChanged` feuern (Statusanzeige bleibt „Prompt in Wartestellung").
4. Zustellung erfolgt später über den Ablauf „Session-Start mit wartendem Plan".

Beteiligte Klassen: `PromptZeitVersandService`, `KiAusfuehrungsService`, `ZeitgesteuerterPromptStore`, `ITerminalSession`.

### Session-Start mit wartendem/fälligem Plan

1. CLI der Aufgabe startet → `KiAusfuehrungsService` feuert `CliProcessStatusChanged(aufgabeId, Gestartet)`.
2. `PromptZeitVersandService` (Abonnent, neu) prüft den In-Memory-Eintrag: Liegt ein Eintrag im Zustand „wartet auf Session" oder mit verstrichener `TargetTime` vor → `SendPromptAsync` → bei Erfolg Datensatz löschen, Eintrag entfernen, `PromptSent` + `ScheduledPromptChanged`.
3. Protokolleintrag (neu): Bei erfolgreicher Nachzustellung wird **immer** ein `Protokolleintrag` (`ProtokollTyp.SystemMeldung`, z. B. „Zeitgesteuerter Prompt nach CLI-Start zugestellt.") via `ProtokollService.AddEintragAsync` geschrieben — auch bei geöffneter Detailansicht, da die Zustellung nicht vom Anwender direkt ausgelöst wurde.
4. Liegt ein Eintrag mit zukünftiger `TargetTime` vor → nichts zu tun (Timer bereits armiert).
5. Scheitert der Zustellversuch erneut, bleibt der Eintrag in „wartet auf Session" (gleiche Retry-Semantik wie bei der Timer-Fälligkeit).

Beteiligte Klassen: `PromptZeitVersandService`, `KiAusfuehrungsService`, `ZeitgesteuerterPromptStore`, `ProtokollService`, `ITerminalSession`.

### App-Start: Re-Arm persistierter Pläne

1. `App.OnStartup`: `db.Database.MigrateAsync()` (unverändert), danach `PromptZeitVersandService` auflösen und `RestoreScheduledPromptsAsync()` aufrufen (neu, im selben Scope-Block).
2. `RestoreScheduledPromptsAsync` lädt alle Datensätze via `ZeitgesteuerterPromptStore.GetAlleAsync`.
3. Pro Datensatz: Aufgabe existiert nicht mehr → Datensatz löschen (defensiv). Aktive Pause jenseits der Zielzeit → Zielzeit auf Pausenende vorladen und Datensatz aktualisieren (Konsistenz mit Planungslogik).
4. Zielzeit in Zukunft → `ScheduledPromptEntry` anlegen + Timer auf Restdauer armieren.
5. Zielzeit verstrichen → `ScheduledPromptEntry` im Zustand „wartet auf Session" anlegen (kein Timer); Zustellung beim nächsten `CliProcessStatusChanged(Gestartet)`. Begründung: nach App-Start existiert noch keine Session (Kindprozesse sterben mit der App) — ein Sofortversand beim Start wäre wirkungslos, der Plan darf aber nicht verworfen werden.

Beteiligte Klassen: `App`, `PromptZeitVersandService`, `ZeitgesteuerterPromptStore`, `AufgabeService`, `TimeProvider`.

### Aufgabenabschluss (Storno — unveränderte Semantik)

1. `EntwicklungsprozessService.AbschliessenCoordinatedAsync`: `StopCliAsync`, dann `await PromptZeitVersandService.CancelScheduledPromptAsync(aufgabeId)` (Signatur async, neu).
2. `CancelScheduledPromptAsync` entfernt In-Memory-Eintrag + Timer und löscht den Datensatz via `ZeitgesteuerterPromptStore.LoeschenAsync`; `ScheduledPromptChanged` feuern.
3. Physisches Löschen der Aufgabe löscht den Datensatz per `DeleteBehavior.Cascade` mit; ein verwaister In-Memory-Eintrag ist harmlos (Session kann nie wieder starten) und verschwindet spätestens beim nächsten App-Start, da kein Datensatz mehr existiert.

Beteiligte Klassen: `EntwicklungsprozessService`, `PromptZeitVersandService`, `ZeitgesteuerterPromptStore`, `SoftwareschmiededDbContext`.

### Manueller Storno über die Detailansicht

1. Anwender klickt den Button „Wartestellung aufheben" (`WartestellungAufheben`) — sichtbar nur solange `ScheduledPromptStatus` gesetzt ist (Button liegt im sichtbarkeitsgesteuerten Status-`StackPanel`).
2. `TaskDetailViewModel.CancelScheduledPromptCommand` (`AsyncRelayCommand`) ruft `await PromptZeitVersandService.CancelScheduledPromptAsync(_aufgabeId)` auf.
3. `CancelScheduledPromptAsync` entfernt In-Memory-Eintrag + Timer und löscht den Datensatz via `ZeitgesteuerterPromptStore.LoeschenAsync`; `ScheduledPromptChanged` feuern.
4. `OnScheduledPromptChanged` im ViewModel liest `GetScheduledPromptStatus` (jetzt `null`) und leert `ScheduledPromptStatus`/`ScheduledPromptTimeDisplay` — Statusanzeige und Button verschwinden über den `NullOrEmptyToVisibilityConverter`; der `⏳`-Indikator in der Aufgabenliste verschwindet bei der nächsten Timer-Aktualisierung.

Beteiligte Klassen: `TaskDetailView` (XAML), `TaskDetailViewModel`, `PromptZeitVersandService`, `ZeitgesteuerterPromptStore`.

### Verlassen und Wiederöffnen der Detailansicht

1. `TaskDetailViewModel.Dispose` ruft `CancelScheduledPrompt` **nicht mehr** auf — Plan läuft weiter (Kernanforderung).
2. `TaskDetailViewModel.OnCliProcessStatusChanged` ruft bei CLI-Stopp `CancelScheduledPrompt` **nicht mehr** auf und leert die Statusanzeige nicht mehr eigenmächtig; Zustandsänderungen kommen ausschließlich über `ScheduledPromptChanged`/`PromptSent`.
3. Beim Wiederöffnen befüllt `LadenAsync` `ScheduledPromptStatus`/`ScheduledPromptTimeDisplay` aus `GetScheduledPromptStatus` — damit ist ein in früherer Sitzung oder vor App-Neustart geplanter Prompt sofort sichtbar.

Beteiligte Klassen: `TaskDetailViewModel`, `PromptZeitVersandService`.

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `ZeitgesteuerterPrompt` | Entity (`src/Softwareschmiede/Domain/Entities/`) | Persistierter Datensatz eines geplanten Prompts: `Id` (Guid, PK), `AufgabeId` (Guid, FK → `Aufgabe`), `PromptText` (string, required), `ZielZeitpunktUtc` (DateTimeOffset, `UnixMillisConverter`), `ErstelltAmUtc` (DateTimeOffset), `AktualisiertAmUtc` (DateTimeOffset — Pause-Verschiebungen/Ersetzen). |
| `ZeitgesteuerterPromptStore` | Scoped Service (`src/Softwareschmiede/Application/Services/`) | EF-Zugriff für `PromptZeitVersandService`: `UpsertAsync` (Ersetzungsregel), `LoeschenAsync(aufgabeId)`, `GetAlleAsync`, `UpdateZielZeitpunktAsync`. |

## Änderungen an bestehenden Klassen

### `PromptZeitVersandService` (Singleton-Service)

- **Neue Events:** `ScheduledPromptChanged` (`Action<Guid>`) — gefeuert bei Planen, Storno, erfolgreichem Versand und Übergang in „wartet auf Session".
- **Neue Methoden:** `RestoreScheduledPromptsAsync()` — lädt persistierte Pläne beim App-Start und armiert Timer bzw. markiert „wartet auf Session" (siehe Ablauf „App-Start").
- **Geänderte Methoden:**
  - `SchedulePromptAsync` — zusätzlich `ZeitgesteuerterPromptStore.UpsertAsync` vor dem Timer-Armieren (nur wenn Zielzeit in Zukunft; Sofortversand persistiert nicht).
  - `CancelScheduledPrompt` → umbenannt zu `CancelScheduledPromptAsync` (`async Task`): entfernt zusätzlich den Datensatz. Aufrufstellen siehe unten.
  - `HandleTimerElapsedAsync` — Pause-Zweig aktualisiert `ZielZeitpunktUtc` im Datensatz; Kein-Pause-Zweig löscht Datensatz nur bei erfolgreichem Versand.
  - `SendPromptAsync` — ohne Session oder bei nicht erfolgreicher Zustellung (`ObjectDisposedException` oder sonstiger `WritePromptAsync`-Fehler) bleibt der Datensatz bestehen und der Eintrag geht in „wartet auf Session" über (bisher: stiller Verwurf). Bei Erfolg Datensatz löschen; anschließend ggf. `SchreibeVersandProtokollAsync` aufrufen (siehe unten).
- **Neue private Methoden:** `SchreibeVersandProtokollAsync(aufgabeId, nachzustellung)` — schreibt `ProtokollTyp.SystemMeldung` via scoped `ProtokollService.AddEintragAsync` (aufgelöst über `_scopeFactory`, wie `GetPausiertBisUtcAsync`). Aufrufbedingung: bei Nachzustellung über `CliProcessStatusChanged(Gestartet)` immer; bei Timer-/Sofortversand nur wenn `PromptSent` keinen Abonnenten hat. Schlägt das Schreiben fehl (z. B. Aufgabe nicht mehr vorhanden) → Warn-Log, kein Einfluss auf den Versand. Ohne `_scopeFactory` (Unit-Test-Konstruktion) entfällt der Eintrag analog zur Pausenprüfung.
- **Neue Event-Handler:** Abonnement von `KiAusfuehrungsService.CliProcessStatusChanged` im Konstruktor — bei `Gestartet` Nachzustellung wartender/fälliger Pläne.
- **Geänderte innere Struktur:** `ScheduledPromptEntry` erhält Zustand „wartet auf Session" (z. B. Flag `WartetAufSession` und nullable/entsorgter `Timer`).
- **Klassenkommentar:** „keine Persistierung"-Hinweis entfernen/aktualisieren.

### `TaskDetailViewModel` (ViewModel)

- **Geänderte Methoden:**
  - `Dispose()` — `CancelScheduledPrompt`-Aufruf entfernen; stattdessen `ScheduledPromptChanged`-Abonnement lösen.
  - `OnCliProcessStatusChanged` — `CancelScheduledPrompt`-Aufruf und das Leeren von `ScheduledPromptStatus`/`ScheduledPromptTimeDisplay` entfernen.
  - `LadenAsync` — nach dem Laden `ScheduledPromptStatus`/`ScheduledPromptTimeDisplay` aus `GetScheduledPromptStatus` befüllen.
- **Neue Properties:** `CancelScheduledPromptCommand` (`ICommand`, `AsyncRelayCommand`) — ruft `await _promptZeitVersandService.CancelScheduledPromptAsync(_aufgabeId)`; das Leeren der Statusanzeige erfolgt über `OnScheduledPromptChanged`, nicht lokal im Command (einheitliche Synchronisation).
- **Neue Event-Handler:** `OnScheduledPromptChanged` — liest `GetScheduledPromptStatus` neu und setzt/leert Status- und Zeitanzeige entsprechend (ersetzt die bisherige implizite Synchronisation; trägt auch den manuellen Storno und externe Stornos wie Aufgabenabschluss in die UI).

### `TaskDetailView` (XAML-View)

- **Neues Element:** Button „Wartestellung aufheben" innerhalb des Status-`StackPanel` (Zeilen ~154–163, `Visibility` bereits via `NullOrEmptyToVisibilityConverter` an `ScheduledPromptStatus` gebunden — der Button erbt die Sichtbarkeit und ist nur sichtbar, solange ein Plan wartet). `AutomationProperties.Name="WartestellungAufheben"` für FlaUI (Muster `ZeitgesteuertSenden`, `ScheduledPromptStatus`); `Command="{Binding CancelScheduledPromptCommand}"`; dezente Gestaltung passend zur Statusanzeige (kleiner Button/Link-Stil neben oder unter `ScheduledPromptTimeDisplay`).

### `ProtokollService` (Service)

- Keine Änderung: `AddEintragAsync(aufgabeId, ProtokollTyp.SystemMeldung, inhalt)` wird unverändert wiederverwendet; die Auflösung erfolgt im `PromptZeitVersandService` über die vorhandene `IServiceScopeFactory`.

### `EntwicklungsprozessService` (Service)

- **Geänderte Methoden:** `AbschliessenCoordinatedAsync` — `CancelScheduledPrompt` → `await CancelScheduledPromptAsync` (Zeile 438). Semantik unverändert: Abschluss storniert den Plan, jetzt inklusive Datensatz.

### `SoftwareschmiededDbContext` (DbContext)

- **Neue Eigenschaften:** `DbSet<ZeitgesteuerterPrompt> ZeitgesteuertePrompts`.
- **`OnModelCreating`:** Mapping: PK `Id`; `PromptText` required; `ZielZeitpunktUtc`, `ErstelltAmUtc`, `AktualisiertAmUtc` über `UnixMillisConverter`; Unique-Index auf `AufgabeId` (Ersetzungsregel); FK `AufgabeId` → `Aufgabe` mit `DeleteBehavior.Cascade` (Muster `Todo`/`Protokolleintrag`).

### `App` (`App.xaml.cs`)

- **DI-Registrierung:** `services.AddScoped<ZeitgesteuerterPromptStore>()`.
- **Startpfad:** Nach `db.Database.MigrateAsync()` im bestehenden Scope-Block (Zeilen ~93–100) `PromptZeitVersandService` auflösen und `await RestoreScheduledPromptsAsync()` aufrufen; Fehler beim Re-Arm loggen und Start fortsetzen (Muster `CliProcessManager`-Auflösung, Zeilen 86–91).

### `MainWindowViewModel` (ViewModel)

- Keine Änderung: `HasScheduledPrompt` liest weiterhin synchron `GetScheduledPromptStatus` (Zeile 406); die Liste wird über den bestehenden `_aktualisierungsTimer` periodisch neu aufgebaut und spiegelt den wiederhergestellten Zustand automatisch.

### `ScheduledPromptInfo` (Record)

- Keine Änderung erforderlich: `AufgabeId` + `PromptText` + `TargetTime` reichen; der Datensatz wird über `AufgabeId` identifiziert (max. ein Plan pro Aufgabe).

## Datenbankmigrationen

| Migrationsname | Betroffene Tabellen/Spalten | Beschreibung der Änderung |
|----------------|----------------------------|---------------------------|
| `AddZeitgesteuerterPrompt` | Neue Tabelle `ZeitgesteuertePrompts` (`Id` PK, `AufgabeId` FK→`Aufgaben` Cascade, `PromptText`, `ZielZeitpunktUtc` long, `ErstelltAmUtc` long, `AktualisiertAmUtc` long); Unique-Index `IX_ZeitgesteuertePrompts_AufgabeId` | Persistenz der Warteschlange; läuft beim App-Start via `db.Database.MigrateAsync()` mit. |

## Validierungsregeln

Keine neuen — die bestehende HH:mm-Validierung in `TaskDetailViewModel.SchedulePromptAsync` (Stunde 0–23, Minute 0–59) bleibt unverändert. Die Ersetzungsregel ist durch den Unique-Index auf `AufgabeId` datenbankseitig abgesichert.

## Konfigurationsänderungen

Keine.

## Seiteneffekte und Risiken

- **Anzeige bei CLI-Stopp:** `ScheduledPromptStatus` bleibt nach CLI-Stopp sichtbar (bewusst — der Plan existiert weiterhin). Andere Beobachter von `CliProcessStatusChanged` (`CliProcessManager`) sind unberührt.
- **`CancelScheduledPrompt` → `CancelScheduledPromptAsync` (Signaturwechsel):** Alle Aufrufer müssen angepasst werden; Fire-and-forget-Aufrufe müssen `SafeFireAndForget` nutzen oder entfallen. Betroffene Tests siehe unten.
- **Verwaiste In-Memory-Einträge** nach physischem Löschen einer Aufgabe bleiben bis App-Ende bestehen (harmlos: keine Session, kein Versand; Datensatz ist per Cascade weg und wird beim nächsten Start nicht wiederhergestellt).
- **Fehlerpfad `WritePromptAsync` (Festlegung 2026-11-07):** Jeder fehlgeschlagene Zustellversand — keine Session, `ObjectDisposedException` oder sonstiger Fehler — erhält Plan + Datensatz und überführt den Eintrag in „wartet auf Session"; erneuter Versand beim nächsten `CliProcessStatusChanged(Gestartet)`. Persistente Sendefehler (z. B. defekte Session-Pipe bei jedem Start) können theoretisch zu wiederholten Fehlversuchen bei jedem CLI-Start führen — sichtbar über Log + weiterhin „in Wartestellung" angezeigtem Plan; Abhilfe über den neuen Storno-Button.
- **Storno-Button-Sichtbarkeit:** Der Button „Wartestellung aufheben" liegt im `ScheduledPromptStatus`-`StackPanel` und ist damit immer genau dann sichtbar wie die Wartestellungs-Anzeige — kein separater Zustand, der auseinanderlaufen könnte. Bei Sofortversand (Zielzeit ≤ jetzt) ist `ScheduledPromptStatus` nicht gesetzt → kein Button, korrekt.
- **Protokolleintrag-Menge:** SystemMeldung-Einträge entstehen nur bei Hintergrundversand ohne `PromptSent`-Abonnenten bzw. bei Nachzustellung — nicht bei jedem regulären Versand vor geöffneter Detailansicht. Ein Fehlschlag des Protokollschreibens (z. B. gelöschte Aufgabe) wird nur geloggt.
- **`PromptSent`-Abonnentenzahl als „Detailansicht offen"-Indikator:** `TaskDetailViewModel` ist der einzige produktive Abonnent und löst das Abo in `Dispose` — die Bedingung ist damit fachlich korrekt. In Tests, die `PromptSent` abonnieren, gilt der Eintrag ebenfalls als „Beobachter vorhanden" (getestetes Verhalten explizit festlegen).
- **Races:** DB-Schreiben erfolgt vor dem Timer-Armieren; ein Timer-Callback zwischen DB-Upsert und `_scheduledPrompts`-Eintrag wird durch die bestehende „inaktiv anlegen, dann `Change`"-Reihenfolge verhindert. `Upsert` muss die Ersetzungsregel atomar abbilden (Unique-Index).
- **Tests mit `FakeTimeProvider`:** Persistenz ist über `IServiceScopeFactory` konditional — Tests ohne Scope-Factory verhalten sich wie bisher in-memory; neue Tests nutzen InMemory-`TestDbContextFactory` (Muster `CreatePausedTaskProviderAsync`).
- **Update-Szenario:** Installationen mit bestehender Datenbank erhalten die Tabelle via `MigrateAsync`; Altbestand ohne Einträge ist unkritisch.

## Umsetzungsreihenfolge

1. **Entity `ZeitgesteuerterPrompt` + `DbSet` + `OnModelCreating`-Mapping**
   - Voraussetzungen: Keine (bestehende EF-Konventionen: `UnixMillisConverter`, FK/Cascade-Muster).
   - Beschreibung: Entity unter `src/Softwareschmiede/Domain/Entities/` anlegen; `DbSet` und Mapping in `SoftwareschmiededDbContext` ergänzen.

2. **EF-Migration `AddZeitgesteuerterPrompt` erstellen**
   - Voraussetzungen: Schritt 1.
   - Beschreibung: `dotnet ef migrations add` analog bestehender Migrationen; Migration und ModelSnapshot unter `src/Softwareschmiede/Migrations/`.

3. **`ZeitgesteuerterPromptStore` anlegen**
   - Voraussetzungen: Schritt 1.
   - Beschreibung: Scoped Service mit `UpsertAsync`, `LoeschenAsync`, `GetAlleAsync`, `UpdateZielZeitpunktAsync`; DI-Registrierung `AddScoped` in `App.xaml.cs`.

4. **`PromptZeitVersandService` um Persistenz und neue Semantik erweitern**
   - Voraussetzungen: Schritt 3.
   - Beschreibung: `Upsert` in `SchedulePromptAsync`; `CancelScheduledPrompt` → `CancelScheduledPromptAsync` inkl. Datensatzlöschung; `SendPromptAsync`/`HandleTimerElapsedAsync` anpassen (Löschen bei Erfolg, „wartet auf Session" bei fehlender Session **oder** Sendefehler, Pause-Update des Datensatzes); `ScheduledPromptEntry` um Zustand erweitern; `ScheduledPromptChanged`-Event; Abonnement `CliProcessStatusChanged` für Nachzustellung; `SchreibeVersandProtokollAsync` (SystemMeldung via scoped `ProtokollService`, Bedingungen siehe Ablauf „Fälligkeit mit aktiver Session"/„Session-Start").

5. **Re-Arm beim App-Start verdrahten**
   - Voraussetzungen: Schritt 4.
   - Beschreibung: `RestoreScheduledPromptsAsync` implementieren und in `App.OnStartup` nach `MigrateAsync` aufrufen (fehlertolerant mit Logging).

6. **`TaskDetailViewModel` anpassen**
   - Voraussetzungen: Schritt 4.
   - Beschreibung: `CancelScheduledPrompt`-Aufrufe aus `Dispose` und `OnCliProcessStatusChanged` entfernen; `LadenAsync` befüllt Statusanzeige aus `GetScheduledPromptStatus`; `OnScheduledPromptChanged`-Handler + Abo-Verwaltung; `CancelScheduledPromptCommand` (`AsyncRelayCommand`) hinzufügen.

7. **Storno-Button in `TaskDetailView.xaml` ergänzen**
   - Voraussetzungen: Schritt 6 (`CancelScheduledPromptCommand` muss existieren).
   - Beschreibung: Button „Wartestellung aufheben" mit `AutomationProperties.Name="WartestellungAufheben"` im Status-`StackPanel` (erbt `Visibility` via `NullOrEmptyToVisibilityConverter`).

8. **`EntwicklungsprozessService` anpassen**
   - Voraussetzungen: Schritt 4.
   - Beschreibung: `await CancelScheduledPromptAsync` statt `CancelScheduledPrompt`.

9. **Unit-Tests anpassen und neue schreiben**
   - Voraussetzungen: Schritte 4–8.
   - Beschreibung: Siehe Abschnitt „Tests".

10. **E2E-Test und E2E-View erweitern**
    - Voraussetzungen: Schritte 6–7.
    - Beschreibung: Hilfsmethode `CancelScheduledPrompt()` in `src/Softwareschmiede.Tests/E2E/Views/TaskDetailView.cs` ergänzen; bestehende Methode `ZeitgesteuerterPrompt_NachPlanen_ZeigtWartestellungStatus_E2E` um Verlassen/Wiederöffnen/Storno-Fluss erweitern (siehe Abschnitt „E2E-Tests").

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `SchedulePromptAsync_SchreibtDatensatz` | `PromptZeitVersandServiceTests` | Planen mit zukünftiger Zielzeit legt genau einen `ZeitgesteuerterPrompt`-Datensatz (Text, Zielzeit, AufgabeId) an. |
| `SchedulePromptAsync_ErneutesPlanen_ErsetztDatensatz` | `PromptZeitVersandServiceTests` | Zweite Planung derselben Aufgabe: weiterhin genau ein Datensatz mit neuem Text/Zielzeit (Upsert). |
| `Versand_LoeschtDatensatz` | `PromptZeitVersandServiceTests` | Nach erfolgreichem `WritePromptAsync` ist Datensatz + In-Memory-Eintrag entfernt. |
| `CancelScheduledPromptAsync_LoeschtDatensatz` | `PromptZeitVersandServiceTests` | Storno entfernt Datensatz und Eintrag. |
| `Timer_OhneSession_BehaeltPlanUndWartetAufSession` | `PromptZeitVersandServiceTests` | Fälligkeit ohne Session: Datensatz bleibt, `GetScheduledPromptStatus` meldet weiterhin den Plan (ersetzt den bisherigen „still verworfen"-Test). |
| `Sendefehler_BehaeltPlanUndWartetAufSession` | `PromptZeitVersandServiceTests` | `WritePromptAsync` wirft einen sonstigen Fehler (≠ `ObjectDisposedException`): Datensatz + Plan bleiben, `GetScheduledPromptStatus` meldet den Plan weiterhin; nächster `CliProcessStatusChanged(Gestartet)` liefert erfolgreich zu (Retry-Semantik). |
| `CliGestartet_MitWartendemPlan_ZustelltNach` | `PromptZeitVersandServiceTests` | `CliProcessStatusChanged(Gestartet)` mit wartendem/fälligem Plan → `WritePromptAsync` + `PromptSent`, Datensatz gelöscht. |
| `Nachzustellung_SchreibtSystemMeldung` | `PromptZeitVersandServiceTests` | Zustellung über `CliProcessStatusChanged(Gestartet)` schreibt `Protokolleintrag` (`ProtokollTyp.SystemMeldung`) auf der Aufgabe — auch mit `PromptSent`-Abonnent. |
| `Versand_OhneAbonnent_SchreibtSystemMeldung` | `PromptZeitVersandServiceTests` | Timer-Versand ohne `PromptSent`-Abonnenten (keine geöffnete Detailansicht) schreibt `SystemMeldung`-Eintrag. |
| `Versand_MitAbonnent_KeinProtokollEintrag` | `PromptZeitVersandServiceTests` | Timer-Versand mit `PromptSent`-Abonnenten schreibt **keinen** Protokolleintrag. |
| `ProtokollEintrag_BeiGeloeschterAufgabe_BrichtVersandNicht` | `PromptZeitVersandServiceTests` | `AddEintragAsync` schlägt fehl (Aufgabe nicht mehr vorhanden): Versand bleibt erfolgreich, Datensatz wird gelöscht, `PromptSent` feuert. |
| `Pause_Verschiebung_AktualisiertDatensatz` | `PromptZeitVersandServiceTests` | Pause-Verschiebung im Timer-Callback schreibt `ZielZeitpunktUtc` = Pausenende in den Datensatz. |
| `Restore_ZukuenftigerPlan_ArmiertTimer` | `PromptZeitVersandServiceTests` | `RestoreScheduledPromptsAsync` mit zukünftigem Datensatz → `GetScheduledPromptStatus` meldet Plan, `Advance` löst Versand aus. |
| `Restore_UeberfaelligerPlan_WartetAufSession` | `PromptZeitVersandServiceTests` | Überfälliger Datensatz → kein Sofortversand ohne Session; Plan bleibt gemeldet; Session-Start stellt zu. |
| `Restore_VerwaisterDatensatz_WirdGeloescht` | `PromptZeitVersandServiceTests` | Datensatz ohne existierende Aufgabe wird beim Re-Arm entfernt. |
| `Restore_NachNeustart_LiefertBeiSessionStartZu` | `PromptZeitVersandServiceTests` | Neuer Service gegen dieselbe DB (simulierter App-Neustart): Plan wird bei `Gestartet` zugestellt — End-to-end-Nachweis der Kernanforderung auf Service-Ebene. |
| `LadenAsync_MitGeplantemPrompt_ZeigtWartestellungStatus` | `TaskDetailViewModelTests_ZeitgesteuerterPrompt` | Wiederöffnen: `ScheduledPromptStatus`/`ScheduledPromptTimeDisplay` werden aus dem Service-Zustand befüllt. |
| `ScheduledPromptChanged_AktualisiertAnzeige` | `TaskDetailViewModelTests_ZeitgesteuerterPrompt` | Externes Storno (Service-seitig) leert die Anzeige; neues Planen setzt sie. |
| `CancelScheduledPromptCommand_EntferntPlanUndAnzeige` | `TaskDetailViewModelTests_ZeitgesteuerterPrompt` | `CancelScheduledPromptCommand` ruft `CancelScheduledPromptAsync`; danach sind `ScheduledPromptStatus`/`ScheduledPromptTimeDisplay` leer und `GetScheduledPromptStatus` liefert `null`. |
| Hilfsmethode `CreateStoreProviderAsync` | `PromptZeitVersandServiceTests` | `ServiceProvider` mit InMemory-`SoftwareschmiededDbContext` + `ZeitgesteuerterPromptStore` + `AufgabeService` + `ProtokollService` (Muster `CreatePausedTaskProviderAsync`). |
| Hilfsmethode `CancelScheduledPrompt()` (+ ggf. `WaitForScheduledPromptStatusGone`) | `src/Softwareschmiede.Tests/E2E/Views/TaskDetailView.cs` (FlaUI-View, kein Test) | Klickt `WartestellungAufheben` per `ByName` und wartet, bis `ScheduledPromptStatus` verschwunden ist (Muster `SendScheduledPrompt`). |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `Dispose_StorniertGeplantePrompts` (`TaskDetailViewModelTests_ZeitgesteuerterPrompt`) | Semantikumkehr: `Dispose` darf nicht mehr stornieren — Test wird zu „Dispose lässt Plan bestehen" (Eintrag weiterhin abrufbar). |
| `OnCliProcessStatusChanged_CliGestoppt_StorniertGeplantenPrompt` (ebd.) | Semantikumkehr: CLI-Stopp storniert nicht mehr — Plan bleibt bestehen (wartet auf nächste Session). |
| `Timer_OhneSession_VerwirftPromptStill` (`PromptZeitVersandServiceTests`) | Neues Verhalten: Plan wird nicht verworfen, sondern wartet auf Session — Test umzuwandeln/ersetzen. |
| `AbschliessenAsync_ShouldCancelScheduledPrompt_WhenPromptGeplant` (`EntwicklungsprozessServiceTests`) | Async-Signatur; ggf. zusätzliche Assertion, dass der Datensatz gelöscht ist. |
| `ScmTaskCompletionMonitoringServiceTests` (`Softwareschmiede.IntegrationTests`) | Nutzt `PromptZeitVersandService`/`GetScheduledPromptStatus` — Signatur-/Verhaltensänderung prüfen und anpassen. |
| `TaskDetailViewModelTestFactory.CreateStub` (Test-Helper) | Falls `PromptZeitVersandService`-Konstruktion oder `ScheduledPromptChanged`-Abo Verdrahtung erfordert. |
| Übrige `PromptZeitVersandServiceTests` (8 Methoden) | Umbenennung `CancelScheduledPrompt` → `CancelScheduledPromptAsync` (await); bestehende Assertions bleiben fachlich gültig. |

### E2E-Tests (primärer Funktionsnachweis)

| Priorität | Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium | Warum E2E nötig ist |
|-----------|----------|------------------------|-------------------------------|-------------------|
| Pflicht | Plan über UI anlegen → „Prompt in Wartestellung" + Storno-Button sichtbar → Detailansicht verlassen (`GoBack` → `ProjectDetailView`) → Datensatz weiterhin in DB (`OpenTestDbContext`) → Detailansicht wiederöffnen (`OpenFirstTask`) → Wartestellung + Storno-Button weiterhin sichtbar → `CancelScheduledPrompt()` klicken → Wartestellung weg + Datensatz gelöscht (DB-Check); danach Bereinigung. | Erweiterung der **bestehenden** Methode `ZeitgesteuerterPrompt_NachPlanen_ZeigtWartestellungStatus_E2E` in `src/Softwareschmiede.Tests/E2E/E2E_ZeitgesteuerterPrompt.cs` (kein neuer Testmethoden-Einstieg — Konsolidierungsregel); neue View-Hilfsmethode `CancelScheduledPrompt()` in `src/Softwareschmiede.Tests/E2E/Views/TaskDetailView.cs` | Kernanforderung: Plan überlebt das Verlassen der Detailansicht und ist beim Wiederöffnen sichtbar; Persistenz nachgewiesen; manueller Storno entfernt Anzeige **und** Datensatz. | Benutzerfluss (Verlassen/Wiederöffnen + persistierte Anzeige + Storno über echte UI-Interaktion) ist nur über die echte UI-Navigation belegbar; Unit-Tests decken den View-Lebenszyklus und die XAML-Verdrahtung des neuen Buttons nicht ab. |

Welche bestehenden E2E-Tests müssen angepasst werden?

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `ZeitgesteuerterPrompt_NachPlanen_ZeigtWartestellungStatus_E2E` (aufgerufen in `MainTest.ConPty_WeitereLebenszyklusSzenarien_E2E`) | Erweiterung um Verlassen/Wiederöffnen/DB-Prüfung sowie den Storno-Fluss (Button sichtbar → Klick → Anzeige weg + Datensatz gelöscht), siehe oben. |
| `SessionLimit_MarkerPauseProtokollUndUpdateSicherheit_E2E` (`MainTest.General_RepositoryUndAufgaben_E2E`) | Nutzt Sofortversand — unverändertes Verhalten erwartet; im E2E-Lauf als Regression mitlaufen lassen. |

Hinweis: Ein echter App-Neustart als E2E-Szenario (App beenden, neu starten, CLI-Start → Zustellung) würde einen zweiten App-Launch in FlaUI und ConPTY erfordern — in dieser Sandbox nicht lauffähig (`OsInterface`-Lane, `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS`). Die Re-Arm-/Nachzustellungslogik wird stattdessen über die Service-Ebene (`Restore_NachNeustart_LiefertBeiSessionStartZu`) nachgewiesen.

## Akzeptanzszenarien

| # | Szenario | Auslöser | Ausgangszustand | Schritte | Sichtbare Wirkung | Nachweis |
|---|----------|----------|------------------|----------|-------------------|----------|
| 1 | Happy Path: Planen → Ansicht verlassen → Versand im Hintergrund | Nutzer: Vorlage + Zielzeit + `ZeitgesteuertSenden`, dann Navigation weg | CLI-Session läuft; kein Plan vorhanden | Plan anlegen (Datensatz + Timer) → Detailansicht schließen (kein Storno mehr) → Zielzeit erreicht → `WritePromptAsync` an laufende Session → Datensatz gelöscht → `SystemMeldung`-Protokolleintrag (kein `PromptSent`-Abonnent = Detailansicht geschlossen) | Prompt erscheint in der CLI; Datensatz gelöscht; `⏳` verschwindet bei nächster Listenaktualisierung; Protokoll zeigt den Versand | E2E (UI-Anteil) + Unit-Tests `Versand_LoeschtDatensatz`, `Versand_OhneAbonnent_SchreibtSystemMeldung` |
| 2 | App-Neustart mit zukünftigem Plan | App-Start | Persistierter Plan mit Zielzeit in Zukunft; keine Session, keine Ansicht | `MigrateAsync` → `RestoreScheduledPromptsAsync` → Timer armiert → Zielzeit erreicht → Session muss bis dahin gestartet worden sein → Versand; sonst „wartet auf Session" | `⏳` in Aufgabenliste nach Start; Versand bei fälligem Zeitpunkt sobald Session existiert | Unit-Test `Restore_ZukuenftigerPlan_ArmiertTimer` |
| 3 | Überfälliger Plan / keine Session → Nachzustellung bei CLI-Start | App-Start bzw. Timer-Fälligkeit ohne Session | Persistierter Plan mit verstrichener Zielzeit; keine `ITerminalSession` | Plan geht in „wartet auf Session" (bleibt persistiert + sichtbar) → Nutzer startet CLI der Aufgabe → `CliProcessStatusChanged(Gestartet)` → Zustellung → Datensatz gelöscht → `SystemMeldung`-Protokolleintrag (Nachzustellung immer) | `ScheduledPromptStatus` bleibt bis zur Zustellung; Prompt wird beim CLI-Start zugestellt; Protokoll dokumentiert die Nachzustellung | Unit-Tests `Restore_UeberfaelligerPlan_WartetAufSession`, `CliGestartet_MitWartendemPlan_ZustelltNach`, `Nachzustellung_SchreibtSystemMeldung` |
| 4 | CLI-Stopp vor Fälligkeit | CLI-Prozessende/`CliProcessStatusChanged` ≠ `Gestartet` | Aktiver Plan, laufende Session | CLI stoppt → Plan bleibt (kein Storno) → Timer läuft weiter → bei Fälligkeit keine Session → „wartet auf Session" → CLI-Neustart → Zustellung | Plan überlebt den CLI-Wechsel; wird beim Neustart zugestellt | Unit-Test (angepasster `OnCliProcessStatusChanged`-Test + `CliGestartet_…`) |
| 5 | Aufgabenabschluss storniert Plan | `AbschliessenAsync` | Aktiver/persistierter Plan | `StopCliAsync` → `CancelScheduledPromptAsync` → Eintrag + Datensatz entfernt | Kein Versand mehr; `⏳` verschwindet; Statusanzeige über `ScheduledPromptChanged` geleert | Angepasster `EntwicklungsprozessServiceTests`-Test |
| 6 | Wiederöffnen zeigt persistierten Plan | Nutzer öffnet Detailansicht erneut | Persistierter Plan aus früherer Sitzung/App-Lauf | `LadenAsync` → `GetScheduledPromptStatus` → Anzeige befüllen | „Prompt in Wartestellung" + Zielzeit + Storno-Button sichtbar ohne erneutes Planen | Unit-Test `LadenAsync_…` + E2E-Erweiterung |
| 7 | Pausierte Aufgabe | Pause gesetzt (vor/nach Planung) | Aktiver Plan | Planungszeit bzw. Timer-Fälligkeit trifft aktive Pause → Zielzeit = `PausiertBisUtc`, Datensatz aktualisiert → Versand nach Pausenende | Anzeige zeigt verschobene Zeit; Versand erst nach Pause | Angepasste Pause-Tests + `Pause_Verschiebung_AktualisiertDatensatz` |
| 8 | Manueller Storno | Nutzer klickt „Wartestellung aufheben" | Persistierter Plan, Detailansicht geöffnet | `CancelScheduledPromptCommand` → `CancelScheduledPromptAsync` → Eintrag + Datensatz gelöscht → `ScheduledPromptChanged` leert Anzeige | Statusanzeige und Button verschwinden; `⏳` verschwindet; kein Versand; Datensatz weg (DB-Check) | E2E-Erweiterung + Unit-Test `CancelScheduledPromptCommand_EntferntPlanUndAnzeige` |
| 9 | Sendefehler bei Zustellung → Retry beim nächsten Session-Start | `WritePromptAsync` wirft sonstigen Fehler | Fälliger Plan, Session existiert aber schlägt fehl | Versand schlägt fehl → Eintrag geht in „wartet auf Session" (kein Verwurf) → nächster `CliProcessStatusChanged(Gestartet)` → erneuter Zustellversand erfolgreich | Plan bleibt sichtbar („in Wartestellung"); Zustellung beim nächsten CLI-Start; kein Datenverlust | Unit-Test `Sendefehler_BehaeltPlanUndWartetAufSession` |
| 10 | Dauerhaft wartender Plan (kein TTL) | — | Plan wartet auf Session, Aufgabe wird nie wieder gestartet | Kein automatischer Verfall; Datensatz bleibt unbegrenzt bestehen (Festlegung 2026-11-07) | `⏳`/Statusanzeige zeigen den Plan weiterhin; auflösbar über Storno-Button/Ersetzen/Abschluss/Löschen | Festlegung — Nachweis implizit über `Restore_`-Tests |

## Offene Punkte

Keine.
