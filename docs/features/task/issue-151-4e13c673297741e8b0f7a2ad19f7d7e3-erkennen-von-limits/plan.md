# Umsetzungsplan: Erkennen von Limits (Issue #151)

## Übersicht

Reguläre `Aufgabe`-Instanzen erhalten einen pausierbaren Zustand über einen persistierten Pausen-Endzeitpunkt `PausiertBisUtc`. Die Pause ist manuell über einen Dialog in der `TaskDetailView` setzbar (Vorbelegung: aktueller Zeitpunkt) und wird in der Seitenleisten-Kachel als „Pausiert" mit Countdown und abgeschwächter Darstellung angezeigt. Beim Erkennen des Session-Limit-Markers `[[SOFTWARESCHMIEDE_RATE_LIMIT:<ISO8601>]]` in der CLI-Ausgabe wird der Limit-Zeitpunkt pro `KiPluginPrefix` persistiert und die Pause automatisch auf alle Aufgaben mit laufender Ausführung desselben Prefix angewendet — ohne laufende CLI-Prozesse zu unterbrechen. Zusätzlich entfällt in `CliUpdateSafetyService` die Heartbeat-Toleranz für Aufgaben, deren KI-Plugin ein zukünftiges Limit gemeldet hat.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Modellierung „Pausiert" | Nullable Zeitstempel `Aufgabe.PausiertBisUtc` (`DateTimeOffset?`); „Pausiert" ist ein abgeleiteter Anzeigestatus (`PausiertBisUtc > UtcNow`), **kein** neuer `AufgabeStatus`-Wert | `AufgabeStatus.Wartend` bildet den Rate-Limit-Lebenszyklus bereits ab (inkl. Transitions `Gestartet ↔ Wartend`). Ein neuer Enum-Wert müsste in `ValidateStatusTransition`, `AufgabeStatusExtensions.AktivOderWartendStatus`, `StatusIndicatorControl`, `ProjectDetailViewModel` und allen `IstAktivOderWartend`-Prädikaten nachgezogen werden. Die Pause ist fachlich ein zeitgebundener Overlay-Zustand über dem Lebenszyklusstatus — analog `AutonomAufgabeKonfiguration.SessionPauseUtc` und `VorschlagAusfuehrenAbUtc`. Beantwortet Offene Frage 1. |
| Wirkung der Pause | Funktionale Blockade: manueller Start (`ProzessStartenAsync`/`ProzessStartenUndCliStartenAsync`), CLI-Neustart/Wiederherstellung (`CliNeustartenAsync`), Recovery (`AufgabeRecoveryService`) und Versand zeitgesteuerter Prompts (`PromptZeitVersandService`) werden blockiert bzw. verschoben. Laufende CLI-Prozesse werden nie angefasst (kein `StopCliAsync` im Pause-Pfad). | Die Anforderung verlangt explizit „ohne eine noch laufende Ausführung zu unterbrechen"; für alle neu ausgelösten Aktionen ist die Pause dagegen nur dann sinnvoll, wenn sie wirksam blockiert — sonst wäre sie rein dekorativ und das Limit-Ziel (keine weiteren Session-Aufrufe an ein limitiertes Plugin) verfehlt. Beantwortet Offene Frage 2. |
| Pausen-Ende | Rein zeitbasiert: `PausiertBisUtc <= UtcNow` ⇒ Pause inaktiv; der Zeitstempel verbleibt inert in der DB. Kein Auto-Restart der CLI, keine Statusänderung; die Kachel kehrt beim nächsten 5-s-Refresh zum normalen Status zurück. | Es existiert kein automatischer Wiederaufnahme-Mechanismus für reguläre Aufgaben (das „Weitermachen"-Pattern ist an `SessionManagementService`/Autonome Aufgaben gebunden). Ein Auto-Restart wäre eine spekulative Erweiterung über die Anforderung hinaus. Beantwortet Offene Frage 3. |
| Persistenzort Plugin-Limit | `AppEinstellung`-Schlüssel `plugins.sessionlimit.<KiPluginPrefix>` (Wert: ISO-8601-Roundtrip-UTC-Zeitstempel), gekapselt in neuem `KiPluginLimitService`. **Nicht** als Spalte an `PluginKonfiguration`. | Die Tabelle `PluginKonfigurationen` existiert zwar im Modell, wird aber zur Laufzeit nirgends befüllt oder gelesen (nur `DbSet`-Deklaration im `SoftwareschmiededDbContext`, keine Instanziierung außerhalb von Migrations). Ein Laufzeitzustand dort würde erst On-Demand-Zeilen mit unklarer Identitätssemantik (`PluginTyp` vs. `PluginPrefix`) erfordern. `AppEinstellung` mit `plugins.*`-Präfix ist dagegen die etablierte Konvention für plugin-bezogene Laufzeit-/Konfigurationswerte (`PluginActivationService.EnabledKeyPrefix` = `plugins.enabled.`). Spart zudem eine zweite Migration. Abgelaufene Limits werden beim Lesen ignoriert (Rückgabe `null`); der Datensatz bleibt inert stehen. Beantwortet Offene Frage 6. |
| Orchestrierung Marker → Pause | Neuer scoped Service `KiPluginLimitService` (Service Layer), aufgerufen aus `CliOutputProtokollWriter.PersistLineAsync` im dort bereits erzeugten `IServiceScope`. `ProtokollService` bleibt unverändert (reine Protokollierung von `CliOutput` + `RateLimit`). | `CliOutputProtokollWriter` erzeugt pro Ausgabezeile bereits einen Async-Scope für den scoped `ProtokollService`; dieselbe Scope kann `KiPluginLimitService` auflösen. So bleibt `ProtokollService` konstruktor-stabil (kein Bruch in `ProtokollServiceTests`, `TaskDetailViewModelTests`, Integrations-Tests) und die Orchestrierung (Plugin-Limit persistieren + Peer-Aufgaben pausieren) ist klar vom Protokollierungs-Concern getrennt. Der Writer parst die Zeile selbst mit der statischen `ProtokollService.TryParseRateLimitMarker` (reine Funktion, keine Seiteneffekte). |
| Marker ohne gültigen Zeitstempel | Nur der bestehende `ProtokollTyp.RateLimit`-Eintrag (Ist-Verhalten); kein Plugin-Limit, keine Pause. | Ohne Reset-Zeitpunkt existiert kein Pausen-Ende, auf das pausiert werden könnte. `TryParseRateLimitMarker` liefert `true` mit `resetUtc == null` — der Writer ruft `KiPluginLimitService` nur bei `resetUtc.HasValue` auf. |
| Erkennungsweg | Ausschließlich der bestehende Marker `[[SOFTWARESCHMIEDE_RATE_LIMIT:<ISO8601>]]` auf dem Pfad `PseudoConsoleSession` → `CliOutputProtokollWriter` → `ProtokollService.AddCliOutputAsync`. Keine Erweiterung von `IKiPlugin`, keine pluginspezifischen Ausgabemuster. | Der Marker ist bereits der kanonische, plugin-agnostische Erkennungsweg und wird vom `Softwareschmiede.Plugin.KiSimulator` für E2E-Tests deterministisch emittierbar gemacht (Echo über `cmd.exe`). Pluginspezifisches Parsing wäre spekulativ und nicht von der Anforderung gefordert. Beantwortet Offene Frage 5. |
| UI-Einstiegspunkt Pause | Neuer `RibbonLargeButton` „Pause einstellen" (AutomationName `PauseEinstellen`) in der bestehenden Ribbon-Gruppe „Aufgabe" der `TaskDetailView`; modaler Dialog `AufgabePausierenDialog` über `IDialogService.ShowAufgabePausierenDialogAsync` (Muster: `ShowAutonomAufgabeInitialisierungsDialogAsync` + `WpfDialogService.ShowDialogAsync`). Datums-/Zeiteingabe über `DatePicker` + Stunden-/Minuten-Textfelder (Muster: `ScheduledPromptStunde`/`ScheduledPromptMinute`). | Die Ribbon-Gruppe „Aufgabe" enthält bereits alle lebenszyklusbezogenen Aktionen (Speichern/Löschen/Starten/Beenden). Der modale Dialog über `IDialogService` folgt dem projektweiten MVVM-Dialogmuster und ist in E2E-Tests über `WaitForWindow` ansteuerbar. Beantwortet Offene Frage 4. |
| Vorzeitiges Aufheben | Ja — im selben Dialog eine Schaltfläche „Pause aufheben" (nur aktiv, wenn aktuell pausiert), die `PausiertBisUtc` auf `null` setzt. | Die Anforderung fragt explizit danach (Offene Frage 8); ein nicht aufhebbarer Zustand wäre ein Usability-Risiko bei versehentlich gesetzten oder überholten Pausen. `SetPauseAsync(id, null)` deckt beide Fälle ab. |
| Countdown-Darstellung | `KiAusfuehrungsStatusConverter` erhält einen Pausiert-Zweig **vor** allen bestehenden Branches: `⏸ Pausiert (noch hh\:mm\:ss)`, ab ≥ 24 h Format `d\.hh\:mm\:ss`. Refresh über den bestehenden 5-s-`DispatcherTimer` in `MainWindowViewModel`; zusätzlich `AufgabeLaufdatenChangedNotifier.NotifyLaufdatenChanged` beim Setzen/Anwenden für sofortige Anzeige. Abblendung: `Opacity`-`DataTrigger` (Wert 0,55) auf `IstPausiert` in beiden Kachel-Templates; E2E-Nachweis über `AutomationProperties.HelpText`-Erweiterung `Pausiert:{IstPausiert}` am Kachel-`Border`. | Der 5-s-Refresh erzeugt ohnehin neue `AktiveAufgabePanelItem`-Instanzen (`ReplaceAll`), damit tickt der Countdown ohne eigenen Timer. Opacity ist über UIA nicht direkt prüfbar — der HelpText-Marker liefert den deterministischen E2E-Anker (gleiches Muster wie `Aktiv:{IsAktiv}`). Beantwortet Offene Frage 9. |
| Update-Sicherheitsprüfung | `CliUpdateSafetyService.CheckAsync` wertet Aufgaben mit persistiertem **zukünftigem** Plugin-Limit (`plugins.sessionlimit.<prefix>` > `UtcNow`) sofort als nicht blockierend — unabhängig vom Heartbeat-Alter. Aufgaben ohne Prefix, ohne Limit oder mit abgelaufenem Limit behalten die bisherige `AufgabeLaufAktivitaet.IstAktiv`-Bewertung (5-Minuten-Fenster). | Entspricht der Anforderungsinterpretation: Ein bekannt limitiertes Plugin kann die Ausführung bis zum Reset ohnehin nicht fortsetzen; die Aufgabe ist daher kein Update-Risiko, auch wenn ihr letzter Heartbeat frisch ist. `ICliUpdateSafetyService` bleibt signaturstabil. Beantwortet Offene Frage 7. |
| Zeitgesteuerte Prompts bei Pause | Zwei Ebenen: (a) UI — `KannPromptVorlageSenden`/`KannPromptPlanen` liefern `false`, solange die Aufgabe pausiert ist; (b) Laufzeit — `PromptZeitVersandService` prüft beim Timer-Ablauf den aktuellen `PausiertBisUtc`-Wert und verschiebt den Versand auf das Pausenende (Timer wird neu gescharf, Prompt geht nicht verloren). | Ebene (a) verhindert neue Prompt-Aktionen in der Pause; Ebene (b) fängt den Fall ab, dass ein Prompt **vor** dem Setzen der Pause geplant wurde und sein Timer mitten in der Pause abläuft. Verschieben statt Verwerfen erhält die Benutzerabsicht und ist mit `GetScheduledPromptStatus` nachvollziehbar. |

## Programmabläufe

### Manuelle Pause setzen und aufheben

1. Der Anwender öffnet die `TaskDetailView` einer Aufgabe und klickt in der Ribbon-Gruppe „Aufgabe" auf „Pause einstellen" (`PauseEinstellenCommand`; CanExecute: `Status` ∈ {`Neu`, `Gestartet`, `Wartend`} und `!IsAutonomAufgabe` und `!IsLoading`).
2. `TaskDetailViewModel` erzeugt `AufgabePausierenDialogViewModel` (Vorbelegung Datum/Stunde/Minute = `DateTime.Now`; Anzeige des aktuell gesetzten `PausiertBisUtc`, falls vorhanden) und ruft `IDialogService.ShowAufgabePausierenDialogAsync` auf.
3. `WpfDialogService` zeigt den modalen `AufgabePausierenDialog` (`Owner = MainWindow`). Der Dialog validiert die Eingabe (parsebares Datum+Uhrzeit, Ergebnis in der Zukunft) und liefert `AufgabePausierenErgebnis` mit `PausiertBisUtc` oder `Aufheben = true`; Abbrechen liefert `null`.
4. Bei „Pause aufheben" oder Ergebnis `null`-Zeitpunkt ruft das ViewModel `AufgabeService.SetPauseAsync(aufgabeId, null)` auf; sonst `SetPauseAsync(aufgabeId, eingabe.ToUniversalTime())`.
5. `AufgabeService.SetPauseAsync` validiert Status und Zeitpunkt, persistiert `PausiertBisUtc` und schreibt einen `ProtokollTyp.SystemMeldung`-Eintrag („Aufgabe pausiert bis …" / „Pause aufgehoben").
6. `TaskDetailViewModel` aktualisiert seine Properties und ruft `AufgabeLaufdatenChangedNotifier.NotifyLaufdatenChanged(aufgabeId)` auf → `MainWindowViewModel` aktualisiert die Seitenleiste sofort.
7. `KiAusfuehrungsStatusConverter` rendert „⏸ Pausiert (noch …)" und der `Opacity`-`DataTrigger` blendet die Kachel ab; nach Ablauf oder Aufheben erscheint wieder der normale Status.

Beteiligte Klassen/Komponenten: `TaskDetailViewModel`, `AufgabePausierenDialogViewModel`, `AufgabePausierenDialog`, `IDialogService`, `WpfDialogService`, `AufgabeService`, `AufgabeLaufdatenChangedNotifier`, `MainWindowViewModel`, `AktiveAufgabePanelItem`, `KiAusfuehrungsStatusConverter`, `ActiveTasksListControl`.

### Automatische Pause bei Session-Limit-Marker

1. `PseudoConsoleSession` liefert Ausgabe-Chunks an `CliOutputProtokollWriter` (`ITerminalOutputSink`); der zeilenbasierte Persistenz-Loop `PersistLineAsync` erzeugt einen `IServiceScope`.
2. Der Scope löst `ProtokollService` auf; `AddCliOutputAsync` schreibt wie bisher den `CliOutput`-Eintrag und bei Marker-Fund zusätzlich den `ProtokollTyp.RateLimit`-Eintrag für die auslösende Aufgabe (unverändertes Ist-Verhalten, erfüllt „im Protokolleintrag der auslösenden Aufgabe gelistet").
3. Anschließend prüft `PersistLineAsync` die Zeile selbst mit `ProtokollService.TryParseRateLimitMarker`. Nur bei `resetUtc.HasValue` wird `KiPluginLimitService` aus demselben Scope aufgelöst und `VerarbeiteRateLimitAsync(aufgabeId, resetUtc.Value, ct)` aufgerufen; `GetService` statt `GetRequiredService`, damit ein Scope ohne Registrierung (Unit-Tests) fehlertolerant bleibt.
4. `KiPluginLimitService.VerarbeiteRateLimitAsync`:
   - Lädt die auslösende Aufgabe; bei leerem `KiPluginPrefix` wird abgebrochen (kein Plugin-Bezug möglich).
   - Persistiert den Limit-Zeitpunkt via `AppEinstellung` unter `plugins.sessionlimit.<KiPluginPrefix>` (ISO-8601-Roundtrip, UTC).
   - Lädt alle Aufgaben mit `Status.IstAktivOderWartend()` und `AusfuehrungsStatus == Aktiv` und filtert clientseitig mit `string.Equals(prefix, StringComparison.OrdinalIgnoreCase)` auf dasselbe `KiPluginPrefix` (konsistent mit `MainWindowViewModel.ResolvePluginName`).
   - Setzt bei jeder gefundenen Aufgabe `PausiertBisUtc = resetUtc` (überschreibt ggf. eine kürzere manuelle Pause — das extern gemeldete Limit ist harte Bedingung), legt pro pausierter Aufgabe einen `ProtokollTyp.SystemMeldung`-Eintrag an und speichert.
   - Ruft für jede pausierte Aufgabe `AufgabeLaufdatenChangedNotifier.NotifyLaufdatenChanged` auf → sofortige Kachel-Aktualisierung.
   - Ruft **keine** Methode von `KiAusfuehrungsService`/`CliProcessManager` auf → laufende CLI-Prozesse bleiben unangetastet.
5. Bei `resetUtc <= UtcNow` wird der Wert zwar persistiert (berichtetes Limit), aber keine Pause gesetzt — ein bereits abgelaufener Zeitpunkt wäre ohnehin inert.

Beteiligte Klassen/Komponenten: `PseudoConsoleSession`, `CliOutputProtokollWriter`, `ProtokollService`, `KiPluginLimitService`, `AppEinstellungService`/`AppEinstellung`, `Aufgabe`, `Protokolleintrag`, `AufgabeLaufdatenChangedNotifier`.

### Blockade von Start, Neustart, Recovery und Prompt-Versand bei Pause

1. Manueller Start: `StartenCommand` (CanExecute `!IstPausiert`) → `EntwicklungsprozessService.ProzessStartenUndCliStartenAsync` prüft nach dem Laden der Aufgabe `PausiertBisUtc > UtcNow` und wirft `InvalidOperationException` mit fachlicher Meldung; analog `ProzessStartenAsync`.
2. CLI-Neustart/Wiederherstellung: `KannCliNeuStarten` liefert bei Pause `false` (Button ausgeblendet); `EntwicklungsprozessService.CliNeustartenAsync` enthält denselben Guard für programmatische Aufrufer (Plugin-Wechsel-Pfad `PluginWechselAsync` inklusive).
3. Recovery: `AufgabeRecoveryService.ScanForRecoveryCandidatesAsync` schließt Aufgaben mit zukünftigem `PausiertBisUtc` aus (pausierte Aufgaben sind keine „festhängenden" Kandidaten); `RecoverManuellAsync` lehnt bei aktiver Pause mit ReasonCode `Paused` ab.
4. Zeitgesteuerte Prompts: `PromptZeitVersandService` lädt beim Timer-Ablauf (`HandleTimerElapsedAsync`/`SendPromptAsync`) über `IServiceScopeFactory` + `AufgabeService` den aktuellen `PausiertBisUtc`-Wert; bei aktiver Pause wird der Eintrag mit `timer.Change(PausiertBisUtc - now)` neu terminiert statt zu senden.
5. Nach Pausen-Ablauf sind alle Guards wirkungslos; ein auf Pausenende verschobener geplanter Prompt wird dann regulär versendet.

Beteiligte Klassen/Komponenten: `EntwicklungsprozessService`, `TaskDetailViewModel`, `AufgabeRecoveryService`, `PromptZeitVersandService`, `AufgabeService`, `KiAusfuehrungsService` (unverändert — kein Stopp im Pause-Pfad).

### Update-Sicherheitsprüfung mit Plugin-Limit

1. `MainWindowViewModel.UpdateStartenAsync` ruft wie bisher `ICliUpdateSafetyService.CheckAsync` auf (Signatur unverändert).
2. `CliUpdateSafetyService.CheckAsync` lädt `AufgabeService.GetAktiveAufgabenAsync`, sammelt die distinkten nicht-leeren `KiPluginPrefix`-Werte und liest über `KiPluginLimitService.GetAktiveSessionLimitsAsync(prefixes)` in einer Abfrage alle gespeicherten Limits (`AppEinstellungService.GetSettingsAsync`).
3. Pro Aufgabe: Hat ihr Prefix ein Limit > `UtcNow` → nicht blockierend (Heartbeat-Toleranz entfällt komplett, auch frischer Heartbeat zählt nicht). Andernfalls → bisherige Bewertung `AufgabeLaufAktivitaet.IstAktiv(AktiveRunId, LastHeartbeatUtc, now)`.
4. `CliUpdateSafetyResult` enthält nur die tatsächlich riskanten Aufgaben; `MainWindowViewModel` zeigt den Sicherheitsdialog nur noch für diese.

Beteiligte Klassen/Komponenten: `CliUpdateSafetyService`, `KiPluginLimitService`, `AppEinstellungService`, `AufgabeService`, `AufgabeLaufAktivitaet`, `MainWindowViewModel`, `ICliUpdateSafetyService` (unverändert).

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `KiPluginLimitService` | Scoped Service (`Softwareschmiede.Application.Services`) | Service Layer für Plugin-Session-Limits: Konstante `SessionLimitKeyPrefix` (`plugins.sessionlimit.`), `VerarbeiteRateLimitAsync(aufgabeId, resetUtc, ct)` (Limit persistieren + Peer-Pause + Protokolleinträge + Notifier), `GetAktiveSessionLimitsAsync(prefixes, ct)` (Batch-Lesen, expired ⇒ nicht enthalten), `GetAktivesSessionLimitAsync(prefix, ct)` (Einzel-Read für Versand/Guards). Abhängigkeiten: `SoftwareschmiededDbContext`, `AppEinstellungService`, `AufgabeLaufdatenChangedNotifier`, `ILogger`. |
| `AufgabePausierenDialogViewModel` | ViewModel (`Softwareschmiede.App.ViewModels`) | Presentation Model des Pause-Dialogs: `PausiertDatum`, `PausiertStunde`, `PausiertMinute` (vorbelegt mit `DateTime.Now`), `AktuellePauseAnzeige`, `IstAktuellPausiert`, Validierung (`KannBestaetigen`), `BestaetigenCommand`, `AufhebenCommand`; Ergebnis über `Ergebnis`-Property vom Typ `AufgabePausierenErgebnis`. |
| `AufgabePausierenErgebnis` | Record (`Softwareschmiede.App.ViewModels`) | Dialog-Ergebnis: `DateTimeOffset? PausiertBisUtc`, `bool Aufheben`. |
| `AufgabePausierenDialog` | Window (`Softwareschmiede.App.Views`) | Modaler Dialog: `DatePicker` (`PausierenDatum`), Stunden-/Minuten-Textfelder (`PausierenStunde`/`PausierenMinute`), Anzeige der aktuellen Pause, Buttons „Übernehmen" (`PausierenBestaetigen`), „Pause aufheben" (`PausierenAufheben`), „Abbrechen". AutomationNames für E2E-Ansteuerung. |

## Änderungen an bestehenden Klassen

### `Aufgabe` (Datenmodellklasse)

- **Neue Eigenschaften:** `PausiertBisUtc` (`DateTimeOffset?`) — UTC-Zeitpunkt, bis zu dem die Aufgabe pausiert ist; `null` oder Vergangenheit = nicht pausiert.

### `SoftwareschmiededDbContext` (Persistenz)

- **Geänderte Methoden:** `OnModelCreating` — `PausiertBisUtc` im `Aufgabe`-Mapping mit `NullableUnixMillisConverter` registrieren (Konvention wie `VorschlagAusfuehrenAbUtc`, `LastHeartbeatUtc`).

### `AufgabeService` (Service Layer)

- **Neue Methoden:** `SetPauseAsync(Guid aufgabeId, DateTimeOffset? pausiertBisUtc, CancellationToken ct)` — Setzt oder leert (`null`) `PausiertBisUtc`; validiert Existenz, Status (nur `Neu`/`Gestartet`/`Wartend`) und — beim Setzen — Zukünftigkeit des Zeitpunkts; schreibt `ProtokollTyp.SystemMeldung`-Eintrag.

### `CliOutputProtokollWriter` (Output-Senke)

- **Geänderte Methoden:** `PersistLineAsync` — nach `AddCliOutputAsync` Zeile mit `ProtokollService.TryParseRateLimitMarker` prüfen; bei `resetUtc.HasValue` `KiPluginLimitService` via `scope.ServiceProvider.GetService<KiPluginLimitService>()` auflösen und `VerarbeiteRateLimitAsync` aufrufen. Konstruktor bleibt unverändert.

### `CliUpdateSafetyService` (Service Layer)

- **Geänderte Methoden:** `CheckAsync` — Aufgaben mit zukünftigem Plugin-Limit (`KiPluginLimitService.GetAktiveSessionLimitsAsync` über die distinkten `KiPluginPrefix`-Werte) vor der `AufgabeLaufAktivitaet.IstAktiv`-Bewertung aussortieren.
- **Geänderter Konstruktor:** zusätzliche Abhängigkeit `KiPluginLimitService` (beide scoped — kompatibel).

### `EntwicklungsprozessService` (Service Layer)

- **Geänderte Methoden:** `ProzessStartenAsync`, `ProzessStartenUndCliStartenAsync`, `CliNeustartenAsync` — nach dem Laden der Aufgabe Guard `PausiertBisUtc > UtcNow` → `InvalidOperationException` („Aufgabe ist bis … pausiert").

### `AufgabeRecoveryService` (Service Layer)

- **Geänderte Methoden:** `ScanForRecoveryCandidatesAsync` — EF-Filter `a.PausiertBisUtc == null || a.PausiertBisUtc <= now` ergänzen; `RecoverManuellAsync` — Ablehnung bei aktiver Pause (`LogRejected` mit ReasonCode `Paused`, `InvalidOperationException`).

### `PromptZeitVersandService` (Singleton-Service)

- **Geänderter Konstruktor:** zusätzlicher **optionaler** Parameter `IServiceScopeFactory? scopeFactory = null` — bestehende direkte Konstruktionen in Tests bleiben kompilierfähig; DI löst ihn automatisch auf.
- **Geänderte Methoden:** `SendPromptAsync`/`HandleTimerElapsedAsync` — vor dem Versand `PausiertBisUtc` der Aufgabe laden (scoped `AufgabeService`); bei aktiver Pause den `ScheduledPromptEntry` mit `timer.Change(pausiertBisUtc - now, InfiniteTimeSpan)` auf das Pausenende verschieben statt zu senden.

### `TaskDetailViewModel` (ViewModel)

- **Neue Eigenschaften:** `PausiertBisUtc` (`DateTimeOffset?`, aus `_aufgabe`), `IstPausiert` (abgeleitet), `PauseAnzeigeText` (Anzeige im Ribbon-Bereich).
- **Neue Methoden/Commands:** `PauseEinstellenCommand` (`AsyncRelayCommand`, CanExecute `KannPausieren`: `Status` ∈ {`Neu`, `Gestartet`, `Wartend`} ∧ `!IsAutonomAufgabe` ∧ `!IsLoading`) → `PauseEinstellenAsync`: Dialog öffnen, Ergebnis an `AufgabeService.SetPauseAsync` übergeben, `NotifyLaufdatenChanged`.
- **Geänderte CanExecute:** `StartenCommand`, `KannCliNeuStarten`, `KannPromptVorlageSenden`, `KannPromptPlanen` — jeweils `&& !IstPausiert` ergänzen; `PropertyChanged`-Kaskade für `IstPausiert` in `IsCliRunning`-Setter/`LadenAsync` erweitern.

### `IDialogService` / `WpfDialogService` (Gateway)

- **Neue Methoden:** `ShowAufgabePausierenDialogAsync(AufgabePausierenDialogViewModel viewModel, CancellationToken ct)` → `Task<AufgabePausierenErgebnis?>`; `WpfDialogService` nutzt das vorhandene `ShowDialogAsync`-Muster (Factory `AufgabePausierenDialog`, Result-Selektor `viewModel.Ergebnis`).

### `AktiveAufgabePanelItem` (Presentation Model)

- **Neue Eigenschaften:** `PausiertBisUtc` (`DateTimeOffset?`, `init`), `IstPausiert` (`get`-only: `PausiertBisUtc > DateTimeOffset.UtcNow`).

### `MainWindowViewModel` (ViewModel)

- **Geänderte Methoden:** `MapAktiveAufgabePanelItem` — `PausiertBisUtc = aufgabe.PausiertBisUtc` mappen. Kein neuer Timer nötig (5-s-`DispatcherTimer` bleibt der Refresh-Takt).

### `KiAusfuehrungsStatusConverter` (Converter)

- **Geänderte Methoden:** `Convert` — `StatusDaten` um `PausiertBisUtc` erweitern; neuer Zweig **vor** `HasScheduledPrompt`: `PausiertBisUtc > UtcNow` → `⏸ Pausiert (noch {hh\:mm\:ss})` bzw. `{d\.hh\:mm\:ss}` ab ≥ 24 h.

### `ActiveTasksListControl.xaml` (View)

- `AufgabenKachelMitNavigationButtonTemplate` und `AufgabenKachelVollflaechigKlickbarTemplate`: `DataTrigger` auf `IstPausiert == True` → `Setter Opacity = 0.55`; `AutomationProperties.HelpText` des Kachel-`Border` um `Pausiert:{IstPausiert}` erweitern (E2E-Anker für die Abblendung).

### `TaskDetailView.xaml` (View)

- Ribbon-Gruppe „Aufgabe": `RibbonLargeButton` „Pause einstellen" (`AutomationName="PauseEinstellen"`, `ButtonCommand="{Binding PauseEinstellenCommand}"`, `Visibility` an `KannPausieren`).

### `App.xaml.cs` (DI-Composition Root)

- `services.AddScoped<KiPluginLimitService>()` und `services.AddTransient<AufgabePausierenDialogViewModel>()` ergänzen.

## Datenbankmigrationen

| Migrationsname | Betroffene Tabellen/Spalten | Beschreibung der Änderung |
|----------------|----------------------------|---------------------------|
| `AddAufgabePausiertBisUtc` (Zeitstempel-Präfix per `dotnet ef migrations add` generiert) | `Aufgaben`.`PausiertBisUtc` (`long?`, Unix-Millis) | Neue nullable Spalte für den Pausen-Endzeitpunkt. Keine Datenmigration nötig (Default `null`). |

Die Plugin-Limit-Persistenz benötigt **keine** Migration — sie nutzt die bestehende `AppEinstellungen`-Tabelle (Schlüssel `plugins.sessionlimit.<KiPluginPrefix>`).

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| `AufgabePausierenDialogViewModel`-Eingabe | Datum parsebar, Stunde 0–23, Minute 0–59; Kombination muss gültiges Datum/Uhrzeit ergeben | „Übernehmen" deaktiviert (`KannBestaetigen == false`) mit Hinweistext im Dialog |
| Pausen-Endzeitpunkt | Muss nach `DateTimeOffset.UtcNow` liegen (nach UTC-Normalisierung der lokalen Eingabe) | Dialog verweigert Bestätigung; `SetPauseAsync` wirft `InvalidOperationException` als Service-Layer-Absicherung |
| `Aufgabe.Status` beim Pausieren | Nur `Neu`, `Gestartet`, `Wartend` | `SetPauseAsync` wirft `InvalidOperationException` bei `Beendet`/`Archiviert`; `KannPausieren` blendet den Button aus |
| `SetPauseAsync(id, null)` | Leert die Pause (vorzeitiges Aufheben); immer zulässig für pausierbare Status | Kein Fehler — idempotent |
| Marker-Reset-Zeitstempel | Nur `resetUtc.HasValue` führt zu Limit-Persistenz/Pause; `resetUtc <= UtcNow` wird persistiert, aber löst keine Pause aus | Marker ohne Zeitstempel ⇒ nur `RateLimit`-Protokolleintrag |
| `KiPluginPrefix`-Abgleich | `StringComparison.OrdinalIgnoreCase` (konsistent mit `ResolvePluginName`); leerer Prefix der auslösenden Aufgabe ⇒ Abbruch ohne Aktion | Keine Übereinstimmung ⇒ keine Pause |
| Recovery bei Pause | `RecoverManuellAsync` auf pausierte Aufgabe | `InvalidOperationException`, `TaskRecoveryRejected` mit `ReasonCode=Paused` |
| Update-Prüfung | Nur Limits mit `> UtcNow` entfallen die Toleranz; fehlender/ungültiger/abgelaufener `AppEinstellung`-Wert ⇒ normale Heartbeat-Bewertung | Ungültiger gespeicherter Wert wird wie „kein Limit" behandelt |

## Konfigurationsänderungen

| Eintrag | Typ | Standardwert | Zweck |
|---------|-----|--------------|-------|
| `plugins.sessionlimit.<KiPluginPrefix>` in `AppEinstellungen` | Laufzeit-Datensatz (ISO-8601 UTC), kein Benutzer-Setting | nicht gesetzt = kein Limit | Persistiertes Session-Limit-Reset pro KI-Plugin; wird von `KiPluginLimitService` gelesen/geschrieben und bei Ablauf als inaktiv behandelt |

Keine `appsettings`- oder Optionsklassen-Änderungen; kein Feature-Flag erforderlich.

## Seiteneffekte und Risiken

- **`CliUpdateSafetyService`-Konstruktor:** Signaturänderung — `CliUpdateSafetyServiceTests` (2 Konstruktionsstellen) müssen `KiPluginLimitService` mit echtem Test-`DbContext` übergeben. `MainWindowViewModelTests` mocken `ICliUpdateSafetyService` (Interface unverändert) — unberührt.
- **`PromptZeitVersandService`-Konstruktor:** Neuer Parameter als optionaler `IServiceScopeFactory?` — die acht direkten Test-Konstruktionen bleiben kompilierfähig; nur neue Pausen-Tests übergeben eine echte Scope-Factory.
- **`CliOutputProtokollWriter`:** Neue zweite Service-Auflösung pro Marker-Zeile; via `GetService` fehlertolerant, damit `CliOutputProtokollWriterTests` mit gemockter `IServiceScopeFactory` ohne `KiPluginLimitService`-Registrierung weiterlaufen.
- **`AufgabeRecoveryService`:** Pausierte Aufgaben verschwinden aus den Recovery-Kandidaten — gewollt; bei Pausenende und weiterhin abgelaufenem Heartbeat tauchen sie wieder auf.
- **Start-/Neustart-Guards:** `InvalidOperationException` bei pausierter Aufgabe wird in `TaskDetailViewModel` als `FehlerMeldung` sichtbar; gleiches Guard-Verhalten wie bei den bestehenden „kein Klonpfad"/„beendet"-Fehlern.
- **Autonome Aufgaben:** Unberührt — sie haben mit `AutonomAufgabeKonfiguration.SessionPauseUtc` einen eigenen Mechanismus; der Pause-Button ist für `IsAutonomAufgabe` deaktiviert.
- **Aufgaben ohne Pause, aber gleichem `KiPluginPrefix`:** Das Plugin-Limit pausiert nur Aufgaben, deren Ausführung zum Erkennungszeitpunkt läuft; ein später manuell gestarteter Task mit demselben Prefix wird nicht automatisch geblockt (Anforderungsgrenze — bewusst kein globaler Plugin-Startbann).
- **Mehrfach-Marker:** Erneuter Marker setzt `PausiertBisUtc`/`plugins.sessionlimit.*` auf den neuen Wert — letzter Stand gewinnt.
- **EF-Migration auf Test-DB:** `SOFTWARESCHMIEDE_TEST_DB_PATH`-Datenbanken werden über `MigrateAsync` beim App-Start migriert — E2E-Tests benötigen keinen separaten Schema-Schritt.

## Umsetzungsreihenfolge

1. **`Aufgabe.PausiertBisUtc` + DbContext-Mapping + Migration**
   - Voraussetzungen: Keine (Konventionen `NullableUnixMillisConverter`, `dotnet ef` sind vorhanden).
   - Beschreibung: Property an `Aufgabe`, Mapping in `SoftwareschmiededDbContext.OnModelCreating`, Migration `AddAufgabePausiertBisUtc` erzeugen.
2. **`AufgabeService.SetPauseAsync`**
   - Voraussetzungen: Schritt 1.
   - Beschreibung: Setzen/Leeren mit Status- und Zeit-Validierung sowie `SystemMeldung`-Protokolleintrag.
3. **`KiPluginLimitService`**
   - Voraussetzungen: Schritt 1 (schreibt `PausiertBisUtc`); `AppEinstellungService` und `AufgabeLaufdatenChangedNotifier` existieren bereits.
   - Beschreibung: Schlüsselkonstante, `VerarbeiteRateLimitAsync`, `GetAktiveSessionLimitsAsync`, `GetAktivesSessionLimitAsync`; DI-Registrierung in `App.xaml.cs`.
4. **`CliOutputProtokollWriter`-Anbindung**
   - Voraussetzungen: Schritt 3.
   - Beschreibung: Marker-Prüfung und `KiPluginLimitService`-Aufruf in `PersistLineAsync`.
5. **Blockade-Pfade: `EntwicklungsprozessService`, `AufgabeRecoveryService`, `PromptZeitVersandService`**
   - Voraussetzungen: Schritt 1 (Feld), Schritt 2 (`SetPauseAsync`-Semantik); `PromptZeitVersandService` nutzt `IServiceScopeFactory` + `AufgabeService`.
   - Beschreibung: Guards in den drei Startmethoden, Recovery-Ausschluss/-Ablehnung, Versand-Verschiebung auf Pausenende.
6. **`CliUpdateSafetyService`-Anpassung**
   - Voraussetzungen: Schritt 3 (`GetAktiveSessionLimitsAsync`).
   - Beschreibung: Konstruktor-Abhängigkeit und Limit-Filter in `CheckAsync`.
7. **Seitenleiste: `AktiveAufgabePanelItem`, `MainWindowViewModel`-Mapping, `KiAusfuehrungsStatusConverter`, `ActiveTasksListControl.xaml`**
   - Voraussetzungen: Schritt 1.
   - Beschreibung: `PausiertBisUtc`/`IstPausiert` durchreichen, Pausiert-Zweig mit Countdown, `Opacity`-Trigger + `Pausiert:`-HelpText.
8. **Pause-Dialog: `AufgabePausierenDialogViewModel`, `AufgabePausierenErgebnis`, `AufgabePausierenDialog`, `IDialogService`/`WpfDialogService`**
   - Voraussetzungen: Schritt 2 (`SetPauseAsync`); Dialog-Muster (`IDialogService`, `WpfDialogService.ShowDialogAsync`) vorhanden.
   - Beschreibung: ViewModel mit Vorbelegung/Validierung/Aufheben, modale View mit AutomationNames, Service-Methode.
9. **`TaskDetailViewModel` + `TaskDetailView.xaml`**
   - Voraussetzungen: Schritte 2, 8.
   - Beschreibung: `PauseEinstellenCommand`, `KannPausieren`, `IstPausiert`-Property, CanExecute-Erweiterungen, Ribbon-Button.
10. **Unit-/Integrationstests**
    - Voraussetzungen: Schritte 1–9.
    - Beschreibung: Neue Testfälle gemäß Abschnitt „Tests"; betroffene bestehende Tests anpassen.
11. **E2E-Tests**
    - Voraussetzungen: Schritte 1–10; E2E-Infrastruktur (`WpfTestBase`, `MenuView`, `SimulatedPseudoConsoleProcessLauncher`, `Softwareschmiede.Plugin.KiSimulator`) vorhanden.
    - Beschreibung: Zwei neue konsolidierte E2E-Methoden in `End2EndTest`-Partial-Dateien, Eingliederung in `RunGeneralTests` bzw. `RunConPtyTests`.

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `SetPauseAsync_SetztUndLeertPausiertBisUtc` | `AufgabeServiceTests` | Setzen, Leeren (`null`), Persistenz, `SystemMeldung`-Einträge |
| `SetPauseAsync_Wirft_WhenZeitpunktInVergangenheit` / `..._WhenStatusBeendetOderArchiviert` | `AufgabeServiceTests` | Validierungsregeln der Service Layer |
| `VerarbeiteRateLimitAsync_PausiertAlleAktivenAufgabenGleichenPrefix` | `KiPluginLimitServiceTests` (neu) | `PausiertBisUtc` auf auslösender + Peer-Aufgaben (gleiches Prefix, `IstAktivOderWartend` ∧ `AusfuehrungsStatus.Aktiv`), OrdinalIgnoreCase-Match, `AppEinstellung`-Schlüssel, `SystemMeldung`-Einträge |
| `VerarbeiteRateLimitAsync_LaesstFremdePrefixeUndBeendeteUnveraendert` / `..._OhnePrefixKeineAktion` / `..._VergangenesLimitKeinePause` | `KiPluginLimitServiceTests` | Grenzfälle der automatischen Pause |
| `GetAktiveSessionLimitsAsync_IgnoriertAbgelaufeneUndUngueltigeWerte` | `KiPluginLimitServiceTests` | Ablauf-Semantik und Robustheit |
| `CheckAsync_SchliesstAufgabenMitZukunftigemLimitAus` / `..._AbgelaufenesLimitBeharltHeartbeatBewertung` / `..._OhnePrefixUnveraendert` | `CliUpdateSafetyServiceTests` | Limit-Filter, Ablauf, Prefix-lose Aufgaben (Konstruktor-Aufrufe auf `KiPluginLimitService` umstellen) |
| `ScanForRecoveryCandidates_SchliesstPausierteAus` / `RecoverManuellAsync_Wirft_WhenPausiert` | `AufgabeRecoveryServiceTests` | Recovery-Blockade bei aktiver Pause |
| `HandleTimerElapsed_VerschiebtVersandAufPausenende` / `..._VersendetNachPausenende` | `PromptZeitVersandServiceTests` | Re-Scheduling mit `FakeTimeProvider` + Scope-Factory gegen Test-DB |
| `CliNeustartenAsync_Wirft_WhenPausiert` / `ProzessStartenUndCliStartenAsync_Wirft_WhenPausiert` | `EntwicklungsprozessServiceTests` | Start-Guards |
| `Convert_ShouldReturnPausiertMitCountdown_WhenPausiertBisUtcZukunftig` / `..._FaelltZurueckAufNormalstatus_NachAblauf` / Panel-Item-Variante | `KiAusfuehrungsStatusConverterTests` | Countdown-Text, Format, Vorrang vor „Prompt in Wartestellung"/„Läuft" |
| `KannPausieren_...` / `StartenCommand_KannNicht_WhenPausiert` / `KannPromptVorlageSenden_IstFalse_WhenPausiert` | `TaskDetailViewModelTests` | CanExecute-Matrix der Pause |
| `MapAktiveAufgabePanelItem_SetztPausiertBisUtc` / `IstPausiert_Abgeleitet` | `MainWindowViewModelTests` / `AktiveAufgabePanelItem`-nahe Tests | Mapping und abgeleiteter Zustand |
| `KannBestaetigen_...` / Vorbelegung ≈ `DateTime.Now` / `AufhebenCommand_Ergebnis` | `AufgabePausierenDialogViewModelTests` (neu) | Dialog-Validierung, Vorbelegung, Aufheben-Ergebnis |
| `MarkerZeile_PersistiertPluginLimitUndPausiertPeers` | `ServiceIntegration`-Tests (neu, z. B. `KiPluginLimitIntegrationTests`) | Ende-zu-Ende über `CliOutputProtokollWriter` mit echtem `ServiceCollection`-Scope: Markerzeile → `RateLimit`-Eintrag + `AppEinstellung` + `PausiertBisUtc` |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `CliUpdateSafetyServiceTests` | Konstruktor erhält `KiPluginLimitService` — beide `new CliUpdateSafetyService(...)`-Stellen umstellen |
| `KiAusfuehrungsStatusConverterTests` | `StatusDaten` intern erweitert; bestehende Erwartungen bleiben gültig (Pausiert-Zweig nur bei gesetztem Feld), ggf. Fixture-Aufgaben bekommen neues Feld (Default `null` → keine Anpassung der Asserts nötig) |
| `CliOutputProtokollWriterTests` | `PersistLineAsync` ruft bei Markerzeilen `GetService<KiPluginLimitService>` auf — Scope-Mock muss `null` liefern (Standard) oder Registrierung ergänzen; Marker-freie Tests unverändert |
| `TaskDetailViewModelTests` (diverse Partial-Dateien) | Falls Fixtures pausierte Aufgaben simulieren sollen, neue Properties nutzen; bestehende Asserts bleiben gültig (`PausiertBisUtc` Default `null`) |

### E2E-Tests (primärer Funktionsnachweis)

Konsolidierung gemäß Projektregel: **zwei** neue Testmethoden in `End2EndTest`-Partial-Dateien, die alle Szenarien gegen je eine gemeinsame App-Instanz verketten — keine Methode pro Aspekt.

| Priorität | Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium | Warum E2E nötig ist |
|-----------|----------|------------------------|-------------------------------|-------------------|
| Pflicht | `AufgabePausieren_DialogCountdownAbblendungUndAufheben_E2E` — Aufgabe per `OpenTestDbContext` auf `Gestartet`/`Aktiv` setzen → Ribbon „Pause einstellen" → Dialog öffnet mit Vorbelegung ≈ aktueller Zeitpunkt → Zeitpunkt +2 h bestätigen → Seitenleisten-Kachel zeigt „⏸ Pausiert (noch …)" und `Pausiert:True` im HelpText (Abblendung) → Dialog erneut öffnen → „Pause aufheben" → Kachel zeigt Normalstatus und `Pausiert:False` | `End2EndTest` (neue Partial-Datei, z. B. `E2E_AufgabePausieren.cs`), aufgerufen aus `RunGeneralTests` | Manuelles Setzen inkl. Vorbelegung, Persistenz des Pausen-Endzeitpunkts, „Pausiert"-Anzeige mit Countdown, abgeschwächte Kachel, vorzeitiges Aufheben | Vollständiger Anwenderfluss über UI (Dialog, Ribbon, Kachel) — Countdown-Rendering und Abblendung sind nur über die laufende WPF-Oberfläche nachweisbar |
| Pflicht | `SessionLimit_MarkerPauseProtokollUndUpdateSicherheit_E2E` — zwei Aufgaben mit `Softwareschmiede.KiSimulator` über UI starten → `PromptVorlage` `echo [[SOFTWARESCHMIEDE_RATE_LIMIT:<ISO>]]` per `OpenTestDbContext` seeden und an Aufgabe B senden → Asserts: `RateLimit`-Protokolleintrag an B, `AppEinstellung` `plugins.sessionlimit.Softwareschmiede.KiSimulator` gesetzt, `PausiertBisUtc` auf beiden Aufgaben, beide Kacheln „Pausiert", CLI von B läuft weiter (`Stoppen`-Button sichtbar, `AktiveRunId` intakt — kein Unterbrechen) → dritte Aufgabe mit anderem Prefix + frischem Heartbeat seeden → in-Test instanziiertem `CliUpdateSafetyService` (`OpenTestDbContext`, Muster `AutonomAufgabeAgentExecution_..._E2E`): pausierte Limit-Aufgaben trotz frischem Heartbeat **nicht** in `RiskyTasks`, Fremd-Prefix-Aufgabe weiterhin riskant; nach Setzen eines abgelaufenen Limits wieder riskant | `End2EndTest` (neue Partial-Datei, z. B. `E2E_SessionLimitPause.cs`), aufgerufen aus `RunConPtyTests` | Marker-Erkennung auf dem echten CLI-Ausgabepfad, Plugin-weite Persistenz, automatische Pause aller gleich-Prefix-Aufgaben, kein Unterbrechen laufender Ausführung, Update-Prüfung ohne Heartbeat-Toleranz bei zukünftigem Limit | Nur der echte Pfad `PseudoConsoleSession` → `CliOutputProtokollWriter` → Scope → `ProtokollService`/`KiPluginLimitService` beweist die Verkettung inkl. DI-Scope-Auflösung und Seitenleisten-Aktualisierung im laufenden App-Prozess |

Welche bestehenden E2E-Tests müssen angepasst werden?

Keine — die neuen Szenarien ergänzen die bestehenden Bündel; kein bestehender E2E-Test setzt/erwartet pausierungsbezogenes Verhalten.

## Offene Punkte

Keine — alle neun Anforderungsfragen wurden anhand von Anforderung, Bestandsaufnahme und etablierten Projektkonventionen zu konkreten Designentscheidungen aufgelöst (siehe Tabelle „Designentscheidungen").
