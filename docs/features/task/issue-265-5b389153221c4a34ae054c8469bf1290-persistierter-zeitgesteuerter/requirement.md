# Übersetzte Anforderung: Persistierter zeitgesteuerter Prompt

**Quelle:** Issue #265 / Aufgaben-ID `5b389153-221c-4a34-ae05-4c8469bf1290`
**Branch:** `task/issue-265-5b389153221c4a34ae054c8469bf1290-persistierter-zeitgesteuerter`

## Fachliche Zusammenfassung

Die bestehende Zeitsteuerung des Prompt-Versands (`PromptZeitVersandService`) hält geplante Prompts ausschließlich in einer In-Memory-Warteschlange (`_scheduledPrompts`, `Dictionary<Guid, ScheduledPromptEntry>` mit `ITimer` je Eintrag). Beim Verlassen der Aufgabendetailansicht wird der geplante Prompt heute storniert (`TaskDetailViewModel.Dispose` → `CancelScheduledPrompt`), bei CLI-Stopp ebenfalls (`OnCliProcessStatusChanged` → `CancelScheduledPrompt`), und ein App-Neustart verliert alle Einträge. Die Anforderung erweitert diesen Mechanismus um Persistenz: Jeder einmal geplante zeitgesteuerte Prompt soll dauerhaft gespeichert werden und zum gewünschten Zeitpunkt auch dann versendet werden, wenn der Anwender die Aufgabendetailansicht zwischenzeitlich verlassen hat bzw. die App neu gestartet wurde.

## Fachlicher Scope

- **Verarbeitungseinheit:** Ein *geplanter zeitgesteuerter Prompt* — d. h. ein gebündelter Datensatz aus Ziel-`Aufgabe`, aufgelöstem Prompttext (`PromptVorlagenPlatzhalterService.Resolve` wird bereits vor der Übergabe an den Service angewendet, siehe `TaskDetailViewModel.SchedulePromptAsync`, Zeile ~1825) und Zielzeitpunkt (`ScheduledPromptInfo`), der zu einem Zeitpunkt an die `ITerminalSession` der Aufgabe versendet werden soll.
- **Auslösende Ereignisse:**
  - Nutzeraktion „Zeitgesteuert senden" in der `TaskDetailView` (Ribbon-Button `ZeitgesteuertSenden`, gebunden an `SchedulePromptCommand` → `TaskDetailViewModel.SchedulePromptAsync` → `PromptZeitVersandService.SchedulePromptAsync`).
  - Systemereignis „Zielzeitpunkt erreicht" (bisher Timer-Callback `HandleTimerElapsedAsync`).
  - Systemereignis „App-Start" (neu: Wiederherstellung/Re-Aktivierung persistierter Pläne — Annahme, da „im Hintergrund ausgeführt" andernfalls nicht über Prozesslebensdauer hinaus erreichbar ist).
  - Bestehende implizite Storno-/Verschiebe-Ereignisse: Aufgaben-Abschluss (`EntwicklungsprozessService.AbschliessenCoordinatedAsync`, Zeile ~438), Pausenverschiebung (`PausiertBisUtc`), CLI-Stopp, Verlassen der Detailansicht — deren Semantik unter Persistenz neu zu definieren ist (siehe Offene Fragen).
- **Erwartete Wirkung:** Der Prompttext wird zum geplanten Zeitpunkt per `ITerminalSession.WritePromptAsync` an die laufende CLI-Session der Aufgabe geschrieben (`PromptZeitVersandService.SendPromptAsync`, heute Zeile ~193) — auch wenn die Aufgabendetailansicht nicht geöffnet ist. Sichtbare Nebenwirkungen: `PromptSent`-Event (UI-Wechsel in die CLI-Ansicht, falls geöffnet), Statusanzeige „Prompt in Wartestellung" in der Detailansicht sowie der `⏳`-Indikator in der aktiven Aufgabenliste (`AktiveAufgabePanelItem.HasScheduledPrompt` → `AppConverters`) müssen den persistierten Zustand widerspiegeln.
- **Umgang mit fehlenden Objekten:**
  - *Keine aktive `ITerminalSession` zum Fälligkeitszeitpunkt* (CLI läuft nicht / nach App-Neustart grundsätzlich keine Session, da CLI-Prozesse Kindprozesse der App sind): heute wird der Prompt still verworfen (`SendPromptAsync` Log-Warnung). Unter Persistenz muss definiert werden: auf spätere Session warten, CLI automatisch (neu) starten, oder als verfallen markieren/benachrichtigen — **offene Frage**.
  - *Aufgabe zwischenzeitlich abgeschlossen oder gelöscht:* heute explizites `CancelScheduledPrompt` im Abschlusspfad; unter Persistenz ist der Datensatz zu löschen/zu invalidieren (Annahme: wie bisher stornieren, siehe Offene Fragen).
  - *Überfällige Persistenz-Einträge beim App-Start* (Zielzeit verstrichen, während die App nicht lief): Verhalten zu klären — **offene Frage**.
