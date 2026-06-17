# Offene Aufgaben

Erstellt am: 2026-06-17
Abbruchgrund: Maximale Iterationsanzahl erreicht

Die folgenden Aufgaben konnten im automatisierten Zyklus nicht abgeschlossen werden
und müssen manuell oder in einem erneuten Lauf bearbeitet werden.

## Offene Planelemente

Keine. Der Plan ist vollständig umgesetzt.

## Code-Review-Befunde

- [ ] **[Hoch] Zombie-Handle in `src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs:119`** — Handle wird vor `process.Start()` in `_handles` eingetragen; wenn `Start()` wirft (nicht nur `false` zurückgibt), bleibt das Handle dauerhaft im Dictionary und blockiert zukünftige Starts derselben Aufgabe. Fix: `_handles.TryRemove(aufgabeId, out _)` in den `finally`-Block verschieben.
- [ ] **[Hoch] Abbruch überspringt Rollback in `src/Softwareschmiede/Application/Services/EntwicklungsprozessService.cs:224`** — `catch (OperationCanceledException) { throw; }` überspringt `RollbackStartAsync` vollständig. Nach einem Abbruch bleibt die Aufgabe im Status `Gestartet` mit Klon-Verzeichnis auf Disk. Fix: `RollbackStartAsync` mit `CancellationToken.None` aufrufen, bevor rethrown wird.
- [ ] **[Mittel] Möglicher Doppel-CLI-Start in `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs:287`** — `StartenAsync` startet CLI, ruft dann `LadenAsync` auf; wenn der Prozess extrem schnell abstürzt, sieht `LadenAsync` `IsRunning=false` + `Status=Gestartet` und löst `CliAutomatischNeustartenAsync` aus. Fix: In `LadenAsync` prüfen ob `_kiService.IsRunning` direkt nach dem Start noch aktiv ist, bevor Auto-Restart getriggert wird.
- [ ] **[Mittel] Fehlschlag nicht von Erfolg unterscheidbar in `src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs:103`** — `PersistFehlgeschlagenAsync` setzt Status `Beendet`, identisch zum Erfolgspfad. Fix: Eigenen Status `AufgabeStatus.Fehlgeschlagen` einführen oder ExitCode im Protokolleintrag vermerken.
- [ ] **[Niedrig] CancellationToken ungeprüft in `src/Softwareschmiede.App/Services/PluginSelectionDialogService.cs:14`** — Abgebrochene Aufrufe öffnen trotzdem den Modal-Dialog. Fix: `ct.ThrowIfCancellationRequested()` am Anfang der Methode.
- [ ] **[Niedrig] Toter `projektId`-Parameter durch 3 Schichten in `src/Softwareschmiede.App/Services/IDialogService.cs:15`** — `IDialogService`, `WpfDialogService` und `PluginSelectionDialogService` akzeptieren alle `Guid projektId`, aber `PluginSelectionDialogViewModel` erhält sie nicht. Fix: Parameter ans ViewModel durchreichen oder aus der Signatur entfernen.
- [ ] **[Niedrig] Plugin-Prefix der Aufgabe bei `SaveAsProjectDefault` nicht gesetzt in `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs:495`** — Wenn `dialogResult.SaveAsProjectDefault == true`, wird `UpdateAsync` übersprungen. Die Aufgabe folgt dann stillschweigend künftigen Projekt-Standard-Änderungen. Fix: `UpdateAsync` auch dann aufrufen, wenn Projekt-Standard gesetzt wird.
- [ ] **[Niedrig] Manuelle Factory mit 9 Args in `src/Softwareschmiede.App/App.xaml.cs:118`** — `EntwicklungsprozessService` per manueller Factory statt automatischer Konstruktorinjektion. Fix: Auf `AddScoped<EntwicklungsprozessService>()` umstellen.

## Fehlgeschlagene Tests

- [ ] `PluginManagerTests.GetSourceCodeManagementPlugins_ShouldNotDuplicatePlugins_WhenCalledMultipleTimes` — Nur 1 statt 2 Plugins geladen; vermutlich Env-Var-Leak aus `WpfTestBase` trotz Cleanup-Fix.
- [ ] `PluginManagerTests.GetSourceCodeManagementPlugins_ShouldLoadGitAndKiPlugins_WhenValidPluginDllsExist` — Gleiche Ursache.
- [ ] `AufgabeRecoveryServiceTests.RecoverManuellAsync_ShouldAllowExactlyOneSuccess_WhenTriggeredInParallel` — Pre-existing Race-Condition-Flake, unabhängig von diesem Feature.
- [ ] `WpfE2ETests.ProjektErstellen_UndNeueAufgabeAnlegen_E2E` — Timeout; UI-Timing-Problem beim Vollsuite-Lauf.
- [ ] `E2E_PluginWechsel.PluginAendernBeiLaufenderCli_StopptUndStartetMitNeuemPlugin_E2E` — Timeout beim Vollsuite-Lauf; in Isolation stabil.
- [ ] `E2E_PluginProjectDefault_NextTask.ZweiteAufgabeImProjekt_UebernimmtGespeichertenProjektStandardOhneDialog_E2E` — Credential-Store-Leak zwischen E2E-Tests (`LocalDirectoryPlugin.WorkspaceMode` wird OS-weit persistiert).
