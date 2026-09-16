# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan vollständig

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| Pause an `Aufgabe` bis Datum+Uhrzeit einstellbar, Vorbelegung aktueller Zeitpunkt | `Aufgabe.PausiertBisUtc` + Migration `AddAufgabePausiertBisUtc` (`NullableUnixMillisConverter`); `PauseEinstellenCommand` in Ribbon-Gruppe „Aufgabe" → `AufgabePausierenDialog` (Vorbelegung `DateTime.Now`) → `AufgabeService.SetPauseAsync` | `SetPauseAsync_SetztUndLeertPausiertBisUtc`, `AufgabePausierenDialogViewModelTests` (Vorbelegung), E2E `AufgabePausieren_DialogCountdownAbblendungUndAufheben_E2E` | Abgedeckt |
| Seitenleiste „Aktive Aufgaben" zeigt Status „Pausiert" mit Countdown | `PausiertBisUtc`/`IstPausiert` auf `AktiveAufgabePanelItem`, Mapping in `MapAktiveAufgabePanelItem`, neuer Pausiert-Zweig in `KiAusfuehrungsStatusConverter` (`⏸ Pausiert (noch hh\:mm\:ss)`, `d\.hh\:mm\:ss` ab 24 h), Refresh über bestehenden 5-s-`DispatcherTimer` + `NotifyLaufdatenChanged` | `Convert_ShouldReturnPausiertMitCountdown_...` + Rückfall-Test, `MapAktiveAufgabePanelItem_SetztPausiertBisUtc`, E2E assertiert Kachel-Text | Abgedeckt |
| Kachel abgeschwächt (blasser) dargestellt | `Opacity`-`DataTrigger` (0,55) auf `IstPausiert` in beiden Kachel-Templates; `Pausiert:{IstPausiert}`-`HelpText` als E2E-Anker (Muster `Aktiv:{IsAktiv}`) | E2E `..._E2E` prüft `Pausiert:True/False` im HelpText | Abgedeckt |
| Erkanntes Session-Limit wird pro KI-Plugin persistiert | `KiPluginLimitService` schreibt `AppEinstellung`-Schlüssel `plugins.sessionlimit.<KiPluginPrefix>` (ISO-8601-UTC), Ablauf-Semantik (expired ⇒ ignoriert); bewusste Entscheidung gegen `PluginKonfiguration`-Spalte begründet | `VerarbeiteRateLimitAsync_...` (AppEinstellung-Assert), `GetAktiveSessionLimitsAsync_IgnoriertAbgelaufeneUndUngueltigeWerte`, E2E assertiert den Schlüssel | Abgedeckt |
| Limit im `Protokolleintrag` der auslösenden Aufgabe gelistet | Ist-Verhalten bleibt: `ProtokollService.AddCliOutputAsync` legt `ProtokollTyp.RateLimit`-Eintrag an (unverändert) | Bestehende `ProtokollServiceTests`/Integrationstests unverändert gültig; E2E assertiert `RateLimit`-Eintrag an Aufgabe B | Abgedeckt |
| Automatische Pause auf alle Aufgaben mit laufender Ausführung desselben `KiPluginPrefix` | `CliOutputProtokollWriter.PersistLineAsync` → `ProtokollService.TryParseRateLimitMarker` → `KiPluginLimitService.VerarbeiteRateLimitAsync` (nur bei `resetUtc.HasValue`); Peer-Auswahl `IstAktivOderWartend` ∧ `AusfuehrungsStatus.Aktiv` ∧ Prefix-Match `OrdinalIgnoreCase` | `KiPluginLimitServiceTests` (Peers, Fremd-Prefix, beendet, leerer Prefix, vergangenes Limit), Integrationstest `MarkerZeile_PersistiertPluginLimitUndPausiertPeers`, E2E mit zwei Simulator-Aufgaben | Abgedeckt |
| Keine Unterbrechung einer laufenden Ausführung | Explizit: kein Aufruf von `KiAusfuehrungsService`/`CliProcessManager` im Pause-Pfad; Guards blockieren nur neue Starts | E2E assertiert laufende CLI von Aufgabe B (`Stoppen`-Button sichtbar, `AktiveRunId` intakt) | Abgedeckt |
| `CliUpdateSafetyService`: Heartbeat-Toleranz entfällt bei zukünftigem Plugin-Limit | `CheckAsync` sortiert Aufgaben mit Limit > `UtcNow` vor der `AufgabeLaufAktivitaet.IstAktiv`-Bewertung aus; `ICliUpdateSafetyService` signaturstabil; Konstruktor erhält `KiPluginLimitService` | `CheckAsync_SchliesstAufgabenMitZukunftigemLimitAus`, `..._AbgelaufenesLimit...`, `..._OhnePrefix...`; E2E instanziiert `CliUpdateSafetyService` gegen Test-DB (`OpenTestDbContext`-Muster) | Abgedeckt |
| Pause vorzeitig aufhebbar (Offene Frage 8) | „Pause aufheben" im selben Dialog → `SetPauseAsync(id, null)` (idempotent) | `AufhebenCommand_Ergebnis` (Dialog-VM), `SetPauseAsync_SetztUndLeertPausiertBisUtc`, E2E (Aufheben → Normalstatus) | Abgedeckt |
| Pause blockiert Start/Neustart/Recovery/Prompt-Versand (Offene Frage 2 — im Plan als funktionale Blockade entschieden) | Guards in `EntwicklungsprozessService` (`ProzessStartenAsync`, `ProzessStartenUndCliStartenAsync`, `CliNeustartenAsync` inkl. `PluginWechselAsync`-Pfad), `AufgabeRecoveryService` (Scan-Ausschluss + `RecoverManuellAsync`-Ablehnung `ReasonCode=Paused`), `PromptZeitVersandService` (Verschiebung auf Pausenende), CanExecute `!IstPausiert` | `EntwicklungsprozessServiceTests`, `AufgabeRecoveryServiceTests`, `PromptZeitVersandServiceTests` (FakeTimeProvider + Scope-Factory), `TaskDetailViewModelTests` (CanExecute-Matrix) | Abgedeckt |