- **Scope-Ausschlüsse:**
  - Versand bei **nicht laufender App**: Die CLI-Sessions sind Kindprozesse der App (`ConPTY`); ohne laufende App existiert keine Zustellmöglichkeit. Ob „im Hintergrund" nur „App läuft, Detailansicht geschlossen/minimiert" meint oder auch „App beendet" (was einen externen Scheduler-Prozess erfordern würde), ist zu klären — **offene Frage**. Annahme: nur bei laufender App.
  - Keine Mehrfach-/Serienplanung: Es bleibt bei der dokumentierten Regel „pro Aufgabe maximal ein geplanter Prompt; erneutes Planen ersetzt den vorherigen" (`docs/help/aufgaben/business-rules.md`, Abschnitt „Zeitgesteuerter Prompt-Versand") — Annahme; die Formulierung „jeder einmal erstellte zeitgesteuerte Prompt" ließe auch mehrere zu (siehe Offene Fragen).
  - Keine Wiederholungspläne (kein Cron/Intervall), keine Änderung an der Pausen-Verschiebe-Logik (`PausiertBisUtc`) als solche — sie bleibt fachlich bestehen.
  - Keine Änderung am Auswahldialog (Promptvorlage + HH:mm-Eingabe) — sofern nicht aus den offenen Fragen Erweiterungen folgen (z. B. Datum statt nur Uhrzeit, expliziter Storno-Button).

## Betroffene Klassen und Komponenten

### Bestehende, voraussichtlich zu ändernde Artefakte

- `src/Softwareschmiede/Application/Services/PromptZeitVersandService.cs` — Kern der Änderung: Persistenz des Warteschlangen-Eintrags beim Planen (`SchedulePromptAsync`), Laden/Re-Armieren beim App-Start, Löschen bei Versand (`HandleTimerElapsedAsync`), Storno (`CancelScheduledPrompt`) und Aufgabenabschluss. Der Service ist bereits Singleton und besitzt mit `IServiceScopeFactory` (`_scopeFactory`, Zeile ~23) einen bestehenden Erweiterungspunkt für DB-Zugriff aus einem Singleton heraus (wird heute für `AufgabeService.GetByIdAsync`/`PausiertBisUtc` genutzt).
- `src/Softwareschmiede/Application/Services/ScheduledPromptInfo.cs` — Record `(AufgabeId, PromptText, TargetTime)`; ggf. um Persistenz-relevante Felder zu erweitern (z. B. Datensatz-ID, Erstellungszeitpunkt, Status) — Annahme.
- `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs` — `Dispose()` (Zeile ~1559) und `OnCliProcessStatusChanged` (Zeile ~1532) rufen heute `CancelScheduledPrompt`; das Verlassen der Detailansicht darf den persistierten Plan nicht mehr verwerfen. Beim Öffnen der Detailansicht ist `ScheduledPromptStatus`/`ScheduledPromptTimeDisplay` aus dem persistierten Zustand zu befüllen (Annahme).
- `src/Softwareschmiede/Application/Services/EntwicklungsprozessService.cs` — `CancelScheduledPrompt`-Aufruf beim Abschluss (Zeile ~438) muss auch den persistierten Datensatz löschen (Annahme: Abschluss storniert den Plan weiterhin).
- `src/Softwareschmiede.App/ViewModels/MainWindowViewModel.cs` — `HasScheduledPrompt` (Zeile ~406) liest heute die In-Memory-Warteschlange via `GetScheduledPromptStatus`; muss den persistierten Zustand widerspiegeln (ggf. transparent über denselben Service).
- `src/Softwareschmiede.App/App.xaml.cs` — DI-Registrierung (Zeile ~281 `AddSingleton<PromptZeitVersandService>`); ggf. Registrierung eines Start-/Hintergrunddiensts für das Re-Armieren persistierter Pläne (Muster: `AddHostedService`, z. B. `ScmEventMonitoringService`, `PullRequestMonitoringService`, `ScmTaskCompletionMonitoringService` — alle `PeriodicBackgroundService`-Ableitungen bzw. `BackgroundService`).
- `src/Softwareschmiede.App/Views/TaskDetailView.xaml` — nur falls aus den offenen Fragen UI-Erweiterungen folgen (z. B. Storno-Button für einen persistierten Plan — existiert heute nicht).

### Neu zu erstellende Artefakte (voraussichtlich, Namen sind Vorschläge)

