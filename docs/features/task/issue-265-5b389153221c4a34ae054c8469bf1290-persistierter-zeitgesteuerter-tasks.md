# Tasks: Persistierter zeitgesteuerter Prompt (Issue #265)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Datenmodell | Entity `ZeitgesteuerterPrompt` unter `src/Softwareschmiede/Domain/Entities/` anlegen (`Id`, `AufgabeId`, `PromptText`, `ZielZeitpunktUtc`, `ErstelltAmUtc`, `AktualisiertAmUtc`) | Offen | — |
| 2 | Datenmodell | `DbSet<ZeitgesteuerterPrompt> ZeitgesteuertePrompts` + `OnModelCreating`-Mapping in `SoftwareschmiededDbContext` ergänzen (UnixMillisConverter, Unique-Index auf `AufgabeId`, FK `DeleteBehavior.Cascade`) | Offen | — |
| 3 | Datenmodell | EF-Migration `AddZeitgesteuerterPrompt` unter `src/Softwareschmiede/Migrations/` erstellen | Offen | — |
| 4 | Persistenz | Scoped Service `ZeitgesteuerterPromptStore` anlegen (`UpsertAsync`, `LoeschenAsync`, `GetAlleAsync`, `UpdateZielZeitpunktAsync`) und in `App.xaml.cs` per `AddScoped` registrieren | Offen | — |
| 5 | Logik | `PromptZeitVersandService.SchedulePromptAsync`: bei zukünftiger Zielzeit Datensatz per `UpsertAsync` schreiben, bevor der Timer armiert wird | Offen | — |
| 6 | Logik | `CancelScheduledPrompt` in `CancelScheduledPromptAsync` (async) umbenennen und Datensatzlöschung ergänzen; alle Aufrufer anpassen | Offen | — |
| 7 | Logik | `ScheduledPromptEntry` um Zustand „wartet auf Session" erweitern (Flag + entsorgter/nullabler Timer) | Offen | — |
| 8 | Logik | `HandleTimerElapsedAsync`/`SendPromptAsync`: Datensatz nur bei erfolgreichem Versand löschen; ohne Session **oder** bei Sendefehler (`ObjectDisposedException`/sonstiger `WritePromptAsync`-Fehler) Plan in „wartet auf Session" überführen und beim nächsten `CliProcessStatusChanged(Gestartet)` erneut versuchen; Pause-Zweig aktualisiert `ZielZeitpunktUtc` im Datensatz | Offen | — |
| 8a | Logik | `PromptZeitVersandService.SchreibeVersandProtokollAsync`: `Protokolleintrag` (`ProtokollTyp.SystemMeldung`) via scoped `ProtokollService` — bei Nachzustellung über `CliProcessStatusChanged(Gestartet)` immer, bei Timer-/Sofortversand nur ohne `PromptSent`-Abonnenten; Fehlschlag (z. B. gelöschte Aufgabe) nur loggen | Offen | — |
| 9 | Logik | `PromptZeitVersandService`: Event `ScheduledPromptChanged` (`Action<Guid>`) ergänzen und bei Planen/Storno/Versand/Zustandswechsel feuern | Offen | — |
| 10 | Logik | `PromptZeitVersandService`: `CliProcessStatusChanged` abonnieren; bei `Gestartet` wartende/fällige Pläne zustellen | Offen | — |
| 11 | Logik | `PromptZeitVersandService.RestoreScheduledPromptsAsync` implementieren (Datensätze laden, verwaiste löschen, Pause-Vorlage, zukünftige Timer armieren, überfällige auf „wartet auf Session" setzen) | Offen | — |
| 12 | Anwendung | `App.OnStartup`: `RestoreScheduledPromptsAsync` nach `db.Database.MigrateAsync()` aufrufen (fehlertolerant mit Logging) | Offen | — |
| 13 | UI | `TaskDetailViewModel.Dispose`: `CancelScheduledPrompt`-Aufruf entfernen; `ScheduledPromptChanged`-Abo lösen | Offen | — |
| 14 | UI | `TaskDetailViewModel.OnCliProcessStatusChanged`: `CancelScheduledPrompt`-Aufruf und Leeren der Statusanzeige entfernen | Offen | — |
| 15 | UI | `TaskDetailViewModel.LadenAsync`: `ScheduledPromptStatus`/`ScheduledPromptTimeDisplay` aus `GetScheduledPromptStatus` befüllen | Offen | — |
| 16 | UI | `TaskDetailViewModel`: Handler `OnScheduledPromptChanged` + Abonnement ergänzen (Anzeige aus `GetScheduledPromptStatus` neu befüllen/leeren) | Offen | — |
| 16a | UI | `TaskDetailViewModel`: `CancelScheduledPromptCommand` (`AsyncRelayCommand`) ergänzen → `CancelScheduledPromptAsync`; Anzeige-Leerung über `OnScheduledPromptChanged` | Offen | — |
| 16b | UI | `TaskDetailView.xaml`: Button „Wartestellung aufheben" (`AutomationProperties.Name="WartestellungAufheben"`, `Command="{Binding CancelScheduledPromptCommand}"`) im `ScheduledPromptStatus`-StackPanel ergänzen (erbt Sichtbarkeit via `NullOrEmptyToVisibilityConverter`) | Offen | — |
| 17 | Logik | `EntwicklungsprozessService.AbschliessenCoordinatedAsync`: `await CancelScheduledPromptAsync` statt `CancelScheduledPrompt` | Offen | — |
| 18 | Logik | `PromptZeitVersandService`-Klassenkommentar aktualisieren (Persistierung statt „keine Persistierung") | Offen | — |
| 19 | Tests | Test-Hilfsmethode `CreateStoreProviderAsync` in `PromptZeitVersandServiceTests` (InMemory-DbContext + `ZeitgesteuerterPromptStore` + `AufgabeService` + `ProtokollService` via `ServiceProvider`) | Offen | — |
| 20 | Tests | Neu: `SchedulePromptAsync_SchreibtDatensatz` | Offen | — |
| 21 | Tests | Neu: `SchedulePromptAsync_ErneutesPlanen_ErsetztDatensatz` | Offen | — |
| 22 | Tests | Neu: `Versand_LoeschtDatensatz` | Offen | — |
| 23 | Tests | Neu: `CancelScheduledPromptAsync_LoeschtDatensatz` | Offen | — |
| 24 | Tests | Anpassen: `Timer_OhneSession_VerwirftPromptStill` → Plan bleibt bestehen / wartet auf Session | Offen | — |
| 25 | Tests | Neu: `CliGestartet_MitWartendemPlan_ZustelltNach` | Offen | — |
| 25a | Tests | Neu: `Sendefehler_BehaeltPlanUndWartetAufSession` (sonstiger `WritePromptAsync`-Fehler → Plan bleibt, Retry beim nächsten `Gestartet`) | Offen | — |
| 25b | Tests | Neu: `Nachzustellung_SchreibtSystemMeldung` (Protokolleintrag bei Zustellung über `CliProcessStatusChanged(Gestartet)`) | Offen | — |
| 25c | Tests | Neu: `Versand_OhneAbonnent_SchreibtSystemMeldung` | Offen | — |
| 25d | Tests | Neu: `Versand_MitAbonnent_KeinProtokollEintrag` | Offen | — |
| 25e | Tests | Neu: `ProtokollEintrag_BeiGeloeschterAufgabe_BrichtVersandNicht` | Offen | — |
| 26 | Tests | Neu: `Pause_Verschiebung_AktualisiertDatensatz` | Offen | — |
| 27 | Tests | Neu: `Restore_ZukuenftigerPlan_ArmiertTimer` | Offen | — |
| 28 | Tests | Neu: `Restore_UeberfaelligerPlan_WartetAufSession` | Offen | — |
| 29 | Tests | Neu: `Restore_VerwaisterDatensatz_WirdGeloescht` | Offen | — |
| 30 | Tests | Neu: `Restore_NachNeustart_LiefertBeiSessionStartZu` (zweite Service-Instanz gegen dieselbe DB) | Offen | — |
| 31 | Tests | Anpassen: `Dispose_StorniertGeplantePrompts` → Dispose lässt Plan bestehen (`TaskDetailViewModelTests_ZeitgesteuerterPrompt`) | Offen | — |
| 32 | Tests | Anpassen: `OnCliProcessStatusChanged_CliGestoppt_StorniertGeplantenPrompt` → Plan bleibt bestehen | Offen | — |
| 33 | Tests | Neu: `LadenAsync_MitGeplantemPrompt_ZeigtWartestellungStatus` (`TaskDetailViewModelTests_ZeitgesteuerterPrompt`) | Offen | — |
| 34 | Tests | Neu: `ScheduledPromptChanged_AktualisiertAnzeige` (`TaskDetailViewModelTests_ZeitgesteuerterPrompt`) | Offen | — |
| 34a | Tests | Neu: `CancelScheduledPromptCommand_EntferntPlanUndAnzeige` (`TaskDetailViewModelTests_ZeitgesteuerterPrompt`) | Offen | — |
| 35 | Tests | Anpassen: `AbschliessenAsync_ShouldCancelScheduledPrompt_WhenPromptGeplant` (`EntwicklungsprozessServiceTests`) auf async + Datensatz-Assertion | Offen | — |
| 36 | Tests | Anpassen: `ScmTaskCompletionMonitoringServiceTests` (IntegrationTests) — Signatur-/Verhaltensänderung prüfen | Offen | — |
| 37 | Tests | Übrige `PromptZeitVersandServiceTests` auf `CancelScheduledPromptAsync` (await) umstellen | Offen | — |
| 38 | Tests | `TaskDetailViewModelTestFactory` prüfen/anpassen (Service-Konstruktion, `ScheduledPromptChanged`-Abo) | Offen | — |
| 39 | E2E-Tests | `ZeitgesteuerterPrompt_NachPlanen_ZeigtWartestellungStatus_E2E` erweitern: Planen → Wartestellung + Storno-Button sichtbar → Detailansicht verlassen → Datensatz via `OpenTestDbContext` prüfen → wiederöffnen (`OpenFirstTask`) → Wartestellung weiterhin sichtbar → Storno klicken → Anzeige weg + Datensatz gelöscht | Offen | — |
| 39a | E2E-Tests | View-Hilfsmethode `CancelScheduledPrompt()` (ggf. + `WaitForScheduledPromptStatusGone`) in `src/Softwareschmiede.Tests/E2E/Views/TaskDetailView.cs` ergänzen (klickt `WartestellungAufheben`, wartet auf Verschwinden der Statusanzeige) | Offen | — |
| 40 | E2E-Tests | `SessionLimit_MarkerPauseProtokollUndUpdateSicherheit_E2E` als Regression im E2E-Lauf verifizieren (Sofortversand unverändert) | Offen | — |