## Fehlende oder unvollständige Testanforderungen

Keine.

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Manuelle Pause setzen: Ribbon „Pause einstellen" → Dialog mit Vorbelegung → Bestätigen → Kachel „Pausiert" + Countdown + Abblendung → „Pause aufheben" → Normalstatus | `AufgabePausieren_DialogCountdownAbblendungUndAufheben_E2E` (`E2E_AufgabePausieren.cs`, eingebunden in `RunGeneralTests`) | Abgedeckt |
| Session-Limit-Marker in echter CLI-Ausgabe → `RateLimit`-Protokolleintrag + `plugins.sessionlimit.<prefix>` + Pause aller gleich-Prefix-Aufgaben + Kachelanzeige + keine Unterbrechung laufender CLI + Update-Prüfung ohne Heartbeat-Toleranz | `SessionLimit_MarkerPauseProtokollUndUpdateSicherheit_E2E` (`E2E_SessionLimitPause.cs`, eingebunden in `RunConPtyTests`); Update-Prüfung über in-Test instanziierten `CliUpdateSafetyService` gegen die laufende Test-DB | Abgedeckt |
| Update-Sicherheitsdialog als reiner UI-Flow | Nicht als eigener UI-E2E geplant — Update-Button-Flow (Sicherheitsdialog anzeigen/ablehnen) ist unverändert und bereits durch `MainWindowViewModelTests` (`UpdateStartenCommand_ShouldStop_WhenSafetyDialogIsDeclined`) abgedeckt; das geänderte Verhalten ist die service-seitige Bewertung, im E2E gegen die echte App-DB nachgewiesen | Nicht erforderlich mit Begründung |

## Fehlende oder unvollständige Planbestandteile

Keine.

## Hinweise

- Stichproben im Code verifiziert: `OpenTestDbContext` (`WpfTestBase.cs` Z. 220), `RunGeneralTests`/`RunConPtyTests` (`MainTest.cs` Z. 18/51), `IDialogService`/`WpfDialogService.ShowDialogAsync<TResult>`-Muster (`WpfDialogService.cs` Z. 170), `CliOutputProtokollWriter.PersistLineAsync` mit `IServiceScope` (`CliOutputProtokollWriter.cs` Z. 204–210), `plugins.enabled.`-Konvention (`PluginActivationService.cs` Z. 9), `NullableUnixMillisConverter` (`SoftwareschmiededDbContext.cs` Z. 17), `CliUpdateSafetyResult.RiskyTasks` (`UpdateModels.cs` Z. 91), `AddTransient<AutonomAufgabeInitialisierungsDialogViewModel>`-Muster (`App.xaml.cs` Z. 326) — alle plangekonform vorhanden.
- Der Plan spricht von „acht direkten Test-Konstruktionen" des `PromptZeitVersandService`; tatsächlich existieren 9 `new PromptZeitVersandService(`-Stellen im Testprojekt (u. a. `TaskDetailViewModelTests*`, `MainWindowViewModelTests*`). Die Schlussfolgerung (optionaler `IServiceScopeFactory?`-Parameter ⇒ kompilierfähig) bleibt korrekt.
- `PromptZeitVersandService.HandleTimerElapsedAsync` entfernt und disposed den Timer-Eintrag vor `SendPromptAsync`; die geplante „Neu-Terminierung per `timer.Change`" setzt daher implizit ein Wiedereinfügen des `ScheduledPromptEntry` voraus — bei der Umsetzung zu beachten, keine Planlücke.
- `AufgabeService` hat aktuell keine `ProtokollService`-Abhängigkeit; der geplante `SystemMeldung`-Eintrag in `SetPauseAsync` ist direkt über `_db.Protokolleintraege` umsetzbar — keine zusätzliche Konstruktor-Änderung nötig.
- `KiAusfuehrungsServiceTests.AddCliOutputAsync_RateLimitMarkerAusConPtyOutput_ErzeugtRateLimitEintrag` (Z. 632) durchläuft denselben `CliOutputProtokollWriter`-Pfad wie die neue Marker-Verarbeitung; er bleibt dank der geplanten `GetService`-Fehlertoleranz funktionsfähig — eine explizite Nennung in „Betroffene bestehende Tests" wäre konsistent gewesen.
- Die im Plan als Blockade entschiedenen UI-Pfade (deaktivierte Start-/Neustart-/Prompt-Buttons bei Pause) werden per CanExecute-Unit-Tests geprüft; eine Mitprüfung der Button-Sichtbarkeit im bestehenden Pausieren-E2E wäre optional möglich, ist aber nicht gefordert, da die Blockade eine Plan-Designentscheidung (Offene Frage 2) und kein explizites Akzeptanzkriterium ist.