- **Persistenz-Entität** für geplante Prompts, z. B. `GeplanterPrompt`/`ZeitgesteuerterPrompt` unter `src/Softwareschmiede/Domain/Entities/` — mit `DbSet` und `OnModelCreating`-Mapping in `src/Softwareschmiede/Infrastructure/Data/SoftwareschmiededDbContext.cs` (Konvention: `DateTimeOffset` über `UnixMillisConverter`/`NullableUnixMillisConverter`, Aufgabenbezug per FK mit `DeleteBehavior.Cascade`) sowie **EF-Core-Migration** unter `src/Softwareschmiede/Migrations/` (Migrationen laufen beim App-Start via `db.Database.MigrateAsync()`, `App.xaml.cs` Zeile ~96). Felder mindestens: `AufgabeId`, Prompttext, Zielzeitpunkt (UTC), Erstellungszeitpunkt; ggf. Status/Versandzeitpunkt (Annahme — genaue Felder offen).
- **Lade-/Re-Arm-Mechanismus beim App-Start**: entweder Erweiterung des `PromptZeitVersandService` (initiales Laden aller persistierten Pläne + Timer-Neuaufsetzung) oder ein neuer `IHostedService`/`PeriodicBackgroundService` — Entwurfsentscheidung (Annahme: bestehendes Timer-Modell mit `TimeProvider` beibehalten, da testbar).

### Tests

- `src/Softwareschmiede.Tests/Application/Services/PromptZeitVersandServiceTests.cs` — Persistenz: Planen schreibt Datensatz; Versand/Storno löscht ihn; Timer-Re-Arm aus Persistenz; Pause-Verschiebung aktualisiert den Datensatz.
- `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests_ZeitgesteuerterPrompt.cs`, `MainWindowViewModelTests_ZeitgesteuerterPrompt.cs`, `TaskDetailViewModelTests_Pause.cs`, `EntwicklungsprozessServiceTests.cs` — Anpassung: Verlassen der Ansicht storniert nicht mehr; `HasScheduledPrompt` aus Persistenz; Abschluss löscht Datensatz.
- Neu: Tests für App-Neustart-/Re-Arm-Verhalten und für den Fall „Zielzeit erreicht ohne aktive Session" (je nach Klärung der offenen Fragen).

## Implementierungsansatz

- **Persistenzschicht:** Neue Entity + DbSet + EF-Migration analog zum bestehenden Muster (`PromptVorlage`, `Todo`, `ScmOutgoingAction` als Referenz für warteschlangenartige persistierte Einträge). Alternativ denkbar: flache Spalten auf `Aufgabe` — dort existieren bereits `VorschlagPrompt`/`VorschlagAusfuehrenAbUtc` (`Aufgabe.cs` Zeilen 91–95, `AufgabeService.SavePromptVorschlagAsync`), die jedoch einem anderen fachlichen Zweck (gespeicherter Prompt-Vorschlag) dienen; Wiederverwendung ist **keine** Annahme, sondern abzuwägende Option (offene Frage).
- **Schreibpfad:** `SchedulePromptAsync` persistiert den Plan atomar zum Timer-Setup (Eintrag zuerst in DB, dann Timer scharf machen — analog der bestehenden Reihenfolge-Garantie im Code-Kommentar ab Zeile ~73). Ersetzungsregel (ein Plan pro Aufgabe) auf DB-Ebene abbilden (Upsert/Unique-Index auf `AufgabeId` — Annahme).
- **Zustellpfad:** `HandleTimerElapsedAsync`/`SendPromptAsync` löschen den Datensatz bei erfolgreichem Versand; `PromptSent` bleibt als UI-Signal. Die eigentliche Zustellung bleibt an die aktive `ITerminalSession` gebunden (`KiAusfuehrungsService.GetTerminalSession`) — Sessions leben prozessweit in `_handles` und überdauern das Verlassen der Detailansicht bereits heute, d. h. „Versand im Hintergrund bei geöffneter App" ist mit dem bestehenden Mechanismus erreichbar, sobald `Dispose`/`OnCliProcessStatusChanged` den Plan nicht mehr verwerfen.
- **Wiederherstellung beim Start:** Beim App-Start persistierte Pläne laden und Timer neu armieren (Service-Initialisierung oder `IHostedService`). Für Einträge mit verstrichener Zielzeit sowie für den Fall „keine Session" gelten die zu klärenden Regeln (offene Fragen). Die Pause-Verschiebung (`GetPausiertBisUtcAsync`) bleibt bestehen und muss den persistierten Zielzeitpunkt aktualisieren.
- **Abhängigkeiten:** `SoftwareschmiededDbContext` (scoped — Zugriff aus dem Singleton via vorhandener `IServiceScopeFactory`), `AufgabeService` (Pause-Prüfung, Aufgaben-Existenz), `TimeProvider`, `KiAusfuehrungsService`/`ITerminalSession`, `CliProcessManager` (indirekt: Session-Lebenszyklus, `CliProcessStatusChanged`).

