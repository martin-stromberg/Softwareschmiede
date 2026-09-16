# Tasks: Erkennen von Limits (Issue #151)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Datenmodell | `Aufgabe` um Property `PausiertBisUtc` (`DateTimeOffset?`) erweitern | Offen | — |
| 2 | Datenmodell | `SoftwareschmiededDbContext.OnModelCreating`: `PausiertBisUtc` mit `NullableUnixMillisConverter` mappen | Offen | — |
| 3 | Persistenz | EF-Core-Migration `AddAufgabePausiertBisUtc` erzeugen (Spalte `Aufgaben.PausiertBisUtc`, `long?`) | Offen | — |
| 4 | Logik | `AufgabeService.SetPauseAsync(aufgabeId, pausiertBisUtc, ct)` implementieren (Setzen/Leeren, Status- und Zeitvalidierung, `ProtokollTyp.SystemMeldung`-Eintrag) | Offen | — |
| 5 | Logik | `KiPluginLimitService` anlegen: Schlüsselkonstante `plugins.sessionlimit.`, `GetAktiveSessionLimitsAsync`/`GetAktivesSessionLimitAsync` mit Ablauf-Prüfung | Offen | — |
| 6 | Logik | `KiPluginLimitService.VerarbeiteRateLimitAsync` implementieren: Limit pro `KiPluginPrefix` in `AppEinstellung` persistieren, aktive Aufgaben gleichen Prefix (`OrdinalIgnoreCase`, `IstAktivOderWartend` ∧ `AusfuehrungsStatus.Aktiv`) pausieren, `SystemMeldung`-Einträge, `AufgabeLaufdatenChangedNotifier` aufrufen | Offen | — |
| 7 | Logik | `CliOutputProtokollWriter.PersistLineAsync`: Marker via `ProtokollService.TryParseRateLimitMarker` prüfen und bei `resetUtc.HasValue` `KiPluginLimitService` aus dem Zeilen-Scope aufrufen | Offen | — |
| 8 | Logik | `EntwicklungsprozessService`: Pause-Guard (`PausiertBisUtc > UtcNow` → `InvalidOperationException`) in `ProzessStartenAsync`, `ProzessStartenUndCliStartenAsync`, `CliNeustartenAsync` | Offen | — |
| 9 | Logik | `AufgabeRecoveryService`: pausierte Aufgaben in `ScanForRecoveryCandidatesAsync` ausschließen und `RecoverManuellAsync` mit ReasonCode `Paused` ablehnen | Offen | — |
| 10 | Logik | `PromptZeitVersandService`: optionalen `IServiceScopeFactory`-Parameter ergänzen und beim Timer-Ablauf `PausiertBisUtc` prüfen — bei Pause Versand auf Pausenende verschieben | Offen | — |
| 11 | Update-Sicherheit | `CliUpdateSafetyService`: `KiPluginLimitService`-Abhängigkeit ergänzen und in `CheckAsync` Aufgaben mit zukünftigem Plugin-Limit vor der `AufgabeLaufAktivitaet.IstAktiv`-Bewertung aussortieren | Offen | — |
| 12 | UI | `AktiveAufgabePanelItem`: `PausiertBisUtc` (init) und abgeleitetes `IstPausiert` ergänzen | Offen | — |
| 13 | UI | `MainWindowViewModel.MapAktiveAufgabePanelItem`: `PausiertBisUtc` mappen | Offen | — |
| 14 | UI | `KiAusfuehrungsStatusConverter`: `StatusDaten` um `PausiertBisUtc` erweitern und Pausiert-Zweig mit Countdown-Format (`hh\:mm\:ss` / `d\.hh\:mm\:ss`) vor allen bestehenden Branches einfügen | Offen | — |
| 15 | UI | `ActiveTasksListControl.xaml`: `Opacity`-`DataTrigger` (0,55) auf `IstPausiert` in beiden Kachel-Templates + HelpText-Erweiterung `Pausiert:{IstPausiert}` | Offen | — |
| 16 | Dialog | `AufgabePausierenErgebnis`-Record und `AufgabePausierenDialogViewModel` anlegen (Vorbelegung `DateTime.Now`, Anzeige aktuelle Pause, `KannBestaetigen`-Validierung, `BestaetigenCommand`, `AufhebenCommand`) | Offen | — |
| 17 | Dialog | `AufgabePausierenDialog` (Window) anlegen: `DatePicker` + Stunden-/Minuten-Felder + Buttons „Übernehmen"/„Pause aufheben"/„Abbrechen" mit AutomationNames | Offen | — |
| 18 | Dialog | `IDialogService` um `ShowAufgabePausierenDialogAsync` erweitern und in `WpfDialogService` über das `ShowDialogAsync`-Muster implementieren | Offen | — |
| 19 | UI | `TaskDetailViewModel`: `PausiertBisUtc`/`IstPausiert`/`PauseAnzeigeText`, `PauseEinstellenCommand` (CanExecute `KannPausieren`) und `!IstPausiert`-Guards in `StartenCommand`, `KannCliNeuStarten`, `KannPromptVorlageSenden`, `KannPromptPlanen` | Offen | — |
| 20 | UI | `TaskDetailView.xaml`: `RibbonLargeButton` „Pause einstellen" (`AutomationName="PauseEinstellen"`) in Ribbon-Gruppe „Aufgabe" einfügen | Offen | — |
| 21 | Dependency Injection | `App.xaml.cs`: `KiPluginLimitService` als scoped und `AufgabePausierenDialogViewModel` als transient registrieren | Offen | — |
| 22 | Tests | `AufgabeServiceTests`: `SetPauseAsync`-Tests (Setzen, Leeren, Vergangenheits-/Status-Validierung, Protokolleintrag) | Offen | — |
| 23 | Tests | `KiPluginLimitServiceTests` (neu): Peer-Pausierung gleicher Prefix, Fremd-Prefix/Beendet/Ohne-Prefix/vergangenes Limit unverändert, `AppEinstellung`-Persistenz, Ablauf-Filter in `GetAktiveSessionLimitsAsync` | Offen | — |
| 24 | Tests | `CliUpdateSafetyServiceTests`: Konstruktoraufrufe auf `KiPluginLimitService` umstellen und Fälle zukünftiges/abgelaufenes/fehlendes Limit sowie prefix-lose Aufgaben ergänzen | Offen | — |
| 25 | Tests | `AufgabeRecoveryServiceTests`: Ausschluss pausierter Kandidaten und `RecoverManuellAsync`-Ablehnung bei Pause | Offen | — |
| 26 | Tests | `PromptZeitVersandServiceTests`: Versand-Verschiebung auf Pausenende und Versand nach Pausenende (Scope-Factory gegen Test-DB) | Offen | — |
| 27 | Tests | `EntwicklungsprozessServiceTests`: `InvalidOperationException` bei Start/Neustart pausierter Aufgaben | Offen | — |
| 28 | Tests | `KiAusfuehrungsStatusConverterTests`: Pausiert-Countdown für `Aufgabe` und `AktiveAufgabePanelItem`, Rückfall auf Normalstatus nach Ablauf, Vorrang vor anderen Branches | Offen | — |
| 29 | Tests | `TaskDetailViewModelTests`: `KannPausieren`-Matrix, `!IstPausiert`-Wirkung auf `StartenCommand`/`KannCliNeuStarten`/`KannPromptVorlageSenden`/`KannPromptPlanen` | Offen | — |
| 30 | Tests | `MainWindowViewModelTests`: Mapping von `PausiertBisUtc`/`IstPausiert` in `MapAktiveAufgabePanelItem` | Offen | — |
| 31 | Tests | `AufgabePausierenDialogViewModelTests` (neu): Vorbelegung, `KannBestaetigen`-Validierung, Aufheben-Ergebnis | Offen | — |
| 32 | Tests | Integrationstest (neu, `ServiceIntegration`): Markerzeile über `CliOutputProtokollWriter` mit echtem `ServiceCollection`-Scope → `RateLimit`-Eintrag + `plugins.sessionlimit.*`-Eintrag + `PausiertBisUtc` auf Peer-Aufgaben | Offen | — |
| 33 | E2E-Tests | `AufgabePausieren_DialogCountdownAbblendungUndAufheben_E2E` in neuer `End2EndTest`-Partial-Datei: Dialog-Vorbelegung, Pause setzen, Kachel „Pausiert" + Countdown + `Pausiert:True`-Abblendung, Aufheben → Normalstatus; Eingliederung in `RunGeneralTests` | Offen | — |
| 34 | E2E-Tests | `SessionLimit_MarkerPauseProtokollUndUpdateSicherheit_E2E` in neuer `End2EndTest`-Partial-Datei: zwei KiSimulator-Aufgaben starten, Marker per `PromptVorlage`-Echo emittieren, `RateLimit`-Eintrag + `AppEinstellung` + `PausiertBisUtc` prüfen, laufende CLI nicht unterbrochen, `CliUpdateSafetyService.CheckAsync` ohne Heartbeat-Toleranz bei Limit; Eingliederung in `RunConPtyTests` | Offen | — |
