# Plan-Review

## Ergebnis

**Status:** Vollständig umgesetzt

## Umgesetzte Planelemente

- [x] `ZeitgesteuerterPrompt` (Entity, `src/Softwareschmiede/Domain/Entities/ZeitgesteuerterPrompt.cs`) — angelegt mit `Id`, `AufgabeId`, `PromptText`, `ZielZeitpunktUtc`, `ErstelltAmUtc`, `AktualisiertAmUtc`
- [x] `ZeitgesteuerterPromptStore` (Scoped Service, `src/Softwareschmiede/Application/Services/ZeitgesteuerterPromptStore.cs`) — angelegt mit `UpsertAsync` (Ersetzungsregel), `LoeschenAsync(aufgabeId)`, `GetAlleAsync`, `UpdateZielZeitpunktAsync`
- [x] Migration `AddZeitgesteuerterPrompt` (`src/Softwareschmiede/Migrations/20261107211018_AddZeitgesteuerterPrompt.cs` + Designer + ModelSnapshot) — Tabelle `ZeitgesteuertePrompts`, Unique-Index `IX_ZeitgesteuertePrompts_AufgabeId`, FK → `Aufgaben` Cascade
- [x] `DbSet<ZeitgesteuerterPrompt> ZeitgesteuertePrompts` + `OnModelCreating`-Mapping in `SoftwareschmiededDbContext` — PK, `PromptText` required, `UnixMillisConverter` für alle drei Zeitstempel, Unique-Index, `DeleteBehavior.Cascade`
- [x] Event `ScheduledPromptChanged` (`Action<Guid>`) in `PromptZeitVersandService` — gefeuert bei Planen, Storno, Versand, „wartet auf Session" und Pausenverschiebung
- [x] Methode `RestoreScheduledPromptsAsync()` in `PromptZeitVersandService` — lädt Datensätze, löscht verwaiste defensiv, zieht Zielzeit bei aktiver Pause aufs Pausenende vor (inkl. Datensatz-Update), armiert Timer für zukünftige Pläne, überfällige → „wartet auf Session"
- [x] `SchedulePromptAsync` erweitert — `UpsertDatensatzAsync` vor Timer-Armierung bei zukünftiger Zielzeit; Sofortversand persistiert nicht; fehlgeschlagener Sofortversand → „wartet auf Session"
- [x] `CancelScheduledPrompt` → `CancelScheduledPromptAsync` (async Task) — entfernt Eintrag + Timer und löscht Datensatz via `LoescheDatensatzAsync`; keine Aufrufe des alten Namens mehr vorhanden
- [x] `HandleTimerElapsedAsync` angepasst — Pause-Zweig legt Eintrag mit `TargetTime = Pausenende` zurück und ruft `UpdateZielZeitpunktDatensatzAsync`; Kein-Pause-Zweig versendet und markiert bei Fehlschlag „wartet auf Session"
- [x] `SendPromptAsync` angepasst — keine Session / `ObjectDisposedException` / sonstiger `WritePromptAsync`-Fehler → `false` (Plan bleibt, „wartet auf Session"); Erfolg → Datensatz löschen, `PromptSent` + `ScheduledPromptChanged`, dann `SchreibeVersandProtokollAsync`
- [x] Private Methode `SchreibeVersandProtokollAsync(aufgabeId, nachzustellung)` — `ProtokollTyp.SystemMeldung` via scoped `ProtokollService.AddEintragAsync`; bei Nachzustellung immer, bei Timer-/Sofortversand nur ohne `PromptSent`-Abonnenten; Fehlschlag → Warn-Log, bricht Versand nicht; ohne `scopeFactory` entfällt der Eintrag
- [x] `CliProcessStatusChanged`-Abonnement im Konstruktor — bei `Gestartet` → `ZustellungNachSessionStartAsync` (wartende/fällige Pläne zustellen, zukünftige unberührt, Fehlschlag → zurück in „wartet auf Session")
- [x] `ScheduledPromptEntry` um `WartetAufSession`-Flag und nullablen `ITimer?` erweitert
- [x] Klassenkommentar `PromptZeitVersandService` aktualisiert (Persistenz + Re-Arm + „wartet auf Session" dokumentiert)
- [x] `TaskDetailViewModel.Dispose` — `CancelScheduledPrompt`-Aufruf entfernt, `ScheduledPromptChanged`-Abo gelöst
- [x] `TaskDetailViewModel.OnCliProcessStatusChanged` — `CancelScheduledPrompt`-Aufruf und Leeren von `ScheduledPromptStatus`/`ScheduledPromptTimeDisplay` entfernt
- [x] `TaskDetailViewModel.LadenAsync` — `AktualisiereWartestellungsAnzeige()` befüllt Statusanzeige aus `GetScheduledPromptStatus`
- [x] `TaskDetailViewModel.OnScheduledPromptChanged` + `AktualisiereWartestellungsAnzeige` — eigene Aufgabe gefiltert, Dispatcher-Invoke, Anzeige aus Service-Zustand setzen/leeren
- [x] `CancelScheduledPromptCommand` (`AsyncRelayCommand`) in `TaskDetailViewModel` — ruft `CancelScheduledPromptAsync`; Anzeige-Leerung über `OnScheduledPromptChanged`
- [x] Button „Wartestellung aufheben" in `TaskDetailView.xaml` — `AutomationProperties.Name="WartestellungAufheben"`, `Command="{Binding CancelScheduledPromptCommand}"`, im sichtbarkeitsgesteuerten `ScheduledPromptStatus`-StackPanel
- [x] `EntwicklungsprozessService.AbschliessenCoordinatedAsync` — `await PromptZeitVersandService.CancelScheduledPromptAsync(aufgabeId)`
- [x] `App.xaml.cs` — `services.AddScoped<ZeitgesteuerterPromptStore>()`; `RestoreScheduledPromptsAsync()` nach `db.Database.MigrateAsync()` in `OnStartup`, try/catch mit Logging (Muster `CliProcessManager`)
- [x] Test-Hilfsmethode `CreateStoreProviderAsync` in `PromptZeitVersandServiceTests` — InMemory-DbContext + `ZeitgesteuerterPromptStore` + `AufgabeService` + `ProtokollService` (optional abschaltbar)
- [x] Alle 16 neuen `PromptZeitVersandServiceTests` — `SchedulePromptAsync_SchreibtDatensatz`, `SchedulePromptAsync_ErneutesPlanen_ErsetztDatensatz`, `Versand_LoeschtDatensatz`, `CancelScheduledPromptAsync_LoeschtDatensatz`, `Timer_OhneSession_BehaeltPlanUndWartetAufSession`, `Sendefehler_BehaeltPlanUndWartetAufSession`, `CliGestartet_MitWartendemPlan_ZustelltNach`, `Nachzustellung_SchreibtSystemMeldung`, `Versand_OhneAbonnent_SchreibtSystemMeldung`, `Versand_MitAbonnent_KeinProtokollEintrag`, `ProtokollEintrag_BeiGeloeschterAufgabe_BrichtVersandNicht`, `Pause_Verschiebung_AktualisiertDatensatz`, `Restore_ZukuenftigerPlan_ArmiertTimer`, `Restore_UeberfaelligerPlan_WartetAufSession`, `Restore_VerwaisterDatensatz_WirdGeloescht`, `Restore_NachNeustart_LiefertBeiSessionStartZu`
- [x] Alle neuen/angepassten `TaskDetailViewModelTests_ZeitgesteuerterPrompt`-Tests — `LadenAsync_MitGeplantemPrompt_ZeigtWartestellungStatus`, `ScheduledPromptChanged_AktualisiertAnzeige`, `CancelScheduledPromptCommand_EntferntPlanUndAnzeige`, `Dispose_LaesstGeplantenPlanBestehen` (Semantikumkehr), `OnCliProcessStatusChanged_CliGestoppt_BehaeltGeplantenPrompt` (Semantikumkehr)
- [x] Übrige `PromptZeitVersandServiceTests` auf `CancelScheduledPromptAsync` umgestellt (z. B. `CancelScheduledPromptAsync_EntferntGeplantenPrompt`)
- [x] `EntwicklungsprozessServiceTests.AbschliessenAsync_ShouldCancelScheduledPrompt_WhenPromptGeplant` — weiterhin gültig (ruft `AbschliessenAsync`, kein direkter Aufruf der umbenannten Methode; Datensatz-Assertion entfällt, da ohne `scopeFactory` keine Persistenz aktiv ist — „ggf."-Klausel des Plans)
- [x] `ScmTaskCompletionMonitoringServiceTests` geprüft — nutzt nur `GetScheduledPromptStatus` (Signatur unverändert), keine Anpassung nötig
- [x] `TaskDetailViewModelTestFactory` geprüft — `scopeFactory`-Parameter optional, Konstruktion kompatibel, keine Anpassung nötig
- [x] E2E-View-Hilfsmethode `CancelScheduledPrompt()` in `src/Softwareschmiede.Tests/E2E/Views/TaskDetailView.cs` — klickt `WartestellungAufheben`, wartet via `WaitUntilGone` auf Verschwinden von `ScheduledPromptStatus`
- [x] `ZeitgesteuerterPrompt_NachPlanen_ZeigtWartestellungStatus_E2E` erweitert — Planen → Statusanzeige → `GoBack`/`ProjectDetailView` → Datensatz via `OpenTestDbContext` → `OpenFirstTask` → Wartestellung sichtbar → `CancelScheduledPrompt()` → Datensatz gelöscht (DB-Check); Aufruf in `MainTest` auf `await` umgestellt
- [x] `SessionLimit_MarkerPauseProtokollUndUpdateSicherheit_E2E` unverändert als Regression im E2E-Lauf verankert (`MainTest.cs:36`)
- [x] `ScheduledPromptInfo` (Record) unverändert — `AufgabeId` + `PromptText` + `TargetTime`, wie geplant keine Änderung
- [x] `ProtokollService`, `MainWindowViewModel` unverändert — wie geplant keine Änderung

## Offene Aufgaben

Keine — alle Planelemente sind vollständig im Code umgesetzt.

## Hinweise

- Der Plan sah vor, `RestoreScheduledPromptsAsync` „im selben Scope-Block" wie `MigrateAsync` aufzurufen; die Umsetzung ruft ihn direkt danach über `_host.Services` auf (Singleton — korrekt auflösbar). Die fachliche Anforderung (Reihenfolge nach Migration, fehlertolerant) ist erfüllt; keine Lücke.
- Der E2E-Test (`ZeitgesteuerterPrompt_NachPlanen_ZeigtWartestellungStatus_E2E`) liegt in der `OsInterface`-/ConPTY-Lane und wurde im Rahmen dieses Reviews nicht ausgeführt — Nachweis hier auf Code-Ebene (Aufbau, Verdrahtung, `WaitUntilGone`-Helper in `BaseWindowView.cs:117` vorhanden).