## Konfiguration

- Aus der Anforderung ist **kein** Konfigurationsbedarf ableitbar. Mögliche, aber nicht geforderte Konfigurationspunkte (nur falls gewünscht, zu klären): Verhalten bei überfälligen Prompts nach App-Start (sofort senden vs. verwerfen vs. auf Session warten) und ob Zustellung einen CLI-(Neu-)Start auslösen darf.

## Offene Fragen

1. **„Im Hintergrund" — wie weit?** Soll der Versand nur bei laufender App (Detailansicht geschlossen/Fenster minimiert) funktionieren, oder auch wenn die App beendet ist? Letzteres wäre ohne laufende App technisch nicht an die CLI-Session zustellbar (CLI ist Kindprozess) und würde einen externen Scheduler/Prozess erfordern — vermutlich außerhalb des gemeinten Scopes, aber zu bestätigen.
2. **App-Neustart mit überfälligem Plan:** Zielzeit liegt beim Start in der Vergangenheit — sofort senden (sobald eine Session existiert), als verfallen markieren und benachrichtigen, oder verwerfen? Hinweis: nach einem Neustart existiert zunächst **keine** CLI-Session (Prozesse überleben den App-Exit nicht).
3. **Zielzeit erreicht, aber keine aktive `ITerminalSession`:** Soll der Prompt (a) solange gespeichert bleiben, bis die CLI der Aufgabe (wieder) läuft, (b) einen automatischen CLI-Start auslösen, oder (c) weiterhin still verworfen werden (heutiges Verhalten, wäre aber gegen den Geist der Persistenz)?
4. **CLI-Stopp vor Fälligkeit:** Heute storniert `OnCliProcessStatusChanged` (TaskDetailViewModel, Zeile ~1532) den Plan bei CLI-Ende. Bleibt diese Regel, oder soll ein persistierter Plan einen CLI-Neustart überdauern und bei der nächsten Session zugestellt werden?
5. **Ersetzungsregel:** Bleibt es bei „maximal ein geplanter Prompt pro Aufgabe, erneutes Planen ersetzt" (dokumentiert in `business-rules.md`), oder erlaubt „jeder einmal erstellte" mehrere parallele Pläne je Aufgabe?
6. **Aufgabenabschluss und -löschung:** Beim Abschluss wird heute storniert (`EntwicklungsprozessService`, Zeile ~438) — persistierter Datensatz dann ebenfalls löschen? Und: Cascade-Löschung des Datensatzes bei physischem Löschen der Aufgabe (DB-Convention wäre `DeleteBehavior.Cascade`)?
7. **Explizites Storno durch den Anwender:** Es gibt heute keinen Storno-Button für einen geplanten Prompt (Storno nur implizit). Soll ein persistierter Plan in der Detailansicht manuell stornierbar sein (und ggf. bearbeitbar)?
8. **Persistenzmodell:** Neue eigene Entity/Tabelle, oder Anknüpfung an `Aufgabe` (z. B. Erweiterung der bestehenden Spalten `VorschlagPrompt`/`VorschlagAusfuehrenAbUtc`, die fachlich aber dem Prompt-Vorschlag dienen)? Zusätzlich zu speichernde Felder (Status, Erstellzeitpunkt, `PromptVorlageId`-Referenz statt nur aufgelöstem Text)?
9. **Zielzeit-Granularität:** Die UI erlaubt nur HH:mm des heutigen Tages (vergangene Zeit ⇒ Sofortversand). Mit Persistenz/App-Neustart wird ein Datumsbezug fachlich relevanter — bleibt die Eingabe auf „heute Uhrzeit" beschränkt?
10. **Sichtbarkeit/Protokoll:** Soll ein versendeter, verfallener oder wiederhergestellter Plan einen `Protokolleintrag` (z. B. `ProtokollTyp.SystemMeldung`) auf der Aufgabe erzeugen — insbesondere wenn der Versand ohne geöffnete Detailansicht im Hintergrund erfolgt?
11. **Anzeige nach Wiederöffnen:** Soll die Detailansicht beim Öffnen den persistierten Plan anzeigen („Prompt in Wartestellung" + Uhrzeit), auch wenn er in einer früheren Sitzung geplant wurde (Annahme: ja)? Gilt das auch für `HasScheduledPrompt`/`⏳` in der aktiven Aufgabenliste über App-Neustarts hinweg?
